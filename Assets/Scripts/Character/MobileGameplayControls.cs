using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    [DisallowMultipleComponent]
    public sealed class MobileGameplayControls : MonoBehaviour
    {
        [SerializeField] private bool simulateInEditor;
        [SerializeField, Range(.2f, 1f)] private float opacity = .65f;
        [Header("Mobile-only HUD placement")]
        [SerializeField] private RectTransform minimapHud, inventoryHud;
        [SerializeField] private Vector2 minimapOffset = new Vector2(32,-130);
        [SerializeField] private Vector2 inventoryOffset = new Vector2(-50,-150);
        private sealed class HudPlacement
        {
            public RectTransform rect;
            public Vector2 min,max,pivot,position;
        }
        private readonly List<HudPlacement> hudPlacements = new List<HudPlacement>();
        private Character character;
        private GameObject canvasObject;
        private GameObject gameplayRoot, arrowRoot;
        private GameObject mapClose;
        private MobileTouchControl[] controls;
        private PauseMenu pauseMenu;
        private MapPanelToggle map;

        public static void Ensure(Character character)
        {
            if (!Application.isMobilePlatform || character.GetComponent<MobileGameplayControls>() != null) return;
            character.gameObject.AddComponent<MobileGameplayControls>();
        }

        private void Start()
        {
            character = GetComponent<Character>();
            if (character == null || (!Application.isMobilePlatform && !simulateInEditor)) { enabled = false; return; }
            foreach (var old in FindObjectsOfType<TouchJoystick>()) old.gameObject.SetActive(false);
            pauseMenu = FindObjectOfType<PauseMenu>(true);
            map = FindObjectOfType<MapPanelToggle>(true);
            MobileGameplayInput.Active = true;
            Build();
            PlaceHud(minimapHud!=null ? minimapHud : FindHud("MiniMapRoot"),new Vector2(0,1),minimapOffset);
            PlaceHud(inventoryHud!=null ? inventoryHud : FindHud("Inventory Buttons"),Vector2.one,inventoryOffset);
        }

        private static RectTransform FindHud(string name)
        {
            foreach(var rect in FindObjectsOfType<RectTransform>(true))
                if(rect.name==name) return rect;
            return null;
        }

        private void PlaceHud(RectTransform rect,Vector2 anchor,Vector2 offset)
        {
            if(rect==null)return;
            hudPlacements.Add(new HudPlacement {rect=rect,min=rect.anchorMin,max=rect.anchorMax,pivot=rect.pivot,position=rect.anchoredPosition});
            rect.anchorMin=rect.anchorMax=rect.pivot=anchor;
            rect.anchoredPosition=offset;
        }

        private RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private MobileTouchControl Control(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, bool stick = false)
        {
            var rect = Rect(label, parent, anchor, position, size);
            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(.07f, .11f, .18f, opacity);
            var textRect = Rect("Label", rect, new Vector2(.5f,.5f), Vector2.zero, size - Vector2.one * 10);
            var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 24;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            var input = rect.gameObject.AddComponent<MobileTouchControl>();
            input.Joystick = stick;
            if (stick)
            {
                input.Radius = size.x * .34f;
                input.Knob = Rect("Knob", rect, new Vector2(.5f,.5f), Vector2.zero, size * .24f);
                var image = input.Knob.gameObject.AddComponent<Image>();
                image.color = new Color(.9f, .94f, 1, .55f);
                image.raycastTarget = false;
            }
            return input;
        }

        private void Build()
        {
            canvasObject = new GameObject("Mobile Gameplay Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var safe = Rect("Safe Area", canvasObject.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>().ResetSafeArea();
            var root = Rect("Gameplay", safe, Vector2.zero, Vector2.zero, Vector2.zero);
            root.anchorMax = Vector2.one;
            gameplayRoot = root.gameObject;
            var move = Control("MOVE", root, Vector2.zero, new Vector2(180,180), Vector2.one * 260, true);
            move.Changed = value => character.Move(value);
            var aim = Control("AIM / CHARGE", root, Vector2.right, new Vector2(-180,180), Vector2.one * 260, true);
            aim.Changed = value => { if (value != Vector2.zero) MobileGameplayInput.Aim = value.normalized; };
            aim.Pressed = () => MobileGameplayInput.ChargeHeld = true;
            aim.Released = () => MobileGameplayInput.ChargeHeld = false;
            Control("DASH", root, Vector2.right, new Vector2(-410,150), Vector2.one * 150).Pressed = () => character.TryDash();
            Control("USE", root, Vector2.right, new Vector2(-180,420), Vector2.one * 140).Pressed = MobileGameplayInput.RequestInteraction;

            var arrows = Rect("Trap Escape", root, new Vector2(.5f,0), new Vector2(0,195), new Vector2(400,300));
            arrowRoot = arrows.gameObject;
            string[] labels = { "↑", "↓", "←", "→" };
            Vector2[] positions = { new Vector2(0,95), new Vector2(0,-95), new Vector2(-130,0), new Vector2(130,0) };
            for (int i=0; i<4; i++)
            {
                int direction = i;
                Control(labels[i], arrows, new Vector2(.5f,.5f), positions[i], Vector2.one * 115).Pressed =
                    () => { if (character.IsTrapBound) MobileGameplayInput.RequestArrow(direction); };
            }
            arrowRoot.SetActive(false);
            // Existing pause/menu UI remains responsible for resuming, preventing
            // gameplay controls from consuming taps on upgrade cards while paused.
            if (pauseMenu != null)
                Control("PAUSE", root, Vector2.one, new Vector2(-90,-70), new Vector2(150,90)).Pressed = pauseMenu.PlayPause;
            if (map != null)
            {
                Control("MAP", root, Vector2.one, new Vector2(-260,-70), new Vector2(150,90)).Pressed = map.Toggle;
                var closeRect=Rect("Close mobile map",safe,Vector2.one,new Vector2(-105,-70),new Vector2(190,90));
                closeRect.gameObject.AddComponent<Image>().color=new Color(.07f,.11f,.18f,.95f);
                var closeButton=closeRect.gameObject.AddComponent<Button>();closeButton.onClick.AddListener(map.Toggle);
                var label=Rect("Label",closeRect,new Vector2(.5f,.5f),Vector2.zero,new Vector2(180,80)).gameObject.AddComponent<TextMeshProUGUI>();
                label.text="CLOSE MAP";label.fontSize=24;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
                mapClose=closeRect.gameObject;mapClose.SetActive(false);
            }
            controls = canvasObject.GetComponentsInChildren<MobileTouchControl>(true);
        }

        private void Update()
        {
            if (gameplayRoot == null) return;
            bool mapOpen=map!=null && map.IsOpen();
            if(mapClose!=null) mapClose.SetActive(mapOpen);
            bool canPlay = Time.timeScale > 0 && character.CurrentHealth > 0 && !mapOpen;
            if (!canPlay) ResetGestures();
            gameplayRoot.SetActive(canPlay);
            arrowRoot.SetActive(canPlay && character.IsTrapBound);
        }

        private void ResetGestures()
        {
            if (controls != null) foreach (var control in controls) control.Cancel();
            MobileGameplayInput.ClearGestures();
            if (character != null) character.Move(Vector2.zero);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            ResetGestures();
            if (Time.timeScale > 0 && pauseMenu != null) pauseMenu.PlayPause();
        }

        private void OnApplicationFocus(bool focused) { if (!focused) ResetGestures(); }
        private void OnDisable()
        {
            ResetGestures();
            MobileGameplayInput.Active = false;
            foreach(var placement in hudPlacements)
                if(placement.rect!=null)
                {
                    placement.rect.anchorMin=placement.min;placement.rect.anchorMax=placement.max;
                    placement.rect.pivot=placement.pivot;placement.rect.anchoredPosition=placement.position;
                }
            hudPlacements.Clear();
            if (canvasObject != null) Destroy(canvasObject);
        }
    }
}
