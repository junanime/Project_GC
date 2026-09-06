using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class BossColaBottleHealPattern : BossPatternBase
    {
        [Header("Cola Bottle Pattern")]
        [Tooltip("보스가 소환할 콜라병 오브젝트 프리팹입니다. 프리팹에는 BossHealColaBottle, Collider2D가 있어야 합니다.")]
        [SerializeField] private GameObject colaBottlePrefab;

        [Tooltip("한 번에 소환할 콜라병 개수입니다. 기본값은 4개입니다.")]
        [SerializeField] private int bottleCount = 4;

        [Tooltip("보스 중심에서 콜라병이 소환될 거리입니다.")]
        [SerializeField] private float spawnDistance = 4.5f;

        [Tooltip("콜라병 배치 각도 오프셋입니다. 45로 두면 대각선 4방향에 배치됩니다.")]
        [SerializeField] private float spawnAngleOffset = 45f;

        [Tooltip("각 콜라병의 체력입니다.")]
        [SerializeField] private float bottleHp = 60f;

        [Tooltip("콜라병 소환 후 보스 회복이 시작되기까지 걸리는 시간입니다.")]
        [SerializeField] private float healDelayAfterSpawn = 20f;

        [Tooltip("콜라병이 하나라도 살아있는 동안 보스가 초당 회복하는 체력입니다.")]
        [SerializeField] private float healPerSecond = 8f;

        [Tooltip("회복 계산 간격입니다. 0.25면 0.25초마다 healPerSecond를 나누어 회복합니다.")]
        [SerializeField] private float healTickInterval = 0.25f;

        [Tooltip("패턴이 강제로 종료되는 최대 시간입니다. 0이면 콜라병이 모두 파괴될 때까지 유지됩니다.")]
        [SerializeField] private float maxPatternDuration = 0f;

        [Header("Boss State")]
        [Tooltip("체크하면 콜라병이 하나라도 살아있는 동안 보스가 무적 상태가 됩니다.")]
        [SerializeField] private bool makeBossInvincibleWhileBottleExists = true;

        [Tooltip("체크하면 콜라병이 살아있는 동안에도 보스가 다른 패턴을 계속 사용할 수 있습니다.")]
        [SerializeField] private bool allowOtherPatternsWhileBottleExists = true;

        [Header("Visual")]
        [Tooltip("콜라병이 소환될 때 생성할 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject bottleSpawnVfxPrefab;

        [Tooltip("콜라병이 파괴될 때 생성할 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject bottleDestroyVfxPrefab;

        [Tooltip("보스가 회복될 때 보스 위치에 생성할 회복 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject bossHealVfxPrefab;

        [Tooltip("회복 이펙트를 생성하는 간격입니다. 너무 낮으면 이펙트가 과하게 많이 나옵니다.")]
        [SerializeField] private float healVfxInterval = 1f;

        [Tooltip("콜라병 SpriteRenderer Sorting Order를 강제로 설정할지 여부입니다.")]
        [SerializeField] private bool forceBottleSortingOrder = true;

        [Tooltip("콜라병 SpriteRenderer Sorting Order입니다. 보스보다 앞에 보이게 하려면 높게 설정하세요.")]
        [SerializeField] private int bottleSortingOrder = 450;

        [Header("Pattern Cast")]
        [Tooltip("콜라병 소환 패턴을 사용한 직후, 이 패턴이 사용 중이라고 취급되는 짧은 시간입니다. 이 시간이 지나면 다른 패턴 사용이 가능해집니다.")]
        [SerializeField] private float patternCastSeconds = 0.2f;

        [Header("Debug")]
        [Tooltip("체크하면 콜라병 회복 패턴 로그를 Console에 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        private readonly List<BossHealColaBottle> activeBottles = new List<BossHealColaBottle>();
        private Coroutine activeRoutine;
        private BossHealPatternLock healPatternLock;
        private bool isPatternActive = false;
        private bool invincibilityApplied = false;

        private void Reset()
        {
            patternName = "Cola Bottle Heal";
            cooldown = 40f;
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

            if (colaBottlePrefab == null)
            {
                Debug.LogWarning("[BossColaBottleHealPattern] Cola Bottle Prefab이 비어 있습니다.");
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

            if (!healPatternLock.TryBegin(BossHealPatternType.ColaBottle))
            {
                yield break;
            }

            isPatternActive = true;
            activeBottles.Clear();

            SpawnBottles();

            if (activeBottles.Count <= 0)
            {
                FinishPattern();
                yield break;
            }

            if (makeBossInvincibleWhileBottleExists)
            {
                BossHealingRuntimeBridge.AddExternalInvincibility(bossController);
                invincibilityApplied = true;
            }

            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }

            activeRoutine = StartCoroutine(ActivePatternRoutine());

            if (debugLog)
            {
                Debug.Log($"[BossColaBottleHealPattern] 콜라병 회복 패턴 시작 / count={activeBottles.Count}");
            }

            if (allowOtherPatternsWhileBottleExists)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, patternCastSeconds));
                yield break;
            }

            while (isPatternActive)
            {
                yield return null;
            }
        }

        public void NotifyBottleDestroyed(BossHealColaBottle bottle)
        {
            if (bottle == null)
            {
                return;
            }

            activeBottles.Remove(bottle);

            if (debugLog)
            {
                Debug.Log($"[BossColaBottleHealPattern] 콜라병 파괴 확인 / remain={activeBottles.Count}");
            }
        }

        private void SpawnBottles()
        {
            int finalBottleCount = Mathf.Max(1, bottleCount);
            float angleStep = 360f / finalBottleCount;

            for (int i = 0; i < finalBottleCount; i++)
            {
                float angle = spawnAngleOffset + angleStep * i;
                float rad = angle * Mathf.Deg2Rad;

                Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * spawnDistance;
                Vector3 spawnPosition = bossController.BossCenterPosition + offset;

                GameObject bottleObject = Instantiate(colaBottlePrefab, spawnPosition, Quaternion.identity);
                bottleObject.name = $"Boss_Heal_ColaBottle_{i + 1}";

                BossHealColaBottle bottle = bottleObject.GetComponent<BossHealColaBottle>();

                if (bottle == null)
                {
                    bottle = bottleObject.AddComponent<BossHealColaBottle>();
                }

                bottle.Setup(this, bottleHp, bottleDestroyVfxPrefab, debugLog);
                ApplyBottleSortingOrder(bottleObject);

                if (bottleSpawnVfxPrefab != null)
                {
                    Instantiate(bottleSpawnVfxPrefab, spawnPosition, Quaternion.identity);
                }

                activeBottles.Add(bottle);
            }
        }

        private IEnumerator ActivePatternRoutine()
        {
            float elapsed = 0f;
            float healTimer = 0f;
            float healVfxTimer = 999f;

            while (isPatternActive)
            {
                if (bossController == null || bossController.IsDead)
                {
                    break;
                }

                CleanupBottleList();

                if (activeBottles.Count <= 0)
                {
                    break;
                }

                elapsed += Time.deltaTime;

                if (maxPatternDuration > 0f && elapsed >= maxPatternDuration)
                {
                    if (debugLog)
                    {
                        Debug.Log("[BossColaBottleHealPattern] Max Pattern Duration 도달로 패턴 종료");
                    }

                    break;
                }

                if (elapsed >= healDelayAfterSpawn)
                {
                    healTimer += Time.deltaTime;
                    healVfxTimer += Time.deltaTime;

                    float finalHealTickInterval = Mathf.Max(0.05f, healTickInterval);

                    while (healTimer >= finalHealTickInterval)
                    {
                        healTimer -= finalHealTickInterval;

                        float healAmount = Mathf.Max(0f, healPerSecond) * finalHealTickInterval;

                        bool healed = BossHealingRuntimeBridge.TryHealBoss(
                            bossController,
                            healAmount,
                            out float beforeHp,
                            out float afterHp,
                            out float maxHp);

                        if (healed)
                        {
                            if (debugLog)
                            {
                                Debug.Log($"[BossColaBottleHealPattern] 보스 회복 / {beforeHp:0.##} → {afterHp:0.##} / max={maxHp:0.##}");
                            }

                            if (bossHealVfxPrefab != null && healVfxTimer >= Mathf.Max(0.05f, healVfxInterval))
                            {
                                healVfxTimer = 0f;
                                Instantiate(bossHealVfxPrefab, bossController.BossCenterPosition, Quaternion.identity);
                            }
                        }
                    }
                }

                yield return null;
            }

            FinishPattern();
        }

        private void CleanupBottleList()
        {
            for (int i = activeBottles.Count - 1; i >= 0; i--)
            {
                if (activeBottles[i] == null || activeBottles[i].IsDestroyed)
                {
                    activeBottles.RemoveAt(i);
                }
            }
        }

        private void FinishPattern()
        {
            if (!isPatternActive)
            {
                return;
            }

            isPatternActive = false;

            CleanupBottleList();

            if (invincibilityApplied)
            {
                BossHealingRuntimeBridge.RemoveExternalInvincibility(bossController);
                invincibilityApplied = false;
            }

            if (healPatternLock != null)
            {
                healPatternLock.End(BossHealPatternType.ColaBottle);
            }

            activeRoutine = null;

            if (debugLog)
            {
                Debug.Log("[BossColaBottleHealPattern] 콜라병 회복 패턴 종료 / 보스 무적 해제");
            }
        }

        private void ApplyBottleSortingOrder(GameObject bottleObject)
        {
            if (!forceBottleSortingOrder || bottleObject == null)
            {
                return;
            }

            SpriteRenderer[] renderers = bottleObject.GetComponentsInChildren<SpriteRenderer>(true);

            foreach (SpriteRenderer renderer in renderers)
            {
                renderer.sortingOrder = bottleSortingOrder;
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
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            for (int i = activeBottles.Count - 1; i >= 0; i--)
            {
                if (activeBottles[i] != null)
                {
                    activeBottles[i].ForceDestroy();
                }
            }

            activeBottles.Clear();

            if (invincibilityApplied)
            {
                BossHealingRuntimeBridge.RemoveExternalInvincibility(bossController);
                invincibilityApplied = false;
            }

            if (healPatternLock != null)
            {
                healPatternLock.End(BossHealPatternType.ColaBottle);
            }

            isPatternActive = false;
        }
    }
}