using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace Vampire
{
    public sealed class FestivalQaRecorder : MonoBehaviour
    {
        static FestivalQaRecorder instance;
        readonly ConcurrentQueue<Signal> pending = new ConcurrentQueue<Signal>();
        int queued;
        long overflow;
        string station, build, folder;
        volatile string context = "menu", runContext = "";
        LevelManager level;
        FestivalQaRun run;
        readonly System.Collections.Generic.List<FestivalQaRun> unsavedRuns = new System.Collections.Generic.List<FestivalQaRun>();
        FestivalQaFrameMeter meter;
        FestivalQaErrors errors;
        double nextSave;
        bool errorsDirty;
        public static string Problem { get; private set; }
        struct Signal { public string kind, message, trace, context, run; }
        [Serializable] sealed class BuildStamp { public string sourceCommit; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { instance = null; Problem = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (FestivalDemo.Enabled && instance == null) new GameObject("Festival minimal QA").AddComponent<FestivalQaRecorder>();
        }
        void Awake()
        {
            if (instance != null) { Destroy(gameObject); return; }
            instance = this; DontDestroyOnLoad(gameObject);
            try
            {
                folder = FestivalQaStore.Folder; station = FestivalQaStore.Station(folder); build = Application.version;
                string stamp = Path.Combine(Application.dataPath, "..", "desktop-build.json");
                if (File.Exists(stamp)) build = JsonUtility.FromJson<BuildStamp>(File.ReadAllText(stamp))?.sourceCommit ?? build;
#if UNITY_EDITOR
                build = "editor";
#endif
                errors = new FestivalQaErrors { station = station, session = Guid.NewGuid().ToString("N"), utc = DateTime.UtcNow.ToString("o"), build = build };
                nextSave = Time.realtimeSinceStartupAsDouble + 5;
                Application.logMessageReceivedThreaded += OnLog;
            }
            catch (Exception) { Problem = "QA 기록 폴더를 열 수 없습니다. 저장 공간과 권한을 확인해 주세요."; }
        }
        public static void Begin(LevelManager owner, string id)
        {
            if (!FestivalDemo.Enabled || owner == null) return;
            Boot(); if (instance.errors == null || instance.run?.run == id) return;
            if (instance.run != null) End(instance.run.run);
            instance.level = owner;
            instance.run = new FestivalQaRun { station = instance.station, run = id, character = OctoberArt.CharacterKey(owner.PlayerCharacter.Blueprint),
                utc = DateTime.UtcNow.ToString("o"), rules = FestivalDemo.Rules, build = instance.build };
            instance.runContext = id; instance.meter = new FestivalQaFrameMeter(instance.run);
            instance.Flush(); // Independent of leaderboard registration.
        }
        public static void End(string id)
        {
            if (instance == null || instance.run == null || instance.run.run != id) return;
            instance.run.closed = true;
            instance.unsavedRuns.Add(instance.run);
            instance.run = null; instance.level = null; instance.meter = null;
            instance.runContext = ""; instance.context = "menu";
            instance.Flush();
        }
        public static void Checkpoint() { if (instance != null) instance.Flush(); }
        void LateUpdate()
        {
            if (errors == null || !FestivalDemo.Enabled) return;
            bool loading = StageEntryLoading.IsLoading;
            bool active = level != null && run != null && !level.IsLevelEnded && !level.IsRunFlowPaused && !loading
                && Time.timeScale > 0 && level.PlayerCharacter != null && level.PlayerCharacter.IsAlive && Application.isFocused;
            context = loading ? "loading" : active ? "gameplay" : run != null ? "paused" : "menu";
            double now = Time.realtimeSinceStartupAsDouble;
            meter?.Observe(now, active); DrainErrors();
            if (now >= nextSave) { Flush(); nextSave = now + 5; }
        }
        void OnApplicationFocus(bool focused) { meter?.ResetBoundary(); if (!focused) Flush(); }
        void OnApplicationPause(bool paused) { meter?.ResetBoundary(); if (paused) Flush(); }
        void OnApplicationQuit() { if (run != null) run.closed = true; Flush(); }
        void OnDestroy()
        {
            if (instance != this) return;
            Application.logMessageReceivedThreaded -= OnLog; Flush(); instance = null;
        }
        void OnLog(string message, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (Interlocked.Increment(ref queued) > 256) { Interlocked.Decrement(ref queued); Interlocked.Increment(ref overflow); return; }
            pending.Enqueue(new Signal { kind = type.ToString(), message = Limit(message, 2000), trace = Limit(trace, 8000), context = context, run = runContext });
        }
        static string Limit(string text, int length) => string.IsNullOrEmpty(text) ? "" : text.Substring(0, Math.Min(length, text.Length));
        // Free-form messages and paths may contain personal data; persist only type/method names.
        public static FestivalQaError Describe(string kind, string message, string trace, string context, string run)
        {
            var category = Regex.Match(message ?? "", @"^([A-Za-z_][A-Za-z0-9_.]*(?:Exception|Error))\b");
            var methods = (trace ?? "").Split('\n').Select(line => Regex.Match(line.Trim(), @"^(?:at )?([A-Za-z_][A-Za-z0-9_.$+<>`]*[.:][A-Za-z0-9_.$+<>`]+)\s*\("))
                .Where(m => m.Success).Take(10).Select(m => m.Groups[1].Value);
            return new FestivalQaError { kind = kind, category = category.Success ? category.Groups[1].Value : kind,
                methods = string.Join(" > ", methods), context = context, run = run, count = 1 };
        }
        void DrainErrors()
        {
            if (errors == null) return;
            while (pending.TryDequeue(out var signal))
            {
                Interlocked.Decrement(ref queued);
                var row = Describe(signal.kind, signal.message, signal.trace, signal.context, signal.run);
                var existing = errors.errors.FirstOrDefault(e => e.kind == row.kind && e.category == row.category && e.methods == row.methods && e.context == row.context && e.run == row.run);
                if (existing != null) existing.count++;
                else if (errors.errors.Count < 256) errors.errors.Add(row); else errors.overflow++;
                errorsDirty = true;
            }
            long dropped = Interlocked.Exchange(ref overflow, 0);
            if (dropped > 0) { errors.overflow += dropped; errorsDirty = true; }
        }
        void Flush()
        {
            if (errors == null) return;
            DrainErrors(); bool ok = run == null || FestivalQaStore.Save(run, folder);
            for (int i = unsavedRuns.Count - 1; i >= 0; i--)
            {
                if (FestivalQaStore.Save(unsavedRuns[i], folder)) unsavedRuns.RemoveAt(i);
                else ok = false;
            }
            if (errorsDirty) { bool saved = FestivalQaStore.Save(errors, folder); errorsDirty = !saved; ok &= saved; }
            Problem = ok ? null : "QA 저장에 실패했습니다. 운영자에게 알려 주세요.";
        }
    }
}
