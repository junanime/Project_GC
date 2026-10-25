using UnityEngine;
namespace Vampire
{
    // Continuous curved strip with the approved body's own noodle cross-section.
    // Reuses buffers, and clips the strip at floor entry/exit planes without painting over the map.
    public sealed class FoodBossCurtainTube:MonoBehaviour
    {
        const int Count=64,Bands=6;
        Mesh mesh;Material material;MeshRenderer surface;LineRenderer outline,fill;
        Vector3[] vertices=new Vector3[Count*(Bands+1)];Vector2[] uv=new Vector2[Count*(Bands+1)];
        Color[] colors=new Color[Count*(Bands+1)];Vector3[] center=new Vector3[Count];
        float width;Texture2D atlas;
        static readonly AnimationCurve FullWidth=AnimationCurve.Linear(0,1,1,1);
        static readonly AnimationCurve GrowingWidth=new AnimationCurve(new Keyframe(0,1),new Keyframe(.87f,1),new Keyframe(1,.03f));
        public static FoodBossCurtainTube Create(Transform parent,float width,int order)
        {
            var n=new GameObject("Connected body-textured curtain noodle").AddComponent<FoodBossCurtainTube>();n.transform.SetParent(parent,false);n.width=width;
            n.outline=FoodBossEffects.Line(n.transform,new[]{Vector2.zero,Vector2.zero},width,new Color(.25f,.13f,.065f),false,order);
            n.fill=FoodBossEffects.Line(n.transform,new[]{Vector2.zero,Vector2.zero},width-.075f,new Color(1,.85f,.59f),false,order+1);
            n.atlas=Resources.Load<Texture2D>("FoodBosses/Walk4P1");n.mesh=new Mesh();n.mesh.MarkDynamic();
            n.material=new Material(Shader.Find("Sprites/Default"));n.material.mainTexture=n.atlas;
            var go=new GameObject("Original noodle texture");go.transform.SetParent(n.transform,false);go.AddComponent<MeshFilter>().sharedMesh=n.mesh;
            n.surface=go.AddComponent<MeshRenderer>();n.surface.sharedMaterial=n.material;n.surface.sortingOrder=order+2;
            var triangles=new int[(Count-1)*Bands*6];int k=0;
            for(int i=0;i<Count-1;i++)for(int j=0;j<Bands;j++){int a=i*(Bands+1)+j,b=a+Bands+1;triangles[k++]=a;triangles[k++]=b;triangles[k++]=a+1;triangles[k++]=a+1;triangles[k++]=b;triangles[k++]=b+1;}
            n.mesh.vertices=n.vertices;n.mesh.triangles=triangles;n.Hide();return n;
        }
        public void Hide(){outline.enabled=fill.enabled=surface.enabled=false;}
        public void Draw(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float reveal,float time,float opacity=1,bool floorClip=false)
        {
            reveal=Mathf.Clamp01(reveal);if(reveal<.008f||opacity<.01f){Hide();return;}
            outline.enabled=fill.enabled=surface.enabled=true;
            Vector2 ground=(d-a).normalized;float length=Vector2.Distance(a,d);
            for(int i=0;i<Count;i++){
                float u=i/(float)(Count-1)*reveal,v=1-u;
                center[i]=v*v*v*a+3*v*v*u*b+3*v*u*u*c+u*u*u*d;
            }
            for(int i=0;i<Count;i++){
                Vector2 tangent=center[Mathf.Min(Count-1,i+1)]-center[Mathf.Max(0,i-1)];tangent.Normalize();Vector2 normal=new Vector2(-tangent.y,tangent.x);
                float swell=1+.026f*Mathf.Sin(i*.24f-time*4);
                if(reveal<.995f)swell*=GrowingWidth.Evaluate(i/(float)(Count-1));
                for(int j=0;j<=Bands;j++){
                    float f=j/(float)Bands;int k=i*(Bands+1)+j;Vector2 pos=(Vector2)center[i]+normal*((f-.5f)*(width-.085f)*swell);
                    if(floorClip){float along=Vector2.Dot(pos-a,ground);pos+=ground*(Mathf.Clamp(along,0,length)-along);}
                    vertices[k]=pos;uv[k]=new Vector2((141+f*42)/atlas.width,(atlas.height-(78+Mathf.Sin(i*.12f-time)*2))/atlas.height);colors[k]=new Color(1,1,1,opacity);
                }
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;mesh.RecalculateBounds();
            foreach(var line in new[]{outline,fill}){line.positionCount=Count;line.SetPositions(center);line.widthCurve=reveal<.995f?GrowingWidth:FullWidth;line.widthMultiplier=line==outline?width:width-.075f;var color=line.startColor;color.a=opacity;line.startColor=line.endColor=color;}
            // Flat caps at ground punctures. No opaque floor-coloured occluder.
            outline.numCapVertices=fill.numCapVertices=floorClip?0:5;
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
