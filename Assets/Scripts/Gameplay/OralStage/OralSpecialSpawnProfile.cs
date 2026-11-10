using UnityEngine;

namespace Vampire
{
    /// <summary>Six-minute oral encounters, copied from (never written into) the stomach schedule.</summary>
    public static class OralSpecialSpawnProfile
    {
        public const float Duration = 360f;
        public const int MaxLivingTimedSpecials = 6;
        public enum Kind { None, Sniper, Popcorn, Spore, Sugar, Thief, Trap, Support, Debuffer, EliteSugar }

        public static Kind Identify(LevelBlueprint level, MonsterBlueprint blueprint)
        {
            if (blueprint == null) return Kind.None;
            if (blueprint is SniperMonsterBlueprint) return Kind.Sniper;
            if (blueprint is ExplodingMonsterBlueprint) return Kind.Spore;
            if (blueprint is TrapMonsterBlueprint) return Kind.Trap;
            if (blueprint is AttackSpeedDebufferMonsterBlueprint) return Kind.Debuffer;
            if (blueprint is SugarCubeMonsterBlueprint sugar)
                return sugar.splitStage == SugarCubeSplitStage.EliteBody ||
                    sugar.splitStage == SugarCubeSplitStage.EliteSplit1 || sugar.splitStage == SugarCubeSplitStage.EliteSplit2
                    ? Kind.EliteSugar : Kind.Sugar;
            if (level == null) return Kind.None;
            foreach (var group in level.monsters)
            {
                if (group.monstersPrefab == null || System.Array.IndexOf(group.monsterBlueprints, blueprint) < 0) continue;
                var prefab = group.monstersPrefab;
                if (prefab.GetComponent<MonsterSupportCaster>() != null) return Kind.Support;
                if (prefab.GetComponent<TreasureRunnerMonster>() != null) return Kind.Thief;
                if (prefab.GetComponent<NutritionThiefBacteriaMonster>() != null) return Kind.Popcorn;
            }
            return Kind.None;
        }

        public static TimedSpecialSpawnSchedule.TimedSpawnEntry Adapt(
            TimedSpecialSpawnSchedule.TimedSpawnEntry source, LevelBlueprint level)
        {
            if (source == null || source.monsterBlueprint == null) return null;
            Kind kind = Identify(level, source.monsterBlueprint);
            if (kind == Kind.None) return null; // An unclassified future encounter must be explicitly scheduled.
            var entry = StageEventTemplate.Copy(source);
            entry.autoResolvePoolIndex = true;
            entry.spawnTimeMode = TimedSpecialSpawnSchedule.SpawnTimeMode.ExactTime;
            entry.spawnTimeSeconds = FirstAppearance(kind);
            entry.randomStartTimeSeconds = entry.randomEndTimeSeconds = entry.spawnTimeSeconds;
            entry.randomEventCount = 1;
            entry.repeatAtRandomInterval = kind != Kind.EliteSugar && kind != Kind.Trap;
            entry.repeatUntilSeconds = Duration;
            entry.minRepeatInterval = kind == Kind.Popcorn || kind == Kind.Debuffer ? 110 : kind == Kind.Thief ? 120 : 90;
            entry.maxRepeatInterval = entry.minRepeatInterval + 20;
            entry.spawnCountMode = TimedSpecialSpawnSchedule.SpawnCountMode.FixedCount;
            entry.spawnCount = entry.randomMinSpawnCount = entry.randomMaxSpawnCount = kind == Kind.Support && entry.companionBlueprint != null ? 2 : 1;
            entry.lateExtraCountChance = 0;
            // A support pair consumes its two reserved slots in the same frame.
            entry.intervalBetweenSpawns = kind == Kind.Support ? 0 : .35f;
            entry.trapSpawnOnStart = false;
            entry.trapActivateTimeSeconds = FirstAppearance(Kind.Trap);
            entry.maxActiveTraps = 2;
            entry.respawnDelay = 60;
            entry.memo = "Stage 0 / " + kind + " / " + entry.spawnTimeSeconds + "s";
            return entry;
        }

        public static float FirstAppearance(Kind kind)
        {
            switch (kind)
            {
                case Kind.Sniper: return 60;
                case Kind.Popcorn: return 90;
                case Kind.Spore: return 120;
                case Kind.Sugar: return 150;
                case Kind.Thief: return 180;
                case Kind.Trap: return 210;
                case Kind.Support: return 240;
                case Kind.Debuffer: return 270;
                case Kind.EliteSugar: return 300;
                default: return Duration;
            }
        }

        public static int SpawnAllowance(LevelManager level, TimedSpecialSpawnSchedule.TimedSpawnEntry entry, int requested)
        {
            if (level == null || level.EntityManager == null || level.CurrentLevelTime >= Duration) return 0;
            Kind requestedKind = Identify(level.CurrentLevelBlueprint, entry.monsterBlueprint);
            int total = 0, sameKind = 0;
            foreach (var monster in level.EntityManager.LivingMonsters)
            {
                if (monster == null || !monster.gameObject.activeInHierarchy || monster.HP <= 0 || monster.IsMiniStageOwned) continue;
                Kind kind = Identify(level.CurrentLevelBlueprint, monster.Blueprint);
                if (kind == Kind.None || kind == Kind.Trap) continue;
                total++;
                if (kind == requestedKind) sameKind++;
            }
            int speciesLimit = requestedKind == Kind.Sniper || requestedKind == Kind.Spore || requestedKind == Kind.Sugar || requestedKind == Kind.Support ? 2 : 1;
            int room = Mathf.Min(MaxLivingTimedSpecials - total, speciesLimit - sameKind);
            // Keep the two support kinds together; no half-wave or deferred burst.
            if (requestedKind == Kind.Support && entry.companionBlueprint != null && room < 2) return 0;
            return Mathf.Max(0, Mathf.Min(requested, room));
        }
    }
}
