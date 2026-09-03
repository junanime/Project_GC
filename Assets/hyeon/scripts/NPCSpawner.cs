using UnityEngine;
using System.Collections;

namespace Vampire
{
    public class NPCSpawner : MonoBehaviour
    {
        [Header("상인 스폰 설정")]
        [Tooltip("생성할 상인 프리팹입니다.")]
        [SerializeField] private GameObject merchantPrefab;

        [Tooltip("상인 스폰을 시도하는 간격입니다.")]
        [SerializeField] private float spawnInterval = 25f;

        [Tooltip("필드에 동시에 존재할 수 있는 최대 상인 수입니다.")]
        [SerializeField] private int maxMerchants = 4;

        [Header("동적 성장 확률 레버")]
        [Tooltip("기본 상인 등장 확률입니다.")]
        [SerializeField] private float baseSpawnChance = 0.25f;

        [Tooltip("최대 상인 등장 확률입니다.")]
        [SerializeField] private float maxSpawnChance = 0.65f;

        [Tooltip("1분마다 증가하는 상인 등장 확률입니다.")]
        [SerializeField] private float chanceIncreasePerMinute = 0.03f;

        [Header("Mini Stage Guard")]
        [Tooltip("미니 스테이지 진행 중에는 상인 NPC 스폰을 멈춥니다.")]
        [SerializeField] private bool blockWhileMiniStage = true;

        [Tooltip("미니 스테이지 중에는 스폰 대기 시간도 멈춥니다. 체크 추천입니다.")]
        [SerializeField] private bool pauseSpawnTimerWhileMiniStage = true;

        [Tooltip("씬의 LevelManager입니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] private LevelManager levelManager;

        [Tooltip("미니 스테이지 때문에 상인 스폰이 차단될 때 로그를 출력합니다.")]
        [SerializeField] private bool logMiniStageBlock = false;

        private Character player;
        private Coroutine spawnRoutine;

        private void Start()
        {
            ResolveReferences();

            if (player != null)
            {
                TrySpawnImmediatelyIfNoMerchant();

                if (spawnRoutine != null)
                {
                    StopCoroutine(spawnRoutine);
                }

                spawnRoutine = StartCoroutine(SpawnRoutine());
            }
            else
            {
                Debug.LogError("[NPCSpawner] 플레이어를 찾을 수 없습니다!");
            }
        }

        private void ResolveReferences()
        {
            if (player == null)
            {
                player = FindObjectOfType<Character>();
            }

            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }
        }

        private IEnumerator SpawnRoutine()
        {
            while (true)
            {
                if (pauseSpawnTimerWhileMiniStage)
                {
                    yield return WaitSecondsRespectingMiniStagePause(spawnInterval);
                }
                else
                {
                    yield return new WaitForSeconds(spawnInterval);
                }

                TrySpawnMerchantByChance();
            }
        }

        private IEnumerator WaitSecondsRespectingMiniStagePause(float seconds)
        {
            float timer = 0f;
            float targetTime = Mathf.Max(0f, seconds);

            while (timer < targetTime)
            {
                if (!IsMiniStageSpawnBlocked())
                {
                    timer += Time.deltaTime;
                }

                yield return null;
            }
        }

        private void TrySpawnImmediatelyIfNoMerchant()
        {
            if (IsMiniStageSpawnBlocked())
            {
                if (logMiniStageBlock)
                {
                    Debug.Log("[NPCSpawner] 미니 스테이지 진행 중이라 즉시 상인 스폰을 막았습니다.", this);
                }

                return;
            }

            MerchantNPC[] currentMerchants = FindObjectsOfType<MerchantNPC>();

            if (currentMerchants.Length == 0)
            {
                TrySpawnMerchantByChance();
            }
        }

        private void TrySpawnMerchantByChance()
        {
            if (IsMiniStageSpawnBlocked())
            {
                if (logMiniStageBlock)
                {
                    Debug.Log("[NPCSpawner] 미니 스테이지 진행 중이라 상인 스폰을 막았습니다.", this);
                }

                return;
            }

            MerchantNPC[] currentMerchants = FindObjectsOfType<MerchantNPC>();

            if (currentMerchants.Length >= maxMerchants)
            {
                return;
            }

            float currentSpawnChance = GetCurrentSpawnChance();

            if (Random.value < currentSpawnChance)
            {
                SpawnMerchant(currentSpawnChance);
            }
            else
            {
                Debug.Log($"[NPCSpawner] 아저씨 스폰 실패 (현재 동적 확률: {currentSpawnChance * 100f:0.#}%)");
            }
        }

        private float GetCurrentSpawnChance()
        {
            float minutesElapsed = Time.timeSinceLevelLoad / 60f;

            return Mathf.Min(
                maxSpawnChance,
                baseSpawnChance + (minutesElapsed * chanceIncreasePerMinute)
            );
        }

        private void SpawnMerchant(float currentChance)
        {
            if (IsMiniStageSpawnBlocked())
            {
                if (logMiniStageBlock)
                {
                    Debug.Log("[NPCSpawner] 미니 스테이지 진행 중이라 상인 Instantiate를 막았습니다.", this);
                }

                return;
            }

            if (merchantPrefab == null)
            {
                Debug.LogWarning("[NPCSpawner] Merchant Prefab이 비어 있습니다.", this);
                return;
            }

            Vector2 safeSpawnPos = GetRandomPositionOutsideScreen();

            Instantiate(merchantPrefab, safeSpawnPos, Quaternion.identity);

            Debug.Log($"[시스템] {currentChance * 100f:0.#}% 확률을 뚫고 수상한 아저씨 등장! (좌표: {safeSpawnPos})");
        }

        private Vector2 GetRandomPositionOutsideScreen()
        {
            Camera cam = Camera.main;

            if (cam == null || player == null)
            {
                return (Vector2)transform.position + Random.insideUnitCircle.normalized * 12f;
            }

            float screenHalfHeight = cam.orthographicSize;
            float screenHalfWidth = screenHalfHeight * cam.aspect;
            float margin = 4f;

            Vector2 spawnOffset = Vector2.zero;

            if (player.Velocity.sqrMagnitude > 0.01f)
            {
                Vector2 moveDir = player.Velocity.normalized;

                if (Mathf.Abs(moveDir.x) > Mathf.Abs(moveDir.y))
                {
                    spawnOffset.x = Mathf.Sign(moveDir.x) * (screenHalfWidth + margin);
                    spawnOffset.y = Random.Range(-screenHalfHeight, screenHalfHeight);
                }
                else
                {
                    spawnOffset.x = Random.Range(-screenHalfWidth, screenHalfWidth);
                    spawnOffset.y = Mathf.Sign(moveDir.y) * (screenHalfHeight + margin);
                }
            }
            else
            {
                if (Random.value > 0.5f)
                {
                    spawnOffset.x = Mathf.Sign(Random.Range(-1f, 1f)) * (screenHalfWidth + margin);
                    spawnOffset.y = Random.Range(-screenHalfHeight, screenHalfHeight);
                }
                else
                {
                    spawnOffset.x = Random.Range(-screenHalfWidth, screenHalfWidth);
                    spawnOffset.y = Mathf.Sign(Random.Range(-1f, 1f)) * (screenHalfHeight + margin);
                }
            }

            return (Vector2)player.transform.position + spawnOffset;
        }

        private bool IsMiniStageSpawnBlocked()
        {
            if (!blockWhileMiniStage)
            {
                return false;
            }

            if (MiniStageRuntimeState.IsInsideMiniStage)
            {
                return true;
            }

            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            return levelManager != null && levelManager.IsRunFlowPaused;
        }

        private void OnDisable()
        {
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }
        }
    }
}