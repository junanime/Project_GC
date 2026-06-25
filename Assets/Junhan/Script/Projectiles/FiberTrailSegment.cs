using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // 섬유침이 남기는 짧은 선분 피해 오브젝트
    // LineRenderer로 선을 보여주고, BoxCollider2D Trigger로 선 위의 몬스터에게 지속 피해를 준다.
    public class FiberTrailSegment : MonoBehaviour
    {
        private readonly Dictionary<int, float> nextDamageTimesByTarget = new Dictionary<int, float>();

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

            LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, new Vector3(-length * 0.5f, 0f, 0f));
            lineRenderer.SetPosition(1, new Vector3(length * 0.5f, 0f, 0f));
            lineRenderer.widthMultiplier = width;
            lineRenderer.numCapVertices = 2;
            lineRenderer.sortingOrder = 20;

            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                lineRenderer.material = new Material(spriteShader);
            }

            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;

            Destroy(gameObject, Mathf.Max(0.05f, lifetime));
        }

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