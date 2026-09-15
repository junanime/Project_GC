using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 상호작용 시 LevelBlueprint의 Final Boss를 송신과 하강 연출 후 소환하는 오브젝트.
    /// 수동/자동 호출 모두 같은 연출과 고정 전투 수치를 사용한다.
    /// </summary>
    public class FinalBossSummonInteractable : InteractableEventObject
    {
        [Header("Boss Spawn")]
        [SerializeField] private bool spawnRelativeToPlayer = true;
        [SerializeField] private bool useRandomDirectionAroundPlayer = true;
        [SerializeField] private float spawnDistanceFromPlayer = 6f;
        [SerializeField] private Vector2 spawnOffsetFromPlayer = new Vector2(0f, 6f);

        [Tooltip("체크하면 지정된 위치에 보스를 소환합니다.")]
        [SerializeField] private bool useFixedSpawnPoint = false;

        [SerializeField] private Transform fixedSpawnPoint;

        [Header("Duplicate Prevention")]
        [Tooltip("true면 이미 보스가 존재할 때 추가 소환하지 않습니다.")]
        [SerializeField] private bool preventDuplicateBoss = true;

        [Tooltip("true면 상호작용으로 보스를 소환한 뒤 기존 BossLevelSpawner를 비활성화합니다.")]
        [SerializeField] private bool disableBossLevelSpawnersAfterSpawn = true;

        private static FinalBossSummonInteractable pendingSummon;
        public static bool IsSummoning => pendingSummon != null;
        private BossSummonPresentation presentation;
        private bool summoning;
        protected override bool KeepVisibleAfterInteraction => true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateOnPlayStart()
        {
            pendingSummon = null;
        }

        protected override void Awake()
        {
            base.Awake();
            presentation = gameObject.AddComponent<BossSummonPresentation>();
            presentation.Initialize(GetComponent<SpriteRenderer>());
        }

        protected override void OnDisable()
        {
            StopAllCoroutines();
            if (presentation != null) presentation.ResetStandby();
            if (pendingSummon == this) pendingSummon = null;
            if (summoning) ResetInteractionAvailability();
            summoning = false;
            base.OnDisable();
        }

        public bool TryAutomaticSummon()
        {
            if (!isActiveAndEnabled || HasInteracted || Time.timeScale <= 0f ||
                MiniStageRuntimeState.IsInsideMiniStage) return false;
            if (levelManager == null) levelManager = FindObjectOfType<LevelManager>();
            if (levelManager == null || levelManager.IsRunFlowPaused || levelManager.IsLevelEnded) return false;
            if (!ExecuteInteraction(levelManager.PlayerCharacter)) return false;
            // A player can leave the terminal far behind. Keep automatic transmission visible.
            Camera camera = Camera.main;
            if (camera != null)
            {
                Vector3 viewport = camera.WorldToViewportPoint(transform.position);
                if (viewport.z <= 0f || viewport.x < .15f || viewport.x > .85f || viewport.y < .15f || viewport.y > .85f)
                {
                    float depth = camera.WorldToViewportPoint(levelManager.PlayerCharacter.transform.position).z;
                    Vector3 visible = camera.ViewportToWorldPoint(new Vector3(.5f, .6f, depth));
                    transform.position = new Vector3(visible.x, visible.y, transform.position.z);
                }
            }
            CompleteInteraction();
            return true;
        }

        protected override bool ExecuteInteraction(Character player)
        {
            if (levelManager == null)
            {
                Debug.LogError("[FinalBossSummonInteractable] LevelManager를 찾지 못했습니다.", this);
                return false;
            }

            if (levelManager.EntityManager == null)
            {
                Debug.LogError("[FinalBossSummonInteractable] EntityManager가 비어 있습니다.", this);
                return false;
            }

            LevelBlueprint levelBlueprint = levelManager.CurrentLevelBlueprint;

            if (levelBlueprint == null)
            {
                Debug.LogError("[FinalBossSummonInteractable] CurrentLevelBlueprint가 비어 있습니다.", this);
                return false;
            }

            if (levelBlueprint.finalBoss == null ||
                levelBlueprint.finalBoss.bossBlueprint == null ||
                levelBlueprint.finalBoss.bossPrefab == null)
            {
                Debug.LogError("[FinalBossSummonInteractable] Final Boss 설정이 비어 있습니다.", this);
                return false;
            }

            if (summoning || IsSummoning || (preventDuplicateBoss && IsBossAlreadyPresent()))
            {
                if (debugLog)
                {
                    Debug.Log("[FinalBossSummonInteractable] 이미 보스가 존재해서 소환하지 않습니다.", this);
                }

                return false;
            }

            pendingSummon = this;
            summoning = true;
            StartCoroutine(SummonSequence(player, levelBlueprint));
            return true;
        }

        private IEnumerator SummonSequence(Character player, LevelBlueprint blueprint)
        {
            bool success = false;
            try
            {
                yield return presentation.Transmit();
                if (levelManager == null || levelManager.CurrentLevelBlueprint != blueprint) yield break;
                Vector3 landing = GetSpawnPosition(player);
                yield return presentation.Descend(blueprint.finalBoss.bossPrefab, landing);
                if (levelManager == null || levelManager.CurrentLevelBlueprint != blueprint) yield break;
                success = SpawnConfiguredBoss(blueprint, landing);
                presentation.FinishArrival();
            }
            finally
            {
                if (pendingSummon == this) pendingSummon = null;
                summoning = false;
                if (!success)
                {
                    presentation.ResetStandby();
                    ResetInteractionAvailability();
                }
            }
        }

        private bool SpawnConfiguredBoss(LevelBlueprint levelBlueprint, Vector3 spawnPosition)
        {
            GameObject spawnedBoss = levelManager.EntityManager.SpawnFinalBoss(
                levelBlueprint, spawnPosition);

            if (spawnedBoss == null)
            {
                Debug.LogError("[FinalBossSummonInteractable] 보스 소환 실패: SpawnMonster가 null을 반환했습니다.", this);
                return false;
            }

            Monster legacyBoss = spawnedBoss.GetComponent<Monster>();
            if (legacyBoss != null) legacyBoss.OnKilled.AddListener(levelManager.LevelPassed);
            levelManager.NotifyExternalFinalBossSpawned();

            BossController bossController = spawnedBoss.GetComponent<BossController>();

            if (bossController == null)
            {
                bossController = spawnedBoss.GetComponentInChildren<BossController>(true);
            }

            if (bossController != null)
            {
                bossController.SetPlayerCharacter(levelManager.PlayerCharacter);
            }


            GameAudioManager.StartBossAudio();

            if (disableBossLevelSpawnersAfterSpawn)
            {
                DisableBossLevelSpawners();
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[FinalBossSummonInteractable] 최종보스 소환 완료 | " +
                    $"Position={spawnPosition} | Boss={spawnedBoss.name}",
                    this
                );
            }

            return true;
        }


        private Vector3 GetSpawnPosition(Character player)
        {
            if (useFixedSpawnPoint && fixedSpawnPoint != null)
            {
                return fixedSpawnPoint.position;
            }

            Character targetPlayer = player != null ? player : levelManager.PlayerCharacter;

            if (spawnRelativeToPlayer && targetPlayer != null)
            {
                if (useRandomDirectionAroundPlayer)
                {
                    Vector2 randomDirection = UnityEngine.Random.insideUnitCircle.normalized;

                    if (randomDirection == Vector2.zero)
                    {
                        randomDirection = Vector2.up;
                    }

                    return targetPlayer.transform.position +
                           (Vector3)(randomDirection * Mathf.Max(0.1f, spawnDistanceFromPlayer));
                }

                return targetPlayer.transform.position + (Vector3)spawnOffsetFromPlayer;
            }

            return transform.position;
        }

        private bool IsBossAlreadyPresent()
        {
            if (IsSummoning)
            {
                return true;
            }

            BossMonster existingBossMonster = FindObjectOfType<BossMonster>();
            BossController existingBossController = FindObjectOfType<BossController>();

            return existingBossMonster != null || existingBossController != null;
        }

        private void DisableBossLevelSpawners()
        {
            TimedSpecialMonsterSpawner[] bossSpawners = FindObjectsOfType<TimedSpecialMonsterSpawner>();

            for (int i = 0; i < bossSpawners.Length; i++)
            {
                if (bossSpawners[i] != null)
                {
                    bossSpawners[i].DisableScheduledFinalBosses();
                }
            }

            if (debugLog && bossSpawners.Length > 0)
            {
                Debug.Log($"[FinalBossSummonInteractable] BossLevelSpawner {bossSpawners.Length}개 비활성화", this);
            }
        }
    }
}
