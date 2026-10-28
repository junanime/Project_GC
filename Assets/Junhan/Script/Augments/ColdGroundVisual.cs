using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // One ground-only mesh: frosted floor, branching veins and small faceted crystals.
    // Gameplay radius and damage remain owned by Ver4GroundEffect.
    public sealed class ColdGroundVisual : MonoBehaviour
    {
        readonly List<Vector3> vertices = new List<Vector3>(800);
        readonly List<Color> colors = new List<Color>(800);
        readonly List<int> triangles = new List<int>(1000);
        Mesh mesh;
        Material material;
        float born, lifetime, nextDraw;
        public const float Radius = 1;
        static readonly Color Blue = new Color(.24f,.64f,.92f), Frost = new Color(.62f,.91f,1), White = new Color(.91f,.99f,1);

        public void Initialize(float duration)
        {
            born = Time.time; lifetime = Mathf.Max(.01f,duration);
            mesh = new Mesh { name = "Ice needle frosted floor" }; mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var visual = gameObject.AddComponent<MeshRenderer>();
            material = new Material(Resources.Load<Shader>("AugmentVfx/OpacitySprite"));
            material.mainTexture = Texture2D.whiteTexture; visual.sharedMaterial = material;
            GroundVisualSorting.Apply(visual, 3);
            AugmentVfxTag.Mark(gameObject);
            Draw(0);
        }
        void Update()
        {
            if(mesh == null || Time.time < nextDraw) return;
            nextDraw = Time.time + .05f; Draw(Time.time-born);
        }
        static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
        static Color Alpha(Color color,float alpha){color.a=alpha;return color;}
        void Triangle(Vector2 a,Vector2 b,Vector2 c,Color ca,Color cb,Color cc)
        {
            int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
            colors.Add(ca);colors.Add(cb);colors.Add(cc);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
        }
        void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {Triangle(a,b,c,color,color,color);Triangle(a,c,d,color,color,color);}
        void Vein(Vector2 a,Vector2 b,float width,Color color)
        {
            var d=(b-a).normalized;var side=new Vector2(-d.y,d.x)*width*.5f;
            Quad(a-side,a+side,b+side,b-side,color);
        }
        void Crystal(Vector2 p,float angle,float size,float alpha)
        {
            Vector2 up=Direction(angle)*size,side=new Vector2(-up.y,up.x)*.38f;
            Triangle(p-up*.5f,p-side,p+up,Alpha(Blue,alpha),Alpha(Frost,alpha),Alpha(White,alpha));
            Triangle(p-up*.5f,p+up,p+side,Alpha(Blue,alpha),Alpha(White,alpha),Alpha(Frost,alpha*.8f));
            Vein(p-up*.35f,p+up*.85f,.014f,Alpha(White,alpha*.8f));
        }
        void Draw(float age)
        {
            vertices.Clear();colors.Clear();triangles.Clear();
            float reveal=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.24f));
            float fade=Mathf.Clamp01((lifetime-age)/Mathf.Min(.35f,lifetime*.4f));
            float alpha=reveal*fade;
            float scale=Mathf.Lerp(.45f,1,reveal);
            // Open, soft irregular edge instead of a uniform wire circle.
            for(int i=0;i<48;i++)
            {
                float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;
                float ra=.96f+.025f*Mathf.Sin(a*7),rb=.96f+.025f*Mathf.Sin(b*7);
                Vector2 innerA=Direction(a)*.74f*scale,innerB=Direction(b)*.74f*scale;
                var center=Alpha(Blue,.16f*alpha);var inner=Alpha(Frost,.28f*alpha);var edge=Alpha(Frost,0);
                Triangle(Vector2.zero,innerA,innerB,center,inner,inner);
                Triangle(innerA,Direction(a)*ra*scale,Direction(b)*rb*scale,inner,edge,edge);
                Triangle(innerA,Direction(b)*rb*scale,innerB,inner,edge,inner);
            }
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f+.21f;
                Vector2 start=Direction(a)*.18f*scale,bend=Direction(a+.12f)*.48f*scale,end=Direction(a)*.81f*scale;
                Vein(start,bend,.022f,Alpha(Frost,.36f*alpha));Vein(bend,end,.015f,Alpha(White,.43f*alpha));
                Vein(bend,Direction(a+.25f)*.65f*scale,.012f,Alpha(Frost,.38f*alpha));
            }
            for(int i=0;i<9;i++)
            {
                float a=i*Mathf.PI*2/9+.12f;
                var p=Direction(a)*(.66f+.06f*Mathf.Sin(i*2.7f))*scale;
                float pulse=GamePreferences.Current.reducedMotion?1:.88f+.12f*Mathf.Sin(age*2.5f+i*1.7f);
                Crystal(p,a+.5f,.13f*scale,.73f*alpha*pulse);
                Crystal(p+Direction(a+.9f)*.07f,a+1.7f,.07f*scale,.5f*alpha);
                // Restrained four-point glints, not continuous flickering particles.
                float glint=GamePreferences.Current.reducedMotion?0:Mathf.Pow(Mathf.Max(0,Mathf.Sin(age*3+i*2.1f)),14)*alpha;
                if(glint>.02f)
                {
                    Vector2 q=p+Vector2.up*.14f;
                    Triangle(q-Vector2.up*.045f,q-Vector2.right*.015f,q+Vector2.up*.045f,Alpha(White,glint),Alpha(White,glint),Alpha(White,glint));
                    Triangle(q-Vector2.up*.045f,q+Vector2.up*.045f,q+Vector2.right*.015f,Alpha(White,glint),Alpha(White,glint),Alpha(White,glint));
                }
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
