using UnityEngine;

namespace Donggyu.MainMenuParallax
{
    public class MainMenuParallaxLayer : MonoBehaviour
    {
        public float moveAmountX = 0.08f;
        public float moveAmountY = 0.03f;
        public float moveSpeed = 0.12f;
        public float floatAmountY = 0.02f;
        public float floatSpeed = 0.5f;
        public float rotateAmount;
        public float rotateSpeed;

        private Vector3 startPosition;
        private Quaternion startRotation;
        private float phase;

        private void Start()
        {
            CaptureStartState();
        }

        private void OnEnable()
        {
            CaptureStartState();
        }

        private void CaptureStartState()
        {
            startPosition = transform.localPosition;
            startRotation = transform.localRotation;
            phase = Mathf.Abs(transform.GetSiblingIndex()) * 0.73f;
        }

        private void Update()
        {
            float t = Time.time + phase;
            float x = Mathf.Sin(t * moveSpeed) * moveAmountX;
            float y = Mathf.Cos(t * moveSpeed * 0.86f) * moveAmountY;
            y += Mathf.Sin(t * floatSpeed) * floatAmountY;

            transform.localPosition = startPosition + new Vector3(x, y, 0f);

            if (!Mathf.Approximately(rotateAmount, 0f))
            {
                float z = Mathf.Sin(t * rotateSpeed) * rotateAmount;
                transform.localRotation = startRotation * Quaternion.Euler(0f, 0f, z);
            }
        }
    }
}
