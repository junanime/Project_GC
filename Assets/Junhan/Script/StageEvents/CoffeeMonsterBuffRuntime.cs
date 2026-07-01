using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 커피수혈 타임 중 몬스터에게 붙는 임시 버프입니다.
    /// 몬스터별 커피 스프라이트를 새로 만들지 않고,
    /// 기존 SpriteRenderer 색상에 갈색 알파 느낌을 덧씌워 커피를 뒤집어쓴 것처럼 보이게 합니다.
    ///
    /// Monster 내부 이동속도 필드를 직접 수정하지 않고,
    /// Rigidbody2D에 플레이어 방향 추가 속도를 주는 방식으로 기존 몬스터 코드를 보존합니다.
    /// </summary>
    public class CoffeeMonsterBuffRuntime : MonoBehaviour
    {
        private Character targetPlayer;
        private Rigidbody2D rb;
        private SpriteRenderer[] spriteRenderers;
        private Color[] originalColors;

        private float expireTime;
        private float extraMoveForce = 2.5f;
        private float maxAddedVelocity = 2f;
        private Color overlayColor = new Color(0.45f, 0.22f, 0.08f, 0.55f);

        private bool initialized;

        public void ApplyOrRefresh(
            Character targetPlayer,
            float duration,
            float extraMoveForce,
            float maxAddedVelocity,
            Color overlayColor)
        {
            this.targetPlayer = targetPlayer;
            this.extraMoveForce = Mathf.Max(0f, extraMoveForce);
            this.maxAddedVelocity = Mathf.Max(0.1f, maxAddedVelocity);
            this.overlayColor = overlayColor;
            this.expireTime = Time.time + Mathf.Max(0.1f, duration);

            if (!initialized)
            {
                CacheComponents();
                ApplyCoffeeTint();
                initialized = true;
            }
        }

        private void FixedUpdate()
        {
            if (!initialized)
            {
                return;
            }

            if (Time.time >= expireTime)
            {
                RemoveBuffAndDestroy();
                return;
            }

            ApplyExtraMovementTowardPlayer();
        }

        private void CacheComponents()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            originalColors = new Color[spriteRenderers.Length];

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    originalColors[i] = spriteRenderers[i].color;
                }
            }
        }

        private void ApplyCoffeeTint()
        {
            if (spriteRenderers == null)
            {
                return;
            }

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer sr = spriteRenderers[i];

                if (sr == null)
                {
                    continue;
                }

                Color baseColor = originalColors[i];
                float alpha = Mathf.Clamp01(overlayColor.a);

                Color tintedColor = Color.Lerp(baseColor, overlayColor, alpha);
                tintedColor.a = baseColor.a;

                sr.color = tintedColor;
            }
        }

        private void RestoreOriginalTint()
        {
            if (spriteRenderers == null || originalColors == null)
            {
                return;
            }

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null && i < originalColors.Length)
                {
                    spriteRenderers[i].color = originalColors[i];
                }
            }
        }

        private void ApplyExtraMovementTowardPlayer()
        {
            if (rb == null || targetPlayer == null)
            {
                return;
            }

            Vector2 direction = ((Vector2)targetPlayer.transform.position - rb.position).normalized;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Vector2 addedVelocity = direction * extraMoveForce * Time.fixedDeltaTime;
            rb.velocity += addedVelocity;

            float projectedSpeed = Vector2.Dot(rb.velocity, direction);

            if (projectedSpeed > maxAddedVelocity)
            {
                Vector2 excess = direction * (projectedSpeed - maxAddedVelocity);
                rb.velocity -= excess;
            }
        }

        public void RemoveBuffAndDestroy()
        {
            RestoreOriginalTint();
            Destroy(this);
        }

        private void OnDestroy()
        {
            RestoreOriginalTint();
        }
    }
}