using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class MonsterNeedleBarrierSmoke
    {
        const string Key = "MonsterNeedleBarrierSmoke";
        static bool started;
        static int checks;
        static double deadline;
        static MonsterNeedleBarrierSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            Vampire.Editor.NeedleBarrierGeometryBaker.Bake();
            SessionState.SetBool(Key, true); SessionState.SetBool(Key+"Done", false); SessionState.SetBool(Key+"Failed", false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position = new Rect(0,0,1280,741);
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false; deadline=EditorApplication.timeSinceStartup+300;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log; Application.logMessageReceived+=Log;
        }
        static void Log(string message,string trace,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true); }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log("[Barrier] FINISHED checks="+checks+" failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(started||!EditorApplication.isPlaying||level==null||level.PlayerCharacter==null||level.CurrentLevelTime<.2f)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static FieldInfo Field(object o,string name)
        {
            for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}
            throw new MissingFieldException(name);
        }
        static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
        static void Check(bool valid,string name){if(!valid)throw new Exception("[Barrier] FAIL "+name);checks++;Debug.Log("[Barrier] PASS "+name);}
        static void Capture(string name)=>typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"barrier-"+name+".png"});
        static void Hit(SyringeProjectile shot,Collider2D target)=>typeof(SyringeProjectile).GetMethod("OnTriggerEnter2D",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shot,new object[]{target});
        static IEnumerator Checks(LevelManager level)
        {
            Application.runInBackground=true;
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            Time.timeScale=1;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())
                if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner){b.enabled=false;b.StopAllCoroutines();}
            level.enabled=false;
            var entity=level.EntityManager;entity.enabled=false;entity.StopAllCoroutines();
            var player=level.PlayerCharacter;Set(player,"isInvincible",true);player.CollectableCollider.enabled=false;
            var manager=Object.FindObjectOfType<AbilityManager>();
            foreach(var ability in manager.GetComponentsInChildren<Ability>()){ability.enabled=false;ability.StopAllCoroutines();}
            foreach(var m in entity.LivingMonsters.ToArray())m.gameObject.SetActive(false);
            var dart=SyringeAbilityResolver.FindOwnedOrFirst(manager);
            int projectilePool=entity.AddPoolForProjectile(dart.ProjectilePrefab);
            var blueprint=level.CurrentLevelBlueprint;
            var roster=new List<Monster>();
            int chosenPool=-1;MonsterBlueprint chosenBlueprint=null;
            foreach(var entry in blueprint.MonsterIndexMap)
            {
                if(blueprint.monsterSpawnTable.GetProbability(630,entry.Key)<=0)continue;
                var pair=entry.Value;var bp=blueprint.monsters[pair.Item1].monsterBlueprints[pair.Item2];
                var m=entity.SpawnMonster(pair.Item1,(Vector2)player.transform.position+Vector2.right*2,bp,0,false);
                if(m==null)continue;
                if(!MonsterNeedleBarrier.Eligible(m)){Check(!MonsterNeedleBarrier.ShouldSpawn(m,900,0),bp.name+" special enemy excluded");m.gameObject.SetActive(false);continue;}
                if(chosenPool<0){chosenPool=pair.Item1;chosenBlueprint=bp;}
                m.enabled=false;m.StopAllCoroutines();m.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
                Set(m,"currentHealth",10000f);
                Check(!MonsterNeedleBarrier.ShouldSpawn(m,629.99f,0),bp.name+" absent before 10:30");
                Check(Enumerable.Range(0,1000).Count(i=>MonsterNeedleBarrier.ShouldSpawn(m,630,i/1000f))==100,bp.name+" current-pool 10 percent chance");
                var b=m.gameObject.GetComponent<MonsterNeedleBarrier>()??m.gameObject.AddComponent<MonsterNeedleBarrier>();b.Arm();
                Check(b.IsProtected&&b.Surface.enabled,bp.name+" fits visible body and arms");
                roster.Add(m);
            }
            Check(roster.Count>0,"ordinary current spawn roster present: "+roster.Count);
            Set(roster[0],"miniStageOwned",true);Check(!MonsterNeedleBarrier.ShouldSpawn(roster[0],900,0),"mini-stage ownership excluded");Set(roster[0],"miniStageOwned",false);
            Check(!MonsterNeedleBarrier.Eligible(blueprint.finalBoss.bossPrefab.GetComponent<Monster>()),"boss prefab excluded");
            for(int i=0;i<roster.Count;i++)roster[i].transform.position=player.transform.position+new Vector3((i%3-1f)*1.8f,.7f+i/3*1.2f,0);
            yield return null;yield return null;Physics2D.SyncTransforms();Capture("roster");
            var target=roster[0];foreach(var m in roster.Skip(1))m.gameObject.SetActive(false);
            target.transform.position=player.transform.position+Vector3.right*1.7f;
            var barrier=target.GetComponent<MonsterNeedleBarrier>();barrier.Arm();
            foreach(var p in Object.FindObjectsOfType<Projectile>())p.ReleaseForPlayerExit(player);
            yield return null;Physics2D.SyncTransforms();
            Capture("intact");
            float hp=target.HP;
            target.TakeDamage(100);target.TakeItemPureDamage(100);
            Check(target.HP==hp,"all direct and pure health damage blocked");
            target.gameObject.AddComponent<PoisonStatus>().Apply(5,1,10);
            target.gameObject.AddComponent<HoneySlowStatus>().Apply(5,.1f);
            target.gameObject.AddComponent<CorrosionStatus>().Apply(5,.2f,.1f,3,false);
            target.gameObject.AddComponent<NeedleMarkStatus>().Apply(5);
            target.gameObject.AddComponent<NeuralBlockedMonsterStatus>().ApplyIce(5);
            Check(target.GetComponents<MonoBehaviour>().OfType<ICombatStatus>().All(s=>s.ActiveStatusTags==CombatStatusTag.None),"direct status entry points reject protected monster");
            // Real pooled projectile and real physics contact, carrying all three continuation behaviours.
            var runtime=new SyringeSpecialRuntime{pierceEnabled=true,pierceCount=5,reflectCount=3,poisonEnabled=true,poisonDuration=5,poisonTickInterval=1,poisonTickDamage=10};
            SyringeProjectile Fire(bool actualPhysics=false)
            {
                Vector2 center=barrier.VisualRoot.position;
                var p=(SyringeProjectile)entity.SpawnProjectile(projectilePool,center-Vector2.right*(barrier.Radii.x+.9f),10,0,3,~0);
                p.ConfigureSpecials(runtime);Set(p,"useLaunchForwardOffset",false);p.Launch(Vector2.right);
                if(!actualPhysics){p.StopAllCoroutines();Hit(p,barrier.IsProtected?barrier.Surface:target.BodyHitbox);}
                return p;
            }
            var first=Fire(true);float timeout=Time.time+2;
            while(barrier.Hits==0&&Time.time<timeout)yield return null;
            Capture("one-needle");
            Check(barrier.Hits==1&&barrier.AttachedNeedles==1&&!first.IsFlying,"physics surface intercepts first piercing/reflection needle: hits="+barrier.Hits+" flying="+first.IsFlying+" shot="+first.transform.position+" surface="+barrier.Surface.bounds);
            Hit(first,target.BodyHitbox);Check(barrier.Hits==1,"same needle duplicate body collision cannot count twice");
            Check(target.HP==hp&&target.GetComponent<PoisonStatus>().ActiveStatusTags==CombatStatusTag.None,"first hit has no health or poison side effect");
            yield return new WaitForSeconds(3);
            Check(barrier.AttachedNeedles==1&&target.GetComponentsInChildren<SpriteRenderer>().Count(r=>r.name=="Needle pinned to membrane")==1,"pinned needle persists beyond old 2.5-second lifetime");
            runtime.returnNeedleEnabled=true;runtime.returnNeedleSpeedMultiplier=1;runtime.returnNeedleDamageMultiplier=1;runtime.returnNeedleMaxDuration=3;runtime.returnNeedleArriveDistance=.1f;
            var second=Fire();Check(barrier.Hits==2&&barrier.AttachedNeedles==2&&!second.IsFlying,"second needle stays and is consumed");
            target.transform.position+=Vector3.up*.3f;target.BodyRenderer.flipX=!target.BodyRenderer.flipX;
            yield return null;Capture("two-needles");
            var pins=target.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="Needle pinned to membrane").ToArray();
            Check(pins.All(p=>p.enabled&&p.sprite!=null)&&Vector3.Distance(pins[0].transform.position,pins[1].transform.position)>.01f,"two pins follow moving flipped monster without overlapping");
            foreach(var pin in pins)
            {
                var tip=VisibleBodyGeometry.Outline(pin.sprite).Select(v=>pin.transform.TransformPoint(v)).OrderByDescending(p=>p.x).First();
                var offset=(Vector2)(tip-barrier.VisualRoot.position);
                float normalized=new Vector2(offset.x/barrier.Radii.x,offset.y/barrier.Radii.y).magnitude;
                Check(Mathf.Abs(normalized-1)<.1f,"visible needle tip touches membrane, radius="+normalized);
            }
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.2f);
            Check(barrier.Hits==2&&barrier.IsProtected,"pause does not expire shield or pins");Time.timeScale=1;
            var third=Fire();
            Check(barrier.Hits==3&&barrier.AttachedNeedles==3&&!barrier.IsProtected&&!barrier.Surface.enabled,"third needle pins before bubble burst and removes protection");
            Check(target.HP==hp&&target.GetComponent<PoisonStatus>().ActiveStatusTags==CombatStatusTag.None,"third hit still has no health or status damage");
            Capture("third-pin");
            Time.captureDeltaTime=1f/30;
            for(int i=0;i<18;i++){Capture("pop-"+i.ToString("D2"));yield return null;}
            Time.captureDeltaTime=0;
            Check(!barrier.VisualRoot.gameObject.activeSelf&&barrier.AttachedNeedles==0,"pop releases membrane and all pinned needles");
            runtime.returnNeedleEnabled=false;
            Fire();Check(target.HP<hp&&target.GetComponent<PoisonStatus>().ActiveStatusTags!=CombatStatusTag.None,"next needle restores health damage and status effects");
            // Pool reuse and production spawn hook, without bypassing EntityManager.
            target.gameObject.SetActive(false);yield return null;
            Check(barrier.Hits==0&&!barrier.IsProtected&&barrier.AttachedNeedles==0,"disable clears pooled protection and pins");
            Set(level,"levelTime",629f);int early=0;
            for(int i=0;i<20;i++){var m=entity.SpawnMonster(chosenPool,player.transform.position,chosenBlueprint);if(MonsterNeedleBarrier.Blocks(m))early++;m.DespawnForStageTransfer();}
            Check(early==0,"production spawn hook gives no early shields");
            Set(level,"levelTime",630f);int protectedCount=0;var randomState=UnityEngine.Random.state;UnityEngine.Random.InitState(10423);
            for(int i=0;i<400;i++){var m=entity.SpawnMonster(chosenPool,player.transform.position,chosenBlueprint);if(MonsterNeedleBarrier.Blocks(m))protectedCount++;m.DespawnForStageTransfer();}
            UnityEngine.Random.state=randomState;
            Check(protectedCount>=20&&protectedCount<=65,"production spawn chance approximately 10 percent: "+protectedCount+"/400");
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
