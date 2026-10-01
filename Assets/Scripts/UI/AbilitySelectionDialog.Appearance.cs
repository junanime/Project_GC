using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public partial class AbilitySelectionDialog
    {
        RectTransform panelContent, panelDimmer;
        TMP_FontAsset panelFont;
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
            var fontSource = GetComponentInChildren<TextMeshProUGUI>(true);
            var config = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            var font = config != null ? config.font : fontSource != null ? fontSource.font : TMP_Settings.defaultFontAsset;
            if (font != null && font.sourceFontFile != null)
            {
                panelFont = TMP_FontAsset.CreateFontAsset(font.sourceFontFile);
                font = panelFont;
            }
            // Retire the old stretched header and title, retaining serialized gameplay references.
            foreach (Transform child in transform)
                if (child != abilityCardsParent && (rerollButton == null || child != rerollButton.transform)) child.gameObject.SetActive(false);
            var oldBackground = GetComponent<Image>(); if (oldBackground != null) oldBackground.enabled = false;
            var dimmer = AugmentPanelTheme.Fill("Training dimmer", transform, new Color(0,0,0,.70f));
            dimmer.raycastTarget = true; panelDimmer = dimmer.rectTransform; panelDimmer.SetAsFirstSibling();
            panelContent = AugmentPanelTheme.Rect("Training content", transform);
            AugmentPanelTheme.Box(panelContent, Vector2.zero, new Vector2(1280,720));
            var banner=AugmentPanelTheme.Fill("Training banner",panelContent,Color.white);
            banner.sprite=OctoberArt.Get("OctoberUI/Banner");banner.preserveAspect=true;
            AugmentPanelTheme.Box(banner.rectTransform,new Vector2(0,287),new Vector2(400,138));
            var title=AugmentPanelTheme.Label("Training title",panelContent,font,"단 련",42);
            title.fontStyle=FontStyles.Bold;title.color=new Color(1,.95f,.7f);title.outlineColor=OctoberArt.Ink;title.outlineWidth=.15f;
            AugmentPanelTheme.Box(title.rectTransform,new Vector2(0,306),new Vector2(300,62));
            var subtitle=AugmentPanelTheme.Label("Training subtitle",panelContent,font,"강해지고 싶은 자 나에게로..",20);
            subtitle.color=Color.white;
            AugmentPanelTheme.Box(subtitle.rectTransform,new Vector2(0,244),new Vector2(490,30));
            abilityCardsParent.SetParent(panelContent,false);
            AugmentPanelTheme.Box((RectTransform)abilityCardsParent,new Vector2(0,-6),new Vector2(890,454));
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
                AugmentPanelTheme.Anchors(light.rectTransform,Vector2.zero,Vector2.one);
                light.button=rerollButton;light.hideIcon=true;light.decorationOnly=true;light.raycastTarget=false;
                rerollButtonText=AugmentPanelTheme.Label("Reroll label",visual,font,"새로고침",26);
                AugmentPanelTheme.Anchors(rerollButtonText.rectTransform,Vector2.zero,Vector2.one);
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
                noble ? new Vector2(0,-38) : new Vector2(0,-6),
                noble ? new Vector2(1124,510) : new Vector2(890,454));
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

        void OnDestroy()
        {
            if (panelFont == null) return;
            foreach (var atlas in panelFont.atlasTextures) if (atlas != null) Destroy(atlas);
            Destroy(panelFont.material); Destroy(panelFont);
        }
    }
}
