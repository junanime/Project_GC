using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Vampire
{
    // Read-only mouse help: never changes pause state or forwards a gameplay click.
    public sealed class HudTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        Func<string> heading, description;
        bool hovered;
        float entered;
        GameObject overlay;
        RectTransform card, canvasRect;
        TMP_Text title, body;
        static HudTooltip owner;

        public static void Bind(GameObject target, Func<string> title, Func<string> body)
        {
            var tooltip = target.GetComponent<HudTooltip>() ?? target.AddComponent<HudTooltip>();
            tooltip.heading = title; tooltip.description = body;
        }
        public void OnPointerEnter(PointerEventData e)
        {
            if (GamePlatform.UsesTouchControls || MobileGameplayInput.Active ||
                e is ExtendedPointerEventData extended && extended.pointerType == UIPointerType.Touch) return;
            hovered = true; entered = Time.unscaledTime;
        }
        public void OnPointerExit(PointerEventData e) => Hide();
        public void OnPointerDown(PointerEventData e) => Hide();
        void OnDisable() => Hide();
        void Hide()
        {
            hovered = false;
            if (overlay != null) { overlay.SetActive(false); Destroy(overlay); overlay = null; }
            if (owner == this) owner = null;
        }
        void Update()
        {
            if (!hovered || Mouse.current == null || Time.unscaledTime - entered < .22f) return;
            if (TutorialGuide.IsOpen) { Hide(); return; }
            if (overlay == null) Create();
            title.text = heading?.Invoke() ?? "";
            body.text = description?.Invoke() ?? "";
            float height = Mathf.Clamp(body.GetPreferredValues(body.text, 324, 0).y + 88, 150, 400);
            card.sizeDelta = new Vector2(360, height);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Mouse.current.position.ReadValue(), null, out var p);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Screen.safeArea.min, null, out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Screen.safeArea.max, null, out var max);
            p += new Vector2(18, 20);
            if (p.x + 360 > max.x - 8) p.x -= 396;
            if (p.y + height > max.y - 8) p.y -= height + 40;
            card.anchoredPosition = new Vector2(Mathf.Clamp(p.x, min.x + 8, max.x - 368), Mathf.Clamp(p.y, min.y + 8, max.y - height - 8));
        }
        void Create()
        {
            if (owner != null && owner != this) owner.Hide();
            owner = this;
            overlay = new GameObject("Read only HUD help", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 600;
            var scaler = overlay.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            overlay.GetComponent<CanvasGroup>().blocksRaycasts = false;
            canvasRect = (RectTransform)overlay.transform;
            card = AugmentPanelTheme.Rect("Help card", canvasRect); card.anchorMin = card.anchorMax = new Vector2(.5f, .5f); card.pivot = Vector2.zero;
            var bg = card.gameObject.AddComponent<Image>(); bg.color = new Color(1, .96f, .86f, .98f); bg.raycastTarget = false;
            var outline = card.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.22f,.13f,.14f); outline.effectDistance = new Vector2(2,-2);
            title = AugmentPanelTheme.Label("Title", card, TrainingUITheme.Font, "", 23);
            title.fontStyle = FontStyles.Bold; title.enableAutoSizing = false; title.alignment = TextAlignmentOptions.MidlineLeft;
            title.rectTransform.anchorMin = new Vector2(0,1); title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(18,-54); title.rectTransform.offsetMax = new Vector2(-18,-10);
            body = AugmentPanelTheme.Label("Description", card, TrainingUITheme.Font, "", 19);
            body.enableAutoSizing = true; body.fontSizeMin = 16; body.fontSizeMax = 19; body.alignment = TextAlignmentOptions.TopLeft;
            AugmentPanelTheme.Anchors(body.rectTransform, Vector2.zero, Vector2.one);
            body.rectTransform.offsetMin = new Vector2(18,16); body.rectTransform.offsetMax = new Vector2(-18,-60);
            title.color = body.color = OctoberArt.Ink; title.raycastTarget = body.raycastTarget = false;
        }
    }
}
