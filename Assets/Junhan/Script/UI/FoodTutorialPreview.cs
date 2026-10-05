using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Reuses the shipped animation frames; guides cannot silently keep the retired wave/runner art.
    public sealed class FoodTutorialPreview : MonoBehaviour
    {
        RectTransform root;Image actor,player,stream,warning;string mode;float start;
        public void Configure(string effect)
        {
            mode=effect;bool use=effect=="wave"||effect=="runner";
            GetComponent<RawImage>().color=use?Color.clear:Color.white;
            if(!use){if(root!=null)root.gameObject.SetActive(false);return;}
            if(root==null)
            {
                root=AugmentPanelTheme.Rect("Live food tutorial",transform);AugmentPanelTheme.Anchors(root,Vector2.zero,Vector2.one);
                var bg=root.gameObject.AddComponent<Image>();bg.color=new Color(.91f,.53f,.59f);bg.raycastTarget=false;
                warning=Picture("Warning",root);warning.color=new Color(1,.45f,.05f,.4f);
                stream=Picture("Acid stream",root);actor=Picture("Toad or walnut",root);player=Picture("Hyuki",root);
                var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
                if(config!=null){var hyuki=config.characters.FirstOrDefault(c=>OctoberArt.CharacterKey(c)=="Hyuki");if(hyuki!=null)player.sprite=OctoberArt.Character(hyuki,0);}
                actor.preserveAspect=player.preserveAspect=true;
            }
            root.gameObject.SetActive(true);Restart();Update();
        }
        public void Restart(){start=Time.unscaledTime;}
        static Image Picture(string name,Transform parent){var r=AugmentPanelTheme.Rect(name,parent);var i=r.gameObject.AddComponent<Image>();i.raycastTarget=false;return i;}
        static void Place(Image image,float x,float y,float w,float h)=>AugmentPanelTheme.Anchors(image.rectTransform,new Vector2(x,y),new Vector2(x+w,y+h));
        void Update()
        {
            if(root==null||!root.gameObject.activeSelf)return;
            float t=Mathf.Repeat(Time.unscaledTime-start,3.5f);
            if(mode=="wave")
            {
                bool fire=t>=1&&t<3;actor.sprite=AcidToadArt.Frame("ToadHead",t<1?Mathf.Min(7,(int)(t*8)):t<3?8+Mathf.Min(7,(int)((t-1)*4)):15);
                Place(actor,.015f,.28f,.29f,.48f);warning.gameObject.SetActive(t<1);Place(warning,.22f,.41f,.77f,.14f);
                stream.gameObject.SetActive(fire);stream.sprite=AcidToadArt.Frame("AcidFx",(int)(t*12)%4);Place(stream,.245f,.405f,.74f*Mathf.Clamp01((t-1)/.3f),.15f);
                Place(player,.63f,Mathf.Lerp(.4f,.72f,Mathf.Clamp01(t/.7f)),.12f,.18f);
            }
            else
            {
                warning.gameObject.SetActive(false);stream.gameObject.SetActive(false);bool flee=t>1.3f;
                actor.sprite=AcidToadArt.Frame("SquirrelMotion",(flee?8:0)+(int)(t*(flee?16:7))%8);
                float x=flee?Mathf.Lerp(.5f,-.28f,(t-1.3f)/2.2f):Mathf.Lerp(.06f,.5f,t/1.3f);
                Place(actor,x,.33f,.26f,.4f);actor.rectTransform.localScale=new Vector3(flee?-1:1,1,1);Place(player,.73f,.35f,.13f,.19f);
            }
            if(mode=="wave")actor.rectTransform.localScale=Vector3.one;
        }
    }
}
