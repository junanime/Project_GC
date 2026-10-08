using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Reuses the shipped animation frames; guides cannot silently keep the retired wave/runner art.
    public sealed class FoodTutorialPreview : MonoBehaviour
    {
        RectTransform root;Image actor,player,stream,warning;string mode;float start;
        Image[] extras; Text caption;
        public static bool Supports(string effect) => effect.StartsWith("chameleon_")||effect=="wave"||effect=="runner"||effect=="toad"||effect=="item_health"||effect=="item_potion"||effect=="item_magnet";
        public void Configure(string effect)
        {
            mode=effect;bool use=Supports(effect);
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
                extras=new Image[6];for(int i=0;i<extras.Length;i++)extras[i]=Picture("Demo effect "+i,root);
                var label=AugmentPanelTheme.Rect("Demo caption",root);caption=label.gameObject.AddComponent<Text>();
                caption.font=Resources.Load<Font>("TrainingUI/Cafe24Ssurround");caption.fontSize=18;caption.alignment=TextAnchor.MiddleCenter;caption.color=OctoberArt.Ink;caption.raycastTarget=false;
                AugmentPanelTheme.Anchors(label,new Vector2(.03f,.02f),new Vector2(.97f,.19f));
            }
            actor.rectTransform.localScale=Vector3.one;
            actor.color=player.color=stream.color=Color.white;
            foreach(var extra in extras)extra.gameObject.SetActive(false);
            root.gameObject.SetActive(true);Restart();Update();
        }
        public void Restart(){start=Time.unscaledTime;}
        static Image Picture(string name,Transform parent){var r=AugmentPanelTheme.Rect(name,parent);var i=r.gameObject.AddComponent<Image>();i.raycastTarget=false;return i;}
        static void Place(Image image,float x,float y,float w,float h)=>AugmentPanelTheme.Anchors(image.rectTransform,new Vector2(x,y),new Vector2(x+w,y+h));
        void Update()
        {
            if(root==null||!root.gameObject.activeSelf)return;
            float t=Mathf.Repeat(Time.unscaledTime-start,3.5f);
            if(mode.StartsWith("item_")){ItemDemo(t);return;}
            if(mode=="toad"||mode.StartsWith("chameleon_")){ChameleonDemo();return;}
            caption.text=mode=="runner"?(t>1.3f?"눈치채면 빠르게 도망!":"조심스럽게 접근"):(t<1?"볼 팽창 · 경로 경고":"옆으로 피해 탄산액 회피");
            if(mode=="wave")
            {
                bool fire=t>=1&&t<3;actor.sprite=ChameleonArt.Portrait(ChameleonKind.Fanta,t<1?10:t<3?11:12,true);
                Place(actor,.015f,.28f,.29f,.48f);warning.gameObject.SetActive(t<1);Place(warning,.22f,.41f,.77f,.14f);
                stream.gameObject.SetActive(fire);stream.sprite=ChameleonArt.Fx(4);stream.preserveAspect=true;Place(stream,.24f+.6f*Mathf.Clamp01((t-1)/2),.405f,.18f,.23f);
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
        void ChameleonDemo()
        {
            var kind=mode=="chameleon_drift"?ChameleonKind.Drift:mode=="chameleon_foam"?ChameleonKind.Foam:mode=="chameleon_latte"?ChameleonKind.Latte:ChameleonKind.Fanta;
            float clock=Mathf.Repeat(Time.unscaledTime-start,15),t=clock%5;int stage=(int)(clock/5);
            foreach(var e in extras)e.gameObject.SetActive(false);
            actor.rectTransform.localScale=Vector3.one;actor.color=Color.white;Place(actor,.06f,.28f,.32f,.49f);Place(player,.74f,.37f,.13f,.2f);
            warning.gameObject.SetActive(false);stream.gameObject.SetActive(false);actor.sprite=ChameleonArt.Frame(kind,0);
            if(stage==0)
            {
                caption.text="기본 공격 · 고유 탄막 3발";actor.sprite=ChameleonArt.Frame(kind,t<1?6:7);
                for(int i=0;i<3;i++){var e=extras[i];e.gameObject.SetActive(t>=1&&t<3);e.sprite=ChameleonArt.Projectile(kind);e.preserveAspect=true;float f=Mathf.Clamp01((t-1)/2);Place(e,.32f+f*.65f,.45f+(i-1)*f*.24f,.075f,.12f);}
            }
            else if(stage==1)
            {
                caption.text=t<3?"3초 은신 → 현재 위치에 외곽 경고":t<4?"1초 경고 동안 자리를 피하세요":"출현 순간 몸에 닿으면 피해";
                actor.color=new Color(1,1,1,t<.3f?1-t/.3f:t<4?0:1);
                if(t>=3)Place(actor,.56f,.28f,.32f,.49f);
                warning.gameObject.SetActive(t>=3&&t<4);warning.sprite=ChameleonArt.Frame(kind,0);warning.preserveAspect=true;
                warning.color=new Color(1,.55f,.1f,.5f);Place(warning,.56f,.28f,.32f,.49f);
                Place(player,.74f,Mathf.Lerp(.37f,.73f,Mathf.Clamp01((t-3)/.7f)),.13f,.2f);
            }
            else
            {
                actor.sprite=ChameleonArt.Frame(kind,8+Mathf.Min(4,(int)t));
                caption.text=kind==ChameleonKind.Drift?"양발 쿵쿵 → 박수 · 연동파 2회":kind==ChameleonKind.Foam?"앞발 모아 끄응 → 거품 안으로 이동":kind==ChameleonKind.Latte?"커피 분수 → 주변 몬스터 강화":"볼 팽창 → 단일 환타 파도";
                stream.gameObject.SetActive(t>=3&&t<4.8f);stream.sprite=ChameleonArt.Projectile(kind);stream.preserveAspect=true;
                Place(stream,.32f+Mathf.Clamp01((t-3)/1.8f)*.55f,.45f,.15f,.2f);
                if(kind==ChameleonKind.Fanta)stream.sprite=ChameleonArt.Fx(4);
            }
        }

        void ItemDemo(float t)
        {
            var art=DiscoveryPreviewArt.Load();if(art==null)return;
            bool magnet=mode=="item_magnet",potion=mode=="item_potion";
            actor.sprite=magnet?art.magnet:potion?art.potion:art.health;
            actor.rectTransform.localScale=Vector3.one;
            float use=Mathf.Clamp01((t-.7f)/.5f);
            Place(actor,Mathf.Lerp(.15f,.48f,use),.43f,.15f,.22f);actor.color=new Color(1,1,1,t>1.2f?0:1);
            Place(player,.46f,.36f,.16f,.26f);warning.gameObject.SetActive(!magnet);stream.gameObject.SetActive(!magnet);
            warning.sprite=stream.sprite=null;warning.color=new Color(.3f,.19f,.19f);stream.color=new Color(.28f,.82f,.48f);
            Place(warning,.36f,.7f,.35f,.045f);
            float fill=t<1.2f?.3f:.85f;Place(stream,.36f,.7f,.35f*fill,.045f);
            caption.text=magnet?"자석 사용 → 코인·경험치 수집":"체력 회복 아이템 사용";
            if(potion)
            {
                caption.text="30초 동안 일반 몬스터 스폰 +50%";
                warning.gameObject.SetActive(false);stream.gameObject.SetActive(false);
                for(int i=0;i<extras.Length;i++)
                {
                    var e=extras[i];e.gameObject.SetActive(t>1.2f||i<4);e.preserveAspect=true;
                    e.sprite=art.monster;
                    float angle=i*Mathf.PI/3;float radius=Mathf.Lerp(.43f,.28f,Mathf.Clamp01((t-1.2f)/2));
                    Place(e,.5f+Mathf.Cos(angle)*radius,.45f+Mathf.Sin(angle)*radius*.7f,.07f,.11f);
                }
                return;
            }
            for(int i=0;i<extras.Length;i++)
            {
                var e=extras[i];e.gameObject.SetActive(magnet&&t<2.8f);e.preserveAspect=true;e.sprite=i%2==0?art.coin:art.gem;
                if(e.sprite==null)e.sprite=art.magnet;
                float angle=i*Mathf.PI/3, f=Mathf.Clamp01((t-1.2f)/1.4f);
                Place(e,Mathf.Lerp(.5f+Mathf.Cos(angle)*.37f,.51f,f),Mathf.Lerp(.48f+Mathf.Sin(angle)*.26f,.45f,f),.055f,.085f);
            }
        }
    }
}
