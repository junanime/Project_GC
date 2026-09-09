using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire
{
    /// <summary>
    /// 크리피커피 Exit Portal에서 사용하는
    /// 실제 Scene -> Scene 런 상태 전달 테스트 서비스입니다.
    ///
    /// Player GameObject를 DontDestroyOnLoad 하지 않습니다.
    /// 현재 값을 CrossSceneData에 저장한 뒤
    /// SceneManager.LoadScene으로 실제 새 Scene을 로드합니다.
    /// </summary>
    public static class RunSceneTransferTestService
    {
        public static bool CaptureCurrentRunAndLoad(
            string targetSceneName)
        {
            if (string.IsNullOrWhiteSpace(
                    targetSceneName))
            {
                Debug.LogError(
                    "[RunSceneTransfer] " +
                    "Target Scene Name이 비어 있습니다.");

                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(
                    targetSceneName))
            {
                Debug.LogError(
                    $"[RunSceneTransfer] " +
                    $"Build Settings에서 Scene을 로드할 수 없습니다. " +
                    $"Target={targetSceneName}");

                return false;
            }

            Character player =
                Object.FindObjectOfType<Character>();

            AbilityManager abilityManager =
                Object.FindObjectOfType<AbilityManager>();

            StatsManager statsManager =
                Object.FindObjectOfType<StatsManager>();

            if (player == null)
            {
                Debug.LogError(
                    "[RunSceneTransfer] " +
                    "현재 Character를 찾지 못했습니다.");

                return false;
            }

            if (abilityManager == null)
            {
                Debug.LogError(
                    "[RunSceneTransfer] " +
                    "현재 AbilityManager를 찾지 못했습니다.");

                return false;
            }

            if (statsManager == null)
            {
                Debug.LogError(
                    "[RunSceneTransfer] " +
                    "현재 StatsManager를 찾지 못했습니다.");

                return false;
            }

            Scene sourceScene =
                SceneManager.GetActiveScene();

            RunSceneTransferSnapshot snapshot =
                new RunSceneTransferSnapshot
                {
                    SourceSceneName =
                        sourceScene.name,

                    TargetSceneName =
                        targetSceneName,

                    SourceSceneHandle =
                        sourceScene.handle,

                    SourceCharacterInstanceId =
                        player.GetInstanceID(),

                    CharacterState =
                        player.CaptureRunSceneState(),

                    AbilityStates =
                        abilityManager
                            .CaptureRunSceneAbilities(),

                    CoinsGained =
                        statsManager.CoinsGained
                };

            CrossSceneData.PendingRunSceneTransfer =
                snapshot;

            Debug.Log(
                $"[RunSceneTransfer] ===== CAPTURE =====\n" +
                $"SourceScene={snapshot.SourceSceneName}\n" +
                $"SourceSceneHandle={snapshot.SourceSceneHandle}\n" +
                $"SourceCharacterID={snapshot.SourceCharacterInstanceId}\n" +
                $"TargetScene={snapshot.TargetSceneName}\n" +
                $"Level={snapshot.CharacterState.CurrentLevel}\n" +
                $"HP={snapshot.CharacterState.CurrentHealth:0.##}\n" +
                $"Coins={snapshot.CoinsGained}\n" +
                $"Abilities={snapshot.AbilityStates.Count}");

            // 증강창/일시정지 등의 영향으로 0이 되어 있어도
            // 씬 로드 전에 정상화합니다.
            Time.timeScale =
                1f;

            SceneManager.LoadScene(
                targetSceneName,
                LoadSceneMode.Single);

            return true;
        }

        public static bool RestorePendingRun()
        {
            RunSceneTransferSnapshot snapshot =
                CrossSceneData.PendingRunSceneTransfer;

            if (snapshot == null)
            {
                return false;
            }

            Scene currentScene =
                SceneManager.GetActiveScene();

            if (!string.Equals(
                    currentScene.name,
                    snapshot.TargetSceneName))
            {
                Debug.LogWarning(
                    $"[RunSceneTransfer] " +
                    $"아직 목표 Scene이 아닙니다. " +
                    $"Current={currentScene.name}, " +
                    $"Target={snapshot.TargetSceneName}");

                return false;
            }

            Character player =
                Object.FindObjectOfType<Character>();

            AbilityManager abilityManager =
                Object.FindObjectOfType<AbilityManager>();

            StatsManager statsManager =
                Object.FindObjectOfType<StatsManager>();

            if (player == null ||
                abilityManager == null ||
                statsManager == null)
            {
                return false;
            }

            int restoredAbilityCount =
                abilityManager.RestoreRunSceneAbilities(
                    snapshot.AbilityStates);

            // Ability 재적용 과정에서 Character 능력치가 변할 수 있으므로
            // 정확한 최종 Character 값은 그 다음에 덮어씁니다.
            player.RestoreRunSceneState(
                snapshot.CharacterState);

            // 새 StatsManager는 0부터 시작하므로
            // 이전 Run Coins를 그대로 더해 줍니다.
            if (snapshot.CoinsGained > 0)
            {
                statsManager.IncreaseCoinsGained(
                    snapshot.CoinsGained);
            }

            int newCharacterId =
                player.GetInstanceID();

            Debug.Log(
                $"[RunSceneTransfer] ===== RESTORE SUCCESS =====\n" +
                $"Scene={snapshot.SourceSceneName} -> {currentScene.name}\n" +
                $"SceneHandle={snapshot.SourceSceneHandle} -> {currentScene.handle}\n" +
                $"CharacterID={snapshot.SourceCharacterInstanceId} -> {newCharacterId}\n" +
                $"Level={player.CurrentLevel}\n" +
                $"HP={player.CurrentHealth:0.##}/{player.MaxHealth:0.##}\n" +
                $"Coins={statsManager.CoinsGained}\n" +
                $"Abilities={restoredAbilityCount}/{snapshot.AbilityStates.Count}");

            CrossSceneData.ClearPendingRunSceneTransfer();

            return true;
        }
    }
}