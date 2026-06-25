using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class HedgehogNeedleController : MonoBehaviour
    {
        private struct NeedleTargetKey : IEquatable<NeedleTargetKey>
        {
            public int needleIndex;
            public int targetId;

            public NeedleTargetKey(int needleIndex, int targetId)
            {
                this.needleIndex = needleIndex;
                this.targetId = targetId;
            }

            public bool Equals(NeedleTargetKey other)
            {
                return needleIndex == other.needleIndex && targetId == other.targetId;
            }

            public override bool Equals(object obj)
            {
                return obj is NeedleTargetKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (needleIndex * 397) ^ targetId;
                }
            }
        }

        private Character sourceCharacter;
        private EntityManager entityManager;
        private SyringeDartAbility sourceNeedleAbility;

        private GameObject projectilePrefab;
        private LayerMask monsterLayer;

        private int fallbackNeedleCount;
        private float orbitRadius;
        private float rotationSpeed;
        private float touchFireCooldown;
        private float damageMultiplier;

        private Transform visualRoot;
        private readonly List<Transform> orbitNeedleVisuals = new List<Transform>();

        private Sprite projectileSprite;
        private int projectileSortingLayerId;
        private int projectileSortingOrder;
        private Vector3 projectileVisualBaseScale = Vector3.one;
        private Quaternion projectileVisualBaseRotation = Quaternion.identity;

        private int currentOrbitNeedleCount = -1;

        [Header("Needle Visual")]
        [Tooltip("침 이미지의 날 부분이 바깥쪽을 향하도록 보정하는 각도입니다. 방향이 이상하면 135, -45, 45, -135 중 하나로 테스트하세요.")]
        [SerializeField] private float visualForwardAngleOffset = 135f;

        [Tooltip("고슴도침 회전 침 시각 크기 배율입니다. Visual_Needle의 실제 크기에 곱해집니다.")]
        [SerializeField] private float orbitVisualScaleMultiplier = 1f;

        [Tooltip("고슴도침 회전 속도 배율입니다. 기존 속도가 너무 빠르므로 기본값은 0.35배입니다.")]
        [SerializeField] private float rotationSpeedMultiplier = 0.35f;

        [Header("Needle Hit")]
        [Tooltip("회전하는 침 하나하나의 피격 반경입니다.")]
        [SerializeField] private float orbitNeedleHitRadius = 0.18f;

        [Tooltip("같은 회전 침이 같은 적에게 다시 피해를 줄 수 있기까지의 시간입니다. 1번 침에게 맞은 적은 3초 동안 1번 침에게 다시 맞지 않습니다.")]
        [SerializeField] private float sameNeedleSameTargetCooldown = 3f;

        [Tooltip("고슴도침 데미지 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        private readonly Dictionary<NeedleTargetKey, float> nextDamageAllowedTimeByNeedleAndTarget =
            new Dictionary<NeedleTargetKey, float>();

        private readonly List<NeedleTargetKey> removeCooldownKeys = new List<NeedleTargetKey>();

        public static HedgehogNeedleController Create(
            Character sourceCharacter,
            EntityManager entityManager,
            SyringeDartAbility sourceNeedleAbility,
            int needleCount,
            float orbitRadius,
            float rotationSpeed,
            float touchFireCooldown,
            float damageMultiplier)
        {
            GameObject controllerObject = new GameObject("Hedgehog Needle Barrier");
            HedgehogNeedleController controller = controllerObject.AddComponent<HedgehogNeedleController>();

            controller.Init(
                sourceCharacter,
                entityManager,
                sourceNeedleAbility,
                needleCount,
                orbitRadius,
                rotationSpeed,
                touchFireCooldown,
                damageMultiplier
            );

            return controller;
        }

        private void Init(
            Character sourceCharacter,
            EntityManager entityManager,
            SyringeDartAbility sourceNeedleAbility,
            int needleCount,
            float orbitRadius,
            float rotationSpeed,
            float touchFireCooldown,
            float damageMultiplier)
        {
            this.sourceCharacter = sourceCharacter;
            this.entityManager = entityManager;
            this.sourceNeedleAbility = sourceNeedleAbility;

            fallbackNeedleCount = Mathf.Max(1, needleCount);
            this.orbitRadius = Mathf.Max(0.1f, orbitRadius);
            this.rotationSpeed = rotationSpeed;

            // 기존 인스펙터 값도 살리되, 기본적으로는 3초 쿨타임을 사용한다.
            this.touchFireCooldown = Mathf.Max(0.05f, touchFireCooldown);
            sameNeedleSameTargetCooldown = Mathf.Max(3f, sameNeedleSameTargetCooldown);

            this.damageMultiplier = Mathf.Max(0.01f, damageMultiplier);

            projectilePrefab = sourceNeedleAbility.ProjectilePrefab;
            monsterLayer = sourceNeedleAbility.MonsterLayer;

            CacheProjectileVisualInfo();

            transform.position = GetSourceCenterPosition();

            RebuildOrbitVisuals(GetDesiredOrbitNeedleCount());

            if (sourceCharacter != null && sourceCharacter.OnDeath != null)
            {
                sourceCharacter.OnDeath.AddListener(DestroySelf);
            }

            if (debugLog)
            {
                Debug.Log("[고슴도침] HedgehogNeedleController 생성 완료 - 회전 침별 개별 피격 판정 사용");
            }
        }

        private void Update()
        {
            if (sourceCharacter == null || entityManager == null || sourceNeedleAbility == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = GetSourceCenterPosition();

            int desiredCount = GetDesiredOrbitNeedleCount();

            if (desiredCount != currentOrbitNeedleCount)
            {
                RebuildOrbitVisuals(desiredCount);
            }

            UpdateOrbitVisualScales();

            if (visualRoot != null)
            {
                visualRoot.Rotate(
                    Vector3.forward,
                    rotationSpeed * Mathf.Max(0f, rotationSpeedMultiplier) * Time.deltaTime
                );
            }

            DetectEnemiesTouchingOrbitNeedles();
            CleanupExpiredCooldowns();
        }

        private int GetDesiredOrbitNeedleCount()
        {
            if (sourceNeedleAbility != null)
            {
                return Mathf.Max(1, sourceNeedleAbility.GetEffectiveProjectileCount());
            }

            return Mathf.Max(1, fallbackNeedleCount);
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
                Debug.LogWarning("[고슴도침] 투사체 프리팹에서 SpriteRenderer를 찾지 못했습니다. 고슴도침 시각 오브젝트가 보이지 않을 수 있습니다.");
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

                if (renderer.gameObject.name.IndexOf("Visual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    renderer.gameObject.name.IndexOf("Needle", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return renderer;
                }
            }

            return firstEnabledRenderer != null ? firstEnabledRenderer : firstRendererWithSprite;
        }

        private Vector2 GetSourceCenterPosition()
        {
            if (sourceCharacter == null)
            {
                return transform.position;
            }

            if (sourceCharacter.CenterTransform != null)
            {
                return sourceCharacter.CenterTransform.position;
            }

            return sourceCharacter.transform.position;
        }

        private void RebuildOrbitVisuals(int needleCount)
        {
            if (visualRoot != null)
            {
                Destroy(visualRoot.gameObject);
            }

            orbitNeedleVisuals.Clear();

            currentOrbitNeedleCount = Mathf.Max(1, needleCount);

            visualRoot = new GameObject("Orbit Needle Visuals").transform;
            visualRoot.SetParent(transform);
            visualRoot.localPosition = Vector3.zero;

            for (int i = 0; i < currentOrbitNeedleCount; i++)
            {
                GameObject visualObject = new GameObject($"Orbit Needle Visual {i + 1}");
                visualObject.transform.SetParent(visualRoot);

                // 항상 원점 기준 균등 배치:
                // 2개 = 180도, 3개 = 120도, 4개 = 90도
                float angle = i * 360f / currentOrbitNeedleCount;
                Vector2 direction = AngleToVector(angle);

                visualObject.transform.localPosition = direction * orbitRadius;
                visualObject.transform.localRotation =
                    Quaternion.Euler(0f, 0f, angle + visualForwardAngleOffset) *
                    projectileVisualBaseRotation;

                SpriteRenderer visualRenderer = visualObject.AddComponent<SpriteRenderer>();

                if (projectileSprite != null)
                {
                    visualRenderer.sprite = projectileSprite;
                    visualRenderer.sortingLayerID = projectileSortingLayerId;
                    visualRenderer.sortingOrder = projectileSortingOrder + 10;
                    visualRenderer.color = new Color(1f, 1f, 1f, 0.65f);
                }

                visualObject.transform.localScale = GetOrbitVisualScale();

                orbitNeedleVisuals.Add(visualObject.transform);
            }

            nextDamageAllowedTimeByNeedleAndTarget.Clear();

            if (debugLog)
            {
                Debug.Log($"[고슴도침] 회전 침 재배치 완료 | 개수 {currentOrbitNeedleCount}");
            }
        }

        private void UpdateOrbitVisualScales()
        {
            Vector3 visualScale = GetOrbitVisualScale();

            for (int i = 0; i < orbitNeedleVisuals.Count; i++)
            {
                if (orbitNeedleVisuals[i] != null)
                {
                    orbitNeedleVisuals[i].localScale = visualScale;
                }
            }
        }

        private Vector3 GetOrbitVisualScale()
        {
            float projectileSizeMultiplier = 1f;

            if (sourceNeedleAbility != null)
            {
                projectileSizeMultiplier = sourceNeedleAbility.GetEffectiveProjectileSizeMultiplier();
            }

            return projectileVisualBaseScale * orbitVisualScaleMultiplier * projectileSizeMultiplier;
        }

        private float GetOrbitHitRadius()
        {
            float projectileSizeMultiplier = 1f;

            if (sourceNeedleAbility != null)
            {
                projectileSizeMultiplier = sourceNeedleAbility.GetEffectiveProjectileSizeMultiplier();
            }

            return Mathf.Max(0.03f, orbitNeedleHitRadius * projectileSizeMultiplier);
        }

        private void DetectEnemiesTouchingOrbitNeedles()
        {
            if (orbitNeedleVisuals == null || orbitNeedleVisuals.Count == 0)
            {
                return;
            }

            float hitRadius = GetOrbitHitRadius();

            for (int needleIndex = 0; needleIndex < orbitNeedleVisuals.Count; needleIndex++)
            {
                Transform needleTransform = orbitNeedleVisuals[needleIndex];

                if (needleTransform == null)
                {
                    continue;
                }

                Collider2D[] hits = Physics2D.OverlapCircleAll(needleTransform.position, hitRadius);

                if (hits == null || hits.Length == 0)
                {
                    continue;
                }

                HashSet<int> checkedTargetsThisNeedle = new HashSet<int>();

                for (int i = 0; i < hits.Length; i++)
                {
                    Collider2D hit = hits[i];

                    Monster monster;

                    if (!TryGetValidMonsterTarget(hit, out monster))
                    {
                        continue;
                    }

                    IDamageable damageable = monster as IDamageable;

                    if (damageable == null)
                    {
                        continue;
                    }

                    int targetId = monster.gameObject.GetInstanceID();

                    if (checkedTargetsThisNeedle.Contains(targetId))
                    {
                        continue;
                    }

                    checkedTargetsThisNeedle.Add(targetId);

                    NeedleTargetKey key = new NeedleTargetKey(needleIndex, targetId);

                    if (!CanDamageWithNeedle(key))
                    {
                        continue;
                    }

                    DamageTargetWithOrbitNeedle(monster, needleIndex);

                    nextDamageAllowedTimeByNeedleAndTarget[key] =
                        Time.time + Mathf.Max(0.05f, sameNeedleSameTargetCooldown);
                }
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

        private bool CanDamageWithNeedle(NeedleTargetKey key)
        {
            if (!nextDamageAllowedTimeByNeedleAndTarget.TryGetValue(key, out float nextAllowedTime))
            {
                return true;
            }

            return Time.time >= nextAllowedTime;
        }

        private void DamageTargetWithOrbitNeedle(Monster targetMonster, int needleIndex)
        {
            if (targetMonster == null || sourceCharacter == null || sourceNeedleAbility == null)
            {
                return;
            }

            IDamageable damageable = targetMonster as IDamageable;
            Component targetComponent = targetMonster;

            if (damageable == null || targetComponent == null)
            {
                return;
            }

            float rawDamage = sourceNeedleAbility.GetEffectiveDamage() * damageMultiplier;

            PlayerGeneralStatRuntime statRuntime =
                PlayerGeneralStatRuntime.GetOrCreate(sourceCharacter);

            bool isCritical = false;
            float finalDamage = rawDamage;

            if (statRuntime != null)
            {
                finalDamage = statRuntime.CalculateOffensiveDamage(
                    sourceCharacter,
                    targetComponent,
                    rawDamage,
                    out isCritical
                );
            }

            // 고슴도침은 넉백 없음.
            damageable.TakeDamage(finalDamage, Vector2.zero);

            if (sourceCharacter.OnDealDamage != null)
            {
                sourceCharacter.OnDealDamage.Invoke(finalDamage);
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[고슴도침] {needleIndex + 1}번 침 피격 | Target={targetMonster.name} | Damage={finalDamage:0.##}"
                );
            }

            if (isCritical)
            {
                Debug.Log($"[치명타] 고슴도침 치명타 발생 | 피해 {finalDamage:0.##}");
            }
        }

        private void CleanupExpiredCooldowns()
        {
            if (nextDamageAllowedTimeByNeedleAndTarget.Count == 0)
            {
                return;
            }

            removeCooldownKeys.Clear();

            foreach (KeyValuePair<NeedleTargetKey, float> pair in nextDamageAllowedTimeByNeedleAndTarget)
            {
                // 너무 오래 지난 기록만 정리한다.
                // 일반 쿨타임보다 조금 늦게 지워도 기능에는 영향 없음.
                if (Time.time >= pair.Value + 1f)
                {
                    removeCooldownKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < removeCooldownKeys.Count; i++)
            {
                nextDamageAllowedTimeByNeedleAndTarget.Remove(removeCooldownKeys[i]);
            }
        }

        private Vector2 AngleToVector(float angleDegrees)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;

            return new Vector2(
                Mathf.Cos(rad),
                Mathf.Sin(rad)
            ).normalized;
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
            Gizmos.DrawWireSphere(transform.position, orbitRadius);

            if (orbitNeedleVisuals != null)
            {
                float hitRadius = GetOrbitHitRadius();

                for (int i = 0; i < orbitNeedleVisuals.Count; i++)
                {
                    if (orbitNeedleVisuals[i] != null)
                    {
                        Gizmos.DrawWireSphere(orbitNeedleVisuals[i].position, hitRadius);
                    }
                }
            }
        }
    }
}