using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 필드 소화효소 몬스터 스포너.
    /// 
    /// 혈전 스포너와 비슷하게 LevelManager의 런 흐름 정지 상태를 확인합니다.
    /// 추가로 MiniStageRuntimeState.IsInsideMiniStage도 확인해서
    /// 미니 스테이지 진행 중에는 필드 소화효소가 생성되지 않게 막습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DigestiveEnzymeFieldSpawner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("필드에 생성할 소화효소 몬스터 프리팹입니다.")]
        [SerializeField] private DigestiveEnzymeMonster digestiveEnzymePrefab;

        [Tooltip("현재 스테이지의 LevelManager입니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] private LevelManager levelManager;

        [Tooltip("플레이어 캐릭터입니다. 비워두면 LevelManager에서 자동으로 가져옵니다.")]
        [SerializeField] private Character playerCharacter;

        [Header("Spawn Timing")]
        [Tooltip("게임 시작 후 첫 소화효소 몬스터가 생성되기까지 걸리는 시간입니다.")]
        [SerializeField] private float firstSpawnTime = 25f;

        [Tooltip("첫 생성 이후 소화효소 몬스터를 다시 생성하는 간격입니다.")]
        [SerializeField] private float spawnInterval = 35f;

        [Tooltip("동시에 필드에 존재할 수 있는 소화효소 몬스터 최대 수입니다.")]
        [SerializeField] private int maxAliveCount = 2;

        [Header("Spawn Position")]
        [Tooltip("플레이어 기준으로 어느 정도 떨어진 곳에 소화효소 몬스터를 생성할지 정합니다.")]
        [SerializeField] private float spawnDistanceFromPlayer = 8f;

        [Tooltip("스폰 거리의 랜덤 오차입니다. 예: 1.5면 거리 8 기준 6.5~9.5 사이에서 생성될 수 있습니다.")]
        [SerializeField] private float spawnDistanceJitter = 1.5f;

        [Tooltip("true면 플레이어 주변 랜덤 방향에 생성합니다. false면 Fixed Spawn Direction 방향으로 생성합니다.")]
        [SerializeField] private bool randomDirectionSpawn = true;

        [Tooltip("Random Direction Spawn이 false일 때 사용할 고정 생성 방향입니다.")]
        [SerializeField] private Vector2 fixedSpawnDirection = Vector2.right;

        [Tooltip("스폰 위치 주변에 겹침을 피할 반경입니다. 0이면 겹침 검사를 하지 않습니다.")]
        [SerializeField] private float avoidSpawnRadius = 0.6f;

        [Tooltip("스폰 위치 겹침 검사에 사용할 레이어입니다. 벽/장애물 레이어가 있다면 지정합니다.")]
        [SerializeField] private LayerMask avoidSpawnLayer;

        [Header("Mini Stage / Pause Blocking")]
        [Tooltip("LevelManager.IsRunFlowPaused가 true일 때 스폰 타이머를 멈추고 생성하지 않습니다.")]
        [SerializeField] private bool blockWhileRunFlowPaused = true;

        [Tooltip("MiniStageRuntimeState.IsInsideMiniStage가 true일 때 생성하지 않습니다.")]
        [SerializeField] private bool blockInsideMiniStage = true;

        [Tooltip("스폰이 차단되는 동안 타이머를 0으로 되돌릴지 여부입니다. false면 필드 복귀 후 남은 시간부터 이어집니다.")]
        [SerializeField] private bool resetTimerWhileBlocked = false;

        [Header("Debug / Test")]
        [Tooltip("소화효소 스폰 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        [Tooltip("테스트용 강제 스폰 키를 사용할지 여부입니다.")]
        [SerializeField] private bool enableForceSpawnKey = true;

        [Tooltip("Play 중 이 키를 누르면 시간과 상관없이 소화효소 몬스터를 하나 생성합니다.")]
        [SerializeField] private KeyCode forceSpawnKey = KeyCode.F9;

        private readonly List<DigestiveEnzymeMonster> aliveEnzymes = new List<DigestiveEnzymeMonster>();

        private float timer;
        private bool firstSpawnDone;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            timer = 0f;
            firstSpawnDone = false;
            CleanupAliveList();
        }

        private void Update()
        {
            CleanupAliveList();

            if (enableForceSpawnKey && Input.GetKeyDown(forceSpawnKey))
            {
                SpawnOne();
                return;
            }

            if (!CanSpawnNow())
            {
                if (resetTimerWhileBlocked)
                {
                    timer = 0f;
                }

                return;
            }

            if (digestiveEnzymePrefab == null)
            {
                return;
            }

            if (maxAliveCount > 0 && aliveEnzymes.Count >= maxAliveCount)
            {
                return;
            }

            timer += Time.deltaTime;

            float requiredTime = firstSpawnDone
                ? Mathf.Max(0.1f, spawnInterval)
                : Mathf.Max(0f, firstSpawnTime);

            if (timer < requiredTime)
            {
                return;
            }

            SpawnOne();

            timer = 0f;
            firstSpawnDone = true;
        }

        public void NotifyEnzymeRemoved(DigestiveEnzymeMonster enzyme)
        {
            if (enzyme == null)
            {
                return;
            }

            aliveEnzymes.Remove(enzyme);
        }

        private bool CanSpawnNow()
        {
            ResolveReferences();

            if (levelManager == null)
            {
                return false;
            }

            if (playerCharacter == null)
            {
                return false;
            }

            if (blockWhileRunFlowPaused && levelManager.IsRunFlowPaused)
            {
                return false;
            }

            if (blockInsideMiniStage && MiniStageRuntimeState.IsInsideMiniStage)
            {
                return false;
            }

            return true;
        }

        private void SpawnOne()
        {
            ResolveReferences();
            CleanupAliveList();

            if (digestiveEnzymePrefab == null)
            {
                Debug.LogWarning("[DigestiveEnzymeFieldSpawner] Digestive Enzyme Prefab이 비어 있습니다.");
                return;
            }

            if (playerCharacter == null)
            {
                Debug.LogWarning("[DigestiveEnzymeFieldSpawner] PlayerCharacter를 찾지 못해 소화효소를 생성할 수 없습니다.");
                return;
            }

            if (maxAliveCount > 0 && aliveEnzymes.Count >= maxAliveCount)
            {
                if (debugLog)
                {
                    Debug.Log(
                        $"[DigestiveEnzymeFieldSpawner] 최대 개체 수에 도달해 생성을 건너뜁니다. active={aliveEnzymes.Count}, max={maxAliveCount}"
                    );
                }

                return;
            }

            Vector2 spawnPosition = GetSpawnPosition();

            DigestiveEnzymeMonster enzyme = Instantiate(
                digestiveEnzymePrefab,
                spawnPosition,
                Quaternion.identity
            );

            enzyme.SetupSpawner(this);
            aliveEnzymes.Add(enzyme);

            if (debugLog)
            {
                Debug.Log(
                    $"[DigestiveEnzymeFieldSpawner] 소화효소 몬스터 생성. position={spawnPosition}, active={aliveEnzymes.Count}/{maxAliveCount}"
                );
            }
        }

        private Vector2 GetSpawnPosition()
        {
            Vector2 playerPosition = playerCharacter.transform.position;

            for (int i = 0; i < 12; i++)
            {
                Vector2 direction = GetSpawnDirection();
                float distance = spawnDistanceFromPlayer + Random.Range(
                    -spawnDistanceJitter,
                    spawnDistanceJitter
                );

                distance = Mathf.Max(0.1f, distance);

                Vector2 candidate = playerPosition + direction * distance;

                if (avoidSpawnRadius <= 0f)
                {
                    return candidate;
                }

                if (avoidSpawnLayer.value == 0)
                {
                    return candidate;
                }

                Collider2D overlap = Physics2D.OverlapCircle(
                    candidate,
                    avoidSpawnRadius,
                    avoidSpawnLayer
                );

                if (overlap == null)
                {
                    return candidate;
                }
            }

            return playerPosition + GetSpawnDirection() * Mathf.Max(0.1f, spawnDistanceFromPlayer);
        }

        private Vector2 GetSpawnDirection()
        {
            if (randomDirectionSpawn)
            {
                Vector2 randomDirection = Random.insideUnitCircle.normalized;

                if (randomDirection.sqrMagnitude < 0.01f)
                {
                    randomDirection = Vector2.right;
                }

                return randomDirection;
            }

            if (fixedSpawnDirection.sqrMagnitude < 0.01f)
            {
                return Vector2.right;
            }

            return fixedSpawnDirection.normalized;
        }

        private void ResolveReferences()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            if (playerCharacter == null && levelManager != null)
            {
                playerCharacter = levelManager.PlayerCharacter;
            }

            if (playerCharacter == null)
            {
                playerCharacter = FindObjectOfType<Character>();
            }
        }

        private void CleanupAliveList()
        {
            for (int i = aliveEnzymes.Count - 1; i >= 0; i--)
            {
                DigestiveEnzymeMonster enzyme = aliveEnzymes[i];

                if (enzyme == null || !enzyme.IsAlive)
                {
                    aliveEnzymes.RemoveAt(i);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (playerCharacter == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(playerCharacter.transform.position, spawnDistanceFromPlayer);
        }
    }
}