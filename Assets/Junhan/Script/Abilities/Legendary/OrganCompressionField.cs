using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 장기압착이 생성하는 원형 압착장.
    /// 범위 안의 적을 중앙으로 끌어당기고 주기적으로 피해를 준다.
    /// </summary>
    public class OrganCompressionField : MonoBehaviour
    {
        private readonly Dictionary<int, float> nextDamageTimes = new Dictionary<int, float>();

        private Vector2 center;
        private float radius;
        private float duration;
        private float tickInterval;
        private float damagePerTick;
        private float pullSpeed;
        private LayerMask monsterLayer;

        private float endTime;
        private LineRenderer lineRenderer;

        public void Init(
            Vector2 center,
            float radius,
            float duration,
            float tickInterval,
            float damagePerTick,
            float pullSpeed,
            LayerMask monsterLayer)
        {
            this.center = center;
            this.radius = Mathf.Max(0.1f, radius);
            this.duration = Mathf.Max(0.1f, duration);
            this.tickInterval = Mathf.Max(0.05f, tickInterval);
            this.damagePerTick = Mathf.Max(0f, damagePerTick);
            this.pullSpeed = Mathf.Max(0f, pullSpeed);
            this.monsterLayer = monsterLayer;

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
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, monsterLayer);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];

                if (hit == null)
                {
                    continue;
                }

                Monster monster = hit.GetComponentInParent<Monster>();

                if (monster == null || !monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                PullMonster(monster);
                TryDamageMonster(monster);
            }
        }

        private void PullMonster(Monster monster)
        {
            Vector2 monsterPosition = monster.CenterTransform != null
                ? (Vector2)monster.CenterTransform.position
                : (Vector2)monster.transform.position;

            Vector2 nextPosition = Vector2.MoveTowards(
                monsterPosition,
                center,
                pullSpeed * Time.deltaTime
            );

            Vector2 delta = nextPosition - monsterPosition;

            Rigidbody2D rb = monster.GetComponent<Rigidbody2D>();

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.MovePosition(rb.position + delta);
            }
            else
            {
                monster.transform.position += (Vector3)delta;
            }
        }

        private void TryDamageMonster(Monster monster)
        {
            int id = monster.gameObject.GetInstanceID();

            if (nextDamageTimes.TryGetValue(id, out float nextTime))
            {
                if (Time.time < nextTime)
                {
                    return;
                }
            }

            monster.TakeDamage(damagePerTick);
            nextDamageTimes[id] = Time.time + tickInterval;
        }

        private void CreateVisual()
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.positionCount = 64;
            lineRenderer.widthMultiplier = 0.07f;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.sortingOrder = 850;

            Shader spriteShader = Shader.Find("Sprites/Default");

            if (spriteShader != null)
            {
                lineRenderer.material = new Material(spriteShader);
            }

            Color color = new Color(0.8f, 0.05f, 0.1f, 0.85f);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            RebuildCircle();
        }

        private void UpdateVisual()
        {
            if (lineRenderer == null)
            {
                return;
            }

            float remainingRatio = Mathf.Clamp01((endTime - Time.time) / duration);
            float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.06f;
            float visualRadius = radius * pulse;

            for (int i = 0; i < lineRenderer.positionCount; i++)
            {
                float angle = i / (float)lineRenderer.positionCount * Mathf.PI * 2f;
                lineRenderer.SetPosition(
                    i,
                    new Vector3(Mathf.Cos(angle) * visualRadius, Mathf.Sin(angle) * visualRadius, 0f)
                );
            }

            Color color = lineRenderer.startColor;
            color.a = Mathf.Lerp(0.15f, 0.85f, remainingRatio);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }

        private void RebuildCircle()
        {
            if (lineRenderer == null)
            {
                return;
            }

            for (int i = 0; i < lineRenderer.positionCount; i++)
            {
                float angle = i / (float)lineRenderer.positionCount * Mathf.PI * 2f;
                lineRenderer.SetPosition(
                    i,
                    new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f)
                );
            }
        }
    }
}