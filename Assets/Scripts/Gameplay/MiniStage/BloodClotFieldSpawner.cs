using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class BloodClotFieldSpawner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("현재 스테이지의 LevelManager입니다. 플레이어 위치와 런 흐름 상태를 확인하기 위해 사용합니다.")]
        [SerializeField] private LevelManager levelManager;

        [Tooltip("몬스터를 스폰하는 EntityManager입니다.")]
        [SerializeField] private EntityManager entityManager;

        [Tooltip("혈전 파괴 후 진입할 미니 스테이지 관리자입니다.")]
        [SerializeField] private MiniStageDirector miniStageDirector;

        [Header("Blood Clot Monster")]
        [Tooltip("혈전 몬스터가 들어있는 Monster Pool Index입니다. LevelBlueprint의 Monsters 배열에 추가한 혈전 프리팹의 Element 번호를 입력합니다.")]
        [SerializeField] private int bloodClotMonsterPoolIndex = 0;

        [Tooltip("필드에 생성할 혈전 MonsterBlueprint입니다.")]
        [SerializeField] private MonsterBlueprint bloodClotBlueprint;

        [Tooltip("혈전의 추가 체력입니다. 0이면 MonsterBlueprint의 기본 체력만 사용합니다.")]
        [SerializeField] private float bloodClotHpBuff = 0f;

        [Header("Spawn Timing")]
        [Tooltip("게임 시작 후 첫 혈전이 생성되기까지 걸리는 시간입니다. 테스트할 때는 3~5초로 줄이면 됩니다.")]
        [SerializeField] private float firstSpawnDelay = 60f;

        [Tooltip("첫 생성 이후 몇 초마다 혈전을 생성할지 정합니다. 기본값 60이면 1분에 1개씩 생성됩니다.")]
        [SerializeField] private float spawnInterval = 60f;

        [Tooltip("미니 스테이지 진행 중 LevelManager가 멈춰 있을 때 혈전 생성 타이머도 멈출지 여부입니다. 보통 true 추천입니다.")]
        [SerializeField] private bool pauseTimerWhileRunFlowPaused = true;

        [Header("Spawn Position")]
        [Tooltip("플레이어로부터 어느 정도 떨어진 위치에 혈전을 생성할지 정합니다. 기본값 8을 추천합니다.")]
        [SerializeField] private float spawnDistanceFromPlayer = 8f;

        [Tooltip("생성 거리의 랜덤 오차입니다. 0이면 항상 정확히 Spawn Distance From Player 거리에서 생성됩니다.")]
        [SerializeField] private float spawnDistanceRandomOffset = 0f;

        [Tooltip("혈전을 플레이어 주변의 완전 랜덤 방향에 생성합니다. false면 오른쪽 방향에 고정 생성되어 테스트하기 쉽습니다.")]
        [SerializeField] private bool useRandomDirection = true;

        [Header("Active Limit")]
        [Tooltip("동시에 필드에 유지할 수 있는 혈전 최대 수입니다. 0 이하이면 제한 없이 1분마다 계속 생성됩니다.")]
        [SerializeField] private int maxActiveBloodClots = 0;

        [Tooltip("최대 수에 도달했을 때 새 혈전 생성을 건너뜁니다. Max Active Blood Clots가 0 이하이면 이 옵션은 의미 없습니다.")]
        [SerializeField] private bool skipSpawnWhenMaxActiveReached = true;

        [Header("Debug / Test")]
        [Tooltip("혈전 생성/사망 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        [Tooltip("테스트용 강제 스폰 키를 사용할지 여부입니다.")]
        [SerializeField] private bool enableForceSpawnKey = true;

        [Tooltip("테스트용 강제 스폰 키입니다. Play 중 이 키를 누르면 시간과 상관없이 혈전을 하나 생성합니다.")]
        [SerializeField] private KeyCode forceSpawnKey = KeyCode.F8;

        private readonly List<BloodClotMonster> activeBloodClots = new List<BloodClotMonster>();

        private float timer;
        private float nextSpawnTime;

        private void Awake()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            if (levelManager != null && entityManager == null)
            {
                entityManager = levelManager.EntityManager;
            }

            if (miniStageDirector == null)
            {
                miniStageDirector = FindObjectOfType<MiniStageDirector>();
            }

            timer = 0f;
            nextSpawnTime = Mathf.Max(0f, firstSpawnDelay);
        }

        private void Update()
        {
            if (enableForceSpawnKey && Input.GetKeyDown(forceSpawnKey))
            {
                SpawnBloodClot();
                return;
            }

            if (pauseTimerWhileRunFlowPaused &&
                levelManager != null &&
                levelManager.IsRunFlowPaused)
            {
                return;
            }

            if (!HasRequiredReferences())
            {
                return;
            }

            timer += Time.deltaTime;

            if (timer < nextSpawnTime)
            {
                return;
            }

            SpawnBloodClot();

            float safeInterval = Mathf.Max(0.1f, spawnInterval);
            nextSpawnTime += safeInterval;

            if (timer > nextSpawnTime + safeInterval)
            {
                nextSpawnTime = timer + safeInterval;
            }
        }

        private bool HasRequiredReferences()
        {
            if (levelManager == null)
            {
                return false;
            }

            if (entityManager == null)
            {
                return false;
            }

            if (miniStageDirector == null)
            {
                return false;
            }

            if (bloodClotBlueprint == null)
            {
                return false;
            }

            return true;
        }

        private void SpawnBloodClot()
        {
            if (!HasRequiredReferences())
            {
                Debug.LogWarning("[BloodClotFieldSpawner] 필수 참조가 비어 있어 혈전을 생성할 수 없습니다.");
                return;
            }

            CleanupNullActiveBloodClots();

            if (maxActiveBloodClots > 0 &&
                activeBloodClots.Count >= maxActiveBloodClots &&
                skipSpawnWhenMaxActiveReached)
            {
                if (debugLog)
                {
                    Debug.Log($"[BloodClotFieldSpawner] 활성 혈전 수가 최대치에 도달해 생성을 건너뜁니다. active={activeBloodClots.Count}, max={maxActiveBloodClots}");
                }

                return;
            }

            Character player = levelManager.PlayerCharacter;

            if (player == null)
            {
                Debug.LogWarning("[BloodClotFieldSpawner] PlayerCharacter를 찾을 수 없습니다.");
                return;
            }

            Vector2 spawnPosition = GetSpawnPosition(player.transform.position);

            Monster spawnedMonster = entityManager.SpawnMonster(
                bloodClotMonsterPoolIndex,
                spawnPosition,
                bloodClotBlueprint,
                bloodClotHpBuff
            );

            BloodClotMonster bloodClot = spawnedMonster as BloodClotMonster;

            if (bloodClot == null)
            {
                Debug.LogWarning("[BloodClotFieldSpawner] 생성된 몬스터가 BloodClotMonster가 아닙니다. 혈전 프리팹에 BloodClotMonster가 붙어 있는지 확인하세요.");
                return;
            }

            bloodClot.ConfigureMiniStage(
                miniStageDirector,
                BloodClotMiniStageRole.EnterMiniStageOnDeath
            );

            bloodClot.OnKilled.RemoveListener(OnBloodClotKilled);
            bloodClot.OnKilled.AddListener(OnBloodClotKilled);

            activeBloodClots.Add(bloodClot);

            if (debugLog)
            {
                Debug.Log($"[BloodClotFieldSpawner] 혈전 생성 완료. active={activeBloodClots.Count}, position={spawnPosition}, nextSpawnTime={nextSpawnTime:0.00}");
            }
        }

        private Vector2 GetSpawnPosition(Vector3 playerPosition)
        {
            Vector2 direction;

            if (useRandomDirection)
            {
                direction = Random.insideUnitCircle.normalized;

                if (direction == Vector2.zero)
                {
                    direction = Vector2.right;
                }
            }
            else
            {
                direction = Vector2.right;
            }

            float randomOffset = 0f;

            if (spawnDistanceRandomOffset > 0f)
            {
                randomOffset = Random.Range(-spawnDistanceRandomOffset, spawnDistanceRandomOffset);
            }

            float finalDistance = Mathf.Max(0.1f, spawnDistanceFromPlayer + randomOffset);

            return (Vector2)playerPosition + direction * finalDistance;
        }

        private void OnBloodClotKilled(Monster killedMonster)
        {
            BloodClotMonster bloodClot = killedMonster as BloodClotMonster;

            if (bloodClot != null)
            {
                bloodClot.OnKilled.RemoveListener(OnBloodClotKilled);
                activeBloodClots.Remove(bloodClot);
            }

            if (debugLog)
            {
                Debug.Log($"[BloodClotFieldSpawner] 필드 혈전 파괴됨. active={activeBloodClots.Count}");
            }
        }

        private void CleanupNullActiveBloodClots()
        {
            for (int i = activeBloodClots.Count - 1; i >= 0; i--)
            {
                if (activeBloodClots[i] == null)
                {
                    activeBloodClots.RemoveAt(i);
                }
            }
        }

        private void OnDisable()
        {
            for (int i = activeBloodClots.Count - 1; i >= 0; i--)
            {
                BloodClotMonster bloodClot = activeBloodClots[i];

                if (bloodClot != null)
                {
                    bloodClot.OnKilled.RemoveListener(OnBloodClotKilled);
                }
            }

            activeBloodClots.Clear();
        }
    }
}