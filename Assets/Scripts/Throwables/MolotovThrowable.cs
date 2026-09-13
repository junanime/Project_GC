using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class MolotovThrowable : Throwable
    {
        [Header("Molotov Settings")]
        [SerializeField] protected MolotovFire molotovFire;
        [SerializeField] protected ParticleSystem molotovExplosion;

        protected float duration;
        protected float fireRadius;
        protected float fireDamageRate;


        // =========================================================
        // Setup Fire
        // =========================================================

        public void SetupFire(
            float duration,
            float fireRadius,
            float fireDamageRate
        )
        {
            this.duration = duration;
            this.fireRadius = fireRadius;
            this.fireDamageRate = fireDamageRate;
        }


        // =========================================================
        // Explode
        // =========================================================

        protected override void Explode()
        {
            StartCoroutine(
                Burn()
            );
        }


        // =========================================================
        // Burn
        // =========================================================

        protected IEnumerator Burn()
        {
            // 던지는 물체 이미지 숨김
            if (throwableSpriteRenderer != null)
            {
                throwableSpriteRenderer.enabled = false;
            }

            // 그림자 숨김
            if (shadowSpriteRenderer != null)
            {
                shadowSpriteRenderer.enabled = false;
            }


            // 화염 활성화
            if (molotovFire != null)
            {
                molotovFire.gameObject.SetActive(true);
            }


            // 폭발 파티클 재생
            if (molotovExplosion != null)
            {
                molotovExplosion.Play();
            }


            // 화염 지속 피해
            if (molotovFire != null)
            {
                yield return StartCoroutine(
                    molotovFire.Burn(
                        this,
                        damage,
                        knockback,
                        duration,
                        fireRadius,
                        fireDamageRate,
                        targetLayer
                    )
                );
            }


            // 화염 종료
            if (molotovFire != null)
            {
                molotovFire.gameObject.SetActive(false);
            }


            // Sprite 복구
            if (throwableSpriteRenderer != null)
            {
                throwableSpriteRenderer.enabled = true;
            }


            if (shadowSpriteRenderer != null)
            {
                shadowSpriteRenderer.enabled = true;
            }


            DestroyThrowable();
        }


        // =========================================================
        // Damage
        // =========================================================

        public void Damage(
            IDamageable damageable
        )
        {
            if (damageable == null)
            {
                return;
            }


            // 넉백 방향
            Vector2 knockbackDirection =
                (
                    damageable.transform.position -
                    transform.position
                ).normalized;


            // 실제 몬스터 피해
            damageable.TakeDamage(
                damage,
                knockback * knockbackDirection
            );


            // =====================================================
            // 피해 기록
            // =====================================================
            //
            // 기존:
            //
            // playerCharacter.OnDealDamage.Invoke(damage);
            //
            //
            // 변경:
            //
            // MolotovThrowable
            //      ↓
            // OnHitDamageable
            //      ↓
            // ThrowableAbility.ReportDamage()
            //      ↓
            // ├─ Character.OnDealDamage
            // │    → StatsManager 총 피해량
            // │
            // └─ AugmentDamageTracker
            //      → 화염병 증강 누적 피해량
            //
            // =====================================================

            OnHitDamageable?.Invoke(
                damage
            );
        }
    }
}