using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 크리피커피 파츠 보스의 최종 사망을 감지해
    /// 기존 Boss Clear Reward Prefab을 3개 원형 배치로 생성합니다.
    ///
    /// 기존 Boss_Real 또는 BossMonster 보상 코드에는 의존하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreepyCoffeeChestDropAdapter : MonoBehaviour
    {
        private const int ChestCount = 3;
        private const float AngleStepDegrees = 120f;

        [Header("Boss Death Source")]
        [Tooltip(
            "크리피커피의 전체 사망 상태를 읽을 RootController입니다. " +
            "비워 두면 같은 GameObject에서 자동으로 찾습니다.")]
        [SerializeField]
        private BossPartDamageTestRootController bossRootController;

        [Header("Chest Drop")]
        [Tooltip(
            "보스 사망 시 그대로 생성할 기존 Boss Chest Prefab입니다. " +
            "현재 프로젝트에서는 Boss_ClearReward_test Prefab을 연결하면 됩니다.")]
        [SerializeField]
        private GameObject chestPrefab;

        [Tooltip(
            "보스 중심에서 각 Chest를 배치할 반지름입니다. " +
            "3개는 0도, 120도, 240도 위치에 생성됩니다.")]
        [SerializeField, Min(0f)]
        private float dropRadius = 1.5f;

        [Header("Debug")]
        [Tooltip(
            "체크하면 Chest 생성 완료 로그를 Console에 출력합니다. " +
            "필수 참조가 없을 때의 오류는 이 옵션과 관계없이 한 번만 출력됩니다.")]
        [SerializeField]
        private bool debugLog;

        private bool hasDropped;
        private bool missingRootLogged;
        private bool missingPrefabLogged;

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
            if (hasDropped)
            {
                return;
            }

            if (!ResolveBossRootController())
            {
                return;
            }

            if (!bossRootController.IsBossDead)
            {
                return;
            }

            DropChestsOnce(
                bossRootController.transform.position);
        }

        /// <summary>
        /// 지정된 중심을 기준으로 기존 Chest Prefab을 정확히 3개 생성합니다.
        /// 동일 어댑터 인스턴스에서는 한 번만 실행됩니다.
        /// </summary>
        private void DropChestsOnce(
            Vector3 center)
        {
            if (hasDropped)
            {
                return;
            }

            if (chestPrefab == null)
            {
                if (!missingPrefabLogged)
                {
                    Debug.LogError(
                        "[CreepyCoffeeChestDropAdapter] " +
                        "Chest Prefab이 비어 있어 보상을 생성할 수 없습니다.",
                        this);

                    missingPrefabLogged = true;
                }

                return;
            }

            // 중복 사망 전달이나 여러 프레임 감지에도
            // 정확히 한 번만 생성되도록 Instantiate 전에 잠급니다.
            hasDropped = true;

            float radius =
                Mathf.Max(
                    0f,
                    dropRadius);

            for (int i = 0;
                 i < ChestCount;
                 i++)
            {
                float angleRadians =
                    i *
                    AngleStepDegrees *
                    Mathf.Deg2Rad;

                Vector3 offset =
                    new Vector3(
                        Mathf.Cos(angleRadians),
                        Mathf.Sin(angleRadians),
                        0f) *
                    radius;

                Instantiate(
                    chestPrefab,
                    center + offset,
                    Quaternion.identity);
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[CreepyCoffeeChestDropAdapter] " +
                    $"보스 사망 Chest 드랍 완료 | " +
                    $"Count={ChestCount}, " +
                    $"Radius={radius:0.##}, " +
                    $"Center={center}",
                    this);
            }
        }

        /// <summary>
        /// Inspector 참조가 비어 있을 때 같은 GameObject에서
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

            if (bossRootController != null)
            {
                return true;
            }

            if (!missingRootLogged)
            {
                Debug.LogError(
                    "[CreepyCoffeeChestDropAdapter] " +
                    "같은 GameObject에서 " +
                    "BossPartDamageTestRootController를 찾지 못했습니다.",
                    this);

                missingRootLogged = true;
            }

            return false;
        }
    }
}
