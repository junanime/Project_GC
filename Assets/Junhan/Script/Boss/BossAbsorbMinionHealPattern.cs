using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class BossAbsorbMinionHealPattern : BossPatternBase
    {
        [Header("Absorb Minion Pattern")]
        [Tooltip("보스에게 흡수될 몬스터 프리팹입니다. 프리팹에는 BossAbsorbHealMinion, Rigidbody2D, Collider2D가 있어야 합니다.")]
        [SerializeField] private GameObject absorbMinionPrefab;

        [Tooltip("한 번의 패턴에서 스폰할 흡수 몬스터 수입니다.")]
        [SerializeField] private int spawnCount = 12;

        [Tooltip("흡수 몬스터를 한 마리씩 스폰하는 간격입니다. 0이면 한 번에 전부 스폰합니다.")]
        [SerializeField] private float spawnInterval = 0.08f;

        [Tooltip("보스 중심에서 흡수 몬스터가 스폰되는 최소 거리입니다.")]
        [SerializeField] private float spawnDistanceMin = 6f;

        [Tooltip("보스 중심에서 흡수 몬스터가 스폰되는 최대 거리입니다.")]
        [SerializeField] private float spawnDistanceMax = 8f;

        [Tooltip("스폰 각도에 랜덤 흔들림을 주는 정도입니다. 0이면 균등 배치에 가깝게 생성됩니다.")]
        [SerializeField] private float spawnAngleJitter = 20f;

        [Header("Minion Stats")]
        [Tooltip("각 흡수 몬스터의 체력입니다.")]
        [SerializeField] private float minionHp = 15f;

        [Tooltip("각 흡수 몬스터가 보스를 향해 이동하는 속도입니다.")]
        [SerializeField] private float minionMoveSpeed = 2.4f;

        [Tooltip("흡수 몬스터 한 마리가 보스에게 닿았을 때 회복시키는 보스 체력입니다.")]
        [SerializeField] private float healAmountPerMinion = 10f;

        [Tooltip("보스 중심과 이 거리 이하로 가까워지면 흡수된 것으로 처리합니다.")]
        [SerializeField] private float absorbDistance = 1.1f;

        [Header("Pattern Duration")]
        [Tooltip("패턴 최대 지속 시간입니다. 이 시간이 지나면 남아있는 흡수 몬스터를 강제로 제거하고 패턴을 종료합니다.")]
        [SerializeField] private float maxPatternDuration = 18f;

        [Tooltip("체크하면 최대 지속 시간이 끝날 때 남아있는 흡수 몬스터가 사망 이펙트를 생성합니다.")]
        [SerializeField] private bool playDeathVfxOnTimeout = false;

        [Header("Boss State")]
        [Tooltip("체크하면 이 패턴이 진행되는 동안 보스 이동을 멈춥니다.")]
        [SerializeField] private bool lockBossMovementDuringPattern = true;

        [Tooltip("체크하면 이 패턴이 진행되는 동안 보스의 접촉 데미지를 막습니다.")]
        [SerializeField] private bool suppressBossContactDamageDuringPattern = true;

        [Header("Visual")]
        [Tooltip("흡수 몬스터가 생성될 때 생성할 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject minionSpawnVfxPrefab;

        [Tooltip("흡수 몬스터가 플레이어에게 처치될 때 생성할 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject minionDeathVfxPrefab;

        [Tooltip("흡수 몬스터가 보스에게 흡수될 때 몬스터 위치에 생성할 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject minionAbsorbVfxPrefab;

        [Tooltip("보스가 회복될 때 보스 위치에 생성할 회복 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject bossHealVfxPrefab;

        [Tooltip("흡수 몬스터 SpriteRenderer Sorting Order를 강제로 설정할지 여부입니다.")]
        [SerializeField] private bool forceMinionSortingOrder = true;

        [Tooltip("흡수 몬스터 SpriteRenderer Sorting Order입니다. 몬스터가 필드 위에 보이도록 적절히 설정하세요.")]
        [SerializeField] private int minionSortingOrder = 430;

        [Header("Debug")]
        [Tooltip("체크하면 흡수 몬스터 회복 패턴 로그를 Console에 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        private readonly List<BossAbsorbHealMinion> activeMinions = new List<BossAbsorbHealMinion>();

        private BossHealPatternLock healPatternLock;
        private bool isPatternActive = false;
        private int absorbedCount = 0;
        private int killedCount = 0;

        private void Reset()
        {
            patternName = "Absorb Minion Heal";
            cooldown = 45f;
        }

        public override void Init(BossController controller)
        {
            base.Init(controller);

            if (bossController != null)
            {
                healPatternLock = bossController.GetComponent<BossHealPatternLock>();

                if (healPatternLock == null)
                {
                    healPatternLock = bossController.gameObject.AddComponent<BossHealPatternLock>();
                }
            }
        }

        public override bool CanUse()
        {
            if (isPatternActive)
            {
                return false;
            }

            if (healPatternLock != null && healPatternLock.IsLocked)
            {
                return false;
            }

            return base.CanUse();
        }

        protected override IEnumerator ExecutePattern()
        {
            if (bossController == null)
            {
                yield break;
            }

            if (absorbMinionPrefab == null)
            {
                Debug.LogWarning("[BossAbsorbMinionHealPattern] Absorb Minion Prefab이 비어 있습니다.");
                yield break;
            }

            if (healPatternLock == null)
            {
                healPatternLock = bossController.GetComponent<BossHealPatternLock>();

                if (healPatternLock == null)
                {
                    healPatternLock = bossController.gameObject.AddComponent<BossHealPatternLock>();
                }
            }

            if (!healPatternLock.TryBegin(BossHealPatternType.AbsorbMinion))
            {
                yield break;
            }

            isPatternActive = true;
            absorbedCount = 0;
            killedCount = 0;
            activeMinions.Clear();

            ApplyBossPatternState(true);

            if (debugLog)
            {
                Debug.Log($"[BossAbsorbMinionHealPattern] 흡수 몬스터 회복 패턴 시작 / spawnCount={spawnCount}");
            }

            float elapsed = 0f;
            int finalSpawnCount = Mathf.Max(0, spawnCount);

            for (int i = 0; i < finalSpawnCount; i++)
            {
                if (!isPatternActive || bossController == null || bossController.IsDead)
                {
                    break;
                }

                if (maxPatternDuration > 0f && elapsed >= maxPatternDuration)
                {
                    break;
                }

                SpawnOneMinion(i, finalSpawnCount);

                if (spawnInterval > 0f && i < finalSpawnCount - 1)
                {
                    float waitTimer = 0f;

                    while (waitTimer < spawnInterval)
                    {
                        if (!isPatternActive || bossController == null || bossController.IsDead)
                        {
                            break;
                        }

                        waitTimer += Time.deltaTime;
                        elapsed += Time.deltaTime;

                        if (maxPatternDuration > 0f && elapsed >= maxPatternDuration)
                        {
                            break;
                        }

                        yield return null;
                    }
                }
            }

            while (isPatternActive)
            {
                if (bossController == null || bossController.IsDead)
                {
                    break;
                }

                CleanupMinionList();

                if (activeMinions.Count <= 0)
                {
                    break;
                }

                elapsed += Time.deltaTime;

                if (maxPatternDuration > 0f && elapsed >= maxPatternDuration)
                {
                    if (debugLog)
                    {
                        Debug.Log("[BossAbsorbMinionHealPattern] 최대 지속 시간 도달 / 남은 흡수 몬스터 강제 제거");
                    }

                    break;
                }

                yield return null;
            }

            ForceDespawnRemainingMinions();
            FinishPattern();
        }

        public void NotifyMinionAbsorbed(BossAbsorbHealMinion minion)
        {
            if (minion != null)
            {
                activeMinions.Remove(minion);
            }

            absorbedCount++;

            if (debugLog)
            {
                Debug.Log($"[BossAbsorbMinionHealPattern] 몬스터 흡수됨 / absorbed={absorbedCount}, remain={activeMinions.Count}");
            }
        }

        public void NotifyMinionKilled(BossAbsorbHealMinion minion)
        {
            if (minion != null)
            {
                activeMinions.Remove(minion);
            }

            killedCount++;

            if (debugLog)
            {
                Debug.Log($"[BossAbsorbMinionHealPattern] 몬스터 처치됨 / killed={killedCount}, remain={activeMinions.Count}");
            }
        }

        private void SpawnOneMinion(int index, int totalCount)
        {
            if (bossController == null || absorbMinionPrefab == null)
            {
                return;
            }

            float angleStep = totalCount > 0 ? 360f / totalCount : 360f;
            float baseAngle = angleStep * index;
            float randomJitter = Random.Range(-spawnAngleJitter, spawnAngleJitter);
            float finalAngle = baseAngle + randomJitter;
            float rad = finalAngle * Mathf.Deg2Rad;

            float minDistance = Mathf.Min(spawnDistanceMin, spawnDistanceMax);
            float maxDistance = Mathf.Max(spawnDistanceMin, spawnDistanceMax);
            float spawnDistance = Random.Range(minDistance, maxDistance);

            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * spawnDistance;
            Vector3 spawnPosition = bossController.BossCenterPosition + offset;

            GameObject minionObject = Instantiate(absorbMinionPrefab, spawnPosition, Quaternion.identity);
            minionObject.name = $"Boss_AbsorbHealMinion_{index + 1}";

            BossAbsorbHealMinion minion = minionObject.GetComponent<BossAbsorbHealMinion>();

            if (minion == null)
            {
                minion = minionObject.AddComponent<BossAbsorbHealMinion>();
            }

            minion.Setup(
                this,
                bossController,
                minionHp,
                minionMoveSpeed,
                healAmountPerMinion,
                absorbDistance,
                minionDeathVfxPrefab,
                minionAbsorbVfxPrefab,
                bossHealVfxPrefab,
                debugLog
            );

            ApplyMinionSortingOrder(minionObject);

            if (minionSpawnVfxPrefab != null)
            {
                Instantiate(minionSpawnVfxPrefab, spawnPosition, Quaternion.identity);
            }

            activeMinions.Add(minion);

            if (debugLog)
            {
                Debug.Log($"[BossAbsorbMinionHealPattern] 흡수 몬스터 스폰 / index={index + 1}, pos={spawnPosition}");
            }
        }

        private void CleanupMinionList()
        {
            for (int i = activeMinions.Count - 1; i >= 0; i--)
            {
                if (activeMinions[i] == null || activeMinions[i].IsFinished)
                {
                    activeMinions.RemoveAt(i);
                }
            }
        }

        private void ForceDespawnRemainingMinions()
        {
            for (int i = activeMinions.Count - 1; i >= 0; i--)
            {
                if (activeMinions[i] != null)
                {
                    activeMinions[i].ForceDespawn(playDeathVfxOnTimeout);
                }
            }

            activeMinions.Clear();
        }

        private void FinishPattern()
        {
            if (!isPatternActive)
            {
                return;
            }

            isPatternActive = false;

            ApplyBossPatternState(false);

            if (healPatternLock != null)
            {
                healPatternLock.End(BossHealPatternType.AbsorbMinion);
            }

            if (debugLog)
            {
                Debug.Log($"[BossAbsorbMinionHealPattern] 흡수 몬스터 회복 패턴 종료 / absorbed={absorbedCount}, killed={killedCount}");
            }
        }

        private void ApplyBossPatternState(bool active)
        {
            if (bossController == null)
            {
                return;
            }

            if (lockBossMovementDuringPattern)
            {
                bossController.SetExternalMovementLock(active);
            }

            if (suppressBossContactDamageDuringPattern)
            {
                bossController.SetSuppressContactDamage(active);
            }
        }

        private void ApplyMinionSortingOrder(GameObject minionObject)
        {
            if (!forceMinionSortingOrder || minionObject == null)
            {
                return;
            }

            SpriteRenderer[] renderers = minionObject.GetComponentsInChildren<SpriteRenderer>(true);

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.sortingOrder = minionSortingOrder;
                }
            }
        }

        private void OnDisable()
        {
            CleanupOnDisableOrDestroy();
        }

        private void OnDestroy()
        {
            CleanupOnDisableOrDestroy();
        }

        private void CleanupOnDisableOrDestroy()
        {
            ForceDespawnRemainingMinions();

            if (isPatternActive)
            {
                ApplyBossPatternState(false);

                if (healPatternLock != null)
                {
                    healPatternLock.End(BossHealPatternType.AbsorbMinion);
                }
            }

            isPatternActive = false;
        }
    }
}