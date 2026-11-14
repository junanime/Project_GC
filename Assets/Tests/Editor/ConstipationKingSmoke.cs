#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Runs the real scene, director, colliders, coroutines and damage pipeline. This is
    // deterministic integration coverage; it is not a claim about human clear rates.
    [InitializeOnLoad]
    public static class ConstipationKingSmoke
    {
        const string Key = "ConstipationKingSmoke";
        static readonly string[] SavedIntPrefs = { "Coins", "LobbySilverCoins", "October.StartingNeedle.Ashi", "October.StartingNeedle.Ari", "October.StartingNeedle.Hyuki", "October.StartingNeedle.Shini" };
        static string Output => SessionState.GetBool(Key + "Death", false) ? "Library/ConstipationKingDeathProof" : SessionState.GetBool(Key + "Ending", false) ? "Library/ConstipationKingEndingProof" : "Library/ConstipationKingProof";
        static bool started; static double deadline; static int checks;
        static ConstipationKingSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        { SessionState.SetBool(Key + "Death", false); SessionState.SetBool(Key + "Ending", false); Start(); }
        public static void RunDeath()
        { SessionState.SetBool(Key + "Death", true); SessionState.SetBool(Key + "Ending", false); Start(); }
        public static void RunEnding()
        { SessionState.SetBool(Key + "Death", false); SessionState.SetBool(Key + "Ending", true); Start(); }
        static void Start()
        {
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/checks.txt", ""); File.WriteAllText(Output + "/errors.txt", "");
            File.WriteAllText(Output + "/method.txt", "Actual Unity Stage 2 scene; invulnerable stationary player, manual time boundary for scheduled boss, all 18 legal phase/pattern combinations (nine per phase), pause, status, wall and defeat integration. No simulated human success rate.\n");
            foreach (string pref in SavedIntPrefs) { SessionState.SetBool(Key + pref + "Had", PlayerPrefs.HasKey(pref)); SessionState.SetInt(Key + pref, PlayerPrefs.GetInt(pref)); }
            SessionState.SetString(Key + "OriginalCharacter", CrossSceneData.CharacterBlueprint != null ? AssetDatabase.GetAssetPath(CrossSceneData.CharacterBlueprint) : "");
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key + "Failed", false);
            SessionState.SetInt(Key + "Checks", 0);
            CrossSceneData.CharacterBlueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems = Array.Empty<MerchantItemBlueprint>(); CrossSceneData.ClearPendingRunSceneTransfer();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity"); Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started = false; checks = SessionState.GetInt(Key + "Checks", 0); deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update -= Tick; EditorApplication.update += Tick; Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string text, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            File.AppendAllText(Output + "/errors.txt", text + "\n" + stack + "\n"); SessionState.SetBool(Key + "Failed", true);
        }
        static void Check(bool condition, string title)
        {
            if (!condition) throw new Exception("[ConstipationKing] " + title);
            checks++; SessionState.SetInt(Key + "Checks", checks); File.AppendAllText(Output + "/checks.txt", title + "\n");
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) { File.AppendAllText(Output + "/errors.txt", "Deadline\n"); SessionState.SetBool(Key + "Failed", true); }
            if (SessionState.GetBool(Key + "Done", false) || SessionState.GetBool(Key + "Failed", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                foreach (string pref in SavedIntPrefs) { if (SessionState.GetBool(Key + pref + "Had", false)) PlayerPrefs.SetInt(pref, SessionState.GetInt(Key + pref, 0)); else PlayerPrefs.DeleteKey(pref); } PlayerPrefs.Save();
                string originalCharacter = SessionState.GetString(Key + "OriginalCharacter", "");
                CrossSceneData.CharacterBlueprint = string.IsNullOrEmpty(originalCharacter) ? null : AssetDatabase.LoadAssetAtPath<CharacterBlueprint>(originalCharacter);
                Time.timeScale = 1; SessionState.SetBool(Key, false); bool fail = SessionState.GetBool(Key + "Failed", false);
                File.WriteAllText(Output + "/result.txt", (fail ? "FAIL" : "PASS") + " checks=" + SessionState.GetInt(Key + "Checks", 0));
                EditorApplication.Exit(fail ? 1 : 0); return;
            }
            if (!EditorApplication.isPlaying) return;
            var tutorial = Object.FindObjectOfType<TutorialGuide>(); if (tutorial != null) { tutorial.Close(); tutorial.enabled = false; }
            var level = Object.FindObjectOfType<LevelManager>();
            if (started || level == null || !level.InitialFieldObjectsReady || level.PlayerCharacter == null) return;
            started = true;
            var host = new GameObject("King integration QA across ending scene changes"); Object.DontDestroyOnLoad(host);
            host.AddComponent<StageEntryLoadingTestHost>().StartCoroutine(Exercise(level));
        }
        static IEnumerator WaitFor(Func<bool> predicate, float timeout, string title)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < end) yield return null;
            Check(predicate(), title);
        }
        static void Capture(ConstipationKingRuntime boss, string name, bool frameBoss = true, bool overlay = false)
        {
            var camera = Camera.main; if (camera == null) return;
            var before = camera.transform.position; var zoom = camera.orthographicSize; var target = camera.targetTexture; var active = RenderTexture.active;
            var canvases = overlay ? Object.FindObjectsOfType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay && c.isActiveAndEnabled).ToArray() : Array.Empty<Canvas>();
            var cameras = canvases.Select(c => c.worldCamera).ToArray(); var distances = canvases.Select(c => c.planeDistance).ToArray();
            var orders = canvases.Select(c => c.sortingOrder).ToArray(); var sorting = canvases.Select(c => c.overrideSorting).ToArray();
            var rt = RenderTexture.GetTemporary(1280, 720, 24); Texture2D screenshot = null;
            try
            {
                if (frameBoss && boss != null) { camera.transform.position = new Vector3(boss.transform.position.x + 2, boss.transform.position.y + 2, before.z); camera.orthographicSize = 8; }
                foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .1f; canvas.overrideSorting = true; canvas.sortingOrder = Mathf.Clamp(32100 + canvas.sortingOrder, 32100, 32760); }
                Canvas.ForceUpdateCanvases();
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                screenshot = new Texture2D(1280, 720, TextureFormat.RGB24, false); screenshot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); screenshot.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", screenshot.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; canvases[i].sortingOrder = orders[i]; canvases[i].overrideSorting = sorting[i]; }
                camera.transform.position = before; camera.orthographicSize = zoom; camera.targetTexture = target; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt);
                if (screenshot != null) Object.Destroy(screenshot);
                Canvas.ForceUpdateCanvases();
            }
        }
        static IEnumerator ObservePattern(ConstipationKingRuntime boss, int phase, int pattern)
        {
            float start = Time.time, timeout = Time.realtimeSinceStartup + 20; int frame = 0;
            while (boss.Busy && Time.realtimeSinceStartup < timeout)
            {
                if (frame < 3 && Time.time - start >= new[] { .65f, 1.75f, 3.2f }[frame]) { Capture(boss, "phase-" + phase + "-pattern-" + pattern + "-" + frame); frame++; }
                yield return null;
            }
            Check(!boss.Busy, "Finish phase " + phase + " pattern " + pattern);
        }
        static IEnumerator PrimeTributeMeal(ConstipationKingRuntime boss)
        {
            Check(boss.PlayPattern(7), "Start controlled tribute interruption check"); yield return new WaitForSeconds(1);
            var carriers = Object.FindObjectsOfType<ConstipationKingMinion>().Where(m => m.IsTribute && m.Health > 0).ToArray();
            Check(carriers.Length == 3, "Three independent carriers offer phase-one tribute");
            foreach (var carrier in carriers.Skip(1)) carrier.TakeDamage(10000);
            var delivery = carriers[0]; delivery.transform.position = boss.transform.position + Vector3.right;
            delivery.GetComponent<Rigidbody2D>().position = delivery.transform.position;
            yield return WaitFor(() => delivery != null && delivery.IsChewing, 8, "Food delivery reaches active chewing before interruption");
        }
        static IEnumerator CheckFlush(ConstipationKingRuntime boss, LevelManager level, int phase)
        {
            yield return new WaitForSeconds(.1f);
            int expected = phase == 2 ? 4 : 1;
            Check(boss.Effects.GetComponentsInChildren<LineRenderer>().Count(r => r.name.StartsWith("Flush warning")) == expected, "Flush marks all " + expected + " independent locations before activation");
            Check(Object.FindObjectsOfType<ConstipationKingVortex>().Length == 0, "Flush damage waits for the lever warning");
            yield return new WaitForSeconds(1.25f);
            var vortices = Object.FindObjectsOfType<ConstipationKingVortex>();
            Check(vortices.Length == expected, "Phase " + phase + " creates exactly " + expected + " independent vortices");
            var map = FiniteStomachMap.Instance;
            float[] expectedRadii = phase == 2 ? new[] { 2.6f * 82 / 178, 2.6f * 101 / 178, 2.6f * 105 / 178, 2.6f * 78 / 178 } : new[] { 2.6f };
            foreach (var radius in expectedRadii) Check(vortices.Any(v => Mathf.Abs(v.Radius - radius) < .001f), "V3 proportional vortex radius " + radius.ToString("F3"));
            for (int i = 0; i < vortices.Length; i++)
            {
                var v = vortices[i];
                Check(map.FloorClearance(v.Center, true) >= v.Radius + .1f && v.CurrentRadius <= v.Radius, "Entire maximum vortex circle fits clear floor");
                Check(v.transform.parent == boss.Effects && v.GetComponentsInChildren<LineRenderer>().Length == 4, "Vortex owns its warning and three swirls without a boss connector");
                for (int j = 0; j < i; j++) Check(Vector2.Distance(v.Center, vortices[j].Center) >= v.Radius + vortices[j].Radius + .5f, "Vortices leave a traversable gap");
            }
            var ages = vortices.Select(v => v.Age).ToArray(); var sizes = vortices.Select(v => v.CurrentRadius).ToArray();
            level.SetRunFlowPaused(true); yield return new WaitForSecondsRealtime(.2f);
            Check(vortices.Select((v, i) => v.Age == ages[i] && v.CurrentRadius == sizes[i]).All(v => v), "All vortex growth and damage clocks freeze together on pause");
            level.SetRunFlowPaused(false); MiniStageRuntimeState.EnterMiniStage(null); yield return new WaitForSecondsRealtime(.15f);
            Check(vortices.Select((v, i) => v.Age == ages[i] && v.CurrentRadius == sizes[i]).All(v => v), "Mini-room state freezes every independent vortex");
            MiniStageRuntimeState.ExitMiniStage(null); yield return new WaitForSeconds(1.1f);
            Check(vortices.All(v => Mathf.Abs(v.CurrentRadius - v.Radius) < .001f), "Visible water and suction reach the same authored radius");
            Capture(boss, "phase-" + phase + "-flush-independent-vortices");
        }
        static IEnumerator CheckPaper(ConstipationKingRuntime boss, int phase)
        {
            yield return new WaitForSeconds(.5f);
            var lanes = boss.Effects.GetComponentsInChildren<ConstipationKingPaper>();
            Check(lanes.Length == (phase == 2 ? 3 : 1), "Paper creates the required one or three independent strips");
            var origins = lanes.Select(p => p.Origin).ToArray();
            if (phase == 1)
            {
                var lane = lanes[0];
                var strip = (LineRenderer)ColonContentSmoke.Field(lane, "paper").GetValue(lane);
                var roll = (SpriteRenderer)ColonContentSmoke.Field(lane, "roll").GetValue(lane);
                yield return WaitFor(() => lane != null && Vector2.Distance(strip.GetPosition(0), origins[0]) > .02f, 8, "Whole paper strip detaches at maximum range");
                Check(strip.enabled && roll.enabled && Vector2.Distance(strip.GetPosition(1), roll.transform.position) < .01f, "Detached strip and roll remain joined and visible");
                Vector2 detached = strip.GetPosition(0); float alpha = roll.color.a;
                Capture(boss, "phase-1-paper-whole-strip-flight");
                yield return new WaitForSeconds(.06f);
                Check(lane != null && Vector2.Distance(strip.GetPosition(0), detached) > .05f && roll.color.a < alpha, "Whole paper strip and core fly away and fade together immediately");
            }
            else
            {
                yield return new WaitForSeconds(.8f);
                Check(lanes.All(p => Vector2.Distance(p.Tip, p.Origin) > .01f) && boss.Visual.DisplayedPoseIndex == 5, "Three paper strips launch before the staff slam");
                yield return new WaitForSeconds(.6f);
                Check(boss.Visual.DisplayedPoseIndex == 6 && lanes.Select((p, i) => p.Origin == origins[i]).All(v => v), "Staff slams during outward travel without moving any strip origin");
                Capture(boss, "phase-2-paper-outbound-staff-slam");
                yield return new WaitForSeconds(1.5f);
                var distances = lanes.Select(p => Vector2.Distance(p.Tip, p.Origin)).ToArray();
                yield return new WaitForSeconds(.5f);
                Check(lanes.Select((p, i) => p != null && p.Origin == origins[i] && Vector2.Distance(p.Tip, p.Origin) < distances[i]).All(v => v), "Every paper lane rewinds straight toward its unchanged source");
                Capture(boss, "phase-2-paper-straight-rewind");
            }
        }
        static IEnumerator CheckSummon(ConstipationKingRuntime boss, LevelManager level, int phase)
        {
            yield return new WaitForSeconds(1.35f);
            var pool = boss.Effects.GetComponentInChildren<ConstipationKingOverflowVisual>();
            Check(pool != null && pool.Progress < 1 && boss.Effects.GetComponentsInChildren<ConstipationKingMinion>().Length == 0, "Seat-gap spill and expanding pool precede summoned monsters");
            float progress = pool.Progress; level.SetRunFlowPaused(true); yield return new WaitForSecondsRealtime(.2f);
            Check(pool.Progress == progress, "Overflow expansion freezes during run pause"); level.SetRunFlowPaused(false);
            yield return WaitFor(() => boss.Effects.GetComponentsInChildren<ConstipationKingMinion>().Length > 0, 6, "First summoned monster appears after pool formation");
            int expected = phase == 2 ? 7 : 5;
            Check(pool.Progress == 1 && Mathf.Abs(pool.VisibleRadius - ConstipationKingRuntime.Size * 1.5f) < .01f, "Summoning waits for the full circle at one and a half body lengths");
            Check(boss.Effects.GetComponentsInChildren<ConstipationKingMinion>().Length < expected, "Summoned monsters emerge separately instead of all at once");
            Capture(boss, "phase-" + phase + "-summon-expansion-and-first-minion");
            yield return WaitFor(() => boss.Effects.GetComponentsInChildren<ConstipationKingMinion>().Length == expected, 6, "Summon produces exactly " + expected + " living minions");
            Check(boss.Effects.GetComponentsInChildren<ConstipationKingMinion>().All(m => FiniteStomachMap.Instance.FloorClearance(m.transform.position, true) >= .35f), "Every summoned actor remains on safe floor");
            Capture(boss, "phase-" + phase + "-summon-full-group");
        }
        static void Silence(LevelManager level)
        {
            level.enabled = false;
            foreach (var component in Object.FindObjectsOfType<MonoBehaviour>())
                if (component is StageEventDirector || component is TimedSpecialMonsterSpawner || component is AcidToadSpawn || component is NPCSpawner || component is SnailBossDebugSpawn)
                { component.enabled = false; component.StopAllCoroutines(); }
            foreach (var ability in Object.FindObjectsOfType<Ability>()) { ability.enabled = false; ability.StopAllCoroutines(); }
        }
        static IEnumerator VerifyEndingReturn(LevelManager level, ConstipationKingRuntime boss, ConstipationKingDirector director, string round)
        {
            var exit = ColonExitSequence.Ensure(level);
            var player = level.PlayerCharacter;
            Check(exit.State == ColonExitSequence.ExitState.Locked && !exit.Opening.IsOpen, round + " seal is present and closed before boss defeat");
            yield return Interact(player, exit.Opening);
            Check(exit.State == ColonExitSequence.ExitState.Locked && !exit.Opening.IsOpen, round + " real gate interaction cannot unlock a living boss seal");
            CaptureExit(exit, round + "-sealed-exit", false);
            if (!boss.PhaseTwo)
            {
                boss.TakeDamage(999999);
                yield return WaitFor(() => boss.PhaseTwo && !boss.Transitioning, 15, round + " reaches final phase before game ending");
            }
            var score = RunScore.Current; var data = score.Data; int bossCount = data.boss;
            var stats = Object.FindObjectOfType<StatsManager>(); stats.IncreaseCoinsGained(7);
            int expectedCoins = ProgressPrefs.GetInt("Coins") + stats.CoinsGained;
            int silver = ProgressPrefs.GetInt("LobbySilverCoins");
            string relic = ProgressPrefs.GetString("EquippedRelicId");
            var character = CrossSceneData.CharacterBlueprint;
            string weaponKey = StartingNeedleSelection.SaveKey(character);
            int weapon = ProgressPrefs.GetInt(weaponKey);
            float deathAt = Time.time;
            boss.TakeDamage(999999); boss.TakeDamage(999999);
            Check(boss.Dead && boss.Health == 0 && !boss.GetComponent<Collider2D>().enabled, round + " final death disables body and ignores duplicate lethal hits");
            Check(data.boss == bossCount + 1, round + " final boss score increments exactly once");
            yield return new WaitForSeconds(.35f);
            Check(level != null && !level.IsLevelEnded && !data.success, round + " existing defeat animation finishes before success is committed");
            var dropAt = exit.Key.position; level.SetRunFlowPaused(true);
            yield return new WaitForSecondsRealtime(.15f);
            Check(exit.Key.position == dropAt, round + " dropped key freezes with run pause"); level.SetRunFlowPaused(false);
            yield return WaitFor(() => exit.State == ColonExitSequence.ExitState.Dropped, 8, round + " intact key lands and becomes interactive after defeat");
            level.LevelPassed(null);
            Check(!level.IsLevelEnded && director.Defeated && !data.success, round + " defeating king cannot bypass key and demo confirmation");
            ColonContentSmoke.Position(player, FiniteStomachMap.Instance.ClampToFloor((Vector2)exit.Pickup.transform.position + Vector2.right * 8, .5f));
            Check(!exit.TakeKey(player), round + " key cannot be taken from outside interaction range");
            yield return Interact(player, exit.Pickup);
            Check(exit.State == ColonExitSequence.ExitState.Carrying && !exit.Pickup.gameObject.activeSelf, round + " focused trigger interaction picks up exactly one key");
            Check(!exit.TakeKey(player), round + " repeated pickup cannot duplicate carried key");
            var carryAt = exit.Key.position; var previousPlayer = player.transform.position;
            ColonContentSmoke.Position(player, FiniteStomachMap.Instance.ClampToFloor((Vector2)player.transform.position + Vector2.right * 2, .5f));
            yield return null; yield return null;
            Check(exit.Key.position.y > player.transform.position.y && Mathf.Abs((exit.Key.position.x - carryAt.x) - (player.transform.position.x - previousPlayer.x)) < .2f, round + " overhead key follows player movement");
            CaptureExit(exit, round + "-carrying-key", true);
            if (round == "first-clear") yield return VerifyCarryCharacters(exit, player);
            MiniStageRuntimeState.EnterMiniStage(null); yield return null;
            Check(!exit.CanInteract && !exit.Key.gameObject.activeSelf, round + " mini-stage hides carried key and disables exit");
            MiniStageRuntimeState.ExitMiniStage(null); yield return null;
            yield return Interact(player, exit.Opening);
            Check(exit.State == ColonExitSequence.ExitState.Unlocking && level.IsRunFlowPaused, round + " gate interaction begins controlled insertion");
            ColonContentSmoke.Set(player, "isInvincible", false);
            float protectedHealth = player.CurrentHealth; Vector2 protectedVelocity = player.Velocity;
            player.TakeDamage(999999, Vector2.right * 100); player.TakeAcidLakeDamage(1, true); player.Knockback(Vector2.right * 100);
            Check(player.IsAlive && player.CurrentHealth == protectedHealth && player.Velocity == protectedVelocity, round + " unlock owns scoped damage and knockback protection without invincibility mutation");
            Check(!BloodClotTravel.CanTravel(player) && !player.TryDash() && !player.Skills.CanActivate, round + " unlock rejects portal travel dash and skills");
            var positionAtUnlock = player.transform.position; player.Move(Vector2.right);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(Vector2.Distance(positionAtUnlock, player.transform.position) < .02f, round + " player stays at lock throughout controlled animation"); player.Move(Vector2.zero);
            yield return new WaitForSeconds(.5f); CaptureExit(exit, round + "-key-insertion", false);
            var frozenKey = exit.Key.position; float oldScale = Time.timeScale; Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.15f);
            Check(exit.Key.position == frozenKey && !exit.CanInteract, round + " unlock respects pause and rejects repeated interactions"); Time.timeScale = oldScale;
            yield return WaitFor(() => exit.State == ColonExitSequence.ExitState.Open, 8, round + " lock fragments and chains clear before passage opens");
            Check(exit.Opening.IsOpen && !level.IsRunFlowPaused && !level.IsLevelEnded, round + " opening alone does not finish the run");
            Check(!player.IsColonExitProtected && BloodClotTravel.CanTravel(player), round + " scoped protection and travel restriction release after opening");
            player.TakeAcidLakeDamage(.1f, false);
            Check(player.CurrentHealth < protectedHealth, round + " ordinary damage works again after unlock"); player.GainHealth(player.MaxHealth); ColonContentSmoke.Set(player, "isInvincible", true);
            CaptureExit(exit, round + "-open-exit", false);
            yield return Interact(player, exit.Opening);
            Check(exit.State == ColonExitSequence.ExitState.Demo && exit.Dialogue != null && exit.Dialogue.Message.text == "데모 버전은 여기까지입니다!", round + " separate exit interaction shows exact festival demo message");
            Check(Time.timeScale == 0 && !level.IsLevelEnded && !data.success, round + " demo waits for explicit confirmation before saving success");
            ApothecaryUI.Instance.Back(); ApothecaryUI.Instance.OpenRunBook(); ApothecaryUI.Instance.OpenSettings();
            yield return null;
            Check(exit.Dialogue != null && exit.Dialogue.gameObject.activeInHierarchy && ApothecaryUI.Instance.Page == "hud", round + " Escape and journal routes cannot cover or dismiss demo modal");
            yield return null; CaptureExit(exit, round + "-demo-completion", false);
            if (round == "first-clear") exit.Dialogue.ConfirmButton.onClick.Invoke();
            else UnityEngine.EventSystems.ExecuteEvents.Execute(exit.Dialogue.ConfirmButton.gameObject, new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.submitHandler);
            exit.Dialogue.Confirm();
            bool resultShown = false, duplicateChecked = false;
            float until = Time.realtimeSinceStartup + 30;
            while (Time.realtimeSinceStartup < until && !(SceneManager.GetActiveScene().buildIndex == 0 && ApothecaryUI.Instance != null && ApothecaryUI.Instance.Page == "prepare"))
            {
                resultShown |= ApothecaryUI.Instance != null && ApothecaryUI.Instance.Page == "result";
                if (!duplicateChecked && level != null && level.IsLevelEnded)
                {
                    Check(director.Defeated, round + " mandatory final completion recorded before returning");
                    director.NotifyDefeated(boss); level.LevelPassed(null); level.LevelPassed(null); GameEndingFlow.Begin(level);
                    duplicateChecked = true;
                }
                yield return null;
            }
            Check(SceneManager.GetActiveScene().buildIndex == 0 && ApothecaryUI.Instance != null && ApothecaryUI.Instance.Page == "prepare", round + " confirmed demo returns to the actual preparation screen");
            Check(Time.time - deathAt >= 1.25f && !resultShown, round + " preserves defeat duration and never opens the success result panel");
            Check(duplicateChecked, round + " duplicate completion callbacks were exercised before scene unload");
            score.Finish(false); score.Finish(true);
            Check(data.success && data.boss == bossCount + 1, round + " successful score survives scene unload and repeated finish calls");
            Check(ProgressPrefs.GetInt("Coins") == expectedCoins, round + " earned gold is saved exactly once");
            Check(ProgressPrefs.GetInt("LobbySilverCoins") == silver && ProgressPrefs.GetString("EquippedRelicId") == relic && ProgressPrefs.GetInt(weaponKey) == weapon && CrossSceneData.CharacterBlueprint == character,
                round + " currency and original character, weapon and relic selections remain intact");
            Check(Time.timeScale == 1 && !StageEntryLoading.IsLoading && !IntroMovie.Active && Object.FindObjectOfType<ConstipationKingRuntime>() == null && RunScore.Current == null,
                round + " preparation is interactive with no old encounter, loading overlay or intro");
            // Exercise the actual localization asset path after every unload/reload.
            // Fonts load by their TMP type rather than the package's nullable Object[] preload cache.
            var font = UnityEngine.Localization.Settings.LocalizationSettings.AssetDatabase.GetLocalizedAssetAsync<TMPro.TMP_FontAsset>("Fonts", "Main Font");
            yield return font;
            Check(font.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded && font.Result != null,
                round + " localized TMP font loads successfully after scene unload");
            yield return new WaitForSecondsRealtime(.5f);
            Capture(null, round + "-preparation-after-ending", false, true);
        }
        static IEnumerator Interact(Character player, InteractableEventObject target)
        {
            ColonContentSmoke.Position(player, target.transform.position);
            Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return null;
            Check(InteractionFocus.SelectTouched(target), "Real trigger registers interaction focus: " + target.name);
            yield return null; target.TryInteract(); yield return null;
        }
        static IEnumerator VerifyCarryCharacters(ColonExitSequence exit, Character player)
        {
            // Rebind the actual animation renderer through Character's normal visual API.
            // Combat stats and persistent character selection are intentionally untouched.
            var original = player.Blueprint;
            var renderer = player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
            foreach (string name in new[] { "Main", "Ari", "Hyuki", "Shini" })
            {
                var blueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/" + (name == "Main" ? "Main Character Blueprint" : name + " Character Blueprint") + ".asset");
                Check(blueprint != null, "Real carry visual blueprint exists: " + name);
                ColonContentSmoke.Set(player, "characterBlueprint", blueprint); player.StopWalkAnimation(); player.StartIdleAnimation();
                yield return null; yield return null;
                var opaque = PromptSpriteGeometry.WorldBounds(renderer);
                Check(renderer.sprite != null && exit.Key.position.y > opaque.max.y && exit.Key.position.y - opaque.max.y < .4f, name + " actual opaque character silhouette carries key just above head");
                var before = exit.Key.position;
                ColonContentSmoke.Position(player, FiniteStomachMap.Instance.ClampToFloor((Vector2)player.transform.position + Vector2.right * .6f, .5f));
                yield return null; yield return null;
                Check(exit.Key.position.x > before.x + .3f, name + " overhead carry tracks movement with own visual dimensions");
                CaptureExit(exit, "carry-character-" + name, true);
            }
            ColonContentSmoke.Set(player, "characterBlueprint", original); player.StopWalkAnimation(); player.StartIdleAnimation();
            yield return null;
        }
        static void CaptureExit(ColonExitSequence exit, string name, bool close)
        {
            var camera = Camera.main; if (camera == null) return;
            var position = camera.transform.position; float zoom = camera.orthographicSize;
            var player = Object.FindObjectOfType<LevelManager>().PlayerCharacter;
            Vector2 point = close ? (Vector2)player.transform.position + Vector2.up : Vector2.Lerp(exit.Opening.Center, player.transform.position, .45f);
            camera.transform.position = new Vector3(point.x, point.y, position.z); camera.orthographicSize = close ? 3.4f : 9;
            Capture(null, name, false, true); camera.transform.position = position; camera.orthographicSize = zoom;
        }
        static IEnumerator RepeatEndingFromPreparation()
        {
            var ui = ApothecaryUI.Instance;
            var ari = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Ari Character Blueprint.asset");
            ColonContentSmoke.Set(ui, "character", ari); ColonContentSmoke.Set(ui, "characterIndex", Array.IndexOf(ui.Config.characters, ari));
            ui.Show("prepare"); ui.StartRun();
            var stageButton = ui.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(b => b.GetComponentInChildren<TMPro.TMP_Text>()?.text == "스테이지 2 · 대장");
            Check(stageButton != null && stageButton.interactable, "Returned preparation offers a working second departure");
            stageButton.onClick.Invoke();
            LevelManager level = null;
            float until = Time.realtimeSinceStartup + 120;
            while (Time.realtimeSinceStartup < until)
            {
                level = Object.FindObjectOfType<LevelManager>();
                if (RunScore.Current != null) RunScore.Exclude("Repeated game ending automated QA");
                if (level != null && level.InitialFieldObjectsReady && StageProgression.Ensure(level).StageNumber == 2 && !StageProgression.Ensure(level).Travelling && !StageEntryLoading.IsLoading) break;
                yield return null;
            }
            Check(level != null && StageProgression.Ensure(level).StageNumber == 2 && !StageProgression.Ensure(level).Travelling && !StageEntryLoading.IsLoading, "Actual preparation button loads a fresh playable Stage 2");
            Check(level.PlayerCharacter.Blueprint == ari, "Second departure fully initializes Ari through actual preparation launch");
            Silence(level); Time.timeScale = 2;
            ColonContentSmoke.Set(level.PlayerCharacter, "isInvincible", true); level.PlayerCharacter.CollectableCollider.enabled = false;
            foreach (var monster in level.EntityManager.LivingMonsters.ToArray()) monster.DespawnForStageTransfer();
            var director = ConstipationKingDirector.Ensure(level); director.enabled = false;
            ColonContentSmoke.Set(level, "levelTime", ConstipationKingDirector.ArrivalSeconds);
            Check(director.TrySpawn(), "Second departure gets its own mandatory final boss");
            var boss = director.Boss; boss.AutoPatterns = false;
            yield return WaitFor(() => !boss.Arriving, 15, "Second final encounter arrival finishes");
            yield return VerifyEndingReturn(level, boss, director, "second-clear");
        }
        static IEnumerator Exercise(LevelManager level)
        {
            var preferences = GamePreferences.Current.Copy(); preferences.pauseOnFocusLoss = false; preferences.muteOnFocusLoss = false; GamePreferences.Apply(preferences, false);
            Application.runInBackground = true; RunScore.Exclude("Constipation King integration validation"); Silence(level);
            yield return ColonContentSmoke.Enter(level); Silence(level); Time.timeScale = 2;
            var player = level.PlayerCharacter; ColonContentSmoke.Set(player, "isInvincible", true); player.CollectableCollider.enabled = false;
            foreach (var monster in level.EntityManager.LivingMonsters.ToArray()) monster.DespawnForStageTransfer();
            var director = ConstipationKingDirector.Ensure(level); director.enabled = false;
            ColonContentSmoke.Set(level, "levelTime", 899f);
            Check(!director.TrySpawn() && !director.Spawned, "No final encounter at 899 seconds");
            ColonContentSmoke.Set(level, "levelTime", 900f); level.SetRunFlowPaused(true);
            Check(!director.TrySpawn(), "The 15-minute trigger waits for field pause to finish"); level.SetRunFlowPaused(false);
            Check(director.TrySpawn() && director.Spawned, "15-minute trigger spawns without any optional boss kills");
            var boss = director.Boss; boss.AutoPatterns = false;
            Check(!director.TrySpawn() && ConstipationKingRuntime.Active.Count == 1, "Repeated spawn calls cannot duplicate the king");
            Check(boss.MaxHealth == 34000 && boss.Health == 34000, "Fixed king health is 34000");
            Check(ColonBossDefinition.RequiredFinalBossImplemented, "Mandatory final-boss readiness flag is enabled");
            level.LevelPassed(null); Check(!level.IsLevelEnded, "Legacy victory callback cannot bypass required king");
            var map = FiniteStomachMap.Instance; var opening = StomachOpening.Find(false);
            Check(opening != null && Vector2.Distance(boss.transform.position, opening.FrontLip) < 12, "Spawn is the lower-left opening approach");
            Check(map.FloorClearance(boss.transform.position) >= ConstipationKingRuntime.BodyRadius, "Arrival root is on navigable ground");
            ColonContentSmoke.Position(player, map.ClampToFloor((Vector2)boss.transform.position + Vector2.right * 7, .5f));
            yield return WaitFor(() => !boss.Arriving, 15, "Arrival completes in real play mode");
            Check(NeedleTargeting.IsValidBossTarget(boss, ~0) && BossTargetUtility.IsBossTarget(boss), "Needle targeting and boss multipliers recognize king");
            Check(Ver4HitEffects.Health(boss) == 34000 && Ver4HitEffects.MaxHealth(boss) == 34000, "Damage accounting reads actual king HP");
            Check(boss.Visual.Body.sprite != null && boss.Visual.CentralJetCount == 1, "Approved body is loaded with exactly one central water column");
            yield return new WaitForSeconds(.7f);
            var framing = Camera.main != null ? Camera.main.GetComponent<ColonBossEncounterCamera>() : null;
            Check(framing != null && framing.FramingFinalBoss, "Actual play camera recognizes the nearby final king");
            var bounds = boss.Visual.Body.bounds; Vector3 lower = Camera.main.WorldToViewportPoint(bounds.min), upper = Camera.main.WorldToViewportPoint(bounds.max);
            File.WriteAllText(Output + "/baseline-camera.txt", "body=" + bounds + "\nlower=" + lower + "\nupper=" + upper + "\nplayer=" + player.transform.position + "\nboss=" + boss.transform.position + "\ncamera=" + Camera.main.transform.position + "\nzoom=" + Camera.main.orthographicSize);
            Capture(boss, "baseline-actual-camera-hud", false, true);
            Check(lower.x >= 0 && lower.y >= 0 && upper.x <= 1 && upper.y <= 1, "Actual camera fits the full approved body without changing its world size");
            if (SessionState.GetBool(Key + "Ending", false))
            {
                yield return VerifyEndingReturn(level, boss, director, "first-clear");
                yield return RepeatEndingFromPreparation();
                SessionState.SetBool(Key + "Done", true); yield break;
            }
            if (SessionState.GetBool(Key + "Death", false))
            {
                var mealCenter = map.ClampToFloor(map.UVToWorld(new Vector2(.76f, .72f)), 3);
                boss.transform.position = mealCenter; boss.GetComponent<Rigidbody2D>().position = mealCenter;
                ColonContentSmoke.Position(player, map.ClampToFloor(mealCenter + Vector2.right * 7, .5f)); Physics2D.SyncTransforms();
                boss.TakeDamage(2000);
                Check(boss.Health < boss.MaxHealth && !boss.PhaseTwo && boss.PlayPattern(7), "Wounded phase-one king begins actual tribute before death scenario");
                ConstipationKingMinion eatingCarrier = null;
                yield return WaitFor(() =>
                {
                    eatingCarrier = boss.Effects.GetComponentsInChildren<ConstipationKingMinion>().FirstOrDefault(m => m.IsTribute && m.IsChewing);
                    return eatingCarrier != null;
                }, 15, "Real tribute courier reaches the king and starts active chewing");
                Check(boss.Visual.Pose == KingPose.Eat && boss.Busy, "Player death is triggered during the active tribute chew window");
                float healthDuringChew = boss.Health;
                var liveShot = ConstipationKingProjectile.Create(boss, boss.HitCenter, Vector2.up, 3, 12, .2f);
                Check(eatingCarrier != null && liveShot != null && boss.Effects.childCount > 0, "Tribute courier and live projectile are owned effects before player death");
                ColonContentSmoke.Set(player, "isInvincible", false); player.TakeDamage(999999);
                yield return WaitFor(() => level.IsLevelEnded && !player.IsAlive, 10, "Actual player death opens loss result");
                yield return null; yield return null;
                Check(Time.timeScale == 0 && boss.Effects.childCount == 0 && !boss.Busy && eatingCarrier == null && liveShot == null,
                    "Player death during chewing cancels tribute and destroys all owned hazards at timeScale zero");
                boss.FinishTributeMeal(boss.MaxHealth * .025f);
                yield return new WaitForSecondsRealtime(.8f);
                Check(boss.Health == healthDuringChew, "Cancelled chewing and late tribute completion cannot heal after player death");
                Check(!director.Defeated && !RunScore.Current.Data.success, "Player death never awards king victory");
                GameEndingFlow.Begin(level);
                Check(level.GetComponent<GameEndingFlow>() == null && ApothecaryUI.Instance.Page == "result",
                    "Ending flow rejects a failed run and keeps the existing loss result");
                Capture(boss, "loss-result-hud", false, true);
                float remainingHealth = boss.Health; boss.TakeDamage(999999);
                Check(boss.Health == remainingHealth, "Damage callbacks after player death cannot complete boss encounter");
                SessionState.SetBool(Key + "Done", true); yield break;
            }

            var needlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Junhan/Prefabs/Projectiles/SyringeProjectile.prefab");
            int needlePool = level.EntityManager.AddPoolForProjectile(needlePrefab);
            float needleHp = boss.Health;
            var needle = level.EntityManager.SpawnProjectile(needlePool, boss.HitCenter + Vector2.right * 3, 30, 0, 12, 1 << boss.gameObject.layer);
            needle.Launch(Vector2.left);
            yield return WaitFor(() => boss.Health < needleHp, 6, "Real pooled syringe collides and damages king");
            Check(boss.Visual.Body.GetComponentsInChildren<StuckNeedleVisual>().Length > 0, "Actual needle embeds on approved king body");
            foreach (var p in Object.FindObjectsOfType<Projectile>()) p.RemoveForItem();

            boss.PlayPattern(0); yield return new WaitForSeconds(.85f);
            var shot = Object.FindObjectsOfType<ConstipationKingProjectile>().FirstOrDefault();
            Check(shot != null && Vector2.Dot(shot.Velocity.normalized, boss.AimFrom(shot.transform.position)) > .8f, "Basic shot travels toward the player");
            Vector2 frozen = shot.transform.position; float frozenVisual = boss.Visual.AnimationTime;
            Time.timeScale = 0; yield return new WaitForSecondsRealtime(.25f);
            Check((Vector2)shot.transform.position == frozen && boss.Visual.AnimationTime == frozenVisual, "Time-scale pause freezes bullet and approved animation"); Time.timeScale = 2;
            level.SetRunFlowPaused(true); frozen = shot.transform.position; yield return new WaitForSecondsRealtime(.2f);
            Check((Vector2)shot.transform.position == frozen, "Run-flow pause freezes encounter projectiles"); level.SetRunFlowPaused(false);
            MiniStageRuntimeState.EnterMiniStage(null); frozen = shot.transform.position; float pausedHp = boss.Health;
            yield return new WaitForSecondsRealtime(.2f); boss.TakeDamage(100);
            Check((Vector2)shot.transform.position == frozen && boss.Health == pausedHp, "Mini-room state suspends field projectiles and boss damage");
            MiniStageRuntimeState.ExitMiniStage(null);
            yield return WaitFor(() => !boss.Busy, 12, "Five-shot barrage completes");
            Check(boss.ProjectilesFired == 5, "Phase-one barrage fires exactly five shots"); boss.CancelPattern(); yield return null;
            Check(boss.Effects.childCount == 0, "Pattern cancellation releases all encounter effects");
            Check(!boss.PlayPattern(1), "Double fan is unavailable in phase one");

            for (int phase = 1; phase <= 2; phase++)
            {
                if (phase == 2)
                {
                    yield return PrimeTributeMeal(boss);
                    boss.TakeDamage(999999); Check(boss.Health == 17000 && boss.Transitioning && !boss.Dead, "Lethal phase-one hit reaches invulnerable midpoint transition");
                    Capture(boss, "phase-transition-hud", true, true);
                    float hp = boss.Health; boss.TakeDamage(100); Check(boss.Health == hp, "Transition rejects incoming damage");
                    yield return WaitFor(() => boss.PhaseTwo && !boss.Transitioning, 15, "Phase-two presentation releases invulnerability");
                    Check(boss.Health == 17000 && !Object.FindObjectsOfType<ConstipationKingMinion>().Any(m => m.IsTribute), "Phase transition cancels chewing with no delayed healing");
                    Check(!boss.PlayPattern(7), "Phase two cannot schedule the phase-one-only tribute technique");
                }
                for (int pattern = phase == 1 ? 2 : 0; pattern < ConstipationKingRuntime.PatternCount; pattern++)
                {
                    if (phase == 2 && pattern == 7) continue;
                    var center = map.ClampToFloor(map.UVToWorld(new Vector2(.76f, .72f)), 3);
                    boss.transform.position = center; boss.GetComponent<Rigidbody2D>().position = center; Physics2D.SyncTransforms();
                    ColonContentSmoke.Position(player, map.ClampToFloor((Vector2)boss.transform.position + Vector2.right * 7, .5f));
                    int before = boss.ProjectilesFired, completed = boss.CompletedPatterns;
                    Check(boss.PlayPattern(pattern), "Start phase " + phase + " pattern " + pattern);
                    if (pattern == 5) yield return CheckPaper(boss, phase);
                    if (pattern == 6) yield return CheckFlush(boss, level, phase);
                    if (pattern == 9) yield return CheckSummon(boss, level, phase);
                    if (pattern == 7)
                    {
                        yield return new WaitForSeconds(1);
                        var carriers = Object.FindObjectsOfType<ConstipationKingMinion>().Where(m => m.IsTribute && m.Health > 0).ToArray();
                        Check(carriers.Length == 3, "Healing carriers exist as three killable actors");
                        Check(NeedleTargeting.IsValidBossTarget(carriers[0], ~0), "Homing needles can acquire tribute carriers");
                        carriers[0].TakeDamage(10000); Check(carriers[0].Health == 0, "Carrier can be killed before healing"); carriers[2].TakeDamage(10000);
                        var delivery = carriers[1]; delivery.transform.position = boss.transform.position + Vector3.right;
                        delivery.GetComponent<Rigidbody2D>().position = delivery.transform.position;
                        float beforeDelivery = boss.Health;
                        yield return new WaitForSeconds(.35f); Check(boss.Health == beforeDelivery, "Carrier bow does not heal before food delivery");
                        Capture(boss, "phase-1-tribute-delivery");
                        yield return WaitFor(() => delivery != null && delivery.IsChewing, 8, "Offered food begins the eating pose");
                        Check(boss.Health == beforeDelivery, "Food arrival begins chewing without an immediate HP gain");
                        level.SetRunFlowPaused(true); yield return new WaitForSecondsRealtime(.3f);
                        Check(delivery != null && delivery.IsChewing && boss.Health == beforeDelivery, "Chewing and recovery stay frozen during run pause"); level.SetRunFlowPaused(false);
                        yield return new WaitForSeconds(.75f); Check(boss.Health > beforeDelivery, "HP rises only after active-time chewing finishes");
                    }
                    if (pattern == 8)
                    {
                        yield return new WaitForSeconds(.95f);
                        var crown = Object.FindObjectOfType<ConstipationKingShield>(); Check(crown != null, "Crown shield is a real collision actor");
                        var outward = ((Vector2)crown.transform.position - boss.HitCenter).normalized; int reflected = boss.ReflectedProjectiles;
                        var shotAtShield = level.EntityManager.SpawnProjectile(needlePool, (Vector2)crown.transform.position + outward * .9f, 30, 0, 14, 1 << boss.gameObject.layer);
                        shotAtShield.Launch(-outward);
                        yield return WaitFor(() => boss.ReflectedProjectiles > reflected, 5, "Crown intercepts and reflects actual pooled needle");
                    }
                    yield return ObservePattern(boss, phase, pattern);
                    Check(boss.CompletedPatterns == completed + 1, "Exactly one completion for phase " + phase + " pattern " + pattern);
                    if (pattern == 0) Check(boss.ProjectilesFired - before == 8, "Phase-two barrage fires exactly eight shots");
                    if (pattern == 1) Check(boss.ProjectilesFired - before == 14, "Phase-two fan fires two seven-shot volleys");
                    Check(map.FloorClearance(boss.transform.position) >= ConstipationKingRuntime.BodyRadius - .1f, "Pattern leaves king clear of walls");
                    boss.CancelPattern(); yield return null; Check(boss.Effects.childCount == 0, "Cancel cleans phase " + phase + " pattern " + pattern);
                    Check(!level.IsLevelEnded, "Pattern completion alone never clears Stage 2");
                    if (pattern == 7)
                    {
                        boss.TakeDamage(100); yield return PrimeTributeMeal(boss); float beforeCancel = boss.Health;
                        boss.CancelPattern(); yield return new WaitForSeconds(.8f);
                        Check(boss.Health == beforeCancel && boss.Effects.childCount == 0, "Pattern cancellation during chewing removes carriers and prevents late healing");
                    }
                }
            }
            var position = (Vector2)boss.transform.position;
            boss.MoveTo(position + Vector2.left * map.WorldRect.width * 2);
            Check(map.FloorClearance(boss.transform.position) >= ConstipationKingRuntime.BodyRadius - .1f, "Swept boss movement stops at external and internal walls");
            var status = boss.gameObject.AddComponent<FoodBossCombatStatus>(); status.Slow(2, .5f);
            Check(status.SpeedMultiplier == .5f, "Legacy needle slow applies to new final boss");
            yield return VerifyEndingReturn(level, boss, director, "full-combat-clear");
            SessionState.SetBool(Key + "Done", true);
        }
    }
}
#endif
