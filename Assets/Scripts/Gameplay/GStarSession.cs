using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Vampire
{
    // A visitor owns three departures, not three scenes. Menu changes never reset this file.
    public static class GStarSession
    {
        [Serializable]
        sealed class State
        {
            public int version = 1, usedAttempts, stageId = 1;
            public string sessionId, runId = "", lastRunId = "", playerName = "", phone = "";
            public bool active, started, hasCompleted, lastSuccess, interrupted, visitorFinished;
            public string[] seenGuides = Array.Empty<string>();
        }

        static State state;
        static string storageError = "";
#if UNITY_EDITOR
        public static string TestDirectory;
        public static void ReloadForTests() { state = null; storageError = ""; }
#endif
        public static string FilePath
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(TestDirectory)) return Path.Combine(TestDirectory, "visitor-session.json");
#endif
                return Path.Combine(GStarDemo.DataDirectory, "visitor-session.json");
            }
        }
        public static int RemainingAttempts => Mathf.Max(0, GStarDemo.AttemptLimit - Current.usedAttempts);
        public static int UsedAttempts => Current.usedAttempts;
        public static bool ActiveAttempt => Current.active;
        public static bool PendingAttempt => Current.active && !Current.started;
        public static bool AttemptStarted => Current.active && Current.started;
        public static int StageId => Current.stageId;
        public static string SessionId => Current.sessionId;
        public static string RunId => Current.runId;
        public static string LastRunId => Current.lastRunId;
        public static bool HasCompletedAttempt => Current.hasCompleted;
        public static bool LastSuccess => Current.lastSuccess;
        public static bool Interrupted => Current.interrupted;
        public static bool VisitorFinished => Current.visitorFinished;
        public static string PlayerName => Current.playerName;
        public static string Phone => Current.phone;
        public static bool HasSeenGuide(string id) => Array.IndexOf(Current.seenGuides ?? Array.Empty<string>(),id)>=0;
        public static bool MarkGuideSeen(string id)
        {
            if(HasSeenGuide(id))return true;
            var next=Copy(Current);var list=new System.Collections.Generic.List<string>(next.seenGuides??Array.Empty<string>());
            list.Add(id);next.seenGuides=list.ToArray();return Commit(next);
        }
        public static string StorageError { get { _ = Current; return storageError; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { state = null; storageError = ""; }

        static State Current
        {
            get
            {
                if (state != null) return state;
                state = Read(FilePath) ?? Read(FilePath + ".bak");
                if (state == null)
                {
                    state = NewVisitor();
                    if (File.Exists(FilePath) || File.Exists(FilePath + ".bak"))
                    {
                        // Damaged files must not silently grant a second set of chances.
                        state.usedAttempts = GStarDemo.AttemptLimit; state.visitorFinished = true;
                        storageError = "체험 기록 복구가 필요합니다. 직원에게 새 체험 시작을 요청해 주세요.";
                    }
                }
                // A failed/crashed loading screen costs nothing. An interrupted playable run
                // keeps its consumed chance, but cannot strand the next launch as "active".
                if (state.active)
                {
                    if (!state.started) state.usedAttempts = Math.Max(0, state.usedAttempts - 1);
                    else
                    {
                        state.lastRunId = state.runId; state.hasCompleted = true;
                        state.lastSuccess = false; state.interrupted = true;
                    }
                    state.active = state.started = false;
                    Save(state);
                }
                return state;
            }
        }
        static State NewVisitor() => new State { sessionId = Guid.NewGuid().ToString("N") };
        static State Read(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var value = JsonUtility.FromJson<State>(File.ReadAllText(path, Encoding.UTF8));
                if (value == null || value.version != 1 || string.IsNullOrEmpty(value.sessionId) ||
                    value.usedAttempts < 0 || value.usedAttempts > GStarDemo.AttemptLimit ||
                    value.stageId < 0 || value.stageId > 2 || (value.active && string.IsNullOrEmpty(value.runId))) return null;
                return value;
            }
            catch (Exception error) { Debug.LogWarning("[GStarSession] Cannot read visitor session: " + error.GetType().Name); return null; }
        }
        static State Copy(State value) => JsonUtility.FromJson<State>(JsonUtility.ToJson(value));
        static bool Save(State value)
        {
            try
            {
                var path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(value, true));
                using (var stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
                else File.Move(path + ".tmp", path);
                storageError = ""; return true;
            }
            catch (Exception error)
            {
                storageError = "체험 기록을 저장하지 못했습니다. 저장 공간을 확인해 주세요.";
                Debug.LogWarning("[GStarSession] Save failed: " + error.GetType().Name);
                return false;
            }
        }
        static bool Commit(State next)
        {
            if (!Save(next)) return false;
            state = next; return true;
        }

        public static bool TryBeginAttempt(int stage, out string reason)
        {
            reason = "";
            if (!GStarDemo.Enabled) return true;
            var current = Current;
            if (stage < 0 || stage > 2) { reason = "선택한 스테이지를 확인해 주세요."; return false; }
            if (current.active) { reason = "이미 출전 중입니다."; return false; }
            if (current.visitorFinished) { reason = "직원에게 다음 체험 시작을 요청해 주세요."; return false; }
            if (RemainingAttempts == 0) { reason = "도전 기회를 모두 사용했습니다."; return false; }
            var next = Copy(current);
            next.usedAttempts++; next.stageId = stage; next.runId = Guid.NewGuid().ToString("N");
            next.active = true; next.started = false; next.interrupted = false;
            if (Commit(next)) return true;
            reason = storageError; return false;
        }
        public static bool MarkAttemptStarted()
        {
            if (!GStarDemo.Enabled || !Current.active || Current.started) return true;
            var next = Copy(Current); next.started = true; return Commit(next);
        }
        public static bool CancelPendingAttempt()
        {
            if (!GStarDemo.Enabled || !PendingAttempt) return false;
            var next = Copy(Current); next.usedAttempts = Math.Max(0, next.usedAttempts - 1);
            next.active = next.started = false; return Commit(next);
        }
        public static bool CompleteAttempt(bool success)
        {
            if (!GStarDemo.Enabled || !Current.active) return false;
            var next = Copy(Current); next.active = next.started = false;
            next.lastRunId = next.runId; next.hasCompleted = true;
            next.lastSuccess = success; next.interrupted = false;
            return Commit(next);
        }
        public static bool SetIdentity(string playerName, string phone)
        {
            var next = Copy(Current);
            next.playerName = (playerName ?? "").Trim(); next.phone = (phone ?? "").Trim();
            return Commit(next);
        }
        public static bool FinishVisitor()
        {
            if (Current.active) return false;
            var next = Copy(Current); next.visitorFinished = true; return Commit(next);
        }
        // Only an explicit booth staff / next-visitor control may call this.
        public static bool ResetVisitor()
        {
            if (Current.active) return false;
            return Commit(NewVisitor());
        }
    }
}
