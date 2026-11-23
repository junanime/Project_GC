using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class CampaignPracticeSmoke
    {
        const string Id = "QA.CampaignPractice";
        const string Output = "Library/CampaignPractice.txt";
        static bool started;
        static double deadline;
        static Keyboard keyboard;
        static InputSettings oldSettings;

        static CampaignPracticeSmoke() { if (SessionState.GetBool(Id, false)) Attach(); }
        public static void Run()
        {
            SessionState.SetBool(Id, true);
            SessionState.SetString(Id+"result", "");
            SessionState.SetInt(Id+"prior", ProgressPrefs.GetInt(GStarControlPractice.CompletionKey, -1));
            File.WriteAllText(Output, "");
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            var gameView = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gameView).position = new Rect(0,0,1280,750);
            Attach();
            EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started = false;
            deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }
        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (!SessionState.GetBool(Id, false)) return;
            File.AppendAllText(Output, message+"\n"+stack+"\n");
            SessionState.SetString(Id+"result", "FAIL");
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Id, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) SessionState.SetString(Id+"result", "TIMEOUT");
            string result = SessionState.GetString(Id+"result", "");
            if (result.Length > 0)
            {
                if (EditorApplication.isPlaying)
                {
                    if (oldSettings != null) InputSystem.settings = oldSettings;
                    int prior = SessionState.GetInt(Id+"prior", -1);
                    if (prior < 0) ProgressPrefs.DeleteKey(GStarControlPractice.CompletionKey);
                    else ProgressPrefs.SetInt(GStarControlPractice.CompletionKey, prior);
                    ProgressPrefs.Save();
                    EditorApplication.ExitPlaymode();
                    return;
                }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Id, false);
                File.AppendAllText(Output, result+"\n");
                EditorApplication.Exit(result == "PASS" ? 0 : 1);
                return;
            }
            if (started || !EditorApplication.isPlaying || ApothecaryUI.Instance == null) return;
            started = true;
            var runner = new GameObject("Campaign practice QA");
            Object.DontDestroyOnLoad(runner);
            runner.AddComponent<CampaignPracticeRunner>().StartCoroutine(CheckCampaign());
        }

        static void Assert(bool condition, string what)
        {
            if (!condition) throw new Exception("Campaign practice: "+what);
            File.AppendAllText(Output, "PASS "+what+"\n");
        }
        static IEnumerator Until(Func<bool> ready, string what, int seconds = 25)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < until) yield return null;
            Assert(ready(), what);
        }
        static IEnumerator Input(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null;
            yield return null;
        }
        static IEnumerator Tap(Key key)
        {
            yield return Input(key);
            yield return Input();
        }
        static void BindKeyboard(Character actor)
        {
            keyboard = InputSystem.AddDevice<Keyboard>("Practice test keyboard");
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            actor.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Keyboard&Mouse",keyboard,mouse);
            if (!keyboard.enabled) InputSystem.EnableDevice(keyboard);
            keyboard.MakeCurrent();
        }
        static IEnumerator CheckCampaign()
        {
            ProgressPrefs.DeleteKey(GStarControlPractice.CompletionKey);
            var settings = GamePreferences.Defaults();
            settings.pauseOnFocusLoss = false;
            GamePreferences.Apply(settings,false,false);
            oldSettings = InputSystem.settings;
            var input = Object.Instantiate(oldSettings);
            input.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            input.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = input;
            Application.runInBackground = true;
            Time.captureFramerate = 30;
            var roster = ApothecaryUI.Instance.Config.characters;
            int index = 0;
            foreach (var key in new[]{"Ashi","Ari","Hyuki","Shini"})
            {
                var character = roster.First(c => OctoberArt.CharacterKey(c)==key);
                CrossSceneData.CharacterBlueprint = character;
                int destination = new[]{1,0,2,1}[index++];
                Assert(StageEntryLoading.Begin(character,null,destination,true),key+" start practice for selected stage "+destination);
                yield return Until(()=>GStarGaugeIntro.IsOpen,"gauge guide opens",90);
                var level = Object.FindObjectOfType<LevelManager>();
                Assert(RunScore.Current == null && level.IsRunFlowPaused,"practice has no ranking ticket or field clock");
                var actor = level.PlayerCharacter;
                var parent=(SyringeSpecialAugmentAbility.SpecialAugmentType)StartingNeedleSelection.DefaultId(character);
                var manager=Object.FindObjectOfType<AbilityManager>();
                Assert(Enumerable.Range(0,3).All(i=>manager.Ver4.Progress.Count(parent.ToString(),i)==1),key+" has three originals");
                BindKeyboard(actor);
                GStarGaugeIntro.Instance.Dismiss();
                yield return Until(()=>GStarControlPractice.Active,"controls active");
                var practice = GStarControlPractice.Instance;
                foreach (var direction in new[]{Key.W,Key.LeftArrow,Key.S,Key.RightArrow}) yield return Tap(direction);
                yield return Until(()=>practice.Step==1,"movement in four directions");
                foreach (var direction in new[]{Key.A,Key.D})
                {
                    yield return Until(()=>actor.CurrentDashCharges>0 && !actor.IsDashing,"dash available");
                    yield return Input(direction);
                    yield return Input(direction,Key.LeftShift);
                    yield return Input();
                    yield return Until(()=>!actor.IsDashing,"dash lands");
                }
                yield return Until(()=>practice.Step==2 && practice.Target!=null && actor.Skills.CanActivate,"target can take active skill");
                yield return Tap(Key.Space);
                if (actor.Skills.IsAri)
                {
                    yield return Input(Key.D);
                    yield return Input(Key.D,Key.LeftShift);
                    yield return Input();
                }
                yield return Until(()=>practice.PracticingAugment && level.EntityManager.AbilitySelectionDialog.MenuOpen,"skill defeated practice target");
                yield return new WaitForSecondsRealtime(.8f);
                yield return Tap(Key.R);
                yield return new WaitForSecondsRealtime(.8f);
                yield return Tap(Key.Digit2);
                yield return Until(()=>practice.Step==3,"reroll and select");
                yield return Tap(Key.Tab);
                yield return Until(()=>practice.Step==4,"journal opens");
                yield return Tap(Key.Tab);
                yield return Until(()=>practice.Step==6,"impact and chest ready");
                yield return Input(Key.D);
                yield return Until(()=>practice.CrateInRange,"walk to chest");
                yield return Input(Key.E);
                yield return Input();
                yield return Until(()=>practice.Step==8,"heart has flown into inventory");
                yield return Tap(Key.Digit2);
                yield return Until(()=>practice.Step==10,"heart used and departure ready");
                Assert(!practice.Complete && RunScore.Current==null, "fresh input required at departure");
                yield return Tap(Key.Space);
                yield return Until(()=>GStarControlPractice.HasCompleted && !GStarControlPractice.Active &&
                    !StageEntryLoading.IsLoading && RunScore.Current!=null && Object.FindObjectOfType<LevelManager>()?.IsRunFlowPaused==false,
                    key+" opens real field",90);
                level=Object.FindObjectOfType<LevelManager>();
                Assert(StageProgression.Ensure(level).StageNumber==destination && actor!=level.PlayerCharacter,
                    "fresh actor in stage "+destination);
                TutorialGuide.QueueBloodClot();
                yield return null;
                Assert(!TutorialGuide.IsOpen && TutorialGuide.Instance.Review("mechanic/blood-clot"),"manual field journal without interruptions");
                TutorialGuide.Instance.Close();
                InputSystem.RemoveDevice(keyboard);
                Time.timeScale=1;
                SceneManager.LoadScene(0);
                yield return Until(()=>ApothecaryUI.Instance!=null && ApothecaryUI.Instance.Page=="main", "returned to real title",90);
            }
            Time.captureFramerate = 0;
            SessionState.SetString(Id+"result","PASS");
        }
    }

    public sealed class CampaignPracticeRunner : MonoBehaviour {}
}
