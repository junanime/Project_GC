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
[InitializeOnLoad] public static class SkillRefreshSmoke {
const string Key="SkillRefreshSmoke";
const string Out="Library/SkillRefreshSmoke";
static bool started; static double deadline; static int checks;
[Serializable] class Catalog {public TutorialGuide.Entry[] entries;}
static SkillRefreshSmoke(){if(SessionState.GetBool(Key,false))Attach();}
public static void RunUI(){Run();SessionState.SetBool(Key+"UI",true);}
public static void Run(){
 SessionState.SetBool(Key+"UI",false);
 Directory.CreateDirectory(Out);
 var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
 var bp=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
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
 if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("SKILL_REFRESH_TIMEOUT");}
 if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false)){
  if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
  foreach(var k in SessionState.GetString(Key+"Keys","").Split('|')){if(SessionState.GetBool(Key+k+"Exists",false))PlayerPrefs.SetInt(k,SessionState.GetInt(Key+k,0));else PlayerPrefs.DeleteKey(k);}PlayerPrefs.Save();
  bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);Debug.Log("SKILL_REFRESH_FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
 }
 var level=Object.FindObjectOfType<LevelManager>();if(!started&&EditorApplication.isPlaying&&level?.PlayerCharacter!=null){started=true;level.StartCoroutine(Checks(level));}
}
static void Check(bool ok,string s){if(!ok)throw new Exception(s);checks++;Debug.Log("SKILL_CHECK "+s);}
static object Get(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f.GetValue(o);}throw new Exception("field "+n);}
static void Set(object o,string n,object v){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null){f.SetValue(o,v);return;}}throw new Exception("field "+n);}

static void Near(float actual,float expected,string label){Check(Mathf.Abs(actual-expected)<.02f,label+" actual="+actual+" expected="+expected);}
static void SeedFailure(float chance){for(int i=0;i<10000;i++){UnityEngine.Random.InitState(i);if(UnityEngine.Random.value>=chance){UnityEngine.Random.InitState(i);return;}}throw new Exception("seed");}
static IEnumerator Checks(LevelManager level){
 var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;Time.timeScale=1;QualitySettings.vSyncCount=0;Application.targetFrameRate=120;
 var tutorial=Object.FindObjectOfType<TutorialGuide>();if(tutorial!=null){tutorial.Close();tutorial.enabled=false;}
 foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn)b.enabled=false;
 level.enabled=false;level.EntityManager.enabled=false;level.EntityManager.StopAllCoroutines();foreach(var npc in Object.FindObjectsOfType<NPCSpawner>()){npc.enabled=false;npc.StopAllCoroutines();}
 var player=level.PlayerCharacter;Set(player,"isInvincible",true);player.CollectableCollider.enabled=false;
 var needle=Object.FindObjectOfType<SyringeDartAbility>();needle.enabled=false;needle.StopAllCoroutines();
 foreach(var projectile in Object.FindObjectsOfType<Projectile>())projectile.gameObject.SetActive(false);
 foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.gameObject.SetActive(false);
 var skill=player.Skills;skill.Restore(0,0,0);skill.RestoreLevels(1,1);yield return null;
 var oldBlueprint=player.Blueprint;var bp=Object.Instantiate(oldBlueprint);Set(player,"characterBlueprint",bp);
 var ashi=Resources.Load<CharacterSkillDefinition>("AshiSkills");var hyuki=Resources.Load<CharacterSkillDefinition>("HyukiSkills");
 Debug.Log("SPRITE_ASSETS Ashi="+ashi.phoenixFrames.Length+" Hyuki="+hyuki.phoenixFrames.Length+" A="+string.Join(",",ashi.phoenixFrames.Select(s=>s!=null?s.name:"null"))+" H="+string.Join(",",hyuki.phoenixFrames.Select(s=>s!=null?s.name:"null")));
 Check(ashi.phoenixFrames.SequenceEqual(hyuki.phoenixFrames)&&ashi.phoenixFrames.Length==16,"Ashi uses exactly the original sixteen ice phoenix sprites");
 Check(ashi.passiveIcon!=null&&ashi.activeIcon!=null&&ashi.passiveWind.Length==8&&ashi.activeWind.Length==8&&ashi.projectileWind.Length==8,"Ashi icons and existing gameplay wind animations preserved");
 Check(skill.TryActivate()&&Time.timeScale==0&&skill.IsCutin,"Ashi retains existing pause during wind summon");
 Check(!skill.TryActivate(),"cannot reenter active while summoning");
 yield return new WaitForSecondsRealtime(.38f);CaptureView("01-wind-phoenix-appear");
 var first=player.GetComponentsInChildren<SpriteRenderer>().First(r=>r.name=="Phoenix current pose");
 Check(first.enabled&&first.sharedMaterial.GetFloat("_WindPalette")==1,"wind palette applied only on Ashi material");
 Check(!Object.FindObjectsOfType<UnityEngine.UI.Image>().Any(i=>i.name=="Ashi cut-in portrait"&&i.gameObject.activeInHierarchy),"retired headband panel stays hidden");
 yield return new WaitForSecondsRealtime(.55f);CaptureView("02-wind-phoenix-wrap");
 var ribbons=player.GetComponentsInChildren<LineRenderer>().Where(r=>r.name.StartsWith("Wind wrap ")).ToArray();
 Check(ribbons.Length==10&&ribbons.All(r=>r.enabled),"five ribbons wrap in separate front and back halves");
 yield return new WaitForSecondsRealtime(.57f);CaptureView("03-wind-phoenix-fade");
 yield return new WaitForSecondsRealtime(.65f);
 Check(!skill.IsCutin&&skill.Active&&Time.timeScale==1,"wind summon completes and restores game time");
 Near(skill.EffectiveActiveDuration,12,"Ashi base buff duration unchanged");Near(skill.EffectiveCooldown,45,"Ashi cooldown unchanged");
 Check(!first.enabled&&ribbons.All(r=>!r.enabled),"phoenix and tornado cleanly disappear");
 skill.Restore(0,0,0);
 // All four characters share Ice Needle pity independently of passive rank.
 foreach(var name in new[]{"AshiSkills","AriSkills","ShiniSkills","HyukiSkills"}){
  bp.skills=Resources.Load<CharacterSkillDefinition>(name);skill.RestoreLevels(1,1);skill.RestoreIceProcFailures(0);
  for(int i=1;i<=6;i++){Check(!skill.RollInstantFreeze(.999f),name+" failed roll "+i);Near(skill.IceProcChance,.1f+.01f*i,name+" pity chance");}
  var snapshot=player.CaptureRunSceneState();skill.RestoreIceProcFailures(0);player.RestoreRunSceneState(snapshot);Check(skill.IceProcFailures==6,name+" snapshot retains pity");
  Check(skill.RollInstantFreeze(0)&&skill.IceProcFailures==0,name+" success resets pity");
  skill.RestoreIceProcFailures(999);Check(skill.IceProcChance==1&&skill.RollInstantFreeze(1),name+" guaranteed upper cap");
 }
 bp.skills=hyuki;skill.RestoreLevels(99,99);Check(skill.PassiveLevel==5&&skill.ActiveLevel==5&&!skill.TryUpgrade(false),"legacy levels clamp passive to five, active stays five");
 Check(hyuki.passiveDescription.Contains("10%")&&hyuki.passiveDescription.Contains("50%"),"Hyuki help describes new passive");
 Check(hyuki.passiveIcon!=null&&hyuki.passiveIcon.name=="HyukiHadaBomyeon","Hyuki keeps approved existing icon");
 var data=level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];
 var enemy=level.EntityManager.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*3,data,0);enemy.enabled=false;Set(enemy,"currentHealth",10000f);
 foreach(var status in enemy.GetComponents<MonoBehaviour>().Where(c=>c is ICombatStatus).ToArray())Object.DestroyImmediate(status);
 Check(CombatStatusRules.ActiveTags(enemy)==CombatStatusTag.None,"clean enemy has no debuff");
 var itemStatus=enemy.gameObject.AddComponent<OctoberItemTargetStatus>();itemStatus.BurnUntil=Time.time+2;Check(CombatStatusRules.ActiveTags(enemy)==CombatStatusTag.Burn,"item burn participates in shared tags");itemStatus.BurnUntil=Time.time-1;Check(CombatStatusRules.ActiveTags(enemy)==CombatStatusTag.None,"expired item burn excluded");Object.DestroyImmediate(itemStatus);
 Near(CombatStatusRules.DamageMultiplier(player,enemy),1,"no bonus on clean enemy");
 var corrosion=enemy.gameObject.AddComponent<CorrosionStatus>();corrosion.Apply(10,.15f,.05f,3,false);
 for(int rank=1;rank<=5;rank++){
  skill.RestoreLevels(rank,1);float bonus=CharacterSkillRuntime.HyukiBonus(rank);Near(CombatStatusRules.DamageMultiplier(player,enemy),1+bonus,"rank "+rank+" bonus");
  float before=enemy.HP;Ver4HitEffects.Damage(enemy,100,Vector2.zero,player,"audit");Near(before-enemy.HP,100*(1+bonus),"rank "+rank+" applied actual direct damage");
  before=enemy.HP;Ver4HitEffects.Damage(enemy,100,Vector2.zero,player,"audit dot",true);Near(before-enemy.HP,100*(1+bonus),"rank "+rank+" applied actual periodic damage");
 }
 var mark=enemy.gameObject.AddComponent<NeedleMarkStatus>();mark.Apply(5);Near(CombatStatusRules.DamageMultiplier(player,enemy),1.5f,"multiple debuffs never multiply passive twice");
 Object.DestroyImmediate(corrosion);Set(mark,"expireTime",Time.time-1);Near(CombatStatusRules.DamageMultiplier(player,enemy),1,"expired mark has no bonus before component removal");
 Object.DestroyImmediate(mark);
 var states=enemy.gameObject.AddComponent<Ver4NeedleStatus>();var progress=new OriginalAugmentProgress();progress.RegisterOwnedParent(P.FireNeedle.ToString());
 var fire=new SyringeSpecialRuntime{ver4=new Ver4CombatSnapshot(progress),ver4HitDamage=100};states.ApplyFire(fire,player,true);
 Check((CombatStatusRules.ActiveTags(enemy)&CombatStatusTag.Burn)!=0,"fire burn tagged");Object.DestroyImmediate(states);
 var honey=enemy.gameObject.AddComponent<HoneySlowStatus>();honey.Apply(5,.8f);Check((CombatStatusRules.ActiveTags(enemy)&CombatStatusTag.Slow)!=0,"honey or boss freeze substitute tagged");Set(honey,"statusExpires",Time.time-1);Near(CombatStatusRules.DamageMultiplier(player,enemy),1,"expired honey no bonus");Object.DestroyImmediate(honey);
 var poison=enemy.gameObject.AddComponent<PoisonStatus>();poison.Apply(5,1,1,player,"audit");Check((CombatStatusRules.ActiveTags(enemy)&CombatStatusTag.Poison)!=0,"poison tagged");Object.DestroyImmediate(poison);
 // Four-stack guarantee preserves failure counter. Freezing active does not change it.
 progress=new OriginalAugmentProgress();progress.RegisterOwnedParent(P.IceNeedle.ToString());var ice=new SyringeSpecialRuntime{ver4=new Ver4CombatSnapshot(progress),ver4HitDamage=100};
 skill.RestoreIceProcFailures(0);
 for(int i=1;i<=4;i++){SeedFailure(skill.IceProcChance);Ver4HitEffects.AfterHit(enemy,ice,player,0,false);Check(skill.IceProcFailures==i,"four-hit guarantee independent of pity "+i);}
 Check(enemy.GetComponent<NeuralBlockedMonsterStatus>().IceFrozen,"fourth failed roll still freezes");
 Near(CombatStatusRules.DamageMultiplier(player,enemy),1.5f,"freeze counts as debuff");
 // Actual syringe path must count the debuff before the breaking hit consumes freeze.
 var shot=new GameObject("Skill refresh test projectile").AddComponent<SyringeProjectile>();Set(shot,"playerCharacter",player);Set(shot,"specials",ice);Set(player,"critChance",0f);Set(shot,"targetLayer",(LayerMask)0);
 float hp=enemy.HP;typeof(SyringeProjectile).GetMethod("DamageTarget",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shot,new object[]{enemy,enemy,100f});
 Near(hp-enemy.HP,180,"frozen hit gets 20 percent shatter and exactly 50 percent passive");
 Object.DestroyImmediate(shot.gameObject);
 foreach(var status in enemy.GetComponents<MonoBehaviour>().Where(c=>c is ICombatStatus).ToArray())Object.DestroyImmediate(status);
 mark=enemy.gameObject.AddComponent<NeedleMarkStatus>();mark.Apply(10);bp.skills=ashi;Near(CombatStatusRules.DamageMultiplier(player,enemy),1,"Ashi never inherits Hyuki passive");bp.skills=hyuki;
 enemy.gameObject.SetActive(false);Near(CombatStatusRules.DamageMultiplier(player,enemy),1,"pooled disabled target does not qualify");enemy.gameObject.SetActive(true);enemy.enabled=false;Near(CombatStatusRules.DamageMultiplier(player,enemy),1,"pool reuse clears old debuffs");
 foreach(var type in new[]{P.IceNeedle,P.FireNeedle,P.CorrosionNeedle,P.Poison,P.Honey})Check(CombatStatusRules.NeedleTags(type)!=CombatStatusTag.None,"needle metadata "+type);
 foreach(var type in new[]{P.WindNeedle,P.HungerNeedle,P.Homing,P.Pierce})Check(CombatStatusRules.NeedleTags(type)==CombatStatusTag.None,"non-debuff metadata "+type);
 Set(player,"characterBlueprint",oldBlueprint);Object.Destroy(bp);skill.RestoreLevels(1,1);skill.Restore(0,0,0);
 Check(skill.TryActivate(),"Ashi active can be used again after reset");yield return null;
 skill.enabled=false;Check(Time.timeScale==1&&!skill.IsCutin,"disabling actor during summon restores time");skill.enabled=true;yield return null;
 Check(ribbons.All(r=>!r.enabled),"cancelled summon clears ribbons");
 Debug.Log("SKILL_REFRESH_CHECKS "+checks);SessionState.SetBool(Key+"Done",true);
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
