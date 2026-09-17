using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class BossLevelSpawner : MonoBehaviour
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

        private IEnumerator Start()
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

            if (useRealtimeForDebug)
            {
                yield return new WaitForSecondsRealtime(spawnAfterSeconds);
            }
            else
            {
                yield return new WaitForSeconds(spawnAfterSeconds);
            }

            if (!spawnOnlyOnce || !hasSpawned)
            {
                SpawnBoss();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(debugSpawnKey))
            {
                Debug.Log("[BossLevelSpawner] Debug spawn key pressed.");
                SpawnBoss();
            }
        }

        private void SpawnBoss()
        {
            ResolveReferences();

            if (spawnOnlyOnce && hasSpawned)
            {
                Debug.Log("[BossLevelSpawner] Spawn skipped because boss already spawned.");
                return;
            }

            if (levelManager == null)
            {
                Debug.LogError("[BossLevelSpawner] LevelManager is NULL.");
                return;
            }

            if (levelManager.EntityManager == null)
            {
                Debug.LogError("[BossLevelSpawner] EntityManager is NULL.");
                return;
            }

            if (levelManager.CurrentLevelBlueprint == null)
            {
                Debug.LogError("[BossLevelSpawner] CurrentLevelBlueprint is NULL.");
                return;
            }

            if (levelManager.CurrentLevelBlueprint.finalBoss == null)
            {
                Debug.LogError("[BossLevelSpawner] Final Boss setting is NULL in LevelBlueprint.");
                return;
            }

            if (levelManager.CurrentLevelBlueprint.finalBoss.bossBlueprint == null)
            {
                Debug.LogError("[BossLevelSpawner] Final Boss BossBlueprint is NULL.");
                return;
            }

            int bossPoolIndex = levelManager.CurrentLevelBlueprint.monsters.Length;
            Vector3 spawnPosition = GetSpawnPosition();

            if (logOnSpawn)
            {
                Debug.Log(
                    $"[BossLevelSpawner] Spawning boss through EntityManager | " +
                    $"PoolIndex={bossPoolIndex} | Position={spawnPosition}"
                );
            }

            spawnedBossMonster = levelManager.EntityManager.SpawnMonster(
                bossPoolIndex,
                spawnPosition,
                levelManager.CurrentLevelBlueprint.finalBoss.bossBlueprint,
                0f
            );

            if (spawnedBossMonster == null)
            {
                Debug.LogError("[BossLevelSpawner] EntityManager.SpawnMonster returned NULL.");
                return;
            }

            BossController bossController =
    spawnedBossMonster.GetComponentInChildren<BossController>(true);

            if (bossController != null)
            {
                if (playerCharacter == null)
                {
                    playerCharacter = FindObjectOfType<Character>();
                }

                bossController.SetPlayerCharacter(playerCharacter);
            }
            else
            {
                Debug.LogWarning("[BossLevelSpawner] Spawned boss has no BossController. Patterns will not run.");
            }

            hasSpawned = true;

            if (logOnSpawn)
            {
                Debug.Log($"[BossLevelSpawner] Boss spawned successfully: {spawnedBossMonster.name}");
            }
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