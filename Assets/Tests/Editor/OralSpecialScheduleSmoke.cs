using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Kind = Vampire.OralSpecialSpawnProfile.Kind;
using Entry = Vampire.TimedSpecialSpawnSchedule.TimedSpawnEntry;

namespace Vampire.Tests.Editor
{
    // Uses the real scene, runtime schedule, spawn coroutine, entity pools and field
    // transfers. Only the stage clocks/event flags are advanced by the fixture.
    [InitializeOnLoad]
    public static class OralSpecialScheduleSmoke
    {
        const string Key = "OralSpecialScheduleSmoke";
        static bool started;
        static double deadline;
        static int checks;
        [Serializable] class Catalog { public TutorialGuide.Entry[] entries; }
        [Serializable] class Entries { public Entry[] entries; }
        sealed class Event
        {
            public TimedSpecialSpawnSchedule module;
            public object runtime;
            public Entry entry => (Entry)Get(runtime, "entry");
            public float time => (float)Get(runtime, "scheduledTime");
        }
        static OralSpecialScheduleSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys = catalog.entries.Select(e => TutorialGuide.SeenKey(e.id)).Concat(new[] { "Coins", "LobbySilverCoins" }).Distinct().ToArray();
            SessionState.SetString(Key + "Prefs", string.Join("|", keys));
            foreach (var key in keys)
            {
                SessionState.SetBool(Key + key + "Exists", PlayerPrefs.HasKey(key));
                SessionState.SetInt(Key + key, PlayerPrefs.GetInt(key));
            }
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key + "Failed", false);
            CrossSceneData.ClearPendingRunSceneTransfer();
            CrossSceneData.CharacterBlueprint = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").characters.First();
            CrossSceneData.StartingLobbyItems = Array.Empty<MerchantItemBlueprint>();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started = false; checks = 0; deadline = EditorApplication.timeSinceStartup + 420;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) SessionState.SetBool(Key + "Failed", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) SessionState.SetBool(Key + "Failed", true);
            if (SessionState.GetBool(Key + "Done", false) || SessionState.GetBool(Key + "Failed", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                foreach (var key in SessionState.GetString(Key + "Prefs", "").Split('|'))
                    if (SessionState.GetBool(Key + key + "Exists", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(Key + key, 0));
                    else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                bool failed = SessionState.GetBool(Key + "Failed", false); SessionState.SetBool(Key, false);
                Debug.Log("ORAL_SPECIAL_SCHEDULE_FINISHED failed=" + failed + " checks=" + checks);
                EditorApplication.Exit(failed ? 1 : 0); return;
            }
            if (!EditorApplication.isPlaying) return;
            var guide = Object.FindObjectOfType<TutorialGuide>();
            if (guide != null) { guide.Close(); guide.enabled = false; }
            var level = Object.FindObjectOfType<LevelManager>();
            if (started || level?.PlayerCharacter == null || !level.InitialFieldObjectsReady || StageEntryLoading.IsLoading) return;
            started = true; level.StartCoroutine(Checks(level));
        }
        static void Check(bool pass, string message)
        {
            if (!pass) throw new Exception("ORAL_SPECIAL_SCHEDULE_FAIL " + message);
            checks++; Debug.Log("ORAL_SPECIAL_SCHEDULE_PASS " + message);
        }
        static FieldInfo Field(object owner, string name)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                var result = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (result != null) return result;
            }
            throw new MissingFieldException(owner.GetType().Name, name);
        }
        static object Get(object owner, string name) => Field(owner, name).GetValue(owner);
        static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
        static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        static List<TimedSpecialSpawnSchedule> Modules(TimedSpecialMonsterSpawner host) => (List<TimedSpecialSpawnSchedule>)Get(host, "timedSpawns");
        static Entry[] Source(TimedSpecialMonsterSpawner host) => Modules(host).SelectMany(m => (Entry[])Get(m, "spawnEntries")).ToArray();
        static string Signature(TimedSpecialMonsterSpawner host) => JsonUtility.ToJson(new Entries { entries = Source(host) });
        static List<Event> Events(TimedSpecialMonsterSpawner host) => Modules(host).Where(m => m.moduleEnabled).SelectMany(m => ((IEnumerable)Get(m, "runtimeTimedSpawnEvents")).Cast<object>().Select(e => new Event { module = m, runtime = e })).ToList();
        // Dormant traps intentionally receive activeHealth only when triggered.
        static Monster[] Living(LevelManager level, Kind kind) => level.EntityManager.LivingMonsters.Where(m => m != null && m.gameObject.activeInHierarchy && (m.HP > 0 || m is TrapMonster) && OralSpecialSpawnProfile.Identify(level.CurrentLevelBlueprint, m.Blueprint) == kind).ToArray();
        static int Total(LevelManager level) => level.EntityManager.LivingMonsters.Count(m => m != null && m.gameObject.activeInHierarchy && m.HP > 0 && OralSpecialSpawnProfile.Identify(level.CurrentLevelBlueprint, m.Blueprint) != Kind.None && OralSpecialSpawnProfile.Identify(level.CurrentLevelBlueprint, m.Blueprint) != Kind.Trap);
        static void Clear(LevelManager level)
        {
            foreach (var monster in level.EntityManager.LivingMonsters.ToArray()) if (monster != null) monster.DespawnForStageTransfer();
        }
        static void Silence(LevelManager level, TimedSpecialMonsterSpawner host)
        {
            level.enabled = false;
            foreach (var behaviour in Object.FindObjectsOfType<MonoBehaviour>())
                if (behaviour is StageEventDirector || behaviour is OralFieldEvents || behaviour is AcidToadSpawn || behaviour is NPCSpawner ||
                    behaviour is SnailBossDebugSpawn || behaviour is SupplyCapsuleDirector || behaviour is ColonSpecialDirector)
                { behaviour.enabled = false; behaviour.StopAllCoroutines(); }
            foreach (var ability in Object.FindObjectsOfType<Ability>()) { ability.enabled = false; ability.StopAllCoroutines(); }
            var modules = (IEnumerable<RuntimeModule>)typeof(TimedSpecialMonsterSpawner).GetProperty("Modules", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
            foreach (var module in modules) if (!(module is TimedSpecialSpawnSchedule)) module.enabled = false;
            Clear(level);
        }
        static IEnumerator TransferFinished(StageProgression flow)
        {
            while (flow.Travelling || StageEntryLoading.IsLoading) yield return null;
            yield return null;
        }
        static IEnumerator Frames() { for (int i = 0; i < 3; i++) yield return null; }
        static IEnumerator Fire(LevelManager level, Event e, float time)
        {
            Set(level, "levelTime", time); Set(e.module, "elapsedTime", time); Set(e.runtime, "spawned", false);
            yield return Frames();
            Check((bool)Get(e.runtime, "spawned"), e.entry.memo + " runtime event consumed");
            foreach (var monster in level.EntityManager.LivingMonsters) if (monster != null) monster.enabled = false;
        }
        static IEnumerator Checks(LevelManager level)
        {
            Application.runInBackground = true;
            var preferences = GamePreferences.Current.Copy(); preferences.pauseOnFocusLoss = false; GamePreferences.Apply(preferences, false);
            Time.timeScale = 1; RunScore.Exclude("Oral special schedule regression");
            Set(level.PlayerCharacter, "currentHealth", 100000f);
            var host = Object.FindObjectOfType<TimedSpecialMonsterSpawner>(); var flow = StageProgression.Ensure(level);
            Check(host != null && flow.StageNumber == 1, "live stomach source found");
            Silence(level, host);
            string sourceSignature = Signature(host);
            var sourceEntries = Source(host); var sourceJson = sourceEntries.Select(JsonUtility.ToJson).ToArray();
            var stomachEntries = Events(host).Select(e => JsonUtility.ToJson(e.entry)).Distinct().OrderBy(x => x).ToArray();
            Check(flow.BeginDirectOral(), "real oral entry accepted"); yield return TransferFinished(flow); Silence(level, host);
            Check(flow.StageNumber == 0 && Signature(host) == sourceSignature, "oral entry retains all serialized stomach entries");
            var events = Events(host);
            var first = events.GroupBy(e => OralSpecialSpawnProfile.Identify(level.CurrentLevelBlueprint, e.entry.monsterBlueprint)).ToDictionary(g => g.Key, g => g.OrderBy(e => e.time).First());
            var expectedKinds = new[] { Kind.Sniper, Kind.Popcorn, Kind.Spore, Kind.Sugar, Kind.Thief, Kind.Support, Kind.Debuffer, Kind.EliteSugar };
            float[] introductions = { 60, 90, 120, 150, 180, 240, 270, 300 };
            Check(first.Count == expectedKinds.Length && expectedKinds.All(first.ContainsKey), "all eight timed kinds have runtime entries");
            for (int i = 0; i < expectedKinds.Length; i++)
            {
                var kind = expectedKinds[i]; var scheduled = events.Where(e => OralSpecialSpawnProfile.Identify(level.CurrentLevelBlueprint, e.entry.monsterBlueprint) == kind).OrderBy(e => e.time).ToArray();
                Check(Mathf.Abs(scheduled[0].time - introductions[i]) < .001f && scheduled.All(e => e.time < 360), kind + " first appearance and six-minute end");
                Check(scheduled.All(e => (int)Get(e.runtime, "spawnCount") == (kind == Kind.Support ? 2 : 1)), kind + " fixed wave size");
                float minimum = kind == Kind.Popcorn || kind == Kind.Debuffer ? 110 : kind == Kind.Thief ? 120 : 90;
                Check(kind == Kind.EliteSugar ? scheduled.Length == 1 : scheduled.Skip(1).Select((e, n) => e.time - scheduled[n].time).All(gap => gap >= minimum - .001f && gap <= minimum + 20.001f), kind + " repeat interval or one-shot");
                Check(scheduled.All(e => !sourceEntries.Any(s => ReferenceEquals(s, e.entry))), kind + " uses copied runtime settings");
            }
            var traps = Modules(host).Where(m => m.moduleEnabled).SelectMany(m => ((IEnumerable)Get(m, "runtimeTrapFieldStates")).Cast<object>().Select(s => new { module = m, state = s })).ToArray();
            Check(traps.Length == 1, "one active trap-maintenance schedule");
            var trap = traps[0]; var trapEntry = (Entry)Get(trap.state, "entry");
            Check(!trapEntry.trapSpawnOnStart && trapEntry.trapActivateTimeSeconds == 210 && trapEntry.maxActiveTraps == 2 && trapEntry.respawnDelay == 60, "trap starts at 210 seconds, maintains two, respawns after sixty");
            Set(trap.state, "activated", true); // Test timed waves independently before exercising trap maintenance below.
            foreach (var e in events) Set(e.runtime, "spawned", true);
            Set(level, "levelTime", 59f); foreach (var m in Modules(host)) Set(m, "elapsedTime", 59f);
            Set(first[Kind.Sniper].runtime, "spawned", false); yield return Frames();
            Check(Total(level) == 0, "no timed special before first introduction");
            foreach (var kind in expectedKinds)
            {
                Clear(level); yield return Fire(level, first[kind], first[kind].time + .01f);
                Check(Living(level, kind).Length == (kind == Kind.Support ? 2 : 1), kind + " actual pooled spawn at first introduction");
                if (kind == Kind.Support)
                    Check(first[kind].entry.companionBlueprint != null && Living(level, kind).Select(m => m.Blueprint).Distinct().Count() == 2, "support wave contains both companion blueprints");
                int count = Total(level); yield return Frames(); Check(Total(level) == count, kind + " event cannot duplicate on another frame");
            }
            // Populate the caps through the production coroutine, not fake actors.
            Clear(level);
            yield return Fire(level, first[Kind.Sniper], 330); yield return Fire(level, first[Kind.Sniper], 330); yield return Fire(level, first[Kind.Sniper], 330);
            Check(Living(level, Kind.Sniper).Length == 2, "sniper species cap blocks third live body");
            yield return Fire(level, first[Kind.Spore], 330); yield return Fire(level, first[Kind.Spore], 330);
            yield return Fire(level, first[Kind.Popcorn], 330); yield return Fire(level, first[Kind.Popcorn], 330);
            Check(Total(level) == 5 && Living(level, Kind.Popcorn).Length == 1, "popcorn species cap retains one live body");
            yield return Fire(level, first[Kind.Support], 330);
            Check(Total(level) == 5 && Living(level, Kind.Support).Length == 0, "one free slot cannot create half a support pair");
            Living(level, Kind.Spore)[0].DespawnForStageTransfer(); yield return Fire(level, first[Kind.Support], 330);
            Check(Total(level) == 6 && Living(level, Kind.Support).Length == 2, "two free slots admit companion pair exactly to global cap");
            yield return Fire(level, first[Kind.Debuffer], 330); Check(Total(level) == 6 && Living(level, Kind.Debuffer).Length == 0, "global cap rejects additional kind");
            Living(level, Kind.Popcorn)[0].DespawnForStageTransfer(); yield return Fire(level, first[Kind.Sugar], 330);
            var sugar = Living(level, Kind.Sugar).Single(); var sugarBlueprint = (SugarCubeMonsterBlueprint)sugar.Blueprint;
            Check(sugarBlueprint.CanSplit(), "normal sugar has its real split-child definition");
            Call(sugar, "TrySpawnSplitChildren", true); sugar.DespawnForStageTransfer();
            Check(Total(level) == 5 + sugarBlueprint.splitChildCount, "sugar death splitting remains allowed above timed-spawn cap");
            Clear(level); Set(trap.state, "activated", false); ((IList)Get(trap.state, "activeTraps")).Clear();
            Set(level, "levelTime", 209f); Set(trap.module, "elapsedTime", 209f); yield return Frames();
            Check(Living(level, Kind.Trap).Length == 0, "no trap before activation");
            Set(level, "levelTime", 210.01f); Set(trap.module, "elapsedTime", 210.01f); yield return Frames();
            Check(Living(level, Kind.Trap).Length == 2, "actual trap field initializes exactly two");
            foreach (var m in Living(level, Kind.Trap)) m.enabled = false;
            var killed = Living(level, Kind.Trap)[0]; killed.OnKilled.Invoke(killed); killed.DespawnForStageTransfer();
            Time.timeScale = 20; float began = Time.time;
            while (Time.time - began < 58) yield return null;
            Check(Living(level, Kind.Trap).Length == 1, "trap replacement waits for its sixty-second timer");
            while (Time.time - began < 62) yield return null;
            Time.timeScale = 1;
            Check(Living(level, Kind.Trap).Length == 2, "real trap death callback replaces one after sixty seconds");
            killed = Living(level, Kind.Trap)[0]; killed.OnKilled.Invoke(killed); killed.DespawnForStageTransfer();
            Set(level, "levelTime", 360f); Time.timeScale = 20; began = Time.time;
            while (Time.time - began < 62) yield return null;
            Time.timeScale = 1;
            Check(Living(level, Kind.Trap).Length == 1, "queued trap replenishment is blocked at six minutes");
            Clear(level); Set(trap.state, "activated", false); ((IList)Get(trap.state, "activeTraps")).Clear(); Set(trap.module, "elapsedTime", 360f);
            yield return Frames(); Check(Living(level, Kind.Trap).Length == 0, "late first trap activation cannot spawn after six minutes");
            foreach (var kind in expectedKinds) { yield return Fire(level, first[kind], 360); Check(Total(level) == 0, kind + " has no overdue replenishment after six minutes"); }
            var oral = level.CurrentLevelBlueprint; var flat = oral.monsters.SelectMany(g => g.monsterBlueprints).ToArray();
            Check(Enumerable.Range(0, 361).All(t => Enumerable.Range(0, flat.Length).All(i => oral.monsterSpawnTable.GetProbability(t / 360f, i) == 0 || OralMonsterRoster.IsBlueprint(flat[i]))), "ordinary spawn table cannot bypass timed-special schedule anywhere in six-minute field");
            Check(Signature(host) == sourceSignature, "original stomach entries remain unchanged after all oral waves");
            Check(flow.BeginOralToStomach(), "real oral-to-stomach return accepted"); yield return TransferFinished(flow); Silence(level, host);
            Check(flow.StageNumber == 1 && level.CurrentLevelTime == 0 && Signature(host) == sourceSignature, "stomach return restores original source and resets stage clock");
            Check(Events(host).Select(e => JsonUtility.ToJson(e.entry)).Distinct().OrderBy(x => x).SequenceEqual(stomachEntries), "all stomach runtime entry settings restored exactly");
            Check(flow.BeginDirectColon(), "real colon entry accepted"); yield return TransferFinished(flow); Silence(level, host);
            Check(flow.StageNumber == 2 && Signature(host) == sourceSignature, "colon transfer retains original serialized schedules");
            Check(Events(host).Count > 0 && Events(host).All(e => sourceJson.Contains(JsonUtility.ToJson(e.entry)) && !e.entry.memo.StartsWith("Stage 0")), "colon legacy runtime keeps stomach timings and counts, without oral adaptations");
            var colon = Object.FindObjectOfType<ColonSpecialDirector>();
            Check(colon != null && ((float[])Get(colon, "next")).SequenceEqual(new[] { 120f, 240f, 360f, 450f, 540f }), "colon new-special introductions retain two/four/six/seven-and-a-half/nine minutes");
            Time.timeScale = 1; SessionState.SetBool(Key + "Done", true);
        }
    }
}
