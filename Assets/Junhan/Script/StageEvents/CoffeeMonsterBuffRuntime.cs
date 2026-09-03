using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 커피수혈 타임 중 몬스터에게 붙는 임시 버프입니다.
    ///
    /// 기존 몬스터 이미지를 직접 교체하지 않고,
    /// 각 SpriteRenderer 위에 갈색 반투명 Overlay SpriteRenderer를 추가해서
    /// 커피를 뒤집어쓴 것처럼 보이게 합니다.
    ///
    /// 이동속도 강화는 기존 Monster 이동 코드를 직접 수정하지 않고,
    /// Rigidbody2D에 플레이어 방향 추가 속도를 얹는 방식으로 처리합니다.
    /// </summary>
    public class CoffeeMonsterBuffRuntime : MonoBehaviour
    {
        private Character targetPlayer;
        private Rigidbody2D rb;

        private SpriteRenderer[] sourceRenderers;
        private SpriteRenderer[] overlayRenderers;

        private float expireTime;
        private float extraMoveForce = 2.5f;
        private float maxAddedVelocity = 2f;
        private Color overlayColor = new Color(0.45f, 0.22f, 0.08f, 0.65f);

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
                CreateOverlayRenderers();
                initialized = true;

                Debug.Log($"[CoffeeBuff] 커피 버프 적용: {name} | renderers={sourceRenderers.Length}");
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

        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            SyncOverlayRenderers();
        }

        private void CacheComponents()
        {
            rb = GetComponent<Rigidbody2D>();
            sourceRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            overlayRenderers = new SpriteRenderer[sourceRenderers.Length];
        }

        private void CreateOverlayRenderers()
        {
            if (sourceRenderers == null)
            {
                return;
            }

            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                SpriteRenderer source = sourceRenderers[i];

                if (source == null)
                {
                    continue;
                }

                // 이미 Coffee Overlay 자체를 다시 대상으로 삼지 않도록 방지
                if (source.gameObject.name.Contains("Coffee_Overlay"))
                {
                    continue;
                }

                GameObject overlayObject = new GameObject("Coffee_Overlay");
                overlayObject.transform.SetParent(source.transform, false);
                overlayObject.transform.localPosition = Vector3.zero;
                overlayObject.transform.localRotation = Quaternion.identity;
                overlayObject.transform.localScale = Vector3.one;

                SpriteRenderer overlay = overlayObject.AddComponent<SpriteRenderer>();
                overlay.sprite = source.sprite;
                overlay.flipX = source.flipX;
                overlay.flipY = source.flipY;
                overlay.sortingLayerName = source.sortingLayerName;
                overlay.sortingOrder = source.sortingOrder + 1;

                Color finalOverlayColor = overlayColor;

                if (finalOverlayColor.a < 0.25f)
                {
                    finalOverlayColor.a = 0.65f;
                }

                overlay.color = finalOverlayColor;

                overlayRenderers[i] = overlay;
            }
        }

        private void SyncOverlayRenderers()
        {
            if (sourceRenderers == null || overlayRenderers == null)
            {
                return;
            }

            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                SpriteRenderer source = sourceRenderers[i];

                if (source == null)
                {
                    continue;
                }

                if (i >= overlayRenderers.Length)
                {
                    continue;
                }

                SpriteRenderer overlay = overlayRenderers[i];

                if (overlay == null)
                {
                    continue;
                }

                overlay.enabled = source.enabled;
                overlay.sprite = source.sprite;
                overlay.flipX = source.flipX;
                overlay.flipY = source.flipY;
                overlay.sortingLayerName = source.sortingLayerName;
                overlay.sortingOrder = source.sortingOrder + 1;

                Color finalOverlayColor = overlayColor;

                if (finalOverlayColor.a < 0.25f)
                {
                    finalOverlayColor.a = 0.65f;
                }

                overlay.color = finalOverlayColor;
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
            DestroyOverlayRenderers();
            Destroy(this);
        }

        private void DestroyOverlayRenderers()
        {
            if (overlayRenderers == null)
            {
                return;
            }

            for (int i = 0; i < overlayRenderers.Length; i++)
            {
                if (overlayRenderers[i] != null)
                {
                    Destroy(overlayRenderers[i].gameObject);
                }
            }
        }

        private void OnDestroy()
        {
            DestroyOverlayRenderers();
        }
    }
}