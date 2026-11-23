using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Parent = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;

namespace Vampire
{
    // 섬유침이 남기는 짧은 선분 피해 오브젝트
    // Pooled pixel-art fibers visualize the unchanged BoxCollider2D damage segment.
    public class FiberTrailSegment : MonoBehaviour
    {
        static readonly Dictionary<Scene, Transform> sceneBodies = new Dictionary<Scene, Transform>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSceneBodies()
        {
            sceneBodies.Clear();
            SceneManager.sceneUnloaded -= RemoveSceneBody;
            SceneManager.sceneUnloaded += RemoveSceneBody;
        }

        static void RemoveSceneBody(Scene scene) => sceneBodies.Remove(scene);

        void AttachBody()
        {
            // A parented residue may belong to a moving object. Preserve its independent
            // body. Normal projectile trails are stationary and created at scene root.
            if (transform.parent != null)
            {
                CreateBody(gameObject);
                return;
            }
            Scene scene = gameObject.scene;
            if (!sceneBodies.TryGetValue(scene, out var body) || body == null)
            {
                var root = new GameObject("Stationary fiber trails");
                if (root.scene != scene) SceneManager.MoveGameObjectToScene(root, scene);
                body = CreateBody(root).transform;
                sceneBodies[scene] = body;
            }
            transform.SetParent(body, true);
        }

        static Rigidbody2D CreateBody(GameObject owner)
        {
            var body = owner.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            return body;
        }

        private readonly Dictionary<int, float> nextDamageTimesByTarget =
            new Dictionary<int, float>();

        private SyringeAugmentVfx augmentVisual;
        Ver4CombatSnapshot feedbackSnapshot;
        private float createdAt;
        private float endsAt;
        private float visualAlpha = 1f;
        [SerializeField, Tooltip("Seconds used to reveal the fiber residue; capped by its gameplay lifetime.")]
        private float fadeInSeconds = 0.1f;
        [SerializeField, Tooltip("Seconds used to fade the residue before the damage segment expires.")]
        private float fadeOutSeconds = 0.2f;
        [SerializeField, Tooltip("Order within GroundEffects; always above backgrounds and below actors and projectiles.") ]
        private int groundSortingOrder = -50;
        private LayerMask targetLayer;
        private float damagePerSecond = 2f;
        private float tickInterval = 0.5f;

        // 피해 출처
        private Character sourceCharacter;
        private string damageSourceName = "섬유침";

        /// <summary>
        /// 기존 호출부 호환용 Init.
        /// 아직 sourceCharacter를 넘기지 않는 코드도 그대로 컴파일되도록 유지한다.
        /// </summary>
        public void Init(
            Vector2 startPosition,
            Vector2 endPosition,
            LayerMask targetLayer,
            float lifetime,
            float damagePerSecond,
            float tickInterval,
            float width,
            Color lineColor)
        {
            Init(
                startPosition,
                endPosition,
                targetLayer,
                lifetime,
                damagePerSecond,
                tickInterval,
                width,
                lineColor,
                null,
                "섬유침"
            );
        }

        /// <summary>
        /// 섬유침 선분 초기화.
        /// sourceCharacter를 전달하면 기존 전체 피해량(OnDealDamage)에도 포함된다.
        /// </summary>
        public void Init(
            Vector2 startPosition,
            Vector2 endPosition,
            LayerMask targetLayer,
            float lifetime,
            float damagePerSecond,
            float tickInterval,
            float width,
            Color lineColor,
            Character sourceCharacter,
            string damageSourceName, Ver4CombatSnapshot snapshot = null)
        {
            this.targetLayer = targetLayer;
            this.damagePerSecond = Mathf.Max(0f, damagePerSecond);
            this.tickInterval = Mathf.Max(0.05f, tickInterval);

            this.sourceCharacter = sourceCharacter;
            this.damageSourceName =
                string.IsNullOrWhiteSpace(damageSourceName)
                    ? "섬유침"
                    : damageSourceName;

            Vector2 direction = endPosition - startPosition;
            float length = direction.magnitude;

            if (length <= 0.01f)
            {
                Destroy(gameObject);
                return;
            }

            width = Mathf.Max(0.02f, width);

            Vector2 center = (startPosition + endPosition) * 0.5f;
            transform.position = center;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            AttachBody();

            BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector2(length, width);
            feedbackSnapshot = snapshot;
            if (snapshot != null)
            {
                snapshot.Impact(center,this,Parent.FiberNeedle,0);
                snapshot.Impact(center,this,Parent.FiberNeedle,2);
            }

            SyringeAugmentVfx.ReleaseOwned(ref augmentVisual);
            augmentVisual = SyringeAugmentVfx.Play("FiberNeedle", center);
            if (augmentVisual != null)
            {
                augmentVisual.transform.rotation = transform.rotation;
                augmentVisual.SetWorldSize(new Vector2(length, width), new Vector2(0.84f, 0.22f));
                augmentVisual.SetGroundSorting(groundSortingOrder);
            }
            createdAt = Time.time;
            endsAt = Time.time + Mathf.Max(0.05f, lifetime);
            visualAlpha = Mathf.Clamp01(lineColor.a);
            UpdateVisual();

            Destroy(gameObject, Mathf.Max(0.05f, lifetime));
        }

        private void Update() { UpdateVisual(); }

        private void UpdateVisual()
        {
            if (augmentVisual == null) return;
            float duration = Mathf.Max(0.05f, endsAt - createdAt);
            float reveal = Mathf.Clamp01((Time.time - createdAt) / Mathf.Max(0.001f, Mathf.Min(fadeInSeconds, duration * 0.25f)));
            float fade = Mathf.Clamp01((endsAt - Time.time) / Mathf.Max(0.001f, Mathf.Min(fadeOutSeconds, duration * 0.4f)));
            augmentVisual.SetStrength(visualAlpha * reveal * fade);
        }

        private void OnDisable() { SyringeAugmentVfx.ReleaseOwned(ref augmentVisual); }
        private void OnDestroy() { SyringeAugmentVfx.ReleaseOwned(ref augmentVisual); }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void TryDamage(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            using var scratch = UnityEngine.Pool.ListPool<IDamageable>.Get(out var parents);
            var target = NeedleTargeting.FindResidueTarget(other, parents);
            if (target == null) return;
            int targetId = target.gameObject.GetInstanceID();
            if (nextDamageTimesByTarget.TryGetValue(targetId, out float nextTick) && Time.time < nextTick) return;

            if (!(target is Monster monster))
            {
                if (!NeedleTargeting.IsValidBossTarget(target, targetLayer)) return;
                if (Ver4HitEffects.Damage(target, damagePerSecond * tickInterval, Vector2.zero, sourceCharacter, damageSourceName, true))
                    feedbackSnapshot?.Impact(target.transform.position, target, Parent.FiberNeedle, 1);
                nextDamageTimesByTarget[targetId] = Time.time + tickInterval;
                return;
            }

            bool layerMatched =
                ((targetLayer.value & (1 << other.gameObject.layer)) != 0) ||
                ((targetLayer.value & (1 << monster.gameObject.layer)) != 0);

            if (!layerMatched)
            {
                return;
            }

            TrapMonster trapMonster = monster as TrapMonster;
            if (trapMonster != null && !trapMonster.IsActive)
            {
                return;
            }

            // 현재 섬유침의 실제 TakeDamage 값.
            // 이 값이 몬스터에게 전달되는 최종 계산 피해량이다.
            float finalDamage =
                damagePerSecond * tickInterval;

            if (finalDamage <= 0f)
            {
                nextDamageTimesByTarget[targetId] =
                    Time.time + tickInterval;

                return;
            }

            finalDamage *= CombatStatusRules.DamageMultiplier(sourceCharacter,monster);
            float previousHealth = monster.HP;
            monster.TakePeriodicDamage(finalDamage);
            if (monster.HP < previousHealth) feedbackSnapshot?.Impact(monster.transform.position,monster,Parent.FiberNeedle,1);

            // 기존 전체 피해량 시스템에도 포함
            if (sourceCharacter != null &&
                sourceCharacter.OnDealDamage != null)
            {
                sourceCharacter.OnDealDamage.Invoke(
                    finalDamage
                );
            }

            // 가장 피해를 많이 준 증강 계산에 포함
            if (AugmentDamageTracker.Instance != null)
            {
                AugmentDamageTracker.Instance.RecordDamage(
                    damageSourceName,
                    finalDamage
                );
            }

            nextDamageTimesByTarget[targetId] =
                Time.time + tickInterval;
        }
    }
}
