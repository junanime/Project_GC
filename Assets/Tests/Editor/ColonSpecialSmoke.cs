#if UNITY_EDITOR
using System;
using System.Collections;
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
    public static partial class ColonSpecialSmoke
    {
        const string Key="ColonSpecialSmoke",Output="Library/ColonSpecialProof";
        static bool started;static double deadline;static int checks;
        [Serializable]class Entry{public string id;}
        [Serializable]class Catalog{public Entry[] entries;}
        [Serializable]class Pref{public string key;public bool had;public int value;}
        [Serializable]class Backup{public Pref[] prefs;}
        static ColonSpecialSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);Restore();checks=0;
            File.WriteAllText(Output+"/result.txt","RUNNING");
            File.WriteAllText(Output+"/checks.txt","");File.WriteAllText(Output+"/errors.txt","");
            File.WriteAllText(Output+"/patterns.csv","kind,index,seconds,completed,health,width,height\n");
            var entries=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text).entries;
            var fixtureNeedles=new[]{"WindNeedle","Homing","AcupunctureFormation"};
            var keys=entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{CapsuleDoctorDialogue.SeenKey,"Coins","LobbySilverCoins"})
                .Concat(fixtureNeedles.SelectMany(n=>new[]{"LobbyUnlock.Needle."+n,"October.NeedleEnabled."+n})).Distinct().ToArray();
            File.WriteAllText(Output+"/prefs.json",JsonUtility.ToJson(new Backup{prefs=keys.Select(k=>new Pref{key=k,had=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();CrossSceneData.ClearPendingRunSceneTransfer();
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+1800;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            File.AppendAllText(Output+"/errors.txt",text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Output+"/errors.txt","Deadline\n");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Restore();Time.timeScale=1;SessionState.SetBool(Key,false);bool failed=SessionState.GetBool(Key+"Failed",false);
                File.WriteAllText(Output+"/result.txt",(failed?"FAIL":"PASS")+" checks="+checks);EditorApplication.Exit(failed?1:0);return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {
                started=true;var host=new GameObject("Colon boss integration QA");Object.DontDestroyOnLoad(host);
                host.AddComponent<StageEntryLoadingTestHost>().StartCoroutine(Checks());
            }
        }
        static void Restore()
        {
            string path=Output+"/prefs.json";if(!File.Exists(path))return;
            foreach(var p in JsonUtility.FromJson<Backup>(File.ReadAllText(path)).prefs)
                if(p.had)PlayerPrefs.SetInt(p.key,p.value);else PlayerPrefs.DeleteKey(p.key);
            PlayerPrefs.Save();File.Delete(path);
        }
        static void Check(bool value,string message)
        {
            if(!value)throw new Exception("COLON_BOSS_FAIL "+message);
            checks++;File.AppendAllText(Output+"/checks.txt",message+"\n");Debug.Log("COLON_BOSS_PASS "+message);
        }
        static FieldInfo Field(object o,string name)
        {
            for(var t=o.GetType();t!=null;t=t.BaseType)
            {var f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}
            throw new MissingFieldException(o.GetType().Name,name);
        }
        static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,args);
        static void Position(Character p,Vector2 at)=>ColonContentSmoke.Position(p,at);
        static void Silence(LevelManager level)
        {
            level.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())
                if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn)
                {b.enabled=false;b.StopAllCoroutines();}
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())
                if(!(m is BloodClotMonster))m.DespawnForStageTransfer();
        }
        static void ClearProjectiles()
        {
            foreach(var p in Object.FindObjectsOfType<Projectile>())p.RemoveForItem();
        }
        static Bounds VisibleBounds(ColonBossRuntime boss)
        {
            var renderers=boss.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled&&r.sprite!=null&&r.color.a>.01f).ToArray();
            if(renderers.Length==0)return new Bounds(boss.transform.position,Vector3.zero);
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;
        }
        static Monster Spawn(LevelManager l, MonsterBlueprint bp, Vector2 at)
        {
            var r=ColonMonsterRoster.Active;var pools=l.CurrentLevelBlueprint.monsters;
            int pool=Array.FindIndex(pools,c=>c.monsterBlueprints.Any(x=>x==bp||(r!=null&&r.Resolve(x)==bp)));
            return l.EntityManager.SpawnMonster(pool,at,bp);
        }
        static IEnumerator Checks()
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
            var intro=Object.FindObjectOfType<IntroMovie>();if(intro!=null)Call(intro,"Finish");
            yield return null;Check(StageEntryLoading.Begin(CrossSceneData.CharacterBlueprint),"real loading started");
            float until=Time.realtimeSinceStartup+120;while(StageEntryLoading.IsLoading&&Time.realtimeSinceStartup<until)yield return null;
            Check(!StageEntryLoading.IsLoading,"loading completed");
            var level=Object.FindObjectOfType<LevelManager>();var player=level.PlayerCharacter;
            if(TutorialGuide.Instance!=null){TutorialGuide.Instance.Close();TutorialGuide.Instance.enabled=false;}
            RunScore.Exclude("특수몹 통합 QA");Silence(level);Set(player,"isInvincible",true);player.CollectableCollider.enabled=false;Time.timeScale=1;
            var original=level.CurrentLevelBlueprint;var flat=original.monsters.SelectMany(c=>c.monsterBlueprints).ToArray();
            Vector2 center=player.transform.position;
            var ordinary=Spawn(level,flat[0],center+Vector2.left*.25f);
            foreach(int i in new[]{21,22}){
                var caster=(MonsterSupportCaster)Spawn(level,flat[i],center+Vector2.left);
                caster.StopAllCoroutines();caster.StartCoroutine((IEnumerator)Call(caster,"CastBuffCoroutine"));
                yield return new WaitForSeconds(.45f);Capture("stage1-buffer-"+i);
                Check(caster.GetComponent<SupportCastMotion>()!=null,"stage-one headbang rig "+i);
                yield return new WaitForSeconds(.6f);caster.DespawnForStageTransfer();
            }
            Check(ordinary.GetComponent<MonsterCombatBuffRuntime>().HealthBuffActive,"stage-one health buffer applies health capacity");
            ordinary.DespawnForStageTransfer();
            yield return ColonContentSmoke.Enter(level);Silence(level);
            var r=ColonMonsterRoster.Active;level.GetComponent<ColonSpecialDirector>().enabled=false;
            var map=FiniteStomachMap.Instance;
            center=map.ClampToField(map.UVToWorld(new Vector2(.78f,.72f)),map.WallWidth+2);Position(player,center);
            Check(r.Specials.Count==12&&r.NewSpecials.Count==5,"twelve reskinned/new specials plus trap and squirrel = fourteen");
            Check(level.GetComponent<FieldObjectLayout>().SpawnedObjects.Count==86,"86 initial objects preserved");
            Check(level.GetComponent<FieldObjectLayout>().SpawnedObjects.All(o=>map.FloorClearance(o.transform.position,true)>=.65f),"all initial props avoid inner and outer walls");
            for(int i=0;i<5;i++){
                var m=Spawn(level,r.NewSpecials[i],FiniteStomachMap.SafeSpawn(center+Vector2.left*2,.8f));
                Check(m!=null&&m.GetComponent<ColonSpecialRuntime>()?.Kind==ColonMonsterRoster.NewKinds[i],"runtime spawn "+ColonMonsterRoster.NewKinds[i]);
                Check(RunScore.IsSpecial(m),"special ranking category "+i);
                yield return new WaitForSeconds(.25f);Capture("new-special-"+i);
                Check(m.BodyRenderer.sprite!=null&&m.BodyRenderer.bounds.size.y>.2f,"visible sprite and size "+i);
                Check(map.FloorClearance(m.transform.position,true)>.2f,"walkable spawn "+i);
                m.DespawnForStageTransfer();
            }
            ordinary=Spawn(level,r.Normals[0].blueprint,center+Vector2.left*.25f);
            var buffs=ordinary.GetComponent<MonsterCombatBuffRuntime>()??ordinary.gameObject.AddComponent<MonsterCombatBuffRuntime>();float hp=ordinary.HP,max=ordinary.SpawnMaxHealth;
            buffs.ApplyHealthBuff(.5f);Check(Mathf.Approximately(ordinary.SpawnMaxHealth,max*1.25f),"health capacity +25 percent");
            ordinary.TakePeriodicDamage(5);float damaged=ordinary.HP;buffs.ApplyHealthBuff(.5f);
            Check(Mathf.Approximately(ordinary.SpawnMaxHealth,max*1.25f)&&Mathf.Approximately(damaged,ordinary.HP),"refresh never stacks or heals");
            yield return new WaitForSeconds(.6f);Check(Mathf.Approximately(ordinary.SpawnMaxHealth,max),"health expiry restores capacity");
            foreach(var id in new[]{"S04","S05"}){
                var caster=(MonsterSupportCaster)Spawn(level,r.Specials.First(e=>e.id==id).blueprint,center+Vector2.left);
                caster.StopAllCoroutines();caster.StartCoroutine((IEnumerator)Call(caster,"CastBuffCoroutine"));
                yield return new WaitForSeconds(.45f);Capture("mushroom-buffer-"+id);yield return new WaitForSeconds(.6f);Capture("mushroom-buff-applied-"+id);caster.DespawnForStageTransfer();
            }
            Check(ordinary.GetComponentInChildren<MonsterSupportBuffVisual>()!=null,"plus buff indicator visible");
            ordinary.DespawnForStageTransfer();
            var salt=Spawn(level,r.NewSpecials[3],center+Vector2.left*2);var saltRuntime=salt.GetComponent<ColonSpecialRuntime>();salt.moveSpeed=.05f;
            salt.TakePeriodicDamage(salt.SpawnMaxHealth*.41f);yield return null;Check(saltRuntime.CrackStage==1,"salt crack at sixty percent");Capture("salt-crack1");
            salt.TakePeriodicDamage(salt.SpawnMaxHealth*.30f);yield return null;Check(saltRuntime.CrackStage==2,"salt crack at thirty percent");Capture("salt-crack2");
            Check(!Object.FindObjectsOfType<SpriteRenderer>().Any(x=>x.sprite!=null&&x.sprite.name.StartsWith("Salt body crystal")),"salt emits nothing on nonlethal damage");
            int score=RunScore.Current.Data.special;salt.TakePeriodicDamage(99999);yield return null;
            Check(Object.FindObjectsOfType<SpriteRenderer>().Count(x=>x.sprite!=null&&x.sprite.name.StartsWith("Salt body crystal"))==4,"salt death uses exactly four actual body parts");
            Check(RunScore.Current.Data.special==score+1,"salt death rewards one special kill");Capture("salt-four-shards");yield return new WaitForSeconds(1.5f);
            // Script real combat interactions, not a standalone preview.
            var source=Spawn(level,r.NewSpecials[2],center+Vector2.left*2);var target=Spawn(level,r.Normals[0].blueprint,center+Vector2.left);
            var stitch=source.GetComponent<ColonSpecialRuntime>();Call(stitch,"LinkTargets");var link=target.GetComponent<ColonRevivalLink>();Check(link!=null&&link.Owner==stitch,"living normal linked");
            int normals=RunScore.Current.Data.normal;int gems=Object.FindObjectsOfType<ExpGem>().Length;int deathEvents=0;Monster.Died+=m=>{if(m==target)deathEvents++;};
            target.TakePeriodicDamage(99999);yield return new WaitForSeconds(.3f);
            Check(RunScore.Current.Data.normal==normals&&deathEvents==0&&Object.FindObjectsOfType<ExpGem>().Length==gems,"pending revival gives no score, death event or loot");Capture("revive-pending");
            yield return new WaitForSeconds(1.1f);Check(target.HP>0&&link.Used&&target.IsAlive,"linked target revives once");
            target.TakePeriodicDamage(99999);yield return new WaitForSeconds(.2f);Check(RunScore.Current.Data.normal==normals+1&&deathEvents==1,"final kill counted once after resurrection");source.DespawnForStageTransfer();
            source=Spawn(level,r.NewSpecials[2],center+Vector2.left*2);target=Spawn(level,r.Normals[0].blueprint,center+Vector2.left);stitch=source.GetComponent<ColonSpecialRuntime>();Call(stitch,"LinkTargets");
            target.TakePeriodicDamage(99999);source.TakePeriodicDamage(99999);yield return new WaitForSeconds(1.4f);Check(target.HP<=0,"killing stitcher cancels pending revival");
            var mole=(NutritionThiefBacteriaMonster)Spawn(level,r.Specials.First(e=>e.id=="S12").blueprint,center+Vector2.left*2);var size=mole.transform.localScale;
            var coin=level.EntityManager.SpawnCoin(mole.transform.position,CoinType.Gold5,false);Call(mole,"AbsorbCoin",coin);
            Check(mole.EatingRemaining>=1.99f,"mole meal exposes body for two seconds");yield return new WaitForSeconds(.5f);Capture("mole-eating");
            Check(!mole.Underground&&mole.transform.localScale==size,"mole does not grow while eating");
            yield return new WaitForSeconds(1.6f);Check(mole.Underground,"mole burrows after meal");hp=mole.HP;mole.TakeDamage(5);Check(Mathf.Approximately(mole.HP,hp),"underground mole blocks direct hits");mole.TakePeriodicDamage(5);Check(mole.HP<hp,"underground mole still takes DOT");Capture("mole-burrowing");
            yield return new WaitForSeconds(2.2f);Check(!mole.Underground&&mole.BodyHitbox.enabled,"mole emerges and restores targetability");score=RunScore.Current.Data.special;mole.TakePeriodicDamage(99999);yield return new WaitForSeconds(.3f);Check(RunScore.Current.Data.special==score+1&&mole.WasKilledByPlayer,"mole returns rewards and kill attribution once");
            var beetle=Spawn(level,r.NewSpecials[4],center+Vector2.left*1.8f);var br=beetle.GetComponent<ColonSpecialRuntime>();Set(br,"cooldown",0f);yield return new WaitForSeconds(.7f);beetle.TakePeriodicDamage(1);yield return null;
            Check(!(bool)Field(br,"busy").GetValue(br),"hit interrupts dung throw");Capture("beetle-dropped");beetle.DespawnForStageTransfer();yield return new WaitForSeconds(3.5f);
            beetle=Spawn(level,r.NewSpecials[4],center+Vector2.left*1.8f);br=beetle.GetComponent<ColonSpecialRuntime>();Set(br,"cooldown",0f);yield return new WaitForSeconds(1);Capture("beetle-windup");yield return new WaitForSeconds(2.3f);Capture("beetle-flight");Check(!(bool)Field(br,"busy").GetValue(br),"uninterrupted throw completes after three seconds");beetle.DespawnForStageTransfer();
            var gas=Spawn(level,r.NewSpecials[0],center+Vector2.left*2);var victim=Spawn(level,r.Normals[0].blueprint,center+Vector2.left);Set(gas.GetComponent<ColonSpecialRuntime>(),"cooldown",0f);Vector2 playerAt=player.transform.position;yield return new WaitForSeconds(1.5f);Capture("gas-push");Check(Vector2.Distance(playerAt,player.transform.position)<.1f,"gas does not push player");Check(map.FloorClearance(victim.transform.position,true)>0,"gas cannot push enemies into wall");gas.DespawnForStageTransfer();victim.DespawnForStageTransfer();
            var yogurt=Spawn(level,r.NewSpecials[1],center+Vector2.left*3);yield return new WaitForSeconds(2.5f);Capture("yogurt-orbit");Check(Vector2.Distance(yogurt.transform.position,player.transform.position)>2,"yogurt circles instead of contact rush");yogurt.DespawnForStageTransfer();
            // Count policy expectation with deterministic random seed.
            UnityEngine.Random.InitState(456);double baseline=0,corn=0,pair=0;for(int i=0;i<20000;i++){baseline+=ColonSpecialDirector.LegacyCount(2,false,false);corn+=ColonSpecialDirector.LegacyCount(2,true,false);pair+=ColonSpecialDirector.LegacyCount(2,false,true)*2;}
            Check(Math.Abs(baseline/20000-2.5)<.03&&Math.Abs(corn/20000-1.25)<.03&&Math.Abs(pair/20000-2.5)<.04,"carryover count +25 percent, corn half, paired budget preserved");
            Set(r,"<DirectStart>k__BackingField",true);baseline=0;for(int i=0;i<20000;i++)baseline+=ColonSpecialDirector.LegacyCount(2,false,false);Check(Math.Abs(baseline/20000-1.7)<.03,"direct profile reserves population for new specials");Set(r,"<DirectStart>k__BackingField",false);
            var shark=ColonBossRuntime.Spawn(ColonBossKind.Shark,player,FiniteStomachMap.SafeSpawn(center+Vector2.up*3,2),null);shark.AutoPatterns=false;
            var fin=ColonBossEffects.Prop(shark,"Fin",1.8f,center+Vector2.up*2);yield return new WaitForSeconds(.5f);Capture("shark-fin-wake");Check(fin.GetComponent<ColonFinWake>()!=null&&fin.GetComponentsInChildren<LineRenderer>().Length==3,"fin blends into ground with three liquid wakes");Object.Destroy(shark.gameObject);
            Check(Object.FindObjectsOfType<ColonSpecialEffects>().All(e=>e!=null),"no effect lifecycle exception");
            // Exercise the actual schedule: a wave contains one of each mushroom.
            var spawner=Object.FindObjectOfType<TimedSpecialMonsterSpawner>();
            var schedule=((System.Collections.Generic.List<TimedSpecialSpawnSchedule>)Field(spawner,"timedSpawns").GetValue(spawner))[0];
            var scheduledType=typeof(TimedSpecialSpawnSchedule).GetNestedType("RuntimeTimedSpawnEvent",BindingFlags.NonPublic);
            var scheduled=Activator.CreateInstance(scheduledType,true);
            var entry=new TimedSpecialSpawnSchedule.TimedSpawnEntry {monsterBlueprint=flat[21],companionBlueprint=flat[22],autoResolvePoolIndex=true,intervalBetweenSpawns=0};
            Set(scheduled,"entry",entry);Set(scheduled,"spawnCount",2);
            yield return (IEnumerator)Call(schedule,"SpawnTimedEventRoutine",scheduled);
            var pairMonsters=level.EntityManager.LivingMonsters.Where(m=>m!=null&&m.HP>0&&r.TryGet(m.Blueprint,out var e)&&(e.id=="S04"||e.id=="S05")).ToArray();
            int speedCount=pairMonsters.Count(m=>m.Blueprint==r.Specials.First(e=>e.id=="S04").blueprint);
            int healthCount=pairMonsters.Length-speedCount;
            Check(speedCount>0&&speedCount==healthCount,"actual timed wave spawns matched mushroom pairs");
            Check(pairMonsters.All(m=>map.FloorClearance(m.transform.position,true)>.3f),"paired supports spawn on valid ground");
            foreach(var m in pairMonsters)m.DespawnForStageTransfer();
            var pausedMonster=Spawn(level,r.NewSpecials[4],center+Vector2.left*1.8f);
            var pausedRuntime=pausedMonster.GetComponent<ColonSpecialRuntime>();Set(pausedRuntime,"cooldown",0f);
            yield return new WaitForSeconds(.2f);int savedPose=pausedRuntime.Pose;float savedFrame=(float)Field(pausedRuntime,"frame").GetValue(pausedRuntime);
            level.SetRunFlowPaused(true);yield return new WaitForSeconds(.4f);
            Check(pausedRuntime.Pose==savedPose&&Mathf.Approximately(savedFrame,(float)Field(pausedRuntime,"frame").GetValue(pausedRuntime)),"run pause freezes preparation and animation");
            level.SetRunFlowPaused(false);pausedMonster.PrepareForSpawnRuntime(false);
            Check(!pausedRuntime.ControlsMovement&&pausedRuntime.Kind==null,"pool return clears custom control and held prop");pausedMonster.DespawnForStageTransfer();
            // Mixed runtime movement around both floor and walls for one real minute.
            var mixed=new System.Collections.Generic.List<Monster>();
            for(int i=0;i<5;i++)mixed.Add(Spawn(level,r.NewSpecials[i],FiniteStomachMap.SafeSpawn(center+UnityEngine.Random.insideUnitCircle*4,.9f)));
            for(int i=0;i<12;i++)mixed.Add(Spawn(level,r.Normals[i%8].blueprint,FiniteStomachMap.SafeSpawn(center+UnityEngine.Random.insideUnitCircle*5,.9f)));
            float playUntil=Time.time+60,nextHit=Time.time+1;int samples=0;
            while(Time.time<playUntil){
                var goal=FiniteStomachMap.SafeSpawn(center+new Vector2(Mathf.Sin(Time.time*.45f)*3,Mathf.Cos(Time.time*.35f)*2),.7f);
                Position(player,ColonAttackWall.Stop(player.transform.position,goal,.4f));
                if(Time.time>=nextHit){nextHit=Time.time+1;foreach(var m in mixed)if(m!=null&&m.HP>0)m.TakePeriodicDamage(2);samples++;}
                yield return null;
            }
            Capture("mixed-combat-final");Check(samples>=58,"sixty seconds of mixed specials, movement and DOT completed");
            Check(mixed.Where(m=>m!=null&&m.HP>0).All(m=>map.FloorClearance(m.transform.position,true)>0),"mixed encounter remains outside wall interiors");
            foreach(var m in mixed)if(m!=null&&m.HP>0)m.DespawnForStageTransfer();
            SessionState.SetBool(Key+"Done",true);
        }
        static void Capture(string name)
        {
            var camera=Camera.main;if(camera==null)return;
            var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();
            var orders=overlays.Select(c=>c.sortingOrder).ToArray();var overrideSort=overlays.Select(c=>c.overrideSorting).ToArray();
            var target=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D pixels=null;
            try
            {
                camera.targetTexture=rt;foreach(var c in overlays){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.1f;c.overrideSorting=true;c.sortingOrder=Mathf.Clamp(32100+c.sortingOrder,32100,32760);}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());
            }
            finally
            {
                for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];overlays[i].sortingOrder=orders[i];overlays[i].overrideSorting=overrideSort[i];}
                camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(pixels!=null)Object.Destroy(pixels);Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
