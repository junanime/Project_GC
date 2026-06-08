using UnityEngine;

namespace Vampire
{
    public class BloodClotFieldSpawner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("현재 스테이지의 LevelManager입니다. 런 시간이 멈춰 있는 동안에는 혈전을 새로 스폰하지 않습니다.")]
        [SerializeField] private LevelManager levelManager;

        [Tooltip("몬스터를 스폰하는 EntityManager입니다.")]
        [SerializeField] private EntityManager entityManager;

        [Tooltip("혈전 파괴 후 진입할 미니 스테이지 관리자입니다.")]
        [SerializeField] private MiniStageDirector miniStageDirector;

        [Header("Blood Clot Spawn")]
        [Tooltip("혈전 몬스터가 들어있는 Monster Pool Index입니다. LevelBlueprint의 Monsters 배열에 추가한 혈전 프리팹의 인덱스를 입력합니다.")]
        [SerializeField] private int bloodClotMonsterPoolIndex = 0;

        [Tooltip("필드에 생성할 혈전 MonsterBlueprint입니다.")]
        [SerializeField] private MonsterBlueprint bloodClotBlueprint;

        [Tooltip("게임 시작 후 첫 혈전이 생성되기까지 걸리는 시간입니다.")]
        [SerializeField] private float firstSpawnTime = 45f;

        [Tooltip("혈전이 사라진 뒤 다음 혈전이 다시 생성되기까지 걸리는 시간입니다.")]
        [SerializeField] private float respawnDelay = 120f;

        [Tooltip("플레이어로부터 어느 정도 떨어진 위치에 혈전을 생성할지 정합니다.")]
        [SerializeField] private float spawnDistanceFromPlayer = 9f;

        [Tooltip("혈전의 추가 체력입니다. 0이면 MonsterBlueprint의 기본 체력만 사용합니다.")]
        [SerializeField] private float bloodClotHpBuff = 0f;

        [Tooltip("한 번에 혈전 1개만 유지할지 여부입니다.")]
        [SerializeField] private bool keepOnlyOneBloodClot = true;

        [Header("Debug")]
        [Tooltip("혈전 생성/사망 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private float timer = 0f;
        private float nextSpawnTime;
        private BloodClotMonster currentBloodClot;

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

            nextSpawnTime = firstSpawnTime;
        }

        private void Update()
        {
            if (levelManager != null && levelManager.IsRunFlowPaused)
            {
                return;
            }

            if (bloodClotBlueprint == null || entityManager == null || levelManager == null)
            {
                return;
            }

            if (keepOnlyOneBloodClot && currentBloodClot != null)
            {
                return;
            }

            timer += Time.deltaTime;

            if (timer >= nextSpawnTime)
            {
                SpawnBloodClot();
                nextSpawnTime = timer + respawnDelay;
            }
        }

        private void SpawnBloodClot()
        {
            Character player = levelManager.PlayerCharacter;

            if (player == null)
            {
                Debug.LogWarning("[BloodClotFieldSpawner] PlayerCharacter를 찾을 수 없습니다.");
                return;
            }

            Vector2 randomDirection = Random.insideUnitCircle.normalized;

            if (randomDirection == Vector2.zero)
            {
                randomDirection = Vector2.right;
            }

            Vector2 spawnPosition = (Vector2)player.transform.position + randomDirection * spawnDistanceFromPlayer;

            Monster spawnedMonster = entityManager.SpawnMonster(
                bloodClotMonsterPoolIndex,
                spawnPosition,
                bloodClotBlueprint,
                bloodClotHpBuff
            );

            currentBloodClot = spawnedMonster as BloodClotMonster;

            if (currentBloodClot == null)
            {
                Debug.LogWarning("[BloodClotFieldSpawner] 생성된 몬스터가 BloodClotMonster가 아닙니다. 혈전 프리팹에 BloodClotMonster가 붙어 있는지 확인하세요.");
                return;
            }

            currentBloodClot.ConfigureMiniStage(
                miniStageDirector,
                BloodClotMiniStageRole.EnterMiniStageOnDeath
            );

            currentBloodClot.OnKilled.AddListener(OnBloodClotKilled);

            if (debugLog)
            {
                Debug.Log($"[BloodClotFieldSpawner] 혈전 생성 완료. position={spawnPosition}");
            }
        }

        private void OnBloodClotKilled(Monster killedMonster)
        {
            if (debugLog)
            {
                Debug.Log("[BloodClotFieldSpawner] 필드 혈전 파괴됨.");
            }

            currentBloodClot = null;
        }
    }
}