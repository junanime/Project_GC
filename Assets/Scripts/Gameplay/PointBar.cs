using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Vampire
{
    public class PointBar : MonoBehaviour
    {
        [SerializeField] protected RectTransform barBackground, barFill;
        [SerializeField] protected UnityEvent onEmpty, onFull;
        [SerializeField] private Image filledImage;
        [SerializeField] private bool tileFillPixels;
        private RectTransform fillViewport;
        private float displayedFraction;

        protected float currentPoints, minPoints, maxPoints;
        protected bool clamp;

        public float CurrentPoints { get => currentPoints; set => currentPoints = value; }

        public void Setup(float currentPoints, float minPoints, float maxPoints, bool clamp = true)
        {
            this.currentPoints = currentPoints;
            this.minPoints = minPoints;
            this.maxPoints = maxPoints;
            this.clamp = clamp;
            UpdateDisplay();
        }

        public void AddPoints(float points)
        {
            currentPoints += points;
            CheckPoints();
            UpdateDisplay();
        }

        public void SubtractPoints(float points)
        {
            currentPoints -= points;
            CheckPoints();
            UpdateDisplay();
        }

        public void SetPoints(float points)
        {
            currentPoints = points;
            CheckPoints();
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            float range = maxPoints - minPoints;
            float fraction = range > 0f ? Mathf.Clamp01((currentPoints - minPoints) / range) : 0f;
            if (filledImage != null)
            {
                if (tileFillPixels)
                {
                    displayedFraction = fraction;
                    UpdateTiledFill();
                    return;
                }
                filledImage.fillAmount = fraction;
                return;
            }
            if (barFill != null && barBackground != null)
                barFill.sizeDelta = new Vector2(barBackground.rect.width * fraction, barFill.sizeDelta.y);
        }

        private void LateUpdate()
        {
            // Layout can change after Setup, including when a hidden boss bar appears.
            if (tileFillPixels && filledImage != null) UpdateTiledFill();
        }

        private void UpdateTiledFill()
        {
            if (barBackground == null || filledImage.sprite == null) return;
            if (fillViewport == null)
            {
                var viewport = new GameObject("Liquid Amount Mask", typeof(RectTransform), typeof(RectMask2D));
                fillViewport = (RectTransform)viewport.transform;
                fillViewport.SetParent(barBackground, false);
                fillViewport.SetSiblingIndex(filledImage.transform.GetSiblingIndex());
                fillViewport.anchorMin = fillViewport.anchorMax = new Vector2(0f, 0.5f);
                fillViewport.pivot = new Vector2(0f, 0.5f);
                filledImage.transform.SetParent(fillViewport, false);
                filledImage.rectTransform.anchorMin = filledImage.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                filledImage.rectTransform.pivot = new Vector2(0f, 0.5f);
                filledImage.rectTransform.anchoredPosition = Vector2.zero;
                filledImage.type = Image.Type.Tiled;
                filledImage.raycastTarget = false;
            }
            float width = Mathf.Max(0f, barBackground.rect.width - 56f);
            float height = Mathf.Max(0f, barBackground.rect.height - 16f);
            fillViewport.anchoredPosition = new Vector2(28f, 0f);
            fillViewport.sizeDelta = new Vector2(width * displayedFraction, height);
            filledImage.rectTransform.sizeDelta = new Vector2(width, height);
            // The tile scales uniformly to the trough height; circles stay circles at every width.
            float referencePPU = filledImage.canvas != null ? filledImage.canvas.referencePixelsPerUnit : 100f;
            filledImage.pixelsPerUnitMultiplier = height > 0f
                ? filledImage.sprite.rect.height * referencePPU / (filledImage.sprite.pixelsPerUnit * height)
                : 1f;
            filledImage.enabled = width > 0f && height > 0f && displayedFraction > 0f;
        }

        private void CheckPoints()
        {
            if (currentPoints >= maxPoints)
            {
                onFull.Invoke();
                if (clamp)
                    currentPoints = maxPoints;
            }
            else if (currentPoints <= minPoints)
            {
                onEmpty.Invoke();
                if (clamp)
                    currentPoints = minPoints;
            }
        }
    }
}
