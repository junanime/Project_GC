using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
            SessionState.SetString(Key+"Data",Path.GetFullPath("Library/DirectColonLoadingQA/data-"+Guid.NewGuid().ToString("N")));
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            Attach();EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if(!SessionState.GetBool(Key,false))return;
            GStarDemo.TestOverride=true;GStarDemo.TestDataDirectory=SessionState.GetString(Key+"Data","");
            GStarSession.TestDirectory=GStarDemo.TestDataDirectory;GStarSession.ReloadForTests();
            GStarLeaderboard.TestDirectory=GStarDemo.TestDataDirectory;GStarLeaderboard.ReloadForTests();
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
                Object.DontDestroyOnLoad(host);host.StartCoroutine(Environment.GetCommandLineArgs().Contains("-timeFieldEntries") ? MeasureEntries() : Checks());
            }
        }
        static void Check(bool ok,string message)
        {if(!ok)throw new Exception("COLON_LOAD_FAIL "+message);checks++;Debug.Log("COLON_LOAD_PASS "+message);}
        // Frame observations are upper bounds. sceneLoaded is recorded at its actual
        // callback, before Start; these probes do not create progression or field objects.
        sealed class EntryTrace
        {
            readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            readonly Dictionary<string,double> times = new Dictionary<string,double>();
            readonly int stage, sample;
            public double Elapsed => clock.Elapsed.TotalSeconds;
            public EntryTrace(int stage,int sample) { this.stage=stage; this.sample=sample; }
            public void Mark(string phase)
            {
                if (!times.ContainsKey(phase)) times.Add(phase,Elapsed);
            }
            public void Rendered()
            {
                if (StageEntryLoading.IsLoading) Mark("firstCanvas");
            }
            public void Loaded(Scene scene,LoadSceneMode mode)
            {
                if (scene.buildIndex==1) Mark("sceneLoadedCallback");
            }
            public void Observe(LevelManager level)
            {
                var overlay=StageEntryLoading.Instance;
                if (overlay!=null && overlay.Progress>0) Mark("progressMoving");
                if (level==null) return;
                Mark("levelObserved");
                var flow=level.GetComponent<StageProgression>();
                if(flow==null) return;
                if(flow.Travelling) Mark("transferStarted");
                if(flow.StageNumber!=stage) return;
                Mark("targetStage");
                var layout=level.GetComponent<FieldObjectLayout>();
                if(layout!=null && layout.IsPlacing) Mark("layoutBuilding");
                if(layout!=null && !layout.IsPlacing && level.InitialFieldObjectsReady) Mark("layoutReady");
                if(stage==0 && OralStageRuntime.Instance!=null && OralStageRuntime.Instance.Ready) Mark("oralRuntimeReady");
                if (!StageEntryLoading.IsLoading) Mark("revealed");
            }
            public void Report()
            {
                Debug.Log("FIELD_ENTRY_PHASES stage="+stage+" sample="+sample+" "+
                    string.Join(" ",times.OrderBy(p=>p.Value).Select(p=>p.Key+"="+p.Value.ToString("F3",System.Globalization.CultureInfo.InvariantCulture))));
            }
        }
        static IEnumerator MeasureEntries()
        {
            Application.runInBackground=true;
            var prefs=GamePreferences.Current.Copy();
            prefs.pauseOnFocusLoss=false;
            GamePreferences.Apply(prefs,false);
            var bp=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").characters.First();
            var args=Environment.GetCommandLineArgs();
            int[] order=args.Contains("-reverseFieldEntries") ? new[]{2,0} : new[]{0,2};
            int samples=args.Contains("-repeatFieldEntries") ? 4 : 2;
            for(int sample=0;sample<samples;sample++)
            {
                if(sample==2) Check(GStarSession.FinishVisitor() && GStarSession.ResetVisitor(),"fresh visitor for warm entry comparison");
                int stage=order[sample%2];
                CrossSceneData.CharacterBlueprint=bp;
                var trace=new EntryTrace(stage,sample+1);
                SceneManager.sceneLoaded+=trace.Loaded; Canvas.willRenderCanvases+=trace.Rendered;
                LevelManager level=null;
                try
                {
                    Check(StageEntryLoading.Begin(bp,stage:stage),"start timing stage "+stage);
                    trace.Mark("overlayBuilt");
                    StageProgression flow=null;
                    do
                    {
                        yield return null;
                        if(level==null)level=Object.FindObjectOfType<LevelManager>();
                        trace.Observe(level);
                        if(level!=null && flow==null) flow=level.GetComponent<StageProgression>();
                    }
                    while(flow==null || flow.StageNumber!=stage || StageEntryLoading.IsLoading || !level.InitialFieldObjectsReady);
                    double reveal=trace.Elapsed;
                    while(flow.Travelling || level.IsRunFlowPaused) { yield return null; trace.Observe(level); }
                    trace.Mark("controllable");
                    trace.Report();
                    Debug.Log($"FIELD_ENTRY_TIMING stage={stage} visible={reveal:F3} controllable={trace.Elapsed:F3}");
                }
                finally
                {
                    SceneManager.sceneLoaded-=trace.Loaded; Canvas.willRenderCanvases-=trace.Rendered;
                }
                Check(GStarSession.AttemptStarted && !StageEntryLoading.IsLoading,"field ready for stage "+stage);
                if (stage == 2) VerifyTerrain(FiniteStomachMap.Instance);
                GStarAnalytics.Finish(false,"timing"); GStarSession.CompleteAttempt(false);
                var back=SceneManager.LoadSceneAsync(0); while(!back.isDone)yield return null;
                yield return null;
            }
            SessionState.SetBool(Key+"Done",true);
        }

        static void VerifyTerrain(FiniteStomachMap map)
        {
            Check(map != null && map.IsColon && map.UsingBakedTerrain && map.FloorTexture != null,
                "colon wall rendering and collision loaded from baked geometry");
            float worstError = 0;
            for (int x = 13; x < 500; x += 29)
            for (int y = 17; y < 500; y += 33)
            {
                var position = map.UVToWorld(new Vector2(x / 511f, y / 511f));
                float exact = map.FloorClearance(position, true);
                worstError = Mathf.Max(worstError, Mathf.Abs(exact - map.FloorClearance(position)));
            }
            Check(worstError < .005f, "baked versus exact floor distance error " + worstError);
        }

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
                    if(level!=null)
                    {
                        if(TutorialGuide.Instance!=null)TutorialGuide.Instance.enabled=false;
                        if(Object.FindObjectsOfType<SyringeProjectile>().Any(p=>p.gameObject.activeInHierarchy))throw new Exception("Needle projectile emitted behind loading");
                        foreach(var ability in Object.FindObjectsOfType<SyringeDartAbility>())
                            if(!ability.AttacksBlocked)throw new Exception("Attack gate open behind loading");
                        var layout=level.GetComponent<FieldObjectLayout>();
                        if(StageProgression.Ensure(level).StageNumber==1 && layout!=null && layout.SpawnedObjects.Count!=0)
                            throw new Exception("Unused stage-one props were built for direct colon entry");
                    }
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
                    foreach(var ability in Object.FindObjectsOfType<SyringeDartAbility>())
                        if(!ability.AttacksBlocked)throw new Exception("Attack gate open during inlet animation");
                    if(Object.FindObjectOfType<AmphibiousCapsule>()!=null)throw new Exception("Legacy stomach capsule appeared");
                    opening|=flow.TransitionPhase=="opening";eject|=flow.TransitionPhase=="eject";
                    yield return null;
                }
                Check(opening&&eject,"inlet opening and character ejection preserved");
                Check(Time.timeScale==1&&!manager.IsRunFlowPaused&&StageEntryLoading.Instance==null,"loading and pause owners released");
                yield return null;yield return null;
                Check(manager.CurrentLevelTime>0,"combat timer starts after entrance");
                GStarAnalytics.Finish(false,"qa_repeat");GStarSession.CompleteAttempt(false);
                var back=SceneManager.LoadSceneAsync(0);while(!back.isDone)yield return null;yield return null;
            }
            // Inter-stage transfers still create an independent 0..100 overlay.
            var transfer=StageEntryLoading.CreateStageTransfer(bp);
            transfer.SetTransferProgress(.4f);Check(Mathf.Abs(transfer.Progress-.4f)<.001f,"ordinary transfer progress unchanged");
            transfer.SetTransferProgress(1);transfer.CompleteStageTransfer();yield return null;
            Check(GStarSession.FinishVisitor() && GStarSession.ResetVisitor(),"new visitor receives fresh attempts");
            Check(StageEntryLoading.Begin(bp,stage:1),"stage-one departure accepted after colon runs");
            while(StageEntryLoading.IsLoading)yield return null;
            var stomach=Object.FindObjectOfType<LevelManager>();
            if(TutorialGuide.Instance!=null){TutorialGuide.Instance.Close();TutorialGuide.Instance.enabled=false;}
            var buried=stomach.GetComponent<BuriedBossDirector>();
            Check(Object.FindObjectOfType<FinalBossSummonInteractable>()==null,"no legacy boss selector/altar in demo");
            Check(buried!=null&&buried.Part!=null&&!buried.Consumed,"assigned boss part remains summonable");
            Check(buried.CanSummon,"buried part retains interaction path");
            var scent=stomach.GetComponentInChildren<BossScentGuide>();
            Check(scent!=null&&!scent.raycastTarget,"scent guide is visual only");
            var savedPart=buried.Part.transform.position;
            var camera=Camera.main;var player=stomach.PlayerCharacter;
            buried.Part.transform.position=player.transform.position+Vector3.right*(camera.orthographicSize*camera.aspect+4);
            Directory.CreateDirectory("Library/DirectColonLoadingQA/scent");
            for(int i=0;i<75;i++)
            {
                yield return new WaitForSecondsRealtime(.04f);
                // Headless editor play mode has no end-of-frame display callback.
                if(!Application.isBatchMode && i%2==0){yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("Library/DirectColonLoadingQA/scent/"+i.ToString("000")+".png",image.EncodeToPNG());Object.Destroy(image);}
            }
            Check(scent.Strength>0,"off-screen part emits screen-edge scent");
            Canvas.ForceUpdateCanvases();
            var mesh=scent.canvasRenderer.GetMesh();
            Debug.Log("SCENT_GEOMETRY rect="+scent.rectTransform.rect+" edge="+scent.SourceEdge+" strength="+scent.Strength+" vertices="+mesh.vertexCount+" cull="+scent.canvasRenderer.cull+" canvas="+scent.canvas.pixelRect+" alpha="+scent.canvasRenderer.GetAlpha());
            Check(mesh!=null&&mesh.vertexCount>100&&!scent.canvasRenderer.cull,"visible scent mesh is rendered");
            var projected=BossScentGuide.ProjectEdge(new Vector2(2,1),new Vector2(480,270),1);
            Check(Mathf.Abs(projected.x-479)<.01f&&projected.y>0,"scent direction projects to correct edge");
            buried.Part.transform.position=player.transform.position+Vector3.right;
            yield return new WaitForSecondsRealtime(1);
            Check(scent.Strength<.01f,"scent disappears when part is visible");
            buried.Part.transform.position=savedPart;
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
