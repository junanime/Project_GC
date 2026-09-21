using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Vampire
{
    public sealed class MobileTouchControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public Action<Vector2> Changed;
        public Action Pressed;
        public Action Released;
        public bool Joystick;
        public float Radius = 90f;
        public RectTransform Knob;
        private int? pointer;

        public void OnPointerDown(PointerEventData data)
        {
            if (pointer.HasValue || Time.timeScale <= 0) return;
            pointer = data.pointerId;
            data.useDragThreshold = false;
            Pressed?.Invoke();
            OnDrag(data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (pointer != data.pointerId || !Joystick) return;
            if (Time.timeScale <= 0) { Cancel(); return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
                data.position, data.pressEventCamera, out var position);
            var vector = Vector2.ClampMagnitude(position / Mathf.Max(1, Radius), 1);
            if (Knob != null) Knob.anchoredPosition = vector * Radius;
            Changed?.Invoke(vector.sqrMagnitude < .01f ? Vector2.zero : vector);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (pointer == data.pointerId) Cancel();
        }

        public void Cancel()
        {
            if (!pointer.HasValue) return;
            pointer = null;
            if (Knob != null) Knob.anchoredPosition = Vector2.zero;
            Changed?.Invoke(Vector2.zero);
            Released?.Invoke();
        }

        private void OnDisable() => Cancel();
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }
    }
}
