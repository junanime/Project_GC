#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    /// <summary>Real play-mode finale, dash and stage-transfer acceptance checks.</summary>
    [InitializeOnLoad]
    public static class OralSwallowSmoke
    {
        const string Key = "OralSwallowSmoke", Root = "Library/OralSwallowProof";
        static readonly string[] Names = { "ashi", "ari", "hyuki", "shini" };
        static readonly string[] Blueprints = { "Main", "Ari", "Hyuki", "Shini" };
        static int Index => SessionState.GetInt(Key + "Index", 0);
        static string Output => Root + "/" + Index + "-" + Names[Index];
        static bool started; static double deadline;
        static Vector2? captureFocus; static float shotSize = 6;
        [Serializable] sealed class Pref { public string key; public bool had; public int value; }
        [Serializable] sealed class Backup { public Pref[] prefs; }

        static OralSwallowSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SeedSelection()
        {
            if (!SessionState.GetBool(Key, false)) return;
            CrossSceneData.CharacterBlueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/" + Blueprints[Index] + " Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems = Array.Empty<MerchantItemBlueprint>();
        }
        public static void Run()
        {
            Directory.CreateDirectory(Root); RestorePrefs();
            File.WriteAllText(Root + "/result.txt", "RUNNING"); File.WriteAllText(Root + "/checks.txt", ""); File.WriteAllText(Root + "/errors.txt", "");
            var keys = new[] { "Coins", "LobbySilverCoins" };
            File.WriteAllText(Root + "/prefs.json", JsonUtility.ToJson(new Backup { prefs = keys.Select(k => new Pref { key = k, had = PlayerPrefs.HasKey(k), value = PlayerPrefs.GetInt(k) }).ToArray() }));
            File.WriteAllText(Root + "/capture-method.txt", "Actual Unity Level 1 scene, selected production CharacterBlueprint, actual runtime and Camera.Render. Controlled placement, game-clock seek and explicit per-frame full health are QA fixtures only; percentage core damage bypasses ordinary invincibility. Actual Character.TryDash executes the recorded escapes. Full-flow review uses a wider camera; close-ups use the same production renderer. Fixed 15 simulation frames per second while recording; no generated or interpolated frames. PlayerPrefs restored on completion.\n");
            var arg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("-swallowCharacter="));
            int index = arg == null ? 0 : int.Parse(arg.Substring("-swallowCharacter=".Length), CultureInfo.InvariantCulture);
            if (index < 0 || index >= Names.Length) throw new ArgumentOutOfRangeException("swallowCharacter");
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Failed", false); SessionState.SetBool(Key + "Done", false);
            SessionState.SetInt(Key + "Index", index); SessionState.SetInt(Key + "Checks", 0); Directory.CreateDirectory(Output);
            CrossSceneData.ClearPendingRunSceneTransfer(); SeedSelection();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity"); Attach(); EditorApplication.EnterPlaymode();
        }
        static void RestorePrefs()
        {
            string file = Root + "/prefs.json"; if (!File.Exists(file)) return;
            foreach (var p in JsonUtility.FromJson<Backup>(File.ReadAllText(file)).prefs)
                if (p.had) PlayerPrefs.SetInt(p.key, p.value); else PlayerPrefs.DeleteKey(p.key);
            PlayerPrefs.Save(); File.Delete(file);
        }
        static void Attach()
        {
            started = false; deadline = EditorApplication.timeSinceStartup + 1800;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string text, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            File.AppendAllText(Root + "/errors.txt", text + "\n" + stack + "\n"); SessionState.SetBool(Key + "Failed", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) { File.AppendAllText(Root + "/errors.txt", "Deadline exceeded\n"); SessionState.SetBool(Key + "Failed", true); }
            if (SessionState.GetBool(Key + "Failed", false) || SessionState.GetBool(Key + "Done", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                bool failed = SessionState.GetBool(Key + "Failed", false);
                string result = (failed ? "FAIL" : "PASS") + " checks=" + SessionState.GetInt(Key + "Checks", 0) + " character=" + Names[Index];
                File.WriteAllText(Root + "/result.txt", result); File.WriteAllText(Output + "/result.txt", result);
                File.Copy(Root + "/checks.txt", Output + "/checks.txt", true); File.Copy(Root + "/errors.txt", Output + "/errors.txt", true);
                RestorePrefs(); Time.timeScale = 1; Time.captureFramerate = 0; SessionState.SetBool(Key, false);
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
            if (!value) throw new Exception("ORAL_SWALLOW_FAIL " + Names[Index] + ": " + message);
            SessionState.SetInt(Key + "Checks", SessionState.GetInt(Key + "Checks", 0) + 1);
            File.AppendAllText(Root + "/checks.txt", "PASS " + message + "\n"); Debug.Log("ORAL_SWALLOW_PASS " + message);
        }
        static FieldInfo Field(object owner, string name)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            { var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f; }
            throw new MissingFieldException(owner.GetType().Name, name);
        }
        static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
        static void Place(Character player, Vector2 position)
        {
            OralStageRuntime.Instance.TeleportPlayer(position, OralZone.Tongue); player.Move(Vector2.zero); Physics2D.SyncTransforms();
        }
        static void Clear(LevelManager level)
        {
            foreach (var m in level.EntityManager.LivingMonsters.ToArray()) if (m != null) m.DespawnForStageTransfer();
            foreach (var p in Object.FindObjectsOfType<Projectile>()) p.RemoveForItem();
            foreach (var c in Object.FindObjectsOfType<Collectable>()) c.DissolveInAcidLake();
        }
        static void CloseDialogs(LevelManager level)
        {
            if (level.EntityManager.AbilitySelectionDialog.MenuOpen) { level.EntityManager.AbilitySelectionDialog.Close(); Time.timeScale = 1; }
        }
        static IEnumerator Checks(LevelManager level)
        {
            var preferences = GamePreferences.Current.Copy(); preferences.pauseOnFocusLoss = false; preferences.muteOnFocusLoss = false; GamePreferences.Apply(preferences, false);
            Application.runInBackground = true; Time.timeScale = 1; RunScore.Exclude("Controlled swallow finale and actual dash acceptance QA");
            var stage = StageProgression.Ensure(level);
            Check(stage.BeginDirectOral(), "direct stage-zero entry uses the production transition");
            float until = Time.realtimeSinceStartup + 90;
            while ((OralStageRuntime.Instance == null || stage.Travelling) && Time.realtimeSinceStartup < until) yield return null;
            Check(OralStageRuntime.Instance != null && !stage.Travelling, "stage zero finishes loading");
            var runtime = OralStageRuntime.Instance; var events = OralFieldEvents.Instance; var finale = OralSwallowFinale.Instance;
            var player = level.PlayerCharacter; var map = FiniteStomachMap.Instance;
            Check(finale != null, "swallow controller installs with stage zero");
            events.AutoSchedule = false; foreach (var c in events.Cavities) { c.Healed = true; c.Active = false; }
            Set(player, "isInvincible", true); Place(player, OralMapGeometry.TongueCenter(map));
            float dashDistance = player.DashDistance, dashDuration = player.DashDuration, dashRecharge = player.DashRechargeTime;
            int maxCharges = player.MaxDashCharges;
            Check(!finale.Active, "finale stays inactive before 9:15");
            if (Index == 0)
            {
                var bodySprite = player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>(); bool originalFlip = bodySprite.flipY;
                Check(events.ForceEvent(OralEventKind.Ceiling), "existing ceiling encounter begins before finale request");
                float ceilingDeadline = Time.realtimeSinceStartup + 45;
                while (events.CeilingPhase != 2 && Time.realtimeSinceStartup < ceilingDeadline) { CloseDialogs(level); yield return null; }
                Check(events.CeilingPhase == 2 && events.CeilingRemaining == 10, "ceiling conflict fixture contains the real ten-bat encounter");
                Check(finale.StartForTesting() && finale.Pending && !finale.Active, "finale request queues while player is on the ceiling");
                yield return new WaitForSeconds(2.5f);
                Check(finale.Pending && !finale.Active && runtime.OnCeiling, "queued finale never bypasses the unfinished ceiling encounter");
                foreach (var bat in events.CeilingMonsters.ToArray()) if (bat != null && bat.HP > 0) bat.TakeDamage(bat.HP + 1000);
                while (!finale.Active && Time.realtimeSinceStartup < ceilingDeadline) { CloseDialogs(level); yield return null; }
                Check(finale.Active && !runtime.OnCeiling && !runtime.IsAirborne, "tenth bat defeat returns safely before queued finale starts");
                Check(bodySprite.flipY == originalFlip && !level.IsRunFlowPaused, "ceiling-to-finale restores player orientation and releases ceiling pause");
                finale.CancelForTransition(); events.Configure(level); events.AutoSchedule = false; finale.Configure(runtime); Set(level, "levelTime", 0f);
                foreach (var cavity in events.Cavities) { cavity.Healed = true; cavity.Active = false; }
                Clear(level); Place(player, OralMapGeometry.TongueCenter(map)); yield return null;
                CloseDialogs(level);
                var previousKeyboard = Keyboard.current; var testKeyboard = InputSystem.AddDevice<Keyboard>("SwallowQaKeyboard"); testKeyboard.MakeCurrent();
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(UnityEngine.InputSystem.Key.F1)); yield return null; yield return null;
                Check(!finale.Active, "native Input System F1 alone leaves the finale inactive");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(UnityEngine.InputSystem.Key.F1, UnityEngine.InputSystem.Key.F2)); yield return null; yield return null;
                string diagnostic = "focused=" + Application.isFocused + " keyboardEnabled=" + testKeyboard.enabled + " currentIsFixture=" + (Keyboard.current == testKeyboard) +
                    " blocked=" + GameInput.KeyboardBlocked + " f1=" + GameInput.GetKey(KeyCode.F1) + " f2=" + GameInput.GetKey(KeyCode.F2) +
                    " active=" + finale.Active + " pending=" + finale.Pending + " ceiling=" + runtime.OnCeiling + " airborne=" + runtime.IsAirborne + " paused=" + level.IsRunFlowPaused + " timeScale=" + Time.timeScale + " choosingAugment=" + player.IsChoosingAugment +
                    " editorInput=" + InputSystem.settings.editorInputBehaviorInPlayMode + " background=" + InputSystem.settings.backgroundBehavior + "\n";
                File.WriteAllText(Output + "/native-keyboard-diagnostics.txt", diagnostic);
                // Batch mode has no focused Game View. Route the temporary device
                // to the player using a transient settings clone, never the asset.
                InputSettings originalInputSettings = InputSystem.settings, fixtureInputSettings = null;
                if (!finale.Active)
                {
                    fixtureInputSettings = Object.Instantiate(originalInputSettings);
                    fixtureInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    fixtureInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings = fixtureInputSettings; InputSystem.EnableDevice(testKeyboard); testKeyboard.MakeCurrent();
                    InputSystem.QueueStateEvent(testKeyboard, new KeyboardState()); yield return null; yield return null;
                    InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(UnityEngine.InputSystem.Key.F1, UnityEngine.InputSystem.Key.F2)); yield return null; yield return null;
                    File.AppendAllText(Output + "/native-keyboard-diagnostics.txt", "temporaryFocusedGameViewFixture: enabled=" + testKeyboard.enabled + " current=" + (Keyboard.current == testKeyboard) +
                        " f1=" + GameInput.GetKey(KeyCode.F1) + " f2=" + GameInput.GetKey(KeyCode.F2) + " active=" + finale.Active + " pending=" + finale.Pending + "\n");
                }
                Check(finale.Active || finale.Pending, "native Input System simultaneous F1 and F2 reaches the runtime trigger");
                float inputDeadline = Time.realtimeSinceStartup + 5;
                while (!finale.Active && Time.realtimeSinceStartup < inputDeadline) { CloseDialogs(level); yield return null; }
                Check(finale.Active, "native shortcut reaches the active finale after any open reward dialog closes");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState()); yield return null;
                InputSystem.RemoveDevice(testKeyboard); if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                InputSystem.settings = originalInputSettings; if (fixtureInputSettings != null) Object.Destroy(fixtureInputSettings);
                finale.CancelForTransition(); events.Configure(level); events.AutoSchedule = false; finale.Configure(runtime); Set(level, "levelTime", 0f);
                foreach (var cavity in events.Cavities) { cavity.Healed = true; cavity.Active = false; }
            }
            Check(!finale.HandleTestShortcut(true, false) && !finale.Active, "F1 alone does not trigger the finale");
            Check(!finale.HandleTestShortcut(false, true) && !finale.Active, "F2 alone does not trigger the finale");
            Check(finale.HandleTestShortcut(true, true), "F1 plus F2 shared production handler requests the finale");
            yield return null;
            Check(finale.Active, "simultaneous shortcut activates the actual finale");
            Check(!finale.HandleTestShortcut(true, true), "held shortcut is edge-triggered rather than restarted each frame");
            float pausedAge = finale.Age;
            Time.timeScale = 0; yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Abs(finale.Age - pausedAge) < .001f, "pause freezes the finale timeline"); Time.timeScale = 1;
            finale.CancelForTransition(); finale.Configure(runtime); Set(level, "levelTime", 0f); yield return null;
            Check(!finale.Active && !level.IsRunFlowPaused, "cancel releases finale ownership and stage time");
            Check(finale.StartForTesting(), "test entry can restart through the same runtime"); yield return null;
            Set(player, "alive", false); yield return null; yield return null;
            Check(!finale.Active, "a dead player cancels the finale rather than advancing stages");
            Set(player, "alive", true); Set(player, "isInvincible", true); events.Configure(level); events.AutoSchedule = false;
            foreach (var cavity in events.Cavities) { cavity.Healed = true; cavity.Active = false; }
            finale.Configure(runtime); Set(level, "levelTime", 0f);
            Check(stage.StageNumber == 0, "cancelled finale does not change the stage");

            // Isolated dash proof uses actual vortices and actual Character.TryDash;
            // seeking age skips the introduction but changes no production stats.
            Check(finale.StartForTesting(), "dash acceptance encounter starts"); Set(finale, "age", 20.2f); yield return null;
            Check(finale.Whirlpools.Count >= 12, "survival has many independently sized vortices");
            Check(finale.Whirlpools.Max(w => w.Radius) > finale.Whirlpools.Min(w => w.Radius) * 1.6f, "vortex field includes meaningfully different small and large sizes");
            Check(finale.Whirlpools.Any(w => Mathf.Abs(w.Position.x - OralMapGeometry.TongueCenter(map).x) < w.Radius * .8f), "vortex placement does not reserve a central corridor");
            Check(!events.ForceEvent(OralEventKind.Ceiling), "ceiling entry cannot take motion ownership during the finale");
            Check(events.Tastes != null && events.Additions != null, "death fixture restores the real field-event controllers before overlap checks");
            var mini = Object.FindObjectOfType<MiniStageDirector>();
            Check(mini == null || !mini.CanEnterFromPortal, "blood-clot portals cannot interrupt the finale");
            var tooth = OralMapGeometry.ToothWalkCenter(map, 1);
            Check(OralMapGeometry.ZoneAt(map, runtime.ConstrainPlayer(tooth, .55f)) == OralZone.Tongue, "closed-jaw phase constrains player destinations to the tongue");
            var moving = finale.Whirlpools[0]; Vector2 movementOrigin = moving.Position; float angleOrigin = moving.Angle;
            yield return new WaitForSeconds(.5f);
            Check(Vector2.Distance(movementOrigin, moving.Position) > .1f && Mathf.Abs(moving.Angle - angleOrigin) > .01f, "vortices both translate and rotate during survival");
            Vector2 pausedPosition = moving.Position; float pausedAngle = moving.Angle, survivalAge = finale.Age;
            Time.timeScale = 0; yield return new WaitForSecondsRealtime(.2f);
            Check(Vector2.Distance(moving.Position, pausedPosition) < .001f && Mathf.Abs(moving.Angle - pausedAngle) < .001f && Mathf.Abs(finale.Age - survivalAge) < .001f, "pause freezes moving vortex position, rotation and survival timer together"); Time.timeScale = 1;
            yield return DenseEscapeChecks(level, finale);
            yield return DashCase(level, finale, false, "02-small-vortex-dash");
            yield return DashCase(level, finale, true, "03-large-vortex-dash");
            Check(player.DashDistance == dashDistance && player.DashDuration == dashDuration && player.DashRechargeTime == dashRecharge && player.MaxDashCharges == maxCharges, "vortex escape leaves base distance, duration, recharge and max charges unchanged");
            finale.CancelForTransition(); finale.Configure(runtime); Set(level, "levelTime", 0f); Clear(level); Place(player, OralMapGeometry.TongueCenter(map));

            // Natural 9:15 -> 9:25 -> 9:35 -> 9:55 chain and real stage transfer.
            player.AddMaxHealthBonus(17); var preserved = player.CaptureRunSceneState(); var skills = player.Skills; var relics = player.Relics;
            Set(level, "levelTime", 554.7f); int spawnBefore = level.NormalMonstersSpawned;
            captureFocus = OralMapGeometry.PixelToWorld(map, new Vector2(701.5f, 620)); shotSize = map.WorldRect.height * .40f;
            bool sawStart = false, sawDry = false, sawWater = false, sawFull = false, sawJaw = false, sawSweep = false, sawStageOne = false, sweepProtection = false;
            float activatedAt = -1, transferredAt = -1, vortexStart = -1, sweepAt = -1, recordStart = Time.time;
            File.WriteAllText(Output + "/timeline.csv", "game_seconds,stage_seconds,stage,age,water01,vortices,jaw01,swallow01,monsters\n");
            yield return Record("01-full-finale-to-stage1", () => !sawStageOne || Time.time - transferredAt < 2f, () =>
            {
                CloseDialogs(level); Set(player, "isInvincible", true); Set(player, "currentHealth", player.MaxHealth); player.Move(Vector2.zero);
                if (stage.StageNumber == 0 && finale != null)
                {
                    if (finale.Active && !sawStart) { sawStart = true; activatedAt = level.CurrentLevelTime; }
                    if (finale.Active && finale.Age < 9.5f) sawDry |= finale.Water01 == 0;
                    if (finale.Age >= 11 && finale.Age <= 18) sawWater |= finale.Water01 > 0 && finale.Water01 < 1;
                    if (finale.Age >= 20 && !sawFull) { sawFull = true; vortexStart = level.CurrentLevelTime; Check(finale.Water01 >= .99f && finale.Whirlpools.Count > 0, "9:35 tongue flooding starts vortex survival"); }
                    if (finale.Age >= 20 && finale.Age < 40) sawJaw |= finale.JawClosure01 >= .99f;
                    if (finale.IsSwallowing && !sawSweep) { sawSweep = true; sweepAt = level.CurrentLevelTime; }
                    if (finale.IsSwallowing && !sweepProtection && finale.Age > 40.3f)
                    {
                        sweepProtection = true; float health = player.CurrentHealth; int charge = player.CurrentDashCharges;
                        Set(player, "isInvincible", false); player.TakeDamage(9999, Vector2.up * 50); player.TryDash();
                        bool skillUsed = player.Skills != null && player.Skills.TryActivate();
                        Check(player.CurrentHealth == health && player.IsAlive, "final sweep protects actual health even without the QA invincibility fixture");
                        Check(!player.IsDashing && player.CurrentDashCharges == charge && !skillUsed, "final sweep blocks dash and skill activation without spending charges");
                        Set(player, "isInvincible", true);
                    }
                    File.AppendAllText(Output + "/timeline.csv", F(Time.time - recordStart) + "," + F(level.CurrentLevelTime) + ",0," + F(finale.Age) + "," + F(finale.Water01) + "," + finale.Whirlpools.Count + "," + F(finale.JawClosure01) + "," + F(finale.Swallow01) + "," + level.EntityManager.LivingMonsters.Count + "\n");
                }
                if (stage.StageNumber == 1 && !stage.Travelling && !sawStageOne) { sawStageOne = true; transferredAt = Time.time; captureFocus = null; }
            }, 240);
            captureFocus = null;
            Check(sawStart && activatedAt >= 555 && activatedAt < 555.5f, "natural finale starts at 9:15 rather than before it");
            Check(sawDry && sawWater && sawFull, "dry opening, canyon fill and full-tongue flood occur in order");
            Check(vortexStart >= 575 && vortexStart < 575.8f, "vortex survival begins at 9:35");
            Check(sawJaw, "upper teeth remain closed during survival");
            Check(sawSweep && sweepAt >= 595 && sweepAt < 595.8f, "twenty seconds of survival precede the final swallow");
            Check(sweepProtection, "cutscene protection was exercised during the real final sweep");
            Check(sawStageOne && stage.StageNumber == 1 && !stage.Travelling, "final swallow completes the real stage-one transition");
            Check(!FiniteStomachMap.Instance.IsOral && !FiniteStomachMap.Instance.IsColon, "stage one restores the stomach map");
            Check(OralStageRuntime.Instance == null && OralSwallowFinale.Instance == null, "stage transfer removes oral runtime and finale ownership");
            Check(level.PlayerCharacter == player && player.Skills == skills && player.Relics == relics, "character, skill runtime and relic runtime survive the transfer");
            var after = player.CaptureRunSceneState();
            Check(Mathf.Abs(preserved.MaxHealthBonus - after.MaxHealthBonus) < .001f && Mathf.Abs(preserved.DamageMultiplier - after.DamageMultiplier) < .001f && Mathf.Abs(preserved.AttackSpeedMultiplier - after.AttackSpeedMultiplier) < .001f && player.CurrentLevel >= preserved.CurrentLevel, "grown health, damage, attack speed and player level are preserved");
            Check(!level.IsRunFlowPaused && player.GetComponent<Rigidbody2D>().simulated, "stage one releases time and player physics");
            var layout = level.GetComponent<FieldObjectLayout>();
            Check(layout != null && layout.Ready && layout.SpawnedObjects.Count(p => p != null && p.activeInHierarchy) > 20, "stage-one NPCs, boxes and field objects rebuild");
            Check(layout.Slots.Any(s => s.kind == FieldObjectLayout.Kind.Merchant) && layout.Slots.Any(s => s.kind == FieldObjectLayout.Kind.Chest) && layout.Slots.Any(s => s.kind == FieldObjectLayout.Kind.BloodClot), "stage-one layout includes merchants, chests and blood clots");
            float resumed = level.CurrentLevelTime; int resumedSpawn = level.NormalMonstersSpawned; var resumePosition = player.Position;
            player.Move(Vector2.right); yield return new WaitForSeconds(2f); player.Move(Vector2.zero);
            Check(level.CurrentLevelTime > resumed + 1.5f && level.CurrentLevelTime < 15, "stage-one clock resets and resumes normally");
            Check(level.NormalMonstersSpawned > resumedSpawn || level.EntityManager.LivingMonsters.Count > 0, "stage-one normal monster spawning resumes");
            Check(Vector2.Distance(resumePosition, player.Position) > .5f, "player can move after loading finishes");
            File.WriteAllText(Output + "/timing.json", "{\"naturalStart\":" + F(activatedAt) + ",\"vortices\":" + F(vortexStart) + ",\"swallow\":" + F(sweepAt) + ",\"preservedMaxHealthBonus\":" + F(after.MaxHealthBonus) + "}");
            SessionState.SetBool(Key + "Done", true);
        }
        static IEnumerator DenseEscapeChecks(LevelManager level, OralSwallowFinale finale)
        {
            var player = level.PlayerCharacter; var center = OralMapGeometry.TongueCenter(FiniteStomachMap.Instance);
            var states = finale.Whirlpools.ToArray();
            var saved = states.Select(w => new { w.Position, w.Radius, w.Angle, Direction = (Vector2)Field(w, "Direction").GetValue(w), Turn = (float)Field(w, "TurnRemaining").GetValue(w) }).ToArray();
            float normalCap = Physics2D.maxTranslationSpeed; var rows = new string[2];
            try
            {
                for (int i = 0; i < states.Length; i++)
                {
                    states[i].Position = center + (i < 7 ? new Vector2(-12 + i * 4, -4) : new Vector2(-2 + (i - 7) * .3f, 7));
                    states[i].Radius = i < 7 ? 2.5f : .2f; Set(states[i], "Direction", Vector2.zero); Set(states[i], "TurnRemaining", 999f);
                }
                for (int mode = 0; mode < 2; mode++)
                {
                    Physics2D.maxTranslationSpeed = mode == 0 ? normalCap : 1f;
                    Place(player, center + new Vector2(-13, -4)); Set(player, "currentDashCharges", player.MaxDashCharges); Set(player, "isInvincible", true); Set(player, "currentHealth", player.MaxHealth);
                    Vector2 start = player.Position;
                    Check(finale.TryGetDashExit(player, Vector2.right, out var target), "overlapping-chain fixture supplies a real vortex escape target");
                    float planned = Vector2.Distance(start, target);
                    Check(planned < 10f, "overlapping-chain escape chooses a nearby union boundary instead of a field-length launch");
                    int charges = player.CurrentDashCharges; player.Move(Vector2.right); player.TryDash(); player.Move(Vector2.zero);
                    Check(player.IsDashing, "overlapping-chain actual dash starts with physics cap " + Physics2D.maxTranslationSpeed);
                    float until = Time.realtimeSinceStartup + 5;
                    while (player.IsDashing && Time.realtimeSinceStartup < until) { CloseDialogs(level); yield return null; }
                    Check(!player.IsDashing && player.CurrentDashCharges == charges - 1, "overlapping-chain escape completes using one charge");
                    Check(states.All(w => Vector2.Distance(player.Position, w.Position) > w.Radius), "overlapping-chain actual endpoint is outside every vortex despite physics speed cap " + Physics2D.maxTranslationSpeed);
                    rows[mode] = "{\"physicsCap\":" + F(Physics2D.maxTranslationSpeed) + ",\"plannedDistance\":" + F(planned) + ",\"actualDistance\":" + F(Vector2.Distance(start, player.Position)) + "}";
                }
            }
            finally
            {
                Physics2D.maxTranslationSpeed = normalCap; player.Move(Vector2.zero);
                for (int i = 0; i < states.Length; i++) { states[i].Position = saved[i].Position; states[i].Radius = saved[i].Radius; states[i].Angle = saved[i].Angle; Set(states[i], "Direction", saved[i].Direction); Set(states[i], "TurnRemaining", saved[i].Turn); }
            }
            Check(Physics2D.maxTranslationSpeed == normalCap, "dense-chain fixture restores the original global physics cap");
            File.WriteAllText(Output + "/dense-chain-metrics.json", "[" + string.Join(",", rows) + "]");
        }
        static IEnumerator DashCase(LevelManager level, OralSwallowFinale finale, bool large, string name)
        {
            var player = level.PlayerCharacter; var map = FiniteStomachMap.Instance;
            var vortex = large ? finale.Whirlpools.OrderByDescending(w => w.Radius).First() : finale.Whirlpools.OrderBy(w => w.Radius).First();
            Vector2 center = vortex.Position; Vector2 towardCenter = (OralMapGeometry.TongueCenter(map) - center).normalized;
            if (towardCenter.sqrMagnitude < .1f) towardCenter = Vector2.right;
            Place(player, center + towardCenter * vortex.Radius * .55f); Set(player, "currentDashCharges", player.MaxDashCharges);
            captureFocus = center; shotSize = Mathf.Max(5, vortex.Radius * 1.7f);
            float began = Time.time, beforePull = Vector2.Distance(player.Position, vortex.Position), pullDistance = 0;
            bool dashStarted = false, dashEnded = false; int chargesBefore = 0; Vector2 start = default, end = default; float endRadius = 0, endDistance = 0;
            yield return Record(name, () => Time.time - began < 3.5f, () =>
            {
                CloseDialogs(level); Set(player, "isInvincible", true); Set(player, "currentHealth", player.MaxHealth);
                if (!dashStarted && Time.time - began >= .75f)
                {
                    pullDistance = beforePull - Vector2.Distance(player.Position, vortex.Position);
                    // Directed to the tongue interior so the demonstration does not
                    // substitute an edge-clamped jump for the intended escape.
                    player.Move(towardCenter); chargesBefore = player.CurrentDashCharges; start = player.Position;
                    player.TryDash(); dashStarted = player.IsDashing; player.Move(Vector2.zero);
                }
                if (dashStarted && !player.IsDashing && !dashEnded)
                {
                    dashEnded = true; end = player.Position; endRadius = vortex.Radius; endDistance = Vector2.Distance(end, vortex.Position);
                    Check(player.CurrentDashCharges == chargesBefore - 1, name + " consumes exactly one dash charge");
                    Check(endDistance > endRadius, name + " ends outside the vortex in a single actual dash");
                    Check(finale.Whirlpools.All(w => Vector2.Distance(end, w.Position) > w.Radius), name + " endpoint also clears every overlapping neighboring vortex");
                }
            });
            player.Move(Vector2.zero); captureFocus = null;
            Check(pullDistance > .03f, name + " visibly pulls a stationary player inward before escape");
            Check(dashStarted && dashEnded, name + " actual dash starts and finishes");
            File.WriteAllText(Output + "/" + name + "-metrics.json", "{\"radius\":" + F(endRadius) + ",\"endpointRadius\":" + F(endDistance) + ",\"dashTravel\":" + F(Vector2.Distance(start, end)) + ",\"normalDashDistance\":" + F(player.DashDistance) + ",\"pullDistance\":" + F(pullDistance) + "}");
        }
        static string F(float value) => value.ToString("F4", CultureInfo.InvariantCulture);
        static IEnumerator Record(string name, Func<bool> active, Action step, float timeoutSeconds = 90)
        {
            string dir = Output + "/" + name; Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "*.png")) File.Delete(old);
            File.WriteAllText(dir + "/times.csv", "frame,game_seconds\n");
            int oldRate = Time.captureFramerate, frame = 0; Time.captureFramerate = 15;
            float began = Time.time, timeout = Time.realtimeSinceStartup + timeoutSeconds;
            do
            {
                yield return null; step?.Invoke(); if (Index == 0) Frame(name + "/" + frame.ToString("D4"));
                File.AppendAllText(dir + "/times.csv", frame + "," + F(Time.time - began) + "\n"); frame++;
            } while (active() && Time.realtimeSinceStartup < timeout);
            Time.captureFramerate = oldRate; Check(!active(), name + " completes before the recording timeout");
        }
        static void Frame(string name)
        {
            var camera = Camera.main; if (camera == null) return;
            var overlays = Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras = overlays.Select(c => c.worldCamera).ToArray(); var distances = overlays.Select(c => c.planeDistance).ToArray();
            var orders = overlays.Select(c => c.sortingOrder).ToArray(); var flags = overlays.Select(c => c.overrideSorting).ToArray();
            var target = camera.targetTexture; var active = RenderTexture.active; var position = camera.transform.position; float zoom = camera.orthographicSize;
            var rt = RenderTexture.GetTemporary(960, 640, 24); Texture2D image = null;
            try
            {
                if (captureFocus.HasValue) { var p = captureFocus.Value; camera.transform.position = new Vector3(p.x, p.y, position.z); camera.orthographicSize = shotSize; }
                foreach (var presentation in Object.FindObjectsOfType<MonoBehaviour>())
                {
                    if (!presentation.enabled || !(presentation is JawCrushVisual || presentation is PlayerCombatBars || presentation is OralSwallowVisual || presentation is OralMapVisual)) continue;
                    var late = presentation.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); late?.Invoke(presentation, null);
                }
                camera.targetTexture = rt;
                foreach (var c in overlays) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = camera.nearClipPlane + .1f; c.overrideSorting = true; c.sortingOrder = Mathf.Clamp(32100 + c.sortingOrder, 32100, 32760); }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                image = new Texture2D(960, 640, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 960, 640), 0, 0); image.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < overlays.Length; i++) if (overlays[i] != null) { overlays[i].renderMode = RenderMode.ScreenSpaceOverlay; overlays[i].worldCamera = cameras[i]; overlays[i].planeDistance = distances[i]; overlays[i].sortingOrder = orders[i]; overlays[i].overrideSorting = flags[i]; }
                camera.targetTexture = target; camera.transform.position = position; camera.orthographicSize = zoom; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt); if (image != null) Object.Destroy(image); Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
