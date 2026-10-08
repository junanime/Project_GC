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
[InitializeOnLoad] public static class AshiBalanceSmoke {
const string Key="AshiBalanceSmoke";
const string Out="Library/AshiBalanceSmoke";
static bool started; static double deadline; static int checks;
[Serializable] class Catalog {public TutorialGuide.Entry[] entries;}
static AshiBalanceSmoke(){if(SessionState.GetBool(Key,false))Attach();}
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
 if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("ASHI_AUDIT_TIMEOUT");}
 if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false)){
  if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
  foreach(var k in SessionState.GetString(Key+"Keys","").Split('|')){if(SessionState.GetBool(Key+k+"Exists",false))PlayerPrefs.SetInt(k,SessionState.GetInt(Key+k,0));else PlayerPrefs.DeleteKey(k);}PlayerPrefs.Save();
  bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);Debug.Log("ASHI_AUDIT_FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
 }
 var level=Object.FindObjectOfType<LevelManager>();if(!started&&EditorApplication.isPlaying&&level?.PlayerCharacter!=null){started=true;level.StartCoroutine(Checks(level));}
}
static void Check(bool ok,string s){if(!ok)throw new Exception(s);checks++;Debug.Log("ASHI_CHECK "+s);}
static object Get(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f.GetValue(o);}throw new Exception("field "+n);}
static void Set(object o,string n,object v){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null){f.SetValue(o,v);return;}}throw new Exception("field "+n);}
static void Original(Ver4AugmentRuntime rt,int[] ranks){var p=new OriginalAugmentProgress();p.RegisterOwnedParent(P.WindNeedle.ToString());for(int i=0;i<3;i++)for(int n=0;n<ranks[i];n++)p.TrySelect(P.WindNeedle.ToString(),i);Set(rt,"<Progress>k__BackingField",p);rt.RefreshProgress();}
static void ResetStats(Character p,PlayerGeneralStatRuntime stats){Set(p,"damageMultiplier",1f);Set(p,"attackSpeedMultiplier",1f);Set(p,"critChance",0f);Set(p,"additionalProjectiles",0);Set(p,"projectileSpeedMultiplier",1f);Set(p,"rangeMultiplier",1f);Set(p,"projectileSizeMultiplier",1f);Set(stats,"critDamageMultiplier",1.5f);Set(stats,"knockbackMultiplier",1f);p.Skills.Restore(0,0,0);}
static IEnumerator Checks(LevelManager level){
 var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;Time.captureDeltaTime=1f/60f;Time.timeScale=1;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
 var tutorial=Object.FindObjectOfType<TutorialGuide>();if(tutorial!=null){tutorial.Close();tutorial.enabled=false;}
 foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn)b.enabled=false;
 level.enabled=false;level.EntityManager.enabled=false;level.EntityManager.StopAllCoroutines();foreach(var npc in Object.FindObjectsOfType<NPCSpawner>()){npc.enabled=false;npc.StopAllCoroutines();}
 var player=level.PlayerCharacter;Set(player,"isInvincible",true);player.CollectableCollider.enabled=false;Set(player,"nextLevelExp",1000000f);Set(player,"expToNextLevel",1000000f);
 var manager=Object.FindObjectOfType<AbilityManager>();var needle=SyringeAbilityResolver.FindOwnedOrFirst(manager);var rt=manager.Ver4;var stats=PlayerGeneralStatRuntime.GetOrCreate(player);
 foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.gameObject.SetActive(false);
 if(SessionState.GetBool(Key+"UI",false)){
  needle.enabled=false;needle.StopAllCoroutines();Screen.SetResolution(1920,1080,false);yield return null;
  var ui=level.EntityManager.AbilitySelectionDialog;ui.OpenOvercharge();yield return new WaitForSecondsRealtime(1);
  CaptureView("overcharge-original");ui.RerollAbilities();yield return new WaitForSecondsRealtime(1);CaptureView("overcharge-reroll");ui.Close();
  Original(rt,new[]{3,3,3});ui.OpenOvercharge();yield return new WaitForSecondsRealtime(1);CaptureView("overcharge-complete");ui.Close();
  SessionState.SetBool(Key+"Done",true);yield break;
 }
 Check(player.Blueprint.name=="아시"&&player.Skills.Definition.kind==CharacterSkillDefinition.SkillKind.Ashi,"actual Ashi scene actor");
 var parents=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true);var wind=parents.Single(p=>p.Type==P.WindNeedle);
 Check(wind.Owned,"Wind is Ashi starting weapon");needle.enabled=false;needle.StopAllCoroutines();
 foreach(var t in new[]{P.Honey,P.WoodNeedle}){
  var retired=parents.Single(p=>p.Type==t);PlayerPrefs.SetInt("LobbyUnlock.Needle."+t,1);PlayerPrefs.SetInt(StartingNeedleSelection.EnabledKey(t),1);
  Check(StartingNeedleSelection.Owned(retired)&&!StartingNeedleSelection.Enabled(retired)&&!manager.AcquireVer4Ability(retired),"owned remake weapon cannot activate "+t);
  Check(!StartingNeedleSelection.SetEnabled(retired,true,Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig")),"toggle cannot enable "+t);
  PlayerPrefs.SetInt(StartingNeedleSelection.SaveKey(player.Blueprint),(int)t);Check(StartingNeedleSelection.SelectedId(player.Blueprint)==(int)P.WindNeedle,"retired starting selection falls back "+t);
 }
 PlayerPrefs.SetInt(StartingNeedleSelection.SaveKey(player.Blueprint),(int)P.WindNeedle);
 // Measured live emission, including serialized per-projectile spacing.
 float baseline=0,windFast=0,hungry=0;ResetStats(player,stats);Original(rt,new[]{0,0,0});
 yield return SampleCadence(needle,player,"windAS0",v=>baseline=v);
 Original(rt,new[]{3,0,0});yield return SampleCadence(needle,player,"windAS3",v=>windFast=v);
 Set(needle,"hungerNeedleEnabled",true);for(int i=0;i<8;i++)HungerNeedleRuntime.RegisterHit(player,1000,.04f,8,false);
 yield return SampleCadence(needle,player,"windAS3_hunger8",v=>hungry=v);HungerNeedleRuntime.Clear(player);Set(needle,"hungerNeedleEnabled",false);
 Check(windFast<baseline*.92f&&hungry<windFast*.87f,"Wind and Hunger each accelerate actual volleys");
 Check(Mathf.Abs(baseline-(.7f/1.10f+.1f))<.04f&&Mathf.Abs(windFast-(.7f/1.34f+.1f))<.04f,"actual volleys match effective cooldown plus shot spacing");
 // Live damage against an immobile 2500-HP mini-boss using the actual projectile path.
 int idx=Array.FindIndex(level.CurrentLevelBlueprint.monsters,c=>c.monstersPrefab.GetComponent<AcidToadMonster>()!=null);
 var bp=level.CurrentLevelBlueprint.monsters[idx].monsterBlueprints[0];
 var dummy=(AcidToadMonster)level.EntityManager.SpawnMonster(idx,(Vector2)player.transform.position+Vector2.right*3,bp,0,false);dummy.AutoPatterns=false;dummy.enabled=false;
 player.Move(Vector2.right);yield return null;player.Move(Vector2.zero);
 float hp=dummy.HP;needle.enabled=true;yield return new WaitForSeconds(8);needle.enabled=false;needle.StopAllCoroutines();
 Check(dummy.HP<hp,"Ashi live projectiles damage mini-boss");Debug.Log($"ASHI_BATTLE seconds=8 startHP={hp} remainingHP={dummy.HP} damage={hp-dummy.HP}");
 for(int rank=0;rank<=3;rank++){
  var prog=new OriginalAugmentProgress();prog.RegisterOwnedParent(P.FireNeedle.ToString());for(int i=0;i<rank;i++)prog.TrySelect(P.FireNeedle.ToString(),2);
  var status=dummy.gameObject.AddComponent<Ver4NeedleStatus>();var combat=new SyringeSpecialRuntime{ver4=new Ver4CombatSnapshot(prog),ver4HitDamage=100};
  for(int i=0;i<40;i++)status.ApplyFire(combat,player,true);Check(status.BurnStacks==3+rank,"40 fire applications respect cap at rank "+rank);Object.DestroyImmediate(status);
 }
 // Harvest uses living stacks at death, cannot pay twice, and resets on pooling.
 var bacteria=dummy.gameObject.AddComponent<GutBacteriaStatus>();bacteria.HarvestRank=3;
 for(int i=0;i<8;i++)bacteria.Apply(6,3,8,1,GemType.White1,0,false);
 Check(bacteria.CurrentStacks==8&&bacteria.ExtraGemChance==1,"8 stacks guarantee exactly one additional harvest gem");
 int gems=Object.FindObjectsOfType<ExpGem>().Length;dummy.OnKilled.Invoke(dummy);Check(Object.FindObjectsOfType<ExpGem>().Length==gems+2,"qualified kill pays basic and extra gem");
 dummy.OnKilled.Invoke(dummy);Check(Object.FindObjectsOfType<ExpGem>().Length==gems+2,"duplicate death cannot duplicate harvest");Object.DestroyImmediate(bacteria);
 bacteria=dummy.gameObject.AddComponent<GutBacteriaStatus>();bacteria.HarvestRank=3;for(int i=0;i<8;i++)bacteria.Apply(6,3,8,1,GemType.White1,0,false);
 var expiries=(List<float>)Get(bacteria,"stackExpireTimes");for(int i=0;i<5;i++)expiries[i]=Time.time-1;
 Check(bacteria.CurrentStacks==3&&bacteria.ExtraGemChance==0,"expired excess stacks cannot keep cached harvest chance");
 gems=Object.FindObjectsOfType<ExpGem>().Length;dummy.OnKilled.Invoke(dummy);Check(Object.FindObjectsOfType<ExpGem>().Length==gems+1,"three remaining stacks pay only basic reward");
 dummy.gameObject.SetActive(false);Check(bacteria.CurrentStacks==0&&bacteria.HarvestRank==0,"pooled status clears harvest state");
 Original(rt,new[]{0,0,0});
 UnityEngine.Random.InitState(29019);int normalWithout=0,multiple=0;
 for(int i=0;i<400;i++){
  var offers=rt.CreateOffers(false,3,true);var cards=offers.Cast<Ver4AugmentOffer>().ToArray();var originals=cards.Where(c=>c.Kind==Ver4RewardKind.Original).ToArray();
  Check(cards.Length==3&&originals.Length>=1&&originals.Select(c=>c.Parent.Type+"/"+c.Option).Distinct().Count()==originals.Length,"guaranteed distinct originals #"+i);
  if(originals.Length>1)multiple++;manager.ReturnAbilities(offers);
  offers=rt.CreateOffers(false,3);if(offers.Cast<Ver4AugmentOffer>().All(c=>c.Kind!=Ver4RewardKind.Original))normalWithout++;manager.ReturnAbilities(offers);
 }
 Check(multiple>0&&normalWithout>0&&rt.Progress.Level("WindNeedle")==0,"other slots retain random pool; previews never grant originals");
 // Actual UI reroll, one-click acquisition and queued chest contexts.
 var dialog=level.EntityManager.AbilitySelectionDialog;
 dialog.OpenOvercharge();Check(manager.OriginalRewardContext&&Cards(dialog).Any(c=>c.Kind==Ver4RewardKind.Original),"overcharge dialog guarantees original");
 dialog.RerollAbilities();Check(Cards(dialog).Any(c=>c.Kind==Ver4RewardKind.Original)&&rt.Progress.Level("WindNeedle")==0,"reroll retains guarantee without applying previews");
 var selected=Cards(dialog).First(c=>c.Kind==Ver4RewardKind.Original);selected.Select();selected.Select();Check(rt.Progress.Level("WindNeedle")==1,"double selection grants one rank");
 dialog.Close();Check(!manager.OriginalRewardContext&&!manager.LegendaryRewardContext,"close clears both reward contexts");
 var chestBp=RemakeBalance.Current.levelUpChest;var first=level.EntityManager.SpawnChest(chestBp,player.transform.position+Vector3.up*6);first.GuaranteesOriginal=true;
 var second=level.EntityManager.SpawnChest(chestBp,player.transform.position+Vector3.up*7);Check(!second.GuaranteesOriginal,"ordinary chest starts without guarantee");
 first.OpenChest();second.OpenChest();int dialogs=0,guaranteedDialogs=0;float end=Time.realtimeSinceStartup+12;
 while(dialogs<2&&Time.realtimeSinceStartup<end){if(dialog.MenuOpen){dialogs++;if(manager.OriginalRewardContext){guaranteedDialogs++;Check(Cards(dialog).Any(c=>c.Kind==Ver4RewardKind.Original),"actual overcharge chest offers original");}dialog.Close();}yield return null;}
 Check(dialogs==2&&guaranteedDialogs==1&&!manager.OriginalRewardContext,"overlapping chests queue two rewards with isolated contexts");
 var pooled=level.EntityManager.SpawnChest(chestBp,player.transform.position+Vector3.up*8);pooled.GuaranteesOriginal=true;pooled.Setup(chestBp);Check(!pooled.GuaranteesOriginal,"chest setup clears pooled guarantee");level.EntityManager.DespawnChest(pooled);
 // Complete all 19 active parents through real acquisition handlers.
 foreach(var parent in parents.Where(p=>StartingNeedleSelection.Available(p.Type))){PlayerPrefs.SetInt("LobbyUnlock.Needle."+parent.Type,1);PlayerPrefs.SetInt(StartingNeedleSelection.EnabledKey(parent.Type),1);if(!parent.Owned)Check(manager.AcquireVer4Ability(parent),"acquire "+parent.Type);}
 rt.RefreshProgress();
 foreach(var parent in parents.Where(p=>StartingNeedleSelection.Available(p.Type)))for(int option=0;option<3;option++)while(rt.Progress.CanSelect(parent.Type.ToString(),option)){
  var offer=Offer(rt,parent,Ver4RewardKind.Original,option,0,AugmentUpgradeGrade.Original);offer.Select();Check(!offer.RequirementsMet(),"consume original "+parent.Type+"/"+option);Object.Destroy(offer.gameObject);
 }
 int legendary=0,supreme=0;
 for(int i=0;i<1000;i++){var offers=rt.CreateOffers(false,3,true);foreach(var c in offers.Cast<Ver4AugmentOffer>()){Check(c.Kind==Ver4RewardKind.Numeric,"exhausted originals fall back to numeric");if(c.Grade==AugmentUpgradeGrade.Legendary)legendary++;else if(c.Grade==AugmentUpgradeGrade.Supreme)supreme++;else throw new Exception("completed parent rolled low grade");}manager.ReturnAbilities(offers);}
 Check(legendary>1950&&legendary<2250&&legendary+supreme==3000,"live completed distribution near 70/30");Debug.Log($"ASHI_COMPLETED_DISTRIBUTION legendary={legendary} supreme={supreme}");
 dialog.OpenOvercharge();Check(Object.FindObjectsOfType<TMPro.TextMeshProUGUI>().Any(t=>t.text.Contains("오리지날 강화 완료"))&&Cards(dialog).Length==3,"exhausted guarantee explains fallback");dialog.RerollAbilities();Check(Cards(dialog).Length==3,"exhausted reroll keeps three choices");dialog.Close();
 var numeric=Offer(rt,wind,Ver4RewardKind.Numeric,0,.23f,AugmentUpgradeGrade.Legendary);numeric.Select();Object.Destroy(numeric.gameObject);
 var saved=rt.CaptureRunSceneConditionalAugmentIds();float damage=player.DamageMultiplier;
 Check(rt.RestoreRunSceneConditionalAugments(saved)&&player.DamageMultiplier==damage,"same-run restore is idempotent");
 // Actual mini-stage entry/exit preserves every acquisition. Test the room's chest producer too.
 foreach(var m in level.EntityManager.LivingMonsters.ToArray())if(m!=null)m.gameObject.SetActive(false);
 var director=Object.FindObjectOfType<MiniStageDirector>();director.OpenEntrancePortal(player.transform.position+Vector3.right);
 var entry=Object.FindObjectsOfType<BloodClotMiniStagePortal>().First(p=>!p.Reserved);var charge=entry.GetComponent<BloodClotOvercharge>();typeof(BloodClotOvercharge).GetProperty("Kind").SetValue(charge,BloodClotOvercharge.Challenge.Stay);
 Check(charge.Begin(),"begin overcharge");charge.Advance(charge.Target);director.EnterMiniStageFromPortal(entry);
 end=Time.realtimeSinceStartup+15;while(director.IsTransitioning&&Time.realtimeSinceStartup<end)yield return null;
 Check(director.IsInsideMiniStage&&director.CurrentEnhanced,"enter enhanced mini-stage");
 Check(rt.CaptureRunSceneConditionalAugmentIds().SequenceEqual(saved)&&player.DamageMultiplier==damage,"mini-stage preserves acquired originals and exact numeric values");
 var room=director.CurrentRoom;typeof(MiniStageRoomBase).GetMethod("SpawnRewardChest",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(room,null);
 var roomChest=(Chest)Get(room,"activeRewardChest");Check(roomChest!=null&&roomChest.GuaranteesOriginal,"enhanced room produces guaranteed chest");roomChest.OpenChest();
 end=Time.realtimeSinceStartup+8;while(!dialog.MenuOpen&&Time.realtimeSinceStartup<end)yield return null;Check(dialog.MenuOpen&&manager.OriginalRewardContext,"real room chest carries context");dialog.Close();
 Set(room,"optionalReturnUnlocked",true);room.ReturnInteractable.SetUnlocked(true);director.ReturnToFieldFromInteractable(room.ReturnInteractable);
 end=Time.realtimeSinceStartup+15;while(director.IsTransitioning&&Time.realtimeSinceStartup<end)yield return null;
 Check(!director.IsInsideMiniStage&&rt.CaptureRunSceneConditionalAugmentIds().SequenceEqual(saved)&&player.DamageMultiplier==damage,"mini-stage return preserves balance state");
 var progression=StageProgression.Ensure(level);progression.FinalBossDefeated(player.transform.position+Vector3.right*4,100);
 Check(progression.TryEnter(player,progression.Portal),"enter stage two through actual travel");end=Time.realtimeSinceStartup+15;while(progression.Travelling&&Time.realtimeSinceStartup<end)yield return null;
 Check(progression.StageNumber==2&&rt.CaptureRunSceneConditionalAugmentIds().SequenceEqual(saved)&&player.DamageMultiplier==damage,"stage two preserves all acquired balance state");
 // A fresh runtime restores legacy retired history without reactivating either parent.
 var legacy=saved.Concat(new[]{"{\"version\":4,\"parent\":\"Honey\",\"original\":true,\"option\":0,\"grade\":3,\"amount\":0}","{\"version\":4,\"parent\":\"WoodNeedle\",\"original\":false,\"option\":0,\"grade\":4,\"amount\":0.24}"}).ToList();
 var copy=new GameObject("Migration runtime").AddComponent<Ver4AugmentRuntime>();copy.Configure(rt.Balance,manager,level.EntityManager,player);
 Check(copy.RestoreRunSceneConditionalAugments(legacy),"legacy retired history does not reject active state");
 Check(copy.Progress.Level("WindNeedle")==9&&!copy.Combat.Has(P.Honey)&&!copy.Combat.Has(P.WoodNeedle),"legacy migration restores active originals only");
 float migratedDamage=player.DamageMultiplier;Check(copy.RestoreRunSceneConditionalAugments(legacy)&&player.DamageMultiplier==migratedDamage,"migrated shared numeric values cannot apply twice");
 Check(Mathf.Abs(migratedDamage-damage-.47f)<.0001f,"legacy saved .24 plus active .23 restore without grade reroll");Object.Destroy(copy.gameObject);needle.Ver4=rt;
 Debug.Log("ASHI_BALANCE_CHECKS "+checks);SessionState.SetBool(Key+"Done",true);
}
static Ver4AugmentOffer[] Cards(AbilitySelectionDialog dialog)=>((List<Ability>)Get(dialog,"displayedAbilities")).Cast<Ver4AugmentOffer>().ToArray();
static Ver4AugmentOffer Offer(Ver4AugmentRuntime rt,Ability parent,Ver4RewardKind kind,int option,float amount,AugmentUpgradeGrade grade){var offer=new GameObject("Balance test offer").AddComponent<Ver4AugmentOffer>();offer.Configure(rt,kind,grade,parent,option,amount,"test","test");return offer;}
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


static IEnumerator SampleCadence(SyringeDartAbility needle,Character p,string label,Action<float> callback){
 needle.enabled=false;needle.StopAllCoroutines();p.Skills.Restore(0,0,0);Set(needle,"timeSinceLastAttack",0f);needle.enabled=true;
 var times=new List<float>();float previous=0;float start=Time.time;float reported=needle.GetEffectiveCooldown();
 while(Time.time-start<18){yield return null;float now=(float)Get(needle,"timeSinceLastAttack");if(now<previous-.03f)times.Add(Time.time);previous=now;}
 needle.enabled=false;needle.StopAllCoroutines();float mean=times.Count>1?(times.Last()-times.First())/(times.Count-1):0;Check(times.Count>=4,"live volleys observed "+label);
 Debug.Log($"ASHI_CADENCE label={label} volleys={times.Count} interval={mean} count={needle.GetEffectiveProjectileCount()} reportedCooldown={reported} activeProjectiles={Object.FindObjectsOfType<SyringeProjectile>().Length}");callback(mean);
}
}
}
