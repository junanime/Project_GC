using System;
using System.Collections;
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
    public static class ToadUpdateSmoke
    {
        const string Key="ToadUpdateSmoke";
        static bool running;static double deadline;
        static ToadUpdateSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            foreach(string key in new[]{"Coins","LobbySilverCoins"}){SessionState.SetBool(Key+key+"Exists",PlayerPrefs.HasKey(key));SessionState.SetInt(Key+key,PlayerPrefs.GetInt(key));}
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){running=false;deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);Debug.Log("TOAD_SMOKE_TIMEOUT");}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                foreach(string key in new[]{"Coins","LobbySilverCoins"}){if(SessionState.GetBool(Key+key+"Exists",false))PlayerPrefs.SetInt(key,SessionState.GetInt(Key+key,0));else PlayerPrefs.DeleteKey(key);}
                PlayerPrefs.Save();bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);Debug.Log("TOAD_SMOKE_FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(!running&&EditorApplication.isPlaying&&level?.PlayerCharacter!=null){running=true;level.StartCoroutine(Checks(level));}
        }
        static void Check(bool ok,string text){if(!ok)throw new Exception("TOAD_FAIL "+text);Debug.Log("TOAD_PASS "+text);}
        static void Capture(string name)
        {
            Directory.CreateDirectory("Library/ToadUpdateProof");
            typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"toad-ui-"+name+".png"});
            File.Copy(Path.GetFullPath("../work/toad-ui-"+name+".png"),"Library/ToadUpdateProof/ui-"+name+".png",true);
            var camera=Camera.main;var rt=RenderTexture.GetTemporary(1280,720,24);var before=camera.targetTexture;camera.targetTexture=rt;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes("Library/ToadUpdateProof/"+name+".png",image.EncodeToPNG());Object.Destroy(image);RenderTexture.active=previous;camera.targetTexture=before;RenderTexture.ReleaseTemporary(rt);
        }
        static IEnumerator Checks(LevelManager level)
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
            var tutorial=Object.FindObjectOfType<TutorialGuide>();if(tutorial!=null){tutorial.Close();tutorial.enabled=false;}
            Time.timeScale=1;
            var director=Object.FindObjectOfType<StageEventDirector>();if(director!=null)director.enabled=false;
            var timed=Object.FindObjectOfType<TimedSpecialMonsterSpawner>();if(timed!=null)timed.enabled=false;
            foreach(var name in new[]{"ToadLocomotion","ToadAttack","SquirrelMotion"})Check(AcidToadArt.Frames(name).Length==16,name+" 16 frames");
            foreach(var name in new[]{"ToadJump","SquirrelReact","AcidFx"})Check(AcidToadArt.Frames(name).Length==8,name+" 8 frames");
            Check(AcidToadArt.Frames("LightWoodUI").Length==4,"Four matching wood assets");
            Check(OriginalCombatRules.FormationEndpointCount(1)==3&&OriginalCombatRules.FormationEndpointCount(2)==5&&OriginalCombatRules.FormationEndpointCount(3)==7,"Endpoint 3/5/7");
            Check(Mathf.Approximately(OriginalCombatRules.FormationDamage(3),1),"Old formation damage bonus removed");
            Check(Mathf.Approximately(AcidToadMonster.BodyWidth,Resources.Load<SnailBossSettings>("SnailBossSettings").bodyWidth*.8f),"Toad 80 percent snail width");
            var schedule=Object.FindObjectOfType<AcidToadSpawn>();Check(schedule!=null&&schedule.SpawnAt>=420&&schedule.SpawnAt<=450&&!schedule.Spawned,"Single random 7m-7m30 schedule");schedule.enabled=false;
            var containers=level.CurrentLevelBlueprint.monsters;int index=Array.FindIndex(containers,c=>c.monstersPrefab.GetComponent<AcidToadMonster>()!=null);
            Check(index>=0,"Toad registered in actual level pool");
            Vector2 origin=level.PlayerCharacter.transform.position;
            var boss=(AcidToadMonster)level.EntityManager.SpawnMonster(index,origin+new Vector2(3,0),containers[index].monsterBlueprints[0],0,false);boss.AutoPatterns=false;
            var body=boss.GetComponent<FoodAtlasMotion>().Renderer;Vector3 scale=boss.transform.localScale;
            yield return new WaitForSeconds(.2f);Check(body.sprite!=null&&boss.HP==1200,"Live miniboss art and health");Capture("toad-idle");
            foreach(var pattern in new[]{AcidToadMonster.Pattern.Basic,AcidToadMonster.Pattern.Jet,AcidToadMonster.Pattern.Leap})
            {
                Check(boss.UsePattern(pattern),"Start "+pattern);float age=0;bool captured=false;
                while(boss.Busy&&age<7)
                {
                    age+=Time.deltaTime;CheckScale(boss.transform.localScale,scale);
                    if(pattern==AcidToadMonster.Pattern.Basic&&age>.68f&&!captured){Check(Object.FindObjectsOfType<AcidGlob>().Length==3,"Exactly three acid spheres");Capture("toad-basic");captured=true;}
                    if(pattern==AcidToadMonster.Pattern.Jet&&age>1.6f&&!captured){Check(Object.FindObjectsOfType<AcidJet>().Length==1,"Miniboss uses shared continuous stream");Capture("toad-jet");captured=true;}
                    if(pattern==AcidToadMonster.Pattern.Leap&&age>1.15f&&!captured){Capture("toad-jump");captured=true;}
                    yield return null;
                }
                Check(!boss.Busy,"Complete and recover "+pattern);
            }
            boss.UsePattern(AcidToadMonster.Pattern.Jet);yield return new WaitForSeconds(1.4f);
            boss.SetFieldRuntimeSuspended(true);yield return null;Check(Object.FindObjectsOfType<AcidJet>().Length==0&&!boss.Busy,"Suspend clears stream and action");boss.SetFieldRuntimeSuspended(false);
            var eventRoutine=level.StartCoroutine(AcidToadEvent.Emit(director.transform,new Rect(origin.x-7,origin.y-4,14,8),true,1,1.8f,1.5f,8,.65f,level.PlayerCharacter));
            yield return new WaitForSeconds(1.3f);Check(Object.FindObjectsOfType<AcidJet>().Length==1,"Event uses same jet");Capture("toad-event");yield return eventRoutine;
            Check(Object.FindObjectsOfType<AcidJet>().Length==0,"Event cleanup");
            int squirrelIndex=Array.FindIndex(containers,c=>c.monstersPrefab.GetComponent<TreasureRunnerMonster>()!=null);
            var squirrel=(TreasureRunnerMonster)level.EntityManager.SpawnMonster(squirrelIndex,origin+Vector2.left*5,containers[squirrelIndex].monsterBlueprints[0],0,false);
            yield return new WaitForSeconds(.3f);Check(!squirrel.IsFleeing,"Squirrel approaches cautiously");Capture("squirrel-approach");
            squirrel.transform.position=level.PlayerCharacter.transform.position+Vector3.left;squirrel.GetComponent<Rigidbody2D>().position=squirrel.transform.position;
            yield return new WaitForSeconds(.4f);Check(squirrel.IsFleeing,"Squirrel switches to fast escape");Capture("squirrel-flee");
            var ui=ApothecaryUI.Instance;Check(ui!=null,"HUD exists");
            int before=level.PlayerCharacter.MaxDashCharges;bool completed=false;Check(ui.RequestSkillReward(()=>completed=true),"Reward queue");
            for(int n=0;n<60&&ui.Page!="skillReward";n++)yield return null;
            yield return null;Canvas.ForceUpdateCanvases();
            var dashIcon=Object.FindObjectOfType<SharedDashIcon>();
            Check(ui.Page=="skillReward"&&dashIcon!=null&&dashIcon.canvasRenderer!=null,"Dash reward panel visible with renderer");
            Capture("dash-reward");
            typeof(ApothecaryUI).GetMethod("FinishDashReward",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
            typeof(ApothecaryUI).GetMethod("FinishDashReward",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
            Check(completed&&level.PlayerCharacter.MaxDashCharges==before+1,"Dash reward exactly once");
            Check(Object.FindObjectsOfType<RectTransform>().Any(t=>t.name=="Light wood medicine tray"),"Wood tray live");
            yield return null;
            var vision=Object.FindObjectOfType<PrescriptionScrollView>();vision.Toggle.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.4f);Check(vision.Reveal>.99f,"Wood vision unfolds");Capture("wood-vision");
            int toads=Object.FindObjectsOfType<AcidToadMonster>().Length;
            typeof(AcidToadSpawn).GetProperty("SpawnAt").SetValue(schedule,0f);
            var spawnTick=typeof(AcidToadSpawn).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            spawnTick.Invoke(schedule,null);spawnTick.Invoke(schedule,null);
            Check(schedule.Spawned&&Object.FindObjectsOfType<AcidToadMonster>().Length==toads+1,"Scheduled spawn fires only once");
            boss.TakeDamage(1000000);yield return new WaitForSeconds(.6f);
            Check(!level.EntityManager.LivingMonsters.Contains(boss),"Miniboss death unregisters and recovers runtime");
            yield return null;SessionState.SetBool(Key+"Done",true);
        }
        static void CheckScale(Vector3 actual,Vector3 expected){if((actual-expected).sqrMagnitude>.00001f)throw new Exception("Toad changed scale between motions");}
    }
}
