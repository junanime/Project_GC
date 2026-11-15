using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Vampire
{
    // Small per-run snapshots avoid rewriting four days of data on every five-second save.
    // Choice/event lines are flushed immediately; snapshots are replaced atomically with a backup.
    public sealed class GStarAnalyticsStore
    {
        [Serializable] sealed class StoredRun { public long revision; public GStarAnalyticsRun run; }
        [Serializable] sealed class Envelope { public string payload, sha256; }
        readonly string directory;
        readonly object gate = new object();
        readonly Dictionary<string, StoredRun> runs = new Dictionary<string, StoredRun>(StringComparer.Ordinal);
        readonly HashSet<string> damagedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool loaded;
        bool unavailable;
        public string LastError { get; private set; } = "";
        string RunDirectory => Path.Combine(directory, "runs");
        string JournalPath => Path.Combine(directory, "events-live.jsonl");
        public GStarAnalyticsStore(string path) { directory = Path.GetFullPath(path); }
        static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        static string Digest(string text)
        { using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text))); }
        static bool ValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 128 && id.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        string RunPath(string id) => Path.Combine(RunDirectory, "run-" + id + ".json");
        static bool Valid(GStarAnalyticsRun run) => run != null && run.schema == 1 && ValidId(run.runId) &&
            ValidId(run.sessionId) && run.stageId >= 0 && run.stageId <= 2 &&
            new[] { "active", "clear", "fail", "abandoned", "interrupted" }.Contains(run.status);
        static StoredRun Read(string path)
        {
            try
            {
                var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path, Encoding.UTF8));
                if (envelope == null || string.IsNullOrEmpty(envelope.payload) || Digest(envelope.payload) != envelope.sha256) return null;
                var stored = JsonUtility.FromJson<StoredRun>(envelope.payload);
                return stored != null && stored.revision > 0 && Valid(stored.run) ? stored : null;
            }
            catch (Exception) { return null; }
        }
        static void AtomicWrite(string path, string contents, bool bom = false, bool keepBackup = true)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            byte[] text = new UTF8Encoding(bom).GetBytes(contents);
            using (var stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (bom) { var preamble = new UTF8Encoding(true).GetPreamble(); stream.Write(preamble, 0, preamble.Length); }
                stream.Write(text, 0, text.Length); stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(path + ".tmp", path, keepBackup ? path + ".bak" : null);
            else File.Move(path + ".tmp", path);
        }
        void Load()
        {
            if (loaded) return;
            loaded = true;
            try { LoadExisting(); }
            catch (Exception e) { unavailable = true; LastError = "QA 기록 읽기 실패: " + e.GetType().Name; }
        }
        void LoadExisting()
        {
            if (!Directory.Exists(RunDirectory)) return;
            var groups = Directory.GetFiles(RunDirectory, "run-*.json*")
                .Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".json.bak", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".json.tmp", StringComparison.OrdinalIgnoreCase))
                .GroupBy(p => p.Substring(0, p.LastIndexOf(".json", StringComparison.OrdinalIgnoreCase) + 5));
            foreach (var group in groups)
            {
                var best = group.Select(Read).Where(r => r != null).OrderByDescending(r => r.revision).FirstOrDefault();
                if (best == null)
                {
                    damagedPaths.Add(group.Key);
                    LastError = "일부 QA 기록과 백업을 읽지 못했습니다. 원본을 보존했습니다.";
                    continue;
                }
                if (!runs.TryGetValue(best.run.runId, out var previous) || best.revision > previous.revision) runs[best.run.runId] = best;
            }
            var journalSequences = ReadEvents().GroupBy(e => e.runId).ToDictionary(g => g.Key, g => g.Max(e => e.sequence));
            foreach (var entry in runs.Values.ToArray())
            {
                if (entry.run.status != "active") continue;
                var interrupted = Copy(entry.run);
                interrupted.status = "interrupted"; interrupted.endingReason = "process_stopped_before_run_finished";
                interrupted.endedUtc = DateTime.UtcNow.ToString("O");
                interrupted.eventCount = Math.Max(interrupted.eventCount, journalSequences.TryGetValue(interrupted.runId, out int sequence) ? sequence : 0) + 1;
                AppendCore(new GStarAnalyticsEvent { runId = interrupted.runId, sessionId = interrupted.sessionId,
                    sequence = interrupted.eventCount, utc = interrupted.endedUtc, elapsedSeconds = interrupted.elapsedSeconds,
                    type = "run_ended", id = "interrupted", detail = interrupted.endingReason });
                SaveCore(interrupted);
            }
        }
        public bool Begin(GStarAnalyticsRun run)
        {
            lock (gate)
            {
                Load();
                if (unavailable || !Valid(run) || run.status != "active") return false;
                if (runs.ContainsKey(run.runId)) return false; // Never reopen or overwrite a completed run.
                return SaveCore(run);
            }
        }
        public bool Save(GStarAnalyticsRun run)
        {
            lock (gate)
            {
                Load();
                if (unavailable || !Valid(run) || !runs.TryGetValue(run.runId, out var current) || current.run.sessionId != run.sessionId) return false;
                if (current.run.status != "active") return false; // Finished records are immutable.
                return SaveCore(run);
            }
        }
        bool SaveCore(GStarAnalyticsRun run)
        {
            if (damagedPaths.Contains(RunPath(run.runId))) return false;
            try
            {
                var next = new StoredRun { revision = runs.TryGetValue(run.runId, out var old) ? old.revision + 1 : 1, run = Copy(run) };
                string payload = JsonUtility.ToJson(next);
                string contents = JsonUtility.ToJson(new Envelope { payload = payload, sha256 = Digest(payload) });
                AtomicWrite(RunPath(run.runId), contents);
                // A finished record's fallback must also be finished, not the preceding active snapshot.
                if (run.status != "active") AtomicWrite(RunPath(run.runId) + ".bak", contents, false, false);
                runs[run.runId] = next;
                return true;
            }
            catch (Exception e) { LastError = "QA 기록 저장 실패: " + e.GetType().Name; return false; }
        }
        public GStarAnalyticsRun GetRun(string id)
        { lock (gate) { Load(); return runs.TryGetValue(id, out var run) ? Copy(run.run) : null; } }

        public bool Append(GStarAnalyticsEvent entry)
        {
            lock (gate)
            {
                Load();
                if (unavailable || entry == null || !runs.TryGetValue(entry.runId ?? "", out var run) || run.run.status != "active" || entry.sessionId != run.run.sessionId) return false;
                return AppendCore(entry);
            }
        }
        bool AppendCore(GStarAnalyticsEvent entry)
        {
            try
            {
                Directory.CreateDirectory(directory);
                // A torn last write must not swallow the next valid event after restart.
                bool separator = false;
                if (File.Exists(JournalPath)) using (var check = new FileStream(JournalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                { if (check.Length > 0) { check.Position = check.Length - 1; separator = check.ReadByte() != '\n'; } }
                byte[] bytes = Encoding.UTF8.GetBytes((separator ? "\n" : "") + JsonUtility.ToJson(entry) + "\n");
                using (var stream = new FileStream(JournalPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                return true;
            }
            catch (Exception e) { LastError = "QA 행동 기록 저장 실패: " + e.GetType().Name; return false; }
        }
        List<GStarAnalyticsEvent> ReadEvents()
        {
            var output = new List<GStarAnalyticsEvent>();
            if (!File.Exists(JournalPath)) return output;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in File.ReadLines(JournalPath, Encoding.UTF8))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var entry = JsonUtility.FromJson<GStarAnalyticsEvent>(line);
                    if (entry == null || entry.schema != 1 || entry.sequence <= 0 || string.IsNullOrEmpty(entry.type) ||
                        !runs.TryGetValue(entry.runId ?? "", out var run) || entry.sessionId != run.run.sessionId ||
                        !seen.Add(entry.runId + "/" + entry.sequence)) continue;
                    output.Add(entry);
                }
                catch (Exception) { /* Preserve a torn line in the live journal; omit it from the valid export. */ }
            }
            return output;
        }
        static string Number(double value) => double.IsNaN(value) || double.IsInfinity(value) ? "" : value.ToString("0.###", CultureInfo.InvariantCulture);
        static string Cell(string value)
        {
            value = value ?? "";
            if (value.Length > 0 && "=+-@\t\r".IndexOf(value[0]) >= 0) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        static void Row(StringBuilder csv, params object[] cells) => csv.AppendLine(string.Join(",", cells.Select(c => Cell(Convert.ToString(c, CultureInfo.InvariantCulture)))));
        static string Joined(string[] values) => string.Join(" | ", values ?? Array.Empty<string>());
        static string RunsCsv(IEnumerable<GStarAnalyticsRun> all)
        {
            var csv = new StringBuilder();
            Row(csv, "run_id", "session_id", "started_utc", "ended_utc", "status", "ending_reason", "character_id", "character_name", "stage", "starting_weapon_id", "starting_relic", "starting_items", "score", "elapsed_seconds", "level", "normal_kills", "special_kills", "elite_kills", "mini_boss_kills", "boss_kills", "total_kills", "dash_count", "active_skill_count", "mini_stage_clears", "overcharge_successes", "gold", "silver", "combat_stats_available", "damage_dealt", "damage_taken", "healing_received", "events");
            foreach (var r in all) Row(csv, r.runId, r.sessionId, r.startedUtc, r.endedUtc, r.status, r.endingReason, r.characterId, r.characterName, r.stageId, r.startingWeaponId, r.startingRelic, Joined(r.startingItems), r.score, Number(r.elapsedSeconds), r.level, r.normalKills, r.specialKills, r.eliteKills, r.miniBossKills, r.bossKills, r.TotalKills, r.dashCount, r.activeSkillCount, r.miniStageClears, r.overchargeSuccesses, r.gold, r.silver, r.combatStatsAvailable, r.combatStatsAvailable ? Number(r.damageDealt) : "", r.combatStatsAvailable ? Number(r.damageTaken) : "", r.combatStatsAvailable ? Number(r.healingReceived) : "", r.eventCount);
            return csv.ToString();
        }
        static string Average(IEnumerable<GStarAnalyticsRun> source, Func<GStarAnalyticsRun, double> selector)
        { var values = source.ToArray(); return values.Length == 0 ? "" : Number(values.Average(selector)); }
        static string Summary(IEnumerable<GStarAnalyticsRun> all, Func<GStarAnalyticsRun, string> key, string groupLabel)
        {
            var csv = new StringBuilder();
            Row(csv, groupLabel, "selected_runs", "clear", "fail", "abandoned", "interrupted", "active", "clear_rate_completed", "clear_avg_kills", "fail_avg_kills", "clear_avg_dash", "fail_avg_dash", "clear_avg_seconds", "fail_avg_seconds", "all_avg_kills", "all_avg_dash", "all_avg_seconds", "all_avg_active_skill", "all_avg_end_level", "measured_avg_damage_dealt", "measured_avg_damage_taken");
            foreach (var group in all.GroupBy(key).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var clear = group.Where(r => r.status == "clear").ToArray();
                var fail = group.Where(r => r.status == "fail").ToArray();
                int finished = clear.Length + fail.Length;
                Row(csv, group.Key, group.Count(), clear.Length, fail.Length, group.Count(r => r.status == "abandoned"), group.Count(r => r.status == "interrupted"), group.Count(r => r.status == "active"), finished == 0 ? "" : Number((double)clear.Length / finished), Average(clear, r => r.TotalKills), Average(fail, r => r.TotalKills), Average(clear, r => r.dashCount), Average(fail, r => r.dashCount), Average(clear, r => r.elapsedSeconds), Average(fail, r => r.elapsedSeconds), Average(group, r => r.TotalKills), Average(group, r => r.dashCount), Average(group, r => r.elapsedSeconds), Average(group, r => r.activeSkillCount), Average(group, r => r.level), Average(group.Where(r => r.combatStatsAvailable), r => r.damageDealt), Average(group.Where(r => r.combatStatsAvailable), r => r.damageTaken));
            }
            return csv.ToString();
        }
        static string EventsCsv(IEnumerable<GStarAnalyticsEvent> events)
        {
            var csv = new StringBuilder();
            Row(csv, "run_id", "session_id", "sequence", "elapsed_seconds", "utc", "event_type", "id", "detail", "offered_options");
            foreach (var e in events) Row(csv, e.runId, e.sessionId, e.sequence, Number(e.elapsedSeconds), e.utc, e.type, e.id, e.detail, Joined(e.offered));
            return csv.ToString();
        }
        static bool ChoiceEvent(GStarAnalyticsEvent e) => (e.offered != null && e.offered.Length > 0) || e.type.Contains("select") || e.type.Contains("choice") || e.type.Contains("chosen") || e.type.Contains("acquired") || e.type.Contains("purchase") || e.type.Contains("reward");
        public GStarAnalyticsExportResult Export()
        {
            lock (gate)
            {
                string output = Path.Combine(directory, "exports");
                try
                {
                    Load();
                    if (unavailable) return new GStarAnalyticsExportResult { success = false, directory = output, error = LastError };
                    var all = runs.Values.Select(r => r.run).OrderBy(r => r.startedUtc, StringComparer.Ordinal).ThenBy(r => r.runId, StringComparer.Ordinal).ToArray();
                    var events = ReadEvents();
                    AtomicWrite(Path.Combine(output, "runs.jsonl"), string.Join("\n", all.Select(JsonUtility.ToJson)) + (all.Length > 0 ? "\n" : ""));
                    AtomicWrite(Path.Combine(output, "events.jsonl"), string.Join("\n", events.Select(JsonUtility.ToJson)) + (events.Count > 0 ? "\n" : ""));
                    AtomicWrite(Path.Combine(output, "runs.csv"), RunsCsv(all), true);
                    AtomicWrite(Path.Combine(output, "character-summary.csv"), Summary(all, r => r.characterId ?? "unknown", "character_id"), true);
                    AtomicWrite(Path.Combine(output, "stage-summary.csv"), Summary(all, r => r.stageId.ToString(), "stage"), true);
                    AtomicWrite(Path.Combine(output, "character-stage-summary.csv"), Summary(all, r => (r.characterId ?? "unknown") + "/" + r.stageId, "character_stage"), true);
                    AtomicWrite(Path.Combine(output, "events.csv"), EventsCsv(events), true);
                    AtomicWrite(Path.Combine(output, "choices.csv"), EventsCsv(events.Where(ChoiceEvent)), true);
                    var choices = new StringBuilder();
                    Row(choices, "event_type", "id", "recorded_count", "distinct_runs");
                    foreach (var group in events.Where(ChoiceEvent).GroupBy(e => new { e.type, e.id }).OrderBy(g => g.Key.type).ThenBy(g => g.Key.id))
                        Row(choices, group.Key.type, group.Key.id, group.Count(), group.Select(e => e.runId).Distinct().Count());
                    AtomicWrite(Path.Combine(output, "choice-counts.csv"), choices.ToString(), true);
                    var offered = new StringBuilder();
                    Row(offered, "event_type", "offered_option", "times_offered", "distinct_runs");
                    foreach (var group in events.SelectMany(e => (e.offered ?? Array.Empty<string>()).Select(id => new { e.type, e.runId, id })).GroupBy(e => new { e.type, e.id }).OrderBy(g => g.Key.type).ThenBy(g => g.Key.id))
                        Row(offered, group.Key.type, group.Key.id, group.Count(), group.Select(e => e.runId).Distinct().Count());
                    AtomicWrite(Path.Combine(output, "offered-counts.csv"), offered.ToString(), true);
                    return new GStarAnalyticsExportResult { success = true, directory = output, error = LastError };
                }
                catch (Exception e)
                {
                    LastError = "QA 자료 내보내기 실패: " + e.GetType().Name;
                    return new GStarAnalyticsExportResult { success = false, directory = output, error = LastError };
                }
            }
        }
    }
}
