using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Parent = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class MainCampaignAugmentSmoke
    {
        const string Key = "QA.MainCampaignAugments";
        const string Report = "Library/MainCampaignAugmentSmoke.txt";
        static readonly string[] Roster = { "Ashi", "Ari", "Hyuki", "Shini" };
        static bool running;
        static double deadline;

        static MainCampaignAugmentSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }

        public static void Run()
        {
            SessionState.SetBool(Key, true);
            SessionState.SetString(Key + ".result", "");
            File.WriteAllText(Report, "");
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            Attach();
            EditorApplication.EnterPlaymode();
        }

        static void Attach()
        {
            deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Key, false) ||
                (type != LogType.Error && type != LogType.Assert && type != LogType.Exception)) return;
            File.AppendAllText(Report, message + "\n" + stack + "\n");
            SessionState.SetString(Key + ".result", "FAIL");
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) SessionState.SetString(Key + ".result", "TIMEOUT");
            string result = SessionState.GetString(Key + ".result", "");
            if (result.Length > 0)
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                foreach (var pref in SessionState.GetString(Key + ".prefs", "").Split('|'))
                {
                    if (string.IsNullOrEmpty(pref)) continue;
                    if (SessionState.GetBool(Key + pref + ".exists", false))
                        PlayerPrefs.SetInt(pref, SessionState.GetInt(Key + pref, 0));
                    else PlayerPrefs.DeleteKey(pref);
                }
                PlayerPrefs.Save();
                SessionState.SetBool(Key, false);
                File.AppendAllText(Report, result + "\n");
                EditorApplication.Exit(result == "PASS" ? 0 : 1);
                return;
            }
            if (!EditorApplication.isPlaying || running || Object.FindObjectOfType<LevelManager>() == null) return;
            running = true;
            var host = new GameObject("Campaign augment regression");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<StageEntryLoadingTestHost>().StartCoroutine(CheckRoster());
        }

        static void Check(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
            File.AppendAllText(Report, "PASS " + message + "\n");
        }

        static IEnumerator CheckRoster()
        {
            var config = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            string pierceUnlock = "LobbyUnlock.Needle." + Parent.Pierce;
            var keys = config.characters.Where(c => c != null).Select(StartingNeedleSelection.SaveKey)
                .Concat(new[] { pierceUnlock, StartingNeedleSelection.EnabledKey(Parent.Pierce) }).Distinct().ToArray();
            SessionState.SetString(Key + ".prefs", string.Join("|", keys));
            foreach (string pref in keys)
            {
                SessionState.SetBool(Key + pref + ".exists", PlayerPrefs.HasKey(pref));
                SessionState.SetInt(Key + pref, PlayerPrefs.GetInt(pref));
            }
            PlayerPrefs.SetInt(pierceUnlock, 1);
            PlayerPrefs.SetInt(StartingNeedleSelection.EnabledKey(Parent.Pierce), 1);
            foreach (string name in Roster)
            {
                var blueprint = config.characters.First(c => c != null && OctoberArt.CharacterKey(c) == name);
                var signature = (Parent)StartingNeedleSelection.DefaultId(blueprint);
                PlayerPrefs.SetInt(StartingNeedleSelection.SaveKey(blueprint), (int)Parent.Pierce);
                CrossSceneData.CharacterBlueprint = blueprint;
                CrossSceneData.ClearStartingLobbyItems();
                CrossSceneData.ClearPendingRunSceneTransfer();
                yield return SceneManager.LoadSceneAsync("Level 1");
                var field = Object.FindObjectOfType<LevelManager>();
                while (field.PlayerCharacter == null || !field.InitialFieldObjectsReady) yield return null;
                yield return null;
                var player = field.PlayerCharacter;
                var manager = Object.FindObjectOfType<AbilityManager>();
                var runtime = manager.Ver4;
                var dart = SyringeAbilityResolver.FindOwnedOrFirst(manager);
                var shot = dart.GetCurrentSpecialRuntime();
                Check(OctoberArt.CharacterKey(player.Blueprint) == name, name + " real field character");
                Check(shot.pierceEnabled && shot.ver4.Has(signature) &&
                    Enumerable.Range(0, 3).All(i => shot.ver4.Count(signature, i) == 1),
                    name + " selected piercing weapon and all three signature effects in live projectile state");
                var seed = runtime.CaptureRunSceneConditionalAugmentIds();
                StartingNeedleSelection.Apply(manager, blueprint);
                Check(seed.Count == 3 && runtime.CaptureRunSceneConditionalAugmentIds().SequenceEqual(seed),
                    name + " repeated initialization is idempotent");
                if (name != "Ashi") continue;

                var legacy = new[] { seed[0], seed[0] };
                Check(runtime.RestoreRunSceneConditionalAugments(legacy), "old save lacking two innate ranks loads");
                Check(runtime.Progress.Count(signature.ToString(), 0) == 2 &&
                      runtime.Progress.Count(signature.ToString(), 1) == 1 &&
                      runtime.Progress.Count(signature.ToString(), 2) == 1, "old 2/0/0 becomes 2/1/1");
                var migrated = runtime.CaptureRunSceneConditionalAugmentIds();
                Check(runtime.RestoreRunSceneConditionalAugments(legacy) && runtime.RestoreRunSceneConditionalAugments(migrated) &&
                      runtime.CaptureRunSceneConditionalAugmentIds().SequenceEqual(migrated), "both save versions reload without another grant");
                var savedLoadout = manager.CaptureRunSceneAbilities();
                yield return CheckOrbit(field, dart);
                PlayerPrefs.SetInt(StartingNeedleSelection.EnabledKey(Parent.Pierce), 0);
                yield return manager.ResetForStage(field.CurrentLevelBlueprint, field.EntityManager, player, savedLoadout);
                var restored = SyringeAbilityResolver.FindOwnedOrFirst(manager).GetCurrentSpecialRuntime();
                Check(restored.pierceEnabled && restored.ver4.Count(signature, 0) == 2,
                    "saved piercing weapon and earned signature ranks survive changed lobby offer toggles");
                PlayerPrefs.SetInt(StartingNeedleSelection.EnabledKey(Parent.Pierce), 1);
            }
            SessionState.SetString(Key + ".result", "PASS");
        }

        static IEnumerator CheckOrbit(LevelManager field, SyringeDartAbility dart)
        {
            var player = field.PlayerCharacter;
            dart.EnableCursorControlLegendary();
            var orbit = Object.FindObjectOfType<CursorControlledNeedleController>();
            yield return null;
            var quill = (Transform)ColonContentSmoke.Field(orbit, "cursorNeedleTransform").GetValue(orbit);
            var glow = (SyringeAugmentVfx)ColonContentSmoke.Field(orbit, "needleGlow").GetValue(orbit);
            Check(glow != null && glow.gameObject.activeInHierarchy, "figure-eight feather is visible before shield contact");
            var monster = field.EntityManager.SpawnMonster(0, quill.position, field.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0], 500);
            var barrier = monster.GetComponent<MonsterNeedleBarrier>() ?? monster.gameObject.AddComponent<MonsterNeedleBarrier>();
            barrier.Arm();
            Physics2D.SyncTransforms();
            float hp = monster.HP;
            var contact = typeof(CursorControlledNeedleController).GetMethod("DetectAndDamageEnemies", BindingFlags.NonPublic | BindingFlags.Instance);
            contact.Invoke(orbit, null);
            contact.Invoke(orbit, null);
            Check(barrier.Hits == 1 && Mathf.Approximately(hp, monster.HP), "shield absorbs only one contact without monster damage");
            float phase = (float)ColonContentSmoke.Field(orbit, "orbitPhase").GetValue(orbit);
            Time.timeScale = 1;
            yield return new WaitForSeconds(.12f);
            Check(glow == (SyringeAugmentVfx)ColonContentSmoke.Field(orbit, "needleGlow").GetValue(orbit) &&
                  glow.gameObject.activeInHierarchy && (float)ColonContentSmoke.Field(orbit, "orbitPhase").GetValue(orbit) != phase,
                  "same feather stays visible and moves during shield contact cooldown");
            Check(orbit.GetComponentInChildren<SpriteRenderer>(true).enabled, "orbit carrier never turns off on shield");
            ColonContentSmoke.Set(player, "currentHealth", player.MaxHealth * .5f);
            Check(player.TryMosquitoHeal(1) && !player.TryMosquitoHeal(1), "same-frame mosquito hits share recovery cooldown");
            yield return new WaitForSeconds(1.51f);
            Check(player.TryMosquitoHeal(1), "mosquito recovers again after 1.5 seconds");
        }
    }
}
