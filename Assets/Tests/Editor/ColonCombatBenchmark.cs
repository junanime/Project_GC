using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class ColonCombatBenchmark
    {
        const string Key="ColonCombatBenchmark",Output="D:/Project_GC/work/colon-combat-benchmark",Source="D:/Project_GC/work/colon-balance-soak";
        static readonly string[] Names={"Main","Ari","Hyuki","Shini"};
        static bool started;static double deadline;
        static ColonCombatBenchmark(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/errors.txt","");
            File.WriteAllText(Output+"/ttk.csv","character,build_stage,target,elite,hp,trial,seconds,killed,remaining_hp,damage,cooldown,projectiles\n");
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Round",0);SessionState.SetBool(Key+"Failed",false);Next();
        }
        static void Next(){SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Scene",true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Attach();EditorApplication.EnterPlaymode();}
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+1600;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string t,string s,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",t+"\n"+s+"\n");SessionState.SetBool(Key+"Failed",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Output+"/errors.txt","Deadline\n");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                int next=SessionState.GetInt(Key+"Round",0)+1;bool fail=SessionState.GetBool(Key+"Failed",false);
                if(next<8&&!fail){SessionState.SetInt(Key+"Round",next);Next();return;}
                SessionState.SetBool(Key,false);File.WriteAllText(Output+"/result.txt",fail?"FAIL":"PASS: measured live projectile combat, stationary 2-unit targets");EditorApplication.Exit(fail?1:0);return;
            }
            if(!EditorApplication.isPlaying)return;
            if(SessionState.GetBool(Key+"Scene",false))
            {
                SessionState.SetBool(Key+"Scene",false);int round=SessionState.GetInt(Key+"Round",0);
                CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/"+Names[round/2]+" Character Blueprint.asset");
                CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();CrossSceneData.ClearPendingRunSceneTransfer();UnityEngine.SceneManagement.SceneManager.LoadScene("Level 1");return;
            }
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var level=Object.FindObjectOfType<LevelManager>();if(started||level==null||!level.InitialFieldObjectsReady||level.PlayerCharacter==null)return;
            started=true;level.StartCoroutine(Measure(level,SessionState.GetInt(Key+"Round",0)));
        }
        static void SilenceField(LevelManager level)
        {
            level.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner){b.enabled=false;b.StopAllCoroutines();}
        }
        static void Clear(LevelManager level)
        {
            foreach(var m in Object.FindObjectsOfType<Monster>())m.DespawnForStageTransfer();
            foreach(var p in Object.FindObjectsOfType<Projectile>())p.RemoveForItem();
            foreach(var c in Object.FindObjectsOfType<Collectable>())c.DissolveInAcidLake();
        }
        static IEnumerator Measure(LevelManager level,int round)
        {
            var pref=GamePreferences.Current.Copy();pref.pauseOnFocusLoss=false;GamePreferences.Apply(pref,false);Application.runInBackground=true;Time.timeScale=1;RunScore.Exclude("Controlled colon combat benchmark");
            var player=level.PlayerCharacter;ColonContentSmoke.Set(player,"isInvincible",true);SilenceField(level);
            yield return ColonContentSmoke.Enter(level);SilenceField(level);Clear(level);
            int buildStage=round%2+1;string character=Names[round/2];
            string buildSource=buildStage==1?Source:"D:/Project_GC/work/colon-growth-revision";
            var build=JsonUtility.FromJson<RunSceneTransferSnapshot>(File.ReadAllText(buildSource+"/"+character+"-stage"+buildStage+"-build.json"));
            build.CharacterState.SkillActiveRemaining=0;build.CharacterState.SkillCooldownRemaining=0;
            var manager=Object.FindObjectOfType<AbilityManager>();
            int restored=manager.RestoreRunSceneAbilities(build.AbilityStates);
            if(restored!=build.AbilityStates.Count)throw new Exception("Incomplete benchmark ability restore: "+restored+"/"+build.AbilityStates.Count);
            player.RestoreRunSceneState(build.CharacterState);
            OctoberItemRuntime.Get(player).Restore(build.OctoberItems);Object.FindObjectOfType<StatsManager>().IncreaseCoinsGained(build.CoinsGained);player.CollectableCollider.enabled=false;
            File.WriteAllText(Output+"/"+character+"-restored"+buildStage+".json",JsonUtility.ToJson(player.CaptureRunSceneState(),true));
            var map=FiniteStomachMap.Instance;var at=map.ClampToField(map.UVToWorld(new Vector2(.78f,.72f)),map.WallWidth+8);
            ColonContentSmoke.Position(player,at);player.Move(Vector2.right);player.Move(Vector2.zero);var dart=Object.FindObjectOfType<SyringeDartAbility>();
            var roster=ColonMonsterRoster.Active;int sampleSeed=91873+(round/2)*1000;
            foreach(var entry in roster.Normals.Concat(roster.Elites))
            for(int trial=0;trial<3;trial++)
            {
                Clear(level);yield return new WaitForSeconds(.25f);UnityEngine.Random.InitState(sampleSeed++);
                var target=level.EntityManager.SpawnMonster(roster.PoolIndex,at+Vector2.right*2,entry.blueprint);
                var rb=target.GetComponent<Rigidbody2D>();var constraints=rb.constraints;rb.constraints=RigidbodyConstraints2D.FreezeAll;rb.velocity=Vector2.zero;
                float hp=target.HP,begin=Time.time;
                while(target.gameObject.activeInHierarchy&&target.HP>0&&Time.time-begin<20)
                {
                    var dialog=level.EntityManager.AbilitySelectionDialog;if(dialog.MenuOpen)dialog.Close();
                    yield return null;
                }
                float elapsed=Time.time-begin;bool killed=target.HP<=0;
                File.AppendAllText(Output+"/ttk.csv",$"{character},{buildStage},{entry.id},{entry.elite},{hp},{trial},{elapsed:F3},{killed},{Mathf.Max(0,target.HP):F3},{dart.GetEffectiveDamage():F3},{dart.GetEffectiveCooldown():F3},{dart.GetEffectiveProjectileCount()}\n");
                rb.constraints=constraints;target.DespawnForStageTransfer();
                File.WriteAllText(Output+"/status.txt",character+" build="+buildStage+" "+entry.name+" trial="+trial);
            }
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
