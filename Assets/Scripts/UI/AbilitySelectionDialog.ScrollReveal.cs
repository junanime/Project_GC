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
                var root=AugmentPanelTheme.Rect("Noble scroll seal",card.transform);
                AugmentPanelTheme.Anchors(root,Vector2.zero,Vector2.one);
                scrollRoots.Add(root);
                var blocker=root.gameObject.AddComponent<Image>();blocker.color=Color.clear;blocker.raycastTarget=true;
                root.gameObject.AddComponent<Button>().onClick.AddListener(RevealScrolls);
                var mask=AugmentPanelTheme.Rect("Paper mask",root);
                AugmentPanelTheme.Anchors(mask,Vector2.zero,Vector2.one);mask.gameObject.AddComponent<RectMask2D>();
                var paper=AugmentPanelTheme.Fill("Parchment",mask,Color.white);
                paper.sprite=AcidToadArt.Frame("NobleScroll",0);
                AugmentPanelTheme.Anchors(paper.rectTransform,Vector2.zero,Vector2.one);
                // An opaque backing also hides edge text beyond the art's alpha margin.
                var backing=AugmentPanelTheme.Fill("Opaque seal backing",mask,new Color(.93f,.84f,.65f));
                AugmentPanelTheme.Anchors(backing.rectTransform,Vector2.zero,Vector2.one);backing.transform.SetAsFirstSibling();
                var rod=AugmentPanelTheme.Fill("Rolling jade rod",root,Color.white);rod.sprite=AcidToadArt.Frame("NobleScroll",1);
                AugmentPanelTheme.Anchors(rod.rectTransform,new Vector2(0,0),new Vector2(1,.095f));
                var glow=AugmentPanelTheme.Fill("Reveal light",root,new Color(1,.88f,.4f,0));glow.raycastTarget=false;
                AugmentPanelTheme.Anchors(glow.rectTransform,Vector2.zero,Vector2.one);
                var label=AugmentPanelTheme.Label("Reveal hint",root,TrainingUITheme.Font,"클릭하여 세 족자 공개",20);
                label.color=OctoberArt.Ink;AugmentPanelTheme.Anchors(label.rectTransform,new Vector2(.08f,.08f),new Vector2(.92f,.17f));
            }
        }
        public void RevealScrolls()
        {
            if (!ScrollRevealPending || scrollRoutine != null) return;
            scrollRoutine=StartCoroutine(RollScrolls());
        }
        IEnumerator RollScrolls()
        {
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
                    {paper.anchorMin=new Vector2(0,1);paper.anchorMax=Vector2.one;paper.pivot=new Vector2(.5f,1);paper.anchoredPosition=Vector2.zero;paper.sizeDelta=new Vector2(0,root.rect.height);}
                    var rod=(RectTransform)root.Find("Rolling jade rod");
                    AugmentPanelTheme.Anchors(rod,new Vector2(0,t*.905f),new Vector2(1,t*.905f+.095f));
                    root.Find("Reveal hint").gameObject.SetActive(false);
                    root.Find("Reveal light").GetComponent<Image>().color=new Color(1,.88f,.4f,Mathf.Sin(t*Mathf.PI)*.25f);
                }
                yield return null;
            }
            foreach(var root in scrollRoots)
            {
                root.Find("Paper mask").gameObject.SetActive(false);
                root.Find("Reveal light").gameObject.SetActive(false);
                AugmentPanelTheme.Anchors((RectTransform)root.Find("Rolling jade rod"),new Vector2(0,.905f),new Vector2(1,1));
                root.GetComponent<Image>().raycastTarget=false;root.GetComponent<Button>().interactable=false;
            }
            ScrollRevealPending=false;revealedFrame=Time.frameCount;scrollRoutine=null;
        }
        void ClearScrollReveal()
        {
            if(scrollRoutine!=null)StopCoroutine(scrollRoutine);scrollRoutine=null;
            foreach(var root in scrollRoots)if(root!=null){root.gameObject.SetActive(false);Destroy(root.gameObject);}
            scrollRoots.Clear();ScrollRevealPending=false;revealedFrame=-1;
        }
    }
}
