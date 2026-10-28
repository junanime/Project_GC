using UnityEngine;
namespace Vampire
{
    // Tiny attached microbes use clean rounded strokes instead of a screen-filling aura.
    [DisallowMultipleComponent]
    public sealed class NeedleAttachmentVisual : MonoBehaviour
    {
        SpriteRenderer body;
        readonly LineRenderer[] outlines = new LineRenderer[3], cores = new LineRenderer[3], glints = new LineRenderer[3];
        Material material;
        float phase, strength = 1;
        public void Bind(SpriteRenderer target)
        {
            body = target; phase = 0; strength = 1;
            if (material == null)
            {
                material = new Material(Resources.Load<Shader>("AugmentVfx/OpacitySprite"));
                for (int i = 0; i < 3; i++)
                {
                    outlines[i] = Stroke("Microbe outline", new Color(.10f,.38f,.36f));
                    cores[i] = Stroke("Microbe mint body", new Color(.35f,.84f,.72f));
                    glints[i] = Stroke("Microbe highlight", new Color(.79f,1,.88f));
                }
            }
            AugmentVfxTag.Mark(gameObject);
            RefreshVisual(0);
        }
        LineRenderer Stroke(string name, Color color)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(transform,false);
            line.sharedMaterial = material; line.useWorldSpace = true; line.positionCount = 2;
            line.numCapVertices = 4; line.startColor = line.endColor = color;
            return line;
        }
        public void SetStrength(float value) { strength = Mathf.Clamp01(value); }
        public void RefreshVisual(float dt)
        {
            bool show = body != null && body.enabled && SyringeAugmentVfx.IsLiving(body);
            if (body != null && !(body.GetComponentInParent<Monster>() is Monster m && m.IsFieldRuntimeSuspended)) phase += dt;
            for (int i = 0; i < 3; i++)
            {
                if (outlines[i] == null) continue;
                outlines[i].enabled = cores[i].enabled = glints[i].enabled = show;
                if (!show) continue;
                var b = body.sprite.bounds;
                float sign = body.flipX ? -1 : 1;
                Vector3 local = b.center + new Vector3((i==0?-.24f:i==1?.24f:.14f)*b.size.x*sign, (i==2?.04f:-.25f)*b.size.y,0);
                Vector3 center = body.transform.TransformPoint(local);
                float size = Mathf.Clamp(Mathf.Min(body.bounds.size.x,body.bounds.size.y)*.075f,.04f,.20f);
                float angle = (i*67+25+Mathf.Sin(phase*3+i)*9)*Mathf.Deg2Rad;
                Vector3 axis = new Vector3(Mathf.Cos(angle),Mathf.Sin(angle))*size*.27f;
                Draw(outlines[i],center-axis,center+axis,size,body.sortingOrder+3,strength);
                Draw(cores[i],center-axis,center+axis,size*.72f,body.sortingOrder+4,strength);
                Draw(glints[i],center+Vector3.up*size*.16f-axis*.4f,center+Vector3.up*size*.16f+axis*.4f,size*.16f,body.sortingOrder+5,strength);
            }
        }
        void Draw(LineRenderer r,Vector3 a,Vector3 b,float width,int order,float alpha)
        {
            r.SetPosition(0,a);r.SetPosition(1,b);r.startWidth=r.endWidth=width;
            r.sortingLayerID=body.sortingLayerID;r.sortingOrder=order;
            var c=r.startColor;c.a=alpha*body.color.a;r.startColor=r.endColor=c;
        }
        void OnDisable(){body=null;foreach(var r in outlines)if(r!=null)r.enabled=false;foreach(var r in cores)if(r!=null)r.enabled=false;foreach(var r in glints)if(r!=null)r.enabled=false;}
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
