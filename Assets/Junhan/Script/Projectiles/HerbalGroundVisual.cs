using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    // World-space presentation only; the owning field retains all combat rules.
    public sealed class HerbalGroundVisual : MonoBehaviour
    {
        static Mesh quad;
        static Material groundMaterial;
        static readonly int Age = Shader.PropertyToID("_Age"), Life = Shader.PropertyToID("_Life"),
            Fade = Shader.PropertyToID("_Fade"), Style = Shader.PropertyToID("_Style");
        static readonly Stack<HerbalGroundVisual> spareImpacts = new Stack<HerbalGroundVisual>();
        static Transform poolRoot;
        MaterialPropertyBlock properties;
        MeshRenderer drawing;
        float started, duration, radius; int style;
        readonly PhoenixRadianceLayer[] feathers = new PhoenixRadianceLayer[3];
        public static HerbalGroundVisual Create(Transform owner, Vector3 position, float radius, float seconds, int style)
        {
            if (style == 2 && poolRoot == null)
            {
                // Spare visuals belong to the field scene. Changing scenes disposes the pool.
                spareImpacts.Clear();
                poolRoot = new GameObject("Compression afterglow pool").transform;
            }
            HerbalGroundVisual effect = null;
            if (style == 2)
                while (effect == null && spareImpacts.Count > 0) effect = spareImpacts.Pop();
            if (effect == null)
            {
                var go = new GameObject(style == 0 ? "봉인흡진 흐름" : style == 1 ? "목침 새싹 회복빛" : "압착 잔광");
                effect = go.AddComponent<HerbalGroundVisual>();
                effect.SetUpDrawing(style);
            }
            effect.transform.SetParent(owner, true);
            effect.transform.SetPositionAndRotation(position, Quaternion.identity);
            effect.radius = radius;
            effect.duration = Mathf.Max(.1f, seconds);
            effect.started = Time.time;
            effect.gameObject.SetActive(true);
            effect.Draw();
            return effect;
        }

        void SetUpDrawing(int kind)
        {
            style = kind;
            if (quad == null)
            {
                quad = new Mesh { name = "Herbal ground shared quad" };
                quad.vertices = new[] { new Vector3(-1,-1), new Vector3(1,-1), new Vector3(-1,1), new Vector3(1,1) };
                quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                quad.triangles = new[] { 0,2,1,1,2,3 };
                quad.RecalculateBounds();
            }
            if (groundMaterial == null)
                groundMaterial = new Material(Resources.Load<Shader>("AugmentVfx/HerbalGround")) { name = "Herbal ground shared material" };
            gameObject.AddComponent<MeshFilter>().sharedMesh = quad;
            drawing = gameObject.AddComponent<MeshRenderer>();
            drawing.sharedMaterial = groundMaterial;
            drawing.sortingLayerName = "GroundEffects";
            drawing.sortingOrder = 24;
            drawing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            properties = new MaterialPropertyBlock();
            if (style == 0) for (int i=0;i<3;i++) feathers[i]=new PhoenixRadianceLayer(transform,"FeatherKit");
            AugmentVfxTag.Mark(gameObject);
        }

        void LateUpdate()
        {
            if (Time.time - started < duration) { Draw(); return; }
            // Only expired impact visuals enter the pool. Every hit is still drawn for its full lifetime.
            if (style == 2 && poolRoot != null && spareImpacts.Count < 256)
            {
                gameObject.SetActive(false);
                transform.SetParent(poolRoot, false);
                spareImpacts.Push(this);
            }
            else Destroy(gameObject);
        }
        void Draw()
        {
            float age=Time.time-started, enter=Mathf.SmoothStep(0,1,age/Mathf.Min(.3f,duration*.25f));
            float tail=Mathf.SmoothStep(0,1,(duration-age)/Mathf.Min(.35f,duration*.3f));
            bool calm=GamePreferences.Current.reducedMotion;
            float extent=style==0?Mathf.Lerp(.45f,1,tail):1;
            var s=transform.parent!=null?transform.parent.lossyScale:Vector3.one;
            transform.localScale=new Vector3(radius*1.16f*extent/Mathf.Max(.001f,Mathf.Abs(s.x)),radius*1.16f*extent/Mathf.Max(.001f,Mathf.Abs(s.y)),1);
            drawing.GetPropertyBlock(properties);
            properties.SetFloat(Age,calm?0:age); properties.SetFloat(Life,age/duration);
            properties.SetFloat(Fade,enter*tail); properties.SetFloat(Style,style);
            drawing.SetPropertyBlock(properties);
            for(int i=0;i<3 && style==0;i++)
            {
                float a=(i*120-(calm?0:age*45))*Mathf.Deg2Rad;
                float r=radius*(.78f-Mathf.Repeat(calm?i*.18f:age*.22f+i*.28f,.6f))*extent;
                var p=transform.position+new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.8f);
                feathers[i].Draw(p,Vector2.one*radius*.19f,a*Mathf.Rad2Deg+80,3,enter*tail*.8f,null,24,0,0,1,0,new Color(1,.88f,.52f));
                feathers[i].Renderer.sortingLayerName="GroundEffects";
            }
        }
    }
}
