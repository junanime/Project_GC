using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 크리피커피 Exit Portal의 ExitRequested 이벤트를 받아
    /// 현재 Run 상태를 저장한 뒤 실제 Scene을 새로 로드하는 테스트용 브리지입니다.
    ///
    /// ExitPortalInteractable 자체는 재사용 가능하게 그대로 두고,
    /// Scene 전환 책임만 이 컴포넌트가 담당합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ExitPortalInteractable))]
    public sealed class CreepyCoffeeExitPortalSceneTransferTest : MonoBehaviour
    {

        [Header("Target Scene")]
        [Tooltip(
            "실제로 새로 로드할 목표 Scene 이름입니다. " +
            "Build Settings에 등록된 Scene 이름과 정확히 같아야 합니다. " +
            "현재 테스트 대상은 'Level 1'입니다.")]
        [SerializeField]
        private string targetSceneName = "Level 1";

        [Header("Debug")]
        [Tooltip(
            "체크하면 Exit Portal에서 Scene 전환을 요청한 시점을 " +
            "Console에 출력합니다.")]
        [SerializeField]
        private bool debugLog = true;

        private ExitPortalInteractable exitPortalInteractable;
        private bool transitionStarted;

        private void Awake()
        {
            exitPortalInteractable =
                GetComponent<ExitPortalInteractable>();
        }

        private void OnEnable()
        {
            if (exitPortalInteractable == null)
            {
                exitPortalInteractable =
                    GetComponent<ExitPortalInteractable>();
            }

            if (exitPortalInteractable != null)
            {
                exitPortalInteractable.ExitRequested +=
                    HandleExitRequested;
            }
        }

        private void OnDisable()
        {
            if (exitPortalInteractable != null)
            {
                exitPortalInteractable.ExitRequested -=
                    HandleExitRequested;
            }
        }

        private void HandleExitRequested(
            ExitPortalInteractable source)
        {
            if (transitionStarted)
            {
                return;
            }

            transitionStarted = true;

            if (source != null)
            {
                source.SetInteractionEnabled(false);
            }

            if (debugLog)
            {
                Debug.Log(
                    "[CreepyCoffeeExitPortalSceneTransferTest] " +
                    "Run 상태 Capture 후 실제 Scene 로드 요청 | " +
                    $"Target={targetSceneName}",
                    this);
            }

            bool started =
                RunSceneTransferTestService
                    .CaptureCurrentRunAndLoad(
                        targetSceneName);

            if (started)
            {
                return;
            }

            transitionStarted = false;

            if (source != null)
            {
                source.SetInteractionEnabled(true);
            }

            Debug.LogError(
                "[CreepyCoffeeExitPortalSceneTransferTest] " +
                "Scene 전환 시작에 실패했습니다. " +
                "Portal 상호작용을 다시 활성화했습니다.",
                this);
        }
    }
}