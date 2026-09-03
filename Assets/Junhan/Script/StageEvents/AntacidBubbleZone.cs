using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 제산 거품 폭주 이벤트에서 생성되는 안전 거품 하나입니다.
    /// 플레이어가 이 반경 안에 있으면 이벤트 지속 피해를 받지 않습니다.
    /// 몬스터 피해까지 막는 완전 무적 구역은 아니며, 1차 구현은 '이벤트 피해 회피 구역'입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class AntacidBubbleZone : MonoBehaviour
    {
        [Header("Runtime")]
        [Tooltip("현재 거품 반경입니다.")]
        [SerializeField] private float radius = 1.8f;

        [Tooltip("현재 거품이 유지되는 시간입니다.")]
        [SerializeField] private float lifetime = 5f;

        [Tooltip("거품이 사라질 때 투명해지는 시간입니다.")]
        [SerializeField] private float fadeOutDuration = 0.35f;

        private float age;
        private SpriteRenderer spriteRenderer;
        private CircleCollider2D circleCollider;
        private bool debugLog;

        public float Radius => radius;

        public void Init(float radius, float lifetime, float fadeOutDuration, Color bubbleColor, bool debugLog)
        {
            this.radius = Mathf.Max(0.1f, radius);
            this.lifetime = Mathf.Max(0.1f, lifetime);
            this.fadeOutDuration = Mathf.Max(0.01f, fadeOutDuration);
            this.debugLog = debugLog;

            age = 0f;

            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            circleCollider = GetComponent<CircleCollider2D>();

            if (circleCollider == null)
            {
                circleCollider = gameObject.AddComponent<CircleCollider2D>();
            }

            circleCollider.isTrigger = true;

            if (spriteRenderer != null && spriteRenderer.transform != transform)
            {
                spriteRenderer.transform.localScale = Vector3.one * this.radius * 2f;
                circleCollider.radius = this.radius;
            }
            else
            {
                transform.localScale = Vector3.one * this.radius * 2f;
                circleCollider.radius = 0.5f;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = bubbleColor;
            }

            if (debugLog)
            {
                Debug.Log($"[제산 거품] 생성 | Radius={this.radius} | Lifetime={this.lifetime}", this);
            }
        }

        private void Update()
        {
            age += Time.deltaTime;

            if (spriteRenderer != null)
            {
                float remaining = lifetime - age;

                if (remaining <= fadeOutDuration)
                {
                    Color color = spriteRenderer.color;
                    color.a = Mathf.Clamp01(remaining / fadeOutDuration);
                    spriteRenderer.color = color;
                }
            }

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        public bool ContainsPoint(Vector2 point)
        {
            float effectiveRadius = Mathf.Max(0.1f, radius);
            return Vector2.Distance(transform.position, point) <= effectiveRadius;
        }
    }
}