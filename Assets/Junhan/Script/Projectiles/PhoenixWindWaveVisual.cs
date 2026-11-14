using UnityEngine;

namespace Vampire
{
    // The screen-reaching tail is cosmetic. Damage remains owned by the wave controller.
    public sealed class PhoenixWindWaveVisual : MonoBehaviour
    {
        public const float UnfoldSeconds = .32f;
        const int Ribbons = 12, Steps = 24;
        PhoenixRadianceLayer wings;
        Character owner;
        Mesh mesh;
        Material material;
        Vector3[] vertices = new Vector3[Ribbons * (Steps + 1) * 2];
        Color[] colors = new Color[Ribbons * (Steps + 1) * 2];
        float age, speed, reach;
        public float Radius { get; private set; }
        public float ScreenReach => reach;

        public static PhoenixWindWaveVisual Play(Character owner, float combatRadius, float combatDuration)
        {
            var go = new GameObject("봉황파동 무지개 날개와 바람");
            go.transform.SetParent(owner.transform, true);
            var v = go.AddComponent<PhoenixWindWaveVisual>();
            v.owner = owner; v.speed = (combatRadius - .15f) / combatDuration;
            v.reach = combatRadius;
            v.wings = new PhoenixRadianceLayer(go.transform, "CloneWings");
            var wind = new GameObject("바깥으로 흐르는 바람", typeof(MeshFilter), typeof(MeshRenderer));
            wind.transform.SetParent(go.transform, false);
            v.mesh = new Mesh { name = "Phoenix curved wind ribbons" }; v.mesh.MarkDynamic();
            var indices = new int[Ribbons * Steps * 6]; int k = 0;
            for (int j = 0; j < Ribbons; j++) for (int s = 0; s < Steps; s++)
            {
                int a = (j * (Steps + 1) + s) * 2;
                indices[k++] = a; indices[k++] = a+2; indices[k++] = a+1;
                indices[k++] = a+1; indices[k++] = a+2; indices[k++] = a+3;
            }
            v.mesh.vertices = v.vertices; v.mesh.colors = v.colors; v.mesh.triangles = indices;
            wind.GetComponent<MeshFilter>().sharedMesh = v.mesh;
            v.material = new Material(Resources.Load<Shader>("AugmentVfx/PhoenixWind"));
            var renderer = wind.GetComponent<MeshRenderer>(); renderer.sharedMaterial = v.material;
            renderer.sortingLayerName = "Monster Full"; renderer.sortingOrder = -5;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            AugmentVfxTag.Mark(go); v.Draw(); return v;
        }
        void LateUpdate()
        {
            if (owner == null || owner.CurrentHealth <= 0 || !owner.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
            age += Time.deltaTime; Draw();
            if (Radius > reach + 2.5f) Destroy(gameObject);
        }
        void Draw()
        {
            var origin = owner.transform.position;
            transform.position = origin; transform.rotation = Quaternion.identity;
            var scale = transform.parent.lossyScale;
            transform.localScale = new Vector3(1 / Mathf.Max(.001f, Mathf.Abs(scale.x)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1);
            var camera = Camera.main;
            if (camera != null && camera.orthographic)
            {
                float h = camera.orthographicSize, w = h * camera.aspect;
                var offset = camera.transform.position - origin;
                // Include the farthest corner, not just the short screen edge; works in portrait too.
                reach = Mathf.Max(reach, new Vector2(Mathf.Abs(offset.x)+w, Mathf.Abs(offset.y)+h).magnitude + .5f);
            }
            float open = Mathf.SmoothStep(0, 1, age / UnfoldSeconds);
            float flap = Mathf.Clamp01((age-UnfoldSeconds)/.38f);
            float wingAlpha = open * (1-Mathf.SmoothStep(0,1,(age-.8f)/.4f));
            float pose = age < UnfoldSeconds ? Mathf.Lerp(0,3,open) : Mathf.Lerp(3,9,flap);
            bool calm = GamePreferences.Current.reducedMotion;
            wings.Draw(origin + Vector3.up * .2f, new Vector2(1.8f,1.8f) * Mathf.Lerp(.65f,1,open), 0,
                calm ? 2 : pose, wingAlpha, SyringeAugmentVfx.FindTarget(owner), -3, 0);
            Radius = .15f + Mathf.Max(0,age-UnfoldSeconds) * speed;
            float fade = 1-Mathf.SmoothStep(0,1,(Radius-reach)/2f);
            float appear = Mathf.SmoothStep(0,1,(age-UnfoldSeconds)/.12f);
            for (int j=0;j<Ribbons;j++)
            {
                int band=j/6; float heading=(j%6)*Mathf.PI/3+band*.16f;
                float radius=Mathf.Max(.05f,Radius-band*.8f);
                float arc=.48f+band*.06f;
                var tint=Color.HSVToRGB(Mathf.Repeat(j/6f+band*.16f,1),.62f,1);
                for(int s=0;s<=Steps;s++)
                {
                    float u=s/(float)Steps, taper=Mathf.Sin(u*Mathf.PI);
                    float angle=heading+(u-.5f)*arc+(calm?0:age*.12f);
                    float curve=radius-.5f*(1-u)*(1-u);
                    float width=(.17f+band*.025f)*taper;
                    float alpha=taper*appear*fade*(band==0?.72f:.42f);
                    for(int side=0;side<2;side++)
                    {
                        int n=(j*(Steps+1)+s)*2+side;
                        float r=curve+(side==0?-width:width);
                        vertices[n]=new Vector3(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r,0);
                        var edge=Color.Lerp(tint,new Color(1,1,.92f),.48f);
                        colors[n]=side==0?new Color(tint.r,tint.g,tint.b,alpha*.42f):new Color(edge.r,edge.g,edge.b,alpha);
                    }
                }
            }
            mesh.vertices=vertices; mesh.colors=colors; mesh.RecalculateBounds();
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
