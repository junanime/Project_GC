using System;
using System.Collections;
using System.Collections.Generic;
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
    public static class PlayQa1004Smoke
    {
        const string Key="PlayQa1004Smoke";
        static double deadline;
        static bool attached, failed, done;
        static PlayQa1004Smoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(type).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            deadline=EditorApplication.timeSinceStartup+240;attached=failed=done=false;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string message,string stack,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)failed=true;}
        public static void Complete(){done=true;}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)failed=true;
            if(failed||done)
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                SessionState.SetBool(Key,false);Debug.Log("[QA1004] FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            if(!attached&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {attached=true;var go=new GameObject("QA1004 verification");UnityEngine.Object.DontDestroyOnLoad(go);go.AddComponent<PlayQa1004Checks>();}
        }
    }
    public sealed class PlayQa1004Checks : MonoBehaviour
    {
        static object Get(object o,string f)=>PacingPatchTests.Get(o,f);
        static void Set(object o,string f,object v)=>PacingPatchTests.Set(o,f,v);
        static void Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,PacingPatchTests.Hidden).Invoke(o,args);
        static void Check(bool result,string label){if(!result)throw new Exception("[QA1004] FAIL "+label);Debug.Log("[QA1004] PASS "+label);}
        static void Capture(string name)
        {
            Directory.CreateDirectory("Library/PlayQA1004");var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes("Library/PlayQA1004/"+name+".png",texture.EncodeToPNG());Destroy(texture);
        }
        IEnumerator Start()
        {
            var prefs=GamePreferences.Current.Copy();var original=CrossSceneData.CharacterBlueprint;
            try
            {
                var p=prefs.Copy();p.pauseOnFocusLoss=false;p.muteOnFocusLoss=false;GamePreferences.Apply(p,false);
                var ui=ApothecaryUI.Instance;ui.Show("prepare",1);yield return new WaitForSecondsRealtime(.6f);
                Check(ui.GetComponentsInChildren<Button>().Any(x=>x.name.StartsWith("Weapon pool toggle")),"weapon checkbox exists only in preparation");Capture("01-weapon-checkboxes");
                CrossSceneData.CharacterBlueprint=ui.Config.characters.First(x=>x.skills!=null&&x.skills.kind==CharacterSkillDefinition.SkillKind.Hyuki);
                CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();SceneManager.LoadScene(1);yield return new WaitForSecondsRealtime(2);
                ui=ApothecaryUI.Instance;var level=FindObjectOfType<LevelManager>();level.enabled=false;
                var player=level.PlayerCharacter;var entities=level.EntityManager;
                foreach(var monster in entities.LivingMonsters.ToArray())monster.gameObject.SetActive(false);
                var needle=FindObjectOfType<SyringeDartAbility>();needle.enabled=false;needle.StopAllCoroutines();
                var bars=player.GetComponent<PlayerCombatBars>();Check(bars!=null,"combat meters attached to actual player");
                player.AddDashCharge(Mathf.Max(0,3-player.MaxDashCharges));yield return null;
                Check(bars.DashSegments==player.MaxDashCharges,"dash cells follow actual charge cap");
                Check(bars.HealthRect.anchoredPosition.y>bars.DashRect.anchoredPosition.y&&bars.DashRect.anchoredPosition.y>bars.ActiveRect.anchoredPosition.y,"head health and stacked foot meters");
                Check(bars.DashRect.anchoredPosition.x==bars.ActiveRect.anchoredPosition.x,"foot meters share horizontal centre");
                Check(bars.ActiveRect.Find("Icon").GetComponent<Image>().sprite==player.Blueprint.skills.activeIcon,"existing active icon preserved");
                player.Skills.Restore(0,0,player.Skills.EffectiveCooldown*.5f);yield return null;
                var activeFill=(RectTransform)bars.ActiveRect.Find("Charge track/Charge 1/Fill");
                Check(Mathf.Abs(activeFill.anchorMax.x-.5f)<.02f,"active meter follows real half cooldown");player.Skills.Restore(0,0,0);
                Check(player.TryDash(),"free player can dash");yield return new WaitForSeconds(.5f);
                Check(player.DashRechargeProgress>0&&player.DashRechargeProgress<1,"actual dash refill progresses");Capture("02-live-meters");
                var runtime=FindObjectOfType<Ver4AugmentRuntime>();
                var weapons=ui.Config.weapons.Where(x=>x!=null).ToArray();
                var disabled=weapons.Where(x=>x.Type==SyringeSpecialAugmentAbility.SpecialAugmentType.IceNeedle||x.Type==SyringeSpecialAugmentAbility.SpecialAugmentType.WindNeedle).ToArray();
                var saved=disabled.Select(x=>(key:StartingNeedleSelection.EnabledKey(x.Type),exists:PlayerPrefs.HasKey(StartingNeedleSelection.EnabledKey(x.Type)),value:PlayerPrefs.GetInt(StartingNeedleSelection.EnabledKey(x.Type)))).ToArray();
                try
                {
                    foreach(var s in saved)PlayerPrefs.SetInt(s.key,0);
                    for(int draw=0;draw<40;draw++)foreach(var offer in runtime.CreateOffers(false,3).Cast<Ver4AugmentOffer>())
                    {
                        Check(offer.Parent==null||!disabled.Any(w=>w.Type==offer.Parent.Type),"disabled owned/new weapon excluded from draw "+draw);
                        Destroy(offer.gameObject);
                    }
                }
                finally {foreach(var s in saved){if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);}PlayerPrefs.Save();}
                var dialog=entities.AbilitySelectionDialog;dialog.Open();yield return null;
                Check(dialog.MenuOpen&&Time.timeScale==0,"augment selection pauses combat");
                ui.OpenRunBook();yield return new WaitForSecondsRealtime(.3f);
                Check(ui.Page=="run"&&dialog.GetComponent<CanvasGroup>().alpha==0,"TAB covers augment selection");Capture("03-tab-over-augment");
                ui.CloseRunBook();yield return null;
                Check(dialog.MenuOpen&&dialog.GetComponent<CanvasGroup>().alpha==1&&Time.timeScale==0,"closing TAB returns to paused augment selection");
                dialog.Close();yield return null;Check(Time.timeScale>0,"closing augment resumes gameplay");
                var scroll=FindObjectOfType<PrescriptionScrollView>();scroll.Toggle.onClick.Invoke();yield return new WaitForSecondsRealtime(.5f);
                Check(scroll.Expanded,"prescription opens once");yield return new WaitForSecondsRealtime(3.7f);
                Check(!scroll.Expanded,"prescription auto retracts after four seconds");
                var trapData=Resources.FindObjectsOfTypeAll<TrapMonsterBlueprint>().First();
                int trapPool=Array.FindIndex(level.CurrentLevelBlueprint.monsters,m=>m.monstersPrefab.GetComponent<TrapMonster>()!=null);
                var trap=(TrapMonster)entities.SpawnMonster(trapPool,(Vector2)player.transform.position+Vector2.right*3,trapData,0);
                Call(trap,"ActivateTrap",player);
                var bind=PlayerTrapBindRuntime.GetOrCreate(player);Check(bind.IsBound,"trap binds Hyuki");
                var locked=player.transform.position;var frozen=trap.gameObject.AddComponent<NeuralBlockedMonsterStatus>();frozen.ApplyIce(3);
                Check(trap.enabled&&bind.IsBound,"freezing trap does not invoke captive release");
                int charges=player.CurrentDashCharges;Check(!player.TryDash()&&player.CurrentDashCharges==charges,"dash input cannot free captive or consume charge");
                player.GetComponent<Rigidbody2D>().velocity=Vector2.right*5;yield return new WaitForSeconds(.2f);
                Check(Vector3.Distance(locked,player.transform.position)<.01f&&bind.IsBound,"physics and vine position stay locked");
                bind.Release(trap);trap.gameObject.SetActive(false);
                var sniperData=Resources.FindObjectsOfTypeAll<SniperMonsterBlueprint>().First();
                int sniperPool=Array.FindIndex(level.CurrentLevelBlueprint.monsters,m=>m.monstersPrefab.GetComponent<SniperMonster>()!=null);
                var snipers=new List<Monster>();for(int i=0;i<8;i++){var m=entities.SpawnMonster(sniperPool,(Vector2)player.transform.position+new Vector2(8,i),sniperData,0);if(m!=null)snipers.Add(m);}
                Check(snipers.Count==5,"eight requested field snipers produce only five");foreach(var m in snipers)m.gameObject.SetActive(false);
                var director=FindObjectOfType<MiniStageDirector>();bool enhanced=director.CurrentEnhanced;
                Set(director,"<CurrentEnhanced>k__BackingField",true);
                var roomObject=new GameObject("QA sniper room");var room=roomObject.AddComponent<MiniStageSniperRoom>();
                Set(room,"director",director);Set(room,"entityManager",entities);Set(room,"playerCharacter",player);
                Set(room,"sniperBlueprint",sniperData);Set(room,"sniperMonsterPoolIndex",sniperPool);Set(room,"sniperSpawnCountOverride",10);
                Call(room,"OnBeginRoom");var roomSnipers=(List<Monster>)Get(room,"spawnedSnipers");
                Check(roomSnipers.Count==5,"hero sniper room is also capped at five");
                Call(room,"OnSniperFired",roomSnipers[0] as SniperMonster);Check((float)Get(room,"teleportAt")<0,"entry grants six seconds before teleport");
                Set(room,"nextTeleportAllowed",Time.time-1);Call(room,"OnSniperFired",roomSnipers[0] as SniperMonster);
                Check((float)Get(room,"teleportAt")>Time.time,"teleport retains warning delay");yield return new WaitForSeconds(.25f);
                Check((float)Get(room,"nextTeleportAllowed")>Time.time+5,"relocation starts a six-second cooldown");
                Call(room,"OnSniperFired",roomSnipers[0] as SniperMonster);Check((float)Get(room,"teleportAt")<0,"repeat shots do not reset or bypass teleport cooldown");
                foreach(var m in roomSnipers)m.gameObject.SetActive(false);Destroy(roomObject);Set(director,"<CurrentEnhanced>k__BackingField",enhanced);
                var portalObject=new GameObject("QA portal");var portal=portalObject.AddComponent<BloodClotMiniStagePortal>();var charge=portalObject.AddComponent<BloodClotOvercharge>();
                Check(!portal.ChallengeInProgress,"normal portal initially available");
                Set(charge,"<Started>k__BackingField",true);Check(portal.ChallengeInProgress,"in-progress overcharge blocks entry");
                Set(charge,"<Enhanced>k__BackingField",true);Check(!portal.ChallengeInProgress,"completed overcharge unlocks entry");Destroy(portalObject);
                var shark=AssetDatabase.LoadAssetAtPath<MonsterBlueprint>("Assets/Prefabs/Monsters/genral Monster BP/3_1.asset");
                var target=entities.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*2,shark,500);target.enabled=false;
                var ice=target.gameObject.AddComponent<NeuralBlockedMonsterStatus>();ice.ApplyIce(4);yield return null;
                var prison=target.GetComponentsInChildren<SpriteRenderer>().First(x=>x.name=="Translucent ice prison");
                Check(Mathf.Abs(prison.transform.lossyScale.x-prison.transform.lossyScale.y)<.001f,"ice aspect ratio remains uniform");
                var shadow=(GameObject)Get(target,"shadow");var body=SyringeAugmentVfx.FindTarget(target);
                Check(Mathf.Abs(shadow.transform.position.x-body.bounds.center.x)<.05f,"shark shadow horizontally centred");
                Capture("04-ice-and-shadow");target.gameObject.SetActive(false);
                var food=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MiniStages/PF_FallingFoodStrike.prefab"),player.transform.position+Vector3.right*2,Quaternion.identity).GetComponent<MiniStageFallingFoodStrike>();
                food.Setup(player,shark.walkSpriteSequence[0],Vector2.one,0,1,2,3,0,Vector2.zero,null);yield return new WaitForSeconds(.3f);
                Check(food.GetComponentInChildren<LineRenderer>().positionCount==48,"food uses explicit hit boundary");Capture("05-food-landing-warning");
                PlayQa1004Smoke.Complete();
            }
            finally {GamePreferences.Apply(prefs,false);CrossSceneData.CharacterBlueprint=original;Time.timeScale=1;}
        }
    }
}
