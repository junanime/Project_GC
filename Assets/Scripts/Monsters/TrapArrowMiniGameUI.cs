using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    /// <summary>
    /// 함정 몬스터 위에 뜨는 방향키 미니게임 UI입니다.
    /// 별도 이미지 에셋 없이 코드로 월드 스페이스 Canvas와 화살표 UI를 생성합니다.
    /// </summary>
    public class TrapArrowMiniGameUI : MonoBehaviour
    {
        private const int SlotSize = 72;
        private const int SlotGap = 18;

        private Image[] borderImages;
        private Image[] innerImages;
        private TextMeshProUGUI[] arrowTexts;

        private int sequenceLength;
        private int clearedCount;

        private float rainbowSpeed = 1.8f;
        private float wrongFlashEndTime;
        private int wrongFlashIndex = -1;

        public static TrapArrowMiniGameUI Create(
            Transform parent,
            char[] arrows,
            float yOffset,
            float worldScale,
            int sortingOrder)
        {
            if (parent == null || arrows == null || arrows.Length <= 0)
            {
                return null;
            }

            GameObject root = new GameObject("Trap Arrow Mini Game UI");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.up * yOffset;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * Mathf.Max(0.001f, worldScale);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(
                arrows.Length * SlotSize + (arrows.Length - 1) * SlotGap,
                SlotSize);

            TrapArrowMiniGameUI ui = root.AddComponent<TrapArrowMiniGameUI>();
            ui.Build(arrows);

            return ui;
        }

        private void Build(char[] arrows)
        {
            sequenceLength = arrows.Length;
            clearedCount = 0;

            borderImages = new Image[sequenceLength];
            innerImages = new Image[sequenceLength];
            arrowTexts = new TextMeshProUGUI[sequenceLength];

            RectTransform rootRect = GetComponent<RectTransform>();
            float totalWidth = sequenceLength * SlotSize + (sequenceLength - 1) * SlotGap;
            float startX = -totalWidth * 0.5f + SlotSize * 0.5f;

            for (int i = 0; i < sequenceLength; i++)
            {
                GameObject slot = new GameObject($"Arrow Slot {i + 1}");
                slot.transform.SetParent(transform, false);

                RectTransform slotRect = slot.AddComponent<RectTransform>();
                slotRect.sizeDelta = new Vector2(SlotSize, SlotSize);
                slotRect.anchorMin = new Vector2(0.5f, 0.5f);
                slotRect.anchorMax = new Vector2(0.5f, 0.5f);
                slotRect.pivot = new Vector2(0.5f, 0.5f);
                slotRect.anchoredPosition = new Vector2(startX + i * (SlotSize + SlotGap), 0f);

                Image border = slot.AddComponent<Image>();
                border.color = Color.white;
                border.raycastTarget = false;
                borderImages[i] = border;

                GameObject inner = new GameObject("Inner");
                inner.transform.SetParent(slot.transform, false);

                RectTransform innerRect = inner.AddComponent<RectTransform>();
                innerRect.anchorMin = Vector2.zero;
                innerRect.anchorMax = Vector2.one;
                innerRect.offsetMin = new Vector2(6f, 6f);
                innerRect.offsetMax = new Vector2(-6f, -6f);

                Image innerImage = inner.AddComponent<Image>();
                innerImage.color = new Color(0f, 0f, 0f, 0.82f);
                innerImage.raycastTarget = false;
                innerImages[i] = innerImage;

                GameObject textObject = new GameObject("Arrow Text");
                textObject.transform.SetParent(inner.transform, false);

                RectTransform textRect = textObject.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                TextMeshProUGUI arrowText = textObject.AddComponent<TextMeshProUGUI>();
                arrowText.text = arrows[i].ToString();
                arrowText.fontSize = 48f;
                arrowText.alignment = TextAlignmentOptions.Center;
                arrowText.color = Color.white;
                arrowText.raycastTarget = false;
                arrowTexts[i] = arrowText;

                Outline outline = textObject.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2f, -2f);
            }

            if (rootRect != null)
            {
                rootRect.sizeDelta = new Vector2(totalWidth, SlotSize);
            }

            RefreshClearedVisual();
        }

        private void Update()
        {
            for (int i = 0; i < sequenceLength; i++)
            {
                if (i < clearedCount)
                {
                    continue;
                }

                if (wrongFlashIndex == i && Time.time < wrongFlashEndTime)
                {
                    borderImages[i].color = Color.red;
                    continue;
                }

                float hue = Mathf.Repeat(Time.time * rainbowSpeed + i * 0.17f, 1f);
                borderImages[i].color = Color.HSVToRGB(hue, 0.95f, 1f);
            }

            if (wrongFlashIndex >= 0 && Time.time >= wrongFlashEndTime)
            {
                wrongFlashIndex = -1;
            }
        }

        public void MarkCleared(int index)
        {
            if (index < 0 || index >= sequenceLength)
            {
                return;
            }

            clearedCount = Mathf.Max(clearedCount, index + 1);
            RefreshClearedVisual();
        }

        public void ResetProgress()
        {
            clearedCount = 0;
            RefreshClearedVisual();
        }

        public void FlashWrong(int expectedIndex, float duration)
        {
            wrongFlashIndex = Mathf.Clamp(expectedIndex, 0, Mathf.Max(0, sequenceLength - 1));
            wrongFlashEndTime = Time.time + Mathf.Max(0.05f, duration);
        }

        private void RefreshClearedVisual()
        {
            for (int i = 0; i < sequenceLength; i++)
            {
                bool cleared = i < clearedCount;

                if (cleared)
                {
                    borderImages[i].color = new Color(0.2f, 0.2f, 0.2f, 0.35f);
                    innerImages[i].color = new Color(0f, 0f, 0f, 0.55f);
                    arrowTexts[i].color = new Color(1f, 1f, 1f, 0.25f);
                }
                else
                {
                    innerImages[i].color = new Color(0f, 0f, 0f, 0.82f);
                    arrowTexts[i].color = Color.white;
                }
            }
        }
    }
}