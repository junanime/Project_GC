using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Vampire
{
    // One interaction contract for mouse, gamepad and touch. The hit target never scales.
    public sealed class ApothecaryButtonFeedback : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        public Button button;
        public RectTransform visual;
        public Image body;
        public TitleMenuSurface sparkle;
        public bool chosen;
        public bool primary;
        bool hovered, pressed, focused;
        public bool IsPressed => pressed;
        public bool IsHighlighted => button != null && button.IsInteractable() && (hovered || focused || chosen);
        static bool Touch(PointerEventData e) => e is ExtendedPointerEventData input && input.pointerType == UIPointerType.Touch;
        public void OnPointerEnter(PointerEventData e)
        {
            if (!Touch(e) && button.IsInteractable())
            {
                hovered = true;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }
        public void OnPointerExit(PointerEventData e)
        {
            hovered = false; pressed = false; focused = false;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !button.IsInteractable()) return;
            pressed = true;
            if (Touch(e)) { hovered = false; focused = false; }
        }
        public void OnPointerUp(PointerEventData e)
        {
            pressed = false;
            if (Touch(e))
            {
                hovered = focused = false;
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
            }
        }
        public void OnSelect(BaseEventData e) { focused = true; }
        public void OnDeselect(BaseEventData e) { focused = false; pressed = false; }
        void OnDisable() { hovered = pressed = focused = false; if (visual != null) visual.localScale = Vector3.one; }
        void OnApplicationFocus(bool focus) { if (!focus) { hovered = pressed = focused = false; } }
        void Update()
        {
            if (button == null || visual == null) return;
            bool available = button.IsInteractable();
            if (!available) hovered = pressed = focused = false;
            bool active = IsHighlighted || pressed;
            bool reduced = GamePreferences.Current.reducedMotion;
            float target = !available || reduced ? 1 : pressed ? .96f : active ? 1.035f : 1;
            visual.localScale = Vector3.Lerp(visual.localScale, Vector3.one * target, 1 - Mathf.Exp(-20 * Time.unscaledDeltaTime));
            body.color = !available ? new Color(.65f,.62f,.59f) : pressed ? new Color(.83f,.70f,.68f) :
                active ? new Color(1,.91f,.72f) : Color.white;
            sparkle.SetActiveStyle(available && active && !reduced);
        }
    }
}
