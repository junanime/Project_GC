using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Vampire
{
    /// <summary>One first-discovery card per content, shared by weapons, events and field monsters.</summary>
    public sealed class TutorialGuide : MonoBehaviour
    {
        [Serializable] public sealed class Entry
        {
            public string id;
            public string kind;
            public string title;
            public string what;
            public string tip;
            public string effect;
        }
        [Serializable] private sealed class Catalog { public Entry[] entries; }

        public static TutorialGuide Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.open;
        public static bool BlocksMenuInput => Instance != null && (Instance.open || Instance.closedAtFrame == Time.frameCount);
        public static string SeenKey(string id) => "tutorial.first-discovery.v1." + id;

        private readonly Queue<Entry> pending = new Queue<Entry>();
        private readonly HashSet<string> pendingIds = new HashSet<string>();
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private LevelManager level;
        private bool ready;
        private bool open;
        private int closedAtFrame = -1;
        private float previousTimeScale;
        private GameObject canvasObject;
        private RawImage preview;
        private Text titleText, kindText, whatText, tipText, tipHeadingText;
        private RectTransform hintBand;
        private Texture2D activeSheet;
        private float clipStart;
        private Font font;
        private const int ClipColumns = 4;
        private const int ClipRows = 3;
        private const int ClipFrames = 12;
        private const float ClipFrameSeconds = .115f;

        public void Initialize(LevelManager owner)
        {
            if (Instance != null && Instance != this) Destroy(Instance);
            ready = false;
            pending.Clear();
            pendingIds.Clear();
            if (canvasObject != null) Destroy(canvasObject);
            Instance = this;
            level = owner;
            var manifest = Resources.Load<TextAsset>("TutorialClips/catalog");
            if (manifest == null) { Debug.LogError("[TutorialGuide] Missing TutorialClips/catalog.json"); return; }
            var catalog = JsonUtility.FromJson<Catalog>(manifest.text);
            if (catalog == null || catalog.entries == null) return;
            foreach (var entry in catalog.entries)
                if (entry != null && !string.IsNullOrEmpty(entry.id)) entries[entry.id] = entry;
            font = Resources.Load<Font>("TrainingUI/Cafe24Ssurround");
            BuildCanvas();
            ready = true;
        }

        public static void QueueWeapon(SyringeSpecialAugmentAbility.SpecialAugmentType type) => Queue("weapon/" + type);
        public static void QueueStageEvent(string id) => Queue("event/" + id);
        public static void QueueBloodClot() => Queue("mechanic/blood-clot");
        public static void QueueMonster(MonsterBlueprint blueprint)
        {
            if (blueprint == null) return;
            string id = MonsterId(((UnityEngine.Object)blueprint).name);
            if (id != null) Queue("monster/" + id);
        }
        public static string MonsterId(string assetName)
        {
            switch (assetName)
            {
                case "Sniper Monster": return "sniper";
                case "NutritionThiefBacteria Monster Blueprint": return "nutrition-thief";
                case "Acid Spore Blueprint": return "acid-spore";
                case "SugarCube_Normal_Body": return "sugar-cube";
                case "MonsterBuffer_MoveSpeed_Blueprint": return "speed-buffer";
                case "MonsterDebuffer_AttackSpeed_Blueprint": return "attack-debuffer";
                case "TreasureRunner Monster Blueprint": return "treasure-runner";
                case "SugarCube_Elite_Body": return "elite-sugar-cube";
                case "Trap Monster": return "trap";
                case "MonsterBuffer_DamageReduction_Blueprint": return "armor-buffer";
                default: return null;
            }
        }
        private static void Queue(string id)
        {
            if (Instance == null || !Instance.ready) return;
            Instance.Enqueue(id);
        }
        private void Enqueue(string id)
        {
            if (!entries.TryGetValue(id, out Entry entry) || PlayerPrefs.GetInt(SeenKey(id), 0) != 0 || !pendingIds.Add(id)) return;
            pending.Enqueue(entry);
        }

        private void Update()
        {
            if (!ready || level == null) return;
            if (open)
            {
                if (activeSheet != null && preview != null)
                {
                    int frame = (int)((Time.unscaledTime - clipStart) / ClipFrameSeconds) % ClipFrames;
                    preview.uvRect = new Rect((frame % ClipColumns) / (float)ClipColumns,
                        (ClipRows - 1 - frame / ClipColumns) / (float)ClipRows,
                        1f / ClipColumns, 1f / ClipRows);
                }
                if (GameInput.GetKeyDown(KeyCode.Escape)) Close();
                return;
            }
            if (pending.Count == 0 || level.IsLevelEnded || level.IsRunFlowPaused || Time.timeScale <= 0f) return;
            if (level.EntityManager != null && level.EntityManager.AbilitySelectionDialog != null &&
                level.EntityManager.AbilitySelectionDialog.MenuOpen) return;
            if (ApothecaryUI.Instance != null && ApothecaryUI.Instance.Page != "hud") return;
            Show(pending.Dequeue());
        }

        private void Show(Entry entry)
        {
            pendingIds.Remove(entry.id);
            activeSheet = Resources.Load<Texture2D>("TutorialClips/Sheets/" + entry.effect);
            if (activeSheet == null)
            {
                Debug.LogError("[TutorialGuide] Missing clip sheet: " + entry.effect);
                return;
            }
            titleText.text = entry.title;
            kindText.text = entry.kind == "weapon" ? "새 무기" : entry.kind == "event" ? "스테이지 이벤트" :
                entry.kind == "mechanic" ? "필드 기믹" : "특수 몬스터";
            whatText.text = entry.what;
            tipText.text = entry.tip;
            tipHeadingText.text = entry.kind == "mechanic" ? "혈전 과충전" : "혁이의 대응";
            whatText.fontSize = entry.kind == "mechanic" ? 18 : 23;
            tipText.fontSize = entry.kind == "mechanic" ? 16 : 21;
            var mechanic = entry.kind == "mechanic";
            SetVertical(whatText.rectTransform, mechanic ? .575f : .52f, mechanic ? .69f : .675f);
            SetVertical(hintBand, mechanic ? .48f : .405f, mechanic ? .56f : .495f);
            SetVertical(tipHeadingText.rectTransform, mechanic ? .485f : .411f, mechanic ? .555f : .487f);
            SetVertical(tipText.rectTransform, mechanic ? .255f : .266f, mechanic ? .47f : .395f);
            preview.texture = activeSheet;
            // The first rendered frame can precede Update after pausing the game.
            // Never expose the complete 4x3 atlas in that frame.
            preview.uvRect = new Rect(0f, (ClipRows - 1f) / ClipRows,
                1f / ClipColumns, 1f / ClipRows);
            clipStart = Time.unscaledTime;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            open = true;
            canvasObject.SetActive(true);
            PlayerPrefs.SetInt(SeenKey(entry.id), 1);
            PlayerPrefs.Save();
        }
        public void Replay() { clipStart = Time.unscaledTime; }
        public void Close()
        {
            if (!open) return;
            open = false;
            closedAtFrame = Time.frameCount;
            canvasObject.SetActive(false);
            activeSheet = null;
            if (level != null && !level.IsLevelEnded) Time.timeScale = previousTimeScale;
        }
        private void OnDestroy()
        {
            if (open) Time.timeScale = previousTimeScale;
            if (Instance == this) Instance = null;
        }

        private void BuildCanvas()
        {
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            canvasObject = new GameObject("First Discovery Tutorial", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            var root = canvasObject.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            RectImage(root, "Dim", 0, 0, 1, 1, new Color(.04f,.025f,.06f,.78f));
            var card = Rect("Cream card", root, .105f, .09f, .895f, .91f);
            var panel = card.gameObject.AddComponent<Image>();
            panel.sprite = Resources.Load<Sprite>("OctoberUI/Panel");
            panel.color = Color.white; panel.raycastTarget = true;
            var ribbon = RectImage(card, "Pink ribbon", .325f, .875f, .675f, 1.025f, new Color(.93f,.49f,.56f));
            ribbon.sprite = Resources.Load<Sprite>("OctoberUI/Banner");
            kindText = Label(ribbon.rectTransform, "", .08f, .16f, .92f, .84f, 31, Color.white);
            titleText = Label(card, "", .10f, .785f, .90f, .87f, 37, new Color(.23f,.12f,.15f));
            RectImage(card, "Preview border", .055f, .235f, .615f, .78f, new Color(.38f,.18f,.27f));
            var previewFrame = RectImage(card, "Preview", .068f, .251f, .602f, .765f, new Color(.59f,.27f,.35f));
            preview = Rect("Looping gameplay demonstration", previewFrame.rectTransform, .015f, .025f, .985f, .975f).gameObject.AddComponent<RawImage>();
            preview.raycastTarget = false;
            RectImage(card, "Description paper", .63f, .235f, .945f, .78f, new Color(1f,.963f,.895f));
            Label(card, "어떤 일이 일어나나요?", .655f, .705f, .922f, .76f, 22, new Color(.52f,.27f,.32f));
            whatText = Label(card, "", .658f, .52f, .915f, .675f, 23, new Color(.20f,.14f,.16f));
            hintBand = RectImage(card, "Mint hint", .65f, .405f, .925f, .495f, new Color(.72f,.88f,.76f)).rectTransform;
            tipHeadingText = Label(card, "혁이의 대응", .664f, .411f, .911f, .487f, 25, new Color(.17f,.31f,.25f));
            tipText = Label(card, "", .66f, .266f, .915f, .395f, 21, new Color(.20f,.14f,.16f));
            ButtonAt(card, "확인", .37f, .07f, .63f, .19f, new Color(.53f,.83f,.72f), Close);
            ButtonAt(card, "다시 보기", .77f, .095f, .925f, .18f, new Color(.99f,.94f,.85f), Replay);
            canvasObject.SetActive(false);
        }
        private static void SetVertical(RectTransform rect, float bottom, float top)
        {
            rect.anchorMin = new Vector2(rect.anchorMin.x, bottom);
            rect.anchorMax = new Vector2(rect.anchorMax.x, top);
        }
        private static RectTransform Rect(string name, Transform parent, float x, float y, float right, float top)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x, y); rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        private static Image RectImage(Transform parent, string name, float x, float y, float right, float top, Color color)
        {
            var image = Rect(name, parent, x, y, right, top).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }
        private Text Label(Transform parent, string value, float x, float y, float right, float top, int size, Color color)
        {
            var label = Rect("Text " + value, parent, x, y, right, top).gameObject.AddComponent<Text>();
            label.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.text = value; label.fontSize = size; label.color = color;
            label.fontStyle = FontStyle.Bold; label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }
        private void ButtonAt(Transform parent, string text, float x, float y, float right, float top, Color color, Action action)
        {
            var body = RectImage(parent, "Button " + text, x, y, right, top, color);
            var button = body.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            Label(body.transform, text, .05f, .05f, .95f, .95f, 26, new Color(.18f,.20f,.19f));
            button.onClick.AddListener(() => action());
        }
    }
}
