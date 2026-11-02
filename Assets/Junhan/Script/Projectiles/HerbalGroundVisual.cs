using UnityEngine;
namespace Vampire
{
    // World-space presentation only; the owning field retains all combat rules.
    public sealed class HerbalGroundVisual : MonoBehaviour
    {
        Mesh mesh; Material material; MeshRenderer drawing;
        float started, duration, radius; int style;
        readonly PhoenixRadianceLayer[] feathers = new PhoenixRadianceLayer[3];
        public static HerbalGroundVisual Create(Transform owner, Vector3 position, float radius, float seconds, int style)
        {
            var go = new GameObject(style == 0 ? "봉인흡진 흐름" : style == 1 ? "목침 새싹 회복빛" : "압착 잔광");
            go.transform.SetParent(owner, true); go.transform.position = position;
            var v = go.AddComponent<HerbalGroundVisual>(); v.radius = radius; v.duration = Mathf.Max(.1f, seconds); v.style = style; v.started = Time.time;
            v.mesh = new Mesh { name = "Herbal ground quad" };
            v.mesh.vertices = new[] { new Vector3(-1,-1),new Vector3(1,-1),new Vector3(-1,1),new Vector3(1,1) };
            v.mesh.uv = new[] { Vector2.zero,Vector2.right,Vector2.up,Vector2.one }; v.mesh.triangles = new[] {0,2,1,1,2,3}; v.mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = v.mesh;
            v.drawing = go.AddComponent<MeshRenderer>(); v.material = new Material(Resources.Load<Shader>("AugmentVfx/HerbalGround")); v.drawing.sharedMaterial = v.material;
            v.drawing.sortingLayerName = "GroundEffects"; v.drawing.sortingOrder = 24;
            v.drawing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (style == 0) for (int i=0;i<3;i++) v.feathers[i]=new PhoenixRadianceLayer(go.transform,"FeatherKit");
            AugmentVfxTag.Mark(go); v.Draw(); return v;
        }
        void LateUpdate() { if(Time.time-started>=duration){Destroy(gameObject);return;} Draw(); }
        void Draw()
        {
            float age=Time.time-started, enter=Mathf.SmoothStep(0,1,age/Mathf.Min(.3f,duration*.25f));
            float tail=Mathf.SmoothStep(0,1,(duration-age)/Mathf.Min(.35f,duration*.3f));
            bool calm=GamePreferences.Current.reducedMotion;
            float extent=style==0?Mathf.Lerp(.45f,1,tail):1;
            var s=transform.parent!=null?transform.parent.lossyScale:Vector3.one;
            transform.localScale=new Vector3(radius*1.16f*extent/Mathf.Max(.001f,Mathf.Abs(s.x)),radius*1.16f*extent/Mathf.Max(.001f,Mathf.Abs(s.y)),1);
            material.SetFloat("_Age",calm?0:age); material.SetFloat("_Life",age/duration);
            material.SetFloat("_Fade",enter*tail); material.SetFloat("_Style",style);
            for(int i=0;i<3 && style==0;i++)
            {
                float a=(i*120-(calm?0:age*45))*Mathf.Deg2Rad;
                float r=radius*(.78f-Mathf.Repeat(calm?i*.18f:age*.22f+i*.28f,.6f))*extent;
                var p=transform.position+new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.8f);
                feathers[i].Draw(p,Vector2.one*radius*.19f,a*Mathf.Rad2Deg+80,3,enter*tail*.8f,null,24,0,0,1,0,new Color(1,.88f,.52f));
                feathers[i].Renderer.sortingLayerName="GroundEffects";
            }
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
