using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vampire;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class StageTwoSmoke
{
    const string Key="StageTwoSmoke", Output="Library/StageTwoSmoke";
    static bool started, checkingRewards; static double deadline;
    [Serializable] class Catalog { public TutorialGuide.Entry[] entries; }
    [Serializable] class Saved { public string key; public bool exists; public int value; }
    [Serializable] class Backup { public Saved[] values; }
    static StageTwoSmoke(){if(SessionState.GetBool(Key,false))Attach();}
    public static void Run()
    {
        Directory.CreateDirectory(Output); RestorePrefs();
        var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
        var keys=catalog.entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins"});
        File.WriteAllText(Output+"/prefs-backup.json",JsonUtility.ToJson(new Backup{values=keys.Select(k=>new Saved{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
        foreach(var e in catalog.entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
        EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();
    }
    public static void InstallAndRun() { StageTwoInstaller.Install(); Run(); }
    static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+240;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);Debug.Log("STAGE_TWO_TIMEOUT");}
        if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
        {
            if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            RestorePrefs();bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
            Debug.Log("STAGE_TWO_SMOKE_DONE failed="+failed);EditorApplication.Exit(failed?1:0);return;
        }
        var level=Object.FindObjectOfType<LevelManager>();
        var dialog=Object.FindObjectOfType<AbilitySelectionDialog>();
        if (!checkingRewards && dialog != null && dialog.MenuOpen) dialog.Close();
        if(!started&&EditorApplication.isPlaying&&level?.EntityManager?.LivingMonsters!=null)
        {started=true;level.StartCoroutine(Checks(level));}
    }
    static void RestorePrefs()
    {
        string path=Output+"/prefs-backup.json";if(!File.Exists(path))return;
        foreach(var s in JsonUtility.FromJson<Backup>(File.ReadAllText(path)).values)
        {if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);}
        PlayerPrefs.Save();File.Move(path,Output+"/prefs-restored-"+DateTime.UtcNow.Ticks+".json");
    }
    static FieldInfo Field(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}throw new Exception(n);}
    static void Set(object o,string n,object value)=>Field(o,n).SetValue(o,value);
    static void Check(bool value,string text){if(!value)throw new Exception("STAGE_TWO_FAIL "+text);Debug.Log("STAGE_TWO_PASS "+text);}
    static void Capture(string name)
    {
        var camera=Camera.main;var target=RenderTexture.GetTemporary(1440,900,24);
        var old=camera.targetTexture;var active=RenderTexture.active;
        camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1440,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
        File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());Object.Destroy(image);
        camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);
    }
    static IEnumerator Checks(LevelManager level)
    {
        var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
        if(TutorialGuide.Instance!=null){TutorialGuide.Instance.Close();TutorialGuide.Instance.enabled=false;}Time.timeScale=1;
        var player=level.PlayerCharacter;Set(player,"isInvincible",true);player.Move(Vector2.zero);
        var source=level.CurrentLevelBlueprint;var progression=StageProgression.Ensure(level);
        var definition=Resources.Load<StageTwoDefinition>("StageTwoDefinition");Check(definition!=null&&definition.IsReady,"complete 9-shell content");
        Check(Mathf.Abs(VisibleBodyGeometry.Bounds(definition.body).width-FoodSnailMonster.BodyWidth)<.005f,"body import width");
        foreach(var shell in definition.shells)Check(Mathf.Abs(VisibleBodyGeometry.Bounds(shell).width-FoodSnailMonster.ShellWidth)<.005f,"equal shell width "+shell.name);
        Check(VisibleBodyGeometry.Bounds(definition.crater).width>4.7f,"large melted crater");
        Check(Mathf.Approximately(StageTwoBalance.EntryHealth(100,1,1),115f),"weak run baseline floor");
        Check(StageTwoBalance.EntryHealth(100,1000000,1)==250f,"extreme damage scaling cap");
        var bossGo=level.EntityManager.SpawnFinalBoss(source,player.transform.position+Vector3.up*3);
        level.NotifyExternalFinalBossSpawned();var boss=bossGo.GetComponent<SnailBossRuntime>();boss.AutoPatterns=false;
        var previousChests=level.EntityManager.chests.ToList();int chestCount=previousChests.Count;
        boss.TakeDamage(1000000);boss.TakeDamage(1000000);
        yield return new WaitForSeconds(1.8f);
        Check(progression.FirstBossDefeated&&!level.IsLevelEnded&&Time.timeScale==1,"first boss death continues run");
        var rewards=level.EntityManager.chests.ToList().Except(previousChests).ToArray();
        Check(rewards.Length==3&&rewards.All(c=>((ChestBlueprint)Field(c,"chestBlueprint").GetValue(c)).abilityChest),"exactly three augment reward chests");
        progression.FinalBossDefeated(player.transform.position,100);Check(level.EntityManager.chests.Count==chestCount+3,"duplicate death cannot duplicate rewards");
        Check(progression.Portal!=null&&boss==null,"crater replaces dead boss");Capture("01-boss-rewards-crater");
        // Exercise the existing travel rig with every character before using it for the stage transition.
        var travel=BloodClotTravel.Ensure(player);var original=player.Blueprint;
        foreach(string key in new[]{"Ashi","Ari","Hyuki","Shini"})
        {
            var bp=Object.Instantiate(original);bp.name=key=="Ashi"?"아시":key=="Ari"?"아리":key=="Hyuki"?"혁이":"신이";
            Set(player,"characterBlueprint",bp);player.transform.position=progression.Portal.transform.position+Vector3.left*3;
            var routine=level.StartCoroutine(travel.Dive(progression.Portal.transform));yield return new WaitForSeconds(.7f);
            Check(travel.Busy&&travel.CharacterKey==key&&!player.GetComponent<Rigidbody2D>().simulated,key+" uses own entry animation");Capture("travel-"+key);
            yield return routine;yield return travel.Eject(progression.Portal.transform,progression.Portal.transform.position+Vector3.left*3);
            Check(!travel.Busy&&player.GetComponent<Rigidbody2D>().simulated,key+" restores physics");travel.ReleasePose();
            Set(player,"characterBlueprint",original);Object.Destroy(bp);
        }
        // Open all three together: the existing queue must grant three separate selections.
        checkingRewards=true;
        foreach(var chest in rewards)chest.OpenChest();
        int choices=0;float limit=Time.realtimeSinceStartup+12;
        var dialog=level.EntityManager.AbilitySelectionDialog;
        while(choices<3&&Time.realtimeSinceStartup<limit)
        {
            if(dialog.MenuOpen){choices++;dialog.Close();}
            yield return null;
        }
        Check(choices==3,"three chests queue three level-up selections");checkingRewards=false;Time.timeScale=1;
        var abilities=Object.FindObjectOfType<AbilityManager>();
        var offer=abilities.Ver4.CreateOffers(false,3).FirstOrDefault() as Ver4AugmentOffer;
        Check(offer!=null&&abilities.Ver4.Apply(offer),"acquire real augment before transfer");
        var augmentIds=abilities.Ver4.CaptureRunSceneConditionalAugmentIds().ToArray();
        var relic=AssetDatabase.LoadAssetAtPath<RelicBlueprint>("Assets/hyeon/Relic/OctoberExpansion/life_sprout.asset");
        player.Relics.Equip(relic);var relicState=player.Relics.State;relicState.revived=true;
        var items=OctoberItemRuntime.Get(player);Check(items.Give(Resources.Load<MerchantItemBlueprint>("OctoberContent/Catalog/Item02")),"acquire merchant item");
        player.AddDamageMultiplier(.3f);player.AddDashCharge(2);Set(player,"currentLevel",17);Set(player,"currentExp",12f);
        var potion=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Chest/Red Potion.prefab")).GetComponent<Collectable>();
        potion.Init(level.EntityManager,player);potion.Setup(true,true);potion.gameObject.SetActive(false);
        var slot=level.PlayerInventory.AddItem(potion);Check(slot!=null,"carry consumable into portal");
        var before=player.CaptureRunSceneState();var skills=player.Skills;
        var inventory=level.PlayerInventory;var itemState=items.State;
        // Keep a reward unopened across the boundary as well.
        var retainedChest=level.EntityManager.SpawnChest(definition.rewardChest,player.transform.position+Vector3.down*4);
        level.ActivateSpawnPotion();
        Set(Object.FindObjectOfType<AcidToadSpawn>(),"<Spawned>k__BackingField",true);
        player.transform.position=progression.Portal.transform.position+Vector3.left*1.2f;
        player.GetComponent<Rigidbody2D>().position=player.transform.position;
        yield return new WaitForFixedUpdate();yield return null;
        var entryPortal=progression.Portal;
        entryPortal.TryInteract();
        Check(progression.Travelling,"stage portal accepts focused field interaction");
        Check(!progression.TryEnter(player,progression.Portal),"repeated entry rejected");
        while(progression.Travelling)yield return null;
        Check(progression.StageNumber==2&&level.CurrentLevelTime<.2f&&level.LevelDuration==900,"new stage starts fresh 15-minute clock");
        Check(!level.IsRunFlowPaused&&!MiniStageRuntimeState.IsInsideMiniStage&&!player.IsPortalTravelling,"transition releases all locks");
        Check(level.PlayerCharacter==player&&player.Skills==skills&&level.PlayerInventory==inventory,"same character skills and inventory objects");
        var after=player.CaptureRunSceneState();
        Check(after.CurrentLevel==before.CurrentLevel&&after.CurrentExp==before.CurrentExp&&after.DamageMultiplier==before.DamageMultiplier,"level XP and upgraded damage preserved");
        Check(ReferenceEquals(player.Relics.State,relicState)&&player.Relics.Equipped==relic&&relicState.revived,"equipped relic and spent revival preserved");
        Check(ReferenceEquals(items.State,itemState)&&items.Has(2)&&abilities.Ver4.CaptureRunSceneConditionalAugmentIds().SequenceEqual(augmentIds),"merchant items and original augments preserved");
        Check(((IList)Field(slot,"items").GetValue(slot)).Contains(potion)&&retainedChest.gameObject.activeSelf,"consumable and unopened chest preserved");
        Check(level.SpawnPotionRemaining>20&&!Object.FindObjectOfType<AcidToadSpawn>().Spawned,"active potion retained and event schedule restarted");
        int pool=level.CurrentLevelBlueprint.monsters.Length-1;
        var entries=level.CurrentLevelBlueprint.monsters[pool].monsterBlueprints;
        var snails=new List<FoodSnailMonster>();
        for(int i=0;i<9;i++)
        {
            Check(i==0||entries[i].hp>entries[i-1].hp,"increasing snail HP tier "+i);
            var p=player.transform.position+new Vector3((i%3-1)*2.6f,(i/3-1)*2.1f+2,0);
            var snail=(FoodSnailMonster)level.EntityManager.SpawnMonster(pool,p,entries[i]);snails.Add(snail);
            Check(snail.Shell.sprite==definition.shells[i]&&snail.BodyHitbox.size.x<1.4f,"correct shell and fitted hitbox "+i);
        }
        yield return null;Capture("02-stage-two-nine-snails");
        float phase=snails[0].WalkPhase;yield return new WaitForSeconds(.3f);
        Check(phase!=snails[0].WalkPhase,"crawl motion advances while moving");
        var shadow=snails[0].transform.Find("Shadow");var shadowScale=shadow.localScale;
        yield return new WaitForSeconds(.3f);Check(shadowScale==shadow.localScale,"shadow does not squash with body");
        MiniStageRuntimeState.EnterMiniStage(null);level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
        phase=snails[0].WalkPhase;yield return new WaitForSeconds(.15f);Check(phase==snails[0].WalkPhase,"mini-stage pauses snail motion");
        MiniStageRuntimeState.ExitMiniStage(null);level.EntityManager.SetFieldMonsterRuntimeSuspended(false);
        float health=snails[0].HP;snails[0].TakeDamage(1);Check(snails[0].HP<health,"snail takes combat damage");
        var table=level.CurrentLevelBlueprint.monsterSpawnTable;int offset=source.MonsterIndexMap.Count;
        float early=0,late=0;
        for(int i=0;i<9;i++){early+=entries[i].hp*table.GetProbability(0,offset+i);late+=entries[i].hp*table.GetProbability(1,offset+i);}
        Check(late>early*1.6f,"late waves favour stronger HP tiers");
        for(int i=0;i<offset;i++)Check(table.GetProbability(.5f,i)==0,"stage 1 normal excluded "+i);
        travel.ReleasePose();player.Move(Vector2.zero);
        Set(level,"levelTime",900f);limit=Time.realtimeSinceStartup+15;
        while(Object.FindObjectOfType<SnailBossRuntime>()==null&&Time.realtimeSinceStartup<limit)yield return null;
        boss=Object.FindObjectOfType<SnailBossRuntime>();Check(boss!=null,"15-minute stage 2 boss automatically spawns");
        boss.AutoPatterns=false;Check(!SnailBossDebugSpawn.TrySpawn(),"no duplicate stage 2 boss");Capture("03-stage-two-boss");
        boss.TakeDamage(1000000);yield return new WaitForSecondsRealtime(2);
        Check(level.IsLevelEnded&&Time.timeScale==0,"stage 2 final clear still ends run");
        SessionState.SetBool(Key+"Done",true);
    }
}
