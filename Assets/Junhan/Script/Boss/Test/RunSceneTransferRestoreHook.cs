using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 새 Level 1 Scene이 로드된 뒤
    /// LevelManager / Character / AbilityManager 초기화를 기다렸다가
    /// 이전 Scene의 Run Snapshot을 복원합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunSceneTransferRestoreHook :
        MonoBehaviour
    {
        [Header("Restore Timing")]
        [Tooltip(
            "새 Scene 로드 직후 LevelManager.Start, Character.Init, " +
            "AbilityManager.Init 등이 끝날 시간을 확보하기 위해 " +
            "몇 프레임 기다릴지 정합니다. 첫 테스트는 4를 권장합니다.")]
        [SerializeField, Min(0)]
        private int waitFrames = 4;

        [Header("Debug")]
        [Tooltip(
            "체크하면 복원 대기/완료 로그를 Console에 출력합니다.")]
        [SerializeField]
        private bool debugLog = true;

        private IEnumerator Start()
        {
            if (!CrossSceneData.HasPendingRunSceneTransfer)
            {
                if (debugLog)
                {
                    Debug.Log(
                        "[RunSceneTransferRestoreHook] " +
                        "복원할 Run Snapshot이 없습니다. " +
                        "일반 Level 1 시작으로 처리합니다.",
                        this);
                }

                yield break;
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[RunSceneTransferRestoreHook] " +
                    $"Run Snapshot 감지. {waitFrames}프레임 후 복원합니다.",
                    this);
            }

            for (int i = 0;
                 i < waitFrames;
                 i++)
            {
                yield return null;
            }

            bool restored =
                RunSceneTransferTestService
                    .RestorePendingRun();

            if (!restored)
            {
                Debug.LogError(
                    "[RunSceneTransferRestoreHook] " +
                    "Run Snapshot 복원에 실패했습니다. " +
                    "Character / AbilityManager / StatsManager 초기화 상태와 " +
                    "Console 앞선 오류를 확인하세요.",
                    this);
            }
        }
    }
}