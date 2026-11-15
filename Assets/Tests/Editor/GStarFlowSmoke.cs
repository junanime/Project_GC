using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Real scene/play-mode integration. QA records live under Library, never booth data.
    [InitializeOnLoad]
    public static class GStarFlowSmoke
    {
        const string Key = "GStarFlowSmoke";
        const string Output = "Library/GStarFlowQA";
        static bool started;
        static bool protectPlayer = true;
        static double deadline;
        static GStarFlowTestHost host;
        static GStarFlowSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/checks.txt", ""); File.WriteAllText(Output + "/errors.txt", "");
            SessionState.SetString(Key + "Data", Path.GetFullPath(Path.Combine(Output, "data-" + Guid.NewGuid().ToString("N"))));
            SessionState.SetBool(Key + "HadDoctor", ProgressPrefs.HasKey(CapsuleDoctorDialogue.SeenKey));
            SessionState.SetInt(Key + "Doctor", ProgressPrefs.GetInt(CapsuleDoctorDialogue.SeenKey));
            SessionState.SetBool(Key + "HadCoins", ProgressPrefs.HasKey("Coins"));
            SessionState.SetInt(Key + "Coins", ProgressPrefs.GetInt("Coins"));
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key + "Fail", false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            Attach(); EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void IsolateBeforeScene()
        {
            if (!SessionState.GetBool(Key, false)) return;
            GStarDemo.TestOverride = true;
            GStarDemo.TestDataDirectory = SessionState.GetString(Key + "Data", "");
            GStarSession.TestDirectory = GStarDemo.TestDataDirectory;
            GStarSession.ReloadForTests();
            StageProgression.PreviewWebDemoEnding = false;
        }

        static void Attach()
        {
            started = false; protectPlayer = true; deadline = EditorApplication.timeSinceStartup + 900;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string text, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            File.AppendAllText(Output + "/errors.txt", text + "\n" + trace + "\n");
            SessionState.SetBool(Key + "Fail", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline)
            { File.AppendAllText(Output + "/errors.txt", "Overall flow deadline\n"); SessionState.SetBool(Key + "Fail", true); }
            if (SessionState.GetBool(Key + "Done", false) || SessionState.GetBool(Key + "Fail", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool(Key + "HadDoctor", false)) ProgressPrefs.SetInt(CapsuleDoctorDialogue.SeenKey, SessionState.GetInt(Key + "Doctor", 0));
                else ProgressPrefs.DeleteKey(CapsuleDoctorDialogue.SeenKey);
                if (SessionState.GetBool(Key + "HadCoins", false)) ProgressPrefs.SetInt("Coins", SessionState.GetInt(Key + "Coins", 0));
                else ProgressPrefs.DeleteKey("Coins");
                ProgressPrefs.Save();
                GStarDemo.TestOverride = null; GStarDemo.TestDataDirectory = null;
                GStarSession.TestDirectory = null; GStarSession.ReloadForTests();
                bool failed = SessionState.GetBool(Key + "Fail", false);
                SessionState.SetBool(Key, false);
                File.WriteAllText(Output + "/result.txt", failed ? "FAIL" : "PASS: stage 0/1/2 endings and HP-zero failure");
                EditorApplication.Exit(failed ? 1 : 0); return;
            }
            if (!EditorApplication.isPlaying || started || ApothecaryUI.Instance == null) return;
            started = true;
            host = new GameObject("G-STAR real ending QA").AddComponent<GStarFlowTestHost>();
            Object.DontDestroyOnLoad(host.gameObject); host.StartCoroutine(Checks());
        }

        static void Check(bool ok, string text)
        {
            if (!ok) throw new Exception("GSTAR_FLOW_FAIL: " + text);
            File.AppendAllText(Output + "/checks.txt", "PASS " + text + "\n");
        }
        static IEnumerator Wait(Func<bool> condition, float seconds, string text)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            Check(condition(), text);
        }
        static void Set(object owner, string name, object value)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) continue; field.SetValue(owner, value); return;
            }
            throw new Exception("Missing QA field " + name);
        }
        static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(owner);
        static GStarAnalyticsRun AnalyticsRun()
        {
            var export = GStarAnalytics.Export();
            Check(export.success, "analytics export succeeds in isolated QA directory");
            return File.ReadAllLines(Path.Combine(export.directory, "runs.jsonl"))
                .Where(line => !string.IsNullOrWhiteSpace(line)).Select(JsonUtility.FromJson<GStarAnalyticsRun>)
                .Single(run => run.runId == GStarSession.RunId);
        }
        static List<GStarAnalyticsEvent> AnalyticsEvents()
        {
            var export = GStarAnalytics.Export();
            Check(export.success, "analytics event export succeeds");
            return File.ReadAllLines(Path.Combine(export.directory, "events.jsonl"))
                .Where(line => !string.IsNullOrWhiteSpace(line)).Select(JsonUtility.FromJson<GStarAnalyticsEvent>)
                .Where(entry => entry.runId == GStarSession.RunId).ToList();
        }
        static IEnumerator AnalyticsActions(LevelManager level)
        {
            var player = level.PlayerCharacter;
            var skills = player.GetComponent<CharacterSkillRuntime>();
            var ui = ApothecaryUI.Instance;
            yield return Wait(() => ui.Page == "hud" && skills != null && skills.CanActivate, 15, "real gameplay permits active skill and choices");
            int previousDash = AnalyticsRun().dashCount;
            player.LookDirection = Vector2.right;
            Check(player.TryDash(), "real player dash executes");
            Check(!player.TryDash(), "second input during dash is rejected");
            yield return Wait(() => !player.IsDashing, 5, "real dash motion completes");
            Check(AnalyticsRun().dashCount == previousDash + 1, "only accepted dash is counted");
            int previousActive = AnalyticsRun().activeSkillCount;
            Check(skills.TryActivate(), "real active skill executes");
            Check(!skills.TryActivate(), "active skill during cooldown is rejected");
            Check(AnalyticsRun().activeSkillCount == previousActive + 1, "only accepted active skill is counted");
            yield return Wait(() => !skills.IsCutin && !skills.IsSummoning, 15, "active skill presentation completes before reward");

            int previousPassive = skills.PassiveLevel;
            bool rewardCompleted = false;
            Check(ui.RequestSkillReward(() => rewardCompleted = true), "real bloodclot reward is queued");
            yield return Wait(() => ui.Page == "skillReward", 5, "actual bloodclot reward panel opens");
            var finishReward = typeof(ApothecaryUI).GetMethod("FinishSkillReward", BindingFlags.Instance | BindingFlags.NonPublic);
            finishReward.Invoke(ui, new object[] { (bool?)false });
            finishReward.Invoke(ui, new object[] { (bool?)false });
            Check(rewardCompleted && skills.PassiveLevel == previousPassive + 1, "real bloodclot passive reward applies once");
            var events = AnalyticsEvents();
            var reward = events.Single(e => e.type == "bloodclot_reward_choice");
            Check(reward.id == "passive" && reward.offered.Any(id => id.StartsWith("passive|")), "bloodclot selection records offered and selected identifiers once");

            var dialog = level.EntityManager.AbilitySelectionDialog;
            dialog.Open(false);
            Check(dialog.MenuOpen, "actual augment choice opens");
            var offers = Get<List<Ability>>(dialog, "displayedAbilities");
            var cards = Get<List<AbilityCard>>(dialog, "abilityCards");
            int selected = offers.Take(3).ToList().FindIndex(a => a != null && a.RequirementsMet());
            Check(selected >= 0, "actual augment has an eligible offered choice");
            dialog.SelectWithMotion(cards[selected], offers[selected]);
            dialog.SelectWithMotion(cards[selected], offers[selected]);
            yield return Wait(() => !dialog.MenuOpen && !dialog.SelectionAnimating, 8, "augment selection animation completes");
            var augment = AnalyticsEvents().Single(e => e.type == "augment_choice");
            Check(augment.offered.Length > 0 && augment.offered.Any(id => id.StartsWith(augment.id + "|")), "actual augment choice and offered alternatives recorded once");
            Time.timeScale = 2;
        }
        static void Position(Character player, Vector2 point)
        {
            player.transform.position = point;
            var body = player.GetComponent<Rigidbody2D>(); if (body != null) { body.position = point; body.velocity = Vector2.zero; }
            Physics2D.SyncTransforms();
        }
        internal static void ProtectDuringQA()
        {
            if (!protectPlayer || !SessionState.GetBool(Key, false)) return;
            var level = Object.FindObjectOfType<LevelManager>();
            var player = level != null ? level.PlayerCharacter : null;
            if (player == null || !player.IsAlive || player.MaxHealth <= 0) return;
            Set(player, "isInvincible", true);
            // Whirlpool cores deliberately bypass ordinary invincibility and remove
            // 25% max HP per pulse. This ending-flow QA must survive those real pulses.
            Set(player, "currentHealth", player.MaxHealth);
        }
        static void Silence(LevelManager level)
        {
            level.enabled = false;
            TutorialGuide.Instance?.Close(); if (TutorialGuide.Instance != null) TutorialGuide.Instance.enabled = false;
            foreach (var component in Object.FindObjectsOfType<MonoBehaviour>())
                if (component is StageEventDirector || component is TimedSpecialMonsterSpawner || component is AcidToadSpawn || component is NPCSpawner || component is SnailBossDebugSpawn)
                { component.enabled = false; component.StopAllCoroutines(); }
            foreach (var ability in Object.FindObjectsOfType<Ability>()) { ability.enabled = false; ability.StopAllCoroutines(); }
            level.EntityManager.enabled = false; level.EntityManager.StopAllCoroutines();
            foreach (var monster in level.EntityManager.LivingMonsters.ToArray()) monster.DespawnForStageTransfer();
            Set(level.PlayerCharacter, "isInvincible", true); level.PlayerCharacter.CollectableCollider.enabled = false;
        }
        static IEnumerator Enter(int stage)
        {
            yield return Wait(() => ApothecaryUI.Instance != null && SceneManager.GetActiveScene().buildIndex == 0, 45, "preparation scene ready");
            var config = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            CrossSceneData.CharacterBlueprint = config.characters.First(c => c != null);
            CrossSceneData.ClearStartingLobbyItems();
            Time.timeScale = 1;
            Check(StageEntryLoading.Begin(CrossSceneData.CharacterBlueprint, null, stage), "stage " + stage + " real departure accepted");
            Check(!StageEntryLoading.Begin(CrossSceneData.CharacterBlueprint, null, stage), "duplicate departure rejected");
            float nextDiagnostic = 0;
            yield return Wait(() =>
            {
                var level = Object.FindObjectOfType<LevelManager>();
                var flow = level != null ? level.GetComponent<StageProgression>() : null;
                if (Time.realtimeSinceStartup >= nextDiagnostic)
                {
                    nextDiagnostic = Time.realtimeSinceStartup + 5;
                    File.AppendAllText(Output + "/checks.txt", $"WAIT stage={stage} actual={flow?.StageNumber} phase={flow?.TransitionPhase} ready={level?.InitialFieldObjectsReady} travelling={flow?.Travelling} loading={StageEntryLoading.IsLoading} hp={level?.PlayerCharacter?.CurrentHealth} pause={level?.IsRunFlowPaused} scale={Time.timeScale}\n");
                }
                return level != null && level.InitialFieldObjectsReady && flow != null && flow.StageNumber == stage && !flow.Travelling && !StageEntryLoading.IsLoading;
            }, 150, "stage " + stage + " field and opening ready");
            Check(GStarSession.AttemptStarted, "attempt marked started only after valid field");
            Check(GStarAnalytics.IsRecording && AnalyticsRun().stageId == stage && AnalyticsRun().status == "active", "actual departure begins correct stage analytics");
            Silence(Object.FindObjectOfType<LevelManager>());
            Time.timeScale = 2;
        }
        static IEnumerator Result(LevelManager level, int stage, bool success, int remaining)
        {
            yield return Wait(() => ApothecaryUI.Instance != null && ApothecaryUI.Instance.Page == "result", 15, "stage " + stage + " shared result shown");
            Check(level.IsLevelEnded && RunScore.Current.IsFinished && RunScore.Current.Data.success == success, "finalized result has correct outcome");
            Check(StageProgression.Ensure(level).StageNumber == stage && SceneManager.GetActiveScene().buildIndex == 1, "result remains in original stage");
            Check(GStarSession.RemainingAttempts == remaining && !GStarSession.ActiveAttempt, "exactly one attempt consumed");
            var analytics = AnalyticsRun();
            Check(!GStarAnalytics.IsRecording && analytics.status == (success ? "clear" : "fail") && analytics.combatStatsAvailable, "actual ending finalizes analytics with combat statistics");
            Check(AnalyticsEvents().Count(e => e.type == "run_ended") == 1, "analytics has exactly one final outcome per attempt");
            level.LevelPassed(null); GStarSession.CompleteAttempt(!success);
            Check(GStarSession.RemainingAttempts == remaining && GStarSession.LastSuccess == success, "duplicate end callback cannot alter result or attempts");
            yield return new WaitForSecondsRealtime(1.1f);
            Check(!StageEntryLoading.IsLoading && SceneManager.GetActiveScene().buildIndex == 1 && ApothecaryUI.Instance.Page == "result", "result does not load next stage or auto-return");
            Capture("stage-" + stage + (success ? "-clear" : "-failure"));
            yield return null;
            yield return new WaitForSecondsRealtime(.25f);
        }

        static void Capture(string name)
        {
            var camera = Camera.main ?? Object.FindObjectOfType<Camera>();
            Check(camera != null, "real camera available for " + name);
            int width = Mathf.Max(960, Screen.width), height = Mathf.Max(540, Screen.height);
            var canvases = Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).OrderBy(c => c.sortingOrder).ToArray();
            var cameras = canvases.Select(c => c.worldCamera).ToArray(); var distances = canvases.Select(c => c.planeDistance).ToArray();
            var layers = canvases.Select(c => c.sortingLayerID).ToArray(); var orders = canvases.Select(c => c.sortingOrder).ToArray();
            var overrides = canvases.Select(c => c.overrideSorting).ToArray();
            int captureLayer = SortingLayer.layers.OrderBy(layer => layer.value).Last().id;
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32); Texture2D pixels = null;
            try
            {
                camera.targetTexture = target;
                // Overlay canvases normally draw above every world sprite. Preserve that
                // behavior while rendering them through a camera for batch-mode capture.
                for (int i = 0; i < canvases.Length; i++)
                {
                    var canvas = canvases[i]; canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .1f;
                    canvas.overrideSorting = true; canvas.sortingLayerID = captureLayer;
                    canvas.sortingOrder = 32760 - (canvases.Length - 1 - i);
                }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i];
                    canvases[i].sortingLayerID = layers[i]; canvases[i].sortingOrder = orders[i]; canvases[i].overrideSorting = overrides[i];
                }
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive; RenderTexture.ReleaseTemporary(target);
                if (pixels != null) Object.Destroy(pixels); Canvas.ForceUpdateCanvases();
            }
        }

        static IEnumerator Checks()
        {
            Application.runInBackground = true;
            var prefs = GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss = false; prefs.muteOnFocusLoss = false; GamePreferences.Apply(prefs, false);
            ProgressPrefs.SetInt(CapsuleDoctorDialogue.SeenKey, 1);
            Check(GStarSession.ResetVisitor(), "isolated visitor starts with three attempts");
            string session = GStarSession.SessionId;
            yield return Enter(0);
            var level = Object.FindObjectOfType<LevelManager>();
            yield return AnalyticsActions(level);
            var finale = level.GetComponent<OralSwallowFinale>();
            Check(finale != null && finale.StartForTesting(), "actual oral swallowing finale begins");
            bool swallowed = false; float until = Time.realtimeSinceStartup + 60;
            while (!level.IsLevelEnded && Time.realtimeSinceStartup < until)
            { swallowed |= finale.IsSwallowing; yield return null; }
            Check(swallowed && finale.Age >= OralSwallowFinale.TransferTime && StageProgression.Ensure(level).TransitionPhase == "gstar-complete", "oral water/survival/swallow completes before result");
            yield return Result(level, 0, true, 2);
            ApothecaryUI.ReturnToPreparation();

            yield return Enter(1);
            level = Object.FindObjectOfType<LevelManager>(); var flow = StageProgression.Ensure(level);
            Check(flow.FinalBossDefeated(level.PlayerCharacter.Position + Vector2.up * 3, 100), "stage-one boss escape callback accepted");
            yield return Wait(() => flow.Capsule != null && flow.Capsule.Ready, 30, "capsule landing completes");
            Position(level.PlayerCharacter, flow.Capsule.BoardingPoint);
            Check(flow.TryBoard(level.PlayerCharacter, flow.Capsule), "player boards actual capsule");
            var phases = new HashSet<string>(); until = Time.realtimeSinceStartup + 35;
            while (!level.IsLevelEnded && Time.realtimeSinceStartup < until) { phases.Add(flow.TransitionPhase); yield return null; }
            foreach (var phase in new[] { "hatch-open", "boarding", "hatch-close", "capsule-travel", "outlet-open" }) Check(phases.Contains(phase), "stage-one ending preserved: " + phase);
            Check(flow.AtOutlet && flow.TransitionPhase == "gstar-complete", "capsule reaches outlet without colon transfer");
            yield return Result(level, 1, true, 1);
            ApothecaryUI.ReturnToPreparation();

            yield return Enter(2);
            level = Object.FindObjectOfType<LevelManager>(); var king = ConstipationKingDirector.Ensure(level);
            Position(level.PlayerCharacter, king.Throne.transform.position);
            Check(king.TrySpawn(), "actual king summon accepted");
            var boss = king.Boss; boss.AutoPatterns = false;
            yield return Wait(() => boss.Targetable && !king.Summoning, 15, "king summon animation completes");
            boss.TakeDamage(999999);
            yield return Wait(() => boss.PhaseTwo && !boss.Transitioning, 20, "king phase transition completes");
            boss.TakeDamage(999999);
            var exit = ColonExitSequence.Ensure(level);
            yield return Wait(() => exit.State == ColonExitSequence.ExitState.Dropped, 15, "king death and physical key drop complete");
            Check(!level.IsLevelEnded, "king defeat alone cannot bypass ending");
            Position(level.PlayerCharacter, exit.PlantedPoint);
            Check(exit.TakeKey(level.PlayerCharacter), "player extracts actual key");
            yield return Wait(() => exit.State == ColonExitSequence.ExitState.Carrying, 10, "key extraction animation completes");
            Position(level.PlayerCharacter, exit.Opening.transform.position);
            Check(exit.InteractAtGate(level.PlayerCharacter, exit.Opening), "key unlock begins");
            yield return Wait(() => exit.State == ColonExitSequence.ExitState.Open, 15, "lock turn seal break and gate opening complete");
            Check(exit.UnlockProgress > .99f && !level.IsLevelEnded, "result waits until complete gate interaction");
            Check(exit.InteractAtGate(level.PlayerCharacter, exit.Opening), "final exit interaction completes stage");
            yield return Result(level, 2, true, 0);
            Check(GStarSession.SessionId == session, "three stages share one visitor session");
            Check(!GStarSession.TryBeginAttempt(1, out _), "fourth attempt blocked");
            ApothecaryUI.ReturnToPreparation();

            yield return Wait(() => SceneManager.GetActiveScene().buildIndex == 0 && ApothecaryUI.Instance != null, 45, "menu ready for next visitor");
            Check(GStarSession.ResetVisitor(), "staff explicitly resets next visitor");
            yield return Enter(1);
            level = Object.FindObjectOfType<LevelManager>();
            protectPlayer = false;
            // An equipped one-hit shield/revive must not make the failure-path QA
            // depend on whichever relic the developer last selected in preparation.
            until = Time.realtimeSinceStartup + 8;
            while (level.PlayerCharacter.IsAlive && Time.realtimeSinceStartup < until)
            {
                Set(level.PlayerCharacter, "isInvincible", false);
                level.PlayerCharacter.TakeDamage(999999);
                yield return new WaitForSecondsRealtime(.2f);
            }
            yield return Wait(() => level.PlayerCharacter.GetComponent<CharacterExitSequence>() != null, 5, "HP-zero exit animation starts");
            Check(!level.IsLevelEnded && ApothecaryUI.Instance.Page != "result", "failure result waits for character exit");
            yield return Result(level, 1, false, 2);
            Check(level.PlayerCharacter.GetComponent<CharacterExitSequence>().Completed, "HP-zero exit completes before failure panel");
            SessionState.SetBool(Key + "Done", true);
        }
    }
    public sealed class GStarFlowTestHost : MonoBehaviour { void Update() { GStarFlowSmoke.ProtectDuringQA(); } }
}
