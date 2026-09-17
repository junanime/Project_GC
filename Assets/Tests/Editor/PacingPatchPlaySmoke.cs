using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Vampire.Tests.Editor.PacingPatchTests;

namespace Vampire.Tests.Editor
{
    // Batch-only functional smoke in a disposable project copy, not a survival test.
    [InitializeOnLoad]
    public static class PacingPatchPlaySmoke
    {
        const string Key="PacingPatchSmoke";
        static bool started;
        static bool transferring;
        static int previousPlayerId;
        static float expectedMaxHealth;
        static double deadline;
        static PacingPatchPlaySmoke() { if(SessionState.GetBool(Key,false)) Attach(); }
        public static void Run()
        {
            PacingPatchTests.RunAll();
            SessionState.SetBool(Key,true); SessionState.SetBool(Key+"Done",false); SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            deadline=EditorApplication.timeSinceStartup+150;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log; Application.logMessageReceived+=Log;
        }
        static void Log(string message,string trace,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) SessionState.SetBool(Key+"Failed",true); }
        static void Check(bool valid,string message)
        { if(!valid) throw new Exception(message); Debug.Log("[PacingSmoke] PASS "+message); }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)) return;
            if(SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                else if(!EditorApplication.isPlayingOrWillChangePlaymode)
                { bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);Debug.Log("[PacingSmoke] FINISHED failed="+failed);EditorApplication.Exit(failed?1:0); }
                return;
            }
            if(EditorApplication.timeSinceStartup>deadline || SessionState.GetBool(Key+"Failed",false))
            {SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);return;}
            if(!EditorApplication.isPlaying) return;
            var level=UnityEngine.Object.FindObjectOfType<LevelManager>();
            if(transferring && level!=null && level.PlayerCharacter!=null && level.PlayerCharacter.GetInstanceID()!=previousPlayerId && level.CurrentLevelTime>1)
            {
                try
                {
                    Check(!CrossSceneData.HasPendingRunSceneTransfer,"Scene transfer snapshot consumed by restore hook");
                    var owner=UnityEngine.Object.FindObjectOfType<SynergyManager>();
                    Check(owner!=null&&owner.OwnedItems.Count==2,"Purchased duplicate items survive a real scene reload");
                    Check(Mathf.Abs(level.PlayerCharacter.MaxHealth-expectedMaxHealth)<.01f,"Item-related character stats survive without double application");
                    Debug.Log("[PacingSmoke] ALL CHECKS COMPLETE");
                }
                catch(Exception e){Debug.LogException(e);}
                transferring=false;SessionState.SetBool(Key+"Done",true);return;
            }
            if(!started && level!=null && level.PlayerCharacter!=null && level.CurrentLevelTime>1)
            { started=true;level.StartCoroutine(Checks(level)); }
            var dialog=UnityEngine.Object.FindObjectOfType<AbilitySelectionDialog>();
            if(dialog!=null && dialog.MenuOpen)
            { var choices=(List<Ability>)Get(dialog,"displayedAbilities");if(choices.Count>0)choices[0].Select();dialog.Close(); }
            Time.timeScale=1;
        }
        static IEnumerator Checks(LevelManager level)
        {
            var player=level.PlayerCharacter;var entity=level.EntityManager;
            Set(player,"isInvincible",true);
            var weapon=SyringeAbilityResolver.FindOwnedOrFirst(UnityEngine.Object.FindObjectOfType<AbilityManager>());
            Set(player,"isDashing",true);Check(weapon.AttacksBlocked,"Dash blocks actual owned syringe");Set(player,"isDashing",false);
            int before=level.NormalMonstersSpawned;
            for(int i=0;i<50;i++)level.SpawnMonsterByFlatIndex(0);
            yield return null;yield return null;
            Check(level.NormalMonstersSpawned>=before+50,"50 successful normal spawn requests are counted");
            Check(entity.LivingMonsters.Any(x=>x.Blueprint is EliteMonsterBlueprint),"Normal spawn quota creates an elite before timed phases");
            level.SetRunFlowPaused(true);
            foreach(var m in entity.LivingMonsters.ToArray()) { entity.LivingMonsters.Remove(m); entity.DespawnMonster((int)Get(m,"monsterIndex"),m,false); }

            var data=level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];
            var target=entity.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*15,data,500);
            var renderer=(SpriteRenderer)Get(target,"monsterSpriteRenderer");var normal=renderer.sharedMaterial;
            target.TakePeriodicDamage(1);
            Check(renderer.sharedMaterial==normal,"Periodic damage keeps monster material");
            target.TakeDamage(1); yield return new WaitForSeconds(.09f);
            Check(renderer.sharedMaterial==normal,"Direct hit flash ends even when hits are rapid");
            for(int i=0;i<8;i++){target.TakeDamage(1);yield return new WaitForSeconds(.03f);}
            yield return new WaitForSeconds(.08f);
            Check(renderer.sharedMaterial==normal,"Continuous direct hits do not leave monster white");

            Set(player,"isInvincible",false);
            float health=player.CurrentHealth;player.TakeDamage(2);float hitHealth=player.CurrentHealth;
            Check(hitHealth<health,"Player test hit reduces health");
            yield return new WaitForSeconds(.3f);player.TakeDamage(2);
            Check(player.CurrentHealth==hitHealth,"Second hit inside 0.5 seconds is ignored");
            yield return new WaitForSeconds(.25f);player.TakeDamage(2);
            Check(player.CurrentHealth<hitHealth,"Hit after 0.5 seconds deals damage");Set(player,"isInvincible",true);

            var background=UnityEngine.Object.FindObjectOfType<InfiniteBackground>();
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Infinite Background 2.mat");
            string serialized=EditorJsonUtility.ToJson(material);
            Check(background.GetComponent<MeshRenderer>().sharedMaterial!=material,"Background uses runtime material clone");
            player.transform.position+=Vector3.right*25;yield return new WaitForSeconds(5.2f);
            Check(EditorJsonUtility.ToJson(material)==serialized,"Scrolling does not mutate material asset");

            // Enter a real acid room while a boss is alive and a bomb is mid-flight.
            level.SetRunFlowPaused(false);
            var bossObject=entity.SpawnFinalBoss(level.CurrentLevelBlueprint,(Vector2)player.transform.position+Vector2.up*8);
            yield return null;yield return null;
            var boss=bossObject.GetComponentInChildren<BossController>();
            var bomb=bossObject.GetComponentsInChildren<BossBombLobPattern>(true).First();
            boss.FiveCoreSkills.BeginPattern(bomb,boss.CurrentPhase);
            var throwMethod=typeof(BossBombLobPattern).GetMethod("SpawnBombWarningProjectileAndExplode",Hidden);
            var throwRoutine=(IEnumerator)throwMethod.Invoke(bomb,new object[]{(Vector2)player.transform.position+Vector2.right*3});
            bomb.StartCoroutine(throwRoutine);
            var mini=UnityEngine.Object.FindObjectOfType<MiniStageDirector>();
            var acid=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MiniStages/MiniStageAcidLureRoom.prefab").GetComponent<MiniStageRoomBase>();
            Set(mini,"roomPrefabs",new[]{acid});Set(mini,"useForcedRoomIndex",true);Set(mini,"forcedRoomIndex",0);
            mini.StartCoroutine((IEnumerator)typeof(MiniStageDirector).GetMethod("EnterMiniStageRoutine",Hidden).Invoke(mini,null));
            yield return new WaitForSeconds(1);
            Check(mini.IsInsideMiniStage&&boss.FieldRuntimeSuspended,"Real mini-stage entry suspends UFO boss");
            Vector3 position=boss.transform.position;
            yield return new WaitForSeconds(2);
            Check(Vector3.Distance(position,boss.transform.position)<.02f,"UFO cannot follow player into mini-stage");
            Check(UnityEngine.Object.FindObjectsOfType<BossSimpleBullet>().Length==0,"Boss emits no bullets while inside mini-stage");
            Check(((IList)Get(bomb,"pendingBombObjects")).Count==0,"In-flight bomb and warning are canceled on entry");
            var field=mini.CurrentRoom.GetComponentsInChildren<MiniStageAcidLureField>().First();
            var collider=(Collider2D)Get(field,"triggerCollider");
            int killed=field.CurrentKillCount;
            var victim=entity.SpawnMonster(0,collider.bounds.center,data,0,true);
            victim.TakeDamage(9999);
            Check(field.CurrentKillCount==killed+1,"One-shot kill on acid counts immediately before death animation");
            var gem=entity.SpawnExpGem(player.transform.position+Vector3.right*4,GemType.Blue2,false);
            var coin=entity.SpawnCoin(player.transform.position+Vector3.right*4,CoinType.Bronze1,false);
            mini.StartCoroutine((IEnumerator)typeof(MiniStageDirector).GetMethod("ReturnToFieldRoutine",Hidden).Invoke(mini,null));
            yield return new WaitForSeconds(1);
            Check(!gem.gameObject.activeSelf&&!coin.gameObject.activeSelf,"Uncollected mini-stage XP and gold return to pool on exit");
            Check(!boss.FieldRuntimeSuspended&&!MiniStageRuntimeState.IsInsideMiniStage,"Boss resumes after returning to main field");
            Check(Get(boss,"patternLoopCoroutine")!=null,"Boss pattern scheduler restarts after return");
            level.SetRunFlowPaused(true);
            boss.SetFieldRuntimeSuspended(true);boss.SetFieldRuntimeSuspended(false);
            Set(boss,"nextPatternTime",float.MaxValue);Set(boss,"basicAttackTimer",-999f);
            boss.FiveCoreSkills.BeginPattern(bomb,boss.CurrentPhase);
            Set(bomb,"warningDuration",.3f);Set(player,"isInvincible",false);
            float beforeBomb=player.CurrentHealth;
            bomb.StartCoroutine((IEnumerator)throwMethod.Invoke(bomb,new object[]{(Vector2)player.transform.position}));
            yield return new WaitForSeconds(.34f);
            Check(player.CurrentHealth<beforeBomb,"Bomb damages at landing, without the former 0.2-second ground delay");
            Set(player,"isInvincible",true);
            var owner=UnityEngine.Object.FindObjectOfType<SynergyManager>();
            if(owner==null)owner=player.gameObject.AddComponent<SynergyManager>();
            var item=AssetDatabase.LoadAssetAtPath<MerchantItemBlueprint>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:MerchantItemBlueprint")[0]));
            var ownership=new MerchantOwnershipSnapshot();ownership.items.Add(item);ownership.items.Add(item);owner.RestoreOwnership(ownership);
            player.AddMaxHealthBonus(7f);expectedMaxHealth=player.MaxHealth;previousPlayerId=player.GetInstanceID();
            transferring=true;
            Check(RunSceneTransferTestService.CaptureCurrentRunAndLoad("Level 1"),"Scene transfer accepted with item ownership snapshot");
        }
    }
}
