using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class DirectorConsolidationPlaySmoke
    {
        private const string Key = "DirectorConsolidationSmoke";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static double next, deadline;
        private static LevelManager level;
        private static TimedSpecialMonsterSpawner owner;
        private static object timed;
        private static float pausedTime, pausedSpawnTime;
        private static MiniStageCollectionRoom room;
        private static Coin rentedAgain;
        static DirectorConsolidationPlaySmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            DirectorConsolidationTestRunner.RunAll();
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + "Failed", false);
            SessionState.SetInt(Key + "Phase", 0);
            Attach();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorApplication.EnterPlaymode();
        }
        private static void Attach()
        {
            next = EditorApplication.timeSinceStartup + 4;
            deadline = EditorApplication.timeSinceStartup + 150;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        private static void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) SessionState.SetBool(Key + "Failed", true);
        }
        private static object Get(object target, string field) => target.GetType().GetField(field, Hidden).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Debug.Log("[ConsolidationSmoke] PASS " + message);
        }
        private static IList Pickups => (IList)Get(room, "spawnedPickups");
        private static void CheckEventTransitions()
        {
            var events = UnityEngine.Object.FindObjectOfType<StageEventDirector>();
            object basic = ((IList)Get(events, "basicEvents"))[0];
            object advanced = ((IList)Get(events, "advancedEvents"))[0];
            foreach (var spec in new[] {
                new[] { "MonsterSurgeEvent", "UpdateMonsterSurgeEvent" },
                new[] { "GoldRushEvent", "UpdateGoldRushEvent" },
                new[] { "AcidSecretionEvent", "UpdateAcidSecretionEvent" },
                new[] { "AcidRefluxWaveEvent", "UpdateAcidRefluxWaveEvent" },
                new[] { "PeristalsisDriftEvent", "UpdatePeristalsisDriftEvent" },
                new[] { "CoffeeTransfusionEvent", "UpdateCoffeeTransfusionEvent" } })
            {
                object module = basic.GetType().GetNestedType(spec[0]) != null ? basic : advanced;
                var type = module.GetType().GetNestedType(spec[0]);
                object entry = Activator.CreateInstance(type);
                type.GetField("resolvedStartTime").SetValue(entry, 0f);
                type.GetField("enabled").SetValue(entry, false);
                var update = module.GetType().GetMethod(spec[1], Hidden);
                update.Invoke(module, new[] { entry, (object)0.1f });
                Check(!(bool)type.GetField("started").GetValue(entry), spec[0] + " remains inactive when disabled");
                type.GetField("enabled").SetValue(entry, true);
                if (type.GetField("damage") != null) type.GetField("damage").SetValue(entry, 0f);
                update.Invoke(module, new[] { entry, (object)0.1f });
                Check((bool)type.GetField("started").GetValue(entry), spec[0] + " starts through its migrated runtime");
            }
            object antacid = ((IList)Get(events, "antacidEvents"))[0];
            antacid.GetType().GetMethod("StartEvent", Hidden).Invoke(antacid, new object[] { 0f });
            Check((bool)Get(antacid, "started"), "Antacid event starts with migrated prefab references");
            antacid.GetType().GetMethod("EndEvent", Hidden).Invoke(antacid, new object[] { 0.1f });
            Check((bool)Get(antacid, "finished") && ((IList)Get(antacid, "activeBubbles")).Count == 0, "Antacid end clears its bubbles");
        }
        private static void BeginRoom(bool gold)
        {
            string path = "Assets/Prefabs/MiniStages/MiniStage" + (gold ? "Gold" : "Exp") + "TimeAttackRoom.prefab";
            room = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), new Vector3(1000, 1000), Quaternion.identity).GetComponent<MiniStageCollectionRoom>();
            Set(room, "timeLimit", 0.6f);
            Set(room, "clearDelayAfterTimeUp", 0f);
            Set(room, "dropAcrossEntireStage", false);
            Set(room, "gridAnchorMode", CollectionGridAnchorMode.RoomTransform);
            Set(room, "gridColumns", 2); Set(room, "gridRows", 2);
            Set(room, "emptyCenterRadius", 0f); Set(room, "randomJitter", 0f);
            Set(room, "spawnChancePerCell", 1f); Set(room, "useSpawnAnimation", false);
            room.InitRoom(UnityEngine.Object.FindObjectOfType<MiniStageDirector>(), level.EntityManager, level.PlayerCharacter);
            room.BeginRoom();
            Check(Pickups.Count == 4, (gold ? "Gold" : "Experience") + " uses the shared 2x2 placement loop");
            foreach (object record in Pickups)
            {
                var pickup = (Collectable)record.GetType().GetField("coin").GetValue(record);
                Check(gold ? pickup is Coin : pickup is ExpGem, "Reward adapter created the correct pooled item");
            }
        }
        private static void Tick()
        {
            int phase = SessionState.GetInt(Key + "Phase", 0);
            if (!EditorApplication.isPlaying)
            {
                if (phase == 9 && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    bool failed = SessionState.GetBool(Key + "Failed", false);
                    SessionState.EraseBool(Key);
                    Debug.Log("[ConsolidationSmoke] FINISHED Failed=" + failed);
                    EditorApplication.Exit(failed ? 1 : 0);
                }
                return;
            }
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Smoke timeout");
                Time.timeScale = 1;
                switch (phase)
                {
                    case 0:
                        level = UnityEngine.Object.FindObjectOfType<LevelManager>();
                        owner = UnityEngine.Object.FindObjectOfType<TimedSpecialMonsterSpawner>();
                        Check(level != null && owner != null && level.PlayerCharacter != null, "Real Level 1 services initialize with unified owners");
                        Check(UnityEngine.Object.FindObjectsOfType<StageEventDirector>().Length == 1, "Exactly one stage-event owner executes");
                        timed = ((IList)Get(owner, "timedSpawns"))[0];
                        Check((float)Get(timed, "elapsedTime") > 0, "Unified special schedule receives frame updates");
                        Check(!((RuntimeModule)((IList)Get(owner, "timedSpawns"))[1]).moduleEnabled, "Old inactive trap stays inactive in Play Mode");
                        level.SetRunFlowPaused(true);
                        MiniStageRuntimeState.EnterMiniStage(UnityEngine.Object.FindObjectOfType<MiniStageDirector>());
                        pausedTime = level.CurrentLevelTime;
                        pausedSpawnTime = (float)Get(timed, "elapsedTime");
                        next = EditorApplication.timeSinceStartup + 0.8;
                        break;
                    case 1:
                        Check(Mathf.Approximately(level.CurrentLevelTime, pausedTime), "Level clock pauses inside mini-stage");
                        Check(Mathf.Approximately((float)Get(timed, "elapsedTime"), pausedSpawnTime), "Special spawn clock pauses inside mini-stage");
                        BeginRoom(true);
                        var record = Pickups[0];
                        Coin first = (Coin)record.GetType().GetField("coin").GetValue(record);
                        uint generation = first.SpawnGeneration;
                        level.EntityManager.DespawnCoin(first, false);
                        rentedAgain = level.EntityManager.SpawnCoin(new Vector2(1500, 1500), CoinType.Bronze1, false);
                        Check(rentedAgain == first && rentedAgain.SpawnGeneration != generation, "Pool reuse gets a new ownership generation");
                        next = EditorApplication.timeSinceStartup + 1.0;
                        break;
                    case 2:
                        Check(room.RoomCleared && room.OptionalReturnUnlocked, "Gold timer finishes and unlocks return");
                        Check(rentedAgain.gameObject.activeInHierarchy, "Room cleanup does not remove a pickup reused by another owner");
                        room.CleanupRoom(); UnityEngine.Object.Destroy(room.gameObject);
                        level.EntityManager.DespawnCoin(rentedAgain, false);
                        BeginRoom(false);
                        next = EditorApplication.timeSinceStartup + 1.0;
                        break;
                    case 3:
                        Check(room.RoomCleared && room.OptionalReturnUnlocked, "Experience timer finishes and unlocks return");
                        Check(Pickups.Count == 0, "Experience cleanup clears owned pickup records");
                        room.CleanupRoom(); UnityEngine.Object.Destroy(room.gameObject);
                        MiniStageRuntimeState.ExitMiniStage(UnityEngine.Object.FindObjectOfType<MiniStageDirector>());
                        level.SetRunFlowPaused(false);
                        next = EditorApplication.timeSinceStartup + 0.5;
                        break;
                    case 4:
                        Check((float)Get(timed, "elapsedTime") > pausedSpawnTime, "Special scheduling resumes after mini-stage return");
                        ((RuntimeModule)timed).enabled = false;
                        pausedSpawnTime = (float)Get(timed, "elapsedTime");
                        next = EditorApplication.timeSinceStartup + 0.3;
                        break;
                    case 5:
                        Check(Mathf.Approximately((float)Get(timed, "elapsedTime"), pausedSpawnTime), "Disabling one schedule freezes its own clock");
                        ((RuntimeModule)timed).enabled = true;
                        next = EditorApplication.timeSinceStartup + 0.3;
                        break;
                    case 6:
                        Check((float)Get(timed, "elapsedTime") > pausedSpawnTime, "Re-enabling a schedule resumes without rebuilding its state");
                        CheckEventTransitions();
                        owner.DisableScheduledFinalBosses();
                        Check(owner.enabled && ((RuntimeModule)timed).moduleEnabled, "Final-boss cancellation leaves other spawns enabled");
                        foreach (RuntimeModule boss in (IList)Get(owner, "bossSpawns")) Check(!boss.moduleEnabled, "Scheduled final-boss module is cancelled");
                        SessionState.SetInt(Key + "Phase", 9);
                        EditorApplication.ExitPlaymode();
                        return;
                }
                SessionState.SetInt(Key + "Phase", phase + 1);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                SessionState.SetBool(Key + "Failed", true);
                SessionState.SetInt(Key + "Phase", 9);
                EditorApplication.ExitPlaymode();
            }
        }
    }
}
