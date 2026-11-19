using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class FieldDayControlsSmoke
    {
        const string KeyName = "FieldDayControlsQA";
        const string Output = "Library/GStarUIQA/field-day";
        static bool started;
        static double deadline;
        static Keyboard keyboard;
        static int frame;
        static FieldDayControlsSmoke() { if (SessionState.GetBool(KeyName,false)) Attach(); }

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"/report.txt","");
            SessionState.SetBool(KeyName,true);
            SessionState.SetString(KeyName+"result","");
            SessionState.SetString(KeyName+"data",Path.GetFullPath(Output+"/isolated-"+Guid.NewGuid().ToString("N")));
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position = new Rect(0,0,1280,750);
            Attach();
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if (!SessionState.GetBool(KeyName,false)) return;
            GStarDemo.TestOverride = true;
            GStarDemo.TestDataDirectory = SessionState.GetString(KeyName+"data","");
            GStarSession.TestDirectory = GStarLeaderboard.TestDirectory = GStarDemo.TestDataDirectory;
            GStarSession.ReloadForTests(); GStarLeaderboard.ReloadForTests();
        }

        static void Attach()
        {
            deadline = EditorApplication.timeSinceStartup+480;
            started = false;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string msg,string stack,LogType type)
        {
            if (type!=LogType.Assert && type!=LogType.Exception && type!=LogType.Error) return;
            if (!SessionState.GetBool(KeyName,false)) return;
            File.AppendAllText(Output+"/report.txt",msg+"\n"+stack+"\n");
            SessionState.SetString(KeyName+"result","FAIL");
        }
        static void Tick()
        {
            if (!SessionState.GetBool(KeyName,false)) return;
            if (EditorApplication.timeSinceStartup>deadline) SessionState.SetString(KeyName+"result","TIMEOUT");
            var result = SessionState.GetString(KeyName+"result","");
            if (result.Length>0)
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(KeyName,false);
                File.WriteAllText(Output+"/result.txt",result);
                EditorApplication.Exit(result=="PASS"?0:1);
                return;
            }
            if (started || !EditorApplication.isPlaying || ApothecaryUI.Instance==null) return;
            started=true;
            var host=new GameObject("G-Star practice QA");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<GStarUITestHost>().StartCoroutine(Drive());
        }
        static void Check(bool ok,string message)
        {
            if (!ok) throw new Exception("Field day booth QA: "+message);
            File.AppendAllText(Output+"/report.txt","PASS "+message+"\n");
        }
        static IEnumerator Wait(Func<bool> condition,string label,int max=90)
        {
            float limit = Time.realtimeSinceStartup+max;
            while (!condition() && Time.realtimeSinceStartup<limit) yield return null;
            Check(condition(),label);
        }
        static void Capture(string id) => GStarUISmoke.Capture("field-day/"+id,960,540);
        static IEnumerator Film(int count)
        {
            for (int i=0;i<count;i++)
            {
                yield return null;
                Capture($"practice-{frame++:000}");
                yield return null; // Two 20 fps simulation frames per capture, independent of disk latency.
            }
        }
        static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        static Button FindButton(string title) => ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.name==title);

        static IEnumerator Drive()
        {
            Application.runInBackground=true;
            var prefs=GamePreferences.Current.Copy();
            prefs.pauseOnFocusLoss=false; prefs.muteOnFocusLoss=false; prefs.keyboard=KeyboardBindingValues.Defaults();
            GamePreferences.Apply(prefs,false);
            yield return Wait(()=>ApothecaryUI.Instance.Page=="main","new visitor sees title");
            ApothecaryUI.Instance.Show("prepare");
            yield return null;
            var options = Environment.GetCommandLineArgs();
            if (!options.Contains("-skipMovieProof") || options.Contains("-movieProofOnly"))
                yield return PreparationSkillMovieSmoke.CheckAll();
            if (options.Contains("-movieProofOnly"))
            {
                SessionState.SetString(KeyName+"result","PASS");
                yield break;
            }
            Check(GStarSession.RemainingAttempts==2,"fresh participant receives two attempts");
            ApothecaryUI.Instance.StartRun();
            yield return Wait(()=>GStarControlPractice.Active,"first departure starts stomach control practice");
            var practice=GStarControlPractice.Instance;
            var level=Object.FindObjectOfType<LevelManager>();
            var rehearsalPlayer=level.PlayerCharacter;
            int originalId=rehearsalPlayer.GetInstanceID();
            Check(StageProgression.Ensure(level).StageNumber==1&&level.IsRunFlowPaused&&!GStarAnalytics.IsRecording&&GStarSession.PendingAttempt,"practice has no real combat or scoring");
            keyboard=InputSystem.AddDevice<Keyboard>("Booth test keyboard");
            var controls=rehearsalPlayer.GetComponent<PlayerInput>();
            if(controls!=null && controls.user.valid) InputUser.PerformPairingWithDevice(keyboard,controls.user);
            keyboard.MakeCurrent();
            Time.captureFramerate=20;
            yield return Film(3);
            Keys(Key.D);
            yield return Wait(()=>practice.Step==1,"real D input moves the character",8);
            yield return Film(3);
            Keys(Key.D,Key.LeftShift);
            yield return Wait(()=>practice.Step==2,"real Shift input starts dash",5);
            yield return Film(4);
            Keys();
            yield return Wait(()=>rehearsalPlayer.Skills.CanActivate,"dash completes and skill becomes available",5);
            yield return Film(2);
            Keys(Key.Space);
            yield return Wait(()=>practice.Step==3,"real Space input activates the selected skill",5);
            Keys();
            for(int i=0;i<13 && GStarControlPractice.Active;i++) yield return Film(1);
            Time.captureFramerate=0;
            yield return Wait(()=>GStarSession.AttemptStarted && !StageEntryLoading.IsLoading && !GStarControlPractice.Active,"first scored run starts");
            level=Object.FindObjectOfType<LevelManager>();
            Check(level.PlayerCharacter.GetInstanceID()!=originalId && !level.PlayerCharacter.Skills.Active&&level.PlayerCharacter.Skills.CooldownRemaining<=0,"fresh player has no rehearsal cooldown");
            Check(StageProgression.Ensure(level).StageNumber==0&&GStarSession.StageId==0&&GStarSession.RemainingAttempts==1,"stage zero is the first scored attempt");
            TutorialGuide.QueueStageEvent("acid-secretion"); TutorialGuide.QueueBloodClot();
            yield return new WaitForSecondsRealtime(.5f);
            Check(!TutorialGuide.IsOpen && Time.timeScale>0 && GStarAnalytics.IsRecording,"discoveries never pause the booth run");
            Capture("first-oral-field");
            yield return GStarCombatFeedbackSmoke.CheckField(level);
            level.GameOver();
            yield return Wait(()=>ApothecaryUI.Instance.Page=="result","actual end callback opens score panel");
            FindButton("GStar result next").onClick.Invoke();
            yield return null;
            Check(ApothecaryUI.Instance.Page=="gstarNext"&&GStarSession.RemainingAttempts==1,"all stages available for second departure");
            Capture("second-challenge");
            FindButton("GStar challenge 1").onClick.Invoke();
            yield return Wait(()=>GStarSession.AttemptStarted&&!StageEntryLoading.IsLoading,"second departure loads selected field directly");
            level=Object.FindObjectOfType<LevelManager>();
            Check(StageProgression.Ensure(level).StageNumber==1&&!GStarControlPractice.Active&&GStarSession.RemainingAttempts==0,"second choice is stage one without repeating practice");
            level.GameOver();
            Check(!GStarSession.TryBeginAttempt(0,out _),"third scored departure is blocked");
            InputSystem.RemoveDevice(keyboard);
            SessionState.SetString(KeyName+"result","PASS");
        }
    }
}
