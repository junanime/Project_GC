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
    // An intentionally simple reactive bot, not an estimate of human difficulty or clear rate.
    // Every encounter reloads the scene, keeps the real HP budget and uses normal skill cooldowns.
    [InitializeOnLoad]
    public static class ColonBossBalanceSmoke
    {
        const string Key="ColonBossBalanceSmoke",Output="Library/ColonBossBalanceProof",Source="Documentation/QA/Stage2CarryoverFixtures";
        const float SimulationSpeed=1.7f,EncounterSeconds=60;
        static readonly string[] Characters={"Main","Ari","Hyuki","Shini"};
        static bool started;static double deadline;
        static ColonBossBalanceSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()=>Begin(1);
        public static void RunThreeSeeds()=>Begin(3);
        static void Begin(int seeds)
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/errors.txt","");
            File.WriteAllText(Output+"/encounters.csv","character,boss,seed,field_second,seconds,wall_seconds,player_hp_start,player_hp_end,boss_hp_start,boss_hp_end,boss_damage,dps,damage_taken,normal_kills,normals_spawned,active_uses,dashes,patterns,bot_outcome\n");
            File.WriteAllText(Output+"/stationary.csv","character,seconds,damage,dps,needle_damage,needle_cooldown,needle_count\n");
            File.WriteAllText(Output+"/method.txt","Four historical stage-one automated build snapshots, restored without new upgrades. Stationary Turtle target: 20 game seconds, invulnerable, active skills excluded. Combat: 60 game seconds, actual HP and normal cooldowns, stage-two ordinary spawn table at 180 seconds, no field specials/events, no pickups/new upgrades. Bot reacts every 0.12 game seconds to nearby bodies/projectiles and map walls, keeps approximately 3 units from the boss, and attempts dash if crowded. It does not interpret warning shapes or remember patterns. Time scale 1.7. These are controlled regression measurements, NOT human success rates. Every boss/character/seed reloads the scene.\n");
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Round",0);SessionState.SetInt(Key+"Seeds",seeds);SessionState.SetBool(Key+"Failed",false);
            foreach(string pref in new[]{"Coins","LobbySilverCoins"}){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            Next();
        }
        static void Next()
        {
            Time.timeScale=1;SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Scene",true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+360;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Output+"/errors.txt","Round deadline\n");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                int next=SessionState.GetInt(Key+"Round",0)+1;bool fail=SessionState.GetBool(Key+"Failed",false);
                if(!fail&&next<24*SessionState.GetInt(Key+"Seeds",1)){SessionState.SetInt(Key+"Round",next);Next();return;}
                foreach(string pref in new[]{"Coins","LobbySilverCoins"}){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();
                Time.timeScale=1;SessionState.SetBool(Key,false);File.WriteAllText(Output+"/result.txt",fail?"FAIL":"PASS: real needle and reactive-bot measurements; not human win rates");EditorApplication.Exit(fail?1:0);return;
            }
            if(!EditorApplication.isPlaying)return;
            int round=SessionState.GetInt(Key+"Round",0);
            if(SessionState.GetBool(Key+"Scene",false))
            {
                SessionState.SetBool(Key+"Scene",false);CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/"+Characters[(round%24)/6]+" Character Blueprint.asset");
                CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();CrossSceneData.ClearPendingRunSceneTransfer();UnityEngine.SceneManagement.SceneManager.LoadScene("Level 1");return;
            }
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var level=Object.FindObjectOfType<LevelManager>();if(started||level==null||!level.InitialFieldObjectsReady||level.PlayerCharacter==null)return;
            started=true;level.StartCoroutine(Measure(level,round));
        }
        static void Silence(LevelManager level)
        {
            level.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())
                if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn){b.enabled=false;b.StopAllCoroutines();}
        }
        static void Clear(LevelManager level)
        {
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())if(!(m is BloodClotMonster))m.DespawnForStageTransfer();
            foreach(var p in Object.FindObjectsOfType<Projectile>())p.RemoveForItem();
            foreach(var pickup in Object.FindObjectsOfType<Collectable>())pickup.DissolveInAcidLake();
        }
        static void Restore(LevelManager level,RunSceneTransferSnapshot build)
        {
            build.CharacterState.SkillActiveRemaining=build.CharacterState.SkillCooldownRemaining=build.CharacterState.SkillSummonRemaining=0;
            var abilities=Object.FindObjectOfType<AbilityManager>();int restored=abilities.RestoreRunSceneAbilities(build.AbilityStates);
            if(restored!=build.AbilityStates.Count)throw new Exception("Incomplete carryover ability restore");
            var player=level.PlayerCharacter;player.RestoreRunSceneState(build.CharacterState);player.GainHealth(player.MaxHealth);player.CollectableCollider.enabled=false;
            OctoberItemRuntime.Get(player).Restore(build.OctoberItems);
            foreach(var ability in Object.FindObjectsOfType<Ability>())ability.enabled=true;
        }
        static string Row(params object[] values)=>string.Join(",",values.Select(v=>v is IFormattable f?f.ToString(null,CultureInfo.InvariantCulture):v.ToString()))+"\n";
        static IEnumerator Measure(LevelManager level,int round)
        {
            var pref=GamePreferences.Current.Copy();pref.pauseOnFocusLoss=false;pref.muteOnFocusLoss=false;GamePreferences.Apply(pref,false);Application.runInBackground=true;
            int ci=(round%24)/6,ki=round%6,seed=91873+(round/24)*1009+ci*71+ki*19;string name=Characters[ci];var kind=(ColonBossKind)ki;
            RunScore.Exclude("Stage-two optional boss controlled balance test");Silence(level);UnityEngine.Random.InitState(seed);
            var player=level.PlayerCharacter;ColonContentSmoke.Set(player,"isInvincible",true);
            yield return ColonContentSmoke.Enter(level);Silence(level);Clear(level);Time.timeScale=SimulationSpeed;
            if(ApothecaryUI.Instance!=null)ApothecaryUI.Instance.Show("hud");
            var build=JsonUtility.FromJson<RunSceneTransferSnapshot>(File.ReadAllText(Source+"/"+name+"-stage1-build.json"));Restore(level,build);
            if(round<24&&ki==0)yield return Stationary(level,name);
            Clear(level);Restore(level,build);yield return null;
            var home=level.GetComponent<ColonBossDirector>().Habitats.First(h=>h.Kind==kind);var map=FiniteStomachMap.Instance;
            Vector2 entry=kind==ColonBossKind.Manatee?home.Doors[1]+home.WallNormal*3:(Vector2)home.transform.position+Vector2.down*3;
            ColonContentSmoke.Position(player,map.ClampToField(entry,map.WallWidth+.5f));
            foreach(var ability in Object.FindObjectsOfType<Ability>())ability.enabled=false;
            var boss=ColonBossRuntime.Spawn(kind,player,home.transform.position,home);boss.AutoPatterns=false;
            float until=Time.time+20;while(boss.Arriving&&Time.time<until)yield return null;if(boss.Arriving)throw new Exception("Arrival timeout "+kind);
            Restore(level,build);ColonContentSmoke.Set(player,"isInvincible",false);
            ColonContentSmoke.Set(level,"levelTime",180f);level.enabled=true;boss.AutoPatterns=true;
            var stats=Object.FindObjectOfType<StatsManager>();float taken=stats.DamageTaken,health=player.CurrentHealth,hp=boss.Health,began=Time.time,wall=Time.realtimeSinceStartup;
            int kills=stats.NormalMonstersKilled,spawned=level.NormalMonstersSpawned,active=0,dashes=0;float nextThink=0,nextSkill=0,nextDash=0;Vector2 direction=Vector2.zero;
            string outcome="time_limit";
            while(Time.time-began<EncounterSeconds)
            {
                if(player.CurrentHealth<=0||!player.IsAlive||level.IsLevelEnded){outcome="bot_defeated";break;}
                if(boss==null||boss.Dead){outcome="boss_defeated";break;}
                var dialog=level.EntityManager.AbilitySelectionDialog;if(dialog.MenuOpen)dialog.Close();
                if(ApothecaryUI.Instance!=null&&ApothecaryUI.Instance.Page!="hud")ApothecaryUI.Instance.Show("hud");
                if(Time.time>=nextThink)
                {
                    nextThink=Time.time+.12f;direction=ChooseDirection(player,boss,level,round%2==0?1:-1);player.Move(direction);
                    if(Time.time>=nextSkill&&player.Skills.CanActivate)
                    {player.Move(((Vector2)boss.transform.position-(Vector2)player.transform.position).normalized);if(player.Skills.TryActivate())active++;nextSkill=Time.time+.5f;}
                    float nearest=level.EntityManager.LivingMonsters.Where(m=>m!=null&&!(m is BloodClotMonster)).Select(m=>Vector2.Distance(m.transform.position,player.transform.position)).DefaultIfEmpty(99).Min();
                    float bossDistance=Vector2.Distance(boss.transform.position,player.transform.position);
                    if(Time.time>=nextDash&&(nearest<1.6f||bossDistance<2||player.Skills.IsAri&&player.Skills.Active))
                    {player.Move(direction);if(player.TryDash())dashes++;nextDash=Time.time+.7f;}
                    File.WriteAllText(Output+"/status.txt",name+" / "+kind+" / "+(Time.time-began).ToString("F1")+" / HP "+player.CurrentHealth.ToString("F1"));
                }
                yield return null;
            }
            player.Move(Vector2.zero);level.enabled=false;
            float elapsed=Time.time-began,remaining=boss!=null?Mathf.Max(0,boss.Health):0,damage=hp-remaining;
            File.AppendAllText(Output+"/encounters.csv",Row(name,kind,seed,180,elapsed,Time.realtimeSinceStartup-wall,health,Mathf.Max(0,player.CurrentHealth),hp,remaining,damage,damage/Mathf.Max(.01f,elapsed),stats.DamageTaken-taken,stats.NormalMonstersKilled-kills,level.NormalMonstersSpawned-spawned,active,dashes,boss!=null?boss.CompletedPatterns:0,outcome));
            SessionState.SetBool(Key+"Done",true);
        }
        static Vector2 ChooseDirection(Character player,ColonBossRuntime boss,LevelManager level,int orbit)
        {
            var at=(Vector2)player.transform.position;var target=(Vector2)boss.transform.position;var map=FiniteStomachMap.Instance;
            var enemies=level.EntityManager.LivingMonsters.Where(m=>m!=null&&!(m is BloodClotMonster)&&Vector2.SqrMagnitude((Vector2)m.transform.position-at)<36).ToArray();
            var bullets=Object.FindObjectsOfType<ColonBossProjectile>();Vector2 radial=(at-target).normalized,tangent=Vector2.Perpendicular(radial)*orbit;
            Vector2 best=tangent;float scoreBest=float.NegativeInfinity;
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI/8;var dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));var next=at+dir*Mathf.Max(.55f,player.CurrentMoveSpeed*.55f);
                float wall=map.NavigationDistance(next,out _);if(wall<map.WallWidth+.3f)continue;
                float distance=Vector2.Distance(next,target),score=-Mathf.Abs(distance-3.2f)*1.8f+Vector2.Dot(dir,tangent)*.9f;
                if(distance<2.2f)score-=(2.2f-distance)*10;
                foreach(var enemy in enemies){float d=Vector2.Distance(enemy.transform.position,next);score-=5/Mathf.Max(.25f,d*d);}
                foreach(var bullet in bullets)
                {if(bullet==null)continue;float d=SnailBossProjectile.SegmentDistance(next,bullet.transform.position,(Vector2)bullet.transform.position+bullet.Velocity*.55f);if(d<1.2f)score-=(1.2f-d)*9;}
                if(score>scoreBest){scoreBest=score;best=dir;}
            }
            return best;
        }
        static IEnumerator Stationary(LevelManager level,string name)
        {
            var player=level.PlayerCharacter;var map=FiniteStomachMap.Instance;var at=map.ClampToField(map.UVToWorld(new Vector2(.76f,.72f)),map.WallWidth+8);
            ColonContentSmoke.Position(player,at);player.Move(Vector2.right);player.Move(Vector2.zero);ColonContentSmoke.Set(player,"isInvincible",true);
            var target=ColonBossRuntime.Spawn(ColonBossKind.Turtle,player,at+Vector2.right*2.5f,null);target.AutoPatterns=false;
            float until=Time.time+20;while(target.Arriving&&Time.time<until)yield return null;
            target.enabled=false;var rb=target.GetComponent<Rigidbody2D>();rb.constraints=RigidbodyConstraints2D.FreezeAll;
            float hp=target.Health,began=Time.time;while(Time.time-began<20&&!target.Dead)yield return null;
            float damage=hp-target.Health,elapsed=Time.time-began;var dart=Object.FindObjectOfType<SyringeDartAbility>();
            File.AppendAllText(Output+"/stationary.csv",Row(name,elapsed,damage,damage/Mathf.Max(.01f,elapsed),dart.GetEffectiveDamage(),dart.GetEffectiveCooldown(),dart.GetEffectiveProjectileCount()));
            Object.Destroy(target.gameObject);yield return null;
        }
    }
}
#endif
