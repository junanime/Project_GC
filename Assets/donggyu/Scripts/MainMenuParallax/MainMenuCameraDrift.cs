using UnityEngine;

namespace Donggyu.MainMenuParallax
{
    [RequireComponent(typeof(Camera))]
    public class MainMenuCameraDrift : MonoBehaviour
    {
        public float moveX = 0.12f;
        public float moveY = 0.05f;
        public float zoomAmount = 0.15f;
        public float speed = 0.12f;

        private Camera targetCamera;
        private Vector3 startPosition;
        private float startOrthographicSize;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            CaptureStartState();
        }

        private void OnEnable()
        {
            targetCamera = GetComponent<Camera>();
            CaptureStartState();
        }

        private void CaptureStartState()
        {
            startPosition = transform.position;
            startOrthographicSize = targetCamera != null ? targetCamera.orthographicSize : 5f;
        }

        private void Update()
        {
            float t = Time.time * speed;
            transform.position = startPosition + new Vector3(Mathf.Sin(t) * moveX, Mathf.Cos(t * 0.83f) * moveY, 0f);

            if (targetCamera != null)
            {
                targetCamera.orthographicSize = startOrthographicSize + Mathf.Sin(t * 0.72f) * zoomAmount;
            }
        }
    }
}
