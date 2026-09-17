using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 장기압착이 생성하는 원형 압착장.
    /// 범위 안의 적을 중앙으로 끌어당기고 주기적으로 피해를 준다.
    /// 
    /// OverlapCircleAll이 레이어 문제로 비어도 FindObjectsOfType<Monster>() fallback으로 작동한다.
    /// </summary>
    public class OrganCompressionField : MonoBehaviour
    {
        private readonly Dictionary<int, float> nextDamageTimes = new Dictionary<int, float>();
        private SyringeAugmentVfx augmentVisual;

        private Vector2 center;
        private float radius;
        private float duration;
        private float tickInterval;
        private float damagePerTick;
        private float pullSpeed;
        private LayerMask monsterLayer;

        private float endTime;
        private SyringeDartAbility sourceNeedleAbility;
        private Character sourceCharacter;

        private struct CompressionTarget
        {
            public IDamageable damageable;
            public Component component;
            public int id;
        }
        public void Init(
            Vector2 center,
            float radius,
            float duration,
            float tickInterval,
            float damagePerTick,
            float pullSpeed,
            LayerMask monsterLayer,
SyringeDartAbility sourceNeedleAbility)
        {
            this.center = center;
            this.radius = Mathf.Max(0.1f, radius);
            this.duration = Mathf.Max(0.1f, duration);
            this.tickInterval = Mathf.Max(0.05f, tickInterval);
            this.damagePerTick = Mathf.Max(0f, damagePerTick);
            this.pullSpeed = Mathf.Max(0f, pullSpeed);
            this.monsterLayer = monsterLayer;
            this.sourceNeedleAbility = sourceNeedleAbility;

            // 장기압착 피해의 원본 플레이어를 캐시한다.
            // SyringeDartAbility는 플레이어 쪽 Ability이므로 같은 오브젝트/부모에서 Character를 찾는다.
            if (sourceNeedleAbility != null)
            {
                sourceCharacter =
                    sourceNeedleAbility.GetComponent<Character>() ??
                    sourceNeedleAbility.GetComponentInParent<Character>();
            }

            transform.position = center;
            endTime = Time.time + this.duration;

            CreateVisual();
        }

        private void Update()
        {
            if (Time.time >= endTime)
            {
                Destroy(gameObject);
                return;
            }

            ApplyCompression();
            UpdateVisual();
        }

        private void ApplyCompression()
        {
            if (sourceCharacter != null && sourceCharacter.IsTrapBound) return;
            List<CompressionTarget> targets = GetDamageableTargetsInRadius();

            for (int i = 0; i < targets.Count; i++)
            {
                CompressionTarget target = targets[i];

                if (target.damageable == null || target.component == null)
                {
                    continue;
                }

                if (!target.component.gameObject.activeInHierarchy)
                {
                    continue;
                }

                PullTarget(target);
                TryDamageTarget(target);
            }
        }

        private List<CompressionTarget> GetDamageableTargetsInRadius()
        {
            List<CompressionTarget> result = new List<CompressionTarget>();
            HashSet<int> addedIds = new HashSet<int>();

            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, monsterLayer);

            for (int i = 0; i < hits.Length; i++)
            {
                CompressionTarget target;

                if (!TryBuildCompressionTargetFromCollider(hits[i], out target))
                {
                    continue;
                }

                if (addedIds.Add(target.id))
                {
                    result.Add(target);
                }
            }

            // fallback 1: 기존 일반 Monster 직접 검색
            Monster[] monsters = FindObjectsOfType<Monster>();

            for (int i = 0; i < monsters.Length; i++)
            {
                Monster monster = monsters[i];

                if (!IsValidMonsterForCompression(monster))
                {
                    continue;
                }

                CompressionTarget target;
                if (!TryBuildCompressionTargetFromComponent(monster, out target))
                {
                    continue;
                }

                if (Vector2.Distance(center, GetTargetWorldPosition(target)) > radius)
                {
                    continue;
                }

                if (addedIds.Add(target.id))
                {
                    result.Add(target);
                }
            }

            // fallback 2: 중립 소화효소 몬스터 직접 검색
            DigestiveEnzymeMonster[] enzymes = FindObjectsOfType<DigestiveEnzymeMonster>();

            for (int i = 0; i < enzymes.Length; i++)
            {
                DigestiveEnzymeMonster enzyme = enzymes[i];

                if (enzyme == null || !enzyme.gameObject.activeInHierarchy)
                {
                    continue;
                }

                CompressionTarget target;
                if (!TryBuildCompressionTargetFromComponent(enzyme, out target))
                {
                    continue;
                }

                if (Vector2.Distance(center, GetTargetWorldPosition(target)) > radius)
                {
                    continue;
                }

                if (addedIds.Add(target.id))
                {
                    result.Add(target);
                }
            }

            return result;
        }

        private bool TryBuildCompressionTargetFromCollider(Collider2D collider, out CompressionTarget target)
        {
            target = default;

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

                IDamageable damageable = component as IDamageable;

                if (damageable == null)
                {
                    continue;
                }

                Monster monster = component as Monster;

                if (monster != null && !IsValidMonsterForCompression(monster))
                {
                    continue;
                }

                target = new CompressionTarget
                {
                    damageable = damageable,
                    component = component,
                    id = component.gameObject.GetInstanceID()
                };

                return true;
            }

            return false;
        }

        private bool TryBuildCompressionTargetFromComponent(Component component, out CompressionTarget target)
        {
            target = default;

            if (component == null)
            {
                return false;
            }

            if (!component.gameObject.activeInHierarchy)
            {
                return false;
            }

            IDamageable damageable = component as IDamageable;

            if (damageable == null)
            {
                return false;
            }

            target = new CompressionTarget
            {
                damageable = damageable,
                component = component,
                id = component.gameObject.GetInstanceID()
            };

            return true;
        }

        private bool IsValidMonsterForCompression(Monster monster)
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

            return true;
        }

        private void PullTarget(CompressionTarget target)
        {
            if (target.component.GetComponentInParent<BloodClotObstacle>() != null) return;
            Vector2 targetPosition = GetTargetWorldPosition(target);
            Vector2 toCenter = center - targetPosition;
            float distance = toCenter.magnitude;

            if (distance <= 0.03f)
            {
                return;
            }

            float distanceRatio = Mathf.Clamp01(distance / radius);
            float effectivePullSpeed = pullSpeed * Mathf.Lerp(0.75f, 1.45f, distanceRatio);

            Vector2 nextPosition = Vector2.MoveTowards(
                targetPosition,
                center,
                effectivePullSpeed * Time.deltaTime
            );

            Vector2 delta = nextPosition - targetPosition;

            Rigidbody2D rb = target.component.GetComponent<Rigidbody2D>() ??
                             target.component.GetComponentInParent<Rigidbody2D>();

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.MovePosition(rb.position + delta);
            }
            else
            {
                target.component.transform.position += (Vector3)delta;
            }
        }

        private Vector2 GetTargetWorldPosition(CompressionTarget target)
        {
            if (target.component == null)
            {
                return Vector2.zero;
            }

            Monster monster = target.component as Monster;

            if (monster != null && monster.CenterTransform != null)
            {
                return monster.CenterTransform.position;
            }

            Collider2D collider = target.component.GetComponentInChildren<Collider2D>();

            if (collider != null)
            {
                return collider.bounds.center;
            }

            return target.component.transform.position;
        }

        private void TryDamageTarget(CompressionTarget target)
        {
            if (sourceCharacter != null && sourceCharacter.IsTrapBound) return;
            if (target.damageable == null || target.component == null)
            {
                return;
            }

            if (nextDamageTimes.TryGetValue(target.id, out float nextTime))
            {
                if (Time.time < nextTime)
                {
                    return;
                }
            }

            float finalDamage = damagePerTick;
            bool consumedNeedleMark = false;
            SyringeSpecialRuntime runtime = default;

            if (sourceNeedleAbility != null)
            {
                runtime = sourceNeedleAbility.GetCurrentSpecialRuntime();

                float statusDamageMultiplier = SyringeSpecialHitEffectUtility.GetPreDamageMultiplier(
                    target.component,
                    runtime,
                    out consumedNeedleMark
                );

                finalDamage *= statusDamageMultiplier;
            }

            target.damageable.TakePeriodicDamage(finalDamage, Vector2.zero, false);
            var reaction = SyringeAugmentVfx.Play("OrganCompressionHit", GetTargetWorldPosition(target), SyringeAugmentVfx.FindTarget(target.component));
            if (reaction != null) reaction.BindTo(target.component.transform);

            // 장기압착은 Projectile.OnHitDamageable을 거치지 않는 독립 피해이므로
            // 기존 전체 피해량 시스템에 여기서 정확히 1회 기록한다.
            if (finalDamage > 0f &&
                sourceCharacter != null &&
                sourceCharacter.OnDealDamage != null)
            {
                sourceCharacter.OnDealDamage.Invoke(finalDamage);
            }

            // 결과 화면의 "가장 피해를 많이 준 증강" 계산에 장기압착 피해를 기록한다.
            if (finalDamage > 0f &&
                AugmentDamageTracker.Instance != null)
            {
                AugmentDamageTracker.Instance.RecordDamage(
                    "장기압착",
                    finalDamage
                );
            }

            if (sourceNeedleAbility != null)
            {
                SyringeSpecialHitEffectUtility.ApplyPostHitEffects(
                    target.component,
                    runtime,
                    sourceCharacter,
                    GetTargetWorldPosition(target),
                    monsterLayer,
                    target.component.gameObject,
                    consumedNeedleMark
                );
            }

            nextDamageTimes[target.id] = Time.time + tickInterval;
        }
        private void CreateVisual()
        {
            SyringeAugmentVfx.ReleaseOwned(ref augmentVisual);
            augmentVisual = SyringeAugmentVfx.Play("OrganCompression", center);
            if (augmentVisual != null) augmentVisual.SetGroundRadius(radius);
        }

        private void UpdateVisual()
        {
            if (augmentVisual == null) return;
            augmentVisual.SetGroundRadius(radius);
            augmentVisual.SetStrength(Mathf.Clamp01((endTime - Time.time) / Mathf.Min(0.3f, duration)));
        }

        private void OnDisable() { SyringeAugmentVfx.ReleaseOwned(ref augmentVisual); }
    }
}
