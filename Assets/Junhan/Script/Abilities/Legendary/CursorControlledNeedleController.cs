using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 전설증강: 이기어침
    /// 
    /// 변경 목표
    /// - 플레이어 위쪽 중심의 "늘어진 8자" 궤도를 그림
    /// - 침 본체는 항상 궤도 위를 따라감
    /// - 침 방향은 궤도 접선 방향을 날카롭게 따라감
    /// - 특수증강 개수에 따라 데미지/속도 증가
    /// - 등 뒤 특수증강 표시 침은 부채꼴로 "서 있는" 형태 유지
    /// </summary>
    public class CursorControlledNeedleController : MonoBehaviour
    {
        private Character sourceCharacter;
        private EntityManager entityManager;
        private SyringeDartAbility sourceNeedleAbility;

        private GameObject projectilePrefab;
        private LayerMask monsterLayer;

        private float orbitBaseSpeed;
        private float hitRadius;
        private float damageMultiplier;
        private float damageInterval;
        private float visualScale;
        private float homingHitRadiusBonus;

        private Vector2 backDisplayOffset;
        private float backDisplaySpacing;
        private float backDisplayArcHeight;
        private float backDisplayScale;

        private Vector2 orbitCenterOffset;
        private float orbitHorizontalRadius;
        private float orbitVerticalRadius;
        private float targetAssistRadius;
        private float targetAssistStrength;
        private float damageBonusPerSpecial;
        private float speedBonusPerSpecial;
        private int maxSpecialBonusCount;
        private int maxTargetsPerTick;
        private bool debugOrbitLog;

        // 신규 보정값
        private float orbitVisualAngleOffset;
        private float backDisplayBaseAngle;
        private float backDisplaySpreadAngle;
        private float orbitStraightness;

        private Transform cursorNeedleTransform;
        private Transform backDisplayRoot;
        private SpriteRenderer cursorNeedleRenderer;

        private Sprite projectileSprite;
        private int projectileSortingLayerId;
        private int projectileSortingOrder;
        private Vector3 projectileVisualBaseScale = Vector3.one;

        private float orbitPhase = 0f;
        private int lastDisplayedSpecialCount = -1;

        private readonly List<GameObject> backDisplayNeedles = new List<GameObject>();
        private readonly Dictionary<int, float> nextDamageAllowedTimeByTarget = new Dictionary<int, float>();

        private static bool hasWarnedHealMethodMissing = false;

        public static CursorControlledNeedleController Create(
            Character sourceCharacter,
            EntityManager entityManager,
            SyringeDartAbility sourceNeedleAbility,
            float followSpeed,
            float hitRadius,
            float damageMultiplier,
            float damageInterval,
            float visualScale,
            float homingHitRadiusBonus,
            Vector2 backDisplayOffset,
            float backDisplaySpacing,
            float backDisplayArcHeight,
            float backDisplayScale,
            Vector2 orbitCenterOffset,
            float orbitHorizontalRadius,
            float orbitVerticalRadius,
            float targetAssistRadius,
            float targetAssistStrength,
            float damageBonusPerSpecial,
            float speedBonusPerSpecial,
            int maxSpecialBonusCount,
            int maxTargetsPerTick,
            bool debugOrbitLog,
            float orbitVisualAngleOffset,
            float backDisplayBaseAngle,
            float backDisplaySpreadAngle,
            float orbitStraightness)
        {
            GameObject controllerObject = new GameObject("Cursor Controlled Needle Controller");
            CursorControlledNeedleController controller = controllerObject.AddComponent<CursorControlledNeedleController>();

            controller.Init(
                sourceCharacter,
                entityManager,
                sourceNeedleAbility,
                followSpeed,
                hitRadius,
                damageMultiplier,
                damageInterval,
                visualScale,
                homingHitRadiusBonus,
                backDisplayOffset,
                backDisplaySpacing,
                backDisplayArcHeight,
                backDisplayScale,
                orbitCenterOffset,
                orbitHorizontalRadius,
                orbitVerticalRadius,
                targetAssistRadius,
                targetAssistStrength,
                damageBonusPerSpecial,
                speedBonusPerSpecial,
                maxSpecialBonusCount,
                maxTargetsPerTick,
                debugOrbitLog,
                orbitVisualAngleOffset,
                backDisplayBaseAngle,
                backDisplaySpreadAngle,
                orbitStraightness
            );

            return controller;
        }

        private void Init(
            Character sourceCharacter,
            EntityManager entityManager,
            SyringeDartAbility sourceNeedleAbility,
            float followSpeed,
            float hitRadius,
            float damageMultiplier,
            float damageInterval,
            float visualScale,
            float homingHitRadiusBonus,
            Vector2 backDisplayOffset,
            float backDisplaySpacing,
            float backDisplayArcHeight,
            float backDisplayScale,
            Vector2 orbitCenterOffset,
            float orbitHorizontalRadius,
            float orbitVerticalRadius,
            float targetAssistRadius,
            float targetAssistStrength,
            float damageBonusPerSpecial,
            float speedBonusPerSpecial,
            int maxSpecialBonusCount,
            int maxTargetsPerTick,
            bool debugOrbitLog,
            float orbitVisualAngleOffset,
            float backDisplayBaseAngle,
            float backDisplaySpreadAngle,
            float orbitStraightness)
        {
            this.sourceCharacter = sourceCharacter;
            this.entityManager = entityManager;
            this.sourceNeedleAbility = sourceNeedleAbility;

            this.orbitBaseSpeed = Mathf.Max(0.01f, followSpeed);
            this.hitRadius = Mathf.Max(0.05f, hitRadius);
            this.damageMultiplier = Mathf.Max(0.01f, damageMultiplier);
            this.damageInterval = Mathf.Max(0.05f, damageInterval);
            this.visualScale = Mathf.Max(0.01f, visualScale);
            this.homingHitRadiusBonus = Mathf.Max(0f, homingHitRadiusBonus);

            this.backDisplayOffset = backDisplayOffset;
            this.backDisplaySpacing = Mathf.Max(0.01f, backDisplaySpacing);
            this.backDisplayArcHeight = Mathf.Max(0f, backDisplayArcHeight);
            this.backDisplayScale = Mathf.Max(0.01f, backDisplayScale);

            this.orbitCenterOffset = orbitCenterOffset;
            this.orbitHorizontalRadius = Mathf.Max(0.1f, orbitHorizontalRadius);
            this.orbitVerticalRadius = Mathf.Max(0.1f, orbitVerticalRadius);
            this.targetAssistRadius = Mathf.Max(0f, targetAssistRadius);
            this.targetAssistStrength = Mathf.Clamp01(targetAssistStrength);
            this.damageBonusPerSpecial = Mathf.Max(0f, damageBonusPerSpecial);
            this.speedBonusPerSpecial = Mathf.Max(0f, speedBonusPerSpecial);
            this.maxSpecialBonusCount = Mathf.Max(0, maxSpecialBonusCount);
            this.maxTargetsPerTick = Mathf.Max(1, maxTargetsPerTick);
            this.debugOrbitLog = debugOrbitLog;

            this.orbitVisualAngleOffset = orbitVisualAngleOffset;
            this.backDisplayBaseAngle = backDisplayBaseAngle;
            this.backDisplaySpreadAngle = backDisplaySpreadAngle;
            this.orbitStraightness = Mathf.Clamp01(orbitStraightness);

            projectilePrefab = sourceNeedleAbility.ProjectilePrefab;
            monsterLayer = sourceNeedleAbility.MonsterLayer;

            CacheProjectileVisualInfo();

            transform.position = GetOrbitCenter();

            CreateCursorNeedleVisual();
            CreateBackDisplayRoot();

            if (sourceCharacter.OnDeath != null)
            {
                sourceCharacter.OnDeath.AddListener(DestroySelf);
            }

            UpdateBackDisplay(true);

            if (debugOrbitLog)
            {
                Debug.Log(
                    $"[이기어침] 생성 | OrbitSpeed={orbitBaseSpeed}, Straightness={orbitStraightness}, " +
                    $"OrbitRadius=({orbitHorizontalRadius}, {orbitVerticalRadius})"
                );
            }
        }

        private void Update()
        {
            if (sourceCharacter == null || sourceNeedleAbility == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = GetOrbitCenter();

            UpdateOrbitNeedle();
            UpdateCursorNeedleHeavyVisual();
            UpdateBackDisplay(false);
            DetectAndDamageEnemies();
        }

        private void CacheProjectileVisualInfo()
        {
            if (projectilePrefab == null)
            {
                return;
            }

            SpriteRenderer sourceRenderer = projectilePrefab.GetComponentInChildren<SpriteRenderer>(true);

            if (sourceRenderer != null)
            {
                projectileSprite = sourceRenderer.sprite;
                projectileSortingLayerId = sourceRenderer.sortingLayerID;
                projectileSortingOrder = sourceRenderer.sortingOrder;
                projectileVisualBaseScale = sourceRenderer.transform.localScale;
            }
        }

        private void CreateCursorNeedleVisual()
        {
            GameObject cursorNeedleObject = new GameObject("Cursor Controlled Needle - Orbit");
            cursorNeedleTransform = cursorNeedleObject.transform;
            cursorNeedleTransform.SetParent(transform);

            Vector3 startPosition = GetOrbitPointWorld(orbitPhase);
            cursorNeedleTransform.position = startPosition;

            cursorNeedleRenderer = cursorNeedleObject.AddComponent<SpriteRenderer>();
            cursorNeedleRenderer.sprite = projectileSprite;
            cursorNeedleRenderer.sortingLayerID = projectileSortingLayerId;
            cursorNeedleRenderer.sortingOrder = projectileSortingOrder + 10;
            cursorNeedleRenderer.color = new Color(1f, 1f, 1f, 0.95f);

            cursorNeedleTransform.localScale = projectileVisualBaseScale * visualScale;
        }

        private void CreateBackDisplayRoot()
        {
            GameObject backRootObject = new GameObject("Cursor Needle Back Display");
            backDisplayRoot = backRootObject.transform;
            backDisplayRoot.SetParent(transform);
            backDisplayRoot.localPosition = backDisplayOffset;
        }

        private void UpdateOrbitNeedle()
        {
            if (cursorNeedleTransform == null)
            {
                return;
            }

            float speedMultiplier = GetSpecialSpeedMultiplier();

            orbitPhase += orbitBaseSpeed * speedMultiplier * Time.deltaTime;

            if (orbitPhase > Mathf.PI * 2f)
            {
                orbitPhase -= Mathf.PI * 2f;
            }

            // 침 본체는 무조건 궤도 위를 따라간다.
            Vector3 orbitPoint = GetOrbitPointWorld(orbitPhase);
            cursorNeedleTransform.position = orbitPoint;

            // 회전은 궤도 접선 방향을 사용
            Vector3 tangentDirection = GetOrbitTangentDirectionWorld(orbitPhase);
            ApplyNeedleRotationByDirection(tangentDirection);
        }

        private Vector3 GetOrbitCenter()
        {
            if (sourceCharacter != null && sourceCharacter.CenterTransform != null)
            {
                return sourceCharacter.CenterTransform.position + (Vector3)orbitCenterOffset;
            }

            if (sourceCharacter != null)
            {
                return sourceCharacter.transform.position + (Vector3)orbitCenterOffset;
            }

            return transform.position;
        }

        private static float SignedPow(float value, float power)
        {
            if (Mathf.Abs(value) <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), power);
        }

        private Vector3 GetOrbitPointWorld(float phase)
        {
            Vector3 center = GetOrbitCenter();

            // 기본 8자 파형
            float rawX = Mathf.Sin(phase);
            float rawY = Mathf.Sin(phase * 2f);

            // straightness가 커질수록
            // X는 더 멀리 곧게 뻗고,
            // Y는 가운데 교차부가 좁아지고 양 끝에서 더 부드럽게 휘어진다.
            float xExponent = Mathf.Lerp(1f, 0.42f, orbitStraightness);
            float yExponent = Mathf.Lerp(1f, 1.9f, orbitStraightness);

            float shapedX = SignedPow(rawX, xExponent);
            float shapedY = SignedPow(rawY, yExponent);

            float x = shapedX * orbitHorizontalRadius;
            float y = shapedY * orbitVerticalRadius;

            return center + new Vector3(x, y, 0f);
        }

        private Vector3 GetOrbitTangentDirectionWorld(float phase)
        {
            // shaped path는 해석 미분보다 수치 미분이 더 안전
            const float delta = 0.01f;

            Vector3 prev = GetOrbitPointWorld(phase - delta);
            Vector3 next = GetOrbitPointWorld(phase + delta);

            Vector3 tangent = next - prev;

            if (tangent.sqrMagnitude <= 0.0001f)
            {
                return Vector3.right;
            }

            return tangent.normalized;
        }

        private void ApplyNeedleRotationByDirection(Vector3 direction)
        {
            if (cursorNeedleTransform == null)
            {
                return;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            cursorNeedleTransform.rotation = Quaternion.Euler(0f, 0f, angle + orbitVisualAngleOffset);
        }

        private float GetSpecialDamageMultiplier()
        {
            if (sourceNeedleAbility == null)
            {
                return 1f;
            }

            int specialCount = sourceNeedleAbility.GetActiveSpecialAugmentCount();
            int bonusCount = maxSpecialBonusCount > 0
                ? Mathf.Min(specialCount, maxSpecialBonusCount)
                : specialCount;

            return 1f + bonusCount * damageBonusPerSpecial;
        }

        private float GetSpecialSpeedMultiplier()
        {
            if (sourceNeedleAbility == null)
            {
                return 1f;
            }

            int specialCount = sourceNeedleAbility.GetActiveSpecialAugmentCount();
            int bonusCount = maxSpecialBonusCount > 0
                ? Mathf.Min(specialCount, maxSpecialBonusCount)
                : specialCount;

            return 1f + bonusCount * speedBonusPerSpecial;
        }

        private void UpdateCursorNeedleHeavyVisual()
        {
            if (cursorNeedleTransform == null || sourceNeedleAbility == null)
            {
                return;
            }

            float heavySizeMultiplier = sourceNeedleAbility.GetCursorNeedleHeavySizeMultiplier();
            cursorNeedleTransform.localScale = projectileVisualBaseScale * visualScale * heavySizeMultiplier;

            if (cursorNeedleRenderer != null)
            {
                float chargeRatio = sourceNeedleAbility.GetCursorHeavyChargeRatio();
                float brightness = Mathf.Lerp(0.95f, 1.25f, chargeRatio);
                cursorNeedleRenderer.color = new Color(brightness, brightness, brightness, 0.95f);
            }
        }

        private void UpdateBackDisplay(bool forceRebuild)
        {
            if (backDisplayRoot == null || sourceNeedleAbility == null)
            {
                return;
            }

            backDisplayRoot.localPosition = backDisplayOffset;

            int specialCount = sourceNeedleAbility.GetActiveSpecialAugmentCount();

            if (!forceRebuild && specialCount == lastDisplayedSpecialCount)
            {
                return;
            }

            lastDisplayedSpecialCount = specialCount;
            RebuildBackDisplay(specialCount);
        }

        private void RebuildBackDisplay(int specialCount)
        {
            for (int i = 0; i < backDisplayNeedles.Count; i++)
            {
                if (backDisplayNeedles[i] != null)
                {
                    Destroy(backDisplayNeedles[i]);
                }
            }

            backDisplayNeedles.Clear();

            if (specialCount <= 0)
            {
                return;
            }

            float totalWidth = (specialCount - 1) * backDisplaySpacing;

            for (int i = 0; i < specialCount; i++)
            {
                GameObject needleObject = new GameObject($"Back Display Needle {i + 1}");
                Transform needleTransform = needleObject.transform;
                needleTransform.SetParent(backDisplayRoot);

                float x = (i * backDisplaySpacing) - (totalWidth * 0.5f);
                float normalized = specialCount <= 1
                    ? 0f
                    : Mathf.InverseLerp(0f, specialCount - 1, i) * 2f - 1f;

                float y = -Mathf.Abs(normalized) * backDisplayArcHeight;

                needleTransform.localPosition = new Vector3(x, y, 0f);
                needleTransform.localScale = projectileVisualBaseScale * backDisplayScale;

                // 핵심 수정:
                // 표시 침은 비행용 회전 보정값을 쓰지 않고,
                // 세로로 "서 있는" 부채꼴 전용 각도를 사용한다.
                float angle = backDisplayBaseAngle - normalized * backDisplaySpreadAngle;
                needleTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

                SpriteRenderer renderer = needleObject.AddComponent<SpriteRenderer>();
                renderer.sprite = projectileSprite;
                renderer.sortingLayerID = projectileSortingLayerId;
                renderer.sortingOrder = projectileSortingOrder + 8;
                renderer.color = new Color(1f, 1f, 1f, 0.82f);

                backDisplayNeedles.Add(needleObject);
            }
        }

        private bool TryGetValidMonsterTarget(Collider2D collider, out Monster monster)
        {
            monster = null;

            if (collider == null)
            {
                return false;
            }

            monster = collider.GetComponentInParent<Monster>();

            if (monster == null || !monster.gameObject.activeInHierarchy)
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

        private bool CanDamageTarget(int targetId)
        {
            if (!nextDamageAllowedTimeByTarget.TryGetValue(targetId, out float nextAllowedTime))
            {
                return true;
            }

            return Time.time >= nextAllowedTime;
        }

        private void DetectAndDamageEnemies()
        {
            if (cursorNeedleTransform == null || sourceNeedleAbility == null)
            {
                return;
            }

            SyringeSpecialRuntime runtime = sourceNeedleAbility.GetCurrentSpecialRuntime();

            float heavySizeMultiplier = sourceNeedleAbility.GetCursorNeedleHeavySizeMultiplier();
            float effectiveHitRadius = hitRadius * heavySizeMultiplier;

            if (runtime.homingEnabled)
            {
                effectiveHitRadius += homingHitRadiusBonus;
            }

            // 위치 보정 대신 판정만 소폭 보정
            if (targetAssistRadius > 0f && targetAssistStrength > 0f)
            {
                effectiveHitRadius += targetAssistRadius * targetAssistStrength;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(
                cursorNeedleTransform.position,
                effectiveHitRadius
            );

            if (hits == null || hits.Length == 0)
            {
                return;
            }

            int damagedTargetCount = 0;
            HashSet<int> checkedTargetsThisFrame = new HashSet<int>();

            foreach (Collider2D hit in hits)
            {
                Monster monster;

                if (!TryGetValidMonsterTarget(hit, out monster))
                {
                    continue;
                }

                IDamageable damageable = monster as IDamageable;
                Component damageableComponent = monster;

                if (damageable == null || damageableComponent == null)
                {
                    continue;
                }

                int targetId = monster.gameObject.GetInstanceID();

                if (checkedTargetsThisFrame.Contains(targetId))
                {
                    continue;
                }

                checkedTargetsThisFrame.Add(targetId);

                if (!CanDamageTarget(targetId))
                {
                    continue;
                }

                DamageTarget(damageable, damageableComponent, runtime);

                nextDamageAllowedTimeByTarget[targetId] = Time.time + damageInterval;
                damagedTargetCount++;

                if (damagedTargetCount >= maxTargetsPerTick)
                {
                    break;
                }
            }
        }

        private void DamageTarget(IDamageable damageable, Component damageableComponent, SyringeSpecialRuntime runtime)
        {
            if (damageable == null || damageableComponent == null || sourceNeedleAbility == null)
            {
                return;
            }

            Vector2 knockbackDirection = Vector2.zero;

            if (cursorNeedleTransform != null)
            {
                knockbackDirection =
                    (Vector2)damageableComponent.transform.position -
                    (Vector2)cursorNeedleTransform.position;

                if (knockbackDirection.sqrMagnitude > 0.0001f)
                {
                    knockbackDirection.Normalize();
                }
            }

            float heavyDamageMultiplier = sourceNeedleAbility.GetCursorNeedleHeavyDamageMultiplier();
            float heavyKnockbackMultiplier = sourceNeedleAbility.GetCursorNeedleHeavyKnockbackMultiplier();
            float specialDamageMultiplier = GetSpecialDamageMultiplier();

            float rawDamage =
                sourceNeedleAbility.GetEffectiveDamage() *
                damageMultiplier *
                heavyDamageMultiplier *
                specialDamageMultiplier;

            float knockback = sourceNeedleAbility.GetEffectiveKnockback() * heavyKnockbackMultiplier;

            PlayerGeneralStatRuntime statRuntime = PlayerGeneralStatRuntime.GetOrCreate(sourceCharacter);

            bool isCritical = false;
            float finalDamage = rawDamage;

            if (statRuntime != null)
            {
                finalDamage = statRuntime.CalculateOffensiveDamage(
                    sourceCharacter,
                    damageableComponent,
                    rawDamage,
                    out isCritical
                );

                knockback *= statRuntime.KnockbackMultiplier;
            }

            damageable.TakeDamage(finalDamage, knockbackDirection * knockback, isCritical);

            if (sourceCharacter != null && sourceCharacter.OnDealDamage != null)
            {
                sourceCharacter.OnDealDamage.Invoke(finalDamage);
            }

            ApplySpecialEffectsAfterHit(damageableComponent, runtime);
        }

        private void ApplySpecialEffectsAfterHit(Component damageableComponent, SyringeSpecialRuntime runtime)
        {
            if (runtime.poisonEnabled)
            {
                ApplyPoison(damageableComponent, runtime);
            }

            if (runtime.honeyEnabled)
            {
                ApplyHoneySlow(damageableComponent, runtime);
            }

            if (runtime.mosquitoEnabled)
            {
                ApplyMosquitoHeal(damageableComponent, runtime);
            }

            if (runtime.explosionEnabled)
            {
                ApplyExplosion(damageableComponent.gameObject, runtime);
            }
        }

        private void ApplyPoison(Component damageableComponent, SyringeSpecialRuntime runtime)
        {
            Monster monster = damageableComponent.GetComponent<Monster>() ??
                              damageableComponent.GetComponentInParent<Monster>();

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
                runtime.poisonTickDamage
            );
        }

        private void ApplyHoneySlow(Component damageableComponent, SyringeSpecialRuntime runtime)
        {
            Monster monster = damageableComponent.GetComponent<Monster>() ??
                              damageableComponent.GetComponentInParent<Monster>();

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

        private void ApplyMosquitoHeal(Component damageableComponent, SyringeSpecialRuntime runtime)
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

            TryHealPlayer(healAmount);
        }

        private bool IsBossLikeTarget(Component damageableComponent)
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

        private void TryHealPlayer(float healAmount)
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
                        "[이기어침/모기침] Character에서 체력 회복 메서드를 찾지 못했습니다. " +
                        "GainHealth(float), Heal(float), AddHealth(float), RestoreHealth(float) 중 하나가 필요합니다."
                    );

                    hasWarnedHealMethodMissing = true;
                }

                return;
            }

            healMethod.Invoke(sourceCharacter, new object[] { healAmount });
        }

        private void ApplyExplosion(GameObject originalTarget, SyringeSpecialRuntime runtime)
        {
            if (cursorNeedleTransform == null)
            {
                return;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(
                cursorNeedleTransform.position,
                runtime.explosionRadius
            );

            HashSet<int> damagedIds = new HashSet<int>();

            foreach (Collider2D hit in hits)
            {
                Monster monster;

                if (!TryGetValidMonsterTarget(hit, out monster))
                {
                    continue;
                }

                int splashId = monster.gameObject.GetInstanceID();

                if (damagedIds.Contains(splashId))
                {
                    continue;
                }

                IDamageable splashDamageable = monster as IDamageable;
                Component splashComponent = monster;

                if (splashDamageable == null || splashComponent == null)
                {
                    continue;
                }

                damagedIds.Add(splashId);
                splashDamageable.TakeDamage(runtime.explosionDamage, Vector2.zero, false);
            }
        }

        private void DestroySelf()
        {
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (sourceCharacter != null && sourceCharacter.OnDeath != null)
            {
                sourceCharacter.OnDeath.RemoveListener(DestroySelf);
            }
        }
    }
}