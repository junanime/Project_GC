using System.Collections;
using UnityEngine;

namespace Vampire
{
    // Stage 2 ends the game. LevelManager commits success once before this flow starts.
    [DisallowMultipleComponent]
    public sealed class GameEndingFlow : MonoBehaviour
    {
        bool running;

        public static void Begin(LevelManager level)
        {
            if (level == null || !level.IsLevelEnded || level.GetComponent<StageProgression>()?.StageNumber != 2 ||
                level.GetComponent<ConstipationKingDirector>()?.Defeated != true || level.GetComponent<RunScore>()?.Data.success != true) return;
            var flow = level.GetComponent<GameEndingFlow>() ?? level.gameObject.AddComponent<GameEndingFlow>();
            if (flow.running) return;
            flow.running = true;
            level.SetRunFlowPaused(true);
            level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
            flow.StartCoroutine(flow.ReturnAfterEnding(level));
        }

        IEnumerator ReturnAfterEnding(LevelManager level)
        {
            // The king's existing 1.3-second defeat finishes before LevelPassed calls us.
            yield return PlayEndingAnimation(level);
            ApothecaryUI.ReturnToPreparation();
        }

        static IEnumerator PlayEndingAnimation(LevelManager level)
        {
            // Future ending animation belongs here. Use unscaled time while combat is
            // stopped, and yield until the animation is complete before returning below.
            // Until that asset is ready there is no extra result screen or user input.
            yield return null;
        }
    }
}
