using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 몬스터 버퍼 특수 몬스터입니다.
    ///
    /// 플레이어를 직접 공격하지 않고,
    /// 현재 필드에서 몬스터가 가장 많이 밀집된 구역으로 이동한 뒤
    /// 주변 몬스터에게 이동속도 증가 또는 받는 피해 감소 버프를 부여합니다.
    ///
    /// 이미지가 아직 없으므로 버프 모션은 원형 LineRenderer 펄스로 표시합니다.
    /// 나중에 별도 버프 모션 이미지가 들어오면 이 펄스 연출은 꺼도 됩니다.
    /// </summary>
    public class MonsterSupportCaster : Monster
    {
        [Header("Support Buff Type")]
        [Tooltip("이 버퍼 몬스터가 사용할 버프 종류입니다.")]
        [SerializeField] private MonsterSupportBuffType buffType = MonsterSupportBuffType.MoveSpeed;

        [Header("Cluster Movement")]
        [Tooltip("가장 몬스터가 밀집된 구역을 다시 계산하는 간격입니다.")]
        [SerializeField] private float clusterRefreshInterval = 0.5f;

        [Tooltip("밀집도를 계산할 때 후보 몬스터 주변 몇 반경 안의 몬스터를 세는지 결정합니다.")]
        [SerializeField] private float clusterScanRadius = 3f;

        [Tooltip("목표 밀집 구역에 이 거리 이하로 가까워지면 멈춥니다.")]
        [SerializeField] private float stopDistanceToCluster = 0.6f;

        [Tooltip("밀집 구역을 찾지 못했을 때 플레이어 기준으로 배회할 거리입니다.")]
        [SerializeField] private float fallbackWanderDistanceFromPlayer = 4f;

        [Header("Buff Cast")]
        [Tooltip("버프를 사용하기 시작하는 거리입니다.")]
        [SerializeField] private float castStartDistance = 1f;

        [Tooltip("버프 사용 후 다시 사용할 수 있을 때까지의 쿨타임입니다.")]
        [SerializeField] private float castCooldown = 4f;

        [Tooltip("버프 모션이 재생되는 시간입니다. 이 시간 동안 버퍼는 잠시 멈춥니다.")]
        [SerializeField] private float castMotionDuration = 0.45f;

        [Tooltip("버프 적용 반경입니다. 사용자가 말한 원지름 2 기준이면 반경 1로 설정하세요.")]
        [SerializeField] private float buffRadius = 1f;

        [Tooltip("버프 지속 시간입니다.")]
        [SerializeField] private float buffDuration = 5f;

        [Tooltip("이동속도 버프 증가량입니다. 0.2 = 20% 증가입니다.")]
        [SerializeField] private float moveSpeedBonusRatio = 0.2f;

        [Tooltip("피해 감소 버프량입니다. 0.2 = 받는 피해 20% 감소입니다.")]
        [SerializeField] private float damageReductionRatio = 0.2f;

        [Header("Buff Icon Visual")]
        [Tooltip("이동속도 버프를 받은 몬스터 주변에 표시할 임시 문자입니다. 나중에 이미지로 교체 가능합니다.")]
        [SerializeField] private string moveSpeedBuffSymbol = "✚";

        [Tooltip("피해 감소 버프를 받은 몬스터 주변에 표시할 임시 문자입니다. 나중에 방패 이미지로 교체 가능합니다.")]
        [SerializeField] private string damageReductionBuffSymbol = "▣";

        [Tooltip("이동속도 버프 문양 색상입니다.")]
        [SerializeField] private Color moveSpeedBuffColor = new Color(0.2f, 0.6f, 1f, 1f);

        [Tooltip("피해 감소 버프 문양 색상입니다.")]
        [SerializeField] private Color damageReductionBuffColor = new Color(0.8f, 0.9f, 1f, 1f);

        [Tooltip("버프 문양이 몬스터 중심에서 얼마나 떨어져 회전할지 설정합니다.")]
        [SerializeField] private float iconOrbitRadius = 0.35f;

        [Tooltip("버프 문양이 몬스터 머리 위로 얼마나 올라갈지 설정합니다.")]
        [SerializeField] private float iconYOffset = 0.85f;

        [Tooltip("버프 문양 회전 속도입니다.")]
        [SerializeField] private float iconOrbitSpeed = 4f;

        [Tooltip("버프 문양 TextMeshPro 글자 크기입니다.")]
        [SerializeField] private float iconFontSize = 2.5f;

        [Tooltip("버프 문양 Sorting Order입니다.")]
        [SerializeField] private int iconSortingOrder = 120;

        [Header("Cast Range Visual")]
        [Tooltip("체크하면 버프 시전 순간 원형 범위 연출을 표시합니다.")]
        [SerializeField] private bool showCastRangePulse = true;

        [Tooltip("버프 범위 원형 연출의 Sorting Order입니다.")]
        [SerializeField] private int castPulseSortingOrder = 110;

        [Header("Debug")]
        [Tooltip("체크하면 버퍼 몬스터 이동/버프 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        private Vector2 currentClusterTarget;
        private float clusterRefreshTimer;
        private float castCooldownTimer;
        private bool isCasting;

        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint monsterBlueprint,
            float hpBuff = 0)
        {
            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);

            clusterRefreshTimer = 0f;
            castCooldownTimer = castCooldown;
            isCasting = false;
            currentClusterTarget = position;
        }

        protected override void FixedUpdate()
        {
            if (!alive || isCasting || rb == null)
            {
                return;
            }

            clusterRefreshTimer -= Time.fixedDeltaTime;
            castCooldownTimer += Time.fixedDeltaTime;

            if (clusterRefreshTimer <= 0f)
            {
                clusterRefreshTimer = Mathf.Max(0.05f, clusterRefreshInterval);
                currentClusterTarget = FindDensestMonsterClusterPosition();
            }

            Vector2 currentPosition = rb.position;
            Vector2 toTarget = currentClusterTarget - currentPosition;
            float distanceToTarget = toTarget.magnitude;

            if (distanceToTarget > stopDistanceToCluster)
            {
                Vector2 direction = toTarget.normalized;
                float acceleration = monsterBlueprint != null ? monsterBlueprint.acceleration : 5f;
                rb.velocity += direction * acceleration * Time.fixedDeltaTime;
            }

            if (distanceToTarget <= castStartDistance &&
                castCooldownTimer >= Mathf.Max(0.1f, castCooldown))
            {
                StartCoroutine(CastBuffCoroutine());
            }
        }

        private Vector2 FindDensestMonsterClusterPosition()
        {
            if (entityManager == null ||
                entityManager.LivingMonsters == null ||
                entityManager.LivingMonsters.Count <= 0)
            {
                return GetFallbackPosition();
            }

            int bestCount = 0;
            Vector2 bestCenter = GetFallbackPosition();

            foreach (Monster candidate in entityManager.LivingMonsters)
            {
                if (!IsValidClusterMonster(candidate))
                {
                    continue;
                }

                int count = 0;
                Vector2 sum = Vector2.zero;

                foreach (Monster other in entityManager.LivingMonsters)
                {
                    if (!IsValidClusterMonster(other))
                    {
                        continue;
                    }

                    float distance = Vector2.Distance(candidate.transform.position, other.transform.position);

                    if (distance <= clusterScanRadius)
                    {
                        count++;
                        sum += (Vector2)other.transform.position;
                    }
                }

                if (count > bestCount)
                {
                    bestCount = count;
                    bestCenter = sum / Mathf.Max(1, count);
                }
            }

            if (debugLog)
            {
                Debug.Log($"[몬스터 버퍼] 밀집 구역 갱신 | Count={bestCount} | Target={bestCenter}", this);
            }

            return bestCenter;
        }

        private bool IsValidClusterMonster(Monster targetMonster)
        {
            if (targetMonster == null)
            {
                return false;
            }

            if (targetMonster == this)
            {
                return false;
            }

            if (!targetMonster.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (targetMonster.HP <= 0f)
            {
                return false;
            }

            return true;
        }

        private Vector2 GetFallbackPosition()
        {
            if (playerCharacter == null)
            {
                return transform.position;
            }

            Vector2 randomDirection = Random.insideUnitCircle.normalized;

            if (randomDirection == Vector2.zero)
            {
                randomDirection = Vector2.right;
            }

            return (Vector2)playerCharacter.transform.position +
                   randomDirection * Mathf.Max(0.1f, fallbackWanderDistanceFromPlayer);
        }

        private IEnumerator CastBuffCoroutine()
        {
            isCasting = true;
            castCooldownTimer = 0f;

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }

            if (monsterSpriteAnimator != null)
            {
                monsterSpriteAnimator.StopAnimating(true);
            }

            if (showCastRangePulse)
            {
                CreateCastPulseVisual();
            }

            if (debugLog)
            {
                Debug.Log($"[몬스터 버퍼] 버프 시전 시작 | Type={buffType}", this);
            }

            yield return new WaitForSeconds(Mathf.Max(0.05f, castMotionDuration));

            ApplyBuffToNearbyMonsters();

            if (monsterSpriteAnimator != null)
            {
                monsterSpriteAnimator.StartAnimating(true);
            }

            isCasting = false;
        }

        private void ApplyBuffToNearbyMonsters()
        {
            if (entityManager == null || entityManager.LivingMonsters == null)
            {
                return;
            }

            int buffedCount = 0;
            float safeBuffRadius = Mathf.Max(0.01f, buffRadius);
            float sqrRadius = safeBuffRadius * safeBuffRadius;

            foreach (Monster targetMonster in entityManager.LivingMonsters)
            {
                if (!IsValidClusterMonster(targetMonster))
                {
                    continue;
                }

                Vector2 toTarget = targetMonster.transform.position - transform.position;

                if (toTarget.sqrMagnitude > sqrRadius)
                {
                    continue;
                }

                MonsterCombatBuffRuntime runtime =
                    targetMonster.GetComponent<MonsterCombatBuffRuntime>();

                if (runtime == null)
                {
                    runtime = targetMonster.gameObject.AddComponent<MonsterCombatBuffRuntime>();
                }

                if (buffType == MonsterSupportBuffType.MoveSpeed)
                {
                    runtime.ApplyMoveSpeedBuff(
                        moveSpeedBonusRatio,
                        buffDuration,
                        moveSpeedBuffSymbol,
                        moveSpeedBuffColor,
                        iconOrbitRadius,
                        iconYOffset,
                        iconOrbitSpeed,
                        iconSortingOrder,
                        iconFontSize);
                }
                else
                {
                    runtime.ApplyDamageReductionBuff(
                        damageReductionRatio,
                        buffDuration,
                        damageReductionBuffSymbol,
                        damageReductionBuffColor,
                        iconOrbitRadius,
                        iconYOffset,
                        iconOrbitSpeed,
                        iconSortingOrder,
                        iconFontSize);
                }

                buffedCount++;
            }

            if (debugLog)
            {
                Debug.Log($"[몬스터 버퍼] 버프 적용 완료 | Type={buffType} | Count={buffedCount}", this);
            }
        }

        private void CreateCastPulseVisual()
        {
            GameObject pulseObject = new GameObject("Support Buff Cast Pulse");
            pulseObject.transform.position = transform.position;

            LineRenderer line = pulseObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 64;
            line.startWidth = 0.04f;
            line.endWidth = 0.04f;
            line.sortingOrder = castPulseSortingOrder;

            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                line.material = new Material(shader);
            }

            Color pulseColor =
                buffType == MonsterSupportBuffType.MoveSpeed
                    ? moveSpeedBuffColor
                    : damageReductionBuffColor;

            line.startColor = pulseColor;
            line.endColor = pulseColor;

            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = ((float)i / line.positionCount) * Mathf.PI * 2f;
                Vector3 point = new Vector3(
                    Mathf.Cos(angle) * buffRadius,
                    Mathf.Sin(angle) * buffRadius,
                    0f);

                line.SetPosition(i, point);
            }

            Destroy(pulseObject, Mathf.Max(0.05f, castMotionDuration));
        }
    }
}