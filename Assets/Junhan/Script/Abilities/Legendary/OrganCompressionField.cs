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
        private SyringeDartAbility sourceNeedleAbility;
        private LineRenderer outerCircleLine;
        private LineRenderer innerPulseLine;

        private const int CirclePointCount = 96;
        private const int SpiralArmCount = 4;
        private const int SpiralPointCount = 42;
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

            target.damageable.TakeDamage(finalDamage, Vector2.zero, false);

            if (sourceNeedleAbility != null)
            {
                SyringeSpecialHitEffectUtility.ApplyPostHitEffects(
                    target.component,
                    runtime,
                    null,
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