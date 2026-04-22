using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class BossLevelSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Character playerCharacter;
        [SerializeField] private GameObject bossPrefab;

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
        private GameObject spawnedBossInstance;

        private void Start()
        {
            if (playerCharacter == null)
            {
                playerCharacter = FindObjectOfType<Character>();
            }

            Debug.Log($"[BossLevelSpawner] Start | bossPrefab={(bossPrefab != null ? bossPrefab.name : "NULL")} | player={(playerCharacter != null ? playerCharacter.name : "NULL")}");
            StartCoroutine(SpawnRoutine());
        }

        private IEnumerator SpawnRoutine()
        {
            Debug.Log($"[BossLevelSpawner] Waiting {spawnAfterSeconds:F1} seconds before spawn...");

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
            if (bossPrefab == null)
            {
                Debug.LogError("[BossLevelSpawner] bossPrefab is NULL.");
                return;
            }

            if (spawnOnlyOnce && hasSpawned)
            {
                Debug.Log("[BossLevelSpawner] Spawn skipped because boss already spawned.");
                return;
            }

            Vector3 spawnPosition = GetSpawnPosition();
            Debug.Log($"[BossLevelSpawner] Spawning boss at {spawnPosition}");

            spawnedBossInstance = Instantiate(bossPrefab, spawnPosition, Quaternion.identity);

            if (spawnedBossInstance == null)
            {
                Debug.LogError("[BossLevelSpawner] Instantiate failed.");
                return;
            }

            BossController bossController = spawnedBossInstance.GetComponent<BossController>();
            if (bossController == null)
            {
                Debug.LogError("[BossLevelSpawner] Spawned prefab has no BossController on root.");
            }
            else
            {
                if (playerCharacter == null)
                {
                    playerCharacter = FindObjectOfType<Character>();
                }

                if (playerCharacter != null)
                {
                    bossController.SetPlayerCharacter(playerCharacter);
                    Debug.Log($"[BossLevelSpawner] Boss linked to player: {playerCharacter.name}");
                }
                else
                {
                    Debug.LogWarning("[BossLevelSpawner] Player not found. Boss spawned without player reference.");
                }
            }

            hasSpawned = true;

            if (logOnSpawn)
            {
                Debug.Log("[BossLevelSpawner] Boss spawned successfully.");
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