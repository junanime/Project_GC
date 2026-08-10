using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 테스트 파츠 보스의 사망 상태를
    /// 기존 BossController에 전달하기 위한 임시 Bridge입니다.
    ///
    /// Core가 파괴되어 BossPartDamageTestRootController가
    /// 사망 상태가 되면 BossController의 패턴 루프도 중지합니다.
    /// </summary>
    public sealed class BossPartPatternTestDeathBridge :
        MonoBehaviour
    {
        [Header("References")]

        [Tooltip(
            "파츠 HP와 Core 사망을 관리하는 테스트 RootController입니다. " +
            "비워 두면 같은 GameObject에서 자동 탐색합니다.")]
        [SerializeField]
        private BossPartDamageTestRootController partRootController;

        [Tooltip(
            "기존 보스 패턴 선택과 실행을 담당하는 BossController입니다. " +
            "비워 두면 같은 GameObject에서 자동 탐색합니다.")]
        [SerializeField]
        private BossController bossController;

        [Header("Settings")]

        [Tooltip(
            "Core 파괴 등으로 테스트 보스가 죽었을 때 " +
            "BossController의 패턴 루프를 중지합니다.")]
        [SerializeField]
        private bool stopBossControllerOnPartBossDeath = true;

        [Header("Debug")]

        [Tooltip(
            "테스트 보스 사망을 BossController에 전달할 때 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog = true;

        private bool deathForwarded;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            deathForwarded = false;

            ResolveReferences();
        }

        private void Update()
        {
            if (deathForwarded)
            {
                return;
            }

            if (!stopBossControllerOnPartBossDeath)
            {
                return;
            }

            ResolveReferences();

            if (partRootController == null ||
                bossController == null)
            {
                return;
            }

            if (!partRootController.IsBossDead)
            {
                return;
            }

            deathForwarded = true;

            if (!bossController.IsDead)
            {
                bossController.NotifyBossDeathStarted();
            }

            if (debugLog)
            {
                Debug.Log(
                    "[BossPartPatternTest] " +
                    "Core 사망 감지 → BossController 패턴 루프 정지",
                    this);
            }
        }

        private void ResolveReferences()
        {
            if (partRootController == null)
            {
                partRootController =
                    GetComponent
                    <
                        BossPartDamageTestRootController
                    >();
            }

            if (bossController == null)
            {
                bossController =
                    GetComponent
                    <
                        BossController
                    >();
            }
        }
    }
}