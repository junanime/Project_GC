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
    public static class LateMushroomSmoke
    {
        const string Key="LateMushroomSmoke", Output="D:/Project_GC/work/late-mushroom-proof";
        static bool started; static double deadline;
        static LateMushroomSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/checks.txt","");File.WriteAllText(Output+"/errors.txt","");
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+420;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",text+"\n"+trace+"\n");SessionState.SetBool(Key+"Failed",true);}}
        static void Check(bool ok,string text){if(!ok)throw new Exception("[Mushrooms] "+text);File.AppendAllText(Output+"/checks.txt",text+"\n");}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log("[Mushrooms] Finished failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(started||!EditorApplication.isPlaying||level==null||level.PlayerCharacter==null||level.GetComponent<FieldObjectLayout>()?.Ready!=true||StageEntryLoading.IsLoading)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static void Set(object o,string name,object value)
        {
            for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null){f.SetValue(o,value);return;}}
            throw new MissingFieldException(name);
        }
        static void Capture(string name)=>typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"late-mushroom-proof/"+name+".png"});
        static IEnumerator Checks(LevelManager level)
        {
            Application.runInBackground=true;var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            Time.timeScale=1;level.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner){b.enabled=false;b.StopAllCoroutines();}
            var entity=level.EntityManager;entity.enabled=false;entity.StopAllCoroutines();var player=level.PlayerCharacter;
            Set(player,"isInvincible",true);player.CollectableCollider.enabled=false;
            var manager=Object.FindObjectOfType<AbilityManager>();foreach(var a in manager.GetComponentsInChildren<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var m in entity.LivingMonsters.ToArray())m.DespawnForStageTransfer();
            var bp=level.CurrentLevelBlueprint;var flat=bp.monsters.SelectMany(p=>p.monsterBlueprints).ToArray();
            int king=Array.FindIndex(flat,b=>b.name=="새송이 대장"),red=Array.FindIndex(flat,b=>b.name=="붉은갓 대장");
            Check(king==31&&red==32,"new entries appended; all 31 existing flat indices retained");
            var table=bp.monsterSpawnTable;
            for(int second=0;second<=1200;second++)
            {
                float t=second/900f;
                if(second<=840)Check(table.GetProbability(t,king)==0,"king locked through "+second+"s");
                if(second<=870)Check(table.GetProbability(t,red)==0,"red locked through "+second+"s");
                float total=Enumerable.Range(0,flat.Length).Sum(i=>table.GetProbability(t,i));
                Check(Mathf.Abs(total-1)<.0001f,"normalized table "+second+"s");
            }
            float[] times={840,855,870,885,900};float[] kings={0,.3f,.4f,.35f,.35f},reds={0,0,0,.35f,.45f};
            var rows=new List<string>{"seconds,king,red,old,total_per_second"};
            for(int i=0;i<times.Length;i++)
            {
                float t=times[i]/900;
                Check(Mathf.Abs(table.GetProbability(t,king)-kings[i])<.0001f&&Mathf.Abs(table.GetProbability(t,red)-reds[i])<.0001f,"late mix "+times[i]);
                rows.Add($"{times[i]},{kings[i]},{reds[i]},{1-kings[i]-reds[i]},{table.GetSpawnRate(t)*1.3f}");
            }
            File.WriteAllLines(Output+"/spawn-table.csv",rows);
            Set(level,"levelTime",850f);
            var dart=SyringeAbilityResolver.FindOwnedOrFirst(manager);int shotPool=entity.AddPoolForProjectile(dart.ProjectilePrefab);
            var runtime=new SyringeSpecialRuntime{pierceEnabled=true,pierceCount=5,reflectCount=3,poisonEnabled=true,poisonDuration=5,poisonTickInterval=1,poisonTickDamage=1};
            var targets=new List<Monster>();
            foreach(int index in new[]{king,red})
            {
                int pool=Array.FindIndex(bp.monsters,p=>p.monsterBlueprints.Contains(flat[index]));
                var m=entity.SpawnMonster(pool,(Vector2)player.transform.position+new Vector2(index==king?-1:1,1),flat[index]);
                Check(m!=null&&m.GetType()==typeof(MeleeMonster)&&!RunScore.IsSpecial(m),flat[index].name+" ordinary melee and score path");
                m.GetComponent<MonsterNeedleBarrier>()?.ResetBarrier();
                Check(Mathf.Approximately(m.HP,flat[index].hp),m.Blueprint.name+" health");
                var start=m.transform.position;var frames=new HashSet<Sprite>();float until=Time.time+1.2f;
                while(Time.time<until){frames.Add(m.BodyRenderer.sprite);yield return null;}
                Check(frames.Count==8,m.Blueprint.name+" all eight approved walk frames run");
                Check(Vector2.Distance(start,m.transform.position)>.1f,m.Blueprint.name+" moves toward player");
                m.enabled=false;m.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
                m.transform.position=player.transform.position+new Vector3(index==king?-1:1,1,0);Physics2D.SyncTransforms();
                var visible=PromptSpriteGeometry.WorldBounds(m.BodyRenderer);
                Check(visible.size.y>.49f&&visible.size.y<.65f,m.Blueprint.name+" consistent late-normal body size");
                Check(m.BodyHitbox.bounds.size.y<.65f&&m.BodyHitbox.bounds.size.y>.45f,m.Blueprint.name+" alpha-fitted hitbox");
                var barrier=m.GetComponent<MonsterNeedleBarrier>()??m.gameObject.AddComponent<MonsterNeedleBarrier>();
                Check(MonsterNeedleBarrier.Eligible(m)&&MonsterNeedleBarrier.ShouldSpawn(m,850,.099f)&&!MonsterNeedleBarrier.ShouldSpawn(m,850,.1f),m.Blueprint.name+" 10 percent membrane rule");
                barrier.Arm();float hp=m.HP;
                for(int hit=1;hit<=4;hit++)
                {
                    var p=(SyringeProjectile)entity.SpawnProjectile(shotPool,(Vector2)m.transform.position+Vector2.left,10,0,3,~0);
                    p.ConfigureSpecials(runtime);Set(p,"useLaunchForwardOffset",false);p.Launch(Vector2.right);p.StopAllCoroutines();
                    typeof(SyringeProjectile).GetMethod("OnTriggerEnter2D",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{barrier.IsProtected?barrier.Surface:m.BodyHitbox});
                    if(hit<=3)Check(m.HP==hp&&barrier.Hits==hit,m.Blueprint.name+" shield absorbs real needle "+hit);
                    else Check(m.HP<hp&&m.GetComponent<PoisonStatus>().ActiveStatusTags!=CombatStatusTag.None,m.Blueprint.name+" fourth needle deals damage and poison");
                    p.ReleaseForPlayerExit(player);
                }
                m.gameObject.AddComponent<CorrosionStatus>().Apply(5,.15f,.1f,3,false);
                m.gameObject.AddComponent<HoneySlowStatus>().Apply(3,.5f);
                m.gameObject.AddComponent<GutBacteriaStatus>().Apply(5,3,5,1,GemType.Green10,.1f,false);
                m.gameObject.AddComponent<NeuralBlockedMonsterStatus>().ApplyIce(3);
                m.gameObject.AddComponent<BurnStatus>().Apply(5,1,1,player);
                Check(m.GetComponent<CorrosionStatus>().ActiveStatusTags!=CombatStatusTag.None&&m.GetComponent<GutBacteriaStatus>().ActiveStatusTags!=CombatStatusTag.None,m.Blueprint.name+" corrosion/infection active");
                targets.Add(m);
            }
            yield return new WaitForSeconds(.5f);Capture("status-effects");
            foreach(var m in targets)m.DespawnForStageTransfer();yield return null;
            for(int i=0;i<2;i++)
            {
                var m=entity.SpawnMonster(bp.monsters.Length-1,player.transform.position+new Vector3(i==0?-1:1,1,0),flat[i==0?king:red]);
                m.GetComponent<MonsterNeedleBarrier>()?.ResetBarrier();
                var remaining=m.GetComponents<MonoBehaviour>().OfType<ICombatStatus>().Where(s=>s.ActiveStatusTags!=CombatStatusTag.None).Select(s=>s.GetType().Name).ToArray();
                Check(remaining.Length==0,m.Blueprint.name+" pooled status cleanup "+string.Join(",",remaining));
                m.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.None;m.GetComponent<Rigidbody2D>().freezeRotation=true;m.enabled=true;
            }
            yield return null;Capture("walk-lineup");
            // Stage two constructs a separate ordinary table despite retaining legacy pool indices.
            var colon=ColonMonsterRoster.Create(level);
            for(int second=0;second<=1200;second+=10)
                Check(colon.monsterSpawnTable.GetProbability(second/900f,king)==0&&colon.monsterSpawnTable.GetProbability(second/900f,red)==0,"stage two excludes late mushrooms "+second);
            Check(ColonMonsterRoster.Active.Normals.Count==8,"stage two retains eight ordinary species");
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
