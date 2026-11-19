using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    [Serializable]
    public sealed class GStarAnalyticsRun
    {
        public int schema = 1;
        public string runId, sessionId, startedUtc, endedUtc = "", characterId, characterName;
        public int stageId, startingWeaponId = -1;
        public string startingRelic = "", status = "active", endingReason = "";
        public string[] startingItems = Array.Empty<string>();
        public int normalKills, specialKills, eliteKills, miniBossKills, bossKills, dashCount, activeSkillCount;
        public int miniStageClears, overchargeSuccesses, gold, silver, score, level = 1, eventCount;
        public float elapsedSeconds, damageDealt, damageTaken, healingReceived;
        public bool combatStatsAvailable;
        public int TotalKills => normalKills + specialKills + eliteKills + miniBossKills + bossKills;
    }

    [Serializable]
    public sealed class GStarAnalyticsEvent
    {
        public int schema = 1, sequence;
        public string runId, sessionId, utc, type, id, detail;
        public float elapsedSeconds;
        public string[] offered = Array.Empty<string>();
    }

    public sealed class GStarAnalyticsExportResult
    {
        public bool success;
        public string directory, error;
    }

    // Gameplay telemetry intentionally has no participant name or telephone fields.
    public static class GStarAnalytics
    {
        static GStarAnalyticsStore store;
        static GStarAnalyticsRun active;
        static LevelManager owner;
        static StatsManager stats;
        public static string DirectoryPath => Path.Combine(GStarDemo.DataDirectory, "analytics");
        public static string LastError => store?.LastError ?? "";
        public static bool IsRecording => GStarDemo.Enabled && active != null && active.status == "active";
        static GStarAnalyticsStore Store => store ?? (store = new GStarAnalyticsStore(DirectoryPath));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { store = null; active = null; owner = null; stats = null; }

        public static void Begin(LevelManager level)
        {
            if (!GStarDemo.Enabled || level == null || !GStarSession.ActiveAttempt || string.IsNullOrEmpty(GStarSession.RunId)) return;
            if (IsRecording && active.runId == GStarSession.RunId) return;
            if (IsRecording) Abandon("new_run_started_before_previous_closed");
            var actor = level.PlayerCharacter;
            var character = actor != null ? actor.Blueprint : CrossSceneData.CharacterBlueprint;
            var data = new GStarAnalyticsRun
            {
                runId = GStarSession.RunId, sessionId = GStarSession.SessionId,
                stageId = RunScore.Current != null ? RunScore.Current.StageId : GStarSession.StageId,
                startedUtc = DateTime.UtcNow.ToString("O"), characterId = OctoberArt.CharacterKey(character),
                characterName = character != null ? character.name : "unknown",
                startingWeaponId = StartingNeedleSelection.SelectedId(character),
                startingRelic = RelicSaveData.GetEquippedRelicId(),
                startingItems = LobbyLoadoutData.SelectedCarryItems.Where(i => i != null)
                    .Select(i => i.octoberId + ":" + i.itemName).ToArray()
            };
            if (!Store.Begin(data)) return;
            owner = level; active = data;
            GStarCombatPacing.Begin();
            stats = level.GetComponent<StatsManager>() ?? UnityEngine.Object.FindObjectOfType<StatsManager>();
            if (level.GetComponent<GStarAnalyticsSampler>() == null) level.gameObject.AddComponent<GStarAnalyticsSampler>();
            Record("run_started", data.stageId.ToString(), "starting loadout captured");
        }

        public static void Record(string type, string id, string detail = "", string[] offered = null)
        {
            if (!IsRecording) return;
            Sample();
            var entry = new GStarAnalyticsEvent
            {
                runId = active.runId, sessionId = active.sessionId, sequence = ++active.eventCount,
                utc = DateTime.UtcNow.ToString("O"), elapsedSeconds = active.elapsedSeconds,
                type = type ?? "", id = id ?? "", detail = detail ?? "", offered = offered ?? Array.Empty<string>()
            };
            Store.Append(entry);
            Store.Save(active);
        }
        public static void Dash() { if (IsRecording) active.dashCount++; }
        public static void ActiveSkill()
        {
            if (!IsRecording) return;
            active.activeSkillCount++;
            GStarCombatPacing.Skill();
            Record("active_skill", active.characterId);
        }
        public static void Finish(bool success, string reason = "")
            => End(success ? "clear" : "fail", string.IsNullOrEmpty(reason) ? (success ? "stage_clear" : "hp_zero") : reason);
        public static void Abandon(string reason = "abandoned") => End("abandoned", reason);
        static void End(string status, string reason)
        {
            if (!IsRecording) return;
            GStarCombatPacing.End();
            Record("run_ended", status, reason);
            active.status = status; active.endingReason = reason; active.endedUtc = DateTime.UtcNow.ToString("O");
            Store.Save(active);
            active = null; owner = null; stats = null;
        }
        public static void Flush()
        {
            if (!IsRecording) return;
            Sample(); Store.Save(active);
        }
        static void Sample()
        {
            if (active == null) return;
            var score = RunScore.Current;
            if (score != null && score.RunId == active.runId)
            {
                var d = score.Data;
                active.normalKills = d.normal; active.specialKills = d.special; active.eliteKills = d.elite;
                active.miniBossKills = d.miniBoss; active.bossKills = d.boss;
                active.miniStageClears = d.cleared; active.overchargeSuccesses = d.overcharge;
                active.gold = d.gold; active.silver = d.silver; active.score = d.Total;
                active.elapsedSeconds = d.seconds;
            }
            if (owner != null && owner.PlayerCharacter != null) active.level = owner.PlayerCharacter.CurrentLevel;
            if (stats != null)
            {
                active.combatStatsAvailable = true;
                active.damageDealt = stats.DamageDealt; active.damageTaken = stats.DamageTaken;
                active.healingReceived = stats.HealingReceived;
            }
        }
        public static GStarAnalyticsExportResult Export()
        {
            if (!GStarDemo.Enabled) return new GStarAnalyticsExportResult { success = false, directory = DirectoryPath, error = "지스타 체험판에서만 사용할 수 있습니다." };
            Flush(); return Store.Export();
        }
    }

    public sealed class GStarAnalyticsSampler : MonoBehaviour
    {
        float nextFlush;
        LevelManager level;
        void Awake() => level = GetComponent<LevelManager>();
        void Update()
        {
            GStarCombatPacing.Tick(level, Time.deltaTime);
            if (Time.unscaledTime < nextFlush) return;
            nextFlush = Time.unscaledTime + 5f;
            GStarAnalytics.Flush();
        }
        void OnApplicationPause(bool paused) { if (paused) GStarAnalytics.Flush(); }
        void OnApplicationQuit() => GStarAnalytics.Flush();
        void OnDestroy() => GStarAnalytics.Flush();
    }
}
