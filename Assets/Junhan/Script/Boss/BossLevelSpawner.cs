using static UnityEngine.Object;
using System.Collections;
using UnityEngine;

namespace Vampire
{
    [System.Serializable]
    public class BossLevelSpawner : RuntimeModule
    {
        [Header("References")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private Character playerCharacter;

        [Header("Spawn Condition")]
        [SerializeField] private float spawnAfterSeconds = 10f;
        [SerializeField] private bool spawnOnlyOnce = true;
        [SerializeField] private bool useRealtimeForDebug = true;

        [Header("Spawn Position")]
        [SerializeField] private bool spawnRelativeToPlayer = true;
        [SerializeField] private bool useRandomDirectionAroundPlayer = true;
        [SerializeField] private float spawnDistanceFromPlayer = 3f;
        [SerializeField] private Vector2 spawnOffsetFromPlayer = new Vector2(0f, 3f);
        [SerializeField] private bool useFixedSpawnPoint = false;
        [SerializeField] private Transform fixedSpawnPoint;

        [Header("Debug")]
        [SerializeField] private bool logOnSpawn = true;
        [SerializeField] private KeyCode debugSpawnKey = KeyCode.F8;

        private bool hasSpawned = false;
        private Monster spawnedBossMonster;

        protected override System.Collections.IEnumerator OnStart()
        {
            ResolveReferences();

            // LevelManager.Start()가 EntityManager.Init()을 끝낼 시간을 준다.
            yield return null;
            yield return null;

            ResolveReferences();

            if (logOnSpawn)
            {
                Debug.Log(
                    $"[BossLevelSpawner] Ready | " +
                    $"LevelManager={(levelManager != null ? "OK" : "NULL")} | " +
                    $"EntityManager={(levelManager != null && levelManager.EntityManager != null ? "OK" : "NULL")} | " +
                    $"Player={(playerCharacter != null ? playerCharacter.name : "NULL")}"
                );
            }

            StartCoroutine(SpawnRoutine());
        }

        private void ResolveReferences()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            if (playerCharacter == null)
            {
                playerCharacter = FindObjectOfType<Character>();
            }
        }

        private IEnumerator SpawnRoutine()
        {
            if (logOnSpawn)
            {
                Debug.Log($"[BossLevelSpawner] Waiting {spawnAfterSeconds:F1} seconds before boss spawn...");
            }

            float elapsed = 0f;
            while (elapsed < Mathf.Max(0f, spawnAfterSeconds))
            {
                if (!SpawnBlocked()) elapsed += useRealtimeForDebug ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
            while (SpawnBlocked()) yield return null;

            if (!spawnOnlyOnce || !hasSpawned)
            {
                SpawnBoss();
            }
        }

        protected override void OnTick()
        {
            if (Vampire.GameInput.GetKeyDown(debugSpawnKey))
            {
                Debug.Log("[BossLevelSpawner] Debug spawn key pressed.");
                SpawnBoss();
            }
        }

        protected override void OnModuleDisable() { StopAllCoroutines(); }

        private bool SpawnBlocked() => MiniStageRuntimeState.IsInsideMiniStage ||
            (levelManager != null && (levelManager.IsRunFlowPaused || levelManager.IsLevelEnded));

        private void SpawnBoss()
        {
            ResolveReferences();
            if (SpawnBlocked() || (spawnOnlyOnce && hasSpawned)) return;
            var terminal = FindObjectOfType<FinalBossSummonInteractable>();
            if (terminal != null && terminal.TryAutomaticSummon()) hasSpawned = true;
        }

        private Vector3 GetSpawnPosition()
        {
            if (useFixedSpawnPoint && fixedSpawnPoint != null)
            {
                return fixedSpawnPoint.position;
            }

            if (spawnRelativeToPlayer)
            {
                if (playerCharacter == null)
                {
                    playerCharacter = FindObjectOfType<Character>();
                }

                if (playerCharacter != null)
                {
                    if (useRandomDirectionAroundPlayer)
                    {
                        Vector2 randomDirection = Random.insideUnitCircle.normalized;

                        if (randomDirection == Vector2.zero)
                        {
                            randomDirection = Vector2.up;
                        }

                        return playerCharacter.transform.position + (Vector3)(randomDirection * spawnDistanceFromPlayer);
                    }

                    return playerCharacter.transform.position + (Vector3)spawnOffsetFromPlayer;
                }

                Debug.LogWarning("[BossLevelSpawner] Player not found. Falling back to spawner transform position.");
            }

            return transform.position;
        }
    }
}
