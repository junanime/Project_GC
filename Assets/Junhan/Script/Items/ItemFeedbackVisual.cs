using System.Collections.Generic;
using TMPro;
using UnityEngine;
namespace Vampire
{
    // Small procedural ink shapes reuse a mesh and lists; no item-card sprites or collisions.
    public sealed class ItemFeedbackVisual : MonoBehaviour
    {
        Mesh mesh;
        MeshRenderer drawing;
        TextMeshPro caption;
        Material captionMaterial;
        MaterialPropertyBlock tint;
        readonly List<Vector3> vertices = new List<Vector3>(512);
        readonly List<Color> colors = new List<Color>(512);
        readonly List<int> triangles = new List<int>(1536);
        Transform follow;
        bool following;
        float age, duration, radius;
        int stacks;
        public ItemFeedbackKind Kind { get; private set; }
        public int Lease { get; private set; }
        static readonly Color Ink = new Color(.13f, .10f, .18f, .95f);
        static readonly Color Gold = new Color(1f, .78f, .23f, .95f);
        static readonly Color Red = new Color(.96f, .32f, .22f, .9f);
        static readonly Color Blue = new Color(.25f, .82f, 1f, .9f);
        static readonly Color Paper = new Color(1, .96f, .81f, .9f);


        bool themed, sustained, acquired;
        ItemEffectTheme theme;
        int variant;
        public void ConfigureTheme(ItemEffectTheme value,bool hold,bool acquisition,string text,int seed)
        {
            themed=true;theme=value;sustained=hold;acquired=acquisition;variant=seed;
            bool ground=!acquired && (theme.shape==ItemEffectShape.Acid || theme.shape==ItemEffectShape.Ember ||
                theme.shape==ItemEffectShape.Vacuum || theme.shape==ItemEffectShape.Portal || theme.shape==ItemEffectShape.Drum);
            drawing.sortingLayerName=ground?"GroundEffects":"Monster Full";drawing.sortingOrder=ground?20:35;
            SetCaption(text);DrawTheme(0);
        }
        void DrawTheme(float t)
        {
            bool calm=GamePreferences.Current.reducedMotion;
            float motion=calm?0:age;
            float alpha=sustained?Mathf.Clamp01((1-t)*10):Mathf.Min(1,t*18+.15f)*Mathf.Clamp01((1-t)*2.5f);
            float scale=acquired?Mathf.Lerp(.7f,1,Mathf.Min(1,t*4)):sustained?1:Mathf.Lerp(.35f,1,Mathf.Sqrt(t));
            if(calm)scale=1;
            transform.localScale=Vector3.one*radius*scale;
            tint.SetColor("_Color",new Color(1,1,1,alpha*GamePreferences.Current.augmentVfxOpacity));drawing.SetPropertyBlock(tint);
            vertices.Clear();colors.Clear();triangles.Clear();
            DrawShape(motion,t);
            if(acquired)
            {
                for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5+variant*.19f;var p=Polar(a)*(.82f+t*.3f);Diamond(p,.04f,Paper);}
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            if(caption!=null && caption.gameObject.activeSelf)
            {
                caption.transform.localScale=Vector3.one/(radius*scale);
                caption.transform.localPosition=Vector3.up*(Mathf.Min(radius,.9f)+.3f+(calm?0:t*.2f))/(radius*scale);
                caption.color=new Color(theme.color.r,theme.color.g,theme.color.b,alpha*GamePreferences.Current.augmentVfxOpacity);
            }
        }
        void DrawShape(float time,float progress)
        {
            Color c=theme.color,soft=c;soft.a=.18f;
            switch(theme.shape)
            {
                case ItemEffectShape.Strike: case ItemEffectShape.Counter:
                    for(int i=0;i<9;i++){var d=Polar(i*Mathf.PI*2/9);Triangle(d*.25f+Vector2.Perpendicular(d)*.07f,d*.92f,d*.25f-Vector2.Perpendicular(d)*.07f,c);}
                    Ring(.3f,.045f,Paper);break;
                case ItemEffectShape.Heal:
                    Stroke(new Vector2(-.22f,.15f),new Vector2(.22f,.15f),.16f,c);Stroke(new Vector2(0,-.08f),new Vector2(0,.38f),.16f,c);
                    for(int i=0;i<5;i++)Leaf(Polar(i*1.256f+time*.5f)*.68f,i*1.256f,c);
                    Arc(.8f,-2.8f,-.35f,.035f,Paper);break;
                case ItemEffectShape.Wind: case ItemEffectShape.Feather: case ItemEffectShape.Mint:
                    for(int i=0;i<3;i++){float a=i*2.094f+time*2;Arc(.45f+i*.2f,a,a+1.6f,.04f,c);if(theme.shape==ItemEffectShape.Feather)Leaf(Polar(a+1.6f)*(.45f+i*.2f),a,c);}
                    if(theme.shape==ItemEffectShape.Mint){Ring(.96f,.035f,c);Ring(.72f,.018f,Paper);}break;
                case ItemEffectShape.Focus:
                    for(int i=0;i<4;i++)Arc(.65f,i*Mathf.PI/2+.15f,i*Mathf.PI/2+.65f,.07f,c);
                    Diamond(Vector2.zero,.16f,Paper);break;
                case ItemEffectShape.Magnet:
                    for(int i=0;i<6;i++){var d=Polar(i*Mathf.PI/3);Diamond(d*(.9f-Mathf.Repeat(time*.7f, .55f)),.07f,c);}
                    Arc(.4f,.15f,2.99f,.06f,c);Arc(.4f,3.3f,6.1f,.06f,Paper);break;
                case ItemEffectShape.Shield: case ItemEffectShape.Memory:
                    Vector2[] shield={new Vector2(-.6f,.5f),new Vector2(0,.7f),new Vector2(.6f,.5f),new Vector2(.5f,-.25f),new Vector2(0,-.7f),new Vector2(-.5f,-.25f)};
                    for(int i=0;i<6;i++){Triangle(Vector2.zero,shield[i],shield[(i+1)%6],soft);Stroke(shield[i],shield[(i+1)%6],.065f,c);}
                    if(theme.shape==ItemEffectShape.Memory)for(int i=0;i<3;i++)Diamond(new Vector2((i-1)*.22f,.12f),.07f,Paper);
                    else {Stroke(new Vector2(-.22f,.05f),new Vector2(-.04f,-.15f),.07f,Paper);Stroke(new Vector2(-.04f,-.15f),new Vector2(.28f,.25f),.07f,Paper);}break;
                case ItemEffectShape.Honey: case ItemEffectShape.Acid:
                    for(int i=0;i<7;i++){var p=Polar(i*2.4f)*(.3f+.05f*i);Disc(p,.26f,soft);Arc(.18f,p.x+i,p.x+i+1.5f,.02f,c);}
                    Ring(.8f,.035f,c);
                    for(int i=0;i<4;i++){float a=i*1.57f;var p=Polar(a)*.45f+Vector2.up*Mathf.Repeat(time*.2f+i*.13f,.25f);Disc(p,.065f,c);Disc(p+new Vector2(-.02f,.02f),.02f,Paper);}break;
                case ItemEffectShape.Ember:
                    Disc(Vector2.zero,.86f,soft);
                    for(int i=0;i<5;i++){var p=Polar(i*1.256f)*.53f;float s=.7f+.12f*Mathf.Sin(time*7+i);Flame(p,s,c);Flame(p,s*.5f,Paper);}break;
                case ItemEffectShape.Lure: case ItemEffectShape.Culture: case ItemEffectShape.Seed: case ItemEffectShape.Orb:
                    Disc(Vector2.zero,.32f,Ink);Disc(Vector2.zero,.285f,c);Disc(new Vector2(-.07f,.08f),.07f,Paper);
                    if(theme.shape==ItemEffectShape.Orb){Arc(.5f,time*2,time*2+4,.04f,c);Arc(.7f,-time*2,-time*2+2,.025f,Paper);}
                    else {Leaf(new Vector2(-.2f,.39f),-.5f,c);Leaf(new Vector2(.18f,.43f),.6f,c);Disc(new Vector2(-.09f,-.03f),.033f,Ink);Disc(new Vector2(.09f,-.03f),.033f,Ink);
                        if(theme.shape==ItemEffectShape.Lure)for(int i=0;i<3;i++)Arc(.5f+i*.15f,-1.1f+time,1.1f+time,.025f,c);}
                    break;
                case ItemEffectShape.Night: case ItemEffectShape.Dawn: case ItemEffectShape.Clock:
                    Ring(.94f,.035f,c);Ring(.78f,.018f,Paper);
                    for(int i=0;i<12;i++){var d=Polar(i*Mathf.PI/6);Stroke(d*.82f,d*.9f,.035f,c);}
                    if(theme.shape==ItemEffectShape.Clock){Stroke(Vector2.zero,Polar(1.57f-time*2)*.55f,.04f,c);Stroke(Vector2.zero,Polar(-time)*.3f,.06f,Paper);}
                    else if(theme.shape==ItemEffectShape.Dawn)for(int i=0;i<8;i++)Leaf(Polar(i*Mathf.PI/4)*.5f,i*Mathf.PI/4,c);
                    else Arc(.4f,1,5,.12f,c);break;
                case ItemEffectShape.Chime: case ItemEffectShape.Drum:
                    for(int i=0;i<3;i++)Ring(.4f+i*.27f,.035f-i*.007f,i==1?Paper:c);
                    for(int i=0;i<4;i++)Diamond(Polar(i*Mathf.PI/2+.4f)*.74f,.07f,c);break;
                case ItemEffectShape.Ferment: case ItemEffectShape.Poison: case ItemEffectShape.Overflow:
                    for(int i=0;i<6;i++){var p=Polar(i*2.4f+time*.4f)*(.2f+i*.08f);float r=.09f+.025f*(i%3);Disc(p,r,c);Disc(p+Vector2.up*r*.4f,r*.25f,Paper);}
                    if(theme.shape==ItemEffectShape.Overflow)Arc(.7f,3.2f,6.2f,.08f,c);break;
                case ItemEffectShape.Crystal:
                    for(int i=0;i<8;i++){var d=Polar(i*Mathf.PI/4);Diamond(d*.62f,.14f,c);Stroke(d*.2f,d*.4f,.025f,Paper);}break;
                case ItemEffectShape.Vacuum: case ItemEffectShape.Portal:
                    for(int j=0;j<3;j++)for(int i=0;i<24;i++){float a=i*.16f+j*2.094f-time*3,b=a+.16f;float r=.1f+i*.035f;Stroke(Polar(a)*r,Polar(b)*(r+.035f),.04f,c);}
                    if(theme.shape==ItemEffectShape.Portal){Ring(.95f,.035f,Paper);for(int i=0;i<6;i++)Diamond(Polar(i*Mathf.PI/3)*.95f,.05f,c);}break;
                case ItemEffectShape.Seal:
                    Ring(.96f,.04f,c);Ring(.8f,.025f,Paper);
                    for(int i=0;i<6;i++){var d=Polar(i*Mathf.PI/3);Stroke(d*.8f,d*.96f,.08f,c);}
                    Stroke(new Vector2(-.15f,-.25f),new Vector2(-.15f,.25f),.1f,c);Stroke(new Vector2(.15f,-.25f),new Vector2(.15f,.25f),.1f,c);break;
                case ItemEffectShape.Ghost:
                    Disc(new Vector2(0,.12f),.3f,c);Polygon(new Vector2(0,-.07f),SpiritOutline,c);
                    Disc(new Vector2(-.1f,.15f),.035f,Ink);Disc(new Vector2(.1f,.15f),.035f,Ink);Diamond(new Vector2(0,.32f),.075f,Paper);break;
                case ItemEffectShape.Experience: case ItemEffectShape.Coin:
                    for(int i=0;i<5;i++){var p=Polar(i*1.256f+time*.7f)*.6f;if(theme.shape==ItemEffectShape.Coin){Disc(p,.12f,c);Stroke(p+Vector2.down*.06f,p+Vector2.up*.06f,.025f,Paper);}else Diamond(p,.12f,c);}
                    Arc(.3f,0,Mathf.PI*1.7f,.035f,c);break;
                case ItemEffectShape.Afterimage:
                    for(int i=0;i<3;i++){float x=-.2f*i;Arc(.35f+i*.12f,2.3f+time*.2f,4.1f+time*.2f,.06f,c);}
                    Leaf(new Vector2(.16f,0),-.3f,Paper);break;
                case ItemEffectShape.Dragon:
                    // Expanding forward crescent, oriented by the actual cone direction.
                    for(int i=0;i<3;i++)Arc(.45f+i*.25f,-.86f,.86f,.08f-i*.018f,i==1?Paper:c);
                    for(int i=0;i<5;i++){float a=(i-2)*.33f;Leaf(Polar(a)*.95f,a,c);}break;
                default: for(int i=0;i<6;i++)Leaf(Polar(i*1.047f)*.5f,i*1.047f,c);break;
            }
        }
        void Arc(float r,float a,float b,float width,Color c)
        {int steps=Mathf.Max(2,Mathf.CeilToInt((b-a)*12));for(int i=0;i<steps;i++)Stroke(Polar(Mathf.Lerp(a,b,i/(float)steps))*r,Polar(Mathf.Lerp(a,b,(i+1)/(float)steps))*r,width,c);}
        void Diamond(Vector2 p,float r,Color c)
        {Triangle(p+Vector2.up*r,p+Vector2.left*r*.6f,p+Vector2.down*r,c);Triangle(p+Vector2.up*r,p+Vector2.down*r,p+Vector2.right*r*.6f,c);}
        void Leaf(Vector2 p,float angle,Color c)
        {Vector2 d=Polar(angle),n=Vector2.Perpendicular(d);Triangle(p-d*.16f,p+n*.09f,p+d*.16f,c);Triangle(p-d*.16f,p+d*.16f,p-n*.09f,c);Stroke(p-d*.1f,p+d*.1f,.015f,Paper);}
        public void Initialize(Material material)
        {
            mesh = new Mesh { name = "Item feedback geometry" }; mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            drawing = gameObject.AddComponent<MeshRenderer>(); drawing.sharedMaterial = material;
            drawing.sortingLayerName = "Monster Full"; drawing.sortingOrder = 35;
            tint = new MaterialPropertyBlock();
            gameObject.SetActive(false);
        }
        public void Play(ItemFeedbackKind kind, Vector3 position, float seconds, float size, Transform target, int count, string text)
        {
            Lease++;
            Kind = kind; themed = false; duration = Mathf.Max(.05f, seconds); radius = Mathf.Max(.1f, size);
            age = 0; follow = target; following = target != null; stacks = count;
            transform.SetPositionAndRotation(position, Quaternion.identity); transform.localScale = Vector3.one;
            drawing.sortingLayerName = kind == ItemFeedbackKind.FireZone ? "GroundEffects" : "Monster Full";
            drawing.sortingOrder = kind == ItemFeedbackKind.FireZone ? 20 : 35;
            Build();
            // TMP initializes its renderer in Awake; the pooled parent must be active first.
            gameObject.SetActive(true);
            SetCaption(text);
            gameObject.SetActive(true); Draw(0);
        }
        void SetCaption(string text)
        {
            if (text.Length > 0 && caption == null)
            {
                var go = new GameObject("Proc caption"); go.transform.SetParent(transform, false);
                caption = go.AddComponent<TextMeshPro>();
                var config = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
                caption.font = config != null && config.font != null ? config.font : TMP_Settings.defaultFontAsset;
                caption.fontSize = 3.6f; caption.alignment = TextAlignmentOptions.Center;
                if(caption.font != null)
                {
                    captionMaterial = new Material(caption.font.material);
                    captionMaterial.SetColor("_OutlineColor",Ink);captionMaterial.SetFloat("_OutlineWidth",.16f);
                    caption.fontSharedMaterial = captionMaterial;
                }
                caption.enableWordWrapping = false; caption.rectTransform.sizeDelta = new Vector2(4, .8f);
                caption.GetComponent<MeshRenderer>().sortingLayerName = "Monster Full";
                caption.GetComponent<MeshRenderer>().sortingOrder = 45;
            }
            if (caption != null) { caption.text = text; caption.gameObject.SetActive(text.Length > 0); }
        }
        public void SetStacks(int count)
        {
            if (stacks == count) return;
            stacks = count; Build();
        }
        public void Release() { follow = null; following = false; gameObject.SetActive(false); }
        public void Release(int lease) { if (Lease == lease) Release(); }
        void Update()
        {
            if (Time.timeScale <= 0) return;
            if (following && (follow == null || !follow.gameObject.activeInHierarchy)) { Release(); return; }
            if (following) transform.position = follow.position;
            age += Time.deltaTime;
            if (age >= duration) { Release(); return; }
            Draw(age / duration);
        }
        void Draw(float t)
        {
            if (themed) { DrawTheme(t); return; }
            bool calm = GamePreferences.Current.reducedMotion;
            bool persistent = Kind == ItemFeedbackKind.FireZone || Kind == ItemFeedbackKind.Spirit || Kind == ItemFeedbackKind.PressureMark;
            float alpha = persistent ? Mathf.Clamp01((1 - t) * 8) : 1 - t;
            tint.SetColor("_Color", new Color(1, 1, 1, alpha * GamePreferences.Current.augmentVfxOpacity));
            drawing.SetPropertyBlock(tint);
            float scale = 1;
            if (!calm)
            {
                if (Kind == ItemFeedbackKind.Steam || Kind == ItemFeedbackKind.Echo) scale = Mathf.Lerp(.35f, 1, Mathf.Sqrt(t));
                else if (Kind == ItemFeedbackKind.Revival) scale = Mathf.Lerp(.3f, 1, Mathf.Min(1, t * 3));
                else if (Kind == ItemFeedbackKind.PressureBurst) scale = 1 + .12f * Mathf.Sin(t * Mathf.PI);
                else if (Kind == ItemFeedbackKind.Spirit) scale = 1 + .05f * Mathf.Sin(age * 14);
            }
            transform.localScale = Vector3.one * (radius * scale);
            if (caption != null && caption.gameObject.activeSelf)
            {
                caption.transform.localScale = Vector3.one / (radius * scale);
                caption.transform.localPosition = new Vector3(0, (Mathf.Min(radius,.9f) + .35f + (calm ? 0 : t * .25f)) / (radius * scale), 0);
                caption.color = new Color(1, .9f, .45f, alpha * GamePreferences.Current.augmentVfxOpacity);
            }
        }
        void Build()
        {
            vertices.Clear(); colors.Clear(); triangles.Clear();
            switch (Kind)
            {
                case ItemFeedbackKind.PressureMark:
                    for (int i = 0; i < 4; i++) Disc(new Vector2((i - 1.5f) * .26f, .65f), .09f, i < stacks ? Gold : Ink);
                    break;
                case ItemFeedbackKind.PressureBurst:
                    Ring(.48f, .075f, Gold);
                    for (int i = 0; i < 8; i++) { var d = Polar(i * Mathf.PI / 4); Stroke(d * .6f, d, .055f, i % 2 == 0 ? Red : Paper); }
                    break;
                case ItemFeedbackKind.FireZone:
                    Disc(Vector2.zero, 1, new Color(.8f, .18f, .08f, .16f)); Ring(.98f, .025f, Red);
                    for (int i = 0; i < 7; i++)
                    {
                        Vector2 c = i == 0 ? Vector2.zero : Polar(i * Mathf.PI / 3) * .58f;
                        Flame(c, 1, Red); Flame(c + new Vector2(.01f,-.03f), .53f, Gold);
                    }
                    break;
                case ItemFeedbackKind.Spirit:
                    Disc(new Vector2(0,.05f),.4f,new Color(.25f,.82f,1,.17f));
                    Disc(new Vector2(0, .12f), .29f, Paper);
                    Polygon(new Vector2(0,-.07f),SpiritOutline,Paper);
                    Disc(new Vector2(-.1f, .15f), .045f, Ink); Disc(new Vector2(.1f, .15f), .045f, Ink);
                    Stroke(new Vector2(0,.23f),new Vector2(0,.38f),.045f,Red);
                    Stroke(new Vector2(-.06f,.30f),new Vector2(.06f,.30f),.025f,Red);
                    Disc(new Vector2(0,-.02f),.045f,Ink);
                    break;
                case ItemFeedbackKind.Steam:
                    Ring(.86f, .05f, Paper);
                    for (int i = 0; i < 8; i++)
                    {
                        var c = Polar(i * Mathf.PI / 4) * .52f;
                        Disc(c, .27f, i % 2 == 0 ? new Color(1, .45f, .28f, .65f) : new Color(.4f, .85f, 1, .65f));
                        Disc(c+Vector2.up*.12f,.18f,new Color(.94f,.96f,1,.65f));
                        Stroke(c+Vector2.up*.24f,c+new Vector2(.05f,.4f),.025f,Paper);
                    }
                    break;
                case ItemFeedbackKind.Revival:
                    for (int i = 0; i < 8; i++)
                    {
                        Petal(i * Mathf.PI / 4, i%2==0 ? new Color(1,.55f,.7f,.85f) : Paper);
                    }
                    Disc(Vector2.zero, .2f, Gold); Ring(1.08f, .025f, Gold);
                    break;
                case ItemFeedbackKind.Echo: Ring(.95f, .04f, Blue); Ring(.7f, .025f, Paper); break;
                case ItemFeedbackKind.Themed: break;
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }
        static Vector2 Polar(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        void Polygon(Vector2 center, Vector2[] points, Color color)
        { for(int i=0;i<points.Length;i++)Triangle(center,points[i],points[(i+1)%points.Length],color); }
        static readonly Vector2[] FlameOutline = {new Vector2(-.14f,-.18f),new Vector2(-.22f,-.02f),new Vector2(-.13f,.19f),new Vector2(-.08f,.06f),new Vector2(.05f,.34f),new Vector2(.06f,.13f),new Vector2(.18f,-.01f),new Vector2(.15f,-.17f),new Vector2(0,-.23f)};
        static readonly Vector2[] SpiritOutline = {new Vector2(-.28f,.12f),new Vector2(-.26f,-.16f),new Vector2(-.17f,-.34f),new Vector2(-.06f,-.23f),new Vector2(.03f,-.35f),new Vector2(.13f,-.23f),new Vector2(.24f,-.27f),new Vector2(.28f,.12f)};
        void Flame(Vector2 center,float scale,Color color)
        { for(int i=0;i<FlameOutline.Length;i++)Triangle(center+new Vector2(0,-.05f)*scale,center+FlameOutline[i]*scale,center+FlameOutline[(i+1)%FlameOutline.Length]*scale,color); }
        void Petal(float angle,Color color)
        {
            Vector2 d=Polar(angle),n=new Vector2(-d.y,d.x);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;
                Vector2 p=d*(.48f+.48f*Mathf.Cos(a))+n*(.21f*Mathf.Sin(a));
                Vector2 q=d*(.48f+.48f*Mathf.Cos(b))+n*(.21f*Mathf.Sin(b));
                Triangle(d*.48f,p,q,color);
            }
        }
        void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colors.Add(color); colors.Add(color); colors.Add(color);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }
        void Disc(Vector2 center, float r, Color color)
        { for (int i = 0; i < 24; i++) Triangle(center, center + Polar(i * Mathf.PI / 12) * r, center + Polar((i + 1) * Mathf.PI / 12) * r, color); }
        void Ring(float r, float width, Color color)
        { for (int i = 0; i < 40; i++) Stroke(Polar(i * Mathf.PI / 20) * r, Polar((i + 1) * Mathf.PI / 20) * r, width, color); }
        void Stroke(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = (b - a).normalized; Vector2 n = new Vector2(-d.y, d.x) * width * .5f;
            Triangle(a - n, a + n, b + n, color); Triangle(a - n, b + n, b - n, color);
        }
        void OnDestroy()
        {
            if (mesh != null) ItemFeedback.Dispose(mesh);
            if (captionMaterial != null) ItemFeedback.Dispose(captionMaterial);
        }
    }
}
