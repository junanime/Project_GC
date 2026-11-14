using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Preview and live arrival use the same art, ground plane and coroutine.
    // The only Hurumi noodle is cut from the actual body and stays the same mesh throughout.
    public sealed class BuriedBossEmergence : MonoBehaviour
    {
        public const float Duration = 3.45f;
        public bool Playing { get; private set; }
        public float Progress { get; private set; }
        public Renderer PartRenderer { get; private set; }
        public bool HoleClosed { get; private set; }
        public Bounds PartBounds
        {
            get
            {
                if(kind!=FoodBossKind.Hurumi)return PartRenderer.bounds;
                var s=body.sprite;
                Vector3 center=part.TransformPoint(new Vector3((152.5f-s.pivot.x)/100,(s.rect.height-47-s.pivot.y)/100,0));
                return new Bounds(center,new Vector3(.85f*bodyScale,.94f*bodyScale,.02f));
            }
        }
        FoodBossKind kind;
        SpriteRenderer body;
        Transform part, hole;
        Material bodyMaterial, partMaterial, floorMaterial;
        Mesh noodleMesh, backMesh, frontMesh;
        float bodyScale, bodyHeight, noodleBase;
        Vector3 partRest;
        readonly List<Sprite> ownedSprites = new List<Sprite>();
        static readonly string[] Files = { "01-rolpangi", "02-denikani", "03-maribi", "04-tangdengi" };
        // Crops are in the approved source canvas. The last 10% is the separate old pink lip.
        static readonly Rect[] Crops = { new Rect(216,201,1178,486),new Rect(345,226,867,507),
            new Rect(614,259,699,294),new Rect(349,260,852,434) };
        static readonly Vector2[] Canvases = { new Vector2(1610,977),new Vector2(1510,1041),new Vector2(1926,816),new Vector2(1536,1024) };
        static readonly float[] PartWidths = { 2.25f, 1.45f, 2.25f, 2.0f, 1.1f };

        public void Initialize(FoodBossKind value)
        {
            kind = value;
            var shader = Resources.Load<Shader>("FoodBosses/BuriedEmergence");
            bodyMaterial = new Material(shader);
            partMaterial = new Material(shader);
            var sprite = FoodBossArt.Walk(kind, false, 0);
            bodyScale = FoodBossArt.WalkScale(kind, false);
            bodyHeight = sprite.bounds.size.y * bodyScale;
            body = new GameObject("Emerging original boss body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(transform, false); body.sprite = sprite;
            body.transform.localScale = Vector3.one * bodyScale; body.sortingOrder = 510;
            body.sharedMaterial = bodyMaterial;
            bodyMaterial.SetVector("_Cell", new Vector4(sprite.rect.x,sprite.rect.y,sprite.rect.width,sprite.rect.height));
            part = new GameObject(kind==FoodBossKind.Hurumi?"Existing body noodle · pinned root":"Exposed boss part").transform;
            part.SetParent(transform,false);
            if (kind == FoodBossKind.Hurumi)
            {
                // Use the exact center raised noodle that ArticulatedBody also articulates.
                noodleBase = (sprite.rect.height - 94 - sprite.pivot.y) / sprite.pixelsPerUnit;
                noodleMesh = BuildNoodle(sprite);
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = noodleMesh;
                var r = part.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=partMaterial;r.sortingOrder=511;PartRenderer=r;
                partMaterial.mainTexture=sprite.texture;
                partMaterial.SetVector("_Cell",new Vector4(sprite.rect.x,sprite.rect.y,sprite.rect.width,sprite.rect.height));
                partMaterial.SetFloat("_NoodleMode",2);partMaterial.SetFloat("_BaseY",noodleBase);partMaterial.SetFloat("_Length",.94f);
                part.localScale=Vector3.one*bodyScale;
                partRest=Vector3.down*noodleBase*bodyScale;
            }
            else
            {
                int n=(int)kind;var texture=Resources.Load<Texture2D>("BossSpawnParts/"+Files[n]);
                var crop=Crops[n];float sx=texture.width/Canvases[n].x,sy=texture.height/Canvases[n].y;
                var rect=new Rect(crop.x*sx,texture.height-(crop.y+crop.height)*sy,crop.width*sx,crop.height*sy);
                var s=Sprite.Create(texture,rect,new Vector2(.5f,0),100,0,SpriteMeshType.FullRect);ownedSprites.Add(s);
                var r=part.gameObject.AddComponent<SpriteRenderer>();r.sprite=s;r.sharedMaterial=partMaterial;r.sortingOrder=511;PartRenderer=r;
                part.localScale=Vector3.one*PartWidths[n]/s.bounds.size.x;
                partRest=Vector3.zero;
            }
            floorMaterial=new Material(Shader.Find("Sprites/Default"));
            hole=new GameObject("Elastic ground opening · no raster stretching").transform;hole.SetParent(transform,false);
            backMesh=BuildOpening(false);frontMesh=BuildOpening(true);
            Floor("Ground opening",backMesh,0,true);Floor("Front organic lip",frontMesh,512,false);
            ResetStandby();
        }

        void Floor(string label,Mesh mesh,int order,bool ground)
        {
            var go=new GameObject(label);go.transform.SetParent(hole,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=floorMaterial;
            if(ground)GroundVisualSorting.Apply(r,order);else r.sortingOrder=order;
        }

        public void ResetStandby()
        {
            if(body==null)return;
            Playing=false;Progress=0;HoleClosed=false;
            body.enabled=false;part.gameObject.SetActive(true);part.localPosition=partRest;part.localRotation=Quaternion.identity;
            partMaterial.SetVector("_Pull",Vector4.zero);partMaterial.SetFloat("_CutY",transform.position.y-.015f);
            hole.gameObject.SetActive(true);hole.localScale=new Vector3(kind==FoodBossKind.Hurumi?.40f:PartWidths[(int)kind]*.44f,.18f,1);
        }

        public IEnumerator Play(Character player)
        {
            Playing=true;
            Vector2 toward=player!=null?((Vector2)player.transform.position-(Vector2)transform.position).normalized:Vector2.left;
            for(float t=0;t<Duration;t+=Time.deltaTime)
            {
                if(FoodBossRuntime.Paused){yield return null;continue;}
                Sample(t,toward);yield return null;
            }
            Sample(Duration,toward);
            // This final pose exactly matches FoodBossVisual.Initialize: no scale or position jump.
            Playing=false;
        }

        void Sample(float t,Vector2 toward)
        {
            Progress=Mathf.Clamp01(t/Duration);
            float pull=Mathf.Sin(Mathf.PI*Mathf.Clamp01(t/.90f));
            float sink=Smooth((t-.9f)/.32f);
            float rise=Smooth((t-1.27f)/1.55f);
            float close=Smooth((t-2.75f)/.70f);
            HoleClosed=close>=1;
            hole.gameObject.SetActive(!HoleClosed);
            float small=kind==FoodBossKind.Hurumi?.40f:PartWidths[(int)kind]*.44f;
            hole.localScale=new Vector3(Mathf.Lerp(small,FoodBossArt.Widths[(int)kind]*.48f,Smooth((t-.8f)/.75f))*(1-close),Mathf.Lerp(.18f,.30f,rise)*(1-close),1);
            body.enabled=t>=1.27f;
            float lift=-bodyHeight*(1-rise);
            float arch=(kind==FoodBossKind.Maribi?.22f:kind==FoodBossKind.Hurumi?.14f:0)*Mathf.Sin(rise*Mathf.PI);
            body.transform.localPosition=Vector3.up*(lift+arch);
            body.transform.localRotation=Quaternion.Euler(0,0,(kind==FoodBossKind.Denikani?-3:kind==FoodBossKind.Tangdengi?2:0)*Mathf.Sin(rise*Mathf.PI));
            bodyMaterial.SetFloat("_CutY",transform.position.y-.015f);
            if(kind==FoodBossKind.Hurumi)
            {
                // No generated rope or replacement noodle during the pull. Root stays at the soil.
                part.gameObject.SetActive(rise<1);
                if(t<1.27f)
                {
                    part.localPosition=partRest-Vector3.up*(1.55f*sink);
                    partMaterial.SetVector("_Pull",new Vector4(toward.x*.48f*pull,.22f*pull,0,0));
                }
                else
                {
                    part.localPosition=body.transform.localPosition;
                    partMaterial.SetVector("_Pull",Vector4.zero);
                }
                bodyMaterial.SetFloat("_NoodleMode",rise<1?1:0);
            }
            else
            {
                part.gameObject.SetActive(t<1.27f);
                float height=PartRenderer is SpriteRenderer sr?sr.sprite.bounds.size.y*part.localScale.y:1;
                part.localPosition=partRest+Vector3.up*(.07f*pull-(height+.1f)*sink);
                part.localRotation=Quaternion.Euler(0,0,(kind==FoodBossKind.Maribi?7:3)*Mathf.Sin(t*17)*pull);
            }
        }
        static float Smooth(float u)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(u));

        static Mesh BuildNoodle(Sprite s)
        {
            const int cols=12,rows=24;
            var vertices=new Vector3[(cols+1)*(rows+1)];var uv=new Vector2[vertices.Length];var colors=new Color[vertices.Length];var triangles=new int[cols*rows*6];
            for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
            {
                int n=y*(cols+1)+x;float px=Mathf.Lerp(108,198,x/(float)cols),py=Mathf.Lerp(94,0,y/(float)rows);
                vertices[n]=new Vector3((px-s.pivot.x)/s.pixelsPerUnit,(s.rect.height-py-s.pivot.y)/s.pixelsPerUnit,0);
                uv[n]=new Vector2((s.rect.x+px)/s.texture.width,(s.rect.y+s.rect.height-py)/s.texture.height);colors[n]=Color.white;
                if(x<cols&&y<rows){int k=(y*cols+x)*6;triangles[k]=n;triangles[k+1]=n+cols+1;triangles[k+2]=n+1;triangles[k+3]=n+1;triangles[k+4]=n+cols+1;triangles[k+5]=n+cols+2;}
            }
            var mesh=new Mesh{name="One original noodle, pinned deformation grid"};mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateBounds();mesh.bounds=new Bounds(mesh.bounds.center,mesh.bounds.size+Vector3.one*2);return mesh;
        }

        static Mesh BuildOpening(bool front)
        {
            const int segments=64;
            float[] rings={0,.72f,.87f,1,1.10f};
            Color[] shades={new Color(.23f,.055f,.11f,.93f),new Color(.32f,.09f,.16f,.96f),new Color(.78f,.30f,.40f,1),new Color(1,.59f,.61f,1),new Color(.80f,.35f,.44f,0)};
            int count=front?32:segments;
            var vertices=new Vector3[(count+1)*rings.Length];var colors=new Color[vertices.Length];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            for(int r=0;r<rings.Length;r++)for(int i=0;i<=count;i++)
            {
                int n=r*(count+1)+i;float a=(front?Mathf.PI:0)+i/(float)count*(front?Mathf.PI:Mathf.PI*2);
                vertices[n]=new Vector3(Mathf.Cos(a)*rings[r],Mathf.Sin(a)*rings[r],0);colors[n]=shades[r];
                // Foreground contains only the lip, never an opaque rectangle over the boss.
                if(front&&r<2)colors[n]=Color.clear;
                if(r<rings.Length-1&&i<count){int b=n+count+1;triangles.AddRange(new[]{n,b,n+1,n+1,b,b+1});}
            }
            var mesh=new Mesh{name=front?"Front lip arcs":"Continuous shaded opening"};mesh.vertices=vertices;mesh.colors=colors;mesh.uv=uv;mesh.triangles=triangles.ToArray();mesh.RecalculateBounds();return mesh;
        }

        void OnDestroy()
        {
            foreach(var s in ownedSprites)if(s!=null)Destroy(s);
            if(noodleMesh!=null)Destroy(noodleMesh);if(backMesh!=null)Destroy(backMesh);if(frontMesh!=null)Destroy(frontMesh);
            if(bodyMaterial!=null)Destroy(bodyMaterial);if(partMaterial!=null)Destroy(partMaterial);if(floorMaterial!=null)Destroy(floorMaterial);
        }
    }
}
