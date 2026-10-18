using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Shared with the lobby's active-skill frame: never maintain a second character palette.
    public static class CharacterHudTheme
    {
        public static readonly Color Paper = new Color(1f, .974f, .88f);
        public static readonly Color Coral = new Color(1f, .43f, .48f);
        public static Color Accent(CharacterBlueprint character) => PreparationIdentity.ColorFor(character);
        public static Color HeaderInk(CharacterBlueprint character) => OctoberArt.CharacterKey(character)=="Ari" ? OctoberArt.Ink : Color.white;
        public static Color Edge(CharacterBlueprint character)
        {
            switch(OctoberArt.CharacterKey(character))
            {
                case "Hyuki": return new Color(.27f,.66f,.81f);
                case "Shini": return new Color(.75f,.39f,.16f);
                case "Ari": return new Color(.66f,.51f,.61f);
                default: return new Color(.25f,.52f,.43f);
            }
        }
        public static HudPaperGraphic PaperAt(string name, Transform parent, Color edge)
        {
            var graphic=AugmentPanelTheme.Rect(name,parent).gameObject.AddComponent<HudPaperGraphic>();
            graphic.color=Paper;graphic.edge=edge;graphic.raycastTarget=false;
            return graphic;
        }
        public static HudOrnament Ornament(string name, Transform parent, CharacterBlueprint character)
        {
            var ornament=AugmentPanelTheme.Rect(name,parent).gameObject.AddComponent<HudOrnament>();
            ornament.SetCharacter(character);ornament.raycastTarget=false;return ornament;
        }
    }

    // Small chamfered paper shapes extend the existing code-native HUD graphics.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudPaperGraphic : MaskableGraphic
    {
        public Color edge = new Color(.25f,.52f,.43f);
        public bool foldedCorner;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();
            Polygon(vh,r,OctoberArt.Ink,7);
            Polygon(vh,Inset(r,1),edge,6);
            Polygon(vh,Inset(r,3),color,5);
            if(foldedCorner)
            {
                var a=new Vector2(r.xMax-15,r.y+3);var b=new Vector2(r.xMax-3,r.y+15);
                int v=vh.currentVertCount;
                vh.AddVert(a,edge,Vector2.zero);vh.AddVert(b,edge,Vector2.zero);vh.AddVert(new Vector2(a.x,b.y),edge,Vector2.zero);vh.AddTriangle(v,v+1,v+2);
            }
        }
        static Rect Inset(Rect r,float p)=>new Rect(r.x+p,r.y+p,Mathf.Max(0,r.width-p*2),Mathf.Max(0,r.height-p*2));
        static void Polygon(VertexHelper vh,Rect r,Color c,float bevel)
        {
            float d=Mathf.Min(bevel,Mathf.Min(r.width,r.height)*.25f);int v=vh.currentVertCount;
            Vector2[] pts={new Vector2(r.x+d,r.y),new Vector2(r.xMax-d,r.y),new Vector2(r.xMax,r.y+d),new Vector2(r.xMax,r.yMax-d),new Vector2(r.xMax-d,r.yMax),new Vector2(r.x+d,r.yMax),new Vector2(r.x,r.yMax-d),new Vector2(r.x,r.y+d)};
            vh.AddVert(r.center,c,Vector2.zero);foreach(var p in pts)vh.AddVert(p,c,Vector2.zero);
            for(int i=0;i<8;i++)vh.AddTriangle(v,v+1+i,v+1+(i+1)%8);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudOrnament : MaskableGraphic
    {
        string key="Ashi";
        public string CharacterKey=>key;
        public void SetCharacter(CharacterBlueprint character){key=OctoberArt.CharacterKey(character);SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch(key)
            {
                case "Ari":
                    Feather(vh,new Vector2(.39f,.43f),-38);Feather(vh,new Vector2(.66f,.49f),32);
                    Gem(vh,new Vector2(.49f,.19f),new Vector2(.12f,.12f),new Color(1,.64f,.73f));break;
                case "Hyuki":
                    Crystal(vh,new Vector2(.48f,.49f),new Vector2(.17f,.44f));
                    Crystal(vh,new Vector2(.23f,.30f),new Vector2(.15f,.25f));Crystal(vh,new Vector2(.73f,.30f),new Vector2(.15f,.27f));break;
                case "Shini":
                    Poly(vh,new Color(.28f,.10f,.12f),new Vector2(.22f,.12f),new Vector2(.12f,.35f),new Vector2(.26f,.70f),new Vector2(.40f,.48f),new Vector2(.49f,.98f),new Vector2(.73f,.66f),new Vector2(.85f,.36f),new Vector2(.76f,.13f),new Vector2(.5f,.04f));
                    Poly(vh,new Color(1,.43f,.12f),new Vector2(.26f,.18f),new Vector2(.20f,.36f),new Vector2(.29f,.57f),new Vector2(.44f,.40f),new Vector2(.52f,.83f),new Vector2(.67f,.61f),new Vector2(.77f,.35f),new Vector2(.70f,.20f),new Vector2(.5f,.12f));
                    Poly(vh,new Color(1,.86f,.32f),new Vector2(.35f,.22f),new Vector2(.4f,.46f),new Vector2(.5f,.36f),new Vector2(.58f,.6f),new Vector2(.66f,.32f),new Vector2(.58f,.19f));break;
                default:
                    Leaf(vh,new Vector2(.28f,.30f),-52,.36f);Leaf(vh,new Vector2(.73f,.34f),48,.34f);Leaf(vh,new Vector2(.5f,.56f),-4,.44f);break;
            }
        }
        void Leaf(VertexHelper vh,Vector2 center,float angle,float length)
        {
            Vector2[] shape={new Vector2(0,-1),new Vector2(-.48f,-.25f),new Vector2(-.46f,.35f),new Vector2(0,1),new Vector2(.45f,.32f),new Vector2(.43f,-.32f)};
            Shape(vh,shape,center,new Vector2(length,length),angle,new Color(.08f,.26f,.16f));
            Shape(vh,shape,center,new Vector2(length*.80f,length*.84f),angle,new Color(.36f,.77f,.29f));
            Shape(vh,new[]{new Vector2(0,-.85f),new Vector2(-.27f,.20f),new Vector2(0,.77f),new Vector2(.10f,-.14f)},center,new Vector2(length,length),angle,new Color(.73f,.92f,.43f));
        }
        void Feather(VertexHelper vh,Vector2 c,float a)
        {
            Vector2[] p={new Vector2(0,-1),new Vector2(-.36f,-.5f),new Vector2(-.40f,.35f),new Vector2(0,1),new Vector2(.36f,.45f),new Vector2(.30f,-.4f)};
            Shape(vh,p,c,new Vector2(.38f,.43f),a,new Color(.38f,.28f,.25f));
            Shape(vh,p,c,new Vector2(.30f,.37f),a,new Color(1,.96f,.84f));
            Shape(vh,new[]{new Vector2(-.04f,-1),new Vector2(.08f,-1),new Vector2(.04f,.8f)},c,new Vector2(.4f,.4f),a,new Color(.77f,.65f,.52f));
        }
        void Crystal(VertexHelper vh,Vector2 c,Vector2 s)
        {
            var p=new[]{new Vector2(-1,-.65f),new Vector2(-1,.5f),new Vector2(0,1),new Vector2(1,.5f),new Vector2(1,-.65f),new Vector2(0,-1)};
            Shape(vh,p,c,s,0,new Color(.12f,.32f,.49f));Shape(vh,p,c,s*.82f,0,new Color(.38f,.8f,1));
            Shape(vh,new[]{new Vector2(0,-.8f),new Vector2(0,.85f),new Vector2(.8f,.42f)},c,s,0,new Color(.85f,.98f,1));
        }
        void Gem(VertexHelper vh,Vector2 c,Vector2 s,Color color)=>Shape(vh,new[]{Vector2.down,Vector2.left,Vector2.up,Vector2.right},c,s,0,color);
        void Shape(VertexHelper vh,Vector2[] p,Vector2 c,Vector2 scale,float angle,Color tint)
        {
            var points=new Vector2[p.Length];var q=Quaternion.Euler(0,0,angle);
            for(int i=0;i<p.Length;i++)points[i]=c+(Vector2)(q*(Vector3)Vector2.Scale(p[i],scale));Poly(vh,tint,points);
        }
        void Poly(VertexHelper vh,Color tint,params Vector2[] points)
        {
            var r=GetPixelAdjustedRect();int first=vh.currentVertCount;
            // Quantized vertices give the ornament the same fine pixel step as the game icons.
            foreach(var p in points)vh.AddVert(new Vector2(r.x+Mathf.Round(p.x*r.width),r.y+Mathf.Round(p.y*r.height)),tint,Vector2.zero);
            for(int i=1;i<points.Length-1;i++)vh.AddTriangle(first,first+i,first+i+1);
        }
    }
}
