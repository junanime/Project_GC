using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class StageEventPresentationSmoke
    {
        const string Key="StageEventPresentationSmoke", Output="Library/StageEventPresentationProof";
        static bool started;
        static double deadline;
        static StageEventPresentationSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            foreach(string key in new[]{"Coins","LobbySilverCoins"})
            { SessionState.SetBool(Key+key+"Exists",PlayerPrefs.HasKey(key)); SessionState.SetInt(Key+key,PlayerPrefs.GetInt(key)); }
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+240;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){Time.timeScale=1;EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                foreach(string key in new[]{"Coins","LobbySilverCoins"})
                {if(SessionState.GetBool(Key+key+"Exists",false))PlayerPrefs.SetInt(key,SessionState.GetInt(Key+key,0));else PlayerPrefs.DeleteKey(key);}
                PlayerPrefs.Save(); bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log("[EventUISmoke] FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(!started&&EditorApplication.isPlaying&&level!=null&&level.PlayerCharacter!=null&&level.CurrentLevelTime>.1f)
            {started=true;level.StartCoroutine(Checks(level));}
        }
        static void Check(bool ok,string text)
        {if(!ok)throw new Exception("[EventUISmoke] FAIL "+text);Debug.Log("[EventUISmoke] PASS "+text);}
        static void Capture(string name)
        {
            Directory.CreateDirectory(Path.GetFullPath("../work"));
            typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{name+".png"});
            File.Copy(Path.GetFullPath("../work/"+name+".png"),Output+"/"+name+".png",true);
        }
        static IEnumerator Checks(LevelManager level)
        {
            Application.runInBackground=true;
            var preferences=GamePreferences.Current.Copy();preferences.pauseOnFocusLoss=false;preferences.muteOnFocusLoss=false;GamePreferences.Apply(preferences,false);
            Time.timeScale=1;
            var director=Object.FindObjectOfType<StageEventDirector>();Check(director!=null,"Real Level 1 stage director exists");
            director.enabled=false;
            var ui=director.gameObject.AddComponent<StageEventCornerUI>();
            var toast=Object.FindObjectOfType<StageEventToastUI>();Check(toast!=null,"Real scene announcement component exists");
            string[] names={"감염 증식","골드 러시","위산분비","산성 역류 파도","연동운동 기류","커피수혈 타임","제산 거품 폭주"};
            var keys=Enumerable.Range(0,7).Select(_=>new object()).ToArray();
            var items=new List<StageEventPresentation>();
            for(int kind=0;kind<7;kind++)
            {
                items.Clear();items.Add(new StageEventPresentation(keys[kind],(StageEventVisualKind)kind,names[kind],-1));
                toast.Show(names[kind]+" 이벤트가 시작됐습니다!");
                float end=Time.time+2.7f;
                bool captured=false;
                while(Time.time<end)
                {
                    ui.Sync(items,false,false);
                    if(!captured&&Time.time>end-1.8f){captured=true;Capture("event-"+kind);}
                    yield return null;
                }
                Check(ui.ActiveCount==1&&ui.IsVisible,"Single event ornaments persist after title: "+names[kind]);
            }
            var current=keys[6];float age=ui.VisualAge(current);
            var animated=Object.FindObjectsOfType<Image>().Where(x=>x.name.StartsWith("Animated Antacid")).ToArray();
            var positions=animated.Select(x=>x.rectTransform.anchoredPosition).ToArray();
            var sprites=animated.Select(x=>x.sprite).ToArray();
            float motionEnd=Time.time+.7f;
            while(Time.time<motionEnd){ui.Sync(items,false,false);yield return null;}
            Check(animated.Where((x,i)=>x.sprite!=sprites[i]).Any(),"Gameplay bubble pop frames advance");
            Check(animated.Where((x,i)=>(x.rectTransform.anchoredPosition-positions[i]).sqrMagnitude>.1f).Any(),"Corner bubbles rise continuously");
            Capture("event-antacid-running");
            age=ui.VisualAge(current);
            ui.Sync(items,false,true);Check(Mathf.Approximately(age,ui.VisualAge(current)),"Paused UI animation freezes");
            ui.Sync(items,true,true);Check(!ui.IsVisible&&ui.ActiveCount==1,"Mini-stage hides but retains current event");
            ui.Sync(items,false,false);Check(ui.IsVisible,"Returning restores event ornaments");
            items.Add(new StageEventPresentation(keys[1],StageEventVisualKind.Gold,names[1]));
            ui.Sync(items,false,false);yield return null;Check(ui.ActiveCount==2,"Concurrent event decorations coexist");
            Capture("event-overlap");
            items.RemoveAt(0);ui.Sync(items,false,false);Check(ui.ActiveCount==1&&ui.IsVisible,"Ending one event preserves the other");
            items.Clear();ui.Sync(items,false,false);Check(ui.ActiveCount==0&&!ui.IsVisible,"Event end removes all stale decorations");
            var canvas=Object.FindObjectsOfType<Canvas>().First(c=>c.name=="Stage event corner ornaments");
            Check(canvas.GetComponent<GraphicRaycaster>()==null&&!canvas.GetComponent<CanvasGroup>().blocksRaycasts,"Ornaments never capture input");
            Check(canvas.sortingOrder<0,"Ornaments render behind existing HUD, not over currency or XP");
            // Exercise the actual director/module collection rather than only the UI API.
            var basic=new StageEventSchedule();
            var evt=new StageEventSchedule.GoldRushEvent{started=true,startTime=0,duration=999};
            StageEventPresentationTests.Set(basic,"goldRushEvents",new List<StageEventSchedule.GoldRushEvent>{evt});
            StageEventPresentationTests.Set(director,"basicEvents",new List<StageEventSchedule>{basic});
            StageEventPresentationTests.Set(director,"advancedEvents",new List<AdvancedStageFieldEventDirector>());
            StageEventPresentationTests.Set(director,"antacidEvents",new List<AntacidBubbleSurgeEventController>());
            StageEventPresentationTests.Set(director,"cornerUI",ui);
            StageEventPresentationTests.Set(basic,"started",true); // RuntimeModule.OnStart must not reset the injected runtime state.
            director.enabled=true;yield return null;yield return null;
            Check(ui.ActiveCount==1,"Director automatically synchronizes live event state");
            evt.finished=true;yield return null;yield return null;
            Check(ui.ActiveCount==0,"Director automatically clears completed event");
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
