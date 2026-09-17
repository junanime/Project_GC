using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 크리피커피 파츠 보스의 최종 사망을 감지해
    /// 보스 사망 위치에 Exit Portal Prefab을 정확히 1개 생성합니다.
    ///
    /// 보상 Chest 드랍과는 독립적으로 동작합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreepyCoffeeExitPortalSpawner : MonoBehaviour
    {
        [Header("Boss Death Source")]
        [Tooltip(
            "크리피커피의 전체 사망 상태를 읽을 RootController입니다. " +
            "비워 두면 같은 GameObject에서 자동으로 찾습니다.")]
        [SerializeField]
        private BossPartDamageTestRootController bossRootController;

        [Header("Exit Portal")]
        [Tooltip(
            "보스 사망 위치에 생성할 Exit Portal Prefab입니다. " +
            "ExitPortalInteractable과 Trigger Collider2D가 설정된 Prefab을 연결하세요.")]
        [SerializeField]
        private GameObject exitPortalPrefab;

        [Tooltip(
            "보스 사망 위치에서 Portal 생성 위치를 미세 조정할 오프셋입니다. " +
            "첫 테스트에서는 (0, 0, 0)을 권장합니다.")]
        [SerializeField]
        private Vector3 spawnOffset = Vector3.zero;

        [Header("Debug")]
        [Tooltip(
            "체크하면 Exit Portal 생성 완료 로그를 Console에 출력합니다. " +
            "필수 참조 누락 오류는 이 옵션과 관계없이 한 번만 출력됩니다.")]
        [SerializeField]
        private bool debugLog = true;

        private bool hasSpawned;
        private bool missingRootLogged;
        private bool missingPrefabLogged;

        public bool HasSpawned => hasSpawned;

        private void Reset()
        {
            bossRootController =
                GetComponent<BossPartDamageTestRootController>();
        }

        private void Awake()
        {
            ResolveBossRootController();
        }

        private void Update()
        {
            if (hasSpawned)
            {
                return;
            }

            if (!ResolveBossRootController())
            {
                LogMissingRootOnce();
                return;
            }

            if (!bossRootController.IsBossDead)
            {
                return;
            }

            if (exitPortalPrefab == null)
            {
                LogMissingPrefabOnce();
                return;
            }

            SpawnExitPortalOnce(
                bossRootController.transform.position);
        }

        /// <summary>
        /// 지정된 보스 사망 위치에 Exit Portal을 정확히 한 번 생성합니다.
        /// </summary>
        private void SpawnExitPortalOnce(
            Vector3 bossDeathPosition)
        {
            if (hasSpawned ||
                exitPortalPrefab == null)
            {
                return;
            }

            // 같은 사망 상태를 여러 프레임 감지해도
            // 중복 생성되지 않도록 Instantiate 전에 잠급니다.
            hasSpawned = true;

            Vector3 spawnPosition =
                bossDeathPosition +
                spawnOffset;

            GameObject spawnedPortal =
                Instantiate(
                    exitPortalPrefab,
                    spawnPosition,
                    Quaternion.identity);

            if (spawnedPortal != null &&
                spawnedPortal.GetComponent<ExitPortalInteractable>() == null)
            {
                Debug.LogWarning(
                    "[CreepyCoffeeExitPortalSpawner] " +
                    "생성된 Exit Portal Prefab 루트에 " +
                    "ExitPortalInteractable이 없습니다. " +
                    "Portal은 생성됐지만 E 상호작용은 작동하지 않습니다.",
                    spawnedPortal);
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[CreepyCoffeeExitPortalSpawner] " +
                    $"Exit Portal 생성 완료 | " +
                    $"Position={spawnPosition}, " +
                    $"Prefab={exitPortalPrefab.name}",
                    this);
            }
        }

        /// <summary>
        /// Inspector 참조가 비어 있으면 같은 GameObject에서
        /// BossPartDamageTestRootController를 자동으로 찾습니다.
        /// </summary>
        private bool ResolveBossRootController()
        {
            if (bossRootController != null)
            {
                return true;
            }

            bossRootController =
                GetComponent<BossPartDamageTestRootController>();

            return bossRootController != null;
        }

        private void LogMissingRootOnce()
        {
            if (missingRootLogged)
            {
                return;
            }

            missingRootLogged = true;

            Debug.LogError(
                "[CreepyCoffeeExitPortalSpawner] " +
                "BossPartDamageTestRootController를 찾지 못했습니다. " +
                "이 컴포넌트를 RootController와 같은 GameObject에 붙이거나 " +
                "Inspector에서 직접 연결하세요.",
                this);
        }

        private void LogMissingPrefabOnce()
        {
            if (missingPrefabLogged)
            {
                return;
            }

            missingPrefabLogged = true;

            Debug.LogError(
                "[CreepyCoffeeExitPortalSpawner] " +
                "Exit Portal Prefab이 비어 있어 생성할 수 없습니다.",
                this);
        }
    }
}
