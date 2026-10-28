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
namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class ColonContentSmoke
    {
        const string Key="ColonContentSmoke",Output="D:/Project_GC/work/colon-content-proof";
        static bool started;static double deadline;static int checks;
        static ColonContentSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void RunNavigation() { Run(); SessionState.SetBool(Key+"Navigation",true); }
        public static void Run()
        {
            SessionState.SetBool(Key+"Navigation",false);
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/checks.txt","");File.WriteAllText(Output+"/errors.txt","");
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+1200;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string t,string s,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",t+"\n"+s+"\n");SessionState.SetBool(Key+"Failed",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);File.AppendAllText(Output+"/errors.txt","Deadline\n");}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                SessionState.SetBool(Key,false);bool fail=SessionState.GetBool(Key+"Failed",false);File.WriteAllText(Output+"/result.txt",(fail?"FAIL":"PASS")+" checks="+checks);EditorApplication.Exit(fail?1:0);return;
            }
            if(!EditorApplication.isPlaying)return;
            var tutorial=Object.FindObjectOfType<TutorialGuide>();if(tutorial!=null){tutorial.Close();tutorial.enabled=false;}
            var level=Object.FindObjectOfType<LevelManager>();if(started||level==null||!level.InitialFieldObjectsReady||level.PlayerCharacter==null)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        internal static FieldInfo Field(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}throw new Exception(n);}
        internal static void Set(object o,string n,object v)=>Field(o,n).SetValue(o,v);
        static void Check(bool ok,string t){if(!ok)throw new Exception("[ColonContent] "+t);checks++;File.AppendAllText(Output+"/checks.txt",t+"\n");}
        internal static void Position(Character p,Vector2 at){p.transform.position=at;p.GetComponent<Rigidbody2D>().position=at;p.GetComponent<Rigidbody2D>().velocity=Vector2.zero;Physics2D.SyncTransforms();}
        internal static IEnumerator Enter(LevelManager level)
        {
            level.SetRunFlowPaused(true);
            // Match the real capsule transition: leave stage-one loose rewards behind.
            foreach(var pickup in Object.FindObjectsOfType<Collectable>())pickup.DissolveInAcidLake();
            foreach(var orb in Object.FindObjectsOfType<SupplyRelicOrb>())Object.Destroy(orb.gameObject);
            foreach(var capsule in Object.FindObjectsOfType<SupplyCapsule>())Object.Destroy(capsule.gameObject);
            FiniteStomachMap.Instance.ChangeDefinition(Resources.Load<StomachMapDefinition>("ColonMap/Stage2"));
            var map=FiniteStomachMap.Instance;
            Position(level.PlayerCharacter,map.ClampToField(map.UVToWorld(new Vector2(.78f,.72f)),map.WallWidth+2));
            Set(StageProgression.Ensure(level),"<StageNumber>k__BackingField",2);
            level.BeginNextStage(ColonMonsterRoster.Create(level));level.SetRunFlowPaused(true);
            yield return level.GetComponent<FieldObjectLayout>().RebuildForStage();
            level.SetRunFlowPaused(false);
        }
        static void NavigationProfile(FiniteStomachMap map)
        {
            var points=new List<Vector2>();
            foreach(float y in new[]{.22f,.4f,.6f,.78f})foreach(float x in new[]{.18f,.38f,.62f,.82f})
                points.Add(map.ClampToField(map.UVToWorld(new Vector2(x,y)),map.WallWidth+2));
            var rows=new List<string>{"pass,from,target,dx,dy"};
            var timer=System.Diagnostics.Stopwatch.StartNew();
            for(int pass=0;pass<3;pass++)for(int from=0;from<16;from++)for(int target=0;target<8;target++)
            {
                var direction=ColonNavigation.Toward(points[from],points[target*2]);
                if(float.IsNaN(direction.x)||float.IsNaN(direction.y)||direction.sqrMagnitude>1.001f)throw new Exception("Invalid navigation direction");
                rows.Add($"{pass},{from},{target},{direction.x:R},{direction.y:R}");
            }
            timer.Stop();File.WriteAllLines(Output+"/navigation.csv",rows);
            File.WriteAllText(Output+"/navigation-ms.txt",timer.Elapsed.TotalMilliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
            Check(rows.Count==385,"384 multi-target route queries return valid directions");
        }
        static Monster Spawn(LevelManager l,MonsterBlueprint bp,Vector2 p,bool mini=false)
        {
            var pools=l.CurrentLevelBlueprint.monsters;
            int pool=Array.FindIndex(pools,c=>c.monsterBlueprints.Contains(bp));
            if(pool<0)pool=Array.FindIndex(pools,c=>c.monsterBlueprints.Any(b=>ColonMonsterRoster.Active.Resolve(b)==bp));
            if(pool<0&&bp is SugarCubeMonsterBlueprint)pool=2;
            return l.EntityManager.SpawnMonster(pool,p,bp,0,mini);
        }
        static IEnumerator Checks(LevelManager level)
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;Time.timeScale=1;
            RunScore.Exclude("Stage 2 content validation");level.enabled=false;Set(level.PlayerCharacter,"isInvincible",true);level.PlayerCharacter.CollectableCollider.enabled=false;
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner){b.enabled=false;b.StopAllCoroutines();}
            var original=level.CurrentLevelBlueprint;float slimeHp=original.monsters[0].monsterBlueprints[0].hp;
            var dying=level.EntityManager.SpawnMonster(0,level.PlayerCharacter.transform.position+Vector3.left*2,original.monsters[0].monsterBlueprints[0]);
            dying.TakeDamage(99999);Check(!level.EntityManager.LivingMonsters.Contains(dying),"killed monster leaves living list before its death animation ends");
            var stageOneDirector=Object.FindObjectOfType<MiniStageDirector>();
            stageOneDirector.OpenEntrancePortal(level.PlayerCharacter.transform.position);
            var oldPortal=(BloodClotMiniStagePortal)Field(stageOneDirector,"activeEntrancePortal").GetValue(stageOneDirector);
            Check(oldPortal!=null,"stage-one entrance exists before transfer");
            yield return Enter(level);Check(oldPortal==null||!oldPortal.gameObject.activeInHierarchy,"stage-one entrance removed on field transfer");var roster=ColonMonsterRoster.Active;var map=FiniteStomachMap.Instance;var player=level.PlayerCharacter;
            if(SessionState.GetBool(Key+"Navigation",false)){NavigationProfile(map);SessionState.SetBool(Key+"Done",true);yield break;}
            Check(roster.Normals.Count==8&&roster.Elites.Count==8&&roster.Specials.Count==6,"8 ordinary, 8 elite variants and 6 newly illustrated specials");
            Check(original.monsters[0].monsterBlueprints[0].hp==slimeHp,"source stage-one blueprint is immutable");
            var layout=level.GetComponent<FieldObjectLayout>();Check(layout.Ready&&layout.SpawnedObjects.Count==86,"all 86 initial objects active");
            for(int k=0;k<FieldObjectLayout.Counts.Length;k++)Check(layout.Slots.Count(s=>(int)s.kind==k)==FieldObjectLayout.Counts[k],"initial object quantity "+(FieldObjectLayout.Kind)k);
            Check(level.EntityManager.LivingMonsters.OfType<BloodClotMonster>().Count()==14,"all 14 blood clots live");
            Check(layout.SpawnedObjects.All(o=>map.NavigationDistance(o.transform.position,out _)>=9.9f),"all actual object positions clear internal and external walls");
            for(int seed=0;seed<128;seed++)
            {
                var slots=FieldObjectLayout.Generate(map,player.transform.position,seed);
                Check(slots.Count==86&&slots.All(s=>map.NavigationDistance(s.position,out _)>=9.99f),"valid complete layout seed "+seed);
                if(seed%4==0)yield return null;
            }
            var table=level.CurrentLevelBlueprint.monsterSpawnTable;
            for(int second=0;second<=900;second+=5)
            {
                int tier=Array.FindLastIndex(ColonMonsterRoster.IntroductionSeconds,t=>t<=second);
                float sum=0;for(int i=0;i<roster.NormalFlatStart+16;i++)
                {float p=table.GetProbability(second/900f,i);sum+=p;Check(p==0||(i>=roster.NormalFlatStart&&i<=roster.NormalFlatStart+tier),"spawn tier time="+second+" index="+i);}
                Check(Mathf.Abs(sum-1)<.001f,"normalized spawn weights "+second);
                Check(Mathf.Approximately(table.GetSpawnRate(second/900f),original.monsterSpawnTable.GetSpawnRate(second/900f)),"base spawn density unchanged "+second);
            }
            var center=(Vector2)player.transform.position;var rows=new List<string>{"name,id,height,actual_height,hp,feet_radius,elite"};
            foreach(var entry in roster.Normals.Concat(roster.Elites).Concat(roster.Specials))
            {
                var monster=Spawn(level,entry.blueprint,center+Vector2.left*2);Check(monster!=null,"spawn "+entry.name);
                if(entry.id.StartsWith("S"))Check(RunScore.IsSpecial(monster)&&!MonsterNeedleBarrier.Eligible(monster),"special score and membrane exclusion "+entry.name);
                else if(!entry.elite)Check(!RunScore.IsSpecial(monster)&&MonsterNeedleBarrier.Eligible(monster),"ordinary score and membrane eligibility "+entry.name);
                monster.SetFieldRuntimeSuspended(true);yield return null;
                float hp=entry.elite?entry.hp*4.5f:entry.hp;
                Check(Mathf.Abs(monster.SpawnMaxHealth-hp)<.01f,"health "+entry.name);
                Check(Mathf.Abs(monster.BodyRenderer.bounds.size.y-entry.height)<.015f,"exact visible body height "+entry.name);
                Check(monster.BodyRenderer.sprite==entry.sprite,"approved sprite "+entry.name);
                Check(monster.BodyHitbox.bounds.size.y<=entry.height*1.1f&&monster.BodyHitbox.bounds.size.y>entry.height*.45f,"body hitbox fits "+entry.name);
                Check(monster.GetComponent<CircleCollider2D>().bounds.size.x<monster.BodyRenderer.bounds.size.x*1.05f,"ground collision fits "+entry.name);
                rows.Add(entry.name+","+entry.id+","+entry.height+","+monster.BodyRenderer.bounds.size.y+","+monster.SpawnMaxHealth+","+monster.GetComponent<CircleCollider2D>().radius+","+entry.elite);
                monster.DespawnForStageTransfer();yield return null;
            }
            File.WriteAllLines(Output+"/actual-roster.csv",rows);
            // Every altar/overcharge uses only elites introduced in the current time segment.
            foreach(float elapsed in new[]{0f,120f,360f,720f})
            {
                Set(level,"levelTime",elapsed);
                for(int attempt=0;attempt<12;attempt++)
                {
                    var elite=level.EntityManager.SpawnOverchargeElite(center+Vector2.left*3);
                    int tier=roster.Elites.FindIndex(e=>e.blueprint==elite.Blueprint);
                    Check(tier>=0&&ColonMonsterRoster.IntroductionSeconds[tier]<=elapsed,"overcharge matches unlocked colon tier at "+elapsed);
                    elite.DespawnForStageTransfer();
                }
                var altar=Object.FindObjectOfType<EliteMonsterSummonInteractable>();
                typeof(EliteMonsterSummonInteractable).GetMethod("BuildMappingDictionary",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(altar,null);
                var plan=(IList)typeof(EliteMonsterSummonInteractable).GetMethod("PlanWave",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(altar,new object[]{3,player});
                Check(plan.Count==3,"altar preserves three-monster wave even with few unlocked types at "+elapsed);
                foreach(var request in plan)
                {
                    var bp=(MonsterBlueprint)Field(request,"blueprint").GetValue(request);int tier=roster.Elites.FindIndex(e=>e.blueprint==bp);
                    Check(tier>=0&&ColonMonsterRoster.IntroductionSeconds[tier]<=elapsed,"altar excludes future elite tier at "+elapsed);
                }
            }
            Set(level,"levelTime",0f);
            var shooter=Spawn(level,roster.Specials.First(e=>e.id=="S01").blueprint,center+Vector2.left*3);
            shooter.StopAllCoroutines();
            typeof(SniperMonster).GetMethod("FireSniperProjectile",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shooter,new object[]{center});
            var popcorn=Object.FindObjectsOfType<ColonPopcornProjectile>().First();
            var bullet=popcorn.GetComponent<Projectile>();var sprite=popcorn.GetComponent<SpriteRenderer>();var collider=popcorn.GetComponent<CapsuleCollider2D>();
            Check(sprite.sprite.name=="Popcorn"&&Mathf.Abs(sprite.bounds.size.y-.2f)<.01f,"corn fires actual popcorn art at defined size");
            Check(collider.bounds.size.y>sprite.bounds.size.y*.65f&&collider.bounds.size.y<sprite.bounds.size.y*1.05f,"popcorn collider follows visible kernel");
            ColonPopcornProjectile.Apply(bullet,false);
            Check(sprite.sprite.name!="Popcorn"&&Mathf.Abs(collider.size.x-1.3f)<.01f,"mini-stage bullet art and collider restore from shared pool");
            bullet.RemoveForItem();shooter.DespawnForStageTransfer();yield return null;
            // Pool round trip: the mini-stage must regain the original sprite, dimensions and HP.
            foreach(int pool in new[]{2,8,9,14,15})
            {
                var bp=original.monsters[pool].monsterBlueprints[0];var colon=level.EntityManager.SpawnMonster(pool,center+Vector2.left*2,bp);
                yield return null;colon.DespawnForStageTransfer();yield return null;
                var mini=level.EntityManager.SpawnMonster(pool,center+Vector2.left*2,bp,0,true);yield return null;
                Check(mini.Blueprint==bp&&mini.GetComponent<ColonMonsterVisual>()?.Entry==null,"mini-stage original blueprint restored pool "+pool);
                mini.DespawnForStageTransfer();yield return null;
            }
            var sugar=(SugarCubeMonsterBlueprint)roster.Specials.First(e=>e.id=="S06").blueprint;
            var brood=Spawn(level,sugar,center+Vector2.left*2);brood.TakeDamage(99999);yield return new WaitForSeconds(.5f);
            var kids=level.EntityManager.LivingMonsters.Where(m=>m.Blueprint==sugar.splitChildBlueprint).ToArray();
            Check(kids.Length==2&&kids.All(m=>m.GetComponent<ColonMonsterVisual>()?.Entry?.id=="S03"),"colony splits into two illustrated peapods");
            foreach(var kid in kids)kid.TakeDamage(99999);yield return new WaitForSeconds(.5f);
            var babies=level.EntityManager.LivingMonsters.Where(m=>m.Blueprint==sugar.splitChildBlueprint.splitChildBlueprint).ToArray();
            Check(babies.Length==4&&babies.All(m=>m.GetComponent<ColonMonsterVisual>()?.Entry?.id=="Pea"),"two peapods split into four baby peas");
            foreach(var m in babies){Check(RunScore.IsSpecial(m)&&!MonsterNeedleBarrier.Eligible(m),"split pea remains special");m.DespawnForStageTransfer();}
            var ordinary=Spawn(level,roster.Normals[0].blueprint,center+Vector2.left*2);
            var barrier=ordinary.gameObject.AddComponent<MonsterNeedleBarrier>();barrier.Arm();float before=ordinary.HP;
            ordinary.TakeDamage(999);Check(ordinary.HP==before,"new ordinary shield blocks health damage");
            for(int i=0;i<3;i++)Check(barrier.AbsorbNeedle(ordinary.BodyRenderer,Vector2.right,ordinary.transform.position),"new ordinary membrane needle "+(i+1));
            Check(!barrier.IsProtected&&barrier.Hits==3,"third needle pops membrane");ordinary.TakeDamage(1);Check(ordinary.HP==before-1,"health damage resumes on fourth hit");ordinary.DespawnForStageTransfer();
            foreach(var pickup in Object.FindObjectsOfType<Collectable>())pickup.DissolveInAcidLake();
            yield return null;
            int silverBefore=SilverRunRewarder.Instance!=null?SilverRunRewarder.Instance.RunEarnedSilver:0;
            var rewardElite=Spawn(level,roster.Elites[0].blueprint,center+Vector2.left*6);rewardElite.TakeDamage(99999);yield return new WaitForSeconds(.2f);
            Check(Object.FindObjectsOfType<Coin>().Sum(c=>(int)c.CoinType)==25,"actual colon elite drops 25 field gold");
            int silverDelta=SilverRunRewarder.Instance.RunEarnedSilver-silverBefore;
            Check(silverDelta>=3&&silverDelta<=6,"actual colon elite grants three silver reward rolls");
            var rewardGems=Object.FindObjectsOfType<ExpGem>();
            Check(rewardGems.Length==1&&new[]{10,50}.Contains((int)(GemType)Field(rewardGems[0],"gemType").GetValue(rewardGems[0])),"actual elite drops configured XP pickup");
            var clot=level.EntityManager.LivingMonsters.OfType<BloodClotMonster>().First();var clotPos=(Vector2)clot.transform.position;
            Position(player,clotPos+Vector2.left*1);clot.TakeDamage(99999);yield return new WaitForSeconds(.4f);
            var director=Object.FindObjectOfType<MiniStageDirector>();var portal=(BloodClotMiniStagePortal)Field(director,"activeEntrancePortal").GetValue(director);
            Check(portal!=null,"stage-two clot death opens entrance");
            var secondClot=level.EntityManager.LivingMonsters.OfType<BloodClotMonster>().First();secondClot.TakeDamage(99999);yield return new WaitForSeconds(.4f);
            var secondPortal=(BloodClotMiniStagePortal)Field(director,"activeEntrancePortal").GetValue(director);
            Check(portal!=null&&secondPortal!=null&&portal!=secondPortal,"multiple preplaced clot entrances persist independently");
            float clock=level.CurrentLevelTime;
            director.EnterMiniStageFromPortal(portal);float limit=Time.time+20;
            while((!director.IsInsideMiniStage||director.IsTransitioning)&&Time.time<limit)yield return null;
            Check(director.IsInsideMiniStage&&!director.IsTransitioning,"stage-two player enters mini-stage");Check(Mathf.Abs(clock-level.CurrentLevelTime)<.1f,"field clock held during mini-stage");
            var room=director.CurrentRoom;Set(room,"optionalReturnUnlocked",true);Set(room,"awaitingSkillReward",false);room.ReturnInteractable.SetUnlocked(true);
            director.ReturnToFieldFromInteractable(room.ReturnInteractable);limit=Time.time+20;
            while((director.IsInsideMiniStage||director.IsTransitioning)&&Time.time<limit)yield return null;
            Check(!director.IsInsideMiniStage&&!director.IsTransitioning,"mini-stage returns to colon");
            Check(secondPortal!=null&&secondPortal.gameObject.activeInHierarchy,"unused second entrance survives first entrance round trip");
            Check(map.AppliesAt(player.transform.position)&&Vector2.Distance(player.transform.position,clotPos)<4,"return point stays beside original clot");
            Check(ColonMonsterRoster.Active==roster&&layout.SpawnedObjects.Count==86,"mini-stage does not reroll colon roster or initial objects");
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
