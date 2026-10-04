using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class TutorialGuidePlaySmoke
    {
        private const string Key = "TutorialGuidePlaySmoke";
        private static double deadline;
        private static bool attached, failed, complete;
        static TutorialGuidePlaySmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(type).position = new Rect(0, 0, 1280, 741);
            Attach(); EditorApplication.EnterPlaymode();
        }
        private static void Attach()
        {
            deadline = EditorApplication.timeSinceStartup + 180;
            attached = failed = complete = false;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        private static void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true;
        }
        public static void Done() { complete = true; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) failed = true;
            if (failed || complete)
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key, false);
                Debug.Log("[TutorialGuideSmoke] FINISHED failed=" + failed);
                EditorApplication.Exit(failed ? 1 : 0);
                return;
            }
            if (!attached && EditorApplication.isPlaying && ApothecaryUI.Instance != null)
            {
                attached = true;
                var runner = new GameObject("Tutorial guide smoke");
                UnityEngine.Object.DontDestroyOnLoad(runner);
                runner.AddComponent<TutorialGuideSmokeRunner>();
            }
        }
    }

    public sealed class TutorialGuideSmokeRunner : MonoBehaviour
    {
        private static void Check(bool value, string name)
        {
            if (!value) throw new Exception("[TutorialGuideSmoke] FAIL " + name);
            Debug.Log("[TutorialGuideSmoke] PASS " + name);
        }
        private static void Capture(string name)
        {
            Directory.CreateDirectory("Library/TutorialGuideSmoke");
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes("Library/TutorialGuideSmoke/" + name + ".png", texture.EncodeToPNG());
            Destroy(texture);
        }
        private IEnumerator Start()
        {
            var oldCharacter = CrossSceneData.CharacterBlueprint;
            string acidKey = TutorialGuide.SeenKey("event/acid-secretion");
            string sniperKey = TutorialGuide.SeenKey("monster/sniper");
            string clotKey = TutorialGuide.SeenKey("mechanic/blood-clot");
            bool acidHad = PlayerPrefs.HasKey(acidKey), sniperHad = PlayerPrefs.HasKey(sniperKey);
            bool clotHad = PlayerPrefs.HasKey(clotKey);
            int acidValue = PlayerPrefs.GetInt(acidKey), sniperValue = PlayerPrefs.GetInt(sniperKey);
            int clotValue = PlayerPrefs.GetInt(clotKey);
            string weaponKey = null;
            bool weaponHad = false;
            int weaponValue = 0;
            try
            {
                var ui = ApothecaryUI.Instance;
                CrossSceneData.CharacterBlueprint = ui.Config.characters.First(x => x.skills != null &&
                    x.skills.kind == CharacterSkillDefinition.SkillKind.Hyuki);
                CrossSceneData.StartingLobbyItems = Array.Empty<MerchantItemBlueprint>();
                PlayerPrefs.DeleteKey(acidKey); PlayerPrefs.DeleteKey(sniperKey); PlayerPrefs.DeleteKey(clotKey);
                SceneManager.LoadScene(1);
                yield return new WaitForSecondsRealtime(2f);
                var level = FindObjectOfType<LevelManager>();
                Check(level != null && TutorialGuide.Instance != null, "guide installed in level");
                level.enabled = false;
                if (level.EntityManager.AbilitySelectionDialog.MenuOpen)
                    level.EntityManager.AbilitySelectionDialog.Close();
                Check(ApothecaryUI.Instance.Page == "hud", "HUD is visible");
                TutorialGuide.QueueStageEvent("acid-secretion");
                yield return new WaitForSecondsRealtime(.25f);
                Check(TutorialGuide.IsOpen && Time.timeScale == 0f, "first event opens and pauses");
                Check(FindObjectsOfType<Text>().Any(x => x.text == "위산분비"), "event content displayed");
                yield return new WaitForEndOfFrame();
                Capture("01-event-card");
                TutorialGuide.Instance.Close();
                yield return new WaitForSecondsRealtime(.2f);
                Check(!TutorialGuide.IsOpen && Time.timeScale > 0, "closing resumes gameplay");
                TutorialGuide.QueueStageEvent("acid-secretion");
                yield return new WaitForSecondsRealtime(.2f);
                Check(!TutorialGuide.IsOpen, "same event does not repeat");
                var sniper = AssetDatabase.LoadAssetAtPath<MonsterBlueprint>("Assets/Prefabs/Monsters/genral Monster BP/Sniper Monster.asset");
                TutorialGuide.QueueMonster(sniper);
                yield return new WaitForSecondsRealtime(.25f);
                Check(TutorialGuide.IsOpen && Time.timeScale == 0f, "different monster gets its own first discovery");
                Check(FindObjectsOfType<Text>().Any(x => x.text == "타코야키 저격수"), "monster content displayed");
                yield return new WaitForEndOfFrame();
                Capture("02-monster-card");
                TutorialGuide.Instance.Close();
                TutorialGuide.QueueBloodClot();
                yield return new WaitForSecondsRealtime(.25f);
                Check(TutorialGuide.IsOpen && FindObjectsOfType<Text>().Any(x => x.text == "혈전 과충전"),
                    "first blood clot has a dedicated overcharge card");
                Check(FindObjectsOfType<Text>().Any(x => x.text.Contains("E로 격화 난이도")),
                    "blood clot card explains harder stage and reward");
                yield return new WaitForEndOfFrame();
                Capture("03-blood-clot-card");
                TutorialGuide.Instance.Close();
                TutorialGuide.QueueBloodClot();
                yield return new WaitForSecondsRealtime(.2f);
                Check(!TutorialGuide.IsOpen, "blood clot card does not repeat");
                var abilities = FindObjectOfType<AbilityManager>();
                var weapon = abilities.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true)
                    .First(x => !x.Owned && x.RequirementsMet());
                weaponKey = TutorialGuide.SeenKey("weapon/" + weapon.Type);
                weaponHad = PlayerPrefs.HasKey(weaponKey);
                weaponValue = PlayerPrefs.GetInt(weaponKey);
                PlayerPrefs.DeleteKey(weaponKey);
                Check(abilities.AcquireVer4Ability(weapon), "new special weapon acquired through game path");
                yield return new WaitForSecondsRealtime(.25f);
                Check(TutorialGuide.IsOpen && FindObjectsOfType<Text>().Any(x => x.text == "새 무기"),
                    "weapon acquisition opens its own card");
                yield return new WaitForEndOfFrame();
                Capture("04-weapon-card");
                TutorialGuide.Instance.Close();
                TutorialGuidePlaySmoke.Done();
            }
            finally
            {
                if (acidHad) PlayerPrefs.SetInt(acidKey, acidValue); else PlayerPrefs.DeleteKey(acidKey);
                if (sniperHad) PlayerPrefs.SetInt(sniperKey, sniperValue); else PlayerPrefs.DeleteKey(sniperKey);
                if (clotHad) PlayerPrefs.SetInt(clotKey, clotValue); else PlayerPrefs.DeleteKey(clotKey);
                if (weaponKey != null)
                {
                    if (weaponHad) PlayerPrefs.SetInt(weaponKey, weaponValue);
                    else PlayerPrefs.DeleteKey(weaponKey);
                }
                PlayerPrefs.Save();
                CrossSceneData.CharacterBlueprint = oldCharacter;
                Time.timeScale = 1f;
            }
        }
    }
}
