using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 전설증강: 이기어침
    ///
    /// 정리 목표
    /// - 마우스 추적 제거
    /// - 플레이어 살짝 위를 중심으로 가로 8자 무한궤도 유지
    /// - 침 본체 위치는 절대 몬스터 쪽으로 끌려가지 않고 궤도 위에 고정
    /// - 침의 날 부분은 궤도의 접선 방향을 바라보며 자연스럽게 회전
    /// - 몬스터 보정은 위치 보정이 아니라 아주 작은 피해 판정 보정만 적용
    /// - 스프라이트는 별도 Override를 쓰지 않고, 기존 기본 침 Projectile Prefab 안의 SpriteRenderer를 자동 탐색
    /// - 등 뒤 특수증강 표시 침은 비행용 회전값과 분리하여 부채꼴로 세움
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

        private float orbitVisualAngleOffset;
        private float backDisplayBaseAngle;
        private float backDisplaySpreadAngle;
        private float orbitStraightness;
        private float maxAssistHitRadiusBonus;
        private float hitRadiusBonusPerSpecial;
        private float maxHitRadiusBonusFromSpecial;

        private Transform cursorNeedleTransform;
        private Transform cursorNeedleVisualTransform;
        private Transform backDisplayRoot;
        private SpriteRenderer cursorNeedleRenderer;

        private Sprite projectileSprite;
        private int projectileSortingLayerId;
        private int projectileSortingOrder;
        private Vector3 projectileVisualBaseScale = Vector3.one;
        private Vector3 projectileVisualLocalPosition = Vector3.zero;
        private Quaternion projectileVisualLocalRotation = Quaternion.identity;

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
            float orbitStraightness,
            float maxAssistHitRadiusBonus,
float hitRadiusBonusPerSpecial,
float maxHitRadiusBonusFromSpecial)
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
                orbitStraightness,
                maxAssistHitRadiusBonus,
hitRadiusBonusPerSpecial,
maxHitRadiusBonusFromSpecial
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
            float orbitStraightness,
            float maxAssistHitRadiusBonus,
float hitRadiusBonusPerSpecial,
float maxHitRadiusBonusFromSpecial)
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
            this.maxAssistHitRadiusBonus = Mathf.Max(0f, maxAssistHitRadiusBonus);
            this.hitRadiusBonusPerSpecial = Mathf.Max(0f, hitRadiusBonusPerSpecial);
            this.maxHitRadiusBonusFromSpecial = Mathf.Max(0f, maxHitRadiusBonusFromSpecial);
            projectilePrefab = sourceNeedleAbility.ProjectilePrefab;
            monsterLayer = sourceNeedleAbility.MonsterLayer;

            CacheProjectileVisualInfo();

            transform.position = GetOrbitCenter();

            CreateCursorNeedleVisual();
            CreateBackDisplayRoot();

            if (sourceCharacter != null && sourceCharacter.OnDeath != null)
            {
                sourceCharacter.OnDeath.AddListener(DestroySelf);
            }

            UpdateBackDisplay(true);

            if (debugOrbitLog)
            {
                Debug.Log(
                    $"[이기어침] 생성 | OrbitSpeed={orbitBaseSpeed:0.##}, Straightness={orbitStraightness:0.##}, " +
                    $"OrbitRadius=({orbitHorizontalRadius:0.##}, {orbitVerticalRadius:0.##}), " +
                    $"AssistBonusMax={maxAssistHitRadiusBonus:0.##}"
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

        /// <summary>
        /// 기존 기본침 Projectile Prefab 안에 들어 있는 침 SpriteRenderer를 자동으로 찾습니다.
        /// 별도 Sprite Override를 쓰지 않습니다.
        /// </summary>
        private void CacheProjectileVisualInfo()
        {
            SpriteRenderer sourceRenderer = FindPreferredProjectileSpriteRenderer();

            if (sourceRenderer == null)
            {
                Debug.LogWarning(
                    "[이기어침] 기본 침 Projectile Prefab 안에서 사용할 SpriteRenderer를 찾지 못했습니다. " +
                    "Projectile Prefab의 침 이미지 오브젝트 이름에 Syringe, Needle, Dart, 침, 주사 중 하나가 들어가면 더 안정적으로 찾습니다.",
                    this
                );

                projectileSortingLayerId = SortingLayer.NameToID("Default");
                projectileSortingOrder = 0;
                projectileVisualBaseScale = Vector3.one;
                projectileVisualLocalPosition = Vector3.zero;
                projectileVisualLocalRotation = Quaternion.identity;
                return;
            }

            projectileSprite = sourceRenderer.sprite;
            projectileSortingLayerId = sourceRenderer.sortingLayerID;
            projectileSortingOrder = sourceRenderer.sortingOrder;
            projectileVisualBaseScale = sourceRenderer.transform.localScale;
            projectileVisualLocalPosition = GetLocalPositionRelativeToProjectileRoot(sourceRenderer.transform);
            projectileVisualLocalRotation = GetLocalRotationRelativeToProjectileRoot(sourceRenderer.transform);

            if (debugOrbitLog)
            {
                Debug.Log(
                    $"[이기어침] 기본침 스프라이트 자동 선택 | Object={sourceRenderer.gameObject.name}, Sprite={projectileSprite.name}",
                    sourceRenderer
                );
            }
        }

        private SpriteRenderer FindPreferredProjectileSpriteRenderer()
        {
            if (projectilePrefab == null)
            {
                return null;
            }

            SpriteRenderer[] renderers = projectilePrefab.GetComponentsInChildren<SpriteRenderer>(true);

            if (renderers == null || renderers.Length == 0)
            {
                return null;
            }

            SpriteRenderer bestRenderer = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];

                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                string objectName = renderer.gameObject.name.ToLower();
                string spriteName = renderer.sprite.name.ToLower();
                string combinedName = objectName + " " + spriteName;

                int score = 0;

                if (combinedName.Contains("syringe")) score += 200;
                if (combinedName.Contains("needle")) score += 200;
                if (combinedName.Contains("dart")) score += 120;
                if (combinedName.Contains("침")) score += 200;
                if (combinedName.Contains("주사")) score += 200;

                if (combinedName.Contains("visual")) score += 30;
                if (renderer.enabled) score += 20;
                if (renderer.gameObject.activeInHierarchy) score += 10;

                if (combinedName.Contains("shuriken")) score -= 500;
                if (combinedName.Contains("kunai")) score -= 250;
                if (combinedName.Contains("표창")) score -= 500;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestRenderer = renderer;
                }
            }

            if (bestRenderer != null && bestScore > -100)
            {
                return bestRenderer;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];

                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                string spriteName = renderer.sprite.name.ToLower();
                string objectName = renderer.gameObject.name.ToLower();

                if (spriteName.Contains("shuriken") || objectName.Contains("shuriken") ||
                    spriteName.Contains("표창") || objectName.Contains("표창"))
                {
                    continue;
                }

                return renderer;
            }

            return bestRenderer;
        }

        private Vector3 GetLocalPositionRelativeToProjectileRoot(Transform visualTransform)
        {
            if (visualTransform == null || projectilePrefab == null)
            {
                return Vector3.zero;
            }

            Transform root = projectilePrefab.transform;

            if (visualTransform == root)
            {
                return Vector3.zero;
            }

            return root.InverseTransformPoint(visualTransform.position);
        }

        private Quaternion GetLocalRotationRelativeToProjectileRoot(Transform visualTransform)
        {
            if (visualTransform == null || projectilePrefab == null)
            {
                return Quaternion.identity;
            }

            Transform root = projectilePrefab.transform;

            if (visualTransform == root)
            {
                return visualTransform.localRotation;
            }

            return Quaternion.Inverse(root.rotation) * visualTransform.rotation;
        }

        private void CreateCursorNeedleVisual()
        {
            GameObject cursorNeedleObject = new GameObject("Cursor Controlled Needle - Orbit");
            cursorNeedleTransform = cursorNeedleObject.transform;
            cursorNeedleTransform.SetParent(transform);

            Vector3 startPosition = GetOrbitPointWorld(orbitPhase);
            cursorNeedleTransform.position = startPosition;

            GameObject visualObject = new GameObject("Visual");
            cursorNeedleVisualTransform = visualObject.transform;
            cursorNeedleVisualTransform.SetParent(cursorNeedleTransform, false);
            cursorNeedleVisualTransform.localPosition = projectileVisualLocalPosition;
            cursorNeedleVisualTransform.localRotation = projectileVisualLocalRotation;
            cursorNeedleVisualTransform.localScale = projectileVisualBaseScale * visualScale;

            cursorNeedleRenderer = visualObject.AddComponent<SpriteRenderer>();
            cursorNeedleRenderer.sprite = projectileSprite;
            cursorNeedleRenderer.sortingLayerID = projectileSortingLayerId;
            cursorNeedleRenderer.sortingOrder = projectileSortingOrder + 10;
            cursorNeedleRenderer.color = new Color(1f, 1f, 1f, 0.95f);
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

            while (orbitPhase > Mathf.PI * 2f)
            {
                orbitPhase -= Mathf.PI * 2f;
            }

            Vector3 orbitPoint = GetOrbitPointWorld(orbitPhase);
            cursorNeedleTransform.position = orbitPoint;

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

        private Vector3 GetOrbitPointWorld(float phase)
        {
            Vector3 center = GetOrbitCenter();

            // Bernoulli Lemniscate 기반 가로 8자.
            // 중앙 교차와 양 끝 곡선 연결이 자연스럽고,
            // 침의 접선 방향 계산도 안정적입니다.
            float sin = Mathf.Sin(phase);
            float cos = Mathf.Cos(phase);

            float curveTightness = Mathf.Lerp(0.75f, 1.45f, orbitStraightness);
            float denominator = 1f + sin * sin * curveTightness;

            float x = (cos / denominator) * orbitHorizontalRadius;
            float y = ((sin * cos * 2f) / denominator) * orbitVerticalRadius;

            return center + new Vector3(x, y, 0f);
        }

        private Vector3 GetOrbitTangentDirectionWorld(float phase)
        {
            const float delta = 0.012f;

            Vector3 previousPoint = GetOrbitPointWorld(phase - delta);
            Vector3 nextPoint = GetOrbitPointWorld(phase + delta);

            Vector3 tangent = nextPoint - previousPoint;

            if (tangent.sqrMagnitude <= 0.0001f)
            {
                return Vector3.right;
            }

            return tangent.normalized;
        }

        private void ApplyNeedleRotationByDirection(Vector3 direction)
        {
            if (cursorNeedleTransform == null || direction.sqrMagnitude <= 0.0001f)
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

        private float GetSpecialHitRadiusBonus()
        {
            if (sourceNeedleAbility == null)
            {
                return 0f;
            }

            int specialCount = sourceNeedleAbility.GetActiveSpecialAugmentCount();
            int bonusCount = maxSpecialBonusCount > 0
                ? Mathf.Min(specialCount, maxSpecialBonusCount)
                : specialCount;

            float bonus = bonusCount * hitRadiusBonusPerSpecial;

            if (maxHitRadiusBonusFromSpecial > 0f)
            {
                bonus = Mathf.Min(bonus, maxHitRadiusBonusFromSpecial);
            }

            return bonus;
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
            if (cursorNeedleVisualTransform == null || sourceNeedleAbility == null)
            {
                return;
            }

            float heavySizeMultiplier = sourceNeedleAbility.GetCursorNeedleHeavySizeMultiplier();
            cursorNeedleVisualTransform.localScale =
                projectileVisualBaseScale * visualScale * heavySizeMultiplier;

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
                needleTransform.localScale = Vector3.one;
                needleTransform.localRotation =
                    Quaternion.Euler(0f, 0f, backDisplayBaseAngle - normalized * backDisplaySpreadAngle);

                GameObject visualObject = new GameObject("Visual");
                Transform visualTransform = visualObject.transform;
                visualTransform.SetParent(needleTransform, false);
                visualTransform.localPosition = projectileVisualLocalPosition;
                visualTransform.localRotation = projectileVisualLocalRotation;
                visualTransform.localScale = projectileVisualBaseScale * backDisplayScale;

                SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
                renderer.sprite = projectileSprite;
                renderer.sortingLayerID = projectileSortingLayerId;
                renderer.sortingOrder = projectileSortingOrder + 8;
                renderer.color = new Color(1f, 1f, 1f, 0.82f);

                backDisplayNeedles.Add(needleObject);
            }
        }

        private bool TryGetValidDamageableTarget(
     Collider2D collider,
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

                if (monster != null)
                {
                    if (!IsValidMonsterForCursorNeedle(monster))
                    {
                        continue;
                    }
                }

                damageable = candidate;
                damageableComponent = component;
                targetId = component.gameObject.GetInstanceID();

                return true;
            }

            return false;
        }

        private bool IsValidMonsterForCursorNeedle(Monster monster)
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

            // 기본 반경은 그대로 유지.
            // 특수증강 개수에 따른 반경 보너스만 별도로 더한다.
            float effectiveHitRadius = hitRadius * heavySizeMultiplier;
            effectiveHitRadius += GetSpecialHitRadiusBonus();

            if (runtime.homingEnabled)
            {
                effectiveHitRadius += homingHitRadiusBonus;
            }
            if (targetAssistRadius > 0f && targetAssistStrength > 0f)
            {
                float assistBonus = targetAssistRadius * targetAssistStrength;
                assistBonus = Mathf.Min(assistBonus, maxAssistHitRadiusBonus);
                effectiveHitRadius += assistBonus;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(
                cursorNeedleTransform.position,
                effectiveHitRadius,
                monsterLayer
            );

            if (hits == null || hits.Length == 0)
            {
                return;
            }

            int damagedTargetCount = 0;
            HashSet<int> checkedTargetsThisFrame = new HashSet<int>();

            foreach (Collider2D hit in hits)
            {
                IDamageable damageable;
                Component damageableComponent;
                int targetId;

                if (!SyringeSpecialHitEffectUtility.TryGetValidDamageableTarget(
                        hit,
                        sourceCharacter,
                        out damageable,
                        out damageableComponent,
                        out targetId))
                {
                    continue;
                }

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

            Vector2 knockbackDirection =
                (Vector2)damageableComponent.transform.position -
                (Vector2)cursorNeedleTransform.position;

            if (knockbackDirection.sqrMagnitude > 0.0001f)
            {
                knockbackDirection.Normalize();
            }

            float heavyDamageMultiplier = sourceNeedleAbility.GetCursorNeedleHeavyDamageMultiplier();
            float heavyKnockbackMultiplier = sourceNeedleAbility.GetCursorNeedleHeavyKnockbackMultiplier();
            float specialDamageMultiplier = GetSpecialDamageMultiplier();

            bool consumedNeedleMark;
            float statusDamageMultiplier = SyringeSpecialHitEffectUtility.GetPreDamageMultiplier(
                damageableComponent,
                runtime,
                out consumedNeedleMark
            );

            float rawDamage =
                sourceNeedleAbility.GetEffectiveDamage() *
                damageMultiplier *
                heavyDamageMultiplier *
                specialDamageMultiplier *
                statusDamageMultiplier;

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

            // 이기어침 피해를 기존 총 피해량과 증강별 피해량에 동시에 기록
            ReportCursorNeedleDamage(finalDamage);

            SyringeSpecialHitEffectUtility.ApplyPostHitEffects(
                damageableComponent,
                runtime,
                sourceCharacter,
                cursorNeedleTransform.position,
                monsterLayer,
                damageableComponent.gameObject,
                consumedNeedleMark
            );
        }
        // =========================================================
        // Damage Report
        // =========================================================

        private void ReportCursorNeedleDamage(float dealtDamage)
        {
            if (dealtDamage <= 0f)
            {
                return;
            }

            // 기존 전체 피해량 시스템 유지
            if (sourceCharacter != null && sourceCharacter.OnDealDamage != null)
            {
                sourceCharacter.OnDealDamage.Invoke(dealtDamage);
            }

            // 증강별 피해량 기록
            if (AugmentDamageTracker.Instance != null)
            {
                AugmentDamageTracker.Instance.RecordDamage(
                    "이기어침",
                    dealtDamage
                );
            }
        }

        private void ApplySpecialEffectsAfterHit(
    Component damageableComponent,
    SyringeSpecialRuntime runtime,
    bool consumedNeedleMark)
        {
            if (damageableComponent == null)
            {
                return;
            }

            if (runtime.poisonEnabled)
            {
                ApplyPoison(damageableComponent, runtime);
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
        private float GetCorrosionDamageMultiplier(Component damageableComponent)
        {
            Monster monster = damageableComponent.GetComponent<Monster>() ??
                              damageableComponent.GetComponentInParent<Monster>();

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

        private void ApplyCorrosion(Component damageableComponent, SyringeSpecialRuntime runtime)
        {
            Monster monster = damageableComponent.GetComponent<Monster>() ??
                              damageableComponent.GetComponentInParent<Monster>();

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

        private bool TryConsumeNeedleMark(Component damageableComponent)
        {
            Monster monster = damageableComponent.GetComponent<Monster>() ??
                              damageableComponent.GetComponentInParent<Monster>();

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

        private void ApplyNeedleMark(Component damageableComponent, SyringeSpecialRuntime runtime)
        {
            Monster monster = damageableComponent.GetComponent<Monster>() ??
                              damageableComponent.GetComponentInParent<Monster>();

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
        private void ApplyMosquitoHeal(Component damageableComponent, SyringeSpecialRuntime runtime)
        {
            if (runtime.healingBlocked || sourceCharacter == null)
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
                runtime.explosionRadius,
                monsterLayer
            );

            HashSet<int> damagedIds = new HashSet<int>();

            foreach (Collider2D hit in hits)
            {
                IDamageable splashDamageable;
                Component splashComponent;
                int splashId;

                if (!TryGetValidDamageableTarget(hit, out splashDamageable, out splashComponent, out splashId))
                {
                    continue;
                }

                if (damagedIds.Contains(splashId))
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
            Gizmos.color = Color.cyan;
            Vector3 center = Application.isPlaying ? GetOrbitCenter() : transform.position;

            const int segments = 96;
            Vector3 previous = center;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments * Mathf.PI * 2f;
                Vector3 point = center + new Vector3(
                    Mathf.Cos(t) * orbitHorizontalRadius,
                    Mathf.Sin(t) * Mathf.Cos(t) * 2f * orbitVerticalRadius,
                    0f
                );

                if (i > 0)
                {
                    Gizmos.DrawLine(previous, point);
                }

                previous = point;
            }

            if (cursorNeedleTransform != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(cursorNeedleTransform.position, hitRadius + maxAssistHitRadiusBonus);
            }
        }
    }
}