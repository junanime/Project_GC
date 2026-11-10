using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Runs the real field transfers and spawner. EntityManager rejection alone cannot
    // pass: every blocked case must leave the variant lottery unselected as well.
    [InitializeOnLoad]
    public static class ChameleonStageScopeSmoke
    {
        const string Key = "ChameleonStageScopeSmoke";
        static bool started;
        static double deadline;
        static int checks;
        [Serializable] class Catalog { public TutorialGuide.Entry[] entries; }
        static ChameleonStageScopeSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
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
            started = false; checks = 0; deadline = EditorApplication.timeSinceStartup + 360;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                SessionState.SetBool(Key + "Failed", true);
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
                Debug.Log("CHAMELEON_SCOPE_FINISHED failed=" + failed + " checks=" + checks);
                EditorApplication.Exit(failed ? 1 : 0); return;
            }
            if (!EditorApplication.isPlaying) return;
            var guide = Object.FindObjectOfType<TutorialGuide>();
            if (guide != null) { guide.Close(); guide.enabled = false; }
            var level = Object.FindObjectOfType<LevelManager>();
            if (started || level?.PlayerCharacter == null || !level.InitialFieldObjectsReady || StageEntryLoading.IsLoading) return;
            started = true; level.StartCoroutine(Checks(level));
        }
        static void Check(bool value, string message)
        {
            if (!value) throw new Exception("CHAMELEON_SCOPE_FAIL " + message);
            checks++; Debug.Log("CHAMELEON_SCOPE_PASS " + message);
        }
        static FieldInfo Field(object owner, string name)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) return field;
            }
            throw new MissingFieldException(owner.GetType().Name, name);
        }
        static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
        static void TickSpawn(AcidToadSpawn spawn) => typeof(AcidToadSpawn).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawn, null);
        static int LiveCount(LevelManager level) => level.EntityManager.LivingMonsters.Count(m => m is AcidToadMonster && m.gameObject.activeInHierarchy);
        static void Blocked(LevelManager level, AcidToadSpawn spawn, string label)
        {
            int before = LiveCount(level);
            TickSpawn(spawn); TickSpawn(spawn);
            Check(!spawn.Spawned && !(bool)Field(spawn, "selected").GetValue(spawn) && LiveCount(level) == before,
                label + " neither selects a variant nor spawns");
        }
        static void Silence(LevelManager level)
        {
            level.enabled = false;
            foreach (var behaviour in Object.FindObjectsOfType<MonoBehaviour>())
                if (behaviour is StageEventDirector || behaviour is TimedSpecialMonsterSpawner || behaviour is OralFieldEvents ||
                    behaviour is AcidToadSpawn || behaviour is NPCSpawner || behaviour is SnailBossDebugSpawn || behaviour is SupplyCapsuleDirector)
                { behaviour.enabled = false; behaviour.StopAllCoroutines(); }
            foreach (var ability in Object.FindObjectsOfType<Ability>()) { ability.enabled = false; ability.StopAllCoroutines(); }
            foreach (var monster in level.EntityManager.LivingMonsters.ToArray()) if (monster != null) monster.DespawnForStageTransfer();
        }
        static IEnumerator TransferFinished(StageProgression flow)
        {
            while (flow.Travelling || StageEntryLoading.IsLoading) yield return null;
            yield return null;
        }
        static IEnumerator Checks(LevelManager level)
        {
            Application.runInBackground = true;
            var preferences = GamePreferences.Current.Copy(); preferences.pauseOnFocusLoss = false; GamePreferences.Apply(preferences, false);
            Time.timeScale = 1; RunScore.Exclude("Chameleon stage-scope regression");
            var player = level.PlayerCharacter; Set(player, "currentHealth", 100000f);
            var spawn = Object.FindObjectOfType<AcidToadSpawn>(); var flow = StageProgression.Ensure(level);
            Check(spawn != null && flow.StageNumber == 1, "live stomach spawner found");
            Silence(level);
            spawn.ResetForStage(); spawn.Record(ChameleonKind.Drift);
            Check(Mathf.Abs(spawn.Lottery.Weight(ChameleonKind.Drift) - 45) < .001f, "stomach field event still adds twenty percentage points");
            Check(flow.BeginDirectOral(), "real oral entry accepted");
            yield return TransferFinished(flow); Silence(level);
            Check(flow.StageNumber == 0 && !level.IsRunFlowPaused, "oral field ready");
            Set(level, "levelTime", 1200f);
            Blocked(level, spawn, "oral long stay");
            spawn.Record(ChameleonKind.Fanta);
            Check(spawn.Lottery.Weight(ChameleonKind.Fanta) == 25, "oral events do not alter stomach lottery");
            spawn.enabled = true; yield return null; spawn.enabled = false;
            Blocked(level, spawn, "oral re-enable after scheduled time");
            Check(flow.BeginOralToStomach(), "real oral-to-stomach return accepted");
            yield return TransferFinished(flow); Silence(level);
            Check(flow.StageNumber == 1 && level.CurrentLevelTime == 0 && !spawn.Spawned, "stomach return resets clock and one-shot flag");
            Check(Enumerable.Range(0, 4).All(k => spawn.Lottery.Weight((ChameleonKind)k) == 25), "stomach return resets all four weights");
            Check(spawn.SpawnAt >= 420 && spawn.SpawnAt <= 450, "stomach return retains seven to seven-and-a-half minute window");
            Set(level, "levelTime", spawn.SpawnAt - .01f); Blocked(level, spawn, "before scheduled time");
            Set(level, "levelTime", 600f);
            MiniStageRuntimeState.EnterMiniStage(null); Blocked(level, spawn, "mini-stage"); MiniStageRuntimeState.ForceClear();
            level.SetRunFlowPaused(true); Blocked(level, spawn, "paused field"); level.SetRunFlowPaused(false);
            Time.timeScale = 0; Blocked(level, spawn, "pause menu"); Time.timeScale = 1;
            Set(level, "levelEnded", true); Blocked(level, spawn, "finished run"); Set(level, "levelEnded", false);
            Set(player, "alive", false); Blocked(level, spawn, "dead player"); Set(player, "alive", true);
            var loading = StageEntryLoading.CreateStomachTransfer(player.Blueprint);
            Check(StageEntryLoading.IsLoading && !level.IsRunFlowPaused, "independent loading blocker exercised");
            Blocked(level, spawn, "loading overlay"); loading.CompleteStageTransfer(); yield return null;

            for (int k = 0; k < 4; k++)
            {
                spawn.ResetForStage();
                Check(spawn.SpawnAt >= 420 && spawn.SpawnAt <= 450, "variant " + k + " scheduled in original window");
                var kind = (ChameleonKind)k;
                for (int n = 0; n < 20; n++) spawn.Record(kind);
                Check(Mathf.Abs(spawn.Lottery.Weight(kind) - 100) < .001f, kind + " existing event lottery remains selectable");
                Set(level, "levelTime", spawn.SpawnAt);
                TickSpawn(spawn);
                var actual = level.EntityManager.LivingMonsters.OfType<AcidToadMonster>().SingleOrDefault(m => m.gameObject.activeInHierarchy);
                Check(spawn.Spawned && spawn.Selected == kind && actual != null && actual.Kind == kind, kind + " spawns once at scheduled time");
                actual.AutoPatterns = false;
                spawn.Record((ChameleonKind)((k + 1) % 4));
                Check(Mathf.Abs(spawn.Lottery.Weight(kind) - 100) < .001f, kind + " selection remains locked");
                // Late enough to catch missed scheduling without activating the unrelated acid flood.
                Set(level, "levelTime", 600f);
                spawn.enabled = true; yield return null; spawn.enabled = false;
                TickSpawn(spawn); Check(LiveCount(level) == 1, kind + " re-enable and overdue time do not duplicate");
                actual.DespawnForStageTransfer(); TickSpawn(spawn);
                Check(spawn.Spawned && LiveCount(level) == 0, kind + " removal does not repeat the stage encounter");
            }

            Set(level, "levelTime", 0f);
            Check(flow.BeginDirectColon(), "real colon transfer accepted");
            yield return TransferFinished(flow); Silence(level);
            Check(flow.StageNumber == 2 && !level.IsRunFlowPaused && !spawn.Spawned, "colon transfer resets spawner without activating it");
            Set(level, "levelTime", 1200f); Blocked(level, spawn, "colon long stay");
            spawn.Record(ChameleonKind.Latte);
            Check(spawn.Lottery.Weight(ChameleonKind.Latte) == 25, "colon events do not alter stomach lottery");
            spawn.enabled = true; yield return null; spawn.enabled = false;
            Blocked(level, spawn, "colon re-enable after scheduled time");
            SessionState.SetBool(Key + "Done", true);
        }
    }
}
