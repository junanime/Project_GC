using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class StageEventToastUI : MonoBehaviour
    {
        // Retained serialized references disable the old scene panel without editing scene data.
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private float visibleDuration = 2f;
        [SerializeField] private float fadeInDuration = .15f;
        [SerializeField] private float fadeOutDuration = .35f;
        [SerializeField] private bool debugLog;
        private readonly Queue<string> pending = new Queue<string>();
        private RectTransform root, titleRect;
        private CanvasGroup bannerGroup;
        private Text title, instruction;
        private Image icon, underline;
        private StageEventCornerArt art;
        private LevelManager level;
        private float age;
        private bool showing;

        private void Awake() => HideLegacyPanel();
        private void HideLegacyPanel()
        {
            if (canvasGroup == null && panelRoot != null) canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (canvasGroup != null) { canvasGroup.alpha = 0; canvasGroup.blocksRaycasts = false; canvasGroup.interactable = false; }
            if (messageText != null) messageText.enabled = false;
            if (panelRoot != null && panelRoot != gameObject) panelRoot.SetActive(false);
        }

        public void Show(string message)
        {
            HideLegacyPanel();
            if (!isActiveAndEnabled || string.IsNullOrWhiteSpace(message)) return;
            EnsureBanner();
            pending.Enqueue(message.Replace(" 이벤트가 시작됐습니다!", "").Trim());
            if (!showing) Next();
            if (debugLog) Debug.Log("[StageEventToastUI] " + message, this);
        }

        private void Next()
        {
            if (pending.Count == 0) { showing = false; bannerGroup.alpha = 0; return; }
            string text = pending.Dequeue();
            StageEventVisualKind kind = Kind(text);
            title.text = text;
            instruction.text = text == "제산 반응 발생" ? "곧 생성되는 거품을 찾아 이동하세요" : StageEventCornerArt.Instruction(kind);
            icon.sprite = art != null && art.Frames(kind) != null && art.Frames(kind).Length > 0 ? art.Frames(kind)[0] : null;
            icon.enabled = icon.sprite != null;
            underline.color = StageEventCornerArt.Accent(kind);
            age = 0; showing = true;
        }

        private static StageEventVisualKind Kind(string text)
        {
            if (text.Contains("감염")) return StageEventVisualKind.Infection;
            if (text.Contains("골드")) return StageEventVisualKind.Gold;
            if (text.Contains("위산")) return StageEventVisualKind.AcidRain;
            if (text.Contains("역류")) return StageEventVisualKind.Reflux;
            if (text.Contains("연동")) return StageEventVisualKind.Drift;
            if (text.Contains("커피")) return StageEventVisualKind.Coffee;
            return StageEventVisualKind.Antacid;
        }

        private void Update()
        {
            if (root == null) return;
            if (level == null) level = FindObjectOfType<LevelManager>();
            if (level != null && level.IsLevelEnded) { pending.Clear(); showing = false; bannerGroup.alpha = 0; return; }
            if (MiniStageRuntimeState.IsInsideMiniStage) { bannerGroup.alpha = 0; return; }
            if (!showing) return;
            if (level == null || !level.IsRunFlowPaused) age += Time.deltaTime;
            float enter = Mathf.Max(.01f, fadeInDuration), leave = Mathf.Max(.01f, fadeOutDuration);
            float end = enter + Mathf.Max(1.6f, visibleDuration) + leave;
            bannerGroup.alpha = Mathf.Min(Mathf.Clamp01(age / enter), Mathf.Clamp01((end - age) / leave));
            titleRect.localScale = Vector3.one * Mathf.Lerp(.94f, 1, Mathf.SmoothStep(0,1,age / enter));
            if (age >= end) Next();
        }

        private void EnsureBanner()
        {
            if (root != null) return;
            root = StageEventCornerUI.CreateCanvas("Stage event title (no translucent box)", 65);
            bannerGroup = root.GetComponent<CanvasGroup>();
            art = Resources.Load<StageEventCornerArt>("StageEventCornerArt");
            titleRect = StageEventCornerUI.Rect("Event heading", root, new Vector2(.5f,1), new Vector2(0,-151), new Vector2(760,105));
            icon = StageEventCornerUI.Picture("Event icon",titleRect,new Vector2(70,67),new Vector2(74,74),null);
            title = Label("Title",titleRect,new Vector2(408,72),new Vector2(610,58),42);
            title.color = new Color(1,.96f,.81f);
            instruction = Label("Action hint",titleRect,new Vector2(400,17),new Vector2(700,35),25);
            instruction.color = new Color(1,.97f,.88f);
            var line = StageEventCornerUI.Rect("Accent underline",titleRect,Vector2.zero,new Vector2(404,43),new Vector2(590,3));
            underline = line.gameObject.AddComponent<Image>(); underline.raycastTarget = false;
        }

        private static Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var rect = StageEventCornerUI.Rect(name,parent,Vector2.zero,position,size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.Load<Font>("TrainingUI/Cafe24Ssurround");
            text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = fontSize-5; text.resizeTextMaxSize = fontSize;
            var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.16f,.075f,.055f,1);
            outline.effectDistance = new Vector2(2,-2); outline.useGraphicAlpha = true;
            return text;
        }

        private void OnDisable() { pending.Clear(); showing = false; if (bannerGroup != null) bannerGroup.alpha = 0; }
        private void OnDestroy() { if (root != null) Destroy(root.gameObject); }
    }
}
