using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 침 계열 공격의 "적중 후 특수증강 효과"를 공통으로 처리하는 유틸리티입니다.
    ///
    /// 사용 목적:
    /// - 일반 SyringeProjectile이 아닌 전설증강 공격에서도
    ///   독침, 꿀침, 모기침, 부식침, 표식침, 소화액낭침, 공복침, 장내균침, 폭발침 같은
    ///   적중형 특수증강을 동일하게 적용하기 위함입니다.
    ///
    /// 여기서 처리하지 않는 것:
    /// - 양극침: 발사 방식 변경
    /// - 관통침: 투사체 이동/충돌 방식
    /// - 유도침: 투사체 이동 보정
    /// - 침귀환: 투사체 이동 방식
    /// - 침술진: 대쉬 기반 별도 생성
    /// - 섬유침: 투사체 이동 궤적 기반 선 생성
    /// - 압력침: 투사체 비행거리 기반 피해 증가
    /// </summary>
    public static class SyringeSpecialHitEffectUtility
    {
        private static bool hasWarnedHealMethodMissing = false;

        public static bool TryGetValidDamageableTarget(
            Collider2D collider,
            Character sourceCharacter,
            out IDamageable damageable,
            out Component damageableComponent,
            out int targetId)
        {
            damageable = null;
            damageableComponent = null;
            targetId = 0;

            if (collider == null)
            {
                return false;
            }

            Component[] parentComponents = collider.GetComponentsInParent<Component>(true);

            for (int i = 0; i < parentComponents.Length; i++)
            {
                Component component = parentComponents[i];

                if (component == null)
                {
                    continue;
                }

                if (!component.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (sourceCharacter != null && component.gameObject == sourceCharacter.gameObject)
                {
                    continue;
                }

                IDamageable candidate = component as IDamageable;

                if (candidate == null)
                {
                    continue;
                }

                Monster monster = component as Monster;

                if (monster != null && !IsValidMonsterTarget(monster))
                {
                    continue;
                }

                damageable = candidate;
                damageableComponent = component;
                targetId = component.gameObject.GetInstanceID();

                return true;
            }

            return false;
        }

        private static bool IsValidMonsterTarget(Monster monster)
        {
            if (monster == null)
            {
                return false;
            }

            if (!monster.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (monster.HP <= 0f)
            {
                return false;
            }

            TrapMonster trapMonster = monster as TrapMonster;

            if (trapMonster != null && !trapMonster.IsActive)
            {
                return false;
            }

            return true;
        }

        public static float GetPreDamageMultiplier(
            Component damageableComponent,
            SyringeSpecialRuntime runtime,
            out bool consumedNeedleMark)
        {
            consumedNeedleMark = false;

            float multiplier = 1f;

            if (damageableComponent == null)
            {
                return multiplier;
            }

            if (runtime.markEnabled)
            {
                consumedNeedleMark = TryConsumeNeedleMark(damageableComponent);

                if (consumedNeedleMark)
                {
                    multiplier *= 1f + Mathf.Max(0f, runtime.markBonusDamageMultiplier);
                }
            }

            if (runtime.corrosionEnabled)
            {
                multiplier *= GetCorrosionDamageMultiplier(damageableComponent);
            }

            return Mathf.Max(0.01f, multiplier);
        }

        public static void ApplyPostHitEffects(
            Component damageableComponent,
            SyringeSpecialRuntime runtime,
            Character sourceCharacter,
            Vector2 hitPosition,
            LayerMask damageableLayer,
            GameObject originalTarget,
            bool consumedNeedleMark)
        {
            if (damageableComponent == null)
            {
                return;
            }

            if (runtime.poisonEnabled)
            {
                ApplyPoison(
                    damageableComponent,
                    runtime,
                    sourceCharacter
                );
            }

            if (runtime.honeyEnabled)
            {
                ApplyHoneySlow(damageableComponent, runtime);
            }

            if (runtime.corrosionEnabled)
            {
                ApplyCorrosion(damageableComponent, runtime);
            }

            if (runtime.markEnabled && !consumedNeedleMark)
            {
                ApplyNeedleMark(damageableComponent, runtime);
            }

            if (runtime.digestiveAcidSacEnabled)
            {
                ApplyDigestiveAcidSac(
                    damageableComponent,
                    runtime,
                    sourceCharacter
                );
            }

            if (runtime.hungerNeedleEnabled)
            {
                ApplyHungerNeedleHit(sourceCharacter, runtime);
            }

            if (runtime.gutBacteriaEnabled)
            {
                ApplyGutBacteria(damageableComponent, runtime);
            }

            if (runtime.mosquitoEnabled)
            {
                ApplyMosquitoHeal(damageableComponent, runtime, sourceCharacter);
            }

            if (runtime.explosionEnabled)
            {
                ApplyExplosion(
                    hitPosition,
                    runtime,
                    sourceCharacter,
                    damageableLayer,
                    originalTarget
                );
            }
        }

        private static Monster GetMonster(Component damageableComponent)
        {
            if (damageableComponent == null)
            {
                return null;
            }

            return damageableComponent.GetComponent<Monster>() ??
                   damageableComponent.GetComponentInParent<Monster>();
        }

        private static bool IsBossLikeTarget(Component damageableComponent)
        {
            if (damageableComponent == null)
            {
                return false;
            }

            Component[] components = damageableComponent.GetComponentsInParent<Component>();

            foreach (Component component in components)
            {
                if (component == null)
                {
                    continue;
                }

                string typeName = component.GetType().Name;

                if (typeName.Contains("Boss"))
                {
                    return true;
                }
            }

            string objectName = damageableComponent.gameObject.name;
            return objectName.Contains("Boss") || objectName.Contains("보스");
        }

        private static void ApplyPoison(
            Component damageableComponent,
            SyringeSpecialRuntime runtime,
            Character sourceCharacter)
        {
            Monster monster = GetMonster(damageableComponent);

            if (monster == null)
            {
                return;
            }

            PoisonStatus poisonStatus = monster.GetComponent<PoisonStatus>();

            if (poisonStatus == null)
            {
                poisonStatus = monster.gameObject.AddComponent<PoisonStatus>();
            }

            poisonStatus.Apply(
                runtime.poisonDuration,
                runtime.poisonTickInterval,
                runtime.poisonTickDamage,
                sourceCharacter,
                "독침"
            );
        }

        private static void ApplyHoneySlow(Component damageableComponent, SyringeSpecialRuntime runtime)
        {
            Monster monster = GetMonster(damageableComponent);

            if (monster == null)
            {
                return;
            }

            HoneySlowStatus honeySlowStatus = monster.GetComponent<HoneySlowStatus>();

            if (honeySlowStatus == null)
            {
                honeySlowStatus = monster.gameObject.AddComponent<HoneySlowStatus>();
            }

            honeySlowStatus.Apply(
                runtime.honeyDuration,
                runtime.honeySlowMultiplier
            );
        }

        private static float GetCorrosionDamageMultiplier(Component damageableComponent)
        {
            Monster monster = GetMonster(damageableComponent);

            if (monster == null)
            {
                return 1f;
            }

            CorrosionStatus corrosionStatus = monster.GetComponent<CorrosionStatus>();

            if (corrosionStatus == null)
            {
                return 1f;
            }

            return Mathf.Max(1f, corrosionStatus.GetDamageTakenMultiplier());
        }

        private static void ApplyCorrosion(Component damageableComponent, SyringeSpecialRuntime runtime)
        {
            Monster monster = GetMonster(damageableComponent);

            if (monster == null)
            {
                return;
            }

            CorrosionStatus corrosionStatus = monster.GetComponent<CorrosionStatus>();

            if (corrosionStatus == null)
            {
                corrosionStatus = monster.gameObject.AddComponent<CorrosionStatus>();
            }

            bool isBossTarget = IsBossLikeTarget(monster);

            corrosionStatus.Apply(
                runtime.corrosionDuration,
                runtime.corrosionDamageTakenBonusPerStack,
                runtime.corrosionBossDamageTakenBonusPerStack,
                runtime.corrosionMaxStacks,
                isBossTarget
            );
        }

        private static bool TryConsumeNeedleMark(Component damageableComponent)
        {
            Monster monster = GetMonster(damageableComponent);

            if (monster == null)
            {
                return false;
            }

            NeedleMarkStatus markStatus = monster.GetComponent<NeedleMarkStatus>();

            if (markStatus == null)
            {
                return false;
            }

            return markStatus.TryConsume();
        }

        private static void ApplyNeedleMark(Component damageableComponent, SyringeSpecialRuntime runtime)
        {
            Monster monster = GetMonster(damageableComponent);

            if (monster == null)
            {
                return;
            }

            NeedleMarkStatus markStatus = monster.GetComponent<NeedleMarkStatus>();

            if (markStatus == null)
            {
                markStatus = monster.gameObject.AddComponent<NeedleMarkStatus>();
            }

            markStatus.Apply(runtime.markDuration);
        }

        private static void ApplyDigestiveAcidSac(
            Component damageableComponent,
            SyringeSpecialRuntime runtime,
            Character sourceCharacter)
        {
            Monster monster = GetMonster(damageableComponent);

            if (monster == null)
            {
                return;
            }

            if (monster.HP <= 0f)
            {
                return;
            }

            DigestiveAcidSacStatus status = monster.GetComponent<DigestiveAcidSacStatus>();

            if (status == null)
            {
                status = monster.gameObject.AddComponent<DigestiveAcidSacStatus>();
            }

            status.Apply(
                runtime.digestiveAcidPuddleLifetime,
                runtime.digestiveAcidPuddleRadius,
                runtime.digestiveAcidPuddleDamagePerSecond,
                runtime.digestiveAcidPuddleTickInterval,
                runtime.digestiveAcidPuddleColor,
                sourceCharacter,
                "소화액낭침"
            );
        }

        private static void ApplyHungerNeedleHit(Character sourceCharacter, SyringeSpecialRuntime runtime)
        {
            HungerNeedleRuntime.RegisterHit(
                sourceCharacter,
                runtime.hungerStackDuration,
                runtime.hungerAttackSpeedBonusPerStack,
                runtime.hungerMaxStacks,
                runtime.debugHungerNeedle
            );
        }

        private static void ApplyGutBacteria(Component damageableComponent, SyringeSpecialRuntime runtime)
        {
            Monster monster = GetMonster(damageableComponent);

            if (monster == null)
            {
                return;
            }

            if (monster.HP <= 0f)
            {
                return;
            }

            GutBacteriaStatus status = monster.GetComponent<GutBacteriaStatus>();

            if (status == null)
            {
                status = monster.gameObject.AddComponent<GutBacteriaStatus>();
            }

            status.Apply(
                runtime.gutBacteriaStackDuration,
                runtime.gutBacteriaRequiredStacks,
                runtime.gutBacteriaMaxStacks,
                runtime.gutBacteriaBonusGemCount,
                runtime.gutBacteriaBonusGemType,
                runtime.gutBacteriaBonusGemSpawnRadius,
                runtime.debugGutBacteria
            );
        }

        private static void ApplyMosquitoHeal(
            Component damageableComponent,
            SyringeSpecialRuntime runtime,
            Character sourceCharacter)
        {
            if (runtime.healingBlocked)
            {
                return;
            }

            if (sourceCharacter == null)
            {
                return;
            }

            float healAmount = runtime.mosquitoHealPerHit;

            if (IsBossLikeTarget(damageableComponent))
            {
                healAmount *= runtime.mosquitoBossHealMultiplier;
            }

            TryHealPlayer(sourceCharacter, healAmount);
        }

        private static void TryHealPlayer(Character sourceCharacter, float healAmount)
        {
            if (sourceCharacter == null || healAmount <= 0f)
            {
                return;
            }

            string[] healMethodNames =
            {
                "GainHealth",
                "Heal",
                "AddHealth",
                "RestoreHealth"
            };

            MethodInfo healMethod = null;

            foreach (string methodName in healMethodNames)
            {
                healMethod = sourceCharacter.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new System.Type[] { typeof(float) },
                    null
                );

                if (healMethod != null)
                {
                    break;
                }
            }

            if (healMethod == null)
            {
                if (!hasWarnedHealMethodMissing)
                {
                    Debug.LogWarning(
                        "[침 특수효과/모기침] Character에서 체력 회복 메서드를 찾지 못했습니다. " +
                        "GainHealth(float), Heal(float), AddHealth(float), RestoreHealth(float) 중 하나가 필요합니다."
                    );

                    hasWarnedHealMethodMissing = true;
                }

                return;
            }

            healMethod.Invoke(sourceCharacter, new object[] { healAmount });
        }

        private static void ApplyExplosion(
            Vector2 hitPosition,
            SyringeSpecialRuntime runtime,
            Character sourceCharacter,
            LayerMask damageableLayer,
            GameObject originalTarget)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                hitPosition,
                runtime.explosionRadius,
                damageableLayer
            );

            HashSet<int> damagedIds = new HashSet<int>();

            for (int i = 0; i < hits.Length; i++)
            {
                IDamageable splashDamageable;
                Component splashComponent;
                int splashId;

                if (!TryGetValidDamageableTarget(
                        hits[i],
                        sourceCharacter,
                        out splashDamageable,
                        out splashComponent,
                        out splashId))
                {
                    continue;
                }

                if (originalTarget != null)
                {
                    Monster originalMonster = originalTarget.GetComponentInParent<Monster>();
                    Monster splashMonster = splashComponent.GetComponent<Monster>() ??
                                            splashComponent.GetComponentInParent<Monster>();

                    if (originalTarget == splashComponent.gameObject)
                    {
                        continue;
                    }

                    if (originalMonster != null && splashMonster != null && originalMonster == splashMonster)
                    {
                        continue;
                    }
                }

                if (!damagedIds.Add(splashId))
                {
                    continue;
                }

                float finalDamage = runtime.explosionDamage;

                splashDamageable.TakeDamage(
                    finalDamage,
                    Vector2.zero,
                    false
                );

                // 이 폭발은 메인 적중 이벤트와 별개의 독립 피해이므로
                // 기존 전체 피해량과 증강별 피해량에 각각 1회 기록한다.
                if (finalDamage > 0f &&
                    sourceCharacter != null &&
                    sourceCharacter.OnDealDamage != null)
                {
                    sourceCharacter.OnDealDamage.Invoke(
                        finalDamage
                    );
                }

                if (finalDamage > 0f &&
                    AugmentDamageTracker.Instance != null)
                {
                    AugmentDamageTracker.Instance.RecordDamage(
                        "폭발침",
                        finalDamage
                    );
                }
            }
        }
    }
}