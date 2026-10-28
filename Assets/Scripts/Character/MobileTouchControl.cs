using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;

namespace Vampire
{
    public sealed class MobileTouchControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler, ICancelHandler
    {
        public Action<Vector2> Changed;
        public Action Pressed;
        public Action Released;
        public bool Joystick;
        public float Radius = 90f;
        public RectTransform Knob;
        private int? pointer;
        private InputDevice contactDevice;
        private int contactTouchId;
        private PointerEventData.InputButton contactButton;
        private Camera contactCamera;
        private readonly List<RaycastResult> touchHits = new List<RaycastResult>();
        private int? browserContact;

        public bool BrowserTouch(int id, int phase, Vector2 position)
        {
            if (phase == 0)
            {
                if (!Joystick || !isActiveAndEnabled || Time.timeScale <= 0 || pointer.HasValue || !IsTopTouchTarget(position, out var camera)) return false;
                browserContact = id;
                pointer = int.MinValue + id;
                contactDevice = null;
                contactCamera = camera;
                Pressed?.Invoke();
            }
            if (browserContact != id) return false;
            if (phase == 2 || !isActiveAndEnabled || Time.timeScale <= 0) Cancel();
            else ApplyPosition(position, contactCamera);
            return true;
        }

        private bool IsTopTouchTarget(Vector2 position, out Camera camera)
        {
            camera = null;
            if (EventSystem.current == null) return false;
            touchHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, touchHits);
            if (touchHits.Count == 0 || touchHits[0].gameObject.GetComponentInParent<MobileTouchControl>() != this) return false;
            camera = touchHits[0].module.eventCamera;
            return true;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (pointer.HasValue && !ContactIsHeld(out _)) Cancel();
            if (pointer.HasValue || Time.timeScale <= 0) return;
            pointer = data.pointerId;
            contactCamera = data.pressEventCamera;
            contactButton = data.button;
            if (data is ExtendedPointerEventData extended)
            {
                // UI pointerId combines device and touch IDs; it is not a finger ID.
                contactDevice = extended.device is Touchscreen || extended.device is Mouse || extended.device is Pen
                    ? extended.device : null;
                contactTouchId = extended.touchId;
            }
            data.useDragThreshold = false;
            Pressed?.Invoke();
            OnDrag(data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (pointer != data.pointerId || !Joystick) return;
            if (Time.timeScale <= 0) { Cancel(); return; }
            ApplyPosition(data.position, data.pressEventCamera);
        }

        private void ApplyPosition(Vector2 screenPosition, Camera eventCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
                screenPosition, eventCamera, out var position);
            var vector = Vector2.ClampMagnitude(position / Mathf.Max(1, Radius), 1);
            if (Knob != null) Knob.anchoredPosition = vector * Radius;
            Changed?.Invoke(vector.sqrMagnitude < .01f ? Vector2.zero : vector);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (pointer == data.pointerId) Cancel();
        }

        public void OnEndDrag(PointerEventData data) => OnPointerUp(data);
        public void OnCancel(BaseEventData data) => Cancel();

        private bool ContactIsHeld(out Vector2 position)
        {
            position = default;
            if (contactDevice == null) return true; // Non-InputSystem UI keeps its event-based release path.
            if (!contactDevice.added) return false;
            if (contactDevice is Touchscreen screen)
            {
                foreach (var touch in screen.touches)
                    if (touch.touchId.ReadValue() == contactTouchId && touch.press.isPressed)
                    { position = touch.position.ReadValue(); return true; }
                return false;
            }
            if (contactDevice is Mouse mouse)
            {
                position = mouse.position.ReadValue();
                return contactButton == PointerEventData.InputButton.Right ? mouse.rightButton.isPressed :
                    contactButton == PointerEventData.InputButton.Middle ? mouse.middleButton.isPressed : mouse.leftButton.isPressed;
            }
            var pen = (Pen)contactDevice;
            position = pen.position.ReadValue(); return pen.tip.isPressed;
        }

        private void LateUpdate()
        {
            // A browser can deliver a Touchscreen state without a matching UI
            // pointer-down (focus/orientation transitions). The joystick also
            // reads fresh contacts directly, while respecting the same UI mask.
            if (!pointer.HasValue && Joystick && GamePlatform.UsesTouchControls && Time.timeScale > 0)
                TryBeginTouch();
            if (!pointer.HasValue) return;
            // A dropped pointer-up/end-drag must never leave movement latched.
            // Inspect only the owning contact: another finger can still be holding a skill.
            if (Time.timeScale <= 0 || !ContactIsHeld(out var position)) { Cancel(); return; }
            if (Joystick && contactDevice != null) ApplyPosition(position, contactCamera);
        }

        private void TryBeginTouch()
        {
            var screen = Touchscreen.current;
            var events = EventSystem.current;
            if (screen == null || events == null) return;
            foreach (var touch in screen.touches)
            {
                if (!touch.press.wasPressedThisFrame || !touch.press.isPressed) continue;
                Vector2 position = touch.position.ReadValue();
                if (!IsTopTouchTarget(position, out var camera)) continue;
                pointer = touch.touchId.ReadValue();
                contactTouchId = touch.touchId.ReadValue();
                contactDevice = screen;
                contactCamera = camera;
                Pressed?.Invoke();
                ApplyPosition(position, contactCamera);
                return;
            }
        }

        public void Cancel()
        {
            if (!pointer.HasValue) return;
            pointer = null;
            browserContact = null;
            contactDevice = null;
            contactTouchId = 0;
            contactCamera = null;
            if (Knob != null) Knob.anchoredPosition = Vector2.zero;
            Changed?.Invoke(Vector2.zero);
            Released?.Invoke();
        }

        private void OnDisable() => Cancel();
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }
    }
}
