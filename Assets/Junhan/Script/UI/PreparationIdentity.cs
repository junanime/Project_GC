using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public static class PreparationIdentity
    {
        public static Color ColorFor(CharacterBlueprint character)
        {
            switch (OctoberArt.CharacterKey(character))
            {
                case "Ari": return new Color(1f,.89f,.65f);
                case "Hyuki": return new Color(.16f,.49f,.91f);
                case "Shini": return new Color(.90f,.20f,.24f);
                default: return new Color(.91f,.19f,.40f);
            }
        }
    }

    // UI geometry, not a stretched raster: capsule headers keep their round ends at every size.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class IdentityShape : MaskableGraphic
    {
        public bool frame, sparkle;
        public Color insetColor=OctoberArt.Ink;
        public float radius=12;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=GetPixelAdjustedRect();
            Round(vh,rect,Mathf.Min(radius,rect.height/2),color);
            if(frame)
            {
                Round(vh,new Rect(rect.x+3,rect.y+3,rect.width-6,rect.height-6),Mathf.Max(1,radius-3),Color.Lerp(color,Color.white,.65f));
                Round(vh,new Rect(rect.x+6,rect.y+6,rect.width-12,rect.height-12),Mathf.Max(1,radius-6),insetColor);
                float pulse=GamePreferences.Current.reducedMotion?.65f:.5f+.5f*Mathf.Sin(Time.unscaledTime*2.2f);
                if(sparkle)
                {
                    var glint=new Color(1,1,.91f,.45f+.5f*pulse);
                    Round(vh,new Rect(rect.x+rect.width*.19f-1,rect.yMax-9,2,12),1,glint);
                    Round(vh,new Rect(rect.x+rect.width*.19f-6,rect.yMax-4,12,2),1,glint);
                    Round(vh,new Rect(rect.xMax-5,rect.y+rect.height*.24f-3,2,7),1,glint);
                }
            }
            else Round(vh,new Rect(rect.x+6,rect.yMax-5,Mathf.Max(0,rect.width-12),2),1,new Color(1,1,1,.22f));
        }
        static void Round(VertexHelper vh,Rect rect,float radius,Color tint)
        {
            int first=vh.currentVertCount;vh.AddVert(rect.center,tint,Vector2.zero);
            const int steps=6;int count=4*(steps+1);
            for(int corner=0;corner<4;corner++)
            {
                Vector2 center=new Vector2(corner<2?rect.xMax-radius:rect.x+radius,corner==0||corner==3?rect.y+radius:rect.yMax-radius);
                for(int i=0;i<=steps;i++)
                {
                    float angle=(-90+corner*90+i*90f/steps)*Mathf.Deg2Rad;
                    vh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,tint,Vector2.zero);
                }
            }
            for(int i=0;i<count;i++)vh.AddTriangle(first,first+1+i,first+1+(i+1)%count);
        }
        void Update(){if(sparkle&&!GamePreferences.Current.reducedMotion)SetVerticesDirty();}
    }

    // Reuses the actual character and skill frames, so the preview matches the playable identity.
    public sealed class PreparationSkillPreview : MonoBehaviour
    {
        public CharacterBlueprint character;
        public Image body, effect;
        float elapsed;
        void Update()
        {
            if(character==null||body==null)return;
            elapsed+=Time.unscaledDeltaTime;
            bool reduced=GamePreferences.Current.reducedMotion;
            float phase=Mathf.Repeat(elapsed,4.8f);bool active=!reduced&&phase<2.8f;
            var d=character.skills;
            var frames=active?character.dashSpriteSequence:character.idleSpriteSequence;
            Sprite[] fx=null;
            if(d!=null)
            {
                switch(d.kind)
                {
                    case CharacterSkillDefinition.SkillKind.Ashi:fx=d.activeWind;break;
                    case CharacterSkillDefinition.SkillKind.Hyuki:frames=character.idleSpriteSequence;fx=d.icePrison;break;
                    case CharacterSkillDefinition.SkillKind.Shini:if(active)frames=d.burningWalk;fx=d.burnVfx;break;
                    case CharacterSkillDefinition.SkillKind.Ari:fx=null;break;
                }
            }
            if(frames==null||frames.Length==0)frames=character.walkSpriteSequence;
            if(frames!=null&&frames.Length>0)body.sprite=frames[reduced?0:(int)(elapsed/(active?.085f:.14f))%frames.Length];
            body.rectTransform.anchoredPosition=active?new Vector2(Mathf.Sin(phase*4)*10,Mathf.Abs(Mathf.Sin(phase*7))*3):Vector2.zero;
            if(effect!=null)
            {
                effect.enabled=active&&fx!=null&&fx.Length>0;
                if(effect.enabled){effect.sprite=fx[(int)(elapsed/.09f)%fx.Length];effect.color=new Color(1,1,1,.85f);}
            }
        }
    }
}
