using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // 섬유침이 남기는 짧은 선분 피해 오브젝트
    // Pooled pixel-art fibers visualize the unchanged BoxCollider2D damage segment.
    public class FiberTrailSegment : MonoBehaviour
    {
        private readonly Dictionary<int, float> nextDamageTimesByTarget = new Dictionary<int, float>();

        private SyringeAugmentVfx augmentVisual;
        private float createdAt;
        private float endsAt;
        private float visualAlpha = 1f;
        [SerializeField, Tooltip("Seconds used to reveal the fiber residue; capped by its gameplay lifetime.")]
        private float fadeInSeconds = 0.1f;
        [SerializeField, Tooltip("Seconds used to fade the residue before the damage segment expires.")]
        private float fadeOutSeconds = 0.2f;
        [SerializeField, Tooltip("Floor order above room backgrounds (-800) and below characters and needle projectiles (50).") ]
        private int groundSortingOrder = -50;
        private LayerMask targetLayer;
        private float damagePerSecond = 2f;
        private float tickInterval = 0.5f;

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
            this.targetLayer = targetLayer;
            this.damagePerSecond = Mathf.Max(0f, damagePerSecond);
            this.tickInterval = Mathf.Max(0.05f, tickInterval);

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

            Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector2(length, width);

            SyringeAugmentVfx.ReleaseOwned(ref augmentVisual);
            augmentVisual = SyringeAugmentVfx.Play("FiberNeedle", center);
            if (augmentVisual != null)
            {
                augmentVisual.transform.rotation = transform.rotation;
                augmentVisual.SetWorldSize(new Vector2(length, width), new Vector2(0.84f, 0.22f));
                var renderer = augmentVisual.GetComponent<SpriteRenderer>();
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = groundSortingOrder;
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

            Monster monster = other.GetComponentInParent<Monster>();

            if (monster == null)
            {
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

            int targetId = monster.gameObject.GetInstanceID();

            if (nextDamageTimesByTarget.TryGetValue(targetId, out float nextDamageTime))
            {
                if (Time.time < nextDamageTime)
                {
                    return;
                }
            }

            float damage = damagePerSecond * tickInterval;
            monster.TakeDamage(damage);

            nextDamageTimesByTarget[targetId] = Time.time + tickInterval;
        }
    }
}
