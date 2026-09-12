using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class ExplosiveProjectile : Projectile
    {
        [SerializeField] protected LayerMask explosionLayerMask;
        [SerializeField] protected float explosionRadius;
        [SerializeField] protected float explosionDamage;
        [SerializeField] protected float explosionKnockback;
        [SerializeField] protected float explosionDuration = 0.25f;
        [SerializeField] protected SpriteRenderer explosionSpriteRenderer;

        public void SetupExplosion(
            float explosionDamage,
            float explosionRadius,
            float explosionKnockback)
        {
            this.explosionDamage = explosionDamage;
            this.explosionRadius = explosionRadius;
            this.explosionKnockback = explosionKnockback;
        }

        protected override void HitDamageable(IDamageable damageable)
        {
            // Explode
            StartCoroutine(Explosion());
        }

        protected override void HitNothing()
        {
            // Explode
            StartCoroutine(Explosion());
        }

        protected IEnumerator Explosion()
        {
            projectileSpriteRenderer.gameObject.SetActive(false);
            destructionParticleSystem.Play();

            float t = 0;

            Dictionary<Collider2D, bool> damagedColliders =
                new Dictionary<Collider2D, bool>();

            while (t < 1)
            {
                Color c = explosionSpriteRenderer.color;
                c.a = EasingUtils.EaseOutQuart(1 - t) * 0.5f;
                explosionSpriteRenderer.color = c;

                explosionSpriteRenderer.transform.localScale =
                    Vector3.one *
                    EasingUtils.EaseOutQuart(t) *
                    explosionRadius *
                    10;

                Collider2D[] hitColliders =
                    Physics2D.OverlapCircleAll(
                        transform.position,
                        t * explosionRadius,
                        explosionLayerMask
                    );

                foreach (Collider2D collider in hitColliders)
                {
                    if (!damagedColliders.ContainsKey(collider))
                    {
                        Vector2 dir =
                            (Vector2)collider.transform.position -
                            (Vector2)transform.position;

                        IDamageable damageable =
                            collider.GetComponentInParent<IDamageable>();

                        if (damageable != null)
                        {
                            Character targetCharacter =
                                damageable as Character;

                            // 결과 화면용:
                            // 몬스터가 발사한 폭발 투사체가 플레이어에게 피해를 주는 경우
                            // 발사한 몬스터의 Blueprint를 함께 전달합니다.
                            if (targetCharacter != null &&
                                sourceMonsterBlueprint != null)
                            {
                                targetCharacter.TakeDamageFromMonster(
                                    explosionDamage,
                                    dir.normalized * explosionKnockback,
                                    sourceMonsterBlueprint
                                );
                            }
                            else
                            {
                                // 플레이어가 발사한 폭발 투사체 또는
                                // Character가 아닌 일반 IDamageable 대상은 기존 방식 유지
                                damageable.TakeDamage(
                                    explosionDamage,
                                    dir.normalized * explosionKnockback
                                );
                            }

                            OnHitDamageable?.Invoke(explosionDamage);
                        }

                        damagedColliders[collider] = true;
                    }
                }

                t += Time.deltaTime / explosionDuration;
                yield return null;
            }

            explosionSpriteRenderer.color =
                explosionSpriteRenderer.color - Color.black;

            if (explosionDuration < destructionParticleSystem.main.duration)
            {
                yield return new WaitForSeconds(
                    destructionParticleSystem.main.duration -
                    explosionDuration
                );
            }

            projectileSpriteRenderer.gameObject.SetActive(true);

            entityManager.DespawnProjectile(
                projectileIndex,
                this
            );
        }
    }
}