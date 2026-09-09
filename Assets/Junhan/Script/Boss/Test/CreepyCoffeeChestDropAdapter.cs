using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 크리피커피 파츠 보스의 최종 사망을 감지해
    /// 프로젝트의 기존 Boss Chest 시스템으로 보상 상자 3개를 생성합니다.
    ///
    /// Boss_Real / BossMonster의 사망 보상 코드를 직접 호출하지 않고,
    /// EntityManager.SpawnChest(ChestBlueprint, Vector2)를 통해
    /// 기존 Chest Pool / Chest / AbilitySelectionDialog 흐름을 그대로 사용합니다.
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

        [Header("Existing Chest System")]
        [Tooltip(
            "현재 레벨의 EntityManager입니다. " +
            "비워 두면 씬에서 자동으로 찾습니다.")]
        [SerializeField]
        private EntityManager entityManager;

        [Tooltip(
            "기존 보스가 사용하는 ChestBlueprint입니다. " +
            "현재 프로젝트에서는 Assets/Blueprints/Chests/Boss Chest.asset을 연결하세요.")]
        [SerializeField]
        private ChestBlueprint bossChestBlueprint;

        [Header("Drop Layout")]
        [Tooltip(
            "보스 중심에서 각 Chest를 배치할 반지름입니다. " +
            "3개는 0도, 120도, 240도 위치에 생성됩니다.")]
        [SerializeField, Min(0f)]
        private float dropRadius = 1.5f;

        [Header("Debug")]
        [Tooltip(
            "체크하면 보스 사망 감지 및 Chest 생성 완료 로그를 출력합니다. " +
            "필수 참조가 없을 때의 오류는 이 옵션과 관계없이 한 번만 출력됩니다.")]
        [SerializeField]
        private bool debugLog;

        private bool hasDropped;
        private bool missingRootLogged;
        private bool missingEntityManagerLogged;
        private bool missingBlueprintLogged;

        private void Reset()
        {
            bossRootController =
                GetComponent<BossPartDamageTestRootController>();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (hasDropped)
            {
                return;
            }

            ResolveReferences();

            if (bossRootController == null)
            {
                LogMissingRootOnce();
                return;
            }

            if (!bossRootController.IsBossDead)
            {
                return;
            }

            if (entityManager == null)
            {
                LogMissingEntityManagerOnce();
                return;
            }

            if (bossChestBlueprint == null)
            {
                LogMissingBlueprintOnce();
                return;
            }

            DropBossChestsOnce(
                bossRootController.transform.position);
        }

        /// <summary>
        /// 지정된 중심을 기준으로 기존 Boss Chest를 정확히 3개 생성합니다.
        /// 동일 어댑터 인스턴스에서는 한 번만 실행됩니다.
        /// </summary>
        private void DropBossChestsOnce(
            Vector3 center)
        {
            if (hasDropped)
            {
                return;
            }

            if (entityManager == null ||
                bossChestBlueprint == null)
            {
                return;
            }

            // 같은 사망 상태를 여러 프레임 감지해도
            // 정확히 한 번만 생성되도록 SpawnChest 호출 전에 잠급니다.
            hasDropped = true;

            float radius =
                Mathf.Max(
                    0f,
                    dropRadius);

            Vector2 center2D =
                new Vector2(
                    center.x,
                    center.y);

            for (int i = 0;
                 i < ChestCount;
                 i++)
            {
                float angleRadians =
                    i *
                    AngleStepDegrees *
                    Mathf.Deg2Rad;

                Vector2 offset =
                    new Vector2(
                        Mathf.Cos(angleRadians),
                        Mathf.Sin(angleRadians)) *
                    radius;

                entityManager.SpawnChest(
                    bossChestBlueprint,
                    center2D + offset);
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[CreepyCoffeeChestDropAdapter] " +
                    $"기존 Boss Chest 드랍 완료 | " +
                    $"Count={ChestCount}, " +
                    $"Radius={radius:0.##}, " +
                    $"Center={center2D}, " +
                    $"Blueprint={bossChestBlueprint.name}",
                    this);
            }
        }

        /// <summary>
        /// Inspector 참조가 비어 있는 경우
        /// 현재 프로젝트의 실제 컴포넌트를 자동 탐색합니다.
        /// </summary>
        private void ResolveReferences()
        {
            if (bossRootController == null)
            {
                bossRootController =
                    GetComponent<BossPartDamageTestRootController>();
            }

            if (entityManager == null)
            {
                entityManager =
                    FindObjectOfType<EntityManager>();
            }
        }

        private void LogMissingRootOnce()
        {
            if (missingRootLogged)
            {
                return;
            }

            missingRootLogged = true;

            Debug.LogError(
                "[CreepyCoffeeChestDropAdapter] " +
                "BossPartDamageTestRootController를 찾지 못했습니다. " +
                "이 어댑터를 RootController와 같은 GameObject에 붙이거나 " +
                "Inspector에서 직접 연결하세요.",
                this);
        }

        private void LogMissingEntityManagerOnce()
        {
            if (missingEntityManagerLogged)
            {
                return;
            }

            missingEntityManagerLogged = true;

            Debug.LogError(
                "[CreepyCoffeeChestDropAdapter] " +
                "현재 씬에서 EntityManager를 찾지 못해 Boss Chest를 생성할 수 없습니다.",
                this);
        }

        private void LogMissingBlueprintOnce()
        {
            if (missingBlueprintLogged)
            {
                return;
            }

            missingBlueprintLogged = true;

            Debug.LogError(
                "[CreepyCoffeeChestDropAdapter] " +
                "Boss Chest Blueprint가 비어 있습니다. " +
                "Assets/Blueprints/Chests/Boss Chest.asset을 연결하세요.",
                this);
        }
    }
}
