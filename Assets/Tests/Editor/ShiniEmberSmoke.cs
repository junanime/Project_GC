using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using P=Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;
namespace Vampire.Editor {
[InitializeOnLoad] public static class ShiniEmberSmoke {
const string Key="ShiniEmberSmoke";
const string Out="Library/ShiniEmberSmoke";
static bool started; static double deadline; static int checks;
[Serializable] class Catalog {public TutorialGuide.Entry[] entries;}
static ShiniEmberSmoke(){if(SessionState.GetBool(Key,false))Attach();}
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
static void SelectShini(){if(SessionState.GetBool(Key,false))CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Shini Character Blueprint.asset");}
public static void RunUI(){Run();SessionState.SetBool(Key+"UI",true);}
public static void Run(){
 SessionState.SetBool(Key+"UI",false);
 Directory.CreateDirectory(Out);
 var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
 var bp=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Shini Character Blueprint.asset");
 var types=Enum.GetValues(typeof(P)).Cast<P>().ToArray();
 var keys=catalog.entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins",StartingNeedleSelection.SaveKey(bp)}).Concat(types.Select(t=>"LobbyUnlock.Needle."+t)).Concat(types.Select(StartingNeedleSelection.EnabledKey)).ToArray();
 SessionState.SetString(Key+"Keys",string.Join("|",keys));
 File.WriteAllLines(Out+"/prefs-before.tsv",keys.Select(k=>k+"\t"+PlayerPrefs.HasKey(k)+"\t"+PlayerPrefs.GetInt(k)));foreach(var k in keys){SessionState.SetBool(Key+k+"Exists",PlayerPrefs.HasKey(k));SessionState.SetInt(Key+k,PlayerPrefs.GetInt(k));}
 foreach(var e in catalog.entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
 foreach(var t in types){PlayerPrefs.SetInt("LobbyUnlock.Needle."+t,t==P.WindNeedle?1:0);PlayerPrefs.SetInt(StartingNeedleSelection.EnabledKey(t),t==P.WindNeedle?1:0);}
 PlayerPrefs.SetInt(StartingNeedleSelection.SaveKey(bp),(int)P.WindNeedle);
 CrossSceneData.CharacterBlueprint=bp;CrossSceneData.ClearStartingLobbyItems();CrossSceneData.ClearPendingRunSceneTransfer();
 SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
 EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();
}
static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+600;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
static void Tick(){
 if(!SessionState.GetBool(Key,false))return;
 if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("SHINI_EMBER_TIMEOUT");}
 if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false)){
  if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
  foreach(var k in SessionState.GetString(Key+"Keys","").Split('|')){if(SessionState.GetBool(Key+k+"Exists",false))PlayerPrefs.SetInt(k,SessionState.GetInt(Key+k,0));else PlayerPrefs.DeleteKey(k);}PlayerPrefs.Save();
  bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);Debug.Log("SHINI_EMBER_FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
 }
 var level=Object.FindObjectOfType<LevelManager>();if(!started&&EditorApplication.isPlaying&&level?.PlayerCharacter!=null){started=true;level.StartCoroutine(Checks(level));}
}
static void Check(bool ok,string s){if(!ok)throw new Exception(s);checks++;Debug.Log("SKILL_CHECK "+s);}
static object Get(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f.GetValue(o);}throw new Exception("field "+n);}
static void Set(object o,string n,object v){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null){f.SetValue(o,v);return;}}throw new Exception("field "+n);}

static void Near(float actual,float expected,string label){Check(Mathf.Abs(actual-expected)<.02f,label+" actual="+actual+" expected="+expected);}
static void SeedFailure(float chance){for(int i=0;i<10000;i++){UnityEngine.Random.InitState(i);if(UnityEngine.Random.value>=chance){UnityEngine.Random.InitState(i);return;}}throw new Exception("seed");}
static IEnumerator Checks(LevelManager level){
 var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;Time.timeScale=1;
 var tutorial=Object.FindObjectOfType<TutorialGuide>();if(tutorial!=null){tutorial.Close();tutorial.enabled=false;}
 foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn)b.enabled=false;
 level.enabled=false;level.EntityManager.enabled=false;level.EntityManager.StopAllCoroutines();foreach(var npc in Object.FindObjectsOfType<NPCSpawner>()){npc.enabled=false;npc.StopAllCoroutines();}
 var player=level.PlayerCharacter;Set(player,"isInvincible",true);var needle=Object.FindObjectOfType<SyringeDartAbility>();needle.enabled=false;needle.StopAllCoroutines();
 foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.gameObject.SetActive(false);
 var skill=player.Skills;var breath=skill.Breath;Check(skill.IsShini&&breath!=null,"new Shini runtime installed");
 Check(player.GetComponent<ShiniSkillRuntime>()==null&&player.GetComponent<ShiniPhoenixWrapVisual>()==null,"retired lava and tornado never activated");
 var art=Resources.Load<ShiniEmberArt>("ShiniEmberArt");Check(art.cast.Length==12&&art.flames.Length==8&&art.digits.Length==10,"all approved animation and numeral frames loaded");
 Check(skill.Definition.passiveName=="잘 먹겠습니다!"&&skill.Definition.activeName=="후우우—!","approved skill names");
 Check(ShiniEmberRuntime.ShouldDrop(.68999f)&&!ShiniEmberRuntime.ShouldDrop(.69f),"69 percent exact drop boundary");
 for(int rank=1;rank<=3;rank++){skill.RestoreLevels(rank,rank);Check(breath.Capacity==24+6*(rank-1),"capacity rank "+rank);Near(breath.DamagePower,1+.2f*(rank-1),"damage rank "+rank);}
 Check(!skill.TryUpgrade(false)&&!skill.TryUpgrade(true),"Shini both skills stop at rank 3");
 skill.RestoreLevels(1,1);breath.RestoreFuel(5);Check(!skill.TryActivate()&&breath.Fuel==5,"insufficient fuel has no side effects");
 breath.RestoreFuel(24);Check(breath.Tap()&&breath.Fuel==18,"tap debits six once");breath.Advance(.9f);Check(breath.Emitting,"tap starts after inflated chest anticipation");
 breath.Advance(1.6f);Check(breath.Fuel==18&&!breath.Emitting,"tap ends without sustained charge");breath.Advance(.6f);Check(!breath.Busy,"tap recovers");
 breath.RestoreFuel(24);Check(breath.Press(),"hold starts");breath.Advance(ShiniEmberRuntime.Preparation+2.01f);Check(breath.Fuel==15&&breath.Emitting,"hold buys three quarter-second intervals after 1.5 seconds");breath.ReleasePointer();breath.Advance(.02f);Check(!breath.Emitting,"release stops channel");breath.Advance(.5f);Check(!breath.Busy,"hold recovers");
 breath.RestoreFuel(6);Check(breath.Press(),"last six can cast");breath.Advance(3);Check(!breath.Busy&&breath.Fuel==0,"empty fuel ends cleanly even on long frame");
 breath.RestoreFuel(24);Check(!breath.AddFuel(),"cap rejects extra collection");breath.SpawnEmber(player.CenterTransform.position);int drops=breath.GroundCount;yield return new WaitForSeconds(.12f);Check(breath.GroundCount==drops,"full-cap pickup stays on ground");
 breath.RestoreFuel(23);yield return new WaitForSeconds(.4f);Check(breath.Fuel==24&&breath.GroundCount==drops-1,"pickup fills freed capacity using pickup range");
 breath.RestoreFuel(17);var snapshot=player.CaptureRunSceneState();breath.RestoreFuel(0);player.RestoreRunSceneState(snapshot);Check(breath.Fuel==17,"scene transfer retains fuel");
 Check(ShiniEmberRuntime.InCone(Vector2.right*3,Vector2.right)&&!ShiniEmberRuntime.InCone(Vector2.left,Vector2.right)&&!ShiniEmberRuntime.InCone(Vector2.right*5,Vector2.right),"fan hits front only and respects range");
 var data=level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];var front=level.EntityManager.SpawnMonster(0,(Vector2)player.CenterTransform.position+Vector2.right*2,data,0);front.enabled=false;Set(front,"currentHealth",10000f);var rear=level.EntityManager.SpawnMonster(0,(Vector2)player.CenterTransform.position-Vector2.right*2,data,0);rear.enabled=false;Set(rear,"currentHealth",10000f);Physics2D.SyncTransforms();
 Set(player,"lookDirection",Vector2.right);breath.RestoreFuel(24);breath.Tap();breath.Advance(1.1f);Check(front.HP<10000&&rear.HP==10000,"actual cone damages front but not rear");breath.Cancel();front.gameObject.SetActive(false);rear.gameObject.SetActive(false);
 var button=Object.FindObjectsOfType<UnityEngine.UI.Button>().First(b=>b.name=="Active skill R");var input=button.GetComponent<ShiniSkillButton>();Check(input!=null&&button.GetComponent<ApothecaryButtonFeedback>()!=null,"hold input preserves original button feedback");
 breath.RestoreFuel(24);var e=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){pointerId=42,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};input.OnPointerDown(e);Check(breath.Busy&&breath.Fuel==18,"HUD pointer down casts once");input.OnPointerUp(e);button.onClick.Invoke();Check(breath.Fuel==18,"pointer click does not double spend");
 yield return new WaitForSeconds(.64f);CaptureView("01-inflated-chest-and-digits");yield return new WaitForSeconds(.65f);CaptureView("02-fan-flame");
 var digits=player.GetComponentsInChildren<SpriteRenderer>().Where(x=>x.name.StartsWith("Shini flame fuel digit")).ToArray();Check(digits.Count(x=>x.enabled)==2,"two overhead digits for fuel 18");Check(digits[0].sprite==art.digits[1]&&digits[1].sprite==art.digits[8],"overhead digits match ledger");
 breath.Cancel();breath.RestoreFuel(9);yield return null;Check(digits.Count(x=>x.enabled)==1&&digits[0].sprite==art.digits[9],"single digit updates without leading zero");
 breath.RestoreFuel(24);breath.Press();Time.timeScale=0;yield return null;Check(!breath.Busy,"pause clears held input");Time.timeScale=1;
 var ui=ApothecaryUI.Instance;ui.OpenSkillHelp();yield return null;var detail=ui.GetType().GetMethod("ShowSkillDetails",BindingFlags.Instance|BindingFlags.NonPublic);detail.Invoke(ui,new object[]{player.Blueprint,false});yield return null;CaptureView("03-passive-description");Check(Object.FindObjectsOfType<TMPro.TextMeshProUGUI>().Any(t=>t.text.Contains("69%")),"existing passive details panel shows new description");
 detail.Invoke(ui,new object[]{player.Blueprint,true});yield return null;CaptureView("04-active-description");Check(Object.FindObjectsOfType<TMPro.TextMeshProUGUI>().Any(t=>t.text.Contains("0.25초")),"existing active details panel shows consumption");ui.CloseRunBook();yield return null;
 var original=player.Blueprint;var bp=Object.Instantiate(original);Set(player,"characterBlueprint",bp);
 foreach(var key in new[]{"AshiSkills","AriSkills","HyukiSkills"}){bp.skills=Resources.Load<CharacterSkillDefinition>(key);for(int rank=1;rank<=5;rank++){skill.RestoreLevels(rank,rank);if(key=="AshiSkills"){Near(skill.EffectiveCooldown,45-4*(rank-1),"Ashi cooldown rank "+rank);Near(skill.EffectiveActiveDuration,12,"Ashi fixed duration");}else if(key=="AriSkills"){Near(skill.EffectiveActiveDuration,8+2*(rank-1),"Ari duration rank "+rank);Near(skill.ActivePower,1,"Ari damage fixed");}else{Near(skill.StatusDamageBonus,.1f*rank,"Hyuki bonus rank "+rank);Near(skill.EffectiveCooldown,60/Mathf.Pow(1.3f,rank-1),"Hyuki cooldown unchanged");}}}
 Set(player,"characterBlueprint",original);Object.Destroy(bp);skill.RestoreLevels(1,1);breath.RestoreFuel(24);breath.Press();breath.enabled=false;Check(!breath.Busy,"disable cancels channel");
 Debug.Log("SHINI_EMBER_CHECKS "+checks);SessionState.SetBool(Key+"Done",true);
}
        static void CaptureView(string filename)
        {
            var camera=Camera.main;
            if(camera==null) { Debug.LogWarning("[Ver4Smoke] No camera for capture"); return; }
            int width=Mathf.Max(1,Screen.width),height=Mathf.Max(1,Screen.height);
            var overlays=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas && c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var oldCameras=overlays.Select(c=>c.worldCamera).ToArray();
            var oldDistances=overlays.Select(c=>c.planeDistance).ToArray();
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
            Texture2D pixels=null;
            try
            {
                camera.targetTexture=target;
                foreach(var canvas in overlays)
                { canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.1f; }
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                System.IO.File.WriteAllBytes(Out+"/"+filename+".png",pixels.EncodeToPNG());
            }
            finally
            {
                for(int i=0;i<overlays.Length;i++)
                { overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=oldCameras[i];overlays[i].planeDistance=oldDistances[i]; }
                camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(target);
                if(pixels!=null)UnityEngine.Object.Destroy(pixels);
                Canvas.ForceUpdateCanvases();
            }
        }


}
}
