using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 장기압착이 생성하는 원형 압착장.
    /// 범위 안의 적을 중앙으로 끌어당기고 주기적으로 피해를 준다.
    /// 
    /// 일반 몬스터와 중립 소화효소는 레이어가 일치하지 않아도 범위 내에서 흡입된다.
    /// </summary>
    public class OrganCompressionField : MonoBehaviour
    {
        private readonly Dictionary<int, float> nextDamageTimes = new Dictionary<int, float>();
        private readonly List<CompressionTarget> targetsInRange = new List<CompressionTarget>();
        private readonly HashSet<int> addedIds = new HashSet<int>();
        private readonly List<Collider2D> hits = new List<Collider2D>();
        private readonly List<IDamageable> parents = new List<IDamageable>();
        private readonly List<Monster> monsters = new List<Monster>();
        private readonly List<DigestiveEnzymeMonster> enzymes = new List<DigestiveEnzymeMonster>();
        private HerbalGroundVisual augmentVisual;

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
        }

        private void ApplyCompression()
        {
            if (sourceCharacter != null && sourceCharacter.IsTrapBound) return;
            List<CompressionTarget> targets = GetDamageableTargetsInRadius();

            for (int i = 0; i < targets.Count; i++)
            {
                CompressionTarget target = targets[i];

                if (target.damageable == null)
                {
                    continue;
                }

                if (!target.damageable.gameObject.activeInHierarchy)
                {
                    continue;
                }

                PullTarget(target);
                TryDamageTarget(target);
            }
        }

        private List<CompressionTarget> GetDamageableTargetsInRadius()
        {
            targetsInRange.Clear();
            addedIds.Clear();
            NeedleTargeting.FillCircle(center, radius, monsterLayer, hits);

            for (int i = 0; i < hits.Count; i++)
            {
                var actor = FirstEligibleParent(hits[i]);
                if (actor == null) continue;
                int id = actor.gameObject.GetInstanceID();
                if (addedIds.Add(id))
                    targetsInRange.Add(new CompressionTarget { damageable = actor, id = id });
            }

            // Preserve the two fallbacks for off-layer targets, including neutral enzymes.
            // The registry supplies active objects in the original InstanceID order.
            StageActorRegistry.CopyActiveMonsters(monsters);
            for (int i = 0; i < monsters.Count; i++)
                if (monsters[i].HP > 0f) AddFallback(monsters[i]);
            StageActorRegistry.CopyActiveEnzymes(enzymes);
            for (int i = 0; i < enzymes.Count; i++) AddFallback(enzymes[i]);
            return targetsInRange;
        }

        void AddFallback(IDamageable actor)
        {
            int id = actor.gameObject.GetInstanceID();
            if (addedIds.Contains(id)) return;
            var target = new CompressionTarget { damageable = actor, id = id };
            if (Vector2.Distance(center, GetTargetWorldPosition(target)) > radius) return;
            addedIds.Add(id);
            targetsInRange.Add(target);
        }

        IDamageable FirstEligibleParent(Collider2D hit)
        {
            if (hit == null) return null;
            var first = hit.GetComponentInParent<IDamageable>(true);
            if (first == null || Eligible(first)) return first;
            // A dead inner actor may cover a live parent, or another component on
            // the same object. Only this unusual hierarchy needs the full ordered list.
            hit.GetComponentsInParent(true, parents);
            for (int i = 0; i < parents.Count; i++)
                if (Eligible(parents[i])) return parents[i];
            return null;
        }

        static bool Eligible(IDamageable actor) =>
            actor != null && actor.gameObject.activeInHierarchy &&
            (!(actor is Monster monster) || monster.HP > 0f);

        private void PullTarget(CompressionTarget target)
        {
            if (target.damageable.GetComponentInParent<BloodClotObstacle>() != null) return;
            // Respect colon encounters' scripted movement and habitat territory. Damage
            // and marks still apply, but a suction field cannot drag them through walls.
            if (target.damageable is ColonBossRuntime || target.damageable is ConstipationKingRuntime) return;
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

            Rigidbody2D rb = target.damageable.GetComponentInParent<Rigidbody2D>();

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.MovePosition(rb.position + delta);
            }
            else
            {
                target.damageable.transform.position += (Vector3)delta;
            }
        }

        private Vector2 GetTargetWorldPosition(CompressionTarget target)
        {
            if (target.damageable == null)
            {
                return Vector2.zero;
            }

            Monster monster = target.damageable as Monster;

            if (monster != null && monster.CenterTransform != null)
            {
                return monster.CenterTransform.position;
            }

            Collider2D collider = target.damageable.GetComponentInChildren<Collider2D>();

            if (collider != null)
            {
                return collider.bounds.center;
            }

            return target.damageable.transform.position;
        }

        private void TryDamageTarget(CompressionTarget target)
        {
            if (sourceCharacter != null && sourceCharacter.IsTrapBound) return;
            if (target.damageable == null)
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

            float debuffMultiplier = CombatStatusRules.DamageMultiplier(sourceCharacter,target.damageable);
            float finalDamage = damagePerTick * debuffMultiplier;
            bool consumedNeedleMark = false;
            SyringeSpecialRuntime runtime = default;

            if (sourceNeedleAbility != null)
            {
                runtime = sourceNeedleAbility.GetCurrentSpecialRuntime();

                float statusDamageMultiplier = SyringeSpecialHitEffectUtility.GetPreDamageMultiplier(
                    target.damageable,
                    runtime,
                    out consumedNeedleMark
                );

                finalDamage *= statusDamageMultiplier;
            }

            float beforeMarkHit = consumedNeedleMark && runtime.ver4?.PendingFeedback == true
                ? Ver4HitEffects.Health(target.damageable) : 0f;
            target.damageable.TakePeriodicDamage(finalDamage, Vector2.zero, false);
            if (beforeMarkHit > 0 && beforeMarkHit > Ver4HitEffects.Health(target.damageable))
                runtime.ver4.Impact(GetTargetWorldPosition(target), target.damageable,
                    SyringeSpecialAugmentAbility.SpecialAugmentType.MarkNeedle, 1);
            HerbalGroundVisual.Create(target.damageable.transform, GetTargetWorldPosition(target), .24f, .24f, 2);

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
                    target.damageable,
                    runtime,
                    sourceCharacter,
                    GetTargetWorldPosition(target),
                    monsterLayer,
                    target.damageable.gameObject,
                    consumedNeedleMark,
                    finalDamage / debuffMultiplier
                );
            }

            nextDamageTimes[target.id] = Time.time + tickInterval;
        }
        private void CreateVisual()
        {
            if (augmentVisual != null) Destroy(augmentVisual.gameObject);
            augmentVisual = HerbalGroundVisual.Create(transform, center, radius, duration, 0);
        }
        private void OnDisable() { if (augmentVisual != null) Destroy(augmentVisual.gameObject); }
    }
}
