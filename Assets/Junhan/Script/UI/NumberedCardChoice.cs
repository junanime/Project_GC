using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vampire
{
    // Invoke the same button as a click only if its card wins the UI raycast:
    // opening the journal, settings or another reward on top takes priority.
    public sealed class NumberedCardChoice : MonoBehaviour
    {
        static int activeChoices;
        static int claimedFrame = -1;
        static KeyCode claimedKey;
        static readonly List<RaycastResult> hits = new List<RaycastResult>();
        Button action;
        RectTransform card;
        CanvasGroup keycap;
        int slot;
        int boundFrame;
        float deniedUntil;
        Graphic keyShape;
        static readonly Color keyFace = new Color(1f,.945f,.79f);
        public bool ShowingUnavailable => Time.unscaledTime < deniedUntil;

        public static bool CaptureInventory => activeChoices > 0 || claimedFrame == Time.frameCount;
        public static bool IsDigit(KeyCode key) =>
            (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha3) ||
            (key >= KeyCode.Keypad1 && key <= KeyCode.Keypad3);

        public static bool CapturesInventoryAction(UnityEngine.InputSystem.InputControl control)
        {
            if (CaptureInventory) return true;
            return claimedKey != KeyCode.None && control is UnityEngine.InputSystem.Controls.KeyControl pressed &&
                pressed.keyCode == GameInput.ResolveKeyboardKey(claimedKey) && ReservesKey(claimedKey);
        }

        public static bool ReservesKey(KeyCode key)
        {
            if (!IsDigit(key)) return false;
            if (CaptureInventory) return true;
            if (key != claimedKey) return false;
            if (GameInput.GetKey(key) && !GameInput.GetKeyDown(key)) return true;
            claimedKey = KeyCode.None;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            activeChoices = 0;
            claimedKey = KeyCode.None;
            claimedFrame = -1;
            hits.Clear();
        }

        public static void Bind(RectTransform card, Button button, int index, bool below, bool augment = false)
        {
            if (!GStarDemo.Enabled || GamePlatform.UsesTouchControls || card == null || button == null ||
                index < 0 || index >= 3) return;
            var choice = card.GetComponent<NumberedCardChoice>();
            if (choice == null) choice = card.gameObject.AddComponent<NumberedCardChoice>();
            choice.action = button;
            choice.card = card;
            choice.slot = index;
            choice.boundFrame = Time.frameCount;
            RectTransform holder;
            if (choice.keycap == null)
            {
                if (augment)
                {
                    var cap = InteractionKeycapGraphic.Create("Choice keycap", card, (index + 1).ToString(), 54);
                    holder = cap.rectTransform;
                    choice.keyShape = cap;
                }
                else
                {
                    holder = AugmentPanelTheme.Rect("Choice keycap", card);
                    var shape = holder.gameObject.AddComponent<KeyboardKeycapGraphic>();
                    shape.face = keyFace;
                    shape.raycastTarget = false;
                    choice.keyShape = shape;
                    var label = AugmentPanelTheme.Label("Key", holder, TrainingUITheme.Font, "", 20);
                    AugmentPanelTheme.Anchors(label.rectTransform, Vector2.zero, Vector2.one);
                    label.color = OctoberArt.Ink;
                    label.fontStyle = FontStyles.Bold;
                }
                choice.keycap = holder.gameObject.AddComponent<CanvasGroup>();
                choice.keycap.blocksRaycasts = false;
            }
            else holder = (RectTransform)choice.keycap.transform;
            holder.SetAsLastSibling();
            if (augment)
            {
                holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0,1);
                holder.anchoredPosition = new Vector2(9,-9);
            }
            else
            {
                holder.anchorMin = holder.anchorMax = below ? new Vector2(.5f,0) : new Vector2(.1f,.094f);
                holder.anchoredPosition = below ? new Vector2(0,-2) : Vector2.zero;
                holder.sizeDelta = new Vector2(34,26);
            }
            holder.GetComponentInChildren<TMP_Text>().text = (index + 1).ToString();
            choice.keycap.alpha = button.interactable ? 1f : .43f;
        }

        internal static bool TryShortcut(Button action, KeyCode key)
        {
            if (!GStarDemo.Enabled || GamePlatform.UsesTouchControls || !GameInput.GetKeyDown(key) ||
                GameInput.KeyboardBlocked || TutorialGuide.BlocksMenuInput || StageEntryLoading.IsLoading ||
                action == null || !action.isActiveAndEnabled || !action.IsInteractable() ||
                UnityEngine.InputSystem.Mouse.current?.leftButton.wasReleasedThisFrame == true ||
                !IsButtonOnTop(action, action.transform)) return false;
            action.onClick.Invoke();
            return true;
        }

        internal static bool IsButtonOnTop(Button action, Transform owner)
        {
            var events = EventSystem.current;
            if (events == null) return false;
            var canvas = action.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) return false;
            var buttonRect = (RectTransform)action.transform;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var point = RectTransformUtility.WorldToScreenPoint(camera, buttonRect.TransformPoint(buttonRect.rect.center));
            hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = point }, hits);
            bool onTop = hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(owner);
            hits.Clear();
            return onTop;
        }

        void OnEnable() { activeChoices++; }
        void OnDisable() { activeChoices = Mathf.Max(0, activeChoices - 1); claimedFrame = Time.frameCount; }

        void LateUpdate()
        {
            if (action == null) return;
            bool available = action.isActiveAndEnabled && action.IsInteractable();
            bool denied = ShowingUnavailable;
            keycap.alpha = available || denied ? 1f : .65f;
            if (denied)
                keycap.transform.localScale = Vector3.one * (1f + .13f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((deniedUntil-Time.unscaledTime)/.32f)));
            else if (deniedUntil > 0f)
            {
                deniedUntil = 0f;
                keycap.transform.localScale = Vector3.one;
                keyShape.canvasRenderer.SetColor(Color.white);
            }
            if (boundFrame == Time.frameCount || claimedFrame == Time.frameCount ||
                GameInput.KeyboardBlocked || TutorialGuide.BlocksMenuInput || StageEntryLoading.IsLoading) return;
            var key = KeyCode.Alpha1 + slot;
            if (!GameInput.GetKeyDown(key))
            {
                key = KeyCode.Keypad1 + slot;
                if (!GameInput.GetKeyDown(key)) return;
            }
            if (UnityEngine.InputSystem.Mouse.current?.leftButton.wasReleasedThisFrame == true) return;
            if (!IsButtonOnTop(action,card)) return;
            claimedKey = key;
            claimedFrame = Time.frameCount;
            if (available) action.onClick.Invoke();
            else if (!action.interactable)
            {
                deniedUntil = Time.unscaledTime + .32f;
                keyShape.canvasRenderer.SetColor(new Color(1f,.65f,.43f));
            }
        }
    }
}
