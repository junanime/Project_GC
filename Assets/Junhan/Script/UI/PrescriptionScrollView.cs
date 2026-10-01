using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // A capsule prescription case unfolds a cream sheet without rescaling quest text.
    public sealed class PrescriptionScrollView : MonoBehaviour
    {
        public RectTransform Dock { get; private set; }
        public Button Toggle { get; private set; }
        public Button Claim { get; private set; }
        public TextMeshProUGUI Title { get; private set; }
        public TextMeshProUGUI Tag { get; private set; }
        public TextMeshProUGUI[] Rows { get; private set; }
        public bool Expanded { get; private set; }
        public float Reveal { get; private set; }
        RectTransform window, foot;
        CanvasGroup sheetInteraction;
        const float SheetHeight=330;
        static readonly Color Ink=OctoberArt.Ink;

        public void Build(TMP_FontAsset font, Action toggle, Action claim, bool expanded)
        {
            Dock=(RectTransform)transform;
            Dock.anchorMin=Dock.anchorMax=Dock.pivot=Vector2.one; Dock.sizeDelta=new Vector2(360,100);
            Dock.localScale=Vector3.one*.7f;
            window=AugmentPanelTheme.Rect("Scroll reveal window",transform);
            TopBox(window,new Vector2(180,-76),new Vector2(252,0));
            window.gameObject.AddComponent<RectMask2D>();
            var paper=AugmentPanelTheme.Rect("Scroll quest sheet",window);
            paper.anchorMin=paper.anchorMax=new Vector2(.5f,1);paper.pivot=new Vector2(.5f,1);
            paper.sizeDelta=new Vector2(252,SheetHeight);paper.anchoredPosition=Vector2.zero;
            var art=paper.gameObject.AddComponent<Image>();
            art.sprite=OctoberArt.Get("OctoberUI/Panel");art.type=Image.Type.Sliced;art.pixelsPerUnitMultiplier=7;art.raycastTarget=true;
            sheetInteraction=paper.gameObject.AddComponent<CanvasGroup>();
            Tag=Text("Scroll tag",paper,font,14,.09f,.83f,.91f,.92f);
            Rows=new TextMeshProUGUI[3];
            for(int i=0;i<3;i++)
            {
                Rows[i]=Text("Scroll quest "+i,paper,font,15,.10f,.59f-i*.20f,.90f,.80f-i*.20f);
                Rows[i].alignment=TextAlignmentOptions.MidlineLeft;
            }
            var claimRect=AugmentPanelTheme.Rect("Noble reward claim",paper);
            AugmentPanelTheme.Anchors(claimRect,new Vector2(.10f,.043f),new Vector2(.90f,.151f));
            var claimImage=claimRect.gameObject.AddComponent<Image>();claimImage.sprite=OctoberArt.Button(true);claimImage.type=Image.Type.Sliced;claimImage.pixelsPerUnitMultiplier=9;
            Claim=claimRect.gameObject.AddComponent<Button>();Claim.targetGraphic=claimImage;
            Claim.onClick.AddListener(()=>claim?.Invoke());
            var claimLabel=Text("Claim label",claimRect,font,14,.04f,.05f,.96f,.95f);
            claimLabel.text="고귀 증강 선택";claimLabel.color=AugmentPanelTheme.Paper;
            var footImage=AugmentPanelTheme.Fill("Moving scroll roller",transform,Color.white);
            footImage.sprite=OctoberArt.Button(false);footImage.type=Image.Type.Sliced;footImage.pixelsPerUnitMultiplier=18;
            foot=footImage.rectTransform;TopBox(foot,new Vector2(180,-76),new Vector2(260,14));
            var header=AugmentPanelTheme.Rect("Prescription toggle",transform);
            AugmentPanelTheme.Anchors(header,Vector2.zero,Vector2.one);
            var headerArt=header.gameObject.AddComponent<Image>();headerArt.sprite=OctoberArt.Get("OctoberUI/PrescriptionCase");headerArt.preserveAspect=true;
            headerArt.raycastTarget=true;
            Toggle=header.gameObject.AddComponent<Button>();Toggle.targetGraphic=headerArt;
            Toggle.onClick.AddListener(()=>{GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.UiClick);toggle?.Invoke();});
            Title=Text("Scroll title",header,font,18,.22f,.32f,.77f,.77f);Title.fontStyle=FontStyles.Bold;
            Expanded=expanded;Reveal=expanded?1:0;ApplyReveal();
        }
        static TextMeshProUGUI Text(string name,Transform parent,TMP_FontAsset font,float size,float x,float y,float right,float top)
        {
            var text=AugmentPanelTheme.Label(name,parent,font,"",size);
            AugmentPanelTheme.Anchors(text.rectTransform,new Vector2(x,y),new Vector2(right,top));
            text.color=Ink;text.enableWordWrapping=true;text.fontSizeMin=12;text.overflowMode=TextOverflowModes.Overflow;
            return text;
        }
        static void TopBox(RectTransform rect,Vector2 position,Vector2 size)
        {
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(.5f,1);
            rect.sizeDelta=size;rect.anchoredPosition=position;
        }
        public void SetExpanded(bool expanded) { Expanded=expanded; }
        void Update()
        {
            Reveal=Mathf.MoveTowards(Reveal,Expanded?1:0,Time.unscaledDeltaTime/.28f);
            ApplyReveal();
        }
        void ApplyReveal()
        {
            float eased=Reveal*Reveal*(3-2*Reveal);
            window.sizeDelta=new Vector2(252,SheetHeight*eased);
            window.gameObject.SetActive(Reveal>0);
            foot.gameObject.SetActive(Reveal>0);
            foot.anchoredPosition=new Vector2(180,-76-SheetHeight*eased+12);
            sheetInteraction.interactable=sheetInteraction.blocksRaycasts=Expanded&&Reveal>=.999f;
        }
    }
}
