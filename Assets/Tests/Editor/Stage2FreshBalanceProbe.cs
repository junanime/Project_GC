#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    // An automated field opportunity/survival observation, never a human win-rate estimate.
    // Source gameplay, drop tables, damage, movement and physics settings are not modified.
    [InitializeOnLoad]
    public static class Stage2FreshBalanceProbe
    {
        const string Key = "Stage2FreshBalanceProbe";
        public const string Output = "Library/Stage2FreshBalanceProof";
        const string StopRequest = "Library/Stage2BalanceStop.request";
        const int DefaultSeed = 261113;
        const string Policy = "field-v1: nearest pickups within 3; chest/shop/capsule/orb within 14; cheapest affordable unowned shop item; first supply relic; active/passive/dash skill upgrade priority; augment ranked by OfferValue then shown order; random safe waypoint every 35 game seconds; skill every 6 and dash every 12; nearby enemy repulsion; correct trap arrow at most every 0.15 game seconds; no mini-stage or boss manual entry";
        static readonly string[] Characters = { "Main", "Ari", "Hyuki", "Shini" };
        static bool started;
        static double deadline;
        static string Mode => SessionState.GetString(Key + "Mode", "all");
        static bool Survival => SessionState.GetBool(Key + "Survival", false);
        static bool Smoke => SessionState.GetBool(Key + "Smoke", false);
        static bool Stopping => SessionState.GetBool(Key + "Stopping", false);
        static string RootFolder => SessionState.GetString(Key + "Root", Output);
        static string Folder => Smoke ? RootFolder + "/smoke" : Survival ? RootFolder + "/survival" : RootFolder;
        static int Seed => SessionState.GetInt(Key + "Seed", DefaultSeed);
        static string[] Cases => SessionState.GetString(Key + "Cases", "0,1,2,3,4,5,6,7").Split(',');
        static int Case => int.Parse(Cases[SessionState.GetInt(Key + "Round", 0)], CultureInfo.InvariantCulture);
        static int CharacterIndex => Case / 2;
        static int Stage => Case % 2 + 1;
        static float Duration => Smoke ? 12 : 885;
        static float Scale => SessionState.GetFloat(Key + "Scale", 3);

        [Serializable] sealed class Pref { public string key, text, kind; public bool had; public int number; public float real; }
        [Serializable] sealed class Backup
        {
            public Pref[] prefs;
            public string preferences, character, transfer;
            public string[] carry, selectedCarry;
            public float timeScale, fixedDeltaTime, maximumDeltaTime;
            public bool runInBackground;
        }
        [Serializable] sealed class Manifest
        {
            public string character, cohort, policy, selectionPolicy, seedTiming, outcome, selectedLoadout;
            public string[] merchantItemPaths;
            public int stage, requestedCheckpoint, seed, layoutSeed, initialLevel, level, kills, gold, items, chests, choices, purchases, supplies, frames, physicsSteps, cappedFrames;
            public float actualFieldSeconds, wallSeconds, uiPauseWallSeconds, targetTimeScale, fixedDeltaTime, maximumDeltaTime, maxObservedDeltaTime, health, maxHealth, damageTaken, healing, dartDamage, dartCooldown;
            public int dartProjectiles;
            public bool invulnerable, rankingExcluded, alive;
        }
        static Stage2FreshBalanceProbe() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run() => Begin("all", "0,1,2,3,4,5,6,7");
        public static void RunStageOne() => Begin("stage1", "0,2,4,6");
        public static void RunStageTwo() => Begin("stage2", "1,3,5,7");
        public static void RunOtherStageTwo() => Begin("other-stage2", "3,5,7");
        public static void RunMain() => Begin("Main", "0,1");
        public static void RunAri() => Begin("Ari", "2,3");
        public static void RunHyuki() => Begin("Hyuki", "4,5");
        public static void RunShini() => Begin("Shini", "6,7");
        public static void RunSmoke() => Begin("smoke", "0,1", false, true);
        public static void RunSmokeAll() => Begin("smoke-all", "0,1,2,3,4,5,6,7", false, true);
        public static void RunSurvival() => Begin("survival", "0,1,2,3,4,5,6,7", true);
        public static void RunSurvivalStageTwo() => Begin("survival-stage2", "1,3,5,7", true);
        public static void RunAt2() => Begin("all-x2", "0,1,2,3,4,5,6,7", false, false, 2);
        public static void RestoreSavedPreferences() { RestoreBackup(); }

        static void Begin(string mode, string cases, bool survival = false, bool smoke = false, float scale = 3)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start this probe outside play mode.");
            int seed = DefaultSeed;
            string scaleOption = Environment.GetEnvironmentVariable("GC_BALANCE_SCALE"), seedOption = Environment.GetEnvironmentVariable("GC_BALANCE_SEED");
            if (!string.IsNullOrEmpty(scaleOption) && (!float.TryParse(scaleOption, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) || float.IsNaN(scale) || float.IsInfinity(scale) || scale < 1 || scale > 3)) throw new ArgumentException("GC_BALANCE_SCALE must be 1..3");
            if (!string.IsNullOrEmpty(seedOption) && !int.TryParse(seedOption, out seed)) throw new ArgumentException("GC_BALANCE_SEED must be an integer");
            string root = Environment.GetEnvironmentVariable("GC_BALANCE_OUTPUT");
            if (string.IsNullOrWhiteSpace(root)) root = Output;
            if (seed != DefaultSeed || !Mathf.Approximately(scale, 3)) root += "/variants/seed-" + seed + "-x" + scale.ToString("0.##", CultureInfo.InvariantCulture);
            Directory.CreateDirectory(Output);
            RestoreBackup();
            SaveBackup();
            SessionState.SetInt(Key + "Seed", seed); SessionState.SetString(Key + "Root", root);
            SessionState.SetString(Key + "Mode", mode); SessionState.SetString(Key + "Cases", cases);
            SessionState.SetBool(Key + "Survival", survival); SessionState.SetBool(Key + "Smoke", smoke); SessionState.SetFloat(Key + "Scale", scale);
            SessionState.SetInt(Key + "Round", 0); SessionState.SetBool(Key + "Failed", false); SessionState.SetBool(Key + "Stopping", false); SessionState.SetBool(Key, true);
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/errors-" + Mode + ".txt", "");
            File.WriteAllText(Folder + "/result-" + Mode + ".txt", "RUNNING");
            File.WriteAllText(Folder + "/method.txt", "Fresh independent scene per character/stage. Stage2 enters from an untouched departure via the real StageGrowthReset.ResetForTransition path. No carried growth fixture. t0,450,885 snapshots; 885=14:45, not a complete 900-second field.\nInvulnerable cohort measures available growth, not survivability. Separate survival cohort stops at first death without revive injection/restart. Neither is human win rate.\nSeed applied before Level 1 scene load; stage2 reapplies the same seed immediately before field transfer/layout. Same seed does not pair different terrain/timelines or guarantee frame-identical RNG. Local System.Random controls route only. Actual layout seed is logged.\nTime scale=" + F(scale) + ", base seed=" + seed + ". fixed timestep and maximum timestep remain project values. Actual physics steps, wall time, pause time and capped frames are logged; no promised 3x throughput.\nNo direct damage, kill, EXP, item, currency or skill grants. Default per-character starting special needle, no equipped relic or carry items; all character unlocks temporarily enabled solely to expose each native starting weapon, paid needle unlocks cleared. This is an accessible minimum-loadout sample; preservation of chosen starting relics is covered separately by StageGrowthResetSmoke.\n" + Policy + "\nProfile backups restore all ProgressPrefs legacy keys, settings, coins, unlocks, tutorials and selected loadout. RunScore excludes ranking. RunSmoke uses separate smoke/ folder. Environment GC_BALANCE_SCALE=1..3, GC_BALANCE_SEED=int, GC_BALANCE_OUTPUT=optional root; nondefault scale/seed add variants/seed-N-xS to avoid replacing primary fixtures.\n");
            File.AppendAllText(Folder + "/method.txt", "Create Library/Stage2BalanceStop.request for a cooperative stop: partial observations use a separate stopped-tN stem, pending profile backup is restored, and result is STOPPED (exit0). The shared request remains until the operator removes it before the next run.\n");
            File.AppendAllText(Folder + "/method.txt", "Field diagnostics are sampled only with the 30-second rows: near10_alive counts active living monsters within 10 units of the player root; near10_visible also requires ColonAttackWall.Clear from player root to monster ground position. nearest_monster_distance, alive_mean_hp, alive_max_hp and supportcasters refer to all active living monsters (including blood clots/specials); distance=-1 if none.\n");
            Next();
        }
        static void SaveBackup()
        {
            var legacy = (IEnumerable<string>)typeof(ProgressPrefs).GetMethod("LegacyKeys", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            var keys = legacy.Concat(new[] { GamePreferences.SaveKey, "Apothecary.Volume", "Apothecary.ReducedMotion" }).Distinct();
            var backup = new Backup
            {
                prefs = keys.Select(ReadPreference).ToArray(),
                preferences = JsonUtility.ToJson(GamePreferences.Current), character = AssetDatabase.GetAssetPath(CrossSceneData.CharacterBlueprint),
                transfer = CrossSceneData.PendingRunSceneTransfer == null ? "" : JsonUtility.ToJson(CrossSceneData.PendingRunSceneTransfer),
                carry = (CrossSceneData.StartingLobbyItems ?? Array.Empty<MerchantItemBlueprint>()).Select(AssetDatabase.GetAssetPath).ToArray(),
                selectedCarry = LobbyLoadoutData.SelectedCarryItems.Select(AssetDatabase.GetAssetPath).ToArray(),
                timeScale = Time.timeScale, fixedDeltaTime = Time.fixedDeltaTime, maximumDeltaTime = Time.maximumDeltaTime, runInBackground = Application.runInBackground
            };
            File.WriteAllText(Output + "/profile-backup.json", JsonUtility.ToJson(backup, true));
        }
        static Pref ReadPreference(string key)
        {
            var p = new Pref { key = key, had = PlayerPrefs.HasKey(key), kind = key == "EquippedRelicId" || key == GamePreferences.SaveKey ? "string" : key == "Apothecary.Volume" ? "float" : "int" };
            if (p.kind == "string") p.text = PlayerPrefs.GetString(key);
            else if (p.kind == "float") p.real = PlayerPrefs.GetFloat(key);
            else p.number = PlayerPrefs.GetInt(key);
            return p;
        }
        static void RestoreBackup()
        {
            string path = Output + "/profile-backup.json";
            if (!File.Exists(path)) return;
            var b = JsonUtility.FromJson<Backup>(File.ReadAllText(path));
            foreach (var p in b.prefs)
            {
                if (!p.had) PlayerPrefs.DeleteKey(p.key);
                else if (p.kind == "string") PlayerPrefs.SetString(p.key, p.text);
                else if (p.kind == "float") PlayerPrefs.SetFloat(p.key, p.real);
                else PlayerPrefs.SetInt(p.key, p.number);
            }
            PlayerPrefs.Save();
            GamePreferences.Apply(JsonUtility.FromJson<GamePreferenceValues>(b.preferences), false, false);
            Time.timeScale = b.timeScale; Time.fixedDeltaTime = b.fixedDeltaTime; Time.maximumDeltaTime = b.maximumDeltaTime; Application.runInBackground = b.runInBackground;
            CrossSceneData.CharacterBlueprint = string.IsNullOrEmpty(b.character) ? null : AssetDatabase.LoadAssetAtPath<CharacterBlueprint>(b.character);
            CrossSceneData.StartingLobbyItems = (b.carry ?? Array.Empty<string>()).Select(AssetDatabase.LoadAssetAtPath<MerchantItemBlueprint>).Where(x => x != null).ToArray();
            CrossSceneData.PendingRunSceneTransfer = string.IsNullOrEmpty(b.transfer) ? null : JsonUtility.FromJson<RunSceneTransferSnapshot>(b.transfer);
            var selected = (List<MerchantItemBlueprint>)typeof(LobbyLoadoutData).GetField("selectedCarryItems", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            selected.Clear(); selected.AddRange((b.selectedCarry ?? Array.Empty<string>()).Select(AssetDatabase.LoadAssetAtPath<MerchantItemBlueprint>).Where(x => x != null));
            File.Copy(path, Output + "/profile-restored.json", true); File.Delete(path);
        }
        static void NormalizeProfile()
        {
            var b = JsonUtility.FromJson<Backup>(File.ReadAllText(Output + "/profile-backup.json"));
            foreach (var p in b.prefs)
            {
                if (p.key.StartsWith("LobbyUnlock.Character.", StringComparison.Ordinal)) PlayerPrefs.SetInt(p.key, 1);
                else if (p.key.StartsWith("LobbyUnlock.Needle.", StringComparison.Ordinal) || p.key.StartsWith("October.StartingNeedle.", StringComparison.Ordinal) || p.key.StartsWith("October.NeedleEnabled.", StringComparison.Ordinal)) PlayerPrefs.DeleteKey(p.key);
                else if (p.key.StartsWith("Tutorial", StringComparison.Ordinal) || p.key == CapsuleDoctorDialogue.SeenKey) PlayerPrefs.SetInt(p.key, 1);
            }
            // Use the actual tutorial catalog keys, whose prefix may evolve.
            var manifest = Resources.Load<TextAsset>("TutorialClips/catalog");
            var entries = JsonUtility.FromJson<TutorialCatalog>(manifest.text).entries;
            foreach (var e in entries) PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id), 1);
            PlayerPrefs.SetInt("Coins", 0); PlayerPrefs.SetInt("LobbySilverCoins", 0); PlayerPrefs.DeleteKey("EquippedRelicId");
            PlayerPrefs.Save();
            CrossSceneData.ClearStartingLobbyItems(); CrossSceneData.ClearPendingRunSceneTransfer(); LobbyLoadoutData.Clear();
        }
        [Serializable] sealed class TutorialCatalog { public TutorialEntry[] entries; }
        [Serializable] sealed class TutorialEntry { public string id; }
        static void Next()
        {
            NormalizeProfile(); SessionState.SetBool(Key + "RoundDone", false); SessionState.SetBool(Key + "NeedsScene", true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started = false; deadline = EditorApplication.timeSinceStartup + (Smoke ? 300 : 1200);
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            File.AppendAllText(Folder + "/errors-" + Mode + ".txt", message + "\n" + stack + "\n"); SessionState.SetBool(Key + "Failed", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (File.Exists(StopRequest)) SessionState.SetBool(Key + "Stopping", true);
            if (Stopping && !started) SessionState.SetBool(Key + "RoundDone", true);
            if (EditorApplication.timeSinceStartup > deadline) { File.AppendAllText(Folder + "/errors-" + Mode + ".txt", "Round wall-time deadline\n"); SessionState.SetBool(Key + "Failed", true); }
            if (SessionState.GetBool(Key + "RoundDone", false) || SessionState.GetBool(Key + "Failed", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                bool failed = SessionState.GetBool(Key + "Failed", false); int next = SessionState.GetInt(Key + "Round", 0) + 1;
                if (!failed && !Stopping && next < Cases.Length) { SessionState.SetInt(Key + "Round", next); Next(); return; }
                RestoreBackup(); SessionState.SetBool(Key, false); Application.logMessageReceived -= Log; EditorApplication.update -= Tick;
                File.WriteAllText(Folder + "/result-" + Mode + ".txt", failed ? "FAIL: see errors; profile restored" : Stopping ? "STOPPED: requested observation stop; completed/partial snapshots retained; profile restored" : "PASS: automated observation completed; profile restored; no human win-rate claim");
                EditorApplication.Exit(failed ? 1 : 0); return;
            }
            if (!EditorApplication.isPlaying) return;
            if (SessionState.GetBool(Key + "NeedsScene", false))
            {
                SessionState.SetBool(Key + "NeedsScene", false); NormalizeProfile();
                CrossSceneData.CharacterBlueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/" + Characters[CharacterIndex] + " Character Blueprint.asset");
                UnityEngine.Random.InitState(Seed + CharacterIndex * 997); Time.timeScale = 0;
                SceneManager.LoadScene("Level 1"); return;
            }
            var guide = TutorialGuide.Instance; if (guide != null) { guide.Close(); guide.enabled = false; }
            var level = Object.FindObjectOfType<LevelManager>();
            if (RunScore.Current != null) RunScore.Exclude("Fresh field automated observation, no ranking");
            if (started || level == null || !level.InitialFieldObjectsReady || level.PlayerCharacter == null) return;
            started = true; var host = new GameObject("Fresh field balance observation"); Object.DontDestroyOnLoad(host);
            var meter = host.AddComponent<Stage2FreshBalanceMeter>(); meter.StartCoroutine(Observe(level, meter));
        }
        static float OfferValue(Ability ability)
        {
            if (!(ability is Ver4AugmentOffer o)) return 1;
            if (o.Kind == Ver4RewardKind.LegendaryAbility) return 12;
            if (o.Kind == Ver4RewardKind.NewSpecial) return o.Parent.Type == SyringeSpecialAugmentAbility.SpecialAugmentType.Pierce ? 12 : 7;
            if (o.Kind == Ver4RewardKind.Original) return 6;
            return (o.Option == 0 ? 8 : o.Option == 3 ? 7 : o.Option == 8 ? 6 : o.Option == 1 ? 4 : 2) + (int)o.Grade * .2f;
        }
        static object Field(object owner, string name) => ColonContentSmoke.Field(owner, name).GetValue(owner);
        static void Invoke(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        static string Prefix => Characters[CharacterIndex] + "-stage" + Stage;
        static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        static IEnumerator Observe(LevelManager level, Stage2FreshBalanceMeter meter)
        {
            var prefs = GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss = false; prefs.muteOnFocusLoss = false; prefs.vSync = false; prefs.frameLimit = 60;
            GamePreferences.Apply(prefs, false, false); Application.runInBackground = true;
            var actor = level.PlayerCharacter; actor.CollectableCollider.enabled = false; level.SetRunFlowPaused(true); Time.timeScale = 0;
            var launch = actor.CaptureRunSceneState();
            string launchAbilities = AbilitySignature(Object.FindObjectOfType<AbilityManager>());
            if (launch.CurrentLevel != 1 || launch.CurrentExp != 0 || actor.CurrentHealth != actor.MaxHealth) throw new Exception("Fresh departure is already grown or damaged");
            if (Stage == 2)
            {
                UnityEngine.Random.InitState(Seed + CharacterIndex * 997);
                // Use the actual inlet placement, suspended field, reset and ejection sequence.
                // No departure loading argument means this is the real field-transition reset.
                Time.timeScale = 1;
                var flow = StageProgression.Ensure(level);
                if (!flow.BeginDirectColon()) throw new Exception("Fresh Stage2 field transfer did not start");
                float entryDeadline = Time.realtimeSinceStartup + 90;
                while (flow.Travelling && Time.realtimeSinceStartup < entryDeadline && !Stopping) yield return null;
                if (Stopping) { level.SetRunFlowPaused(true); Time.timeScale = 0; SessionState.SetBool(Key + "RoundDone", true); yield break; }
                if (flow.Travelling || flow.StageNumber != 2) throw new Exception("Fresh Stage2 field transfer did not finish");
                level.SetRunFlowPaused(true); Time.timeScale = 0;
                if (level.GetComponent<StageGrowthReset>().ResetCount != 1) throw new Exception("Stage2 must use real launch growth reset exactly once");
            }
            var abilities = Object.FindObjectOfType<AbilityManager>(); var stats = Object.FindObjectOfType<StatsManager>(); var dart = Object.FindObjectOfType<SyringeDartAbility>();
            var fresh = actor.CaptureRunSceneState();
            if (fresh.CurrentLevel != launch.CurrentLevel || fresh.CurrentExp != launch.CurrentExp || fresh.SkillActiveLevel != launch.SkillActiveLevel || fresh.SkillPassiveLevel != launch.SkillPassiveLevel || fresh.SkillDashRewardUpgrades != 0 || OctoberItemRuntime.Get(actor).State.owned.Count != 0 || stats.CoinsGained != 0 || CrossSceneData.StartingLobbyItems.Length != 0 || !string.IsNullOrEmpty(ProgressPrefs.GetString("EquippedRelicId"))) throw new Exception("Fresh baseline leaked carried growth/loadout");
            if (fresh.CurrentHealth != actor.MaxHealth || fresh.DamageMultiplier != launch.DamageMultiplier || fresh.AttackSpeedMultiplier != launch.AttackSpeedMultiplier || fresh.AdditionalProjectiles != launch.AdditionalProjectiles || AbilitySignature(abilities) != launchAbilities) throw new Exception("Fresh baseline HP, combat stats or starting abilities differ from departure");
            ColonContentSmoke.Set(actor, "isInvincible", !Survival);
            if (actor.Blueprint != CrossSceneData.CharacterBlueprint) throw new Exception("Selected character mismatch");
            string loadout = string.Join(";", abilities.CaptureRunSceneAbilities().Select(a => a.AbilityObjectName + ":" + a.Level));
            File.WriteAllText(Folder + "/" + Prefix + "-choices.txt", "Policy: " + Policy + "\nStarting loadout: " + loadout + "\n");
            File.WriteAllText(Folder + "/" + Prefix + "-samples.csv", "character,stage,cohort,field_seconds,wall_seconds,ui_pause_wall,level,health,max_health,damage_taken,healing,damage,cooldown,projectiles,kills,gold,items,chests,choices,purchases,supplies,population,frames,physics_steps,capped_frames,managed_mb,pos_x,pos_y,active_projectiles,near10_alive,near10_visible,nearest_monster_distance,alive_mean_hp,alive_max_hp,supportcasters\n");
            int chests = 0, choices = 0, purchases = 0; Action<Chest> onChest = c => chests++; Chest.OnAnyChestOpened += onChest;
            float began = Time.realtimeSinceStartup, nextSample = 0, nextSkill = 0, nextDash = 0, nextSearch = 0, nextTarget = 0, nextTrap = 0;
            float nextDrive = 0, lastDistance = float.MaxValue, stuckSince = 0;
            var rng = new System.Random(Seed + CharacterIndex * 997); var map = FiniteStomachMap.Instance;
            Vector2 waypoint = actor.transform.position; Chest targetChest = null; MerchantNPC targetShop = null; SupplyCapsule targetCapsule = null; SupplyRelicOrb targetOrb = null;
            var visited = new Dictionary<MerchantNPC, int>(); var checkpoints = Smoke ? new[] { 0, 12 } : new[] { 0, 450, 885 }; int checkpoint = 0;
            meter.ResetCounters(); meter.Counting = true;
            try
            {
                SaveSnapshot(level, meter, 0, began, chests, choices, purchases, loadout, "baseline"); checkpoint++;
                actor.CollectableCollider.enabled = true; level.SetRunFlowPaused(false); Time.timeScale = Scale;
                while (actor != null && actor.IsAlive && !level.IsLevelEnded && level.CurrentLevelTime < Duration && !Stopping)
                {
                    float now = level.CurrentLevelTime;
                    if (checkpoint < checkpoints.Length && now >= checkpoints[checkpoint]) SaveSnapshot(level, meter, checkpoints[checkpoint++], began, chests, choices, purchases, loadout, "checkpoint");
                    var dialog = level.EntityManager.AbilitySelectionDialog; var ui = ApothecaryUI.Instance;
                    bool paused = dialog.MenuOpen || ui != null && ui.Page != "hud" || Time.timeScale <= 0 || level.IsRunFlowPaused || StageEntryLoading.IsLoading;
                    meter.UiPaused = paused;
                    if (dialog.MenuOpen)
                    {
                        actor.Move(Vector2.zero);
                        if (!dialog.SelectionAnimating)
                        {
                            var offers = Field(dialog, "displayedAbilities") as List<Ability>;
                            var selected = offers?.Where(a => a != null).OrderByDescending(OfferValue).FirstOrDefault();
                            if (selected != null) { File.AppendAllText(Folder + "/" + Prefix + "-choices.txt", F(now) + " augment=" + selected.name + " score=" + F(OfferValue(selected)) + "\n"); selected.Select(); choices++; dialog.Close(); }
                        }
                        yield return null; continue;
                    }
                    if (ui != null && ui.Page != "hud")
                    {
                        actor.Move(Vector2.zero);
                        if (ui.Page == "supplyRelic")
                        {
                            var relics = Field(ui, "supplyChoices") as RelicBlueprint[]; var accept = Field(ui, "supplyAccept") as Func<RelicBlueprint, bool>;
                            bool accepted = relics != null && relics.Length > 0 && accept != null && accept(relics[0]); Invoke(ui, "CloseSupplyRelics", accepted);
                        }
                        else if (ui.Page == "skillReward")
                        {
                            if (actor.Skills.CanUpgrade(true)) Invoke(ui, "FinishSkillReward", (object)(bool?)true);
                            else if (actor.Skills.CanUpgrade(false)) Invoke(ui, "FinishSkillReward", (object)(bool?)false);
                            else if (actor.Skills.CanUpgradeDash) Invoke(ui, "FinishDashReward");
                            else Invoke(ui, "FinishSkillReward", new object[] { null });
                        }
                        else if (ui.Page == "merchant" || ui.Page == "bossChoice") ui.Back();
                        else if (ui.Page == "result") break;
                        else ui.Show("hud");
                        yield return null; continue;
                    }
                    if (level.IsRunFlowPaused || StageEntryLoading.IsLoading || Time.timeScale <= 0) { yield return null; continue; }
                    Time.timeScale = Scale;
                    if (now >= nextSample)
                    {
                        nextSample = now + 30; Sample(level, meter, began, chests, choices, purchases);
                        File.WriteAllText(Folder + "/status.txt", Prefix + " " + (Survival ? "survival" : "invulnerable-growth") + " field=" + F(now) + " wall=" + F(Time.realtimeSinceStartup - began));
                    }
                    if (now < nextDrive) { yield return null; continue; } nextDrive = now + .1f;
                    if (actor.IsTrapBound && now >= nextTrap)
                    {
                        nextTrap = now + .15f;
                        var trap = Object.FindObjectsOfType<TrapMonster>().FirstOrDefault(t => t.IsActive && t.EscapeTarget >= 0);
                        if (trap != null) { var method = typeof(TrapMonster).GetMethod("HandleArrowInput", BindingFlags.Instance | BindingFlags.NonPublic); method.Invoke(trap, new[] { Enum.ToObject(method.GetParameters()[0].ParameterType, trap.EscapeTarget) }); }
                    }
                    Vector2 position = actor.transform.position;
                    if (now >= nextSearch)
                    {
                        nextSearch = now + .7f;
                        targetChest = Object.FindObjectsOfType<Chest>().Where(c => c.CanInteract && Vector2.Distance(c.transform.position, position) < 14).OrderBy(c => Vector2.Distance(c.transform.position, position)).FirstOrDefault();
                        targetShop = Object.FindObjectsOfType<MerchantNPC>().Where(n => n.CanInteract && (!visited.TryGetValue(n, out int gold) || stats.CoinsGained >= gold + 25) && Vector2.Distance(n.transform.position, position) < 14).OrderBy(n => Vector2.Distance(n.transform.position, position)).FirstOrDefault();
                        targetCapsule = Object.FindObjectsOfType<SupplyCapsule>().Where(c => c.Landed && !c.Opened && Vector2.Distance(c.transform.position, position) < 14).OrderBy(c => Vector2.Distance(c.transform.position, position)).FirstOrDefault();
                        targetOrb = Object.FindObjectsOfType<SupplyRelicOrb>().Where(o => !o.Collected && Vector2.Distance(o.transform.position, position) < 14).OrderBy(o => Vector2.Distance(o.transform.position, position)).FirstOrDefault();
                    }
                    var targets = new Component[] { targetChest, targetShop, targetCapsule, targetOrb }.Where(o => o != null).OrderBy(o => Vector2.Distance(position, o.transform.position)).ToArray();
                    if (targets.Length > 0) waypoint = targets[0].transform.position;
                    else if (Vector2.Distance(position, waypoint) < 2 || now >= nextTarget)
                    {
                        for (int i = 0; i < 200; i++) { var r = map.WorldRect; waypoint = new Vector2(Mathf.Lerp(r.xMin, r.xMax, (float)rng.NextDouble()), Mathf.Lerp(r.yMin, r.yMax, (float)rng.NextDouble())); if (map.NavigationDistance(waypoint, out _) >= 12) break; }
                        nextTarget = now + 35;
                    }
                    if (targetChest != null && Vector2.Distance(position, targetChest.transform.position) < 1.25f) { targetChest.OpenChest(); targetChest = null; }
                    if (targetCapsule != null && Vector2.Distance(position, targetCapsule.transform.position) < 1.8f) targetCapsule.TryInteract();
                    if (targetOrb != null && Vector2.Distance(position, targetOrb.transform.position) < .55f) targetOrb.TryCollect();
                    if (targetShop != null && Vector2.Distance(position, targetShop.transform.position) < 1.8f && targetShop.TryOpenShop())
                    {
                        actor.Move(Vector2.zero); visited[targetShop] = stats.CoinsGained;
                        var item = targetShop.GetShopItems().Where(i => !OctoberItemRuntime.Get(actor).Has(i.octoberId) && RelicRuntime.Price(actor, i.cost) <= stats.CoinsGained).OrderBy(i => i.cost).FirstOrDefault();
                        if (item != null) { MerchantUIManager.Instance.OnClickPurchaseItem(item, null); purchases++; }
                        else MerchantUIManager.Instance.CloseShop(); targetShop = null; yield return null; continue;
                    }
                    var pickup = level.EntityManager.MagneticCollectables.Where(c => c != null && !c.IsBeingCollected && Vector2.Distance(c.transform.position, position) < 3).OrderBy(c => Vector2.Distance(c.transform.position, position)).FirstOrDefault();
                    if (pickup != null) waypoint = pickup.transform.position;
                    float distance = Vector2.Distance(position, waypoint); if (distance < lastDistance - .05f) stuckSince = now; lastDistance = distance;
                    if (now - stuckSince > 8) { nextTarget = 0; targetChest = null; targetShop = null; targetCapsule = null; targetOrb = null; waypoint = position; stuckSince = now; }
                    Vector2 direction = ColonNavigation.Toward(position, waypoint), avoid = Vector2.zero;
                    foreach (var enemy in level.EntityManager.LivingMonsters) if (enemy != null) { Vector2 delta = position - (Vector2)enemy.transform.position; if (delta.sqrMagnitude > .01f && delta.sqrMagnitude < 3.24f) avoid += delta.normalized * (1 - delta.magnitude / 1.8f); }
                    actor.Move((direction + Vector2.ClampMagnitude(avoid, 1) * .65f).normalized);
                    if (now >= nextSkill) { actor.Skills.TryActivate(); nextSkill = now + 6; }
                    if (now >= nextDash) { actor.TryDash(); nextDash = now + 12; }
                    yield return null;
                }
                bool alive = actor != null && actor.IsAlive;
                meter.UiPaused = false; meter.Counting = false;
                if (!Survival && !alive) throw new Exception("Invulnerable opportunity cohort died unexpectedly");
                if (alive && level.CurrentLevelTime < Duration && !Stopping) throw new Exception("Field ended before observation limit without a measured death");
                int final = alive && !Stopping ? (int)Duration : Mathf.FloorToInt(level.CurrentLevelTime);
                if (actor != null) { actor.Move(Vector2.zero); SaveSnapshot(level, meter, final, began, chests, choices, purchases, loadout, Stopping ? "stopped-by-request" : alive ? "censored-at-" + Duration : "first-death", Stopping); Sample(level, meter, began, chests, choices, purchases); }
                level.SetRunFlowPaused(true); Time.timeScale = 0;
                File.WriteAllText(Folder + "/" + Prefix + "-result.txt", Stopping ? "STOPPED at " + F(level.CurrentLevelTime) + " field seconds" : alive ? "CENSORED_ALIVE at " + F(level.CurrentLevelTime) + " field seconds" : "FIRST_DEATH at " + F(level.CurrentLevelTime) + " field seconds; no restart");
                SessionState.SetBool(Key + "RoundDone", true);
            }
            finally { Chest.OnAnyChestOpened -= onChest; meter.Counting = false; if (actor != null) actor.Move(Vector2.zero); }
        }
        static void SaveSnapshot(LevelManager level, Stage2FreshBalanceMeter meter, int checkpoint, float began, int chests, int choices, int purchases, string loadout, string outcome, bool stopped = false)
        {
            var actor = level.PlayerCharacter; var stats = Object.FindObjectOfType<StatsManager>(); var dart = Object.FindObjectOfType<SyringeDartAbility>();
            string stem = Folder + "/" + Prefix + (stopped ? "-stopped" : "") + "-t" + checkpoint;
            var snapshot = new RunSceneTransferSnapshot { CharacterState = actor.CaptureRunSceneState(), AbilityStates = Object.FindObjectOfType<AbilityManager>().CaptureRunSceneAbilities(), OctoberItems = OctoberItemRuntime.Get(actor).State, MerchantItems = SynergyManager.Instance?.CaptureOwnership(), CoinsGained = stats.CoinsGained };
            File.WriteAllText(stem + "-build.json", JsonUtility.ToJson(snapshot, true));
            File.WriteAllText(stem + "-general.json", JsonUtility.ToJson(PlayerGeneralStatRuntime.GetOrCreate(actor), true));
            var manifest = new Manifest
            {
                character = Characters[CharacterIndex], stage = Stage, requestedCheckpoint = checkpoint, actualFieldSeconds = level.CurrentLevelTime, cohort = Survival ? "noninvulnerable-survival" : "invulnerable-growth-opportunity",
                merchantItemPaths = snapshot.MerchantItems?.items.Where(i => i != null).Select(AssetDatabase.GetAssetPath).ToArray() ?? Array.Empty<string>(),
                policy = Policy, selectionPolicy = "Legendary/Pierce=12, other special=7, Original=6, general option0=8/3=7/8=6/1=4/other=2 + grade*.2; ties shown order; active then passive then dash",
                seedTiming = "UnityEngine.Random.InitState before Level 1 load; same seed reapplied before Stage2 transfer; no later gameplay reseed; movement uses independent System.Random; terrain/timeline not paired",
                seed = Seed + CharacterIndex * 997, layoutSeed = level.GetComponent<FieldObjectLayout>().Seed, selectedLoadout = loadout, initialLevel = 1, level = actor.CurrentLevel, invulnerable = !Survival,
                wallSeconds = Time.realtimeSinceStartup - began, uiPauseWallSeconds = meter.UiPauseWall, targetTimeScale = Scale, fixedDeltaTime = Time.fixedDeltaTime, maximumDeltaTime = Time.maximumDeltaTime,
                frames = meter.Frames, physicsSteps = meter.PhysicsSteps, cappedFrames = meter.CappedFrames, maxObservedDeltaTime = meter.MaxDelta,
                health = actor.CurrentHealth, maxHealth = actor.MaxHealth, damageTaken = stats.DamageTaken, healing = stats.HealingReceived, dartDamage = dart.GetEffectiveDamage(), dartCooldown = dart.GetEffectiveCooldown(), dartProjectiles = dart.GetEffectiveProjectileCount(),
                kills = stats.MonstersKilled, gold = stats.CoinsGained, items = OctoberItemRuntime.Get(actor).State.owned.Count, chests = chests, choices = choices, purchases = purchases, supplies = stats.SupplyCapsulesCollected,
                outcome = outcome, alive = actor.IsAlive, rankingExcluded = RunScore.Current != null && !string.IsNullOrEmpty(RunScore.Current.Exclusion)
            };
            if (!manifest.rankingExcluded) throw new Exception("Observation must be excluded from ranking");
            File.WriteAllText(stem + "-manifest.json", JsonUtility.ToJson(manifest, true));
        }
        static string AbilitySignature(AbilityManager manager) => string.Join(";", manager.CaptureRunSceneAbilities().Select(a => a.AbilityTypeName + "/" + a.AbilityObjectName + "/" + a.Level + "/" + string.Join(",", a.ConditionalAugmentIds)).OrderBy(a => a, StringComparer.Ordinal));
        static void Sample(LevelManager level, Stage2FreshBalanceMeter meter, float began, int chests, int choices, int purchases)
        {
            var a = level.PlayerCharacter; var s = Object.FindObjectOfType<StatsManager>(); var d = Object.FindObjectOfType<SyringeDartAbility>();
            int alive = 0, near = 0, visible = 0, supports = 0; float nearest = float.PositiveInfinity, hpSum = 0, hpMax = 0; Vector2 position = a.transform.position;
            foreach (var monster in level.EntityManager.LivingMonsters)
            {
                if (monster == null || !monster.gameObject.activeInHierarchy || !monster.IsAlive || monster.HP <= 0) continue;
                alive++; hpSum += monster.HP; hpMax = Mathf.Max(hpMax, monster.HP); if (monster is MonsterSupportCaster) supports++;
                float distance = Vector2.Distance(position, monster.Position); nearest = Mathf.Min(nearest, distance);
                if (distance <= 10) { near++; if (ColonAttackWall.Clear(position, monster.Position)) visible++; }
            }
            File.AppendAllText(Folder + "/" + Prefix + "-samples.csv", string.Join(",", new object[] { Characters[CharacterIndex], Stage, Survival ? "survival" : "invulnerable-growth", F(level.CurrentLevelTime), F(Time.realtimeSinceStartup - began), F(meter.UiPauseWall), a.CurrentLevel, F(a.CurrentHealth), F(a.MaxHealth), F(s.DamageTaken), F(s.HealingReceived), F(d.GetEffectiveDamage()), F(d.GetEffectiveCooldown()), d.GetEffectiveProjectileCount(), s.MonstersKilled, s.CoinsGained, OctoberItemRuntime.Get(a).State.owned.Count, chests, choices, purchases, s.SupplyCapsulesCollected, level.EntityManager.LivingMonsters.Count, meter.Frames, meter.PhysicsSteps, meter.CappedFrames, UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576, F(a.transform.position.x), F(a.transform.position.y), Object.FindObjectsOfType<Projectile>().Count(p => p.isActiveAndEnabled), near, visible, F(alive > 0 ? nearest : -1), F(alive > 0 ? hpSum / alive : 0), F(hpMax), supports }) + "\n");
        }
    }
    public sealed class Stage2FreshBalanceMeter : MonoBehaviour
    {
        public bool Counting, UiPaused;
        public int Frames, PhysicsSteps, CappedFrames;
        public float UiPauseWall, MaxDelta;
        public void ResetCounters() { Frames = PhysicsSteps = CappedFrames = 0; UiPauseWall = MaxDelta = 0; }
        void Update() { if (!Counting) return; Frames++; MaxDelta = Mathf.Max(MaxDelta, Time.deltaTime); if (UiPaused) UiPauseWall += Time.unscaledDeltaTime; if (Time.deltaTime >= Time.maximumDeltaTime * .99f) CappedFrames++; }
        void FixedUpdate() { if (Counting) PhysicsSteps++; }
    }
}
#endif
