using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Clone only the player's health-meter graphics: identical heart, pixel frame,
    // red fill and screen size, without dash/skill bars or combat bindings.
    [DefaultExecutionOrder(1400)]
    public sealed class OralGuardHealthBar : MonoBehaviour
    {
        OralGuardEscort escort;
        Character player;
        Canvas canvas;
        RectTransform surface, meter, fill;
        bool visible = true;
        public void Bind(OralGuardEscort owner, Character character) { escort = owner; player = character; }
        public void SetVisible(bool value) { visible = value; if (canvas != null) canvas.enabled = value; }
        void LateUpdate()
        {
            if (escort == null || player == null) return;
            if (canvas == null)
            {
                var source = player.GetComponent<PlayerCombatBars>()?.HealthRect;
                if (source == null) return;
                var go = new GameObject("수호대 공용 체력 80", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                go.transform.SetParent(transform, false);
                canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 189;
                var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                surface = (RectTransform)go.transform;
                meter = Instantiate(source, surface, false); meter.name = "Escort health meter";
                var barrier = meter.Find("Relic barrier"); if (barrier != null) Destroy(barrier.gameObject);
                fill = meter.Find("Charge track/Charge 1/Fill") as RectTransform;
                foreach (var image in meter.GetComponentsInChildren<Image>()) image.raycastTarget = false;
            }
            var camera = Camera.main; var ui = ApothecaryUI.Instance;
            canvas.enabled = visible && camera != null && escort.isActiveAndEnabled && !escort.Complete && !escort.Failed &&
                !escort.Suspended && (ui == null || ui.Page == "hud") && Time.timeScale > 0;
            if (!canvas.enabled) return;
            var screen = camera.WorldToScreenPoint(escort.HealthAnchor);
            if (screen.z <= 0) { canvas.enabled = false; return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, screen, null, out var point);
            meter.anchoredPosition = point;
            if (fill != null) fill.anchorMax = new Vector2(escort.Health.Current / OralGuardHealth.Maximum, 1);
        }
    }
}
