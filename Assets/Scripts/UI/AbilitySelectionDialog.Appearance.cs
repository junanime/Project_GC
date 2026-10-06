using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public partial class AbilitySelectionDialog
    {
        RectTransform panelContent, panelDimmer;
        readonly Vector3[] panelScreenCorners = new Vector3[4];

        void InstallPanelTheme()
        {
            if (panelContent != null || abilityCardsParent == null) return;
            var root = (RectTransform)transform;
            AugmentPanelTheme.Anchors(root, Vector2.zero, Vector2.one);
            var modalCanvas = GetComponent<Canvas>();
            if (modalCanvas == null) modalCanvas = gameObject.AddComponent<Canvas>();
            modalCanvas.overrideSorting = true; modalCanvas.sortingOrder = 30000;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            // Retire the old stretched header and title, retaining serialized gameplay references.
            foreach (Transform child in transform)
                if (child != abilityCardsParent && (rerollButton == null || child != rerollButton.transform)) child.gameObject.SetActive(false);
            var oldBackground = GetComponent<Image>(); if (oldBackground != null) oldBackground.enabled = false;
            var dimmer = AugmentPanelTheme.Fill("Training dimmer", transform, new Color(0,0,0,.70f));
            dimmer.raycastTarget = true; panelDimmer = dimmer.rectTransform; panelDimmer.SetAsFirstSibling();
            panelContent = AugmentPanelTheme.Rect("Training content", transform);
            AugmentPanelTheme.Box(panelContent, Vector2.zero, new Vector2(1280,720));
            var banner=AugmentPanelTheme.Fill("Training banner",panelContent,Color.white);
            // Approved lettering, ribbon, leaves and subtitle plaque form one piece of artwork.
            banner.sprite=OctoberArt.Get("TrainingUI/TrainingBanner");banner.preserveAspect=true;
            AugmentPanelTheme.Box(banner.rectTransform,new Vector2(0,283),new Vector2(392,137));
            abilityCardsParent.SetParent(panelContent,false);
            AugmentPanelTheme.Box((RectTransform)abilityCardsParent,new Vector2(0,-14),new Vector2(890,454));
            var parentImage = abilityCardsParent.GetComponent<Image>(); if(parentImage != null) parentImage.enabled = false;
            var fitter = abilityCardsParent.GetComponent<ContentSizeFitter>(); if(fitter != null) fitter.enabled = false;
            var layout = abilityCardsParent.GetComponent<HorizontalLayoutGroup>();
            if(layout == null) layout = abilityCardsParent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.enabled = true; layout.spacing = 22; layout.padding = new RectOffset();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            if (rerollButton != null)
            {
                rerollButton.transform.SetParent(panelContent,false);
                AugmentPanelTheme.Box((RectTransform)rerollButton.transform,new Vector2(0,-291),new Vector2(310,66));
                foreach (Transform child in rerollButton.transform) child.gameObject.SetActive(false);
                rerollButtonImage = rerollButton.GetComponent<Image>();
                rerollButtonImage.color=Color.clear;
                var visual=AugmentPanelTheme.Rect("Visual",rerollButton.transform);
                AugmentPanelTheme.Anchors(visual,Vector2.zero,Vector2.one);
                var body=visual.gameObject.AddComponent<Image>();body.sprite=OctoberArt.Button(false);body.type=Image.Type.Sliced;body.pixelsPerUnitMultiplier=4;body.raycastTarget=false;
                var light=AugmentPanelTheme.Rect("Orbiting edge light",visual).gameObject.AddComponent<TitleMenuSurface>();
                if(light.GetComponent<CanvasRenderer>()==null)light.gameObject.AddComponent<CanvasRenderer>();
                AugmentPanelTheme.Anchors(light.rectTransform,Vector2.zero,Vector2.one);
                light.button=rerollButton;light.hideIcon=true;light.decorationOnly=true;light.raycastTarget=false;
                rerollButtonText=AugmentPanelTheme.Label("Reroll label",visual,TrainingUITheme.Font,"새로고침",26);
                AugmentPanelTheme.Anchors(rerollButtonText.rectTransform,new Vector2(.22f,0),new Vector2(.95f,1));
                var refresh=AugmentPanelTheme.Rect("Refresh symbol",visual).gameObject.AddComponent<TitleMenuSurface>();
                if(refresh.GetComponent<CanvasRenderer>()==null)refresh.gameObject.AddComponent<CanvasRenderer>();
                AugmentPanelTheme.Anchors(refresh.rectTransform,new Vector2(.08f,.16f),new Vector2(.23f,.84f));
                refresh.button=rerollButton;refresh.index=1;refresh.hideIcon=false;refresh.decorationOnly=true;refresh.raycastTarget=false;
                refresh.centerIcon=true;refresh.iconColor=OctoberArt.Ink;
                rerollButtonText.fontStyle=FontStyles.Bold;rerollButtonText.color=OctoberArt.Ink;
                var feedback=rerollButton.gameObject.AddComponent<ApothecaryButtonFeedback>();
                feedback.button=rerollButton;feedback.visual=visual;feedback.body=body;feedback.sparkle=light;feedback.label=rerollButtonText;
                rerollButton.targetGraphic=rerollButtonImage;rerollButton.transition=Selectable.Transition.None;
                rerollAvailableImageColor=rerollUsedImageColor=Color.clear;
                rerollAvailableTextColor=rerollUsedTextColor=OctoberArt.Ink;

            }
            UpdatePanelLayout();
        }

        void LateUpdate() { if (panelContent != null) UpdatePanelLayout(); }

        void UpdatePanelLayout()
        {
            bool noble = false;
            foreach (Transform child in abilityCardsParent)
            {
                var card = child.GetComponent<AbilityCard>();
                if (child.gameObject.activeSelf && card != null && card.UsesNoblePanel) { noble=true; break; }
            }
            AugmentPanelTheme.Box((RectTransform)abilityCardsParent,
                noble ? new Vector2(0,-48) : new Vector2(0,-14),
                noble ? new Vector2(1124,540) : new Vector2(890,454));
            var root = (RectTransform)transform;
            float scale = Mathf.Min(root.rect.width/1280f, root.rect.height/720f);
            panelContent.localScale = Vector3.one * Mathf.Max(.01f,scale);
            // The modal content follows the scene safe area, but the black scrim covers the whole screen.
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var screen = (RectTransform)canvas.rootCanvas.transform;
            screen.GetWorldCorners(panelScreenCorners);
            Vector2 min = root.InverseTransformPoint(panelScreenCorners[0]), max = root.InverseTransformPoint(panelScreenCorners[2]);
            AugmentPanelTheme.Box(panelDimmer,(min+max)*.5f,max-min);
        }

    }
}
