using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public sealed class AcidLakeVisual : MonoBehaviour
    {
        public const float RainPerSecond = 145f / 3f;
        AcidLakeEvent lake;
        Mesh mesh;
        Material material;
        MeshRenderer water;
        public Renderer SurfaceRenderer => water;
        Sprite[,] frames;
        SpriteRenderer[] drops, impacts;
        readonly List<Rain> rain = new List<Rain>();
        readonly List<Impact> splashes = new List<Impact>();
        readonly Stack<SpriteRenderer> freeDrops = new Stack<SpriteRenderer>(), freeImpacts = new Stack<SpriteRenderer>();
        struct Rain { public SpriteRenderer art; public Vector2 ground; public float age, duration, height, size; }
        struct Impact { public SpriteRenderer art; public Vector2 point; public float age, size; }
        float emission;
        bool visible;
        public static AcidLakeVisual Create(AcidLakeEvent owner)
        {
            var go = new GameObject("위산 호수 · 월드 상단 연출");
            var v = go.AddComponent<AcidLakeVisual>(); v.lake = owner; v.Build(); return v;
        }
        void Build()
        {
            var rect = lake.Map.WorldRect;
            var surface = new GameObject("Continuous acid surface", typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(transform, false);
            mesh = new Mesh { name = "Acid lake fixed world bounds" };
            mesh.vertices = new[] { new Vector3(rect.xMin,rect.yMin),new Vector3(rect.xMax,rect.yMin),new Vector3(rect.xMax,rect.yMax),new Vector3(rect.xMin,rect.yMax) };
            mesh.triangles = new[] {0,2,1,0,3,2}; mesh.RecalculateBounds();
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Resources.Load<Shader>("AcidLake/Surface"));
            material.SetTexture("_MainTex", Resources.Load<Texture2D>("AcidLake/Water"));
            material.SetTexture("_DistanceTex", lake.Map.BoundaryDistanceTexture);
            material.SetVector("_MapRect", new Vector4(rect.xMin, rect.yMin, rect.width, rect.height));
            material.SetFloat("_PixelSize", lake.Map.Definition.pixelWorldSize * .25f);
            water = surface.GetComponent<MeshRenderer>(); water.sharedMaterial = material;
            water.sortingLayerName = "Monster Full"; water.sortingOrder = 32000;
            water.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; water.receiveShadows = false;
            var atlas = Resources.Load<Texture2D>("AcidLake/Fx");
            frames = new Sprite[4,6]; int w = atlas.width / 6, h = atlas.height / 4;
            for (int row=0;row<4;row++) for(int col=0;col<6;col++)
                frames[row,col] = Sprite.Create(atlas, new Rect(col*w, atlas.height-(row+1)*h, w, h), Vector2.one*.5f, w);
            drops = new SpriteRenderer[90]; impacts = new SpriteRenderer[80];
            for(int i=0;i<drops.Length;i++) { drops[i]=Make("Acid rain",32002); freeDrops.Push(drops[i]); }
            for(int i=0;i<impacts.Length;i++) { impacts[i]=Make("Lake ripple",32001); freeImpacts.Push(impacts[i]); }
            SetVisible(false);
        }
        SpriteRenderer Make(string name,int order)
        {
            var sr=new GameObject(name).AddComponent<SpriteRenderer>(); sr.transform.SetParent(transform,false);
            sr.sortingLayerName="Monster Full";sr.sortingOrder=order;sr.enabled=false;return sr;
        }
        public void SetVisible(bool value)
        {
            visible=value;
            if(water!=null)water.enabled=value && lake.Flooding;
            foreach(var d in rain)d.art.enabled=value;
            foreach(var s in splashes)s.art.enabled=value;
        }
        public void Tick(float dt)
        {
            material.SetFloat("_SurfaceY",lake.SurfaceY);
            material.SetFloat("_Clock",lake.VisualTime);
            if(!visible || dt<=0)return;
            var camera=Camera.main;
            if(camera==null)return;
            float height=camera.orthographicSize*2, width=height*camera.aspect;
            emission+=dt*RainPerSecond;
            while(emission>=1)
            {
                emission--;
                if(freeDrops.Count==0)continue;
                var art=freeDrops.Pop();art.enabled=true;art.sprite=frames[0,Random.Range(0,6)];
                float size=Random.Range(.35f,.66f);art.transform.localScale=Vector3.one*size;
                art.color=new Color(1,1,1,Random.Range(.48f,.8f));
                Vector2 ground=(Vector2)camera.transform.position + new Vector2(Random.Range(-width*.52f,width*.52f),Random.Range(-height*.5f,height*.5f));
                rain.Add(new Rain{art=art,ground=ground,duration=Random.Range(.42f,.7f),height=height*.85f,size=size});
            }
            for(int i=rain.Count-1;i>=0;i--)
            {
                var d=rain[i]; d.age+=dt;
                if(d.age>=d.duration)
                {
                    if(lake.IsSubmerged(d.ground))Splash(d.ground,d.size*1.5f);
                    d.art.enabled=false;freeDrops.Push(d.art);rain.RemoveAt(i);continue;
                }
                d.art.transform.position=d.ground+Vector2.up*d.height*(1-d.age/d.duration);rain[i]=d;
            }
            for(int i=splashes.Count-1;i>=0;i--)
            {
                var s=splashes[i];s.age+=dt;
                if(s.age>=.85f || !lake.IsSubmerged(s.point))
                {s.art.enabled=false;freeImpacts.Push(s.art);splashes.RemoveAt(i);continue;}
                bool ring=s.age>.38f;float progress=ring?(s.age-.38f)/.47f:s.age/.38f;
                s.art.sprite=frames[ring?3:1,Mathf.Clamp((int)(progress*6),0,5)];
                s.art.color=new Color(1,1,1,ring?1-progress:.9f);
                s.art.transform.localScale=Vector3.one*s.size*(ring?1.3f:1);splashes[i]=s;
            }
        }
        void Splash(Vector2 point,float size)
        {
            if(freeImpacts.Count==0)return;
            var art=freeImpacts.Pop();art.enabled=true;art.transform.position=point;
            splashes.Add(new Impact{art=art,point=point,size=size});
        }
        void OnDestroy()
        {
            if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);
            if(frames!=null)foreach(var sprite in frames)if(sprite!=null)Destroy(sprite);
        }
    }
}
