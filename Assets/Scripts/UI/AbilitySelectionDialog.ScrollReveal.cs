using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public partial class AbilitySelectionDialog
    {
        readonly List<RectTransform> scrollRoots = new List<RectTransform>();
        Coroutine scrollRoutine;
        public bool ScrollRevealPending { get; private set; }
        int revealedFrame = -1;
        public bool CanSelectRevealedCard => !ScrollRevealPending && Time.frameCount > revealedFrame;

        void PrepareScrollReveal()
        {
            ClearScrollReveal();
            if (abilityCards == null || !abilityCards.Any(c=>c.gameObject.activeSelf && c.UsesNoblePanel)) return;
            ScrollRevealPending=true;
            foreach(var card in abilityCards)
            {
                if(!card.gameObject.activeSelf)continue;
                card.SetNobleArtVisible(false);
                var root=AugmentPanelTheme.Rect("Noble scroll seal",card.transform);
                AugmentPanelTheme.Anchors(root,Vector2.zero,Vector2.one);
                scrollRoots.Add(root);
                var blocker=root.gameObject.AddComponent<Image>();blocker.color=Color.clear;blocker.raycastTarget=true;
                root.gameObject.AddComponent<Button>().onClick.AddListener(RevealScrolls);
                var mask=AugmentPanelTheme.Rect("Paper mask",root);
                AugmentPanelTheme.Anchors(mask,Vector2.zero,Vector2.one);mask.gameObject.AddComponent<RectMask2D>();
                var paper=AugmentPanelTheme.Fill("Parchment",mask,Color.white);
                paper.sprite=CharacterNoblePanelArt.Frame;
                AugmentPanelTheme.Anchors(paper.rectTransform,Vector2.zero,Vector2.one);
                // An opaque backing also hides edge text beyond the art's alpha margin.
                var backing=AugmentPanelTheme.Fill("Opaque seal backing",mask,new Color(1,.98f,.90f));
                AugmentPanelTheme.Anchors(backing.rectTransform,new Vector2(.13f,.09f),new Vector2(.87f,.88f));backing.transform.SetAsFirstSibling();
                var rod=AugmentPanelTheme.Fill("Rolling jade rod",root,new Color(1,.79f,.29f));
                AugmentPanelTheme.Anchors(rod.rectTransform,new Vector2(.115f,.06f),new Vector2(.885f,.155f));
                rod.gameObject.SetActive(false); // Closed cover already contains the matching bottom rod.
                var glow=AugmentPanelTheme.Fill("Reveal light",root,new Color(1,.88f,.4f,0));glow.raycastTarget=false;
                AugmentPanelTheme.Anchors(glow.rectTransform,new Vector2(.13f,.09f),new Vector2(.87f,.88f));
                var label=AugmentPanelTheme.Label("Reveal hint",root,TrainingUITheme.Font,"클릭하여 고귀 증강 공개",16);
                label.color=OctoberArt.Ink;AugmentPanelTheme.Anchors(label.rectTransform,new Vector2(.2f,.20f),new Vector2(.8f,.28f));
            }
        }
        public void RevealScrolls()
        {
            if (!ScrollRevealPending || scrollRoutine != null) return;
            scrollRoutine=StartCoroutine(RollScrolls());
        }
        IEnumerator RollScrolls()
        {
            foreach (var card in abilityCards) if (card != null) card.SetNobleArtVisible(true);
            const float duration=.85f;
            for(float age=0;age<duration;age+=Time.unscaledDeltaTime)
            {
                float t=Mathf.SmoothStep(0,1,age/duration);
                foreach(var root in scrollRoots)
                {
                    var mask=(RectTransform)root.Find("Paper mask");
                    mask.anchorMin=new Vector2(0,t);mask.offsetMin=mask.offsetMax=Vector2.zero;
                    // Keep original paper dimensions while the lower clipping edge rises.
                    foreach(RectTransform paper in mask)
                    {
                        bool backing = paper.name == "Opaque seal backing";
                        paper.anchorMin=new Vector2(backing ? .13f : 0,1);paper.anchorMax=new Vector2(backing ? .87f : 1,1);
                        paper.pivot=new Vector2(.5f,1);paper.anchoredPosition=new Vector2(0,backing ? -root.rect.height*.12f : 0);
                        paper.sizeDelta=new Vector2(0,root.rect.height*(backing ? .79f : 1));
                    }
                    var rod=(RectTransform)root.Find("Rolling jade rod");
                    rod.gameObject.SetActive(t > .12f);
                    float rodY=Mathf.Lerp(.06f,.84f,t);
                    AugmentPanelTheme.Anchors(rod,new Vector2(.13f,rodY),new Vector2(.87f,rodY+.004f));
                    root.Find("Reveal hint").gameObject.SetActive(false);
                    root.Find("Reveal light").GetComponent<Image>().color=new Color(1,.88f,.4f,Mathf.Sin(t*Mathf.PI)*.25f);
                }
                yield return null;
            }
            foreach(var root in scrollRoots)
            {
                root.Find("Paper mask").gameObject.SetActive(false);
                root.Find("Reveal light").gameObject.SetActive(false);
                // The revealed shared frame already has its own top rod; retire the moving one.
                root.Find("Rolling jade rod").gameObject.SetActive(false);
                root.GetComponent<Image>().raycastTarget=false;root.GetComponent<Button>().interactable=false;
            }
            ScrollRevealPending=false;revealedFrame=Time.frameCount;scrollRoutine=null;
        }
        void ClearScrollReveal()
        {
            if(scrollRoutine!=null)StopCoroutine(scrollRoutine);scrollRoutine=null;
            foreach(var root in scrollRoots)if(root!=null){root.gameObject.SetActive(false);Destroy(root.gameObject);}
            scrollRoots.Clear();ScrollRevealPending=false;revealedFrame=-1;
            if (abilityCards != null) foreach (var card in abilityCards) if (card != null) card.SetNobleArtVisible(true);
        }
    }
}
