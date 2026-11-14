#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Engine measurements using freshly observed builds. Never a human success-rate model.
    [InitializeOnLoad]
    public static class Stage2CombatBalanceProbe
    {
        const string Key = "Stage2CombatBalanceProbe";
        const string DefaultFixtures = "Library/Stage2FreshBalanceProof";
        static string Fixtures => SessionState.GetString(Key + "Fixtures", DefaultFixtures);
        const string Output = "Library/Stage2CombatBalanceProof";
        const string StopRequest = "Library/Stage2BalanceStop.request";
        const string ProfileBackup = Output + "/profile-backup.json";
        const float Speed = 1f;
        static readonly FieldInfo NeedleDirection = typeof(SyringeDartAbility).GetField("lastNeedleDirection", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly string[] Characters = { "Main", "Ari", "Hyuki", "Shini" };
        static readonly int[] Checkpoints = { 0, 450, 885 };
        [Serializable] sealed class FixtureManifest
        {
            public string character; public int stage, requestedCheckpoint;
            public float dartDamage, dartCooldown, maxHealth; public int dartProjectiles;
            public string[] merchantItemPaths;
            public int seed, layoutSeed; public float targetTimeScale, actualFieldSeconds;
            public string cohort, outcome;
        }
        [Serializable] sealed class SavedPref { public string key, text, kind; public bool had; public int value; public float real; }
        [Serializable] sealed class SavedProfile
        {
            public SavedPref[] prefs; public string preferences, character, transfer;
            public string[] carry, selectedCarry;
            public float timeScale; public bool runInBackground;
        }
        static bool started;
        static double deadline;
        static string Mode => SessionState.GetString(Key + "Mode", "calibration");
        static string DirectoryPath => Output + "/" + SessionState.GetString(Key + "OutputName", Mode);
        static Stage2CombatBalanceProbe() { if (SessionState.GetBool(Key, false)) Attach(); }

        public static void RunCalibration() => Begin("calibration", 12);
        public static void RunEncounters() => Begin("encounters", 48);
        public static void RunPatterns() => Begin("patterns", 8);
        public static void RunFinalCalibration() => Begin("calibration", 4, new[] { 2, 5, 8, 11 }, "final-calibration",
            "Priority subset: four characters at t885, each against all three stationary targets; 12 measurement windows.");
        public static void RunFinalEncounters() => Begin("encounters", 16, Enumerable.Range(0, 4).SelectMany(c => Enumerable.Range(c * 12 + 8, 4)).ToArray(), "final-encounters",
            "Priority subset: four characters at t885, optional boss and final king, both movement policies; 16 independent encounters.");
        public static void RunMidOptional() => Begin("encounters", 8, Enumerable.Range(0, 4).SelectMany(c => new[] { c * 12 + 4, c * 12 + 5 }).ToArray(), "mid-optional",
            "Priority subset: four characters at t450, optional boss only, both movement policies; eight independent encounters. No premature king encounter.");
        // A bounded early integration check uses the newest available fresh Main fixture.
        public static void RunPilot()
        {
            string inputRoot = FixtureRootFromEnvironment();
            int checkpoint = Checkpoints.Reverse().FirstOrDefault(t => File.Exists(inputRoot + "/Main-stage2-t" + t + "-build.json"));
            SessionState.SetInt(Key + "PilotCheckpoint", checkpoint); Begin("pilot", 1);
        }
        static string FixtureRootFromEnvironment()
        {
            string path = Environment.GetEnvironmentVariable("GC_COMBAT_FIXTURES");
            return string.IsNullOrWhiteSpace(path) ? DefaultFixtures : path.TrimEnd('/', '\\');
        }

        static void Begin(string mode, int rounds, int[] sourceRounds = null, string outputName = null, string subset = null)
        {
            Directory.CreateDirectory(Output); RestoreProfile(); SaveProfile();
            SessionState.SetString(Key + "Fixtures", FixtureRootFromEnvironment());
            SessionState.SetString(Key + "Mode", mode);
            SessionState.SetString(Key + "OutputName", outputName ?? mode);
            if (sourceRounds == null) sourceRounds = Enumerable.Range(0, rounds).ToArray();
            if (sourceRounds.Length != rounds) throw new ArgumentException("Subset round count mismatch");
            for (int i = 0; i < rounds; i++) SessionState.SetInt(Key + "SourceRound." + i, sourceRounds[i]);
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(DirectoryPath + "/errors.txt", "");
            File.WriteAllText(DirectoryPath + "/censoring.txt", "");
            File.WriteAllText(DirectoryPath + "/result.txt", "RUNNING");
            File.WriteAllText(DirectoryPath + "/results.csv", "character,checkpoint,target,phase,pattern,policy,seed,seconds,wall_seconds,frames,max_frame_game_seconds,capped_frames,hp_start,hp_end,enemy_hp_start,enemy_hp_end,observed_damage,observed_healing_lower_bound,net_dps,player_damage_counter_including_overkill,player_healing,damaged_frames,skill_uses,dashes,reflections,completed_patterns,blocked_decisions,decisions,minimum_wall_clearance,initial_floor_clearance,initial_shot_center_distance,initial_shot_surface_distance,needle_reach,in_range_fraction,blocked_fraction,contact_frames,minimum_hurtbox_distance,balance_valid,outcome,ttk_measured_seconds,naive_hp_over_dps_seconds\n");
            File.WriteAllText(DirectoryPath + "/damage-events.csv", "character,target,policy,seconds,damage_counter_delta,player_hp,hurtbox_distance,contact,king_actors,optional_actors,food_actors,living_monsters,player_x,player_y,boss_x,boss_y\n");
            File.WriteAllText(DirectoryPath + "/hurtboxes.csv", "character,target,collider,type,offset_x,offset_y,world_size_x,world_size_y,world_center_x,world_center_y,scale_x,scale_y,rotation_z,axis\n");
            File.WriteAllText(DirectoryPath + "/arena.csv", "target,actual_director_spawn_x,actual_director_spawn_y,arena_x,arena_y,distance_from_actual_director_spawn,verified_floor_radius\n");
            File.WriteAllText(DirectoryPath + "/samples.csv", "character,checkpoint,target,policy,seconds,player_hp,enemy_hp,pattern,phase,reflections\n");
            File.WriteAllText(DirectoryPath + "/fixtures.csv", "character,checkpoint,source,sha256,source_seed,source_layout_seed,source_time_scale,source_field_seconds,source_cohort,level,max_hp,needle_damage,needle_cooldown,needle_count,boss_multiplier,source_live_damage,source_live_cooldown,source_live_count\n");
            File.WriteAllText(DirectoryPath + "/method.txt",
                (subset ?? "Full matrix for " + mode + ".") + " Source matrix indices=" + string.Join(",", sourceRounds) + ".\n" +
                "Fixture input root=" + Fixtures + " (GC_COMBAT_FIXTURES; unset uses the historical default path). Input manifest seed, layout seed, target time scale, actual field time and cohort are recorded per fixture in fixtures.csv. 3x growth observations are accelerated diagnostics and must not be called representative normal-speed growth; the 1x variant is the intended normal-speed source.\n" +
                "Fresh Stage2 growth-reset builds only: t0, t450 and t885 are observed checkpoints, not synthetic expected player progression. Source JSON and SHA256 are recorded. No historical Stage1 carryover fixture is used.\n" +
                "Each character/build/encounter/policy reloads the actual Level 1 scene and performs the actual Stage2 reset entry. Before EVERY window, only special-needle parents actually present in the observed fixture are temporarily unlocked/enabled so a user's original purchase settings cannot veto fixture restoration. All legacy preferences are restored on completion/stop. AbilityManager.ResetForStage destroys the entire existing ability graph and rebuilds only the fixture; a full owned-ability/type/level/acquisition signature must match. Player/general numeric snapshots and owned item assets are restored. User-selected starting weapons cannot remain as extra attacks. No purchased departure gear is added.\n" +
                "Independent-start normalization: full HP, skill active/passive durations/cooldown and dash charges reset; item proc cooldowns ready, charges/streaks/recent-hit/heal/kill state cleared; no delayed EXP or preloaded overflow/retaliation damage. Item elapsed growth and learned resistance are retained. This deliberately does NOT restore the original live item proc state, which the growth serializer did not capture. Restore never calls GainHealth, so normalization cannot charge healing-triggered items. Each window's field clock starts at its fixture checkpoint and advances normally without background spawns. No pickups or additional upgrades during measurement. Actual weapons, projectile collisions, armor, critical hits, statuses, skill cooldowns, healing and boss phases remain active. Seed is paired across the two policies. Time scale 1; all durations are game seconds unless explicitly wall_seconds. Each result records frames, maximum game-frame duration, and frames reaching 99% of Time.maximumDeltaTime so frame-limited attack throughput remains auditable.\n" +
                "Arena: unchanged Stage2 terrain, the director's actual lower-left spawn if it provides 6.5 units of clear floor, otherwise the nearest verified footprint from that spawn. An offset represents luring the boss onto nearby open floor; no walls are added or removed. Actual spawn, measured arena and offset distance are logged. The player starts outside all enabled body colliders with floor clearance at least0.55. Placement first seeks a shot-origin ring at80% of base needle reach (maximum3.5), then0.95 reach, then a wider ring still requiring actual collider-surface reach. Initial reach/line of sight and at least one valid movement direction are required.\n" +
                "Calibration: invulnerable stationary player, no active skills, real stationary bodies: Stage1 Tangdengi 24000 HP reference, Stage2 Turtle 3000 HP, King 34000 HP. Body AI is disabled only after arrival. Phase invulnerability still runs. Initial aim is toward the actual collider center within measured needle reach. Limits: 45/90/180 seconds for t0/t450/t885. Measured TTK is populated ONLY after actual defeat; time-limited trials are right-censored. HP/net-DPS is a separately named naive extrapolation, never a measured kill time.\n" +
                "Encounters: actual player HP, no invulnerability, no field background mobs/events/specials. Optional Turtle and King retain actual movement and pattern AI. Limits: 60/90/180 seconds. orbit_control follows a collider-centered ring at 80% of actual needle reach, observes walls/bodies, and cannot predict projectiles. Both policies decide every 0.15 seconds among16 directions using wall lookahead=currentMoveSpeed*0.18 (minimum0.22). reactive_world additionally scores currently visible projectiles projected0.5 seconds, vortex boundaries and minions. Dashes require a clear full path. If pushed onto a wall boundary, the bot permits ordinary movement only toward increasing clearance; it never teleports during measurement. Neither policy learns patterns or guarantees optimal evasion. Both attempt normal skills and use real dash charges. Rows with blocked decisions above20%, negative wall penetration below-0.05, in-range frames below40%, or no damage are labelled balance_valid=false rather than interpreted as difficulty evidence.\n" +
                "Patterns: t885 build only; each of the 18 legal phase/pattern combinations starts with a fresh king, a full-health player and fresh skill cooldowns. Phase2 is reached by an explicitly artificial phase setup before measurement; phase1 starts wounded to expose tribute healing. A window lasts at least eight seconds and until its coroutine completes (20-second cap). Full HP between windows prevents attrition from disguising single-technique damage; death censors remaining windows. This is not a continuous boss clear.\n" +
                "Shini uses only fuel actually stored in the fixture; none is granted and no additional field pickups are available in this isolated combat. When a real Tap starts, the bot stops walking, faces the boss and suppresses dashes until the natural preparation/short emission/fade ends, preventing the movement policy from aiming sideways or cancelling its own cast. It does not simulate holding a key for extended fuel consumption. Other characters can activate while moving; the retired sleep passive is not used.\n" +
                "Contact-aware policy revision: all enabled player body colliders, including the fitted Full Hitbox and feet, are considered; the disabled pickup collider is excluded. Future positions use translated actual world-space collider bounds against boss collider bounds, a conservative AABB approximation accounting for current offset/scale/rotation. A candidate overlapping these bounds is rejected unless escaping an already overlapping position. Actual contact is measured independently each engine frame with Physics2D.Distance; initial real collider separation must exceed0.05. Contact frames/minimum separation and collider geometry are recorded. Damage events include nearby actor counts so body contact can be distinguished from a suspected extra encounter; this is diagnostic correlation, not a source-labelled damage event. No source-game hitbox or damage value is changed.\n" +
                "Range revision: the actual public GetReturnCatchPosition(lastNeedleDirection) API calls the same spawn-position implementation as basic needles, including current hand offset, walking pose, facing and world transform. initial_shot_center_distance records that origin to boss collider center; initial_shot_surface_distance and in_range_fraction use Collider2D.ClosestPoint(origin), matching automatic boss acquisition, plus a clear center-directed lane. This differs from the earlier diagnostic's foot-to-center metric. The range remains the unextended effective basic needle travel range; legendary extra range is deliberately not credited. Future movement scoring translates the current actual shot origin; subsequent frames remeasure the real hand anchor.\n" +
                "Health deltas are sampled each engine frame. Simultaneous damage and tribute healing can cancel in the same frame, so observed_healing_lower_bound explicitly understates such hidden recovery. player_damage_counter_including_overkill is the actual StatsManager damage counter; a lethal hit can include damage beyond remaining HP. damaged_frames counts frames in which that counter increased, not independent hits when several attacks coincide. Reflections use the runtime counter. Minimum wall clearance and blocked direction decisions measure the bot's available routes, not a proof that a human can evade every attack. No output is a human win rate or human clear-time prediction.\n");
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + "Round", 0); SessionState.SetInt(Key + "Rounds", rounds);
            SessionState.SetBool(Key + "Failed", false); SessionState.SetBool(Key + "Stopped", false);
            foreach (string pref in new[] { "Coins", "LobbySilverCoins" })
            { SessionState.SetBool(Key + pref + "Had", PlayerPrefs.HasKey(pref)); SessionState.SetInt(Key + pref, PlayerPrefs.GetInt(pref)); }
            Next();
        }
        static void SaveProfile()
        {
            var keys = (IEnumerable<string>)typeof(ProgressPrefs).GetMethod("LegacyKeys", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            var saved = new SavedProfile
            {
                prefs = keys.Concat(new[] { GamePreferences.SaveKey, "Apothecary.Volume", "Apothecary.ReducedMotion" }).Distinct().Select(key =>
                {
                    string kind = key == "EquippedRelicId" || key == GamePreferences.SaveKey ? "string" : key == "Apothecary.Volume" ? "float" : "int";
                    return new SavedPref { key = key, had = PlayerPrefs.HasKey(key), kind = kind, text = kind == "string" ? PlayerPrefs.GetString(key) : "", value = kind == "int" ? PlayerPrefs.GetInt(key) : 0, real = kind == "float" ? PlayerPrefs.GetFloat(key) : 0 };
                }).ToArray(),
                preferences = JsonUtility.ToJson(GamePreferences.Current), character = AssetDatabase.GetAssetPath(CrossSceneData.CharacterBlueprint),
                transfer = CrossSceneData.PendingRunSceneTransfer == null ? "" : JsonUtility.ToJson(CrossSceneData.PendingRunSceneTransfer),
                carry = (CrossSceneData.StartingLobbyItems ?? Array.Empty<MerchantItemBlueprint>()).Select(AssetDatabase.GetAssetPath).ToArray(),
                selectedCarry = LobbyLoadoutData.SelectedCarryItems.Select(AssetDatabase.GetAssetPath).ToArray(), timeScale = Time.timeScale, runInBackground = Application.runInBackground
            };
            File.WriteAllText(ProfileBackup, JsonUtility.ToJson(saved, true));
        }
        public static void RestoreSavedPreferences() => RestoreProfile();
        static void RestoreProfile()
        {
            if (!File.Exists(ProfileBackup)) return;
            var saved = JsonUtility.FromJson<SavedProfile>(File.ReadAllText(ProfileBackup));
            foreach (var pref in saved.prefs)
                if (!pref.had) PlayerPrefs.DeleteKey(pref.key);
                else if (pref.kind == "string") PlayerPrefs.SetString(pref.key, pref.text);
                else if (pref.kind == "float") PlayerPrefs.SetFloat(pref.key, pref.real);
                else PlayerPrefs.SetInt(pref.key, pref.value);
            PlayerPrefs.Save(); GamePreferences.Apply(JsonUtility.FromJson<GamePreferenceValues>(saved.preferences), false, false);
            CrossSceneData.CharacterBlueprint = string.IsNullOrEmpty(saved.character) ? null : AssetDatabase.LoadAssetAtPath<CharacterBlueprint>(saved.character);
            CrossSceneData.StartingLobbyItems = (saved.carry ?? Array.Empty<string>()).Select(AssetDatabase.LoadAssetAtPath<MerchantItemBlueprint>).Where(x => x != null).ToArray();
            CrossSceneData.PendingRunSceneTransfer = string.IsNullOrEmpty(saved.transfer) ? null : JsonUtility.FromJson<RunSceneTransferSnapshot>(saved.transfer);
            var selected = (List<MerchantItemBlueprint>)typeof(LobbyLoadoutData).GetField("selectedCarryItems", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            selected.Clear(); selected.AddRange((saved.selectedCarry ?? Array.Empty<string>()).Select(AssetDatabase.LoadAssetAtPath<MerchantItemBlueprint>).Where(x => x != null));
            Time.timeScale = saved.timeScale; Application.runInBackground = saved.runInBackground;
            File.Copy(ProfileBackup, Output + "/profile-restored.json", true); File.Delete(ProfileBackup);
        }
        static int CharacterIndex(int round) => Mode == "encounters" ? round / 12 : Mode == "patterns" ? round / 2 : Mode == "pilot" ? 0 : round / 3;
        static int Checkpoint(int round) => Mode == "encounters" ? Checkpoints[(round / 4) % 3] : Mode == "pilot" ? SessionState.GetInt(Key + "PilotCheckpoint", 0) : Mode == "patterns" ? 885 : Checkpoints[round % 3];
        static void Next()
        {
            Time.timeScale = 1; SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key + "NeedsScene", true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started = false; deadline = EditorApplication.timeSinceStartup + 900;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string text, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            File.AppendAllText(DirectoryPath + "/errors.txt", text + "\n" + stack + "\n"); SessionState.SetBool(Key + "Failed", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (File.Exists(StopRequest) && !SessionState.GetBool(Key + "Stopped", false))
            {
                SessionState.SetBool(Key + "Stopped", true); SessionState.SetBool(Key + "Done", true);
                File.AppendAllText(DirectoryPath + "/censoring.txt", "Operator stop during round " + SessionState.GetInt(Key + "Round", 0) + "; any unfinished window has samples only and no completed result row.\n");
            }
            if (EditorApplication.timeSinceStartup > deadline)
            { File.AppendAllText(DirectoryPath + "/errors.txt", "Round deadline\n"); SessionState.SetBool(Key + "Failed", true); }
            if (SessionState.GetBool(Key + "Done", false) || SessionState.GetBool(Key + "Failed", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                int next = SessionState.GetInt(Key + "Round", 0) + 1;
                bool failed = SessionState.GetBool(Key + "Failed", false);
                bool stopped = SessionState.GetBool(Key + "Stopped", false);
                if (!failed && !stopped && next < SessionState.GetInt(Key + "Rounds", 1)) { SessionState.SetInt(Key + "Round", next); Next(); return; }
                foreach (string pref in new[] { "Coins", "LobbySilverCoins" })
                    if (SessionState.GetBool(Key + pref + "Had", false)) PlayerPrefs.SetInt(pref, SessionState.GetInt(Key + pref, 0)); else PlayerPrefs.DeleteKey(pref);
                PlayerPrefs.Save(); RestoreProfile(); SessionState.SetBool(Key, false);
                File.WriteAllText(DirectoryPath + "/result.txt", failed ? "FAIL" : stopped ? "STOPPED: profile restored; no further rounds; completed rows remain valid" : "PASS: actual-engine controlled measurements; not human success rates");
                EditorApplication.Exit(failed ? 1 : 0); return;
            }
            if (!EditorApplication.isPlaying) return;
            int ordinal = SessionState.GetInt(Key + "Round", 0);
            int round = SessionState.GetInt(Key + "SourceRound." + ordinal, ordinal);
            if (SessionState.GetBool(Key + "NeedsScene", false))
            {
                SessionState.SetBool(Key + "NeedsScene", false);
                CrossSceneData.CharacterBlueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/" + Characters[CharacterIndex(round)] + " Character Blueprint.asset");
                LobbyLoadoutData.Clear(); CrossSceneData.StartingLobbyItems = Array.Empty<MerchantItemBlueprint>(); CrossSceneData.ClearPendingRunSceneTransfer();
                UnityEngine.SceneManagement.SceneManager.LoadScene("Level 1"); return;
            }
            var guide = Object.FindObjectOfType<TutorialGuide>(); if (guide != null) { guide.Close(); guide.enabled = false; }
            var level = Object.FindObjectOfType<LevelManager>();
            if (started || level == null || !level.InitialFieldObjectsReady || level.PlayerCharacter == null) return;
            started = true; level.StartCoroutine(Exercise(level, round));
        }

        static void Silence(LevelManager level)
        {
            level.enabled = false;
            foreach (var b in Object.FindObjectsOfType<MonoBehaviour>())
                if (b is StageEventDirector || b is TimedSpecialMonsterSpawner || b is AcidToadSpawn || b is NPCSpawner || b is SnailBossDebugSpawn || b is ConstipationKingDirector || b is ColonBossDirector)
                { b.enabled = false; b.StopAllCoroutines(); }
        }
        static void Fire(bool enabled)
        { foreach (var ability in Object.FindObjectsOfType<Ability>()) ability.enabled = enabled; }
        static void Clear(LevelManager level)
        {
            foreach (var m in level.EntityManager.LivingMonsters.ToArray()) m.DespawnForStageTransfer();
            foreach (var p in Object.FindObjectsOfType<Projectile>()) p.RemoveForItem();
            foreach (var pickup in Object.FindObjectsOfType<Collectable>()) pickup.DissolveInAcidLake();
        }
        static string Stem(string character, int checkpoint) => Fixtures + "/" + character + "-stage2-t" + checkpoint;
        static RunSceneTransferSnapshot Load(string character, int checkpoint)
        {
            string path = Stem(character, checkpoint) + "-build.json";
            if (!File.Exists(path)) throw new FileNotFoundException("Fresh growth fixture is required; historical carryover is deliberately unsupported", path);
            return JsonUtility.FromJson<RunSceneTransferSnapshot>(File.ReadAllText(path));
        }
        static IEnumerator Restore(LevelManager level, string character, int checkpoint, bool log)
        {
            bool previousPause = level.IsRunFlowPaused; float previousScale = Time.timeScale;
            level.SetRunFlowPaused(true); Time.timeScale = 0;
            try
            {
            var snapshot = Load(character, checkpoint); var player = level.PlayerCharacter;
            var manifest = JsonUtility.FromJson<FixtureManifest>(File.ReadAllText(Stem(character, checkpoint) + "-manifest.json"));
            if (manifest.character != character || manifest.stage != 2 || manifest.requestedCheckpoint != checkpoint) throw new Exception("Fresh growth manifest does not match its requested combat fixture");
            snapshot.CharacterState.SkillPassiveRemaining = 0; snapshot.CharacterState.SkillActiveRemaining = 0; snapshot.CharacterState.SkillCooldownRemaining = 0; snapshot.CharacterState.SkillSummonRemaining = 0;
            snapshot.CharacterState.CurrentHealth = manifest.maxHealth; snapshot.CharacterState.CurrentDashCharges = snapshot.CharacterState.MaxDashCharges;
            snapshot.CharacterState.ThermometerStacks = 0;
            // End all previous-window attacks/buffs before the replacement graph is created.
            player.PrepareStageGrowthReset(); ClearPlayerEffects(player);
            OctoberItemRuntime.Get(player).ResetForStage(); Clear(level);
            var manager = Object.FindObjectOfType<AbilityManager>();
            EnsureFixtureParentAccess(manager, snapshot.AbilityStates);
            yield return manager.ResetForStage(level.CurrentLevelBlueprint, level.EntityManager, player, snapshot.AbilityStates);
            string expectedAbilities = AbilitySignature(snapshot.AbilityStates), actualAbilities = AbilitySignature(manager.CaptureRunSceneAbilities());
            if (expectedAbilities != actualAbilities) throw new Exception("Fresh fixture graph mismatch. Expected=" + expectedAbilities + " Actual=" + actualAbilities);
            player.ResetStageGrowth(snapshot.CharacterState);
            if (snapshot.OctoberItems != null)
            {
                // State-only source snapshots cannot recover live proc timers; normalize explicitly.
                var independentItems = new OctoberItemState
                {
                    owned = new List<int>(snapshot.OctoberItems.owned), elapsed = snapshot.OctoberItems.elapsed,
                    resistance = snapshot.OctoberItems.resistance, cooldownRemaining = new float[67],
                    hurtAge = 999, healAge = 999, killAge = 999, baseDamage = 8
                };
                OctoberItemRuntime.Get(player).Restore(independentItems);
                float day = level.LevelDuration > 0 ? level.LevelDuration : 1440;
                ColonContentSmoke.Set(OctoberItemRuntime.Get(player), "lastHour", Mathf.FloorToInt(checkpoint / day * 24));
            }
            if (snapshot.MerchantItems != null)
            {
                // Unity instance IDs in the previous editor process are not durable asset references.
                snapshot.MerchantItems.items = (manifest.merchantItemPaths ?? Array.Empty<string>()).Select(path => AssetDatabase.LoadAssetAtPath<MerchantItemBlueprint>(path)).ToList();
                if (snapshot.MerchantItems.items.Any(item => item == null)) throw new Exception("Missing merchant asset in fresh fixture");
                SynergyManager.Instance?.RestoreOwnership(snapshot.MerchantItems);
            }
            string generalPath = Stem(character, checkpoint) + "-general.json";
            if (!File.Exists(generalPath)) throw new FileNotFoundException("General stats companion is required", generalPath);
            var general = PlayerGeneralStatRuntime.GetOrCreate(player); JsonUtility.FromJsonOverwrite(File.ReadAllText(generalPath), general); general.SyncPickupCollider();
            player.CollectableCollider.enabled = false;
            if (!Mathf.Approximately(player.CurrentHealth, player.MaxHealth) || !Mathf.Approximately(player.MaxHealth, manifest.maxHealth)) throw new Exception("Full-HP fixture restore differs from its observed maximum health");
            VerifyNumericCharacter(snapshot.CharacterState, player.CaptureRunSceneState());
            if (CompactJson(File.ReadAllText(generalPath)) != CompactJson(JsonUtility.ToJson(general))) throw new Exception("General-stat snapshot differs after exact restoration");
            ColonContentSmoke.Set(level, "levelTime", (float)checkpoint);
            Fire(false);
            if (log)
            {
                var dart = Object.FindObjectOfType<SyringeDartAbility>(); string path = Stem(character, checkpoint) + "-build.json";
                string hash; using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
                File.AppendAllText(DirectoryPath + "/fixtures.csv", Row(character, checkpoint, path, hash, manifest.seed, manifest.layoutSeed, manifest.targetTimeScale, manifest.actualFieldSeconds, manifest.cohort, player.CurrentLevel, player.MaxHealth, dart.GetEffectiveDamage(), dart.GetEffectiveCooldown(), dart.GetEffectiveProjectileCount(), general.BossDamageMultiplier, manifest.dartDamage, manifest.dartCooldown, manifest.dartProjectiles));
                File.WriteAllText(DirectoryPath + "/" + character + "-t" + checkpoint + "-source-manifest.json", File.ReadAllText(Stem(character, checkpoint) + "-manifest.json"));
            }
            }
            finally { level.SetRunFlowPaused(previousPause); Time.timeScale = previousScale; }
        }
        static string CompactJson(string json) => new string(json.Where(c => !char.IsWhiteSpace(c)).ToArray());
        static void EnsureFixtureParentAccess(AbilityManager manager, IEnumerable<RunSceneAbilitySnapshot> snapshots)
        {
            var candidates = manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true);
            foreach (var snapshot in snapshots.Where(s => s.AbilityTypeName == typeof(SyringeSpecialAugmentAbility).FullName))
            {
                var parent = candidates.FirstOrDefault(candidate => FixtureAbilityName(candidate.gameObject.name) == snapshot.AbilityObjectName);
                if (parent == null || !StartingNeedleSelection.Available(parent.Type)) throw new Exception("Unavailable special parent in observed fixture: " + snapshot.AbilityObjectName);
                // Fresh growth temporarily exposed the native needles of all four characters.
                // Recreate access only for the observed parents; the full graph check prevents extras.
                PlayerPrefs.SetInt("LobbyUnlock.Needle." + parent.Type, 1);
                PlayerPrefs.SetInt(StartingNeedleSelection.EnabledKey(parent.Type), 1);
            }
        }
        static string FixtureAbilityName(string name) => (name.EndsWith("(Clone)", StringComparison.Ordinal) ? name.Substring(0, name.Length - "(Clone)".Length) : name).Trim();
        static string AbilitySignature(IEnumerable<RunSceneAbilitySnapshot> snapshots) => string.Join(";", snapshots.Select(a => a.AbilityTypeName + "/" + a.AbilityObjectName + "/" + a.Level + "/" + string.Join(",", a.ConditionalAugmentIds ?? new List<string>())).OrderBy(a => a, StringComparer.Ordinal));
        static void VerifyNumericCharacter(RunSceneCharacterSnapshot expected, RunSceneCharacterSnapshot actual)
        {
            foreach (var field in typeof(RunSceneCharacterSnapshot).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType == typeof(float))
                {
                    if (!Mathf.Approximately((float)field.GetValue(expected), (float)field.GetValue(actual))) throw new Exception("Character fixture mismatch: " + field.Name);
                }
                else if (field.FieldType.IsPrimitive && !Equals(field.GetValue(expected), field.GetValue(actual))) throw new Exception("Character fixture mismatch: " + field.Name);
            }
        }
        static void ClearPlayerEffects(Character player)
        {
            HungerNeedleRuntime.Clear(player); PoisonContagionRuntime.Disable();
            RemovePlayerEffect<LegendaryConditionalSpecialRuntime>(player); RemovePlayerEffect<GastricPeristalsisWaveController>(player);
            RemovePlayerEffect<MucosalFortressShieldController>(player); RemovePlayerEffect<HungrySpiritController>(player); RemovePlayerEffect<SnailSlowStatus>(player);
            RemoveWorldEffect<AcupunctureFormationController>(); RemoveWorldEffect<CursorControlledNeedleController>(); RemoveWorldEffect<HedgehogNeedleController>();
            RemoveWorldEffect<SyringeCloneController>(); RemoveWorldEffect<OrganCompressionField>(); RemoveWorldEffect<Ver4GroundEffect>();
            RemoveWorldEffect<DigestiveAcidPuddle>(); RemoveWorldEffect<FiberTrailSegment>();
        }
        static void RemovePlayerEffect<T>(Character player) where T : MonoBehaviour
        { foreach (var effect in player.GetComponents<T>()) { effect.enabled = false; effect.StopAllCoroutines(); Object.Destroy(effect); } }
        static void RemoveWorldEffect<T>() where T : MonoBehaviour
        { foreach (var effect in Object.FindObjectsOfType<T>()) { effect.gameObject.SetActive(false); Object.Destroy(effect.gameObject); } }
        static IEnumerator Exercise(LevelManager level, int round)
        {
            var preferences = GamePreferences.Current.Copy(); preferences.pauseOnFocusLoss = false; preferences.muteOnFocusLoss = false; GamePreferences.Apply(preferences, false);
            Application.runInBackground = true; RunScore.Exclude("Fresh Stage2 controlled combat balance probe"); Silence(level); Fire(false);
            ColonContentSmoke.Set(level.PlayerCharacter, "isInvincible", true);
            yield return ColonContentSmoke.Enter(level); Silence(level); Clear(level); Time.timeScale = Speed;
            var reset = level.GetComponent<StageGrowthReset>(); if (reset == null || reset.ResetCount != 1) throw new Exception("Combat fixture must enter through the actual fresh growth reset");
            string character = Characters[CharacterIndex(round)]; int checkpoint = Checkpoint(round);
            yield return Restore(level, character, checkpoint, true); Fire(false); yield return null; Clear(level);
            if (Mode == "calibration")
            {
                foreach (int type in new[] { 0, 1, 2 })
                {
                    yield return Restore(level, character, checkpoint, false); Fire(false); Clear(level);
                    var target = Spawn(level, type); yield return Ready(target);
                    target.Freeze(); PlaceInReach(level.PlayerCharacter, target);
                    level.PlayerCharacter.Move(Vector2.left); level.PlayerCharacter.Move(Vector2.zero);
                    ColonContentSmoke.Set(level.PlayerCharacter, "isInvincible", true); Fire(true);
                    yield return Measure(level, target, character, checkpoint, "stationary", 73319 + CharacterIndex(round) * 91 + checkpoint, checkpoint == 0 ? 45 : checkpoint == 450 ? 90 : 180, false, -1, 0);
                    Fire(false); target.Destroy(); Clear(level); yield return null;
                }
            }
            else if (Mode == "encounters" || Mode == "pilot")
            {
                int type = Mode == "pilot" ? 2 : (round / 2) % 2 + 1;
                int policy = Mode == "pilot" ? 1 : round % 2;
                var target = Spawn(level, type); yield return Ready(target);
                PlaceInReach(level.PlayerCharacter, target);
                yield return Restore(level, character, checkpoint, false); Fire(true); ColonContentSmoke.Set(level.PlayerCharacter, "isInvincible", false); target.Auto(true);
                yield return Measure(level, target, character, checkpoint, policy == 0 ? "orbit_control" : "reactive_world", 91873 + CharacterIndex(round) * 1009 + checkpoint * 3 + type * 71, Mode == "pilot" ? 30 : checkpoint == 0 ? 60 : checkpoint == 450 ? 90 : 180, true, -1, 0);
                target.Destroy();
            }
            else
            {
                int policy = round % 2;
                for (int phase = 1; phase <= 2 && level.PlayerCharacter.IsAlive; phase++)
                for (int pattern = 0; pattern < ConstipationKingRuntime.PatternCount && level.PlayerCharacter.IsAlive; pattern++)
                {
                    if (phase == 1 && pattern == 1 || phase == 2 && pattern == 7) continue;
                    Fire(false); Clear(level); yield return Restore(level, character, checkpoint, false);
                    ColonContentSmoke.Set(level.PlayerCharacter, "isInvincible", true);
                    var target = Spawn(level, 2); yield return Ready(target);
                    if (phase == 2)
                    {
                        target.King.TakeDamage(target.King.MaxHealth);
                        yield return Until(() => target.King.PhaseTwo && !target.King.Transitioning, 15, "phase-two fixture setup");
                    }
                    else target.King.TakeDamage(target.King.MaxHealth * .2f);
                    PlaceInReach(level.PlayerCharacter, target);
                    yield return Restore(level, character, checkpoint, false); Fire(true); ColonContentSmoke.Set(level.PlayerCharacter, "isInvincible", false);
                    if (!target.King.PlayPattern(pattern)) throw new Exception("Legal pattern fixture did not start: " + phase + "/" + pattern);
                    yield return Measure(level, target, character, checkpoint, policy == 0 ? "orbit_control" : "reactive_world", 81731 + CharacterIndex(round) * 101 + phase * 31 + pattern, 20, true, pattern, phase);
                    Fire(false); target.Destroy(); Clear(level); yield return null;
                    if (!level.PlayerCharacter.IsAlive) File.AppendAllText(DirectoryPath + "/censoring.txt", character + " policy=" + policy + " died in phase=" + phase + " pattern=" + pattern + "; later pattern windows were not observed.\n");
                }
            }
            level.PlayerCharacter.Move(Vector2.zero); SessionState.SetBool(Key + "Done", true);
        }

        sealed class Target
        {
            public FoodBossRuntime Food; public ColonBossRuntime Optional; public ConstipationKingRuntime King;
            public string Name => Food != null ? "Stage1_Tangdengi_24000" : Optional != null ? "Stage2_Turtle_3000" : "Stage2_King_34000";
            public Component Actor => Food != null ? (Component)Food : Optional != null ? (Component)Optional : King;
            public float Health => Food != null ? Food.Health : Optional != null ? Optional.Health : King != null ? King.Health : 0;
            public bool Dead => Actor == null || (Food != null ? Food.Dead : Optional != null ? Optional.Dead : King.Dead);
            public Vector2 Foot => Actor == null ? Vector2.zero : (Vector2)Actor.transform.position;
            public Vector2 Center => Food != null ? (Vector2)Food.GetComponent<Collider2D>().bounds.center : Optional != null ? Optional.HitCenter : King.HitCenter;
            public bool Arriving => Optional != null ? Optional.Arriving : King != null && King.Arriving;
            public int Phase => King != null && King.PhaseTwo || Food != null && Food.PhaseTwo ? 2 : 1;
            public int Pattern => King != null ? King.CurrentPattern : Optional != null ? Optional.CurrentPattern : -1;
            public int Patterns => King != null ? King.CompletedPatterns : Optional != null ? Optional.CompletedPatterns : 0;
            public int Reflections => King != null ? King.ReflectedProjectiles : 0;
            public void Auto(bool value) { if (Food != null) Food.AutoPatterns = value; if (Optional != null) Optional.AutoPatterns = value; if (King != null) King.AutoPatterns = value; }
            public void Freeze() { if (Actor != null) { ((MonoBehaviour)Actor).enabled = false; Actor.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll; } }
            public void Destroy() { if (Actor != null) { ((MonoBehaviour)Actor).StopAllCoroutines(); Object.Destroy(Actor.gameObject); } }
        }
        static Target Spawn(LevelManager level, int type)
        {
            var map = FiniteStomachMap.Instance; var opening = StomachOpening.Find(false); var shape = map.OpeningShape(false);
            Vector2 approach = opening != null ? opening.FrontLip + opening.Inward * 3 : map.UVToWorld(new Vector2(shape.x, shape.y)) + Vector2.up * 7;
            Vector2 actualSpawn = map.ClampToFloor(approach, ConstipationKingRuntime.BodyRadius + .4f);
            Vector2 at = map.ClampToFloor(actualSpawn, 6.5f);
            float clearance = Mathf.Min(map.FloorClearance(at), map.FloorClearance(at, true));
            if (clearance < 6.45f) throw new Exception("No verified open-floor encounter footprint near the actual king approach");
            PlacePlayer(level.PlayerCharacter, at + Vector2.right * 4);
            var target = new Target();
            if (type == 0) target.Food = FoodBossRuntime.Spawn(FoodBossKind.Tangdengi, level.PlayerCharacter, at);
            else if (type == 1) target.Optional = ColonBossRuntime.Spawn(ColonBossKind.Turtle, level.PlayerCharacter, at, null);
            else target.King = ConstipationKingRuntime.Spawn(level, at);
            target.Auto(false);
            File.AppendAllText(DirectoryPath + "/arena.csv", Row(target.Name, actualSpawn.x, actualSpawn.y, at.x, at.y, Vector2.Distance(actualSpawn, at), clearance));
            return target;
        }
        static IEnumerator Ready(Target target) => Until(() => !target.Arriving, 20, "boss arrival");
        static IEnumerator Until(Func<bool> predicate, float timeout, string title)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < until) yield return null;
            if (!predicate()) throw new Exception("Timed out: " + title);
        }
        static void PlacePlayer(Character player, Vector2 position)
        { ColonContentSmoke.Position(player, FiniteStomachMap.Instance.ClampToFloor(position, .5f)); }
        static float NeedleReach() => Object.FindObjectOfType<SyringeDartAbility>().GetEffectiveSyringeMaxDistance();
        static float PreferredDistance() => Mathf.Min(3.5f, NeedleReach() * .8f);
        static Vector2 ShotOrigin(SyringeDartAbility weapon) => weapon.GetReturnCatchPosition((Vector2)NeedleDirection.GetValue(weapon));
        static float ShotSurfaceDistance(Vector2 origin, Target target) => Vector2.Distance(origin, target.Actor.GetComponent<Collider2D>().ClosestPoint(origin));
        static Collider2D[] PlayerHurtboxes(Character player) => player.GetComponentsInChildren<Collider2D>(true).Where(c => c.enabled && c.gameObject.activeInHierarchy && c != player.CollectableCollider).ToArray();
        static float ActualBodyGap(Character player, Target target)
        {
            var boss = target.Actor.GetComponent<Collider2D>(); if (!boss.enabled) return 100;
            float gap = 100;
            foreach (var collider in PlayerHurtboxes(player))
            { var separation = Physics2D.Distance(collider, boss); if (separation.isValid) gap = Mathf.Min(gap, separation.distance); }
            return gap;
        }
        static float PredictedBodyGap(Character player, Target target, Vector2 next)
        {
            var boss = target.Actor.GetComponent<Collider2D>().bounds; Vector3 shift = (Vector3)next - player.transform.position; float gap = 100;
            foreach (var collider in PlayerHurtboxes(player))
            {
                var body = collider.bounds; body.center += shift;
                float dx = Mathf.Max(boss.min.x - body.max.x, body.min.x - boss.max.x), dy = Mathf.Max(boss.min.y - body.max.y, body.min.y - boss.max.y);
                float distance = dx > 0 || dy > 0 ? new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude : Mathf.Max(dx, dy);
                gap = Mathf.Min(gap, distance);
            }
            return gap;
        }
        static void PlaceInReach(Character player, Target target)
        {
            var map = FiniteStomachMap.Instance; var weapon = Object.FindObjectOfType<SyringeDartAbility>();
            float reach = weapon.GetEffectiveSyringeMaxDistance();
            float bossExtent = Mathf.Min(target.Actor.GetComponent<Collider2D>().bounds.extents.x, target.Actor.GetComponent<Collider2D>().bounds.extents.y);
            foreach (float radius in new[] { PreferredDistance(), reach * .95f, reach + bossExtent * .6f })
            for (int i = 0; i < 32; i++)
            {
                Vector2 offset = ShotOrigin(weapon) - (Vector2)player.transform.position;
                Vector2 at = target.Center + FoodBossRuntime.Dir(i * 11.25f) * radius - offset;
                if (map.FloorClearance(at, true) < .55f || PredictedBodyGap(player, target, at) < .12f) continue;
                ColonContentSmoke.Position(player, at);
                Vector2 origin = ShotOrigin(weapon);
                if (ActualBodyGap(player, target) > .05f && ShotSurfaceDistance(origin, target) < reach && ColonAttackWall.Clear(origin, target.Center, .1f)) return;
            }
            throw new Exception("No wall-safe player position inside actual needle reach");
        }
        static string Row(params object[] values) => string.Join(",", values.Select(v => v == null ? "" : v is IFormattable number ? number.ToString(null, CultureInfo.InvariantCulture) : v.ToString())) + "\n";

        static IEnumerator Measure(LevelManager level, Target target, string character, int checkpoint, string policy, int seed, float limit, bool moving, int patternWindow, int explicitPhase)
        {
            UnityEngine.Random.InitState(seed);
            var player = level.PlayerCharacter; var stats = Object.FindObjectOfType<StatsManager>(); var map = FiniteStomachMap.Instance;
            var weapon = Object.FindObjectOfType<SyringeDartAbility>();
            Vector2 initialOrigin = ShotOrigin(weapon);
            float initialClearance = map.FloorClearance(player.transform.position, true), reach = weapon.GetEffectiveSyringeMaxDistance(), initialDistance = Vector2.Distance(initialOrigin, target.Center), initialSurfaceDistance = ShotSurfaceDistance(initialOrigin, target);
            ChooseDirection(player, target, false, out int initialDirections, out _);
            if (initialClearance < .55f || initialDirections == 0 || initialSurfaceDistance >= reach || ActualBodyGap(player, target) <= .05f || !ColonAttackWall.Clear(initialOrigin, target.Center, .1f)) throw new Exception("Invalid initial combat geometry: floor=" + initialClearance + " directions=" + initialDirections + " shot surface distance=" + initialSurfaceDistance + " reach=" + reach);
            foreach (var collider in PlayerHurtboxes(player))
            {
                var b = collider.bounds; var s = collider.transform.lossyScale;
                File.AppendAllText(DirectoryPath + "/hurtboxes.csv", Row(character, target.Name, collider.name, collider.GetType().Name, collider.offset.x, collider.offset.y, b.size.x, b.size.y, b.center.x, b.center.y, s.x, s.y, collider.transform.eulerAngles.z, collider is CapsuleCollider2D capsule ? capsule.direction.ToString() : "n/a"));
            }
            float began = Time.time, wall = Time.realtimeSinceStartup, health = player.CurrentHealth, enemyHealth = target.Health;
            float damageTaken = stats.DamageTaken, healing = stats.HealingReceived, lastHealth = target.Health, observedDamage = 0, observedHealing = 0;
            float lastTaken = damageTaken, nextThink = 0, nextSkill = 0, nextDash = 0, nextSample = 0, minWall = float.MaxValue;
            int hits = 0, skillUses = 0, dashes = 0, decisions = 0, blocked = 0, startReflections = target.Reflections, startPatterns = target.Patterns;
            int frames = 0, cappedFrames = 0, inRangeFrames = 0, contactFrames = 0; float maxFrameGameSeconds = 0, minimumBodyGap = 100;
            string outcome = "time_limit"; string targetName = target.Name;
            while (Time.time - began < limit)
            {
                frames++; maxFrameGameSeconds = Mathf.Max(maxFrameGameSeconds, Time.deltaTime);
                if (Time.deltaTime >= Time.maximumDeltaTime * .99f) cappedFrames++;
                Vector2 shotOrigin = ShotOrigin(weapon);
                if (ShotSurfaceDistance(shotOrigin, target) <= weapon.GetEffectiveSyringeMaxDistance() && ColonAttackWall.Clear(shotOrigin, target.Center, .1f)) inRangeFrames++;
                // Keep stage-time equipment honest while background spawning remains disabled.
                ColonContentSmoke.Set(level, "levelTime", checkpoint + Time.time - began);
                float current = target.Health;
                observedDamage += Mathf.Max(0, lastHealth - current); observedHealing += Mathf.Max(0, current - lastHealth); lastHealth = current;
                float actualGap = ActualBodyGap(player, target); minimumBodyGap = Mathf.Min(minimumBodyGap, actualGap); if (actualGap <= 0) contactFrames++;
                if (stats.DamageTaken > lastTaken + .001f)
                {
                    hits++;
                    File.AppendAllText(DirectoryPath + "/damage-events.csv", Row(character, targetName, policy, Time.time - began, stats.DamageTaken - lastTaken, player.CurrentHealth, actualGap, actualGap <= 0,
                        Object.FindObjectsOfType<ConstipationKingRuntime>().Length, Object.FindObjectsOfType<ColonBossRuntime>().Length, Object.FindObjectsOfType<FoodBossRuntime>().Length, level.EntityManager.LivingMonsters.Count,
                        player.transform.position.x, player.transform.position.y, target.Foot.x, target.Foot.y));
                }
                lastTaken = stats.DamageTaken;
                if (!player.IsAlive || player.CurrentHealth <= 0 || level.IsLevelEnded) { outcome = "bot_defeated"; break; }
                if (target.Dead) { outcome = "target_defeated"; break; }
                if (!moving && Time.time - began >= 8 && observedDamage <= 0) { outcome = "invalid_no_weapon_hits"; break; }
                if (patternWindow >= 0 && Time.time - began >= 8 && target.King != null && !target.King.Busy) { outcome = "pattern_window_complete"; break; }
                var dialog = level.EntityManager.AbilitySelectionDialog; if (dialog.MenuOpen) dialog.Close();
                if (ApothecaryUI.Instance != null && ApothecaryUI.Instance.Page != "hud") ApothecaryUI.Instance.Show("hud");
                Time.timeScale = Speed;
                if (player.Skills.IsShini && player.Skills.Breath != null && player.Skills.Breath.Busy)
                { player.Move(Vector2.zero); player.LookDirection = (target.Foot - (Vector2)player.CenterTransform.position).normalized; }
                minWall = Mathf.Min(minWall, map.FloorClearance(player.transform.position, true));
                if (moving && Time.time >= nextThink)
                {
                    nextThink = Time.time + .15f; decisions++;
                    Vector2 direction = ChooseDirection(player, target, policy == "reactive_world", out int available, out float danger);
                    if (available == 0) blocked++;
                    bool shiniCasting = player.Skills.IsShini && player.Skills.Breath != null && player.Skills.Breath.Busy;
                    if (shiniCasting) direction = Vector2.zero;
                    player.Move(direction);
                    if (Time.time >= nextSkill && player.Skills.CanActivate)
                    {
                        player.LookDirection = (target.Foot - (Vector2)player.CenterTransform.position).normalized;
                        player.Move(player.Skills.IsShini ? Vector2.zero : (target.Center - (Vector2)player.transform.position).normalized);
                        if (player.Skills.TryActivate()) skillUses++;
                        shiniCasting = player.Skills.IsShini && player.Skills.Breath != null && player.Skills.Breath.Busy;
                        if (shiniCasting) direction = Vector2.zero;
                        player.Move(direction); nextSkill = Time.time + .5f;
                    }
                    float bodyDistance = Vector2.Distance(player.transform.position, target.Foot);
                    Vector2 dashEnd = (Vector2)player.transform.position + direction * player.DashDistance;
                    if (!shiniCasting && Time.time >= nextDash && direction.sqrMagnitude > .1f && PredictedBodyGap(player, target, dashEnd) >= .12f && map.FloorClearance(dashEnd, true) >= .6f && ColonAttackWall.Clear(player.transform.position, dashEnd, .4f) && (bodyDistance < 2.6f || policy == "reactive_world" && danger < 1.1f || player.Skills.IsAri && player.Skills.Active))
                    { if (player.TryDash()) dashes++; nextDash = Time.time + .5f; }
                }
                if (Time.time >= nextSample)
                {
                    nextSample = Time.time + 5;
                    File.AppendAllText(DirectoryPath + "/samples.csv", Row(character, checkpoint, targetName, policy, Time.time - began, player.CurrentHealth, target.Health, target.Pattern, target.Phase, target.Reflections));
                    File.WriteAllText(DirectoryPath + "/status.txt", character + " t" + checkpoint + " " + targetName + " " + policy + " " + (Time.time - began).ToString("F1") + "s HP=" + player.CurrentHealth.ToString("F1"));
                }
                yield return null;
            }
            player.Move(Vector2.zero);
            float elapsed = Time.time - began, remaining = target.Health, net = enemyHealth - remaining, dps = net / Mathf.Max(.001f, elapsed);
            observedDamage += Mathf.Max(0, lastHealth - remaining); observedHealing += Mathf.Max(0, remaining - lastHealth);
            float blockedFraction = decisions == 0 ? 0 : blocked / (float)decisions, inRangeFraction = frames == 0 ? 0 : inRangeFrames / (float)frames;
            bool valid = blockedFraction <= .2f && minWall >= -.05f && inRangeFraction >= .4f && observedDamage > 0;
            File.AppendAllText(DirectoryPath + "/results.csv", Row(character, checkpoint, targetName, explicitPhase, patternWindow, policy, seed, elapsed, Time.realtimeSinceStartup - wall, frames, maxFrameGameSeconds, cappedFrames,
                health, Mathf.Max(0, player.CurrentHealth), enemyHealth, remaining, observedDamage, observedHealing, dps, stats.DamageTaken - damageTaken, stats.HealingReceived - healing,
                hits, skillUses, dashes, target.Reflections - startReflections, target.Patterns - startPatterns, blocked, decisions, minWall, initialClearance, initialDistance, initialSurfaceDistance, reach, inRangeFraction, blockedFraction, contactFrames, minimumBodyGap, valid, outcome,
                outcome == "target_defeated" ? elapsed.ToString("R", CultureInfo.InvariantCulture) : "", dps > 0 ? (enemyHealth / dps).ToString("R", CultureInfo.InvariantCulture) : ""));
        }

        static Vector2 ChooseDirection(Character player, Target target, bool reactive, out int available, out float danger)
        {
            Vector2 at = player.transform.position, origin = ShotOrigin(Object.FindObjectOfType<SyringeDartAbility>()), radial = (origin - target.Center).normalized;
            if (radial.sqrMagnitude < .01f) radial = Vector2.right;
            Vector2 tangent = Vector2.Perpendicular(radial), best = Vector2.zero;
            var map = FiniteStomachMap.Instance; float preferred = PreferredDistance(), currentClearance = map.FloorClearance(at, true);
            float currentBodyGap = PredictedBodyGap(player, target, at);
            var kingShots = reactive ? Object.FindObjectsOfType<ConstipationKingProjectile>() : Array.Empty<ConstipationKingProjectile>();
            var optionalShots = reactive ? Object.FindObjectsOfType<ColonBossProjectile>() : Array.Empty<ColonBossProjectile>();
            var pools = reactive ? Object.FindObjectsOfType<ConstipationKingVortex>() : Array.Empty<ConstipationKingVortex>();
            var minions = reactive ? Object.FindObjectsOfType<ConstipationKingMinion>() : Array.Empty<ConstipationKingMinion>();
            danger = 99; available = 0; float bestScore = float.NegativeInfinity;
            foreach (var shot in kingShots) danger = Mathf.Min(danger, SnailBossProjectile.SegmentDistance(at, shot.transform.position, (Vector2)shot.transform.position + shot.Velocity * .5f));
            foreach (var shot in optionalShots) danger = Mathf.Min(danger, SnailBossProjectile.SegmentDistance(at, shot.transform.position, (Vector2)shot.transform.position + shot.Velocity * .5f));
            for (int i = 0; i < 16; i++)
            {
                // Wall lookahead follows one 0.15-second decision, independently of bullet prediction.
                Vector2 direction = FoodBossRuntime.Dir(i * 22.5f), next = at + direction * Mathf.Max(.22f, player.CurrentMoveSpeed * .18f);
                float clearance = map.FloorClearance(next, true);
                float bodyGap = PredictedBodyGap(player, target, next);
                if (bodyGap < .12f && !(currentBodyGap < .12f && bodyGap > currentBodyGap + .04f)) continue;
                bool escapingBoundary = currentClearance < .45f && clearance > currentClearance + .08f;
                if (!escapingBoundary && (clearance < .4f || !ColonAttackWall.Clear(at, next, .24f))) continue;
                available++;
                float distance = Vector2.Distance(origin + next - at, target.Center), score = -Mathf.Abs(distance - preferred) * 2.5f + Vector2.Dot(direction, tangent);
                if (bodyGap < .4f) score -= (.4f - bodyGap) * 20;
                if (currentClearance < .65f) score += Mathf.Min(2, clearance - currentClearance) * 30;
                foreach (var shot in kingShots)
                { float d = SnailBossProjectile.SegmentDistance(next, shot.transform.position, (Vector2)shot.transform.position + shot.Velocity * .5f); if (d < 1.3f) score -= (1.3f - d) * 10; }
                foreach (var shot in optionalShots)
                { float d = SnailBossProjectile.SegmentDistance(next, shot.transform.position, (Vector2)shot.transform.position + shot.Velocity * .5f); if (d < 1.3f) score -= (1.3f - d) * 10; }
                foreach (var pool in pools) { float d = Vector2.Distance(next, pool.Center); if (d < pool.Radius + .3f) score -= (pool.Radius + .3f - d) * 7; }
                foreach (var minion in minions) score -= 3 / Mathf.Max(.25f, ((Vector2)minion.transform.position - next).sqrMagnitude);
                if (score > bestScore) { bestScore = score; best = direction; }
            }
            return best;
        }
    }
}
#endif
