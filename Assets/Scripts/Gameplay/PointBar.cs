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
                // Clip the liquid at full size so its pixels and highlights never stretch.
                filledImage.fillAmount = fraction;
                return;
            }
            if (barFill != null && barBackground != null)
                barFill.sizeDelta = new Vector2(barBackground.rect.width * fraction, barFill.sizeDelta.y);
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
