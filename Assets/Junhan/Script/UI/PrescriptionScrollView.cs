using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // A stationary character magnet holds a folded note; gameplay and reward data remain live.
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
        public Color HeaderColor => titleFill!=null?titleFill.color:Color.clear;
        public HudOrnament Magnet { get; private set; }
        RectTransform window, fold, foldedEdge;
        CanvasGroup sheetInteraction;
        HudPaperGraphic paper, header, crease;
        Image titleFill;
        const float Width=312, SheetHeight=258, HeaderHeight=62;
        public void Build(TMP_FontAsset font, Action toggle, Action claim, bool expanded, CharacterBlueprint character=null)
        {
            Dock=(RectTransform)transform;
            Dock.anchorMin=Dock.anchorMax=Dock.pivot=Vector2.one;
            Dock.sizeDelta=new Vector2(Width,HeaderHeight);Dock.localScale=Vector3.one*.75f;
            window=AugmentPanelTheme.Rect("Quest unfold window",transform);
            TopBox(window,new Vector2(Width/2,-HeaderHeight+8),new Vector2(Width,0));
            window.gameObject.AddComponent<RectMask2D>();
            paper=CharacterHudTheme.PaperAt("Quest note",window,CharacterHudTheme.Edge(character));paper.foldedCorner=true;
            TopBox(paper.rectTransform,new Vector2(Width/2,0),new Vector2(Width,SheetHeight));paper.raycastTarget=true;
            sheetInteraction=paper.gameObject.AddComponent<CanvasGroup>();
            Tag=Text("Quest category",paper.transform,font,15,.07f,.86f,.93f,.97f);
            Rows=new TextMeshProUGUI[3];
            for(int i=0;i<3;i++)
            {
                Rows[i]=Text("Quest objective "+i,paper.transform,font,19,.07f,.64f-i*.20f,.93f,.84f-i*.20f);
                Rows[i].alignment=TextAlignmentOptions.MidlineLeft;
            }
            var claimRect=AugmentPanelTheme.Rect("Noble reward claim",paper.transform);
            AugmentPanelTheme.Anchors(claimRect,new Vector2(.07f,.04f),new Vector2(.93f,.20f));
            var claimFace=claimRect.gameObject.AddComponent<HudPaperGraphic>();claimFace.color=CharacterHudTheme.Coral;claimFace.edge=new Color(.74f,.32f,.36f);
            Claim=claimRect.gameObject.AddComponent<Button>();Claim.targetGraphic=claimFace;
            Claim.onClick.AddListener(()=>claim?.Invoke());
            var label=Text("Claim label",claimRect,font,16,.03f,.03f,.97f,.97f);label.text="고귀 증강 선택";
            // Layered paper under the header is visible even when the note is latched shut.
            var edge=CharacterHudTheme.PaperAt("Folded paper edge",transform,CharacterHudTheme.Edge(character));
            foldedEdge=edge.rectTransform;TopBox(foldedEdge,new Vector2(Width/2,-HeaderHeight+8),new Vector2(Width-8,12));
            crease=CharacterHudTheme.PaperAt("Moving paper fold",transform,CharacterHudTheme.Edge(character));
            crease.color=new Color(.97f,.92f,.79f);fold=crease.rectTransform;
            TopBox(fold,new Vector2(Width/2,-HeaderHeight),new Vector2(Width-6,24));
            header=CharacterHudTheme.PaperAt("Quest toggle",transform,CharacterHudTheme.Edge(character));
            header.foldedCorner=true;header.raycastTarget=true;
            TopBox(header.rectTransform,new Vector2(Width/2,0),new Vector2(Width,HeaderHeight));
            titleFill=AugmentPanelTheme.Fill("Character quest title color",header.transform,CharacterHudTheme.Accent(character));
            AugmentPanelTheme.Anchors(titleFill.rectTransform,new Vector2(.045f,.19f),new Vector2(.955f,.84f));
            Toggle=header.gameObject.AddComponent<Button>();Toggle.targetGraphic=header;
            Toggle.transition=Selectable.Transition.None;
            Toggle.onClick.AddListener(()=>{GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.UiClick);toggle?.Invoke();});
            Title=Text("Quest title",header.transform,font,23,.07f,.19f,.93f,.84f);Title.fontStyle=FontStyles.Bold;
            Magnet=CharacterHudTheme.Ornament("Quest magnet",header.transform,character);
            TopBox(Magnet.rectTransform,new Vector2(Width/2,25),new Vector2(44,44));Magnet.raycastTarget=true;
            ApplyTheme(character);Expanded=expanded;Reveal=expanded?1:0;ApplyReveal();
        }
        public void ApplyTheme(CharacterBlueprint character)
        {
            titleFill.color=CharacterHudTheme.Accent(character);Title.color=CharacterHudTheme.HeaderInk(character);
            foreach(var p in GetComponentsInChildren<HudPaperGraphic>(true)){p.edge=CharacterHudTheme.Edge(character);p.SetVerticesDirty();}
            Magnet.SetCharacter(character);
        }
        static TextMeshProUGUI Text(string name,Transform parent,TMP_FontAsset font,float size,float x,float y,float right,float top)
        {
            var text=AugmentPanelTheme.Label(name,parent,font,"",size);
            AugmentPanelTheme.Anchors(text.rectTransform,new Vector2(x,y),new Vector2(right,top));
            text.color=OctoberArt.Ink;text.enableWordWrapping=true;text.fontSizeMin=12;text.overflowMode=TextOverflowModes.Ellipsis;
            return text;
        }
        static void TopBox(RectTransform rect,Vector2 position,Vector2 size)
        {
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(.5f,1);
            rect.sizeDelta=size;rect.anchoredPosition=position;
        }
        public void SetExpanded(bool expanded){Expanded=expanded;}
        void Update()
        {
            Reveal=GamePreferences.Current.reducedMotion?(Expanded?1:0):Mathf.MoveTowards(Reveal,Expanded?1:0,Time.unscaledDeltaTime/.36f);
            ApplyReveal();
        }
        void ApplyReveal()
        {
            float eased=Reveal*Reveal*(3-2*Reveal);
            float visible=SheetHeight*eased;
            window.sizeDelta=new Vector2(Width,visible);window.gameObject.SetActive(Reveal>0);
            bool moving=Reveal>0&&Reveal<1;
            fold.gameObject.SetActive(moving);foldedEdge.gameObject.SetActive(Reveal<.99f);
            // A bent, shaded paper lip travels with the fold, rather than scaling quest lettering.
            fold.anchoredPosition=new Vector2(Width/2,-HeaderHeight+8-visible+4);
            fold.sizeDelta=new Vector2(Width-6,8+Mathf.Sin(Reveal*Mathf.PI)*26);
            fold.localRotation=Quaternion.Euler(0,0,Mathf.Sin(Reveal*Mathf.PI)*(Expanded?-1.4f:1.4f));
            sheetInteraction.interactable=sheetInteraction.blocksRaycasts=Expanded&&Reveal>=.999f;
        }
    }
}
