using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vampire
{
    public class CursorControlledNeedleController : MonoBehaviour
    {
        private Character sourceCharacter;
        private EntityManager entityManager;
        private SyringeDartAbility sourceNeedleAbility;

        private GameObject projectilePrefab;
        private LayerMask monsterLayer;

        private float followSpeed;
        private float hitRadius;
        private float damageMultiplier;
        private float damageInterval;
        private float visualScale;
        private float homingHitRadiusBonus;

        private Vector2 backDisplayOffset;
        private float backDisplaySpacing;
        private float backDisplayArcHeight;
        private float backDisplayScale;

        private Transform cursorNeedleTransform;
        private Transform backDisplayRoot;
        private SpriteRenderer cursorNeedleRenderer;

        private Sprite projectileSprite;
        private int projectileSortingLayerId;
        private int projectileSortingOrder;
        private Vector3 projectileVisualBaseScale = Vector3.one;
        private Quaternion projectileVisualBaseRotation = Quaternion.identity;

        [Header("Needle Visual")]
        [Tooltip("침 이미지의 날 부분이 이동 방향을 향하도록 보정하는 각도입니다. 침 방향이 반대면 135, -45, 45, -135 중 하나로 테스트하세요.")]
        [SerializeField] private float visualForwardAngleOffset = 135f;

        private int lastDisplayedSpecialCount = -1;

        private readonly List<GameObject> backDisplayNeedles = new List<GameObject>();
        private readonly Dictionary<int, float> nextDamageAllowedTimeByTarget =
            new Dictionary<int, float>();

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
            float backDisplayScale)
        {
            GameObject controllerObject = new GameObject("Cursor Controlled Needle Controller");
            CursorControlledNeedleController controller =
                controllerObject.AddComponent<CursorControlledNeedleController>();

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
                backDisplayScale
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
            float backDisplayScale)
        {
            this.sourceCharacter = sourceCharacter;
            this.entityManager = entityManager;
            this.sourceNeedleAbility = sourceNeedleAbility;

            this.followSpeed = Mathf.Max(0.01f, followSpeed);
            this.hitRadius = Mathf.Max(0.05f, hitRadius);
            this.damageMultiplier = Mathf.Max(0.01f, damageMultiplier);
            this.damageInterval = Mathf.Max(0.05f, damageInterval);
            this.visualScale = Mathf.Max(0.01f, visualScale);
            this.homingHitRadiusBonus = Mathf.Max(0f, homingHitRadiusBonus);

            this.backDisplayOffset = backDisplayOffset;
            this.backDisplaySpacing = Mathf.Max(0.01f, backDisplaySpacing);
            this.backDisplayArcHeight = Mathf.Max(0f, backDisplayArcHeight);
            this.backDisplayScale = Mathf.Max(0.01f, backDisplayScale);

            projectilePrefab = sourceNeedleAbility.ProjectilePrefab;
            monsterLayer = sourceNeedleAbility.MonsterLayer;

            CacheProjectileVisualInfo();

            transform.position = sourceCharacter.CenterTransform.position;

            CreateCursorNeedleVisual();
            CreateBackDisplayRoot();

            if (sourceCharacter.OnDeath != null)
            {
                sourceCharacter.OnDeath.AddListener(DestroySelf);
            }

            UpdateBackDisplay(true);
        }

        private void Update()
        {
            if (sourceCharacter == null || sourceNeedleAbility == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = sourceCharacter.CenterTransform.position;

            UpdateCursorNeedleFollow();
            UpdateCursorNeedleHeavyVisual();
            UpdateBackDisplay(false);
            DetectAndDamageEnemies();
        }

        private void CacheProjectileVisualInfo()
        {
            SpriteRenderer sourceRenderer = FindPreferredProjectileSpriteRenderer();

            if (sourceRenderer != null)
            {
                projectileSprite = sourceRenderer.sprite;
                projectileSortingLayerId = sourceRenderer.sortingLayerID;
                projectileSortingOrder = sourceRenderer.sortingOrder;
                projectileVisualBaseScale = sourceRenderer.transform.localScale;
                projectileVisualBaseRotation = sourceRenderer.transform.localRotation;
            }
            else
            {
                Debug.LogWarning("[이기어침] 투사체 프리팹에서 SpriteRenderer를 찾지 못했습니다. 이기어침 시각 오브젝트가 보이지 않을 수 있습니다.");
                projectileVisualBaseScale = Vector3.one;
                projectileVisualBaseRotation = Quaternion.identity;
            }
        }

        private SpriteRenderer FindPreferredProjectileSpriteRenderer()
        {
            if (projectilePrefab == null)
            {
                return null;
            }

            SpriteRenderer[] renderers = projectilePrefab.GetComponentsInChildren<SpriteRenderer>(true);

            SpriteRenderer firstEnabledRenderer = null;
            SpriteRenderer firstRendererWithSprite = null;

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];

                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                if (firstRendererWithSprite == null)
                {
                    firstRendererWithSprite = renderer;
                }

                if (renderer.enabled && firstEnabledRenderer == null)
                {
                    firstEnabledRenderer = renderer;
                }

                if (renderer.gameObject.name.IndexOf("Visual", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    renderer.gameObject.name.IndexOf("Needle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return renderer;
                }
            }

            return firstEnabledRenderer != null ? firstEnabledRenderer : firstRendererWithSprite;
        }

        private void CreateCursorNeedleVisual()
        {
            GameObject cursorNeedleObject = new GameObject("Cursor Controlled Needle");
            cursorNeedleTransform = cursorNeedleObject.transform;
            cursorNeedleTransform.SetParent(transform);

            Vector3 startPosition = GetMouseWorldPositionOrPlayerPosition();
            cursorNeedleTransform.position = startPosition;

            cursorNeedleRenderer = cursorNeedleObject.AddComponent<SpriteRenderer>();
            cursorNeedleRenderer.sprite = projectileSprite;
            cursorNeedleRenderer.sortingLayerID = projectileSortingLayerId;
            cursorNeedleRenderer.sortingOrder = projectileSortingOrder + 10;
            cursorNeedleRenderer.color = new Color(1f, 1f, 1f, 0.95f);

            cursorNeedleTransform.localScale = projectileVisualBaseScale * visualScale;
            cursorNeedleTransform.localRotation = projectileVisualBaseRotation;
        }

        private void CreateBackDisplayRoot()
        {
            GameObject backRootObject = new GameObject("Cursor Needle Back Display");
            backDisplayRoot = backRootObject.transform;
            backDisplayRoot.SetParent(transform);
            backDisplayRoot.localPosition = backDisplayOffset;
        }

        private void UpdateCursorNeedleFollow()
        {
            if (cursorNeedleTransform == null)
            {
                return;
            }

            Vector3 targetPosition = GetMouseWorldPositionOrPlayerPosition();
            Vector3 currentPosition = cursorNeedleTransform.position;

            float lerpFactor = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            Vector3 nextPosition = Vector3.Lerp(currentPosition, targetPosition, lerpFactor);
            Vector3 moveDirection = nextPosition - currentPosition;

            cursorNeedleTransform.position = nextPosition;

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                cursorNeedleTransform.rotation = Quaternion.Euler(0f, 0f, angle + visualForwardAngleOffset);
            }
        }

        private void UpdateCursorNeedleHeavyVisual()
        {
            if (cursorNeedleTransform == null || sourceNeedleAbility == null)
            {
                return;
            }

            float heavySizeMultiplier = sourceNeedleAbility.GetCursorNeedleHeavySizeMultiplier();

            cursorNeedleTransform.localScale =
                projectileVisualBaseScale * visualScale * heavySizeMultiplier;

            if (cursorNeedleRenderer != null)
            {
                float chargeRatio = sourceNeedleAbility.GetCursorHeavyChargeRatio();

                float brightness = Mathf.Lerp(0.95f, 1.25f, chargeRatio);
                cursorNeedleRenderer.color = new Color(brightness, brightness, brightness, 0.95f);
            }
        }

        private Vector3 GetMouseWorldPositionOrPlayerPosition()
        {
            if (Mouse.current != null && Camera.main != null && sourceCharacter != null)
            {
                Vector3 screenPosition = Mouse.current.position.ReadValue();
                screenPosition.z = Mathf.Abs(
                    Camera.main.transform.position.z -
                    sourceCharacter.CenterTransform.position.z
                );

                Vector3 worldPosition = Camera.main.ScreenToWorldPoint(screenPosition);
                worldPosition.z = sourceCharacter.CenterTransform.position.z;

                return worldPosition;
            }

            if (sourceCharacter != null)
            {
                return sourceCharacter.CenterTransform.position;
            }

            return Vector3.zero;
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
                needleTransform.localRotation =
                    Quaternion.Euler(0f, 0f, -90f - normalized * 18f + visualForwardAngleOffset);

                SpriteRenderer renderer = needleObject.AddComponent<SpriteRenderer>();
                renderer.sprite = projectileSprite;
                renderer.sortingLayerID = projectileSortingLayerId;
                renderer.sortingOrder = projectileSortingOrder + 8;
                renderer.color = new Color(1f, 1f, 1f, 0.75f);

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

            if (monster == null)
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

        private bool IsSameTarget(GameObject originalTarget, Monster monster)
        {
            if (originalTarget == null || monster == null)
            {
                return false;
            }

            if (originalTarget == monster.gameObject)
            {
                return true;
            }

            Monster originalMonster = originalTarget.GetComponentInParent<Monster>();

            return originalMonster != null && originalMonster == monster;
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

            Collider2D[] hits = Physics2D.OverlapCircleAll(
                cursorNeedleTransform.position,
                effectiveHitRadius
            );

            if (hits == null || hits.Length == 0)
            {
                return;
            }

            int maxTargetsThisTick = CalculateMaxTargetsThisTick(runtime);
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

                if (damagedTargetCount >= maxTargetsThisTick)
                {
                    break;
                }
            }
        }

        private int CalculateMaxTargetsThisTick(SyringeSpecialRuntime runtime)
        {
            if (sourceNeedleAbility.IsCursorNeedleHeavyPierceUnlimited())
            {
                return int.MaxValue;
            }

            int maxTargets = 1;

            if (runtime.pierceEnabled)
            {
                if (runtime.pierceCount >= int.MaxValue / 2)
                {
                    return int.MaxValue;
                }

                maxTargets += Mathf.Max(0, runtime.pierceCount);
            }

            int heavyPierceBonus = sourceNeedleAbility.GetCursorNeedleHeavyPierceBonus();

            if (heavyPierceBonus >= int.MaxValue / 2)
            {
                return int.MaxValue;
            }

            maxTargets += Mathf.Max(0, heavyPierceBonus);

            return Mathf.Max(1, maxTargets);
        }

        private bool CanDamageTarget(int targetId)
        {
            if (!nextDamageAllowedTimeByTarget.TryGetValue(targetId, out float nextAllowedTime))
            {
                return true;
            }

            return Time.time >= nextAllowedTime;
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

            float rawDamage =
                sourceNeedleAbility.GetEffectiveDamage() *
                damageMultiplier *
                heavyDamageMultiplier;

            float knockback = sourceNeedleAbility.GetEffectiveKnockback() * heavyKnockbackMultiplier;

            PlayerGeneralStatRuntime statRuntime =
                PlayerGeneralStatRuntime.GetOrCreate(sourceCharacter);

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

            if (isCritical)
            {
                Debug.Log($"[치명타] 이기어침 치명타 발생 | 피해 {finalDamage:0.##}");
            }

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
            Monster monster =
                damageableComponent.GetComponent<Monster>() ??
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
            Monster monster =
                damageableComponent.GetComponent<Monster>() ??
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

                if (IsSameTarget(originalTarget, monster))
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

        private void OnDrawGizmosSelected()
        {
            if (cursorNeedleTransform == null)
            {
                return;
            }

            float previewRadius = hitRadius;

            if (sourceNeedleAbility != null)
            {
                previewRadius *= sourceNeedleAbility.GetCursorNeedleHeavySizeMultiplier();
            }

            Gizmos.DrawWireSphere(cursorNeedleTransform.position, previewRadius);
        }
    }
}