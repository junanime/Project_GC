using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class WebDemoEndingSmoke
    {
        const string Key="WebDemoEndingSmoke", Output="D:/Project_GC/work/web-demo-ending-proof";
        static bool started,returnExpected;static int pass;static double deadline;
        static WebDemoEndingSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/checks.txt","");File.WriteAllText(Output+"/errors.txt","");
            SessionState.SetInt(Key+"Doctor",PlayerPrefs.GetInt(CapsuleDoctorDialogue.SeenKey,-1));
            SessionState.SetBool(Key+"HadCoins",PlayerPrefs.HasKey("Coins"));
            SessionState.SetInt(Key+"Coins",PlayerPrefs.GetInt("Coins",0));
            PlayerPrefs.SetInt(CapsuleDoctorDialogue.SeenKey,1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Fail",false);SessionState.SetBool(Key+"Done",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            // Resource exclusion is reversible and does not change the native stage flag.
            Check(StageProgression.StageTwoEnabled,"native build retains Stage 2");
            var guid=AssetDatabase.AssetPathToGUID("Assets/Resources/ColonMap/Stage2.asset");
            using(var scope=new Vampire.Editor.WebDemoStageScope())
                Check(!Directory.Exists("Assets/Resources/ColonMap")&&!Directory.Exists("Assets/Resources/ColonMonsters"),"Stage 2 Resources excluded during web packaging");
            Check(AssetDatabase.AssetPathToGUID("Assets/Resources/ColonMap/Stage2.asset")==guid,"Stage 2 restored with same GUID");
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;returnExpected=false;pass=0;deadline=EditorApplication.timeSinceStartup+600;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string s,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",s+"\n"+trace+"\n");SessionState.SetBool(Key+"Fail",true);}}
        static void Check(bool ok,string s){if(!ok)throw new Exception("Demo ending: "+s);File.AppendAllText(Output+"/checks.txt",s+"\n");}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Fail",true);File.AppendAllText(Output+"/errors.txt","Deadline\n");}
            if(SessionState.GetBool(Key+"Fail",false)||SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                StageProgression.PreviewWebDemoEnding=false;GamePlatform.PreviewTouchControls=false;
                int seen=SessionState.GetInt(Key+"Doctor",-1);if(seen<0)PlayerPrefs.DeleteKey(CapsuleDoctorDialogue.SeenKey);else PlayerPrefs.SetInt(CapsuleDoctorDialogue.SeenKey,seen);PlayerPrefs.Save();
                if(SessionState.GetBool(Key+"HadCoins",false))PlayerPrefs.SetInt("Coins",SessionState.GetInt(Key+"Coins",0));else PlayerPrefs.DeleteKey("Coins");PlayerPrefs.Save();
                SessionState.SetBool(Key,false);bool fail=SessionState.GetBool(Key+"Fail",false);File.WriteAllText(Output+"/result.txt",fail?"FAIL":"PASS PC + mobile");EditorApplication.Exit(fail?1:0);return;
            }
            if(!EditorApplication.isPlaying)return;
            StageProgression.PreviewWebDemoEnding=true;GamePlatform.PreviewTouchControls=pass==1;
            if(returnExpected&&SceneManager.GetActiveScene().buildIndex==0)
            {
                Check(Time.timeScale==1,"main menu restored with unpaused time, touch="+(pass==1));
                Check(Object.FindObjectOfType<DemoCompletionDialogue>()==null&&Object.FindObjectOfType<StageProgression>()==null,"no ending/capsule runtime survives return");
                pass++;returnExpected=false;
                if(pass==2){SessionState.SetBool(Key+"Done",true);return;}
                started=false;GamePlatform.PreviewTouchControls=true;SceneManager.LoadScene(1);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(started||level==null||level.PlayerCharacter==null||level.GetComponent<FieldObjectLayout>()?.Ready!=true||StageEntryLoading.IsLoading)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static void Set(object o,string n,object v){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null){f.SetValue(o,v);return;}}throw new Exception(n);}
        static IEnumerator Checks(LevelManager level)
        {
            Application.runInBackground=true;var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            Time.timeScale=1;RunScore.Exclude("web demo ending QA");level.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner){b.enabled=false;b.StopAllCoroutines();}
            var entity=level.EntityManager;entity.enabled=false;entity.StopAllCoroutines();var player=level.PlayerCharacter;Set(player,"isInvincible",true);player.CollectableCollider.enabled=false;
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var m in entity.LivingMonsters.ToArray())m.DespawnForStageTransfer();
            var p=StageProgression.Ensure(level);var original=FiniteStomachMap.Instance.Definition;
            int oldCoins=PlayerPrefs.GetInt("Coins",0);var stats=Object.FindObjectOfType<StatsManager>();stats.IncreaseCoinsGained(7);
            int expectedCoins=oldCoins+stats.CoinsGained;
            p.FinalBossDefeated(player.transform.position+Vector3.up*3,100);
            float end=Time.time+20;while((p.Capsule==null||!p.Capsule.Ready)&&Time.time<end)yield return null;
            Check(p.Capsule!=null&&p.Capsule.Ready,"capsule lands and is interactable");
            player.transform.position=p.Capsule.BoardingPoint;
            Check(p.TryBoard(player,p.Capsule),"capsule boarding accepted");
            var phases=new System.Collections.Generic.HashSet<string>();end=Time.time+25;
            while(p.TransitionPhase!="demo-complete"&&Time.time<end){phases.Add(p.TransitionPhase);yield return null;}
            foreach(var phase in new[]{"hatch-open","boarding","hatch-close","capsule-travel","outlet-open"})Check(phases.Contains(phase),"preserved "+phase);
            var dialogue=Object.FindObjectOfType<DemoCompletionDialogue>();Check(dialogue!=null&&dialogue.Message.text==DemoCompletionDialogue.CompletionMessage,"exact demo completion dialogue");
            Check(p.StageNumber==1&&p.AtOutlet&&FiniteStomachMap.Instance.Definition==original,"ending at Stage 1 southwest outlet, no colon load");
            Check(Vector2.Distance(p.Capsule.transform.position,StomachOpening.Find(false).FrontLip+StomachOpening.Find(false).Inward*1.25f)<.01f,"boat arrives at exact outlet destination");
            float clock=level.CurrentLevelTime;yield return new WaitForSeconds(1);
            Check(level.CurrentLevelTime==clock&&level.IsRunFlowPaused,"dialogue preserves frozen run");
            Check(RunScore.Current.Data.success,"completed demo records success");
            Check(level.IsLevelEnded&&PlayerPrefs.GetInt("Coins")==expectedCoins,"demo marks run complete and saves collected coins");
            level.CompleteWebDemo();Check(PlayerPrefs.GetInt("Coins")==expectedCoins,"completion cannot award coins twice");
            Canvas.ForceUpdateCanvases();
            typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"web-demo-ending-proof/"+(pass==0?"pc":"mobile")+".png"});
            returnExpected=true;dialogue.ConfirmButton.onClick.Invoke();dialogue.ConfirmButton.onClick.Invoke();
            Check(dialogue.Confirmed,"confirm is idempotent");
        }
    }
}
