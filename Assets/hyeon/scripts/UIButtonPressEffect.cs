using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Vampire
{
    public class UIButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float pressedScale = 0.88f;
        [SerializeField] private float animationTime = 0.08f;

        private Vector3 originalScale;
        private Coroutine scaleRoutine;

        private void Awake()
        {
            originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            transform.localScale = originalScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            AnimateScale(originalScale * pressedScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            AnimateScale(originalScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateScale(originalScale);
        }

        private void AnimateScale(Vector3 targetScale)
        {
            if (scaleRoutine != null)
            {
                StopCoroutine(scaleRoutine);
            }

            scaleRoutine = StartCoroutine(ScaleRoutine(targetScale));
        }

        private IEnumerator ScaleRoutine(Vector3 targetScale)
        {
            Vector3 startScale = transform.localScale;
            float timer = 0f;

            while (timer < animationTime)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / animationTime;

                transform.localScale = Vector3.Lerp(startScale, targetScale, t);

                yield return null;
            }

            transform.localScale = targetScale;
            scaleRoutine = null;
        }
    }
}