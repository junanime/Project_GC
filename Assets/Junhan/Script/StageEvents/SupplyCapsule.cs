using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // The same closed sprite and transform exist from descent through landing. Only the cap pivots when opened.
    public sealed class SupplyCapsule : InteractableEventObject
    {
        // Ashi's visible body is 330 px / 675 PPU × 1.25 scale. Exclude transparent frame padding.
        public const float BodyWidth=2.444444f; // Four visible Ashi body widths.
        public bool Landed { get; private set; }
        public bool Opened { get; private set; }
        public bool StartedDescent { get; private set; }
        public Transform Visual => bodyRoot;
        public SupplyRelicOrb RewardOrb { get; private set; }
        Transform bodyRoot,rightCap;
        SpriteRenderer closed;
        SpriteRenderer groundShadow;
        readonly List<SpriteRenderer> parachutes=new List<SpriteRenderer>();
        readonly List<Mesh> meshes=new List<Mesh>();
        Material splitMaterial;
        float fallAge,fallDuration,landAge;
        float groundY;
        MapMarker marker;
        protected override bool KeepVisibleAfterInteraction=>true;
        protected override bool InteractionAvailable=>Landed&&!Opened;
        protected override Component PromptOwner=>closed!=null?(Component)closed:this;
        bool Paused=>Time.timeScale<=0||levelManager==null||levelManager.IsRunFlowPaused||levelManager.IsLevelEnded||MiniStageRuntimeState.IsInsideMiniStage;
        public static SupplyCapsule Create(Vector2 position,LevelManager level)
        {
            var go=new GameObject("보급 캡슐");go.transform.position=position;
            var hit=go.AddComponent<BoxCollider2D>();hit.isTrigger=true;hit.size=new Vector2(BodyWidth+1.5f,2.6f);hit.enabled=false;
            var capsule=go.AddComponent<SupplyCapsule>();capsule.levelManager=level;capsule.Build();return capsule;
        }
        void Build()
        {
            bodyRoot=new GameObject("Capsule body").transform;bodyRoot.SetParent(transform,false);
            closed=new GameObject("Capsule shell").AddComponent<SpriteRenderer>();closed.transform.SetParent(bodyRoot,false);
            closed.sprite=SupplyCapsuleArt.Closed;closed.sortingLayerName="Monster Full";closed.sortingOrder=20;
            float scale=BodyWidth/Mathf.Max(.01f,closed.sprite.bounds.size.x);bodyRoot.localScale=Vector3.one*scale;
            groundY=-closed.sprite.bounds.size.y*scale*.43f;
            // The shadow belongs to the landing surface, never to the airborne body.
            groundShadow=new GameObject("Capsule ground shadow").AddComponent<SpriteRenderer>();
            groundShadow.transform.SetParent(transform,false);
            groundShadow.transform.localPosition=new Vector3(0,groundY,0);
            groundShadow.sprite=SupplyCapsuleArt.Droplet;
            groundShadow.sortingLayerName="Default";groundShadow.sortingOrder=-20;
            groundShadow.enabled=false;
            for(int i=0;i<2;i++)
            {
                var chute=new GameObject("Small parachute "+i).AddComponent<SpriteRenderer>();chute.transform.SetParent(bodyRoot,false);
                chute.sprite=SupplyCapsuleArt.Parachute;chute.sortingLayerName="Monster Full";chute.sortingOrder=21;
                // Sprite pivot is at the cord convergence, fixed to the rounded-end shoulder.
                chute.transform.localPosition=new Vector3((i==0?-.29f:.29f)*closed.sprite.bounds.size.x,closed.sprite.bounds.size.y*.29f,0);
                chute.transform.localScale=Vector3.one*(closed.sprite.bounds.size.x*.36f/chute.sprite.bounds.size.x);
                parachutes.Add(chute);
            }
            bodyRoot.gameObject.SetActive(false);
            marker=gameObject.AddComponent<MapMarker>();marker.Configure(MapMarkerKind.Event,"보급 캡슐",true);marker.enabled=false;
        }
        public void BeginDescent(float seconds)
        {
            if(StartedDescent)return;StartedDescent=true;fallDuration=Mathf.Max(.1f,seconds);
            bodyRoot.gameObject.SetActive(true);bodyRoot.localPosition=Vector3.up*7;
            groundShadow.enabled=true;UpdateGroundShadow(0);
        }
        void UpdateGroundShadow(float descent)
        {
            float progress=Mathf.SmoothStep(0,1,descent);
            groundShadow.color=new Color(.24f,.07f,.16f,Mathf.Lerp(.06f,.24f,progress));
            groundShadow.transform.localScale=new Vector3(BodyWidth*Mathf.Lerp(1.22f,.90f,progress),Mathf.Lerp(.68f,.40f,progress),1);
        }
        protected override void Update()
        {
            if(Paused){SetPromptVisible(false);return;}
            if(StartedDescent&&!Landed)
            {
                fallAge+=Time.deltaTime;float t=Mathf.Clamp01(fallAge/fallDuration);
                bodyRoot.localPosition=new Vector3(Mathf.Sin(t*Mathf.PI*4)*.13f*(1-t),7*(1-Mathf.SmoothStep(0,1,t)),0);
                UpdateGroundShadow(t);
                // Director owns the exact ten-second landing and notification boundary.
            }
            if(Landed&&landAge<1.1f)
            {
                landAge+=Time.deltaTime;float a=Mathf.Clamp01(landAge/.9f);
                foreach(var chute in parachutes)
                {
                    chute.color=new Color(1,1,1,1-a);
                    if(a>=1)chute.enabled=false;
                    chute.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(a*4)*12);
                    // Collapse the fabric gently after contact rather than leaving taut cords in the air.
                    float s=closed.sprite.bounds.size.x*.36f/chute.sprite.bounds.size.x;
                    chute.transform.localScale=new Vector3(s,s*Mathf.Lerp(1,.35f,a),1);
                }
            }
            base.Update();
        }
        public void Land()
        {
            if(Landed)return;if(!StartedDescent)BeginDescent(.1f);
            Landed=true;bodyRoot.localPosition=Vector3.zero;
            UpdateGroundShadow(1);
            GetComponent<Collider2D>().enabled=true;marker.enabled=true;
            StartCoroutine(LandingSplash());
        }
        IEnumerator LandingSplash()
        {
            var drops=new List<SpriteRenderer>();
            for(int i=0;i<10;i++)
            {
                var drop=new GameObject("Pink landing droplet").AddComponent<SpriteRenderer>();drop.transform.SetParent(transform,false);
                drop.sprite=SupplyCapsuleArt.Droplet;drop.color=new Color(1,.48f,.65f);drop.sortingLayerName="Monster Full";drop.sortingOrder=22;drop.transform.localScale=new Vector3(.09f,.14f,1);drops.Add(drop);
                drop.transform.localPosition=new Vector3((i%2==0?-1:1)*BodyWidth*.34f,groundY,0);
            }
            float age=0;
            while(age<.6f)
            {
                yield return null;if(Paused)continue;age+=Time.deltaTime;float t=Mathf.Clamp01(age/.6f);
                for(int i=0;i<drops.Count;i++)
                {
                    float side=i%2==0?-1:1;float spread=.15f+(i/2)*.15f;
                    drops[i].transform.localPosition=new Vector3(side*(BodyWidth*.34f+spread*t),groundY+Mathf.Sin(t*Mathf.PI)*(.2f+spread),0);
                    drops[i].color=new Color(1,.48f,.65f,1-t);
                }
            }
            foreach(var drop in drops)Destroy(drop.gameObject);
        }
        protected override bool ExecuteInteraction(Character player)
        {
            if(!Landed||Opened||Paused)return false;
            Opened=true;StartCoroutine(OpenRoutine(player));return true;
        }
        IEnumerator OpenRoutine(Character player)
        {
            MakeHingedCap();closed.enabled=false;
            float age=0;
            while(age<.65f)
            {
                yield return null;if(Paused)continue;age+=Time.deltaTime;
                rightCap.localRotation=Quaternion.Euler(0,0,-48*Mathf.SmoothStep(0,1,age/.65f));
            }
            RewardOrb=SupplyRelicOrb.Create(transform.position+new Vector3(.12f,.2f,0),player,levelManager,()=>marker.enabled=false);
        }
        void MakeHingedCap()
        {
            var sprite=closed.sprite;
            // Follow the curved painted seam. A straight diagonal cut leaves an ivory
            // sliver attached to the mint cap and tears the join when it rotates.
            var seam=new[]{new Vector2(.52f,0),new Vector2(.546f,.12f),new Vector2(.565f,.175f),
                new Vector2(.581f,.27f),new Vector2(.581f,.49f),new Vector2(.570f,.605f),
                new Vector2(.557f,.675f),new Vector2(.548f,.72f),new Vector2(.520f,.81f),
                new Vector2(.508f,.835f),new Vector2(.485f,.855f),new Vector2(.465f,.88f),new Vector2(.42f,1)};
            Vector2 pivot=seam[1];
            splitMaterial=new Material(Shader.Find("Sprites/Default"));splitMaterial.mainTexture=sprite.texture;
            MakePiece("Ivory half",seam,false,new Vector2(.5f,.5f),sprite);
            rightCap=MakePiece("Hinged mint half",seam,true,pivot,sprite);
        }
        Transform MakePiece(string name,Vector2[] seam,bool right,Vector2 pivot,Sprite sprite)
        {
            Vector2 size=sprite.bounds.size;
            var piece=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;piece.SetParent(bodyRoot,false);
            piece.localPosition=new Vector3((pivot.x-.5f)*size.x,(pivot.y-.5f)*size.y,0);
            var verts=new Vector3[seam.Length*2];var uv=new Vector2[verts.Length];
            var triangles=new int[(seam.Length-1)*6];
            for(int row=0;row<seam.Length;row++)
            {
                for(int side=0;side<2;side++)
                {
                    Vector2 point=new Vector2(right?(side==0?seam[row].x:1):(side==0?0:seam[row].x),seam[row].y);
                    int index=row*2+side;
                    verts[index]=new Vector3((point.x-pivot.x)*size.x,(point.y-pivot.y)*size.y,0);
                    uv[index]=new Vector2((sprite.rect.x+point.x*sprite.rect.width)/sprite.texture.width,(sprite.rect.y+point.y*sprite.rect.height)/sprite.texture.height);
                }
                if(row==seam.Length-1)continue;
                int n=row*2,t=row*6;
                triangles[t]=n;triangles[t+1]=n+1;triangles[t+2]=n+3;
                triangles[t+3]=n;triangles[t+4]=n+3;triangles[t+5]=n+2;
            }
            var mesh=new Mesh{name=name};mesh.vertices=verts;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();meshes.Add(mesh);
            piece.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=piece.GetComponent<MeshRenderer>();renderer.sharedMaterial=splitMaterial;renderer.sortingLayerName="Monster Full";renderer.sortingOrder=20;return piece;
        }
        void OnDestroy(){foreach(var mesh in meshes)Destroy(mesh);if(splitMaterial!=null)Destroy(splitMaterial);}
    }
}
