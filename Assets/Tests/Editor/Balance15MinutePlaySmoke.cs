using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    // Run only in an isolated test copy: accelerates time and grants test-only invulnerability.
    [InitializeOnLoad]
    public static class Balance15MinutePlaySmoke
    {
        private const string Key = "Balance15Smoke";
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static double deadline, next, pauseUntil;
        private static LevelManager level;
        private static bool initialized, paused, pauseChecked, sawSignal;
        private static float pauseTime;
        private static int lastMinute = -1;
        static Balance15MinutePlaySmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            StartRun(false);
        }
        private static void StartRun(bool manual)
        {
            Balance15MinuteTestRunner.RunAll();
            SessionState.SetBool(Key + "Manual", manual);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Failed", false);
            SessionState.SetBool(Key + "Done", false);
            Attach();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorApplication.EnterPlaymode();
        }
        public static void RunManualAndCancellation()
        {
            StartRun(true);
        }
        private static void Attach()
        {
            deadline = EditorApplication.timeSinceStartup + 420;
            next = EditorApplication.timeSinceStartup + 4;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        private static void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                SessionState.SetBool(Key + "Failed", true);
        }
        private static void Check(bool ok, string description)
        {
            if (!ok) throw new Exception(description);
            Debug.Log("[Balance15Smoke] PASS " + description);
        }
        private static object Get(object target, string name) => target.GetType().GetField(name, Hidden).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (SessionState.GetBool(Key + "Done", false))
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    bool failed = SessionState.GetBool(Key + "Failed", false);
                    SessionState.SetBool(Key, false);
                    Debug.Log("[Balance15Smoke] FINISHED failed=" + failed);
                    EditorApplication.Exit(failed ? 1 : 0);
                }
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + .05;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("15 minute simulation timed out");
                if (!initialized)
                {
                    level = UnityEngine.Object.FindObjectOfType<LevelManager>();
                    Check(level != null && level.PlayerCharacter != null, "Real Level 1 initialized");
                    float originalTime = level.CurrentLevelTime;
                    foreach (float time in new[] { 0f, 420f, 899f })
                    {
                        Set(level, "levelTime", time);
                        level.SpawnMonsterByFlatIndex(0);
                        var living = level.EntityManager.LivingMonsters;
                        Check(Mathf.Abs(living.Last().HP - 25f) < .001f, "Slime HP stays 25 at " + time);
                    }
                    Set(level, "levelTime", originalTime);
                    var events = UnityEngine.Object.FindObjectOfType<StageEventDirector>();
                    int count = 0;
                    foreach (var module in (IList)Get(events, "basicEvents"))
                        foreach (string field in new[] { "monsterSurgeEvents", "goldRushEvents", "acidSecretionEvents" }) count += ((IList)Get(module, field)).Count;
                    foreach (var module in (IList)Get(events, "advancedEvents"))
                        foreach (string field in new[] { "acidRefluxWaveEvents", "peristalsisDriftEvents", "coffeeTransfusionEvents" }) count += ((IList)Get(module, field)).Count;
                    count += ((IList)Get(events, "antacidEvents")).Count;
                    Check(count == 11, "Both window tracks materialize all 11 events with real scene templates");
                    if (SessionState.GetBool(Key + "Manual", false))
                    {
                        var terminal = UnityEngine.Object.FindObjectOfType<FinalBossSummonInteractable>();
                        Check(terminal.TryAutomaticSummon(), "Can start transmission");
                        terminal.gameObject.SetActive(false);
                        Check(!FinalBossSummonInteractable.IsSummoning, "Cancellation clears pending summon");
                        terminal.gameObject.SetActive(true);
                        // Drive the actual interaction entry point with a player trigger candidate.
                        typeof(InteractableEventObject).GetMethod("OnTriggerEnter2D", Hidden).Invoke(terminal, new object[] { level.PlayerCharacter.GetComponent<Collider2D>() });
                        terminal.TryInteract();
                        Check(FinalBossSummonInteractable.IsSummoning, "Manual interaction restarts canceled transmission");
                    }
                    initialized = true;
                }
                Set(level.PlayerCharacter, "isInvincible", true);
                var dialog = UnityEngine.Object.FindObjectOfType<AbilitySelectionDialog>();
                if (dialog != null && dialog.MenuOpen)
                {
                    var choices = (List<Ability>)Get(dialog, "displayedAbilities");
                    if (choices != null && choices.Count > 0) choices[0].Select();
                    dialog.Close();
                }
                if (SessionState.GetBool(Key + "Manual", false))
                {
                    Time.timeScale = 1;
                    if (level.CurrentLevelTime > 12)
                    {
                        Check(UnityEngine.Object.FindObjectsOfType<BossController>().Length == 1, "Manual transmission finishes with one UFO");
                        SessionState.SetBool(Key + "Manual", false);
                        SessionState.SetBool(Key + "Done", true);
                        EditorApplication.ExitPlaymode();
                    }
                    return;
                }
                if (!pauseChecked && level.CurrentLevelTime >= 360)
                {
                    if (!paused)
                    {
                        paused = true; pauseTime = level.CurrentLevelTime;
                        level.SetRunFlowPaused(true); pauseUntil = EditorApplication.timeSinceStartup + 1;
                    }
                    if (EditorApplication.timeSinceStartup < pauseUntil) return;
                    Check(Mathf.Approximately(pauseTime, level.CurrentLevelTime), "Run-flow pause freezes level clock and automatic summon deadline");
                    level.SetRunFlowPaused(false); paused = false; pauseChecked = true;
                }
                int minute = (int)(level.CurrentLevelTime / 60);
                if (minute != lastMinute)
                {
                    lastMinute = minute;
                    Debug.Log("[Balance15Smoke] minute=" + minute + " population=" + level.EntityManager.LivingMonsters.Count);
                }
                if (level.CurrentLevelTime < 899)
                    CheckNoEarlyBoss();
                if (FinalBossSummonInteractable.IsSummoning) sawSignal = true;
                if (level.CurrentLevelTime >= 898 && level.CurrentLevelTime < 899)
                    UnityEngine.Object.FindObjectOfType<FinalBossSummonInteractable>().transform.position = new Vector3(1000, 1000, 0);
                Time.timeScale = level.CurrentLevelTime >= 899 ? 1 : 20;
                if (level.CurrentLevelTime >= 907)
                {
                    Check(sawSignal, "15:00 automatic summon used transmission/descent coroutine");
                    var bosses = UnityEngine.Object.FindObjectsOfType<BossController>();
                    Check(bosses.Length == 1, "Exactly one UFO boss after deadline");
                    Check(Mathf.Approximately(UnityEngine.Object.FindObjectOfType<BossPartDamageTestRootController>().TotalMaxHealth, 9000),
                        "Five active cores aggregate exactly 9000 HP");
                    var terminal = UnityEngine.Object.FindObjectOfType<FinalBossSummonInteractable>();
                    Check(!terminal.TryAutomaticSummon(), "Consumed terminal cannot summon twice");
                    Check(!FinalBossSummonInteractable.IsSummoning, "Pending summon flag cleared after descent");
                    Check(UnityEngine.Object.FindObjectOfType<FinalBossSummonInteractable>().transform.position.x < 100,
                        "Offscreen terminal is made visible for automatic transmission");
                    Time.timeScale = 1;
                    SessionState.SetBool(Key + "Done", true);
                    EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e); SessionState.SetBool(Key + "Failed", true);
                SessionState.SetBool(Key + "Done", true); EditorApplication.ExitPlaymode();
            }
        }
        private static void CheckNoEarlyBoss()
        {
            if (UnityEngine.Object.FindObjectsOfType<BossController>().Length != 0)
                throw new Exception("Boss appeared before 15 minutes without terminal interaction");
        }
    }
}
