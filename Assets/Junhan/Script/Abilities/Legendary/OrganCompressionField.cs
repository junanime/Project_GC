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
        private readonly List<LineRenderer> spiralLines = new List<LineRenderer>();

        private Vector2 center;
        private float radius;
        private float duration;
        private float tickInterval;
        private float damagePerTick;
        private float pullSpeed;
        private LayerMask monsterLayer;

        private float endTime;
        private float visualSpinAngle;

        private LineRenderer outerCircleLine;
        private LineRenderer innerPulseLine;

        private const int CirclePointCount = 96;
        private const int SpiralArmCount = 4;
        private const int SpiralPointCount = 42;

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
            List<Monster> targets = GetMonstersInRadius();

            for (int i = 0; i < targets.Count; i++)
            {
                Monster monster = targets[i];

                if (monster == null || !monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                PullMonster(monster);
                TryDamageMonster(monster);
            }
        }

        private List<Monster> GetMonstersInRadius()
        {
            List<Monster> result = new List<Monster>();
            HashSet<int> addedIds = new HashSet<int>();

            // 1차: 레이어 기반 OverlapCircleAll
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

                int id = monster.gameObject.GetInstanceID();

                if (addedIds.Add(id))
                {
                    result.Add(monster);
                }
            }

            // 2차 fallback:
            // monsterLayer가 root/child 구조와 안 맞거나, 콜라이더 레이어가 다를 때도 끌어당기기 위해
            // Monster 컴포넌트 기준으로 직접 거리 검사한다.
            if (result.Count <= 0)
            {
                Monster[] monsters = FindObjectsOfType<Monster>();

                for (int i = 0; i < monsters.Length; i++)
                {
                    Monster monster = monsters[i];

                    if (monster == null || !monster.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Vector2 monsterPosition = GetMonsterWorldPosition(monster);

                    if (Vector2.Distance(center, monsterPosition) > radius)
                    {
                        continue;
                    }

                    int id = monster.gameObject.GetInstanceID();

                    if (addedIds.Add(id))
                    {
                        result.Add(monster);
                    }
                }
            }

            return result;
        }

        private void PullMonster(Monster monster)
        {
            Vector2 monsterPosition = GetMonsterWorldPosition(monster);

            Vector2 toCenter = center - monsterPosition;
            float distance = toCenter.magnitude;

            if (distance <= 0.03f)
            {
                return;
            }

            float distanceRatio = Mathf.Clamp01(distance / radius);
            float effectivePullSpeed = pullSpeed * Mathf.Lerp(0.75f, 1.45f, distanceRatio);

            Vector2 nextPosition = Vector2.MoveTowards(
                monsterPosition,
                center,
                effectivePullSpeed * Time.deltaTime
            );

            Vector2 delta = nextPosition - monsterPosition;

            Rigidbody2D rb = monster.GetComponent<Rigidbody2D>() ?? monster.GetComponentInParent<Rigidbody2D>();

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.MovePosition(rb.position + delta);
            }
            else
            {
                monster.transform.position += (Vector3)delta;
            }
        }

        private Vector2 GetMonsterWorldPosition(Monster monster)
        {
            if (monster != null && monster.CenterTransform != null)
            {
                return monster.CenterTransform.position;
            }

            return monster != null ? monster.transform.position : Vector2.zero;
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
            Color outerColor = new Color(0.78f, 0.05f, 1f, 0.9f);
            Color innerColor = new Color(1f, 0.35f, 1f, 0.75f);
            Color spiralColor = new Color(0.62f, 0.02f, 1f, 0.92f);

            outerCircleLine = CreateLineRenderer(
                "Outer Purple Compression Circle",
                transform,
                true,
                0.075f,
                outerColor,
                870
            );

            innerPulseLine = CreateLineRenderer(
                "Inner Purple Pulse Circle",
                transform,
                true,
                0.045f,
                innerColor,
                872
            );

            for (int i = 0; i < SpiralArmCount; i++)
            {
                LineRenderer spiral = CreateLineRenderer(
                    $"Purple Vortex Arm {i + 1}",
                    transform,
                    false,
                    0.055f,
                    spiralColor,
                    875 + i
                );

                spiralLines.Add(spiral);
            }

            RebuildCircle(outerCircleLine, radius);
            RebuildCircle(innerPulseLine, radius * 0.45f);
            RebuildSpirals(0f, 1f);
        }

        private LineRenderer CreateLineRenderer(
            string objectName,
            Transform parent,
            bool loop,
            float width,
            Color color,
            int sortingOrder)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(parent, false);
            lineObject.transform.localPosition = Vector3.zero;
            lineObject.transform.localRotation = Quaternion.identity;
            lineObject.transform.localScale = Vector3.one;

            LineRenderer lr = lineObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = loop;
            lr.positionCount = loop ? CirclePointCount : SpiralPointCount;
            lr.widthMultiplier = Mathf.Max(0.01f, width);
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = sortingOrder;

            Shader spriteShader = Shader.Find("Sprites/Default");

            if (spriteShader != null)
            {
                lr.material = new Material(spriteShader);
            }

            lr.startColor = color;
            lr.endColor = color;

            return lr;
        }

        private void UpdateVisual()
        {
            float remainingRatio = Mathf.Clamp01((endTime - Time.time) / duration);
            float aliveRatio = 1f - remainingRatio;

            visualSpinAngle += 360f * Time.deltaTime;

            float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.055f;
            float outerRadius = radius * pulse;

            float innerPulse =
                radius *
                Mathf.Lerp(0.18f, 0.62f, Mathf.PingPong(Time.time * 1.8f, 1f));

            RebuildCircle(outerCircleLine, outerRadius);
            RebuildCircle(innerPulseLine, innerPulse);

            float squeezeRatio = Mathf.Lerp(1f, 0.82f, aliveRatio);
            RebuildSpirals(visualSpinAngle, squeezeRatio);

            SetAlpha(outerCircleLine, Mathf.Lerp(0.15f, 0.9f, remainingRatio));
            SetAlpha(innerPulseLine, Mathf.Lerp(0.08f, 0.65f, remainingRatio));

            for (int i = 0; i < spiralLines.Count; i++)
            {
                SetAlpha(spiralLines[i], Mathf.Lerp(0.1f, 0.92f, remainingRatio));
            }
        }

        private void RebuildCircle(LineRenderer lr, float circleRadius)
        {
            if (lr == null)
            {
                return;
            }

            lr.positionCount = CirclePointCount;

            for (int i = 0; i < CirclePointCount; i++)
            {
                float angle = i / (float)CirclePointCount * Mathf.PI * 2f;

                lr.SetPosition(
                    i,
                    new Vector3(
                        Mathf.Cos(angle) * circleRadius,
                        Mathf.Sin(angle) * circleRadius,
                        0f
                    )
                );
            }
        }

        private void RebuildSpirals(float spinAngleDegrees, float squeezeRatio)
        {
            float spinRadians = spinAngleDegrees * Mathf.Deg2Rad;

            for (int arm = 0; arm < spiralLines.Count; arm++)
            {
                LineRenderer lr = spiralLines[arm];

                if (lr == null)
                {
                    continue;
                }

                lr.positionCount = SpiralPointCount;

                float armOffset = arm / (float)SpiralArmCount * Mathf.PI * 2f;

                for (int i = 0; i < SpiralPointCount; i++)
                {
                    float t = i / (float)(SpiralPointCount - 1);

                    float spiralRadius = Mathf.Lerp(radius * squeezeRatio, 0.08f, t);
                    float angle = armOffset + spinRadians + t * Mathf.PI * 2.8f;

                    Vector3 point = new Vector3(
                        Mathf.Cos(angle) * spiralRadius,
                        Mathf.Sin(angle) * spiralRadius,
                        0f
                    );

                    lr.SetPosition(i, point);
                }
            }
        }

        private void SetAlpha(LineRenderer lr, float alpha)
        {
            if (lr == null)
            {
                return;
            }

            Color start = lr.startColor;
            Color end = lr.endColor;

            start.a = alpha;
            end.a = alpha;

            lr.startColor = start;
            lr.endColor = end;
        }
    }
}