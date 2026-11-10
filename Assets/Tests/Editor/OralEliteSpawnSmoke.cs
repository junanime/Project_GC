#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Exercise the live scene host's Update, not SpawnOverchargeElite or a mapping-only probe.
    [InitializeOnLoad]
    public static class OralEliteSpawnSmoke
    {
        const string Key = "OralEliteSpawnSmoke", Output = "Library/OralEliteSpawnProof";
        static bool started;
        static double deadline;
        [Serializable] class Entry { public string id; }
        [Serializable] class Catalog { public Entry[] entries; }
        [Serializable] class Pref { public string key; public bool had; public int value; }
        [Serializable] class Backup { public Pref[] prefs; }
        static OralEliteSpawnSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }

        public static void Run()
        {
            Directory.CreateDirectory(Output); Restore();
            File.WriteAllText(Output + "/result.txt", "RUNNING");
            File.WriteAllText(Output + "/checks.txt", ""); File.WriteAllText(Output + "/errors.txt", "");
            var entries = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text).entries;
            var keys = entries.Select(e => TutorialGuide.SeenKey(e.id)).Concat(new[] { "Coins", "LobbySilverCoins" }).Distinct().ToArray();
            File.WriteAllText(Output + "/prefs.json", JsonUtility.ToJson(new Backup { prefs = keys.Select(k => new Pref { key = k, had = PlayerPrefs.HasKey(k), value = PlayerPrefs.GetInt(k) }).ToArray() }));
            foreach (var entry in entries) PlayerPrefs.SetInt(TutorialGuide.SeenKey(entry.id), 1);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key + "Failed", false); SessionState.SetInt(Key + "Checks", 0);
            CrossSceneData.ClearPendingRunSceneTransfer();
            CrossSceneData.CharacterBlueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems = Array.Empty<MerchantItemBlueprint>();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity"); Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started = false; deadline = EditorApplication.timeSinceStartup + 480;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string text, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            File.AppendAllText(Output + "/errors.txt", text + "\n" + stack + "\n"); SessionState.SetBool(Key + "Failed", true);
        }
        static void Restore()
        {
            var path = Output + "/prefs.json"; if (!File.Exists(path)) return;
            foreach (var p in JsonUtility.FromJson<Backup>(File.ReadAllText(path)).prefs)
                if (p.had) PlayerPrefs.SetInt(p.key, p.value); else PlayerPrefs.DeleteKey(p.key);
            PlayerPrefs.Save(); File.Delete(path);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) { File.AppendAllText(Output + "/errors.txt", "Deadline\n"); SessionState.SetBool(Key + "Failed", true); }
            if (SessionState.GetBool(Key + "Done", false) || SessionState.GetBool(Key + "Failed", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Restore(); Time.timeScale = 1; SessionState.SetBool(Key, false);
                bool failed = SessionState.GetBool(Key + "Failed", false);
                File.WriteAllText(Output + "/result.txt", (failed ? "FAIL" : "PASS") + " checks=" + SessionState.GetInt(Key + "Checks", 0));
                EditorApplication.Exit(failed ? 1 : 0); return;
            }
            if (!EditorApplication.isPlaying) return;
            var guide = Object.FindObjectOfType<TutorialGuide>(); if (guide != null) { guide.Close(); guide.enabled = false; }
            var level = Object.FindObjectOfType<LevelManager>();
            if (started || level == null || !level.InitialFieldObjectsReady || level.PlayerCharacter == null) return;
            started = true; level.StartCoroutine(Checks(level));
        }
        static void Check(bool value, string message)
        {
            if (!value) throw new Exception("ORAL_ELITE_FAIL " + message);
            SessionState.SetInt(Key + "Checks", SessionState.GetInt(Key + "Checks", 0) + 1);
            File.AppendAllText(Output + "/checks.txt", "PASS " + message + "\n");
        }
        static FieldInfo Field(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) return field;
            }
            throw new MissingFieldException(obj.GetType().Name, name);
        }
        static object Get(object obj, string name) => Field(obj, name).GetValue(obj);
        static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
        static MonsterBlueprint[] Flat(LevelBlueprint data) => data.monsters.SelectMany(g => g.monsterBlueprints).ToArray();
        static void UseNormal(LevelManager level, int index)
        {
            var weights = new float[Flat(level.CurrentLevelBlueprint).Length]; weights[index] = 1;
            level.CurrentLevelBlueprint.monsterSpawnTable = new MonsterSpawnTable
            {
                spawnChanceKeyframes = new[] { new MonsterSpawnTable.SpawnChanceKeyframe { t = 0, spawnChances = weights } },
                spawnRateKeyframes = new[] { new MonsterSpawnTable.SpawnRateKeyframe { t = 0, spawnRate = 0 } }
            };
        }
        static void Clear(LevelManager level, EliteMonsterSpawner module)
        {
            foreach (var monster in level.EntityManager.LivingMonsters.ToArray()) if (monster != null) monster.DespawnForStageTransfer();
            typeof(EliteMonsterSpawner).GetMethod("CleanupActiveElites", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(module, null);
        }
        static IEnumerator ExpectSpawn(LevelManager level, EliteMonsterSpawner module, MonsterBlueprint expectedSource, string label)
        {
            var active = (List<Monster>)Get(module, "activeElites");
            float until = Time.realtimeSinceStartup + 5;
            while (active.Count == 0 && Time.realtimeSinceStartup < until) yield return null;
            Check(active.Count == 1, label + " creates one actor through live automatic spawner");
            var elite = active[0].Blueprint as EliteMonsterBlueprint;
            Check(elite != null && elite.sourceNormalBlueprint == expectedSource && OralMonsterRoster.IsBlueprint(expectedSource), label + " uses corresponding oral elite and rewards");
            Check(Mathf.Approximately(elite.hpMultiplier, 4.5f) && Mathf.Approximately(elite.scaleMultiplier, 2), label + " keeps oral elite stats");
            Clear(level, module);
        }
        static IEnumerator Checks(LevelManager level)
        {
            var prefs = GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss = false; prefs.muteOnFocusLoss = false;
            GamePreferences.Apply(prefs, false); Application.runInBackground = true; Time.timeScale = 1;
            RunScore.Exclude("Oral automatic elite regression QA");
            level.enabled = false; Set(level.PlayerCharacter, "isInvincible", true); level.PlayerCharacter.CollectableCollider.enabled = false;
            foreach (var ability in Object.FindObjectsOfType<Ability>()) { ability.enabled = false; ability.StopAllCoroutines(); }
            var source = level.CurrentLevelBlueprint; var sourceFlat = Flat(source);
            var sourceData = JsonUtility.ToJson(source);
            Check(StageProgression.Ensure(level).BeginDirectOral(), "real stage zero transition starts");
            float until = Time.realtimeSinceStartup + 120;
            while (StageProgression.Ensure(level).Travelling && Time.realtimeSinceStartup < until) yield return null;
            Check(OralStageRuntime.Instance != null && !level.IsRunFlowPaused, "oral field is ready");
            var oral = level.CurrentLevelBlueprint; var oralFlat = Flat(oral);
            OralFieldEvents.Instance.AutoSchedule = false;
            var host = Object.FindObjectOfType<TimedSpecialMonsterSpawner>();
            var module = ((List<EliteMonsterSpawner>)Get(host, "eliteSpawns")).First(m => m.enabled);
            foreach (var field in host.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (field.Name.StartsWith("initial_", StringComparison.Ordinal)) continue;
                var value = field.GetValue(host);
                if (value is RuntimeModule single) single.enabled = false;
                if (value is IEnumerable list) foreach (var entry in list) if (entry is RuntimeModule item && item != module) item.enabled = false;
            }
            var phase = ((EliteMonsterSpawner.EliteSpawnPhase[])Get(module, "spawnPhases"))[0];
            phase.startTime = 10000; phase.endTime = 0; phase.minSpawnCount = phase.maxSpawnCount = 1;
            Set(module, "spawnPhases", new[] { phase }); Set(module, "debugLog", false);
            Set(module, "rewardedNormalSpawnMilestones", 0); Set(level, "<NormalMonstersSpawned>k__BackingField", 0);
            Clear(level, module); host.enabled = true;
            int milestone = 0;
            foreach (var normal in oralFlat.Where(OralMonsterRoster.IsBlueprint))
            {
                UseNormal(level, Array.IndexOf(oralFlat, normal));
                Set(level, "<NormalMonstersSpawned>k__BackingField", ++milestone * 50);
                yield return ExpectSpawn(level, module, normal, "50-spawn milestone " + normal.name);
            }
            // A scheduled wave must use the same corrected selection even without a new milestone.
            var timedNormal = oralFlat.First(OralMonsterRoster.IsBlueprint);
            UseNormal(level, Array.IndexOf(oralFlat, timedNormal));
            phase.startTime = 150; phase.endTime = 300; phase.spawnInterval = .05f;
            Set(level, "levelTime", 200f);
            yield return ExpectSpawn(level, module, timedNormal, "timed wave");
            phase.startTime = 10000; phase.endTime = 0;
            phase.selectionMode = EliteMonsterSpawner.EliteSelectionMode.ManualFlatIndexPool;
            foreach (int index in Enumerable.Range(0, sourceFlat.Length).Where(i => sourceFlat[i] is EliteMonsterBlueprint))
            {
                phase.eliteMonsterFlatIndices = new[] { index };
                var expected = ((EliteMonsterBlueprint)OralMonsterRoster.Resolve(sourceFlat[index])).sourceNormalBlueprint;
                Set(level, "<NormalMonstersSpawned>k__BackingField", ++milestone * 50);
                yield return ExpectSpawn(level, module, expected, "manual source index " + index);
            }
            int sourceElite = Array.FindIndex(sourceFlat, b => b is EliteMonsterBlueprint);
            phase.selectionMode = EliteMonsterSpawner.EliteSelectionMode.FollowNormalSpawnTable;
            phase.normalToEliteMappings = Array.Empty<EliteMonsterSpawner.NormalToEliteMapping>();
            phase.fallbackToManualPoolIfNoMappedElite = true; phase.eliteMonsterFlatIndices = new[] { 0, sourceElite };
            UseNormal(level, Array.FindIndex(oralFlat, b => !OralMonsterRoster.IsBlueprint(b)));
            Set(level, "<NormalMonstersSpawned>k__BackingField", ++milestone * 50);
            yield return ExpectSpawn(level, module, ((EliteMonsterBlueprint)OralMonsterRoster.Resolve(sourceFlat[sourceElite])).sourceNormalBlueprint, "manual fallback ignores non-elite source index");
            phase.selectionMode = EliteMonsterSpawner.EliteSelectionMode.ManualFlatIndexPool; phase.eliteMonsterFlatIndices = new[] { 0, -1, int.MaxValue };
            Set(level, "<NormalMonstersSpawned>k__BackingField", ++milestone * 50);
            for (int i = 0; i < 8; i++) yield return null;
            Check(((List<Monster>)Get(module, "activeElites")).Count == 0, "invalid manual candidates cannot leak stomach normals or specials");
            module.enabled = false;
            Check(JsonUtility.ToJson(source) == sourceData, "stage-one blueprint remains unchanged");
            Check(StageProgression.Ensure(level).BeginOralToStomach(), "return to stomach starts");
            until = Time.realtimeSinceStartup + 120;
            while (StageProgression.Ensure(level).Travelling && Time.realtimeSinceStartup < until) yield return null;
            Check(level.CurrentLevelBlueprint == source && !OralMonsterRoster.IsLevel(level.CurrentLevelBlueprint), "stomach uses original roster after return");
            Check(!OralMonsterRoster.TryEliteIndex(oral, 0, out _), "oral selection mappings are released on transition");
            SessionState.SetBool(Key + "Done", true);
        }
    }
}
#endif
