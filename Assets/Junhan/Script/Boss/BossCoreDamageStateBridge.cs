using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// BossCoreGuardController의 물리적인 Core 노출 상태와
    /// BossPartDamageRules의 실제 피해 상태를 연결합니다.
    ///
    /// 역할:
    /// - 양팔 생존: Normal
    /// - 한쪽 팔 파괴 + 살아남은 팔이 Core Guard: Guarded
    /// - Guard 팔 담당 패턴 사용: Open
    /// - 양팔 파괴: Open
    /// - Groggy 강제 상태: Groggy
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossCoreDamageStateBridge : MonoBehaviour
    {
        [Header("References")]

        [Tooltip(
            "팔의 Core Guard/Open 상태를 관리하는 BossCoreGuardController입니다. " +
            "비워 두면 현재 오브젝트, 부모, 최상위 루트의 자식에서 자동 탐색합니다."
        )]
        [SerializeField]
        private BossCoreGuardController coreGuardController;

        [Tooltip(
            "Core 피해 배율을 실제 파츠 피해에 적용하는 BossPartDamageRules입니다. " +
            "BossPartDamageTestRoot 최상위 오브젝트에 붙이는 것을 권장합니다."
        )]
        [SerializeField]
        private BossPartDamageRules damageRules;

        [Header("Runtime - Read Only")]

        [Tooltip(
            "현재 외부 기믹에 의해 Groggy 상태가 강제되어 있는지 표시합니다."
        )]
        [SerializeField]
        private bool groggyOverride;

        [Tooltip("마지막으로 BossPartDamageRules에 적용한 Core 피해 상태입니다.")]
        [SerializeField]
        private BossCoreDamageState lastAppliedState =
            BossCoreDamageState.Normal;

        [Tooltip("Core Guard 이벤트를 정상적으로 구독 중인지 표시합니다.")]
        [SerializeField]
        private bool subscribed;

        [Header("Debug")]

        [Tooltip(
            "Core Guard/Open/Groggy 상태가 BossPartDamageRules로 전달될 때 " +
            "Console 로그를 출력합니다."
        )]
        [SerializeField]
        private bool debugLog = true;

        public bool IsGroggy => groggyOverride;
        public BossCoreDamageState CurrentDamageState => lastAppliedState;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            SynchronizeDamageState(true);
        }

        private void Start()
        {
            ResolveReferences();
            Subscribe();
            SynchronizeDamageState(true);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        public void SetGroggy(bool groggy)
        {
            if (groggyOverride == groggy)
            {
                SynchronizeDamageState(false);
                return;
            }

            groggyOverride = groggy;

            ResolveReferences();

            if (coreGuardController != null)
            {
                coreGuardController.SetExternalCoreOpen(groggy);
            }

            SynchronizeDamageState(true);
        }

        public void ForceSynchronize()
        {
            ResolveReferences();
            Subscribe();
            SynchronizeDamageState(true);
        }

        [ContextMenu("Force Synchronize Core Damage State")]
        private void ForceSynchronizeContextMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[BossCoreDamageBridge] Play Mode에서 실행하세요.",
                    this
                );

                return;
            }

            ForceSynchronize();
        }

        [ContextMenu("Debug/Groggy ON")]
        private void DebugGroggyOn()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[BossCoreDamageBridge] Play Mode에서 실행하세요.",
                    this
                );

                return;
            }

            SetGroggy(true);
        }

        [ContextMenu("Debug/Groggy OFF")]
        private void DebugGroggyOff()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[BossCoreDamageBridge] Play Mode에서 실행하세요.",
                    this
                );

                return;
            }

            SetGroggy(false);
        }

        private void HandleCoreExposureChanged(
            bool isOpen,
            float damageMultiplier)
        {
            SynchronizeDamageState(false);
        }

        private void SynchronizeDamageState(bool forceLog)
        {
            ResolveReferences();

            if (damageRules == null)
            {
                if (debugLog && forceLog)
                {
                    Debug.LogWarning(
                        "[BossCoreDamageBridge] BossPartDamageRules를 찾지 못했습니다.",
                        this
                    );
                }

                return;
            }

            BossCoreDamageState targetState =
                DetermineTargetDamageState();

            bool changed =
                targetState != lastAppliedState ||
                damageRules.CoreDamageState != targetState;

            lastAppliedState = targetState;

            damageRules.SetCoreDamageState(
                targetState
            );

            if (debugLog &&
                (changed || forceLog))
            {
                Debug.Log(
                    $"[BossCoreDamageBridge] Core Damage State 동기화 | " +
                    $"State={targetState}, " +
                    $"Multiplier=x{damageRules.CurrentCoreDamageMultiplier:0.##}, " +
                    $"Guarded={(coreGuardController != null && coreGuardController.IsCoreGuarded)}, " +
                    $"Open={(coreGuardController != null && coreGuardController.IsCoreOpen)}, " +
                    $"Groggy={groggyOverride}",
                    this
                );
            }
        }

        private BossCoreDamageState DetermineTargetDamageState()
        {
            if (groggyOverride)
            {
                return BossCoreDamageState.Groggy;
            }

            if (coreGuardController == null)
            {
                return BossCoreDamageState.Normal;
            }

            if (coreGuardController.IsCoreOpen)
            {
                return BossCoreDamageState.Open;
            }

            if (coreGuardController.IsCoreGuarded)
            {
                return BossCoreDamageState.Guarded;
            }

            return BossCoreDamageState.Normal;
        }

        private void ResolveReferences()
        {
            if (coreGuardController == null)
            {
                coreGuardController =
                    GetComponent<BossCoreGuardController>();
            }

            if (coreGuardController == null)
            {
                coreGuardController =
                    GetComponentInParent<BossCoreGuardController>(true);
            }

            if (coreGuardController == null &&
                transform.root != null)
            {
                coreGuardController =
                    transform.root.GetComponentInChildren
                    <
                        BossCoreGuardController
                    >(true);
            }

            if (damageRules == null)
            {
                damageRules =
                    GetComponent<BossPartDamageRules>();
            }

            if (damageRules == null)
            {
                damageRules =
                    GetComponentInParent<BossPartDamageRules>(true);
            }

            if (damageRules == null &&
                transform.root != null)
            {
                damageRules =
                    transform.root.GetComponentInChildren
                    <
                        BossPartDamageRules
                    >(true);
            }
        }

        private void Subscribe()
        {
            if (subscribed ||
                coreGuardController == null)
            {
                return;
            }

            coreGuardController.CoreExposureChanged +=
                HandleCoreExposureChanged;

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (coreGuardController != null)
            {
                coreGuardController.CoreExposureChanged -=
                    HandleCoreExposureChanged;
            }

            subscribed = false;
        }
    }
}
