using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    /// <summary>
    /// 함정 몬스터 위에 뜨는 방향키 미니게임 UI입니다.
    /// 별도 이미지 에셋 없이 코드로 월드 스페이스 Canvas와
    /// 도트(픽셀) 블록을 조합한 8비트 스타일 화살표를 생성합니다.
    /// </summary>
    public class TrapArrowMiniGameUI : MonoBehaviour
    {
        [Tooltip("화살표 한 칸의 기준 크기입니다.")]
        private const int SlotSize = 104;

        [Tooltip("화살표 사이 간격입니다.")]
        private const int SlotGap = 20;

        private PixelArrowDisplay[] arrowDisplays;
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
            arrowDisplays = new PixelArrowDisplay[sequenceLength];

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
                slotRect.anchoredPosition = new Vector2(
                    startX + i * (SlotSize + SlotGap),
                    0f);

                GameObject arrowRoot = new GameObject("Pixel Arrow");
                arrowRoot.transform.SetParent(slot.transform, false);

                RectTransform arrowRect = arrowRoot.AddComponent<RectTransform>();
                arrowRect.anchorMin = Vector2.zero;
                arrowRect.anchorMax = Vector2.one;
                arrowRect.offsetMin = Vector2.zero;
                arrowRect.offsetMax = Vector2.zero;

                PixelArrowDisplay display = arrowRoot.AddComponent<PixelArrowDisplay>();
                display.Build(ConvertToDirection(arrows[i]));
                arrowDisplays[i] = display;
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
                if (arrowDisplays == null || i >= arrowDisplays.Length || arrowDisplays[i] == null)
                {
                    continue;
                }

                if (i < clearedCount)
                {
                    continue;
                }

                if (wrongFlashIndex == i && Time.time < wrongFlashEndTime)
                {
                    arrowDisplays[i].SetColor(Color.red);
                    continue;
                }

                float hue = Mathf.Repeat(Time.time * rainbowSpeed + i * 0.17f, 1f);
                arrowDisplays[i].SetColor(Color.HSVToRGB(hue, 0.95f, 1f));
            }

            if (wrongFlashIndex >= 0 && Time.time >= wrongFlashEndTime)
            {
                wrongFlashIndex = -1;
                RefreshClearedVisual();
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
            wrongFlashIndex = Mathf.Clamp(
                expectedIndex,
                0,
                Mathf.Max(0, sequenceLength - 1));

            wrongFlashEndTime = Time.time + Mathf.Max(0.05f, duration);
        }

        private void RefreshClearedVisual()
        {
            if (arrowDisplays == null)
            {
                return;
            }

            for (int i = 0; i < sequenceLength; i++)
            {
                if (arrowDisplays[i] == null)
                {
                    continue;
                }

                bool cleared = i < clearedCount;

                if (cleared)
                {
                    // 너무 투명하지 않게, 회색이 섞인 "꺼진 화살표" 느낌
                    arrowDisplays[i].SetColor(new Color(0.62f, 0.62f, 0.62f, 0.72f));
                }
                else
                {
                    float hue = Mathf.Repeat(Time.time * rainbowSpeed + i * 0.17f, 1f);
                    arrowDisplays[i].SetColor(Color.HSVToRGB(hue, 0.95f, 1f));
                }
            }
        }

        private PixelArrowDisplay.ArrowDirection ConvertToDirection(char arrowChar)
        {
            switch (arrowChar)
            {
                case '↑':
                    return PixelArrowDisplay.ArrowDirection.Up;
                case '↓':
                    return PixelArrowDisplay.ArrowDirection.Down;
                case '←':
                    return PixelArrowDisplay.ArrowDirection.Left;
                case '→':
                    return PixelArrowDisplay.ArrowDirection.Right;
                default:
                    return PixelArrowDisplay.ArrowDirection.Up;
            }
        }
    }

    /// <summary>
    /// 여러 개의 UI Image 블록을 조합해 8비트 스타일 화살표를 표시합니다.
    /// 몸통 폭을 기존보다 넓혀서 화살촉과의 비율을 맞춘 버전입니다.
    /// </summary>
    public class PixelArrowDisplay : MonoBehaviour
    {
        public enum ArrowDirection
        {
            Up,
            Down,
            Left,
            Right
        }

        [Tooltip("도트 그리드 한 변의 칸 수입니다.")]
        private const int GridSize = 11;

        [Tooltip("화살표 바깥 여백입니다.")]
        private const float Padding = 4f;

        [Tooltip("도트 사이 간격 비율입니다.")]
        private const float CellSpacingRatio = 0.08f;

        private readonly List<Image> pixelImages = new List<Image>();

        /// <summary>
        /// 11x11 기준 위쪽 화살표 도트 패턴.
        /// 기존보다 몸통(막대) 부분을 가로로 한 줄 더 넓혀서
        /// 화살촉이 너무 커 보이지 않도록 비율을 조정했습니다.
        /// y는 아래 -> 위로 증가.
        /// </summary>
        private static readonly Vector2Int[] UpPattern = new Vector2Int[]
        {
            // 몸통 (기존 3칸 폭 -> 5칸 폭으로 확장)
            new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(5, 0), new Vector2Int(6, 0), new Vector2Int(7, 0),
            new Vector2Int(3, 1), new Vector2Int(4, 1), new Vector2Int(5, 1), new Vector2Int(6, 1), new Vector2Int(7, 1),
            new Vector2Int(3, 2), new Vector2Int(4, 2), new Vector2Int(5, 2), new Vector2Int(6, 2), new Vector2Int(7, 2),
            new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(5, 3), new Vector2Int(6, 3), new Vector2Int(7, 3),
            new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(5, 4), new Vector2Int(6, 4), new Vector2Int(7, 4),

            // 화살촉 밑변
            new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(2, 5), new Vector2Int(3, 5),
            new Vector2Int(4, 5), new Vector2Int(5, 5), new Vector2Int(6, 5), new Vector2Int(7, 5),
            new Vector2Int(8, 5), new Vector2Int(9, 5), new Vector2Int(10, 5),

            // 화살촉
            new Vector2Int(1, 6), new Vector2Int(2, 6), new Vector2Int(3, 6), new Vector2Int(4, 6),
            new Vector2Int(5, 6), new Vector2Int(6, 6), new Vector2Int(7, 6), new Vector2Int(8, 6),
            new Vector2Int(9, 6),

            new Vector2Int(2, 7), new Vector2Int(3, 7), new Vector2Int(4, 7),
            new Vector2Int(5, 7), new Vector2Int(6, 7), new Vector2Int(7, 7), new Vector2Int(8, 7),

            new Vector2Int(3, 8), new Vector2Int(4, 8), new Vector2Int(5, 8),
            new Vector2Int(6, 8), new Vector2Int(7, 8),

            new Vector2Int(4, 9), new Vector2Int(5, 9), new Vector2Int(6, 9),

            new Vector2Int(5, 10)
        };

        public void Build(ArrowDirection direction)
        {
            ClearPixels();

            RectTransform rect = GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = gameObject.AddComponent<RectTransform>();
            }

            float size = Mathf.Min(rect.rect.width, rect.rect.height);
            if (size <= 0f)
            {
                size = 104f;
            }

            float usableSize = size - Padding * 2f;
            float cellSize = usableSize / GridSize;
            float spacing = cellSize * CellSpacingRatio;
            float actualCellSize = cellSize - spacing;

            bool[,] filled = BuildFilledGrid(direction);

            float totalGridWidth = GridSize * cellSize;
            float startX = -totalGridWidth * 0.5f + cellSize * 0.5f;
            float startY = -totalGridWidth * 0.5f + cellSize * 0.5f;

            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    if (!filled[x, y])
                    {
                        continue;
                    }

                    GameObject pixel = new GameObject($"Pixel_{x}_{y}");
                    pixel.transform.SetParent(transform, false);

                    RectTransform pixelRect = pixel.AddComponent<RectTransform>();
                    pixelRect.anchorMin = new Vector2(0.5f, 0.5f);
                    pixelRect.anchorMax = new Vector2(0.5f, 0.5f);
                    pixelRect.pivot = new Vector2(0.5f, 0.5f);
                    pixelRect.sizeDelta = new Vector2(actualCellSize, actualCellSize);
                    pixelRect.anchoredPosition = new Vector2(
                        startX + x * cellSize,
                        startY + y * cellSize);

                    Image image = pixel.AddComponent<Image>();
                    image.color = Color.white;
                    image.raycastTarget = false;

                    pixelImages.Add(image);
                }
            }
        }

        public void SetColor(Color color)
        {
            for (int i = 0; i < pixelImages.Count; i++)
            {
                if (pixelImages[i] != null)
                {
                    pixelImages[i].color = color;
                }
            }
        }

        private void ClearPixels()
        {
            pixelImages.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private bool[,] BuildFilledGrid(ArrowDirection direction)
        {
            bool[,] result = new bool[GridSize, GridSize];

            for (int i = 0; i < UpPattern.Length; i++)
            {
                Vector2Int source = UpPattern[i];
                Vector2Int target = RotatePoint(source, direction);

                if (target.x >= 0 && target.x < GridSize &&
                    target.y >= 0 && target.y < GridSize)
                {
                    result[target.x, target.y] = true;
                }
            }

            return result;
        }

        private Vector2Int RotatePoint(Vector2Int point, ArrowDirection direction)
        {
            switch (direction)
            {
                case ArrowDirection.Up:
                    return point;

                case ArrowDirection.Down:
                    return new Vector2Int(GridSize - 1 - point.x, GridSize - 1 - point.y);

                case ArrowDirection.Left:
                    return new Vector2Int(GridSize - 1 - point.y, point.x);

                case ArrowDirection.Right:
                    return new Vector2Int(point.y, GridSize - 1 - point.x);

                default:
                    return point;
            }
        }
    }
}