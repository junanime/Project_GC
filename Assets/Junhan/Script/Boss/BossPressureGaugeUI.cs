using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    /// <summary>
    /// 크리피커피 압력 게이지의 런타임 UI입니다.
    ///
    /// 기존 MiniStageAcidBalanceGaugeUI와 동일한 방향:
    /// - 별도 프리팹 없이 코드로 Canvas/UI 자동 생성
    /// - Screen Space Overlay
    /// - 1920x1080 기준 CanvasScaler
    /// - 화면 오른쪽 중앙 배치
    /// - 세로형 게이지
    ///
    /// BossPressureGaugeController가 보스 스폰 직후 생성하고 값을 갱신합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossPressureGaugeUI : MonoBehaviour
    {
        [Header("UI References")]

        [Tooltip("전체 압력 게이지 Canvas입니다. 런타임 자동 생성 시 자동 연결됩니다.")]
        [SerializeField]
        private Canvas canvas;

        [Tooltip("UI 전체 루트 RectTransform입니다.")]
        [SerializeField]
        private RectTransform rootRect;

        [Tooltip("게이지 배경 RectTransform입니다.")]
        [SerializeField]
        private RectTransform gaugeBackgroundRect;

        [Tooltip("현재 압력량을 아래에서 위로 채우는 Fill RectTransform입니다.")]
        [SerializeField]
        private RectTransform gaugeFillRect;

        [Tooltip("현재 압력 Fill Image입니다.")]
        [SerializeField]
        private Image gaugeFillImage;

        [Tooltip("현재 압력 퍼센트 텍스트입니다.")]
        [SerializeField]
        private Text percentText;

        [Tooltip("누적 피해량 / 발동 기준 피해량 텍스트입니다.")]
        [SerializeField]
        private Text damageText;

        [Tooltip("현재 압력 상태 텍스트입니다.")]
        [SerializeField]
        private Text stateText;

        [Tooltip("현재 보스 페이즈 텍스트입니다.")]
        [SerializeField]
        private Text phaseText;

        [Header("Gauge Layout")]

        [Tooltip("게이지 높이입니다. 기존 미니스테이지 세로 게이지와 동일하게 420을 기본값으로 사용합니다.")]
        [SerializeField]
        private float gaugeHeight = 420f;

        [Tooltip("게이지 너비입니다.")]
        [SerializeField]
        private float gaugeWidth = 60f;

        [Tooltip("UI가 화면 오른쪽 끝에서 얼마나 떨어질지 설정합니다.")]
        [SerializeField]
        private float rightOffset = 150f;

        [Tooltip("화면 중앙 기준 세로 위치 보정값입니다.")]
        [SerializeField]
        private float verticalOffset = 0f;

        [Header("Temporary Colors")]

        [Tooltip("전체 패널 배경 색상입니다.")]
        [SerializeField]
        private Color panelColor =
            new Color(0f, 0f, 0f, 0.75f);

        [Tooltip("빈 게이지 배경 색상입니다.")]
        [SerializeField]
        private Color backgroundColor =
            new Color(0.05f, 0.05f, 0.05f, 1f);

        [Tooltip("게이지 테두리 색상입니다.")]
        [SerializeField]
        private Color borderColor =
            new Color(1f, 1f, 1f, 0.9f);

        [Tooltip("압력이 낮을 때 Fill 색상입니다.")]
        [SerializeField]
        private Color lowPressureColor =
            new Color(1f, 0.88f, 0.18f, 1f);

        [Tooltip("압력이 중간 이상일 때 Fill 색상입니다.")]
        [SerializeField]
        private Color midPressureColor =
            new Color(1f, 0.48f, 0.08f, 1f);

        [Tooltip("압력이 위험 수준일 때 Fill 색상입니다.")]
        [SerializeField]
        private Color dangerPressureColor =
            new Color(1f, 0.12f, 0.08f, 1f);

        [Tooltip("압력 게이지가 가득 찼을 때 제목/상태 강조 색상입니다.")]
        [SerializeField]
        private Color fullPressureColor =
            new Color(1f, 0.2f, 0.2f, 1f);

        private Text titleText;
        private bool initialized;

        public static BossPressureGaugeUI CreateTemporaryGauge()
        {
            GameObject canvasObject =
                new GameObject(
                    "BossPressureGaugeCanvas");

            Canvas runtimeCanvas =
                canvasObject.AddComponent<Canvas>();

            runtimeCanvas.renderMode =
                RenderMode.ScreenSpaceOverlay;

            // 기존 MiniStageAcidBalanceGaugeUI와 동일한 높은 UI 우선순위.
            runtimeCanvas.sortingOrder =
                30000;

            CanvasScaler scaler =
                canvasObject.AddComponent
                <
                    CanvasScaler
                >();

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution =
                new Vector2(
                    1920f,
                    1080f);

            scaler.matchWidthOrHeight =
                0.5f;

            canvasObject.AddComponent
            <
                GraphicRaycaster
            >();

            BossPressureGaugeUI gaugeUI =
                canvasObject.AddComponent
                <
                    BossPressureGaugeUI
                >();

            gaugeUI.canvas =
                runtimeCanvas;

            gaugeUI.BuildTemporaryLayout();

            return gaugeUI;
        }

        public void Initialize(
            float thresholdDamage,
            int currentPhase)
        {
            if (canvas == null)
            {
                canvas =
                    GetComponent<Canvas>();
            }

            if (rootRect == null ||
                gaugeBackgroundRect == null ||
                gaugeFillRect == null ||
                gaugeFillImage == null ||
                percentText == null ||
                damageText == null ||
                stateText == null ||
                phaseText == null)
            {
                BuildTemporaryLayout();
            }

            initialized =
                true;

            if (canvas != null)
            {
                canvas.renderMode =
                    RenderMode.ScreenSpaceOverlay;

                canvas.sortingOrder =
                    30000;

                canvas.gameObject.SetActive(
                    true);
            }

            UpdateGauge(
                0f,
                0f,
                thresholdDamage,
                false,
                currentPhase);
        }

        public void UpdateGauge(
            float normalized,
            float currentPressureDamage,
            float thresholdDamage,
            bool isFull,
            int currentPhase)
        {
            if (!initialized)
            {
                return;
            }

            float clamped =
                Mathf.Clamp01(
                    normalized);

            UpdateFillSize(
                clamped);

            UpdateFillColor(
                clamped,
                isFull);

            if (percentText != null)
            {
                percentText.text =
                    $"{Mathf.RoundToInt(clamped * 100f)}%";

                percentText.color =
                    isFull
                        ? fullPressureColor
                        : Color.white;
            }

            if (damageText != null)
            {
                damageText.text =
                    $"누적 피해\n" +
                    $"{Mathf.Max(0f, currentPressureDamage):0.0} / " +
                    $"{Mathf.Max(0f, thresholdDamage):0.0}";
            }

            if (phaseText != null)
            {
                phaseText.text =
                    currentPhase > 0
                        ? $"PHASE {currentPhase}"
                        : "PHASE -";
            }

            if (stateText != null)
            {
                if (isFull)
                {
                    stateText.text =
                        "압력 한계 도달";

                    stateText.color =
                        fullPressureColor;
                }
                else if (clamped >= 0.8f)
                {
                    stateText.text =
                        "위험";

                    stateText.color =
                        dangerPressureColor;
                }
                else if (clamped >= 0.5f)
                {
                    stateText.text =
                        "압력 상승";

                    stateText.color =
                        midPressureColor;
                }
                else
                {
                    stateText.text =
                        "안정";

                    stateText.color =
                        lowPressureColor;
                }
            }

            if (titleText != null)
            {
                titleText.color =
                    isFull
                        ? fullPressureColor
                        : new Color(
                            1f,
                            0.95f,
                            0.7f,
                            1f);
            }
        }

        public void ShowWaitingForBossHealth(
            int currentPhase)
        {
            if (!initialized)
            {
                Initialize(
                    0f,
                    currentPhase);
            }

            UpdateFillSize(
                0f);

            if (percentText != null)
            {
                percentText.text =
                    "0%";
            }

            if (damageText != null)
            {
                damageText.text =
                    "보스 HP 동기화 대기";
            }

            if (stateText != null)
            {
                stateText.text =
                    "대기";

                stateText.color =
                    Color.white;
            }

            if (phaseText != null)
            {
                phaseText.text =
                    currentPhase > 0
                        ? $"PHASE {currentPhase}"
                        : "PHASE -";
            }
        }

        public void DestroyGauge()
        {
            Destroy(
                gameObject);
        }

        private void UpdateFillSize(
            float normalized)
        {
            if (gaugeFillRect == null)
            {
                return;
            }

            float fillHeight =
                gaugeHeight *
                Mathf.Clamp01(
                    normalized);

            gaugeFillRect.sizeDelta =
                new Vector2(
                    gaugeWidth,
                    fillHeight);

            // Bottom pivot이므로 anchoredPosition은 0을 유지하면
            // 아래에서 위로 채워집니다.
            gaugeFillRect.anchoredPosition =
                Vector2.zero;
        }

        private void UpdateFillColor(
            float normalized,
            bool isFull)
        {
            if (gaugeFillImage == null)
            {
                return;
            }

            if (isFull ||
                normalized >= 0.8f)
            {
                gaugeFillImage.color =
                    dangerPressureColor;

                return;
            }

            if (normalized >= 0.5f)
            {
                float t =
                    Mathf.InverseLerp(
                        0.5f,
                        0.8f,
                        normalized);

                gaugeFillImage.color =
                    Color.Lerp(
                        midPressureColor,
                        dangerPressureColor,
                        t);

                return;
            }

            float lowT =
                Mathf.InverseLerp(
                    0f,
                    0.5f,
                    normalized);

            gaugeFillImage.color =
                Color.Lerp(
                    lowPressureColor,
                    midPressureColor,
                    lowT);
        }

        private void BuildTemporaryLayout()
        {
            ClearChildren(
                transform);

            GameObject rootObject =
                new GameObject(
                    "PressureGaugeRoot");

            rootObject.transform.SetParent(
                transform,
                false);

            rootRect =
                rootObject.AddComponent
                <
                    RectTransform
                >();

            rootRect.anchorMin =
                new Vector2(
                    1f,
                    0.5f);

            rootRect.anchorMax =
                new Vector2(
                    1f,
                    0.5f);

            rootRect.pivot =
                new Vector2(
                    0.5f,
                    0.5f);

            rootRect.sizeDelta =
                new Vector2(
                    280f,
                    650f);

            rootRect.anchoredPosition =
                new Vector2(
                    -rightOffset,
                    verticalOffset);

            rootRect.localScale =
                Vector3.one;

            GameObject panelObject =
                CreateImageObject(
                    "Panel",
                    rootRect,
                    panelColor);

            RectTransform panelRect =
                panelObject.GetComponent
                <
                    RectTransform
                >();

            panelRect.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f);

            panelRect.anchorMax =
                new Vector2(
                    0.5f,
                    0.5f);

            panelRect.pivot =
                new Vector2(
                    0.5f,
                    0.5f);

            panelRect.sizeDelta =
                new Vector2(
                    260f,
                    630f);

            panelRect.anchoredPosition =
                Vector2.zero;

            titleText =
                CreateTextObject(
                    "TitleText",
                    rootRect,
                    new Vector2(
                        0f,
                        gaugeHeight * 0.5f +
                        105f),
                    new Vector2(
                        250f,
                        44f),
                    26,
                    TextAnchor.MiddleCenter);

            titleText.text =
                "압력 게이지";

            titleText.color =
                new Color(
                    1f,
                    0.95f,
                    0.7f,
                    1f);

            damageText =
                CreateTextObject(
                    "DamageText",
                    rootRect,
                    new Vector2(
                        0f,
                        gaugeHeight * 0.5f +
                        55f),
                    new Vector2(
                        250f,
                        70f),
                    22,
                    TextAnchor.MiddleCenter);

            GameObject borderObject =
                CreateImageObject(
                    "GaugeBorder",
                    rootRect,
                    borderColor);

            RectTransform borderRect =
                borderObject.GetComponent
                <
                    RectTransform
                >();

            borderRect.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f);

            borderRect.anchorMax =
                new Vector2(
                    0.5f,
                    0.5f);

            borderRect.pivot =
                new Vector2(
                    0.5f,
                    0.5f);

            borderRect.sizeDelta =
                new Vector2(
                    gaugeWidth + 14f,
                    gaugeHeight + 14f);

            borderRect.anchoredPosition =
                Vector2.zero;

            GameObject backgroundObject =
                CreateImageObject(
                    "GaugeBackground",
                    rootRect,
                    backgroundColor);

            gaugeBackgroundRect =
                backgroundObject.GetComponent
                <
                    RectTransform
                >();

            gaugeBackgroundRect.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f);

            gaugeBackgroundRect.anchorMax =
                new Vector2(
                    0.5f,
                    0.5f);

            gaugeBackgroundRect.pivot =
                new Vector2(
                    0.5f,
                    0.5f);

            gaugeBackgroundRect.sizeDelta =
                new Vector2(
                    gaugeWidth,
                    gaugeHeight);

            gaugeBackgroundRect.anchoredPosition =
                Vector2.zero;

            GameObject fillObject =
                CreateImageObject(
                    "PressureFill",
                    gaugeBackgroundRect,
                    lowPressureColor);

            gaugeFillRect =
                fillObject.GetComponent
                <
                    RectTransform
                >();

            gaugeFillImage =
                fillObject.GetComponent
                <
                    Image
                >();

            gaugeFillRect.anchorMin =
                new Vector2(
                    0.5f,
                    0f);

            gaugeFillRect.anchorMax =
                new Vector2(
                    0.5f,
                    0f);

            gaugeFillRect.pivot =
                new Vector2(
                    0.5f,
                    0f);

            gaugeFillRect.sizeDelta =
                new Vector2(
                    gaugeWidth,
                    0f);

            gaugeFillRect.anchoredPosition =
                Vector2.zero;

            percentText =
                CreateTextObject(
                    "PercentText",
                    rootRect,
                    Vector2.zero,
                    new Vector2(
                        180f,
                        60f),
                    30,
                    TextAnchor.MiddleCenter);

            stateText =
                CreateTextObject(
                    "StateText",
                    rootRect,
                    new Vector2(
                        0f,
                        -gaugeHeight * 0.5f -
                        58f),
                    new Vector2(
                        250f,
                        50f),
                    24,
                    TextAnchor.MiddleCenter);

            phaseText =
                CreateTextObject(
                    "PhaseText",
                    rootRect,
                    new Vector2(
                        0f,
                        -gaugeHeight * 0.5f -
                        108f),
                    new Vector2(
                        250f,
                        42f),
                    20,
                    TextAnchor.MiddleCenter);
        }

        private void ClearChildren(
            Transform root)
        {
            for (int i =
                     root.childCount - 1;
                 i >= 0;
                 i--)
            {
                Destroy(
                    root.GetChild(i)
                        .gameObject);
            }
        }

        private GameObject CreateImageObject(
            string objectName,
            RectTransform parent,
            Color color)
        {
            GameObject imageObject =
                new GameObject(
                    objectName);

            imageObject.transform.SetParent(
                parent,
                false);

            RectTransform rectTransform =
                imageObject.AddComponent
                <
                    RectTransform
                >();

            rectTransform.localScale =
                Vector3.one;

            Image image =
                imageObject.AddComponent
                <
                    Image
                >();

            image.color =
                color;

            image.raycastTarget =
                false;

            return imageObject;
        }

        private Text CreateTextObject(
            string objectName,
            RectTransform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject textObject =
                new GameObject(
                    objectName);

            textObject.transform.SetParent(
                parent,
                false);

            RectTransform rectTransform =
                textObject.AddComponent
                <
                    RectTransform
                >();

            rectTransform.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f);

            rectTransform.anchorMax =
                new Vector2(
                    0.5f,
                    0.5f);

            rectTransform.pivot =
                new Vector2(
                    0.5f,
                    0.5f);

            rectTransform.sizeDelta =
                size;

            rectTransform.anchoredPosition =
                anchoredPosition;

            rectTransform.localScale =
                Vector3.one;

            Text text =
                textObject.AddComponent
                <
                    Text
                >();

            text.font =
                GetBuiltInFont();

            text.fontSize =
                fontSize;

            text.alignment =
                alignment;

            text.color =
                Color.white;

            text.raycastTarget =
                false;

            return text;
        }

        private Font GetBuiltInFont()
        {
            Font font =
                Resources.GetBuiltinResource
                <
                    Font
                >(
                    "LegacyRuntime.ttf");

            if (font == null)
            {
                font =
                    Resources.GetBuiltinResource
                    <
                        Font
                    >(
                        "Arial.ttf");
            }

            return font;
        }
    }
}
