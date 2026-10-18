using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public static class StitchPatchTheme
    {
        public static readonly Color Coral=new Color(.98f,.38f,.43f), Mint=new Color(.73f,.88f,.79f), Thread=new Color(.88f,.58f,.42f);
        static readonly Dictionary<string,Sprite> motifs=new Dictionary<string,Sprite>();
        public static Color CardAccent(AugmentUpgradeGrade grade)=>grade==AugmentUpgradeGrade.Common?new Color(1,.55f,.48f):AugmentPanelTheme.Accent(grade);
        public static Sprite Motif(string key)
        {
            if(motifs.TryGetValue(key,out var cached)&&cached!=null)return cached;
            var texture=Resources.Load<Texture2D>("StitchPatch/CharacterMotifs");if(texture==null)return null;
            // Native sprite rectangles crop transparent gutters; original atlas remains untouched.
            Rect top=key=="Hyuki"?new Rect(759,153,352,374):key=="Shini"?new Rect(166,728,359,381):key=="Ari"?new Rect(747,815,362,285):new Rect(157,163,345,362);
            top=new Rect(top.x*texture.width/1254f,top.y*texture.height/1254f,top.width*texture.width/1254f,top.height*texture.height/1254f);
            var sprite=Sprite.Create(texture,new Rect(top.x,texture.height-top.yMax,top.width,top.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
            sprite.name="Stitch motif "+key;motifs[key]=sprite;return sprite;
        }
        public static StitchPatchGraphic Attach(Transform parent,Color accent,bool fill=false,bool corners=false,bool stars=false)
        {
            var rect=AugmentPanelTheme.Rect("Stitch decoration",parent);AugmentPanelTheme.Anchors(rect,Vector2.zero,Vector2.one);
            var g=rect.gameObject.AddComponent<StitchPatchGraphic>();g.accent=accent;g.fill=fill;g.corners=corners;g.stars=stars;g.raycastTarget=false;return g;
        }
        public static void Button(ApothecaryButtonFeedback f,bool coral,bool replaceBody=true)
        {
            if(f==null||f.visual.Find("Stitch decoration")!=null)return;
            var g=Attach(f.visual,replaceBody?(coral?Coral:Mint):Thread,true);g.button=replaceBody;g.inset=8;g.radius=replaceBody?15:12;
            f.body.enabled=false;f.useThemeStates=false;g.transform.SetAsFirstSibling();
            var state=f.gameObject.AddComponent<StitchPatchButton>();state.feedback=f;state.surface=g;state.coral=coral;state.replaceBody=replaceBody;
        }
    }
    public sealed class StitchPatchButton:MonoBehaviour
    {
        public ApothecaryButtonFeedback feedback;public StitchPatchGraphic surface;public bool coral,replaceBody;
        Color last;
        void LateUpdate()
        {
            if(feedback==null||surface==null)return;
            bool usable=feedback.button.IsInteractable();
            bool red=coral||!feedback.borderOnly&&(feedback.IsHighlighted||feedback.IsPressed);
            Color c=replaceBody?(red?StitchPatchTheme.Coral:StitchPatchTheme.Mint):StitchPatchTheme.Thread;
            if(!usable)c=Color.Lerp(c,new Color(.55f,.55f,.55f),.55f);
            if(feedback.IsPressed&&!feedback.borderOnly)c*=.9f;
            if(c!=last){surface.accent=c;surface.SetVerticesDirty();last=c;}
            if(replaceBody&&feedback.label!=null)feedback.label.color=red?Color.white:OctoberArt.Ink;
        }
    }
}
