using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 플레이어 위치를 기준으로 테스트용 파츠 보스를 생성합니다.
    /// 정식 보스 스포너, EntityManager, BossMonsterBlueprint에는 연결하지 않습니다.
    /// </summary>
    public sealed class BossPartDamageTestSpawner : MonoBehaviour
    {
        [Header("테스트 프리팹")]
        [Tooltip("필드에 생성할 테스트용 파츠 보스 프리팹입니다.")]
        [SerializeField] private GameObject testBossPrefab;

        [Tooltip("스폰 위치의 기준이 되는 플레이어입니다. 비워 두면 씬에서 Character를 자동 탐색합니다.")]
        [SerializeField] private Character playerCharacter;

        [Header("스폰 위치")]
        [Tooltip("플레이어 중심에서 테스트 보스 루트까지의 거리입니다. 이번 테스트 기본값은 1입니다.")]
        [SerializeField, Min(0f)] private float spawnDistance = 1f;

        [Tooltip("활성화하면 플레이어가 바라보는 방향에 생성합니다.")]
        [SerializeField] private bool usePlayerLookDirection = true;

        [Tooltip("플레이어의 바라보는 방향이 0이거나 바라보는 방향을 사용하지 않을 때 적용할 방향입니다.")]
        [SerializeField] private Vector2 fallbackDirection = Vector2.right;

        [Header("실행")]
        [Tooltip("활성화하면 게임 시작 시 테스트 보스를 자동 생성합니다.")]
        [SerializeField] private bool spawnOnStart = true;

        [Tooltip("다시 생성할 때 이전에 생성된 테스트 보스를 제거합니다.")]
        [SerializeField] private bool destroyPreviousBeforeSpawn = true;

        [Tooltip("플레이 중 테스트 보스를 다시 생성하는 키입니다. 사용하지 않으려면 None으로 설정합니다.")]
        [SerializeField] private KeyCode respawnKey = KeyCode.F8;

        [Header("디버그")]
        [Tooltip("스폰 성공과 오류 내용을 Console에 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private GameObject spawnedBoss;

        private void Start()
        {
            if (spawnOnStart)
            {
                SpawnTestBoss();
            }
        }

        private void Update()
        {
            if (respawnKey != KeyCode.None &&
                Input.GetKeyDown(respawnKey))
            {
                SpawnTestBoss();
            }
        }

        [ContextMenu("Spawn Test Boss")]
        public void SpawnTestBoss()
        {
            if (testBossPrefab == null)
            {
                Debug.LogError(
                    "[BossPartTestSpawner] Test Boss Prefab이 비어 있습니다.",
                    this);

                return;
            }

            if (!TryResolvePlayer())
            {
                return;
            }

            Vector2 direction = GetSpawnDirection();

            Vector3 spawnPosition =
                playerCharacter.transform.position +
                (Vector3)(direction * spawnDistance);

            if (destroyPreviousBeforeSpawn &&
                spawnedBoss != null)
            {
                Destroy(spawnedBoss);
            }

            spawnedBoss = Instantiate(
                testBossPrefab,
                spawnPosition,
                Quaternion.identity);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartTestSpawner] 테스트 보스 생성 완료. " +
                    $"Player={playerCharacter.transform.position}, " +
                    $"Spawn={spawnPosition}, " +
                    $"Distance={spawnDistance}, " +
                    $"Direction={direction}",
                    spawnedBoss);
            }
        }

        private bool TryResolvePlayer()
        {
            if (playerCharacter != null)
            {
                return true;
            }

            playerCharacter = FindObjectOfType<Character>();

            if (playerCharacter != null)
            {
                return true;
            }

            Debug.LogError(
                "[BossPartTestSpawner] 씬에서 Character를 찾지 못했습니다. " +
                "Inspector의 Player Character에 플레이어를 직접 연결하세요.",
                this);

            return false;
        }

        private Vector2 GetSpawnDirection()
        {
            Vector2 direction = usePlayerLookDirection
                ? playerCharacter.LookDirection
                : fallbackDirection;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = fallbackDirection;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            return direction.normalized;
        }
    }
}