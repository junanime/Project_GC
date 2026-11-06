#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
    // Full HP depletion with actual carried attacks. Fixed target + invulnerable attacker:
    // this is an offensive-capacity regression, never an estimate of human clear time.
    [InitializeOnLoad]
    public static class ColonBossDefeatSmoke
    {
        const string Key="ColonBossDefeatSmoke",Output="Library/ColonBossDefeatProof";
        const string Source="Documentation/QA/Stage2CarryoverFixtures";
        static readonly string[] Characters={"Shini","Hyuki"};
        static bool started;static double deadline;
        static ColonBossDefeatSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/errors.txt","");
            if(File.Exists(Output+"/result.txt"))File.Delete(Output+"/result.txt");
            File.WriteAllText(Output+"/defeat.csv","character,boss_hp,game_seconds,wall_seconds,active_uses,outcome,stage_ended\n");
            File.WriteAllText(Output+"/method.txt","Two historical Stage 1 automated carryover builds; full health restored, unchanged normal skill cooldowns, actual needle and active-skill damage. Maximum-HP Turtle (36,000) is fixed 2.5 units away, all field attackers/pickups disabled, player invulnerable. 240-game-second deadline, time scale 1.7. This measures finite offensive capacity, NOT real combat clear times or human success rates.\n");
            foreach(string pref in new[]{"Coins","LobbySilverCoins"}){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Failed",false);SessionState.SetInt(Key+"Round",0);Next();
        }
        static void Next()
        {
            Time.timeScale=1;SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Scene",true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+360;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);File.AppendAllText(Output+"/errors.txt","Deadline\n");}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                int next=SessionState.GetInt(Key+"Round",0)+1;bool failed=SessionState.GetBool(Key+"Failed",false);
                if(!failed&&next<Characters.Length){SessionState.SetInt(Key+"Round",next);Next();return;}
                foreach(string pref in new[]{"Coins","LobbySilverCoins"}){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();
                Time.timeScale=1;SessionState.SetBool(Key,false);File.WriteAllText(Output+"/result.txt",failed?"FAIL":"PASS: both real-attack fixtures defeated maximum-HP optional boss; not a human clear-rate test");EditorApplication.Exit(failed?1:0);return;
            }
            if(!EditorApplication.isPlaying)return;
            if(SessionState.GetBool(Key+"Scene",false))
            {
                SessionState.SetBool(Key+"Scene",false);CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/"+Characters[SessionState.GetInt(Key+"Round",0)]+" Character Blueprint.asset");
                CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();CrossSceneData.ClearPendingRunSceneTransfer();UnityEngine.SceneManagement.SceneManager.LoadScene("Level 1");return;
            }
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var level=Object.FindObjectOfType<LevelManager>();if(started||level==null||!level.InitialFieldObjectsReady||level.PlayerCharacter==null)return;
            started=true;level.StartCoroutine(Measure(level,Characters[SessionState.GetInt(Key+"Round",0)]));
        }
        static void Silence(LevelManager level)
        {
            level.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn){b.enabled=false;b.StopAllCoroutines();}
        }
        static IEnumerator Measure(LevelManager level,string name)
        {
            var pref=GamePreferences.Current.Copy();pref.pauseOnFocusLoss=false;pref.muteOnFocusLoss=false;GamePreferences.Apply(pref,false);Application.runInBackground=true;
            RunScore.Exclude("Stage-two fixed-target full-depletion regression");Silence(level);var player=level.PlayerCharacter;ColonContentSmoke.Set(player,"isInvincible",true);
            yield return ColonContentSmoke.Enter(level);Silence(level);Time.timeScale=1.7f;
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())if(!(m is BloodClotMonster))m.DespawnForStageTransfer();
            foreach(var p in Object.FindObjectsOfType<Projectile>())p.RemoveForItem();foreach(var p in Object.FindObjectsOfType<Collectable>())p.DissolveInAcidLake();
            var snapshot=JsonUtility.FromJson<RunSceneTransferSnapshot>(File.ReadAllText(Source+"/"+name+"-stage1-build.json"));
            snapshot.CharacterState.SkillActiveRemaining=snapshot.CharacterState.SkillCooldownRemaining=snapshot.CharacterState.SkillSummonRemaining=0;
            var abilities=Object.FindObjectOfType<AbilityManager>();if(abilities.RestoreRunSceneAbilities(snapshot.AbilityStates)!=snapshot.AbilityStates.Count)throw new Exception("Incomplete carried abilities: "+name);
            player.RestoreRunSceneState(snapshot.CharacterState);player.GainHealth(player.MaxHealth);player.CollectableCollider.enabled=false;OctoberItemRuntime.Get(player).Restore(snapshot.OctoberItems);
            foreach(var a in Object.FindObjectsOfType<Ability>())a.enabled=false;
            var map=FiniteStomachMap.Instance;var point=map.ClampToField(map.UVToWorld(new Vector2(.76f,.72f)),map.WallWidth+8);ColonContentSmoke.Position(player,point);
            var target=ColonBossRuntime.Spawn(ColonBossKind.Turtle,player,point+Vector2.right*2.5f,null);target.AutoPatterns=false;
            float until=Time.time+20;while(target.Arriving&&Time.time<until)yield return null;if(target.Arriving)throw new Exception("Turtle arrival timeout");
            target.enabled=false;target.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
            foreach(var a in Object.FindObjectsOfType<Ability>())a.enabled=true;
            float began=Time.time,wall=Time.realtimeSinceStartup,nextSkill=0;int uses=0;float hp=target.Health;
            player.Move(Vector2.right);player.Move(Vector2.zero);
            while(target!=null&&!target.Dead&&Time.time-began<240)
            {
                if(Time.time>=nextSkill&&player.Skills.CanActivate){player.Move(Vector2.right);player.Move(Vector2.zero);if(player.Skills.TryActivate())uses++;nextSkill=Time.time+.5f;}
                File.WriteAllText(Output+"/status.txt",name+" / "+(Time.time-began).ToString("F1")+"s / boss "+target.Health.ToString("F0"));yield return null;
            }
            bool dead=target==null||target.Dead;float elapsed=Time.time-began;
            File.AppendAllText(Output+"/defeat.csv",string.Join(",",name,hp.ToString(CultureInfo.InvariantCulture),elapsed.ToString(CultureInfo.InvariantCulture),(Time.realtimeSinceStartup-wall).ToString(CultureInfo.InvariantCulture),uses,dead?"boss_defeated":"timeout",level.IsLevelEnded)+"\n");
            if(!dead||level.IsLevelEnded)throw new Exception("Fixed-target depletion failed or ended stage: "+name);
            yield return new WaitForSeconds(1.2f);SessionState.SetBool(Key+"Done",true);
        }
    }
}
#endif
