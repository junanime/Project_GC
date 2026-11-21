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
    public static class GStarLeaderboard
    {
        // Public display rows deliberately cannot expose a participant's telephone number.
        public sealed class Row
        {
            public int rank, score, stageId;
            public string name, runId;
            public bool success;
        }
        public sealed class Registration
        {
            public bool success, duplicate, improvedBest;
            public string message;
            public Row current;
            public int submittedScore, bestScore;
        }
        public sealed class CompletedRun
        {
            public RunScoreData data;
            public int stageId;
            public string sessionId, runId, exclusion;
            public bool registered, stageBest;
        }
        static GStarLeaderboardStore store;
#if UNITY_EDITOR
        public static string TestDirectory;
        public static void ReloadForTests() { store = null; }
#endif
        public static string ExportDirectory
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(TestDirectory)) return TestDirectory;
#endif
                return GStarDemo.DataDirectory;
            }
        }
        static GStarLeaderboardStore Store => store ?? (store = new GStarLeaderboardStore(ExportDirectory));
        public static string LastError => Store.LastError;
        public static string FilePath => Store.FilePath;
        public static bool CaptureCompleted(string runId, string sessionId, int stage, RunScoreData data, string exclusion = null)
            => Store.CaptureCompleted(runId, sessionId, stage, data, exclusion);
        public static Registration Submit(string name, string phone, int stage, string runId, string sessionId, RunScoreData data, string exclusion = null)
        {
            // Retry a failed disk write from the finished runtime snapshot; never accept a live run.
            var run = RunScore.Current;
            if (run != null && run.IsFinished && run.RunId == runId && !Store.ContainsRun(runId))
                Store.CaptureCompleted(run.RunId, run.SessionId, run.StageId, run.Data, run.Exclusion);
            return Store.Submit(name, phone, stage, runId, sessionId, data, exclusion);
        }
        public static List<CompletedRun> GetVisitorRuns(string sessionId) => Store.GetVisitorRuns(sessionId);
        public static Registration SubmitVisitor(string name, string phone, string sessionId) => Store.SubmitVisitor(name, phone, sessionId);
        public static List<Row> GetTop(int stage, int limit = 3) => Store.GetTop(stage, limit);
        public static Row GetRunRank(string runId) => Store.GetRunRank(runId);
        public static Row GetPlayerRank(string phone, int stage) => Store.GetPlayerRank(phone, stage);
        public static Row GetParticipantRank(string phone, string sessionId, int stage) => Store.GetParticipantRank(phone, sessionId, stage);
        public static CompletedRun GetCompletedRun(string runId) => Store.GetCompletedRun(runId);
        public static bool ExportCsv() => Store.ExportCsv();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { store = null; }
    }

    // One append-only run ledger, three independent boards. Registration attaches identity once.
    // A phone identifies a participant across visits; without one, the visitor session identifies
    // them. Display names never merge people. Ties use completion time, then ordinal run ID.
    public sealed class GStarLeaderboardStore
    {
        [Serializable]
        sealed class Entry
        {
            public string runId, sessionId, name = "", phone = "", exclusion = "";
            public int stageId;
            public long completedUtcTicks;
            public bool registered;
            public RunScoreData data;
        }
        [Serializable]
        sealed class Ledger
        {
            public int schema = 1;
            public long revision;
            public List<Entry> runs = new List<Entry>();
        }
        [Serializable]
        sealed class Envelope
        {
            public string payload, sha256;
        }
        readonly string directory;
        readonly object gate = new object();
        Ledger ledger;
        bool blocked;
        string lastError = "";
        public string LastError { get { lock (gate) { Load(); return lastError; } } private set { lastError = value; } }
        public string FilePath => Path.Combine(directory, "leaderboard.json");
        public int RunCount { get { lock (gate) { Load(); return ledger.runs.Count; } } }

        public GStarLeaderboardStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A storage directory is required.");
            this.directory = Path.GetFullPath(directory);
        }
        static string Digest(string value)
        {
            using (var hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
        static string Serialize(Ledger value)
        {
            string payload = JsonUtility.ToJson(value);
            return JsonUtility.ToJson(new Envelope { payload = payload, sha256 = Digest(payload) });
        }
        static Ledger Copy(Ledger value) => JsonUtility.FromJson<Ledger>(JsonUtility.ToJson(value));
        static Ledger Read(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path, Encoding.UTF8));
                if (envelope == null || string.IsNullOrEmpty(envelope.payload) || Digest(envelope.payload) != envelope.sha256) return null;
                var value = JsonUtility.FromJson<Ledger>(envelope.payload);
                if (value == null || value.schema != 1 || value.revision < 0 || value.runs == null) return null;
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in value.runs)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.runId) || !ids.Add(entry.runId) ||
                        string.IsNullOrEmpty(entry.sessionId) || entry.stageId < 0 || entry.stageId > 2 ||
                        entry.completedUtcTicks <= 0 || entry.data == null || entry.data.rules != GStarScoreRules.Version ||
                        (entry.registered && string.IsNullOrEmpty(entry.name))) return null;
                }
                return value;
            }
            catch (Exception) { return null; }
        }
        void Load()
        {
            if (ledger != null) return;
            Ledger best = null;
            string bestPath = null;
            bool exists = false;
            foreach (string path in new[] { FilePath, FilePath + ".bak", FilePath + ".tmp" })
            {
                exists |= File.Exists(path);
                var candidate = Read(path);
                if (candidate != null && (best == null || candidate.revision > best.revision))
                { best = candidate; bestPath = path; }
            }
            ledger = best ?? new Ledger();
            blocked = exists && best == null;
            if (blocked)
                LastError = "순위 기록과 백업을 읽을 수 없습니다. 기존 파일을 보존했습니다. 운영자에게 알려 주세요.";
            else if (best != null && bestPath != FilePath)
            {
                // Keep the valid backup intact while repairing a corrupt or older primary.
                try { WritePrimary(best, false); MirrorBackup(); }
                catch (Exception) { LastError = "백업 기록을 읽었지만 복구 저장에 실패했습니다. 저장 공간을 확인해 주세요."; }
            }
        }
        static void DurableWrite(string path, string text)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        }
        void WritePrimary(Ledger value, bool keepPrevious)
        {
            Directory.CreateDirectory(directory);
            DurableWrite(FilePath + ".tmp", Serialize(value));
            if (File.Exists(FilePath)) File.Replace(FilePath + ".tmp", FilePath, keepPrevious ? FilePath + ".bak" : null);
            else File.Move(FilePath + ".tmp", FilePath);
        }
        void MirrorBackup()
        {
            string backup = FilePath + ".bak";
            DurableWrite(backup + ".tmp", File.ReadAllText(FilePath, Encoding.UTF8));
            if (File.Exists(backup)) File.Replace(backup + ".tmp", backup, null);
            else File.Move(backup + ".tmp", backup);
        }
        bool Commit(Ledger next)
        {
            if (blocked) return false;
            next.revision = ledger.revision + 1;
            try { WritePrimary(next, true); }
            catch (Exception)
            {
                LastError = "기록 저장에 실패했습니다. 저장 공간을 확인한 뒤 다시 등록해 주세요.";
                return false;
            }
            ledger = next;
            LastError = "";
            try { MirrorBackup(); }
            catch (Exception) { LastError = "기록은 저장됐지만 백업 갱신에 실패했습니다. 운영자에게 알려 주세요."; }
            ExportCsv();
            return true;
        }
        public bool ContainsRun(string runId)
        {
            lock (gate) { Load(); return ledger.runs.Any(r => r.runId == runId); }
        }
        public bool CaptureCompleted(string runId, string sessionId, int stage, RunScoreData data, string exclusion = null)
        {
            lock (gate)
            {
                Load();
                if (blocked) return false;
                if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(sessionId) || stage < 0 || stage > 2 ||
                    data == null || data.rules != GStarScoreRules.Version)
                { LastError = "정상적으로 종료된 체험 기록을 찾을 수 없습니다."; return false; }
                var previous = ledger.runs.Find(r => r.runId == runId);
                if (previous != null)
                {
                    bool same = previous.sessionId == sessionId && previous.stageId == stage;
                    if (!same) LastError = "이미 저장된 도전 식별 정보와 일치하지 않습니다.";
                    return same;
                }
                var next = Copy(ledger);
                next.runs.Add(new Entry
                {
                    runId = runId, sessionId = sessionId, stageId = stage,
                    data = GStarScoreRules.Snapshot(data), exclusion = exclusion ?? "",
                    completedUtcTicks = DateTime.UtcNow.Ticks
                });
                return Commit(next);
            }
        }
        public GStarLeaderboard.Registration Submit(string name, string phone, int stage, string runId, string sessionId, RunScoreData data, string exclusion = null)
        {
            lock (gate)
            {
                Load();
                if (blocked) return Failure(LastError);
                var entry = ledger.runs.Find(r => r.runId == runId);
                if (entry == null || entry.stageId != stage || entry.sessionId != sessionId)
                    return Failure("종료 기록을 찾을 수 없습니다. 도전을 마친 뒤 등록해 주세요.");
                if (!string.IsNullOrEmpty(entry.exclusion) || !string.IsNullOrEmpty(exclusion))
                    return Failure("연습·검수 기록은 순위에 등록할 수 없습니다.");
                string error = ValidateIdentity(name, phone, sessionId, out var normalizedName, out var normalizedPhone);
                if (error != null) return Failure(error);
                if (entry.registered)
                {
                    if (entry.phone != normalizedPhone) return Failure("이미 등록된 기록입니다. 등록한 연락처를 변경할 수 없습니다.");
                    return Result(entry, true, false);
                }
                string participant = ParticipantKey(normalizedPhone, sessionId);
                string previousParticipant = ParticipantKey(entry.phone, sessionId);
                var previousBest = Best(stage).Find(r => ParticipantKey(r.phone, r.sessionId) == participant || ParticipantKey(r.phone, r.sessionId) == previousParticipant);
                bool improved = previousBest == null || Compare(entry, previousBest) < 0;
                var next = Copy(ledger);
                var registered = next.runs.Find(r => r.runId == runId);
                registered.name = normalizedName; registered.phone = normalizedPhone; registered.registered = true;
                if (!Commit(next)) return Failure(LastError);
                return Result(registered, false, improved);
            }
        }
        string ValidateIdentity(string name, string phone, string sessionId, out string normalizedName, out string normalizedPhone)
        {
            normalizedName = NormalizeName(name);
            normalizedPhone = string.IsNullOrWhiteSpace(phone) ? "" : NormalizePhone(phone);
            if (normalizedName == null) return "이름을 1~16자로 입력해 주세요. 줄바꿈과 특수 기호는 사용할 수 없습니다.";
            if (normalizedPhone == null) return "전화번호를 확인하거나 비워 두고 등록해 주세요.";
            string knownPhone = SessionPhone(sessionId);
            if (normalizedPhone.Length > 0 && knownPhone != null && normalizedPhone != knownPhone)
                return "이번 체험에서 처음 등록한 전화번호와 다릅니다. 같은 번호를 입력하거나 비워 두고 등록해 주세요.";
            return null;
        }

        // The ranking preview and the atomic write must choose the same winner on ties.
        Dictionary<int, Entry> VisitorBests(string sessionId)
        {
            var best = new Dictionary<int, Entry>();
            foreach (var entry in ledger.runs)
            {
                if (entry.sessionId != sessionId || !string.IsNullOrEmpty(entry.exclusion)) continue;
                if (!best.TryGetValue(entry.stageId, out var previous) || Compare(entry, previous) < 0)
                    best[entry.stageId] = entry;
            }
            return best;
        }

        public List<GStarLeaderboard.CompletedRun> GetVisitorRuns(string sessionId)
        {
            lock (gate)
            {
                Load();
                var best = VisitorBests(sessionId);
                return ledger.runs.Where(run => run.sessionId == sessionId)
                    .Select(run => new GStarLeaderboard.CompletedRun {
                        runId = run.runId, sessionId = run.sessionId, stageId = run.stageId,
                        data = GStarScoreRules.Snapshot(run.data), exclusion = run.exclusion,
                        registered = run.registered,
                        stageBest = best.TryGetValue(run.stageId, out var winner) && winner == run
                    }).ToList();
            }
        }

        public GStarLeaderboard.Registration SubmitVisitor(string name, string phone, string sessionId)
        {
            lock (gate)
            {
                Load();
                if (blocked) return Failure(LastError);
                var best = VisitorBests(sessionId);
                if (best.Count == 0) return Failure("등록할 도전 기록이 없습니다.");
                string error = ValidateIdentity(name, phone, sessionId, out var normalizedName, out var normalizedPhone);
                if (error != null) return Failure(error);
                var pending = new HashSet<string>(best.Values.Where(entry => !entry.registered).Select(entry => entry.runId));
                if (pending.Count == 0)
                    return new GStarLeaderboard.Registration { success = true, duplicate = true, message = "이미 등록된 기록입니다." };

                // Update every stage in one durable revision, including the derived prize exports.
                var next = Copy(ledger);
                foreach (var entry in next.runs.Where(entry => pending.Contains(entry.runId)))
                {
                    entry.name = normalizedName;
                    entry.phone = normalizedPhone;
                    entry.registered = true;
                }
                if (!Commit(next)) return Failure(LastError);
                return new GStarLeaderboard.Registration { success = true, message = "스테이지별 최고 기록을 등록했습니다." };
            }
        }

        static GStarLeaderboard.Registration Failure(string message) => new GStarLeaderboard.Registration { message = message };
        GStarLeaderboard.Registration Result(Entry entry, bool duplicate, bool improved)
        {
            var row = GetRunRank(entry.runId);
            return new GStarLeaderboard.Registration
            {
                success = true, duplicate = duplicate, improvedBest = improved,
                message = duplicate ? "이미 등록된 기록입니다." : string.IsNullOrEmpty(entry.phone) ? "전화번호 없이 기록을 저장했습니다." : "기록을 이 PC에 저장했습니다.",
                current = row, submittedScore = entry.data.Total, bestScore = row != null ? row.score : 0
            };
        }
        static int Compare(Entry a, Entry b)
        {
            int score = b.data.Total.CompareTo(a.data.Total);
            if (score != 0) return score;
            int time = a.completedUtcTicks.CompareTo(b.completedUtcTicks);
            return time != 0 ? time : StringComparer.Ordinal.Compare(a.runId, b.runId);
        }
        string SessionPhone(string sessionId) => string.IsNullOrEmpty(sessionId) ? null :
            ledger.runs.Find(r => r.sessionId == sessionId && r.registered && !string.IsNullOrEmpty(r.phone) && string.IsNullOrEmpty(r.exclusion))?.phone;
        string ParticipantKey(string phone, string sessionId)
        {
            // Supplying a contact on a later attempt links the visitor's earlier anonymous
            // records to that phone's best. The original records retain blank contact fields.
            string knownPhone = SessionPhone(sessionId);
            if (!string.IsNullOrEmpty(knownPhone)) phone = knownPhone;
            return string.IsNullOrEmpty(phone) ? "visitor:" + sessionId : "phone:" + phone;
        }
        List<Entry> Best(int stage)
        {
            var best = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in ledger.runs)
            {
                if (entry.stageId != stage || !entry.registered || !string.IsNullOrEmpty(entry.exclusion)) continue;
                string participant = ParticipantKey(entry.phone, entry.sessionId);
                if (!best.TryGetValue(participant, out var old) || Compare(entry, old) < 0) best[participant] = entry;
            }
            var result = best.Values.ToList(); result.Sort(Compare); return result;
        }
        static GStarLeaderboard.Row Row(Entry entry, int rank) => new GStarLeaderboard.Row
        { rank = rank, score = entry.data.Total, name = entry.name, stageId = entry.stageId, runId = entry.runId, success = entry.data.success };
        public List<GStarLeaderboard.Row> GetTop(int stage, int limit = 3)
        {
            lock (gate)
            {
                Load();
                var board = Best(stage);
                var rows = new List<GStarLeaderboard.Row>();
                for (int i = 0; i < board.Count && i < Math.Max(0, limit); i++) rows.Add(Row(board[i], i + 1));
                return rows;
            }
        }
        public GStarLeaderboard.Row GetRunRank(string runId)
        {
            lock (gate)
            {
                Load();
                var run = ledger.runs.Find(r => r.runId == runId);
                if (run == null || !run.registered || !string.IsNullOrEmpty(run.exclusion)) return null;
                var board = Best(run.stageId);
                string participant = ParticipantKey(run.phone, run.sessionId);
                int index = board.FindIndex(r => ParticipantKey(r.phone, r.sessionId) == participant);
                return index < 0 ? null : Row(board[index], index + 1);
            }
        }
        public GStarLeaderboard.Row GetPlayerRank(string phone, int stage)
            => string.IsNullOrWhiteSpace(phone) ? null : GetParticipantRank(phone, null, stage);
        public GStarLeaderboard.Row GetParticipantRank(string phone, string sessionId, int stage)
        {
            lock (gate)
            {
                Load();
                string normalized = string.IsNullOrWhiteSpace(phone) ? "" : NormalizePhone(phone);
                if (normalized == null || (normalized.Length == 0 && string.IsNullOrWhiteSpace(sessionId))) return null;
                string participant = ParticipantKey(normalized, sessionId);
                var board = Best(stage);
                int index = board.FindIndex(r => ParticipantKey(r.phone, r.sessionId) == participant);
                return index < 0 ? null : Row(board[index], index + 1);
            }
        }
        public GStarLeaderboard.CompletedRun GetCompletedRun(string runId)
        {
            lock (gate)
            {
                Load();
                var run = ledger.runs.Find(r => r.runId == runId);
                return run == null ? null : new GStarLeaderboard.CompletedRun
                {
                    runId = run.runId, sessionId = run.sessionId, stageId = run.stageId,
                    exclusion = run.exclusion, data = GStarScoreRules.Snapshot(run.data)
                };
            }
        }
        public static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var digits = new StringBuilder();
            foreach (char c in phone.Trim())
            {
                if (c >= '0' && c <= '9') digits.Append(c);
                else if (c != '+' && c != '-' && c != '(' && c != ')' && c != ' ') return null;
            }
            string value = digits.ToString();
            if (value.StartsWith("82", StringComparison.Ordinal) && (value.Length == 11 || value.Length == 12)) value = "0" + value.Substring(2);
            return value.Length >= 9 && value.Length <= 15 ? value : null;
        }
        static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            name = name.Trim();
            if (name.Length > 16) return null;
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '_' && c != '-') return null;
            return name;
        }
        static string Cell(string text)
        {
            text = text ?? "";
            string trimmed = text.TrimStart();
            if (trimmed.Length > 0 && "=+-@\t\r".IndexOf(trimmed[0]) >= 0) text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
        static void CsvRow(StringBuilder csv, params string[] values)
        { csv.AppendLine(string.Join(",", values.Select(Cell))); }
        void WriteExport(string name, string contents)
        {
            var path = Path.Combine(directory, name);
            DurableWrite(path + ".tmp", "\uFEFF" + contents);
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
            else File.Move(path + ".tmp", path);
        }
        public bool ExportCsv()
        {
            lock (gate)
            {
                Load();
                if (blocked) return false;
                try
                {
                    Directory.CreateDirectory(directory);
                    var all = new StringBuilder();
                    CsvRow(all, "run_id", "session_id", "completed_utc", "stage", "registered", "name", "phone_private", "score", "clear", "seconds", "normal", "special", "elite", "mini_boss", "boss", "mini_clear", "overcharge", "gold", "silver", "rules", "excluded");
                    foreach (var r in ledger.runs)
                        CsvRow(all, r.runId, r.sessionId, new DateTime(r.completedUtcTicks, DateTimeKind.Utc).ToString("O"), r.stageId.ToString(), r.registered.ToString(), r.name,
                            string.IsNullOrEmpty(r.phone) ? "" : "'" + r.phone, r.data.Total.ToString(), r.data.success.ToString(), r.data.seconds.ToString("0.0", CultureInfo.InvariantCulture),
                            r.data.normal.ToString(), r.data.special.ToString(), r.data.elite.ToString(), r.data.miniBoss.ToString(), r.data.boss.ToString(), r.data.cleared.ToString(),
                            r.data.overcharge.ToString(), r.data.gold.ToString(), r.data.silver.ToString(), r.data.rules, r.exclusion);
                    var batch = new GStarReportBatch(WriteExport);
                    batch.Write("all-runs-private.csv", all.ToString());
                    var winners = new StringBuilder(); var publicBoard = new StringBuilder();
                    var draw = new StringBuilder();
                    CsvRow(draw, "stage", "rank", "name", "phone_private", "score", "run_id", "draw_weight");
                    CsvRow(winners, "stage", "rank", "name", "phone_private", "score", "run_id");
                    CsvRow(publicBoard, "stage", "rank", "name", "score");
                    for (int stage = 0; stage <= 2; stage++)
                    {
                        var board = Best(stage);
                        for (int i = 0; i < board.Count; i++)
                        {
                            var r = board[i];
                            CsvRow(publicBoard, stage.ToString(), (i + 1).ToString(), r.name, r.data.Total.ToString());
                            // One entry per person per stage. Rank weights decrease evenly
                            // from twice the last place weight down to a positive minimum.
                            long weight = board.Count == 1 ? 1L : 2L * (board.Count - 1) - i;
                            // The personal best may have been registered without a phone.
                            // Use the same visitor contact as the ranking identity for prize lists only.
                            string contact = string.IsNullOrEmpty(r.phone) ? SessionPhone(r.sessionId) : r.phone;
                            string prizePhone = string.IsNullOrEmpty(contact) ? "" : "'" + contact;
                            CsvRow(draw, stage.ToString(), (i + 1).ToString(), r.name, prizePhone, r.data.Total.ToString(), r.runId,
                                weight.ToString(CultureInfo.InvariantCulture));
                            if (i < 3) CsvRow(winners, stage.ToString(), (i + 1).ToString(), r.name, prizePhone, r.data.Total.ToString(), r.runId);
                        }
                    }
                    batch.Write("prize-draw-private.csv", draw.ToString());
                    batch.Write("prize-top3-private.csv", winners.ToString());
                    batch.Write("ranking-public.csv", publicBoard.ToString());
                    if (!batch.Succeeded) LastError = "순위 원본은 저장됐습니다. 갱신하지 못한 파일: " + batch.FailedNames;
                    return batch.Succeeded;
                }
                catch (Exception)
                {
                    LastError = "순위 원본은 보존됐지만 CSV 내보내기에 실패했습니다. 운영자에게 알려 주세요.";
                    return false;
                }
            }
        }
    }
}
