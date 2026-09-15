using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 소화액낭침으로 생성되는 소화액 웅덩이.
    /// 일정 시간 유지되며, 범위 안의 몬스터에게 주기적으로 피해를 줍니다.
    /// </summary>
    public class DigestiveAcidPuddle : MonoBehaviour
    {
        private readonly Dictionary<int, float> nextDamageTimes =
            new Dictionary<int, float>();

        private float lifetime;
        private float radius;
        private float damagePerSecond;
        private float tickInterval;
        private Color puddleColor;

        private SyringeAugmentVfx augmentVisual;
        private float endTime;
        private LineRenderer outerLine;
        private LineRenderer innerLine;

        private Character sourceCharacter;
        private string damageSourceName = "소화액낭침";

        private const int CirclePointCount = 72;

        /// <summary>
        /// 기존 호출부 호환용 Init.
        /// </summary>
        public void Init(
            float lifetime,
            float radius,
            float damagePerSecond,
            float tickInterval,
            Color puddleColor)
        {
            Init(
                lifetime,
                radius,
                damagePerSecond,
                tickInterval,
                puddleColor,
                null,
                "소화액낭침"
            );
        }

        /// <summary>
        /// 소화액 웅덩이 초기화.
        /// sourceCharacter와 damageSourceName을 받아
        /// 기존 전체 피해량과 증강별 피해량에 기록할 수 있게 한다.
        /// </summary>
        public void Init(
            float lifetime,
            float radius,
            float damagePerSecond,
            float tickInterval,
            Color puddleColor,
            Character sourceCharacter,
            string damageSourceName)
        {
            this.lifetime = Mathf.Max(0.1f, lifetime);
            this.radius = Mathf.Max(0.1f, radius);
            this.damagePerSecond = Mathf.Max(0f, damagePerSecond);
            this.tickInterval = Mathf.Max(0.05f, tickInterval);
            this.puddleColor = puddleColor;

            this.sourceCharacter = sourceCharacter;
            this.damageSourceName =
                string.IsNullOrWhiteSpace(damageSourceName)
                    ? "소화액낭침"
                    : damageSourceName;

            endTime = Time.time + this.lifetime;

            CreateVisual();
        }

        private void Update()
        {
            if (Time.time >= endTime)
            {
                Destroy(gameObject);
                return;
            }

            if (!MiniStageRuntimeState.IsInsideMiniStage)
            {
                ApplyDamage();
            }

            UpdateVisual();
        }

        private void ApplyDamage()
        {
            Collider2D[] hits =
                Physics2D.OverlapCircleAll(
                    transform.position,
                    radius
                );

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];

                if (hit == null)
                {
                    continue;
                }

                Monster monster =
                    hit.GetComponentInParent<Monster>();

                if (monster == null ||
                    !monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (monster.HP <= 0f)
                {
                    continue;
                }

                int id =
                    monster.gameObject.GetInstanceID();

                if (nextDamageTimes.TryGetValue(
                        id,
                        out float nextTime))
                {
                    if (Time.time < nextTime)
                    {
                        continue;
                    }
                }

                // 현재 실제로 몬스터 TakeDamage에 전달되는 피해값.
                // 게임의 기존 피해 계산은 바꾸지 않고,
                // 실제 적용값 그대로 최종 계산 피해량으로 기록한다.
                float finalDamage =
                    damagePerSecond * tickInterval;

                if (finalDamage > 0f)
                {
                    monster.TakePeriodicDamage(
                        finalDamage
                    );

                    // 기존 전체 피해량 시스템에 포함
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
                }

                nextDamageTimes[id] =
                    Time.time + tickInterval;
            }
        }

        private void CreateVisual()
        {
            ReleaseAugmentVisual();
            augmentVisual = SyringeAugmentVfx.Play("DigestiveAcidSacNeedle", transform.position);
            if (augmentVisual != null)
            {
                augmentVisual.transform.localScale = new Vector3(radius * 2f / 0.84f, radius * 2f / 0.70f, 1f);
                augmentVisual.SetGroundSorting();
            }
        }

        private LineRenderer CreateCircleLine(
            string objectName,
            float circleRadius,
            float width,
            Color color,
            int sortingOrder)
        {
            GameObject lineObject =
                new GameObject(objectName);

            lineObject.transform.SetParent(
                transform,
                false
            );

            lineObject.transform.localPosition =
                Vector3.zero;

            lineObject.transform.localRotation =
                Quaternion.identity;

            lineObject.transform.localScale =
                Vector3.one;

            LineRenderer lr =
                lineObject.AddComponent<LineRenderer>();

            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = CirclePointCount;
            lr.widthMultiplier =
                Mathf.Max(0.01f, width);

            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            GroundVisualSorting.Apply(lr, sortingOrder);

            Shader spriteShader =
                Shader.Find("Sprites/Default");

            if (spriteShader != null)
            {
                lr.material =
                    new Material(spriteShader);
            }

            lr.startColor = color;
            lr.endColor = color;

            RebuildCircle(
                lr,
                circleRadius
            );

            return lr;
        }

        private void ReleaseAugmentVisual()
        {
            if (augmentVisual != null) augmentVisual.Release();
            augmentVisual = null;
        }
        private void OnDisable() { ReleaseAugmentVisual(); }
        private void OnDestroy() { ReleaseAugmentVisual(); }

        private void UpdateVisual()
        {
        }

        private void RebuildCircle(
            LineRenderer lr,
            float circleRadius)
        {
            if (lr == null)
            {
                return;
            }

            lr.positionCount =
                CirclePointCount;

            for (int i = 0;
                 i < CirclePointCount;
                 i++)
            {
                float angle =
                    i /
                    (float)CirclePointCount *
                    Mathf.PI *
                    2f;

                lr.SetPosition(
                    i,
                    new Vector3(
                        Mathf.Cos(angle) *
                        circleRadius,
                        Mathf.Sin(angle) *
                        circleRadius,
                        0f
                    )
                );
            }
        }

        private void SetAlpha(
            LineRenderer lr,
            float alpha)
        {
            if (lr == null)
            {
                return;
            }

            Color start =
                lr.startColor;

            Color end =
                lr.endColor;

            start.a = alpha;
            end.a = alpha;

            lr.startColor = start;
            lr.endColor = end;
        }
    }
}
