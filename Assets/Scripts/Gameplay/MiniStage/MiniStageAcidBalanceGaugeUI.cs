using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class MiniStageAcidBalanceGaugeUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("전체 UI Canvas입니다. 임시 UI 자동 생성 시 자동으로 연결됩니다.")]
        [SerializeField] private Canvas canvas;

        [Tooltip("UI 전체를 감싸는 루트 RectTransform입니다.")]
        [SerializeField] private RectTransform rootRect;

        [Tooltip("개체수 게이지의 배경 RectTransform입니다.")]
        [SerializeField] private RectTransform gaugeBackgroundRect;

        [Tooltip("목표 개체수 범위를 표시하는 영역 RectTransform입니다.")]
        [SerializeField] private RectTransform safeZoneRect;

        [Tooltip("현재 개체수를 표시하는 마커 RectTransform입니다.")]
        [SerializeField] private RectTransform currentMarkerRect;

        [Tooltip("현재 개체수 / 목표 범위 텍스트입니다.")]
        [SerializeField] private Text countText;

        [Tooltip("남은 시간 텍스트입니다.")]
        [SerializeField] private Text timerText;

        [Tooltip("성공/실패 결과 텍스트입니다.")]
        [SerializeField] private Text resultText;

        [Header("Gauge Settings")]
        [Tooltip("게이지가 표시할 최대 개체수입니다. 실제 몬스터 수가 이 값보다 높으면 마커가 맨 위에 붙습니다.")]
        [SerializeField] private int gaugeMaxCount = 40;

        [Tooltip("게이지 높이입니다.")]
        [SerializeField] private float gaugeHeight = 420f;

        [Tooltip("게이지 너비입니다.")]
        [SerializeField] private float gaugeWidth = 54f;

        [Tooltip("UI가 화면 오른쪽에서 얼마나 떨어질지 설정합니다.")]
        [SerializeField] private float rightOffset = 120f;

        [Tooltip("UI가 화면 중앙에서 위아래로 얼마나 이동할지 설정합니다.")]
        [SerializeField] private float verticalOffset = 0f;

        [Header("Temporary Colors")]
        [Tooltip("전체 패널 배경 색상입니다.")]
        [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.45f);

        [Tooltip("임시 게이지 배경 색상입니다.")]
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.95f);

        [Tooltip("목표 개체수 범위 색상입니다.")]
        [SerializeField] private Color safeZoneColor = new Color(0.25f, 1f, 0.25f, 0.9f);

        [Tooltip("현재 개체수 마커 색상입니다.")]
        [SerializeField] private Color markerColor = new Color(1f, 1f, 1f, 1f);

        [Tooltip("실패 범위일 때 현재 개체수 마커 색상입니다.")]
        [SerializeField] private Color dangerMarkerColor = new Color(1f, 0.15f, 0.15f, 1f);

        [Tooltip("게이지 테두리 색상입니다.")]
        [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.85f);

        private int safeMinCount;
        private int safeMaxCount;
        private bool initialized;

        public static MiniStageAcidBalanceGaugeUI CreateTemporaryGauge(int gaugeMaxCount)
        {
            GameObject canvasObject = new GameObject("MiniStageAcidBalanceGaugeCanvas");

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = new GameObject("AcidBalanceGaugeUI");
            rootObject.transform.SetParent(canvasObject.transform, false);

            MiniStageAcidBalanceGaugeUI gaugeUI = rootObject.AddComponent<MiniStageAcidBalanceGaugeUI>();
            gaugeUI.canvas = canvas;
            gaugeUI.gaugeMaxCount = gaugeMaxCount;
            gaugeUI.BuildTemporaryLayout(rootObject.transform);

            return gaugeUI;
        }

        public void Initialize(int safeMin, int safeMax, int maxCount)
        {
            safeMinCount = Mathf.Max(0, safeMin);
            safeMaxCount = Mathf.Max(safeMinCount, safeMax);
            gaugeMaxCount = Mathf.Max(1, maxCount);

            if (gaugeBackgroundRect == null ||
                safeZoneRect == null ||
                currentMarkerRect == null ||
                countText == null ||
                timerText == null ||
                resultText == null)
            {
                BuildTemporaryLayout(transform);
            }

            initialized = true;

            ApplySafeZone();
            SetResultText(string.Empty, Color.white);
            gameObject.SetActive(true);

            if (canvas != null)
            {
                canvas.gameObject.SetActive(true);
                canvas.sortingOrder = 5000;
            }
        }

        public void UpdateGauge(int currentCount, float remainingTime)
        {
            if (!initialized)
            {
                return;
            }

            int clampedCount = Mathf.Clamp(currentCount, 0, gaugeMaxCount);
            float normalized = Mathf.InverseLerp(0f, gaugeMaxCount, clampedCount);

            if (currentMarkerRect != null)
            {
                float y = -gaugeHeight * 0.5f + normalized * gaugeHeight;
                currentMarkerRect.anchoredPosition = new Vector2(0f, y);
            }

            bool inSafeRange = currentCount >= safeMinCount && currentCount <= safeMaxCount;

            Image markerImage = currentMarkerRect != null ? currentMarkerRect.GetComponent<Image>() : null;

            if (markerImage != null)
            {
                markerImage.color = inSafeRange ? markerColor : dangerMarkerColor;
            }

            if (countText != null)
            {
                countText.text = $"위산 슬라임\n{currentCount} / {safeMinCount}~{safeMaxCount}";
                countText.color = inSafeRange ? Color.white : new Color(1f, 0.35f, 0.35f, 1f);
            }

            if (timerText != null)
            {
                timerText.text = $"남은 시간\n{Mathf.CeilToInt(Mathf.Max(0f, remainingTime))}초";
            }
        }

        public void ShowResult(bool success, int finalCount, int safeMin, int safeMax)
        {
            if (success)
            {
                SetResultText($"성공!\n최종 {finalCount}마리", new Color(0.35f, 1f, 0.35f, 1f));
            }
            else
            {
                SetResultText($"실패\n최종 {finalCount}마리\n목표 {safeMin}~{safeMax}", new Color(1f, 0.25f, 0.25f, 1f));
            }
        }

        public void DestroyGauge()
        {
            if (canvas != null)
            {
                Destroy(canvas.gameObject);
                return;
            }

            Destroy(gameObject);
        }

        private void ApplySafeZone()
        {
            if (safeZoneRect == null)
            {
                return;
            }

            float minNormalized = Mathf.InverseLerp(0f, gaugeMaxCount, safeMinCount);
            float maxNormalized = Mathf.InverseLerp(0f, gaugeMaxCount, safeMaxCount);

            float zoneHeight = Mathf.Max(6f, (maxNormalized - minNormalized) * gaugeHeight);
            float centerNormalized = (minNormalized + maxNormalized) * 0.5f;
            float centerY = -gaugeHeight * 0.5f + centerNormalized * gaugeHeight;

            safeZoneRect.sizeDelta = new Vector2(gaugeWidth, zoneHeight);
            safeZoneRect.anchoredPosition = new Vector2(0f, centerY);
        }

        private void SetResultText(string message, Color color)
        {
            if (resultText == null)
            {
                return;
            }

            resultText.text = message;
            resultText.color = color;
        }

        private void BuildTemporaryLayout(Transform root)
        {
            ClearChildren(root);

            rootRect = root as RectTransform;

            if (rootRect == null)
            {
                rootRect = root.gameObject.AddComponent<RectTransform>();
            }

            rootRect.anchorMin = new Vector2(1f, 0.5f);
            rootRect.anchorMax = new Vector2(1f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(260f, 620f);
            rootRect.anchoredPosition = new Vector2(-rightOffset, verticalOffset);

            GameObject panelObject = CreateImageObject("Panel", root, panelColor);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(240f, 600f);
            panelRect.anchoredPosition = Vector2.zero;

            Text titleText = CreateTextObject(
                "TitleText",
                root,
                new Vector2(0f, gaugeHeight * 0.5f + 100f),
                new Vector2(230f, 44f),
                24,
                TextAnchor.MiddleCenter
            );
            titleText.text = "산도 조절";

            countText = CreateTextObject(
                "CountText",
                root,
                new Vector2(0f, gaugeHeight * 0.5f + 58f),
                new Vector2(230f, 70f),
                22,
                TextAnchor.MiddleCenter
            );

            GameObject borderObject = CreateImageObject("GaugeBorder", root, borderColor);
            RectTransform borderRect = borderObject.GetComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(0.5f, 0.5f);
            borderRect.anchorMax = new Vector2(0.5f, 0.5f);
            borderRect.pivot = new Vector2(0.5f, 0.5f);
            borderRect.sizeDelta = new Vector2(gaugeWidth + 10f, gaugeHeight + 10f);
            borderRect.anchoredPosition = Vector2.zero;

            GameObject backgroundObject = CreateImageObject("GaugeBackground", root, backgroundColor);
            gaugeBackgroundRect = backgroundObject.GetComponent<RectTransform>();
            gaugeBackgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
            gaugeBackgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            gaugeBackgroundRect.pivot = new Vector2(0.5f, 0.5f);
            gaugeBackgroundRect.sizeDelta = new Vector2(gaugeWidth, gaugeHeight);
            gaugeBackgroundRect.anchoredPosition = Vector2.zero;

            GameObject safeZoneObject = CreateImageObject("SafeZone", gaugeBackgroundRect.transform, safeZoneColor);
            safeZoneRect = safeZoneObject.GetComponent<RectTransform>();
            safeZoneRect.anchorMin = new Vector2(0.5f, 0.5f);
            safeZoneRect.anchorMax = new Vector2(0.5f, 0.5f);
            safeZoneRect.pivot = new Vector2(0.5f, 0.5f);
            safeZoneRect.sizeDelta = new Vector2(gaugeWidth, 80f);
            safeZoneRect.anchoredPosition = Vector2.zero;

            GameObject markerObject = CreateImageObject("CurrentMarker", gaugeBackgroundRect.transform, markerColor);
            currentMarkerRect = markerObject.GetComponent<RectTransform>();
            currentMarkerRect.anchorMin = new Vector2(0.5f, 0.5f);
            currentMarkerRect.anchorMax = new Vector2(0.5f, 0.5f);
            currentMarkerRect.pivot = new Vector2(0.5f, 0.5f);
            currentMarkerRect.sizeDelta = new Vector2(gaugeWidth + 42f, 10f);
            currentMarkerRect.anchoredPosition = Vector2.zero;

            timerText = CreateTextObject(
                "TimerText",
                root,
                new Vector2(0f, -gaugeHeight * 0.5f - 54f),
                new Vector2(230f, 60f),
                22,
                TextAnchor.MiddleCenter
            );

            resultText = CreateTextObject(
                "ResultText",
                root,
                new Vector2(0f, -gaugeHeight * 0.5f - 132f),
                new Vector2(240f, 94f),
                22,
                TextAnchor.MiddleCenter
            );
        }

        private void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }

        private GameObject CreateImageObject(string objectName, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(objectName);
            imageObject.transform.SetParent(parent, false);

            RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
            rectTransform.localScale = Vector3.one;

            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return imageObject;
        }

        private Text CreateTextObject(
            string objectName,
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            TextAnchor alignment
        )
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform = textObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;

            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;

            return text;
        }
    }
}