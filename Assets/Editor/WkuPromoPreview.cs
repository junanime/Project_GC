using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace Vampire.Editor
{
    [InitializeOnLoad] public static class WkuPromoPreview
    {
        const string Output="Library/WkuPromoQA";static bool started;
        static WkuPromoPreview(){if(SessionState.GetBool("WkuPreview",false)||SessionState.GetBool("WkuBuildAfterVerification",false))EditorApplication.update+=Tick;}
        public static void RunAndBuild(){Run();SessionState.SetBool("WkuBuildAfterVerification",true);}
        public static void Run()
        {
            Directory.CreateDirectory(Output);WkuPromoAssets.Prepare();SessionState.SetBool("WkuPreview",true);
            SessionState.SetFloat("WkuDeadline",(float)EditorApplication.timeSinceStartup+900);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,761);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!SessionState.GetBool("WkuPreview",false)&&SessionState.GetBool("WkuBuildAfterVerification",false)&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {SessionState.SetBool("WkuBuildAfterVerification",false);WkuPromoBuild.Run();return;}
            if(!SessionState.GetBool("WkuPreview",false))return;
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat("WkuDeadline",float.MaxValue)){EditorApplication.Exit(1);return;}
            if(started||!EditorApplication.isPlaying||ApothecaryUI.Instance==null)return;
            started=true;Application.logMessageReceived+=Log;var host=new GameObject("WKU verification").AddComponent<WkuPreviewHost>();Object.DontDestroyOnLoad(host);host.StartCoroutine(Checks());
        }
        static void Log(string message,string trace,LogType type)
        {
            if(type!=LogType.Exception)return;File.AppendAllText(Output+"/failed.txt",message+"\n"+trace);SessionState.SetBool("WkuPreview",false);SessionState.SetBool("WkuBuildAfterVerification",false);EditorApplication.Exit(1);
        }
        static void Check(bool ok,string description){if(!ok)throw new Exception(description);File.AppendAllText(Output+"/checks.txt","PASS "+description+"\n");}
        static void Set(string name,object value)=>typeof(ApothecaryUI).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ApothecaryUI.Instance,value);
        static void Capture(string name)
        {
            Directory.CreateDirectory("Library/FestivalQA");typeof(Vampire.Tests.Editor.FestivalDemoSmoke).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"wku-"+name});
            File.Copy("Library/FestivalQA/wku-"+name+".png",Output+"/"+name+".png",true);
        }
        static IEnumerator Checks()
        {
            File.WriteAllText(Output+"/checks.txt","");Application.runInBackground=true;var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            var ui=ApothecaryUI.Instance;yield return null;yield return null;
            Check(FestivalDemo.Settings.duration==45&&!FestivalDemo.Settings.enableSpecialMonsters,"45 second festival rules with special monsters disabled");
            Check(FestivalLeaderboard.FilePath.Contains("WkuDepartmentPromo"),"school ledger isolated");
            foreach(bool success in new[]{true,false})
            {
                ApothecaryUI.WkuPreviewData=new RunScoreData{seconds=success?45:28.4f,success=success,normal=success?82:37};Set("passed",success);ui.Show("result");
                yield return null;yield return null;yield return new WaitForSecondsRealtime(.15f);
                Check(ui.GetComponentInChildren<WkuResultForm>().School.isFocused,"real school caret receives focus "+success);
                Check(typeof(TMP_InputField).GetField("m_CachedInputRenderer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui.GetComponentInChildren<WkuResultForm>().School)!=null,"native insertion caret renderer exists "+success);
                Check(ui.GetComponentsInChildren<Image>().Count(i=>i.name=="WKU shared party illustration"&&i.sprite!=null)==1,"one shared interacting four character illustration "+success);
                Check(!ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("경품")||t.text.Contains("game.wku.ac.kr")||t.text.Contains("이 PC에 저장")),"removed requested result copy "+success);
                Check(ui.GetComponentsInChildren<Button>().Count(b=>b.name=="Button 기록 등록")==1,"single registration button "+success);Capture(success?"success":"failure");
            }
            yield return Vampire.Tests.Editor.FestivalIdentityInputChecks.Run(Check);ApothecaryUI.WkuPreviewData=null;
            foreach(bool success in new[]{false,true})
            {
                Time.timeScale=1;Check(StageEntryLoading.Begin(ui.Config.characters.First(c=>FestivalDemo.Preset(c)!=null)),"real stage starts");
                float deadline=Time.realtimeSinceStartup+180;while(StageEntryLoading.IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(!StageEntryLoading.IsLoading,"real stage finishes loading");yield return Vampire.Tests.Editor.FestivalDemoSmoke.ContinueGaugeGuide();
                var level=Object.FindObjectOfType<LevelManager>();
                if(!success){yield return WkuOrdinaryMonsterChecks.Run(level,Check);yield return new WaitForSecondsRealtime(2);Capture("combat-skins");level.GameOver();yield return null;}
                else{typeof(LevelManager).GetField("levelTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(level,44.95f);yield return new WaitForSecondsRealtime(.4f);}
                Check(RunScore.Current.Finished&&RunScore.Current.Data.success==success,"correct actual game result "+success);yield return null;yield return null;Capture(success?"success-live":"failure-live");
                FestivalLeaderboard.TestFilePath=Path.GetFullPath(Output+"/synthetic-"+Guid.NewGuid().ToString("N")+".json");yield return WkuResultKeyboardChecks.Register(Check,!success);
                ui=ApothecaryUI.Instance;ui.Back();yield return null;Check(ui.Page=="result","ranking returns to result");
                Check(ui.GetComponentsInChildren<TMP_InputField>().All(f=>!f.interactable),"saved fields lock");ui.GetComponentsInChildren<Button>().First(b=>b.name=="Button 등록 완료").onClick.Invoke();Check(FestivalLeaderboard.Read().Count==1,"duplicate saved registration blocked");
                ui.GetComponentsInChildren<Button>().First(b=>b.name=="Button 다음 참가자").onClick.Invoke();while(ApothecaryUI.Instance==null||ApothecaryUI.Instance.Page!="festival")yield return null;ui=ApothecaryUI.Instance;
            }
            ui.WkuMonsterGallery();yield return null;yield return null;Capture("monster-concepts");
            File.WriteAllText(Output+"/complete.txt","All current ordinary-monster, result, IME and keyboard checks completed. Synthetic records only.");
            Application.logMessageReceived-=Log;SessionState.SetBool("WkuPreview",false);EditorApplication.ExitPlaymode();
            if(!SessionState.GetBool("WkuBuildAfterVerification",false))EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
        }
    }
    public sealed class WkuPreviewHost:MonoBehaviour{}
}
