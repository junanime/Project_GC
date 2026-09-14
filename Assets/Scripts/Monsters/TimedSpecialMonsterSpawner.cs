using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    [DisallowMultipleComponent]
    public class TimedSpecialMonsterSpawner : RuntimeModuleHost
    {
        [SerializeField] private LevelBossSchedule levelBossSchedule = new LevelBossSchedule();
        public void InitializeLevel(LevelManager manager) => levelBossSchedule.InitializeLevel(manager);
        public void NotifyFinalBossSpawned() => levelBossSchedule.NotifyFinalBossSpawned();
        [SerializeField] private List<TimedSpecialSpawnSchedule> timedSpawns = new List<TimedSpecialSpawnSchedule>();
        [SerializeField] private List<DigestiveEnzymeFieldSpawner> enzymeSpawns = new List<DigestiveEnzymeFieldSpawner>();
        [SerializeField] private List<EliteMonsterSpawner> eliteSpawns = new List<EliteMonsterSpawner>();
        [SerializeField] private List<BloodClotFieldSpawner> bloodClotSpawns = new List<BloodClotFieldSpawner>();
        [SerializeField] private List<EliteSummonObjectSpawner> summonObjectSpawns = new List<EliteSummonObjectSpawner>();
        [SerializeField] private List<MiniBossSpawner> miniBossSpawns = new List<MiniBossSpawner>();
        [SerializeField] private List<BossLevelSpawner> bossSpawns = new List<BossLevelSpawner>();
        protected override IEnumerable<RuntimeModule> Modules
        {
            get
            {
                if (timedSpawns != null) foreach (var module in timedSpawns) yield return module;
                if (enzymeSpawns != null) foreach (var module in enzymeSpawns) yield return module;
                if (eliteSpawns != null) foreach (var module in eliteSpawns) yield return module;
                if (bloodClotSpawns != null) foreach (var module in bloodClotSpawns) yield return module;
                if (summonObjectSpawns != null) foreach (var module in summonObjectSpawns) yield return module;
                if (miniBossSpawns != null) foreach (var module in miniBossSpawns) yield return module;
                if (bossSpawns != null) foreach (var module in bossSpawns) yield return module;
                yield return levelBossSchedule;
            }
        }
        public void DisableScheduledFinalBosses()
        {
            foreach (var entry in bossSpawns) if (entry != null) entry.enabled = false;
        }
        public void ResetSpawnerRuntime()
        {
            foreach (var entry in timedSpawns) if (entry != null) entry.ResetSpawnerRuntime();
        }
    }
}
