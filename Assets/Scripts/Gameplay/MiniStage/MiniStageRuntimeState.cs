using UnityEngine;

namespace Vampire
{
    public static class MiniStageRuntimeState
    {
        public static bool IsInsideMiniStage { get; private set; }
        public static MiniStageDirector CurrentDirector { get; private set; }

        public static void EnterMiniStage(MiniStageDirector director)
        {
            IsInsideMiniStage = true;
            CurrentDirector = director;

            Debug.Log("[MiniStageRuntimeState] 미니 스테이지 상태 진입.");
        }

        public static void ExitMiniStage(MiniStageDirector director)
        {
            if (CurrentDirector != null && CurrentDirector != director)
            {
                Debug.LogWarning("[MiniStageRuntimeState] 다른 MiniStageDirector가 Exit을 요청했습니다. 안전을 위해 상태를 해제합니다.");
            }

            IsInsideMiniStage = false;
            CurrentDirector = null;

            Debug.Log("[MiniStageRuntimeState] 미니 스테이지 상태 해제.");
        }

        public static void ForceClear()
        {
            IsInsideMiniStage = false;
            CurrentDirector = null;

            Debug.Log("[MiniStageRuntimeState] 미니 스테이지 상태 강제 해제.");
        }
    }
}