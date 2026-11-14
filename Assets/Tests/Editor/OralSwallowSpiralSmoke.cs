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
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    /// <summary>Actual play-mode acceptance for the swallow capture, damage and spawn cutoff.</summary>
    [InitializeOnLoad]
    public static class OralSwallowSpiralSmoke
    {
        const string Key = "OralSwallowSpiralSmoke", Root = "Library/OralSwallowSpiralProof";
        static bool started;
        static double deadline;
        static int normalPool;
        static MonsterBlueprint normalBlueprint;
        static Vector2 fixtureCenter;
        [Serializable] sealed class Pref { public string key; public bool had; public int value; }
        [Serializable] sealed class Backup { public Pref[] prefs; }

        static OralSwallowSpiralSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SeedSelection()
        {
            if (!SessionState.GetBool(Key, false)) return;
            CrossSceneData.CharacterBlueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems = Array.Empty<MerchantItemBlueprint>();
        }
        public static void Run()
        {
            Directory.CreateDirectory(Root); RestorePrefs();
            File.WriteAllText(Root + "/result.txt", "RUNNING"); File.WriteAllText(Root + "/checks.txt", ""); File.WriteAllText(Root + "/errors.txt", "");
            File.WriteAllText(Root + "/samples.csv", "test,age,elapsed,radius,angle,health,monsters\n");
            var keys = new[] { "Coins", "LobbySilverCoins" };
            File.WriteAllText(Root + "/prefs.json", JsonUtility.ToJson(new Backup { prefs = keys.Select(k => new Pref { key = k, had = PlayerPrefs.HasKey(k), value = PlayerPrefs.GetInt(k) }).ToArray() }));
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Failed", false); SessionState.SetBool(Key + "Done", false); SessionState.SetInt(Key + "Checks", 0);
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
            started = false; deadline = EditorApplication.timeSinceStartup + 1200;
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
                File.WriteAllText(Root + "/result.txt", (failed ? "FAIL" : "PASS") + " checks=" + SessionState.GetInt(Key + "Checks", 0));
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
            if (!value) throw new Exception("ORAL_SWALLOW_SPIRAL_FAIL: " + message);
            SessionState.SetInt(Key + "Checks", SessionState.GetInt(Key + "Checks", 0) + 1);
            File.AppendAllText(Root + "/checks.txt", "PASS " + message + "\n"); Debug.Log("ORAL_SWALLOW_SPIRAL_PASS " + message);
        }
        static FieldInfo Field(object owner, string name)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            { var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f; }
            throw new MissingFieldException(owner.GetType().Name, name);
        }
        static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
        static object Call(object owner, string name, params object[] args)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            { var m = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); if (m != null) return m.Invoke(owner, args); }
            throw new MissingMethodException(owner.GetType().Name, name);
        }
        static bool Flag(object owner, string name) => (bool)owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(owner);
        static int Counter(object owner, string name) => Convert.ToInt32(owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(owner));
        static void CloseDialogs(LevelManager level)
        {
            if (level.EntityManager.AbilitySelectionDialog.MenuOpen) { level.EntityManager.AbilitySelectionDialog.Close(); Time.timeScale = 1; }
        }
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
        static IEnumerator Wait(LevelManager level, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { CloseDialogs(level); yield return null; }
        }
        static Monster Spawn(LevelManager level, Vector2 p)
        {
            var monster = level.EntityManager.SpawnMonster(normalPool, p, normalBlueprint, 0, false);
            if (monster != null) Set(monster, "currentHealth", 100000f);
            return monster;
        }
        static void Reset(LevelManager level, OralSwallowFinale finale)
        {
            finale.CancelForTransition(); finale.Configure(OralStageRuntime.Instance); Set(level, "levelTime", 0f);
            level.SetRunFlowPaused(false); Time.timeScale = 1; Clear(level);
            var player = level.PlayerCharacter; Set(player, "isInvincible", true); Set(player, "currentHealth", player.MaxHealth);
            Place(player, fixtureCenter + Vector2.down * 6);
        }
        static OralWhirlpoolState Isolate(OralSwallowFinale finale, float radius = 4)
        {
            var center = OralMapGeometry.TongueCenter(FiniteStomachMap.Instance);
            for (int i = 0; i < finale.Whirlpools.Count; i++)
            {
                var w = finale.Whirlpools[i]; w.Radius = .03f; w.Position = center + new Vector2(8 + (i % 5) * .15f, 5 + i / 5f * .1f);
                Set(w, "Direction", Vector2.zero); Set(w, "TurnRemaining", 999f);
            }
            var main = finale.Whirlpools[0]; main.Position = fixtureCenter; main.Radius = radius;
            return main;
        }
        static void BeginIsolated(OralSwallowFinale finale)
        {
            Check(finale.StartForTesting(), "isolated mechanics fixture starts the production finale");
            Set(finale, "age", 19.8f); Isolate(finale); Set(finale, "age", 20.01f);
        }
        static Vector2 FindOpenFixtureCenter(FiniteStomachMap map)
        {
            Vector2 center = OralMapGeometry.TongueCenter(map);
            int layer = LayerMask.GetMask(BloodClotObstacle.LayerName);
            for (int y = -2; y <= 3; y++) for (int x = -5; x <= 5; x++)
            {
                Vector2 p = center + new Vector2(x, y);
                if (OralMapGeometry.TongueDistance(map, p, out _) > 5 && Physics2D.OverlapCircle(p, 4.7f, layer) == null) return p;
            }
            throw new Exception("No blood-clot-free spiral fixture center");
        }
        static void Sample(string label, LevelManager level, OralSwallowFinale finale, Vector2 p, Vector2 origin, float elapsed)
        {
            var offset = p - origin;
            File.AppendAllText(Root + "/samples.csv", label + "," + F(finale.Age) + "," + F(elapsed) + "," + F(offset.magnitude) + "," + F(Mathf.Atan2(offset.y, offset.x)) + "," + F(level.PlayerCharacter.CurrentHealth) + "," + level.EntityManager.LivingMonsters.Count + "\n");
        }
        static IEnumerator Checks(LevelManager level)
        {
            var preferences = GamePreferences.Current.Copy(); preferences.pauseOnFocusLoss = false; preferences.muteOnFocusLoss = false; GamePreferences.Apply(preferences, false);
            Application.runInBackground = true; Time.timeScale = 1; RunScore.Exclude("Swallow spiral mechanics acceptance");
            var progression = StageProgression.Ensure(level);
            Check(progression.BeginDirectOral(true), "real stage-zero entry starts");
            float until = Time.realtimeSinceStartup + 90;
            while ((OralStageRuntime.Instance == null || progression.Travelling) && Time.realtimeSinceStartup < until) yield return null;
            Check(OralStageRuntime.Instance != null && !progression.Travelling, "real stage-zero loading completes");
            var stage = OralStageRuntime.Instance; var events = OralFieldEvents.Instance; var finale = OralSwallowFinale.Instance;
            var player = level.PlayerCharacter; var map = FiniteStomachMap.Instance;
            events.AutoSchedule = false; foreach (var c in events.Cavities) { c.Healed = true; c.Active = false; }
            Set(player, "isInvincible", true); Set(player, "healOnIdlePerSecond", 0f);
            var group = level.CurrentLevelBlueprint.monsters.Select((g, i) => new { g, i }).First(x => x.g.monsterBlueprints.Any(b => b != null && OralMonsterRoster.IsBlueprint(b)));
            normalPool = group.i; normalBlueprint = group.g.monsterBlueprints.First(b => b != null && OralMonsterRoster.IsBlueprint(b));
            fixtureCenter = FindOpenFixtureCenter(map); Clear(level); Place(player, fixtureCenter + Vector2.down * 6);
            yield return SpawnCutoff(level, finale);
            yield return SpiralAndEscape(level, finale);
            yield return PlayerDamage(level, finale);
            yield return ReleaseAndTransfer(level, finale, progression);
            SessionState.SetBool(Key + "Done", true);
        }
        static IEnumerator SpawnCutoff(LevelManager level, OralSwallowFinale finale)
        {
            Reset(level, finale); Check(finale.StartForTesting(), "spawn-cutoff encounter starts"); Set(finale, "age", 19.8f);
            var before = Spawn(level, fixtureCenter + Vector2.left * 5);
            Check(before != null, "normal monster spawn remains allowed immediately before vortex activation");
            var sugarSource = level.CurrentLevelBlueprint.monsters.SelectMany((g, i) => g.monsterBlueprints.OfType<SugarCubeMonsterBlueprint>().Where(b => b.CanSplit()).Select(b => new { i, b })).FirstOrDefault();
            Check(sugarSource != null, "real splitting-sugar blueprint is available for cutoff regression");
            var sugar = level.EntityManager.SpawnMonster(sugarSource.i, fixtureCenter + Vector2.up * 5, sugarSource.b, 0, false) as SugarCubeMonster;
            Check(sugar != null, "pre-cutoff sugar parent is spawned through the production pool");
            Set(finale, "age", 20.01f);
            Check(EntityManager.OralFinaleSuppressesMonsterSpawns, "spawn gate turns on at age twenty");
            Check(Spawn(level, fixtureCenter) == null, "direct normal spawn is blocked");
            Check(level.EntityManager.SpawnMonster(normalPool, fixtureCenter, normalBlueprint, 0, true) == null, "mini-stage override cannot bypass finale spawn gate");
            Check(level.EntityManager.SpawnMonsterRandomPosition(normalPool, normalBlueprint, 0, false) == null, "random field spawn is blocked");
            Check(level.EntityManager.SpawnMonsterRandomPosition(normalPool, normalBlueprint, 0, true) == null, "random override spawn is blocked");
            var host = Object.FindObjectOfType<TimedSpecialMonsterSpawner>();
            var enzyme = ((List<DigestiveEnzymeFieldSpawner>)Field(host, "enzymeSpawns").GetValue(host)).First();
            int enzymes = Object.FindObjectsOfType<DigestiveEnzymeMonster>().Length;
            Check(!(bool)Call(enzyme, "CanSpawnNow"), "direct-instantiation enzyme scheduler is blocked"); Call(enzyme, "SpawnOne");
            Check(Object.FindObjectsOfType<DigestiveEnzymeMonster>().Length == enzymes, "forced enzyme creation cannot bypass the cutoff");
            int clots = Object.FindObjectsOfType<BloodClotMonster>().Length;
            foreach (var clot in (List<BloodClotFieldSpawner>)Field(host, "bloodClotSpawns").GetValue(host)) Call(clot, "SpawnBloodClot");
            Check(Object.FindObjectsOfType<BloodClotMonster>().Length == clots, "blood-clot spawn entry is blocked without creating a new actor");
            var vending = Object.FindObjectsOfType<EliteMonsterSummonInteractable>();
            Check(vending.Length > 0 && vending.All(v => !(bool)Call(v, "ExecuteInteraction", level.PlayerCharacter)), "existing elite vending interactions reject spawning during survival");
            foreach (var timed in (List<TimedSpecialSpawnSchedule>)Field(host, "timedSpawns").GetValue(host)) Call(timed, "OnTick");
            foreach (var elite in (List<EliteMonsterSpawner>)Field(host, "eliteSpawns").GetValue(host)) Call(elite, "OnTick");
            int count = level.EntityManager.LivingMonsters.Count;
            Call(sugar, "TrySpawnSplitChildren", true);
            Check(level.EntityManager.LivingMonsters.Count == count, "existing sugar parent cannot spawn children after the cutoff");
            int normalCount = level.NormalMonstersSpawned;
            yield return Wait(level, 1.7f);
            Check(level.NormalMonstersSpawned == normalCount && level.EntityManager.LivingMonsters.Count <= count, "survival no longer adds normal waves or delayed children");
            Reset(level, finale); yield return null;
            Check(!EntityManager.OralFinaleSuppressesMonsterSpawns && Spawn(level, fixtureCenter) != null, "cancel releases the field spawn gate");
        }
        static IEnumerator SpiralAndEscape(LevelManager level, OralSwallowFinale finale)
        {
            Reset(level, finale); var player = level.PlayerCharacter;
            var monster = Spawn(level, fixtureCenter + Vector2.left * 3.3f);
            var armor = monster.GetComponent<OralTartarArmor>() ?? monster.gameObject.AddComponent<OralTartarArmor>(); armor.Arm(monster);
            var barrier = monster.GetComponent<MonsterNeedleBarrier>() ?? monster.gameObject.AddComponent<MonsterNeedleBarrier>(); barrier.Arm();
            float shieldedHp = monster.HP; monster.TakeDamage(1000);
            Check(monster.HP == shieldedHp && barrier.IsProtected && armor.IsProtected, "monster fixture has both active damage-blocking shields");
            int deaths = 0, playerKillCallbacks = 0; Action<Monster> onDeath = m => { if (m == monster) deaths++; }; Monster.Died += onDeath;
            monster.OnKilled.AddListener(m => playerKillCallbacks++);
            BeginIsolated(finale); Place(player, fixtureCenter + Vector2.right * 3.3f); player.Move(Vector2.right);
            Vector2 playerStart = player.Position - fixtureCenter, monsterStart = monster.Position - fixtureCenter;
            float began = Time.time;
            yield return Wait(level, .65f);
            Vector2 playerDelta = player.Position - fixtureCenter, monsterDelta = monster.Position - fixtureCenter;
            Sample("player-spiral", level, finale, player.Position, fixtureCenter, Time.time - began);
            Sample("monster-spiral", level, finale, monster.Position, fixtureCenter, Time.time - began);
            Check(playerDelta.magnitude < playerStart.magnitude - .15f && Mathf.Abs(Vector2.SignedAngle(playerStart, playerDelta)) > 7, "player moves inward with angular rotation despite outward walking input");
            Check(monsterDelta.magnitude < monsterStart.magnitude - .15f && Mathf.Abs(Vector2.SignedAngle(monsterStart, monsterDelta)) > 7, "monster moves along a spiral instead of a straight pull");
            Check(Flag(finale, "IsPlayerCaptured"), "player capture is active while inside the vortex");
            Vector2 pausedPlayer = player.Position, pausedMonster = monster.Position; float pausedAge = finale.Age;
            Time.timeScale = 0; yield return new WaitForSecondsRealtime(.25f);
            File.AppendAllText(Root + "/checks.txt", "PAUSE_DIAGNOSTIC player=" + F(Vector2.Distance(player.Position, pausedPlayer)) + " monster=" + F(Vector2.Distance(monster.Position, pausedMonster)) + " age=" + F(finale.Age - pausedAge) + "\n");
            Check(Vector2.Distance(player.Position, pausedPlayer) < .001f && Vector2.Distance(monster.Position, pausedMonster) < .001f && Mathf.Abs(finale.Age - pausedAge) < .001f, "pause freezes captured actors and event time"); Time.timeScale = 1;
            Set(player, "currentDashCharges", player.MaxDashCharges); int charges = player.CurrentDashCharges;
            player.Move(Vector2.right); player.TryDash(); int used = charges - player.CurrentDashCharges; player.Move(Vector2.zero);
            Check(player.IsDashing && used == 1, "captured player can start one real dash while walking stays locked");
            float until = Time.realtimeSinceStartup + 5;
            while (player.IsDashing && Time.realtimeSinceStartup < until) { CloseDialogs(level); yield return null; }
            Check(!player.IsDashing && finale.Whirlpools.All(w => Vector2.Distance(player.Position, w.Position) > w.Radius), "one dash exits every overlapping vortex");
            Check(!Flag(finale, "IsPlayerCaptured"), "dash completion releases player capture");
            Place(player, fixtureCenter + Vector2.down * 6);
            until = Time.realtimeSinceStartup + 12;
            while (monster != null && monster.IsAlive && Time.realtimeSinceStartup < until) { CloseDialogs(level); yield return null; }
            Check(monster == null || !monster.IsAlive || !monster.gameObject.activeInHierarchy, "shielded monster is removed on arrival at the vortex center");
            yield return Wait(level, .3f);
            File.AppendAllText(Root + "/checks.txt", "DIAGNOSTIC center monster death events=" + deaths + "\n");
            Check(deaths == 1 && playerKillCallbacks == 0, "center removal emits one environmental death and no player-kill callback");
            Check(finale.SwallowedMonsters == 1 && Object.FindObjectsOfType<Collectable>().Length == 0, "environmental swallowing removes exactly one fixture monster without loot rewards");
            Monster.Died -= onDeath;
        }
        static IEnumerator PlayerDamage(LevelManager level, OralSwallowFinale finale)
        {
            Reset(level, finale); var player = level.PlayerCharacter;
            player.AddMaxHealthBonus(20f); float max = player.MaxHealth; Set(player, "currentHealth", max); Set(player, "isInvincible", false);
            BeginIsolated(finale); Place(player, fixtureCenter);
            float until = Time.realtimeSinceStartup + 3;
            while (player.CurrentHealth >= max - .01f && Time.realtimeSinceStartup < until) yield return null;
            float firstAt = Time.time, firstHealth = player.CurrentHealth;
            Check(Mathf.Abs(firstHealth - max * .75f) < .05f, "first center arrival immediately deals twenty-five percent of current max health");
            Vector2 pinned = player.Position; player.Move(Vector2.right); yield return Wait(level, .3f);
            Check(Vector2.Distance(player.Position, pinned) < .2f, "walking input cannot leave the vortex center");
            level.SetRunFlowPaused(true); float pausedHealth = player.CurrentHealth, pausedAge = finale.Age, pauseBegan = Time.time;
            yield return new WaitForSecondsRealtime(.3f);
            Check(Mathf.Abs(player.CurrentHealth - pausedHealth) < .001f && Mathf.Abs(finale.Age - pausedAge) < .001f, "run-flow pause freezes center damage and timer"); level.SetRunFlowPaused(false);
            float pausedDuration = Time.time - pauseBegan; Set(player, "isInvincible", true);
            // Move the real source against a real solid footprint, then move it back.
            // This displaces the captured actor outside the core without replacing its capture.
            var source = finale.Whirlpools[0]; Vector2 originalCenter = source.Position;
            var blocker = new GameObject("QA temporary blood-clot footprint"); blocker.transform.position = originalCenter + Vector2.right * .8f;
            var shape = blocker.AddComponent<CircleCollider2D>(); shape.radius = .5f; shape.isTrigger = true;
            BloodClotObstacle.Ensure(blocker); Physics2D.SyncTransforms(); source.Position = (Vector2)blocker.transform.position;
            yield return Wait(level, .2f);
            Check(!finale.PlayerAtCenter && finale.IsPlayerCaptured, "moving vortex against a solid obstacle temporarily moves the same capture outside its core");
            source.Position = originalCenter; Object.Destroy(blocker);
            until = Time.realtimeSinceStartup + 2;
            while (!finale.PlayerAtCenter && Time.realtimeSinceStartup < until) yield return null;
            Check(finale.PlayerAtCenter && Mathf.Abs(player.CurrentHealth - firstHealth) < .05f, "returning to the same core does not trigger an early duplicate damage pulse");
            while (Time.time - firstAt - pausedDuration < 1.28f) yield return null;
            Check(Mathf.Abs(player.CurrentHealth - firstHealth) < .05f, "center damage does not repeat before the one-and-a-half-second interval");
            until = Time.realtimeSinceStartup + 3;
            while (Mathf.Abs(player.CurrentHealth - firstHealth) < .05f && Time.realtimeSinceStartup < until) yield return null;
            float elapsed = Time.time - firstAt - pausedDuration;
            Check(Mathf.Abs(player.CurrentHealth - max * .5f) < .05f, "second center tick uses max-health percentage rather than remaining health");
            Check(elapsed >= 1.45f && elapsed < 1.95f, "second center damage tick occurs after 1.5 active seconds including paused time exclusion");
            Sample("center-damage", level, finale, player.Position, fixtureCenter, elapsed);
            finale.CancelForTransition(); Set(player, "isInvincible", true); float after = player.CurrentHealth;
            Check(!Flag(finale, "IsPlayerCaptured"), "cancel immediately releases player movement lock");
            player.Move(Vector2.right); Vector2 origin = player.Position; yield return Wait(level, .7f); player.Move(Vector2.zero);
            Check(Vector2.Distance(player.Position, origin) > .2f && Mathf.Abs(player.CurrentHealth - after) < .05f, "cancel restores walking and stops pending center damage");
        }
        static IEnumerator ReleaseAndTransfer(LevelManager level, OralSwallowFinale finale, StageProgression progression)
        {
            Reset(level, finale); var player = level.PlayerCharacter;
            var monster = Spawn(level, fixtureCenter + Vector2.left * 3.5f);
            BeginIsolated(finale); yield return Wait(level, .15f);
            Check(monster.IsFieldRuntimeSuspended, "captured monster ordinary AI is suspended");
            finale.CancelForTransition();
            Check(!monster.IsFieldRuntimeSuspended && monster.GetComponent<Rigidbody2D>().simulated, "cancel releases captured monster AI and physics");
            Reset(level, finale); monster = Spawn(level, fixtureCenter + Vector2.left * 3.5f);
            BeginIsolated(finale); yield return Wait(level, .15f); monster.DespawnForStageTransfer();
            finale.CancelForTransition();
            Check(!monster.gameObject.activeInHierarchy, "cleanup does not reactivate a captured monster returned to its pool");
            Reset(level, finale); var stats = player.CaptureRunSceneState(); var skills = player.Skills; var relics = player.Relics;
            Check(finale.StartForTesting() && finale.Whirlpools.Count == 47, "real transition encounter retains the forty-seven-vortex layout");
            Set(finale, "age", 39.75f); Set(player, "isInvincible", true);
            float until = Time.realtimeSinceStartup + 90;
            while ((progression.StageNumber != 1 || progression.Travelling) && Time.realtimeSinceStartup < until) { CloseDialogs(level); Set(player, "isInvincible", true); yield return null; }
            Check(progression.StageNumber == 1 && !progression.Travelling, "final swallow completes the actual stage-one transition");
            Check(OralStageRuntime.Instance == null && OralSwallowFinale.Instance == null && !EntityManager.OralFinaleSuppressesMonsterSpawns, "stage-one entry clears oral capture and spawn-cutoff ownership");
            Check(!FiniteStomachMap.Instance.IsOral && !level.IsRunFlowPaused && player.GetComponent<Rigidbody2D>().simulated, "stomach map, game time and player physics resume");
            Check(level.PlayerCharacter == player && player.Skills == skills && player.Relics == relics && Mathf.Abs(player.CaptureRunSceneState().MaxHealthBonus - stats.MaxHealthBonus) < .001f, "stage transfer preserves character, growth and skill runtimes");
            var g = level.CurrentLevelBlueprint.monsters.Select((x, i) => new { x, i }).First(x => x.x.monsterBlueprints.Any(b => b != null && b.GetType() == typeof(MeleeMonsterBlueprint)));
            var bp = g.x.monsterBlueprints.First(b => b != null && b.GetType() == typeof(MeleeMonsterBlueprint));
            Check(level.EntityManager.SpawnMonster(g.i, player.Position + Vector2.left * 5, bp, 0, false) != null, "stage-one normal monster spawning is allowed again");
            Vector2 start = player.Position; float clock = level.CurrentLevelTime; player.Move(Vector2.right); yield return Wait(level, .8f); player.Move(Vector2.zero);
            Check(Vector2.Distance(player.Position, start) > .2f && level.CurrentLevelTime > clock + .5f, "player movement and stage-one clock advance after loading");
        }
        static string F(float value) => value.ToString("F4", CultureInfo.InvariantCulture);
    }
}
#endif
