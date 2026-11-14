#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class StageGrowthResetSmoke
    {
        const string Key="StageGrowthResetSmoke", Output="Library/StageGrowthResetProof";
        static bool started,absorptionSeen,absorptionCaptured;static float absorptionAge;static int checks;static double deadline;
        static RunSceneCharacterSnapshot launch;static string launchAbilities;
        static float launchBossDamage;static int coins,silver;static RunScore score;
        static MonoBehaviour[] groundEffects;
        [Serializable]class Entry{public string id;}
        [Serializable]class Catalog{public Entry[] entries;}
        [Serializable]class Pref{public string key;public bool had;public int value;}
        [Serializable]class Backup{public Pref[] prefs;}
        static StageGrowthResetSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);RestorePrefs();
            File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/checks.txt","");File.WriteAllText(Output+"/errors.txt","");
            var entries=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text).entries;
            var keys=entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{CapsuleDoctorDialogue.SeenKey,"Coins","LobbySilverCoins"}).Distinct().ToArray();
            File.WriteAllText(Output+"/prefs.json",JsonUtility.ToJson(new Backup{prefs=keys.Select(k=>new Pref{key=k,had=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            CrossSceneData.ClearStartingLobbyItems();CrossSceneData.ClearPendingRunSceneTransfer();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;absorptionSeen=absorptionCaptured=false;absorptionAge=0;deadline=EditorApplication.timeSinceStartup+600;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string message,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            File.AppendAllText(Output+"/errors.txt",message+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);
        }
        static void RestorePrefs()
        {
            var path=Output+"/prefs.json";if(!File.Exists(path))return;
            foreach(var p in JsonUtility.FromJson<Backup>(File.ReadAllText(path)).prefs)
                if(p.had)PlayerPrefs.SetInt(p.key,p.value);else PlayerPrefs.DeleteKey(p.key);
            PlayerPrefs.Save();File.Delete(path);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Output+"/errors.txt","Timeout\n");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                RestorePrefs();SessionState.SetBool(Key,false);bool failed=SessionState.GetBool(Key+"Failed",false);
                File.WriteAllText(Output+"/result.txt",(failed?"FAIL":"PASS")+" checks="+checks);EditorApplication.Exit(failed?1:0);return;
            }
            if(!EditorApplication.isPlaying)return;
            var guide=TutorialGuide.Instance;if(guide!=null){guide.Close();guide.enabled=false;}
            if(StageEntryLoading.Instance!=null&&StageEntryLoading.Instance.AbsorbingEnergy)
            {
                absorptionSeen=true;absorptionAge+=Time.unscaledDeltaTime;
                if(!absorptionCaptured&&absorptionAge>.65f){CaptureAbsorption();absorptionCaptured=true;}
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(started||level==null||!level.InitialFieldObjectsReady)return;
            started=true;
            var host=new GameObject("Stage growth reset QA host");Object.DontDestroyOnLoad(host);
            host.AddComponent<StageEntryLoadingTestHost>().StartCoroutine(Checks(level));
        }
        static void Check(bool ok,string message)
        {
            if(!ok)throw new Exception("STAGE_GROWTH_FAIL "+message);
            checks++;File.AppendAllText(Output+"/checks.txt","PASS "+message+"\n");
        }
        static void CaptureAbsorption()
        {
            var camera=Camera.main;if(camera==null)return;
            var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();
            var nodes=overlays.SelectMany(c=>c.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();var layers=nodes.Select(n=>n.gameObject.layer).ToArray();
            var target=camera.targetTexture;var mask=camera.cullingMask;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);
            var go=new GameObject("Growth absorption proof camera");var ui=go.AddComponent<Camera>();ui.enabled=false;ui.CopyFrom(camera);
            ui.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);ui.clearFlags=CameraClearFlags.Depth;ui.cullingMask=1<<31;ui.targetTexture=rt;Texture2D pixels=null;
            try
            {
                foreach(var node in nodes)node.gameObject.layer=31;
                camera.targetTexture=rt;camera.cullingMask=mask&~(1<<31);
                foreach(var canvas in overlays){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=ui;canvas.planeDistance=ui.nearClipPlane+.1f;}
                Canvas.ForceUpdateCanvases();camera.Render();ui.Render();RenderTexture.active=rt;
                pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Output+"/energy-absorption.png",pixels.EncodeToPNG());
            }
            finally
            {
                for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];}
                for(int i=0;i<nodes.Length;i++)nodes[i].gameObject.layer=layers[i];
                camera.targetTexture=target;camera.cullingMask=mask;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);
                if(pixels!=null)Object.Destroy(pixels);Object.Destroy(go);Canvas.ForceUpdateCanvases();
            }
        }
        static string AbilitySignature(AbilityManager manager)=>string.Join(";",manager.CaptureRunSceneAbilities().Select(a=>a.AbilityTypeName+"/"+a.AbilityObjectName+"/"+a.Level+"/"+string.Join(",",a.ConditionalAugmentIds)).OrderBy(x=>x));
        static void Silence(LevelManager level)
        {
            level.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())
                if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn)
                {b.enabled=false;b.StopAllCoroutines();}
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())if(!(m is BloodClotMonster))m.DespawnForStageTransfer();
            level.PlayerCharacter.CollectableCollider.enabled=false;
        }
        static void SeedGrowth(LevelManager level,AbilityManager manager)
        {
            var player=level.PlayerCharacter;
            var grown=player.CaptureRunSceneState();grown.CurrentLevel=9;grown.CurrentExp=40;grown.NextLevelExp=55;grown.ExpToNextLevel=20;
            grown.DamageMultiplier+=.8f;grown.AttackSpeedMultiplier+=.5f;grown.MaxHealthBonus+=50;grown.ReviveCount+=2;
            player.RestoreRunSceneState(grown);player.Skills.TryUpgrade(true);player.Skills.TryUpgrade(false);player.Skills.TryUpgradeDash();
            PlayerGeneralStatRuntime.GetOrCreate(player).AddBossDamageMultiplier(.5f);
            HungerNeedleRuntime.RegisterHit(player,60,.1f,8,false);
            PoisonContagionRuntime.Enable(3,1,1,~0,false);
            var item=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").items.First(i=>i!=null&&i.octoberId==2);
            OctoberItemRuntime.Get(player).Give(item);SynergyManager.Instance?.AddItem(item);
            var relic=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").relics.First(r=>r!=null&&!player.Relics.Has(r.relicId));player.Relics.AddReward(relic);
            var extra=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).FirstOrDefault(a=>!a.Owned&&a.RequirementsMet());
            Check(extra!=null&&manager.AcquireVer4Ability(extra),"fixture acquires a second live weapon");
            var prescription=player.GetComponent<PrescriptionRuntime>();var state=prescription.Capture();state.claimed=true;prescription.Restore(state);
            SeedGroundEffects(player);
            Check(player.CurrentLevel==9&&player.DamageMultiplier>launch.DamageMultiplier,"fixture has earned growth before transfer");
        }
        static void SeedGroundEffects(Character player)
        {
            // Real gameplay constructors, with a lifetime longer than any transfer. Place
            // them away from actors so fixture damage cannot alter score or progression.
            var at=new Vector2(10000,10000);
            var previous=Object.FindObjectsOfType<Ver4GroundEffect>();
            Ver4GroundEffect.Create(at,600,false,12,player,~0);
            Ver4GroundEffect.Create(at+Vector2.right*3,600,true,0,player,~0);
            var puddle=new GameObject("Stage reset QA long-lived acid").AddComponent<DigestiveAcidPuddle>();
            puddle.transform.position=at+Vector2.right*6;
            puddle.Init(600,1,12,.5f,Color.green,player,"Stage reset QA acid");
            var fiber=new GameObject("Stage reset QA long-lived fiber").AddComponent<FiberTrailSegment>();
            fiber.Init(at+Vector2.right*9,at+Vector2.right*10,~0,600,12,.5f,.15f,Color.white,player,"Stage reset QA fiber");
            groundEffects=Object.FindObjectsOfType<Ver4GroundEffect>().Except(previous).Cast<MonoBehaviour>().Concat(new MonoBehaviour[]{puddle,fiber}).ToArray();
            Check(groundEffects.Length==4&&groundEffects.All(effect=>effect!=null&&effect.gameObject.activeInHierarchy),"actual long-lived cold, healing, acid and fiber effects exist before transfer");
        }
        static void VerifyReset(LevelManager level,AbilityManager manager,string route)
        {
            var player=level.PlayerCharacter;var state=player.CaptureRunSceneState();
            Check(state.CurrentLevel==launch.CurrentLevel&&state.CurrentExp==launch.CurrentExp&&state.NextLevelExp==launch.NextLevelExp,route+" level and XP reset");
            Check(Mathf.Approximately(state.DamageMultiplier,launch.DamageMultiplier)&&Mathf.Approximately(state.AttackSpeedMultiplier,launch.AttackSpeedMultiplier)&&Mathf.Approximately(state.MaxHealthBonus,launch.MaxHealthBonus),route+" stats reset without stacked bonuses");
            Check(state.ReviveCount==launch.ReviveCount&&state.MaxDashCharges==launch.MaxDashCharges,route+" earned revives and dash upgrades removed");
            Check(player.Skills.ActiveLevel==1&&player.Skills.PassiveLevel==1&&player.Skills.DashRewardUpgrades==0,route+" skills return to level one");
            Check(AbilitySignature(manager)==launchAbilities,route+" only original departure weapon remains at its original level");
            Check(player.Relics.State.id==launch.Relic.id&&player.Relics.State.additionalIds.Count==0,route+" original relic kept and reward relics cleared");
            Check(OctoberItemRuntime.Get(player).State.owned.Count==0&&(SynergyManager.Instance==null||SynergyManager.Instance.OwnedItems.Count==0),route+" item effects and ownership cleared");
            Check(Mathf.Approximately(PlayerGeneralStatRuntime.GetOrCreate(player).BossDamageMultiplier,launchBossDamage),route+" independent general-stat runtime reset");
            Check(HungerNeedleRuntime.GetCurrentStacks(player)==0&&!PoisonContagionRuntime.Enabled,route+" static hunger stacks and legendary contagion cleared");
            Check(!player.GetComponent<PrescriptionRuntime>().Claimed,route+" stage prescription rewards restart");
            Check(groundEffects.All(effect=>effect==null),route+" old cold, healing, acid and fiber effects destroyed before their lifetime expires");
            Check(RunScore.Current==score&&score.Data.normal==3&&score.Data.miniBoss==2,route+" ranking counters retained");
            Check(PlayerPrefs.GetInt("Coins")==coins&&PlayerPrefs.GetInt("LobbySilverCoins")==silver,route+" permanent currency unchanged");
        }
        static IEnumerator WaitForField(StageProgression flow,int expected)
        {
            float until=Time.realtimeSinceStartup+120;
            while(flow.Travelling&&Time.realtimeSinceStartup<until)yield return null;
            Check(!flow.Travelling&&flow.StageNumber==expected&&!StageEntryLoading.IsLoading,"real field entry completed: "+expected);
        }
        static IEnumerator Checks(LevelManager level)
        {
            checks=0;Time.timeScale=1;Application.runInBackground=true;
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            RunScore.Exclude("Stage growth reset automated QA");Silence(level);yield return null;
            var manager=Object.FindObjectOfType<AbilityManager>();var player=level.PlayerCharacter;var reset=level.GetComponent<StageGrowthReset>();var flow=StageProgression.Ensure(level);
            launch=player.CaptureRunSceneState();launchAbilities=AbilitySignature(manager);launchBossDamage=PlayerGeneralStatRuntime.GetOrCreate(player).BossDamageMultiplier;
            coins=PlayerPrefs.GetInt("Coins");silver=PlayerPrefs.GetInt("LobbySilverCoins");score=RunScore.Current;score.Data.normal=3;score.Data.miniBoss=2;
            Check(reset!=null&&reset.ResetCount==0,"immutable departure baseline installed before growth");
            Check(flow.BeginDirectOral(true),"initial direct stage zero begins");yield return WaitForField(flow,0);Silence(level);
            Check(reset.CurrentStage==0&&reset.ResetCount==0,"direct-entry bootstrap does not absorb departure equipment");
            SeedGrowth(level,manager);level.SetRunFlowPaused(false);
            Check(flow.BeginOralToStomach(),"actual stage zero to one transfer starts");yield return WaitForField(flow,1);Silence(level);
            Check(absorptionSeen,"real loading displays the king absorbing energy");VerifyReset(level,manager,"0 to 1");
            SeedGrowth(level,manager);level.SetRunFlowPaused(false);
            Check(flow.BeginDirectOral(),"real stage one to zero revisit begins");yield return WaitForField(flow,0);Silence(level);
            VerifyReset(level,manager,"1 to 0 revisit");
            SeedGrowth(level,manager);level.SetRunFlowPaused(false);
            Check(flow.BeginOralToStomach(),"return after oral revisit begins");yield return WaitForField(flow,1);Silence(level);
            VerifyReset(level,manager,"0 to 1 revisit");
            Check(!flow.BeginDirectOral(true)&&!flow.Travelling,"a later visit cannot masquerade as initial oral departure");
            var initialOverlay=StageEntryLoading.Create(player.Blueprint);
            Check(!flow.BeginDirectColon(initialOverlay)&&!flow.Travelling&&!flow.IsDirectColonStart,"a later visit cannot bypass reset as initial colon departure");
            Object.Destroy(initialOverlay.gameObject);yield return null;
            SeedGrowth(level,manager);yield return ColonContentSmoke.Enter(level);Silence(level);
            VerifyReset(level,manager,"1 to 2");Check(reset.ResetCount==4,"four main-field changes reset exactly four times");
            // The reusable reset contract covers all ordered pairs, including future return exits.
            foreach(int target in new[]{0,2,1,0,1,2})
            {
                int source=reset.CurrentStage;SeedGrowth(level,manager);
                yield return reset.ResetForTransition(target,level.CurrentLevelBlueprint);
                VerifyReset(level,manager,source+" to "+target+" reset contract");
            }
            SeedGrowth(level,manager);int count=reset.ResetCount;float grownDamage=player.DamageMultiplier;
            yield return reset.ResetForTransition(reset.CurrentStage,level.CurrentLevelBlueprint);
            Check(reset.ResetCount==count&&player.DamageMultiplier==grownDamage,"same-stage reentry never resets growth");
            Check(groundEffects.All(effect=>effect!=null&&effect.gameObject.activeInHierarchy),"same-stage reentry preserves live ground effects");
            var mini=Object.FindObjectOfType<MiniStageDirector>();MiniStageRuntimeState.EnterMiniStage(mini);
            yield return reset.ResetForTransition(1,level.CurrentLevelBlueprint);MiniStageRuntimeState.ExitMiniStage(mini);
            Check(reset.ResetCount==count&&player.DamageMultiplier==grownDamage,"miniroom visit and return preserve earned growth");
            Check(groundEffects.All(effect=>effect!=null&&effect.gameObject.activeInHierarchy),"miniroom visit and return preserve live ground effects");
            Time.timeScale=0;
            player.GainExp(1000);
            var queuedChest=level.EntityManager.SpawnChest(RemakeBalance.Current.levelUpChest,player.Position+Vector2.right*2);
            queuedChest.CompleteInitialPlacement();queuedChest.OpenChest();
            yield return reset.ResetForTransition(1,level.CurrentLevelBlueprint);
            Check(Time.timeScale==0,"pre-existing pause remains owned by its caller");VerifyReset(level,manager,"paused transfer");
            Check(!queuedChest.gameObject.activeSelf&&!level.EntityManager.AbilitySelectionDialog.MenuOpen,"opening reward chest is returned and pending reward menu cancelled");
            Time.timeScale=1;yield return new WaitForSeconds(.3f);
            Check(player.CurrentLevel==1&&!level.EntityManager.AbilitySelectionDialog.MenuOpen,"queued old XP cannot grant a level or reopen a menu after transfer");
            foreach(var key in new[]{"Hyuki","Shini","Ari"})yield return CheckCharacterSkillReset(key);
            SessionState.SetBool(Key+"Done",true);
        }

        static IEnumerator CheckCharacterSkillReset(string key)
        {
            var blueprint=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").characters.First(c=>OctoberArt.CharacterKey(c)==key);
            CrossSceneData.CharacterBlueprint=blueprint;CrossSceneData.ClearStartingLobbyItems();
            var load=SceneManager.LoadSceneAsync(1);while(!load.isDone)yield return null;
            LevelManager level=null;float until=Time.realtimeSinceStartup+120;
            while(Time.realtimeSinceStartup<until)
            {
                level=Object.FindObjectOfType<LevelManager>();
                if(level!=null&&level.InitialFieldObjectsReady)break;
                yield return null;
            }
            Check(level!=null&&level.InitialFieldObjectsReady,key+" real character scene initialized");
            RunScore.Exclude("Stage growth character-skill cleanup QA");Silence(level);Time.timeScale=1;
            var player=level.PlayerCharacter;var reset=level.GetComponent<StageGrowthReset>();var manager=Object.FindObjectOfType<AbilityManager>();
            var baseline=player.CaptureRunSceneState();string weapon=AbilitySignature(manager);
            if(player.Skills.IsShini)
            {
                player.Skills.Breath.RestoreFuel(12);player.Skills.Breath.SpawnEmber(player.Position+Vector2.right*3);
                Check(player.Skills.Breath.GroundCount>0,"Shini field ember exists before reset");
            }
            Check(player.Skills.TryActivate(),key+" actual skill activates before reset");
            if(key=="Hyuki")Check(player.GetComponent<PhoenixSkillVisual>().Playing,"Hyuki independent phoenix timer is running");
            player.Skills.TryUpgrade(true);player.Skills.TryUpgrade(false);
            yield return reset.ResetForTransition(2,level.CurrentLevelBlueprint);
            Check(player.Skills.ActiveLevel==1&&player.Skills.PassiveLevel==1&&!player.Skills.Active&&!player.Skills.IsSummoning&&player.Skills.CooldownRemaining==0,key+" active skill, levels, summon and cooldown reset");
            Check(AbilitySignature(manager)==weapon&&player.Relics.State.id==baseline.Relic.id,key+" original weapon and relic preserved");
            if(key=="Hyuki")Check(!player.GetComponent<PhoenixSkillVisual>().Playing&&player.GetComponent<PhoenixSkillVisual>().BlizzardStrength==0,"Hyuki independent phoenix and blizzard visuals cancelled");
            if(key=="Shini")Check(player.Skills.Breath.Fuel==baseline.ShiniFuel&&!player.Skills.Breath.Busy&&player.Skills.Breath.GroundCount==0,"Shini earned fuel, ground embers and active flame cancelled");
        }
    }
}
#endif
