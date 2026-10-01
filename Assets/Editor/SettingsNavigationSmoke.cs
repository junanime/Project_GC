using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vampire;

// Run with -batchmode -executeMethod SettingsNavigationSmoke.Run (without -quit).
[InitializeOnLoad]
public static class SettingsNavigationSmoke
{
    const string Key = "SettingsNavigationSmoke";
    static double deadline;
    static int step;
    static SettingsNavigationSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
    public static void Run()
    {
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
        Attach();
        EditorApplication.EnterPlaymode();
    }
    static void Attach()
    {
        deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }
    static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        Debug.Log("SETTINGS_CHECK_PASS " + label);
    }
    static object Call(ApothecaryUI ui, string method, params object[] args) =>
        typeof(ApothecaryUI).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, args);
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (!EditorApplication.isPlaying) return;
            var ui = ApothecaryUI.Instance;
            if (ui == null) return;
            if (step == 0)
            {
                var level = UnityEngine.Object.FindObjectOfType<LevelManager>();
                if (level == null || level.PlayerCharacter == null) return;
                var dialog = UnityEngine.Object.FindObjectOfType<AbilitySelectionDialog>();
                if (dialog != null && dialog.MenuOpen) dialog.Close();
                Time.timeScale = 1;
                ui.Back();
                Check(ui.Page == "settings" && ui.Tab == 3 && Time.timeScale == 0, "Escape opens paused game menu");
                Call(ui, "RequestSessionAction", "quit"); ui.Back();
                Check(ui.Page == "settings" && Time.timeScale == 0, "Escape cancels quit confirmation without resuming");
                ui.Back();
                Check(ui.Page == "hud" && Time.timeScale == 1, "Escape closes settings and resumes");
                ui.OpenRunBook();
                Check(ui.Page == "run" && Time.timeScale == 0, "Tab route still opens book");
                ui.Back();
                Check(ui.Page == "settings" && Time.timeScale == 0, "Escape from book opens settings");
                ui.Back();
                Check(ui.Page == "run" && Time.timeScale == 0, "Back restores book while paused");
                ui.CloseRunBook();
                Time.timeScale = 0; ui.OpenSettings();
                Check(ui.Page == "hud", "External pause is not stolen");
                Time.timeScale = 1; ui.OpenSettings();
                // Avoid modifying the user's progression while exercising actual scene navigation.
                var stats = UnityEngine.Object.FindObjectOfType<StatsManager>();
                if (stats != null) typeof(StatsManager).GetField("coinsGained", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stats, 0);
                Call(ui, "RequestSessionAction", "prepare");
                Call(ui, "ConfirmSessionAction");
                step = 1;
            }
            else if (step == 1 && ui.Page == "prepare")
            {
                Check(Time.timeScale == 1, "Return to preparation loads lobby and unpauses");
                ui.Back(); ui.Back();
                Check(ui.Page == "prepare", "Lobby settings returns to originating page");
                ui.OpenSettings(); Call(ui, "RequestSessionAction", "main"); Call(ui, "ConfirmSessionAction");
                Check(ui.Page == "main", "Main menu action navigates correctly");
                SessionState.SetBool(Key, false);
                Debug.Log("SETTINGS_SMOKE_PASS");
                EditorApplication.Exit(0);
            }
        }
        catch (Exception ex)
        {
            SessionState.SetBool(Key, false);
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }
}
