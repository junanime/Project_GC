using UnityEngine;
using static UnityEngine.Object;

namespace Vampire
{
    [System.Serializable]
    public sealed class LevelBossSchedule : RuntimeModule
    {
        [Tooltip("LevelBlueprint에 지정된 미니보스와 최종보스 시간표를 사용합니다.")]
        public bool useLevelBlueprintSchedule = true;
        [SerializeField] private LevelManager levelManager;
        private bool miniBossSpawned, finalBossSpawned;

        public void InitializeLevel(LevelManager manager)
        {
            levelManager = manager;
            miniBossSpawned = finalBossSpawned = false;
        }
        public void NotifyFinalBossSpawned() { finalBossSpawned = true; }
        protected override void OnTick()
        {
            if (!useLevelBlueprintSchedule || levelManager == null || levelManager.IsRunFlowPaused ||
                levelManager.IsLevelEnded || MiniStageRuntimeState.IsInsideMiniStage) return;
            var data = levelManager.CurrentLevelBlueprint;
            var entity = levelManager.EntityManager;
            var player = levelManager.PlayerCharacter;
            if (data == null || entity == null || player == null) return;
            float time = levelManager.CurrentLevelTime;
            if (!miniBossSpawned && data.miniBosses != null && data.miniBosses.Length > 0 && time > data.miniBosses[0].spawnTime)
            {
                miniBossSpawned = true;
                entity.SpawnMonsterRandomPosition(data.monsters.Length, data.miniBosses[0].bossBlueprint);
                GameAudioManager.PlayBossAppearOnly();
            }
            if (!finalBossSpawned && !FinalBossSummonInteractable.IsSummoning && time >= data.levelTime &&
                FindObjectOfType<BossController>() == null && FindObjectOfType<BossMonster>() == null)
            {
                // Automatic expiry must go through the terminal's transmission and descent.
                var terminal = FindObjectOfType<FinalBossSummonInteractable>();
                if (terminal != null) terminal.TryAutomaticSummon();
                // Completion is acknowledged by NotifyFinalBossSpawned, so cancellation can retry.
            }
        }
    }
}
