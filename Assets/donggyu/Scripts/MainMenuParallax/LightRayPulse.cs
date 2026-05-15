using UnityEngine;

namespace Donggyu.MainMenuParallax
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class LightRayPulse : MonoBehaviour
    {
        public float minAlpha = 0.45f;
        public float maxAlpha = 0.75f;
        public float speed = 0.4f;

        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
            Color color = spriteRenderer.color;
            color.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            spriteRenderer.color = color;
        }
    }
}
