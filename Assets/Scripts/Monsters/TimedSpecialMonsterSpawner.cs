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
        List<TimedSpecialSpawnSchedule> initial_timedSpawns;
        List<DigestiveEnzymeFieldSpawner> initial_enzymeSpawns;
        List<EliteMonsterSpawner> initial_eliteSpawns;
        List<BloodClotFieldSpawner> initial_bloodClotSpawns;
        List<EliteSummonObjectSpawner> initial_summonObjectSpawns;
        List<MiniBossSpawner> initial_miniBossSpawns;
        List<BossLevelSpawner> initial_bossSpawns;
        protected override void Awake()
        {
            initial_timedSpawns = timedSpawns.ConvertAll(StageEventTemplate.Copy);
            initial_enzymeSpawns = enzymeSpawns.ConvertAll(StageEventTemplate.Copy);
            initial_eliteSpawns = eliteSpawns.ConvertAll(StageEventTemplate.Copy);
            initial_bloodClotSpawns = bloodClotSpawns.ConvertAll(StageEventTemplate.Copy);
            initial_summonObjectSpawns = summonObjectSpawns.ConvertAll(StageEventTemplate.Copy);
            initial_miniBossSpawns = miniBossSpawns.ConvertAll(StageEventTemplate.Copy);
            initial_bossSpawns = bossSpawns.ConvertAll(StageEventTemplate.Copy);
            base.Awake();
        }
        public void RestartForStage(LevelManager manager)
        {
            foreach (var module in Modules) module?.Dispose();
            timedSpawns = initial_timedSpawns.ConvertAll(StageEventTemplate.Copy);
            enzymeSpawns = initial_enzymeSpawns.ConvertAll(StageEventTemplate.Copy);
            eliteSpawns = initial_eliteSpawns.ConvertAll(StageEventTemplate.Copy);
            bloodClotSpawns = initial_bloodClotSpawns.ConvertAll(StageEventTemplate.Copy);
            summonObjectSpawns = initial_summonObjectSpawns.ConvertAll(StageEventTemplate.Copy);
            miniBossSpawns = initial_miniBossSpawns.ConvertAll(StageEventTemplate.Copy);
            bossSpawns = initial_bossSpawns.ConvertAll(StageEventTemplate.Copy);
            levelBossSchedule = new LevelBossSchedule();
            levelBossSchedule.InitializeLevel(manager);
            base.Awake();
        }
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
