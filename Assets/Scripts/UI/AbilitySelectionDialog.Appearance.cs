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
            var title = AugmentPanelTheme.Label("Training title", panelContent, font, "단 련", 52);
            title.fontStyle = FontStyles.Bold;
            AugmentPanelTheme.Box(title.rectTransform, new Vector2(0,300), new Vector2(500,64));
            var subtitle = AugmentPanelTheme.Label("Training subtitle", panelContent, font, "강해지고 싶은 자 나에게로..", 23);
            AugmentPanelTheme.Box(subtitle.rectTransform, new Vector2(0,246), new Vector2(520,38));
            for (int sign=-1; sign<=1; sign+=2)
            {
                var line = AugmentPanelTheme.Fill("Header jade rule", panelContent, AugmentPanelTheme.Jade);
                AugmentPanelTheme.Box(line.rectTransform, new Vector2(sign*275,246), new Vector2(52,1.5f));
                var diamond = AugmentPanelTheme.Fill("Header jade seal", panelContent, AugmentPanelTheme.Jade);
                AugmentPanelTheme.Box(diamond.rectTransform, new Vector2(sign*239,246), new Vector2(5,5));
                diamond.rectTransform.localRotation = Quaternion.Euler(0,0,45);
            }
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
                rerollButtonImage.sprite = Resources.Load<Sprite>("AugmentPanels/Reroll");
                rerollButtonImage.type = Image.Type.Simple; rerollButtonImage.preserveAspect = false;
                rerollButton.targetGraphic = rerollButtonImage;
                rerollUsedImageColor = new Color(.45f,.45f,.45f,1);
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
