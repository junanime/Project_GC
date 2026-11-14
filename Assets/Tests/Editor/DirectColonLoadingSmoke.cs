using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class DirectColonLoadingSmoke
    {
        const string Key="DirectColonLoadingSmoke";
        static bool started;
        static double deadline;
        static int checks;
        static DirectColonLoadingSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+300;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string message,string trace,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);
            if(message.Contains("Base BGM START | Ingame")&&StageEntryLoading.IsLoading)
            {Debug.LogError("COLON_LOAD_FAIL gameplay music started behind loading");}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log("COLON_LOAD_FINISHED failed="+failed+" checks="+checks);EditorApplication.Exit(failed?1:0);return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {
                started=true;
                var host=new GameObject("Direct colon regression").AddComponent<StageEntryLoadingTestHost>();
                Object.DontDestroyOnLoad(host);host.StartCoroutine(Checks());
            }
        }
        static void Check(bool ok,string message)
        {if(!ok)throw new Exception("COLON_LOAD_FAIL "+message);checks++;Debug.Log("COLON_LOAD_PASS "+message);}
        static IEnumerator Checks()
        {
            Application.runInBackground=true;
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            var bp=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").characters.First();
            // Two fresh departures catch stale singleton/pause state as well as first-entry bugs.
            for(int repeat=0;repeat<2;repeat++)
            {
                CrossSceneData.CharacterBlueprint=bp;
                Check(StageEntryLoading.Begin(bp,stage:2),"direct stage-two departure accepted");
                var initial=StageEntryLoading.Instance;float last=0;bool colonPrepared=false;
                while(StageEntryLoading.IsLoading)
                {
                    CheckFrame(initial,last);
                    last=initial.Progress;
                    var level=Object.FindObjectOfType<LevelManager>();
                    if(level!=null&&StageProgression.Ensure(level).StageNumber==2)
                    {
                        colonPrepared=true;
                        if(level.CurrentLevelTime!=0)throw new Exception("Timer ran behind loading");
                    }
                    yield return null;
                }
                Check(colonPrepared&&last>.99f,"same overlay reaches 100% after colon preparation");
                var manager=Object.FindObjectOfType<LevelManager>();var flow=StageProgression.Ensure(manager);
                Check(manager.InitialFieldObjectsReady&&FiniteStomachMap.Instance.IsColon,"colon field is fully placed before reveal");
                bool opening=false,eject=false;
                while(flow.Travelling)
                {
                    if(StageEntryLoading.IsLoading)throw new Exception("Second loading appeared during inlet animation");
                    if(manager.CurrentLevelTime!=0)throw new Exception("Timer ran during inlet animation");
                    opening|=flow.TransitionPhase=="opening";eject|=flow.TransitionPhase=="eject";
                    yield return null;
                }
                Check(opening&&eject,"inlet opening and character ejection preserved");
                Check(Time.timeScale==1&&!manager.IsRunFlowPaused&&StageEntryLoading.Instance==null,"loading and pause owners released");
                yield return null;yield return null;
                Check(manager.CurrentLevelTime>0,"combat timer starts after entrance");
                var back=SceneManager.LoadSceneAsync(0);while(!back.isDone)yield return null;yield return null;
            }
            // Inter-stage transfers still create an independent 0..100 overlay.
            var transfer=StageEntryLoading.CreateStageTransfer(bp);
            transfer.SetTransferProgress(.4f);Check(Mathf.Abs(transfer.Progress-.4f)<.001f,"ordinary transfer progress unchanged");
            transfer.SetTransferProgress(1);transfer.CompleteStageTransfer();yield return null;
            SessionState.SetBool(Key+"Done",true);
        }
        static void CheckFrame(StageEntryLoading expected,float previous)
        {
            if(StageEntryLoading.Instance!=expected||Object.FindObjectsOfType<StageEntryLoading>().Length!=1)
                throw new Exception("Duplicate or replaced loading overlay");
            if(expected.Progress<previous-.0001f)throw new Exception("Loading progress went backwards");
        }
    }
}
