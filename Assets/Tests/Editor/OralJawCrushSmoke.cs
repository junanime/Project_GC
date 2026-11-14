#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    /// <summary>One real character session per process; run -oralCharacter=0..3 for the full matrix.</summary>
    [InitializeOnLoad]
    public static class OralJawCrushSmoke
    {
        const string Key="OralJawCrushSmoke", Root="Library/OralJawCrushProof";
        static readonly string[] Names={"ashi","ari","hyuki","shini"};
        static readonly string[] Blueprints={"Main","Ari","Hyuki","Shini"};
        static int Index=>SessionState.GetInt(Key+"Index",0);
        static string Output=>Root+"/"+Index+"-"+Names[Index];
        static bool started; static double deadline; static float shotSize=2.8f;
        [Serializable] sealed class Pref { public string key; public bool had; public int value; }
        [Serializable] sealed class Backup { public Pref[] prefs; }
        static OralJawCrushSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SeedActualCharacterSelection()
        {
            if(!SessionState.GetBool(Key,false))return;
            // Editor domain reload clears CrossSceneData. Restore the same selection
            // before Character.Awake, exactly where a normal scene transfer uses it.
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/"+Blueprints[Index]+" Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
        }
        public static void Run()
        {
            Directory.CreateDirectory(Root);Restore();
            File.WriteAllText(Root+"/result.txt","RUNNING");File.WriteAllText(Root+"/checks.txt","");File.WriteAllText(Root+"/errors.txt","");
            var keys=new[]{"Coins","LobbySilverCoins"};
            File.WriteAllText(Root+"/prefs.json",JsonUtility.ToJson(new Backup{prefs=keys.Select(k=>new Pref{key=k,had=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            File.WriteAllText(Root+"/capture-method.txt","Actual Unity play-mode camera renders; four separate gameplay sessions using each actual CharacterBlueprint. Fixed 20 fps simulation during recorded sequences only. Events and actor positioning are controlled for reproducible QA. No sprite editing or interpolated frames.\n");
            string selected=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-oralCharacter="));
            int selectedIndex=selected!=null?int.Parse(selected.Substring("-oralCharacter=".Length),CultureInfo.InvariantCulture):0;
            if(selectedIndex<0||selectedIndex>=Names.Length)throw new ArgumentOutOfRangeException("oralCharacter");
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Failed",false);SessionState.SetInt(Key+"Index",selectedIndex);SessionState.SetInt(Key+"Checks",0);BeginSession();
        }
        static void BeginSession()
        {
            SessionState.SetBool(Key+"Done",false);started=false;Directory.CreateDirectory(Output);
            CrossSceneData.ClearPendingRunSceneTransfer();
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/"+Blueprints[Index]+" Character Blueprint.asset");
            if(CrossSceneData.CharacterBlueprint==null)throw new Exception("Missing actual character blueprint "+Names[Index]);
            CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+900;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            File.AppendAllText(Root+"/errors.txt",Names[Index]+": "+text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);
        }
        static void Restore()
        {
            var file=Root+"/prefs.json";if(!File.Exists(file))return;
            foreach(var p in JsonUtility.FromJson<Backup>(File.ReadAllText(file)).prefs)if(p.had)PlayerPrefs.SetInt(p.key,p.value);else PlayerPrefs.DeleteKey(p.key);
            PlayerPrefs.Save();File.Delete(file);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Root+"/errors.txt","Deadline\n");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Restore();Time.timeScale=1;Time.captureFramerate=0;SessionState.SetBool(Key,false);
                bool failed=SessionState.GetBool(Key+"Failed",false);
                string result=(failed?"FAIL":"PASS")+" checks="+SessionState.GetInt(Key+"Checks",0)+" character="+Names[Index]+" characterSessions=1";
                File.WriteAllText(Root+"/result.txt",result);File.WriteAllText(Output+"/result.txt",result);
                File.Copy(Root+"/checks.txt",Output+"/checks.txt",true);File.Copy(Root+"/errors.txt",Output+"/errors.txt",true);
                EditorApplication.Exit(failed?1:0);return;
            }
            if(!EditorApplication.isPlaying)return;
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var level=Object.FindObjectOfType<LevelManager>();
            if(started||level==null||!level.InitialFieldObjectsReady||level.PlayerCharacter==null)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static void Check(bool condition,string message)
        {
            if(!condition)throw new Exception("ORAL_JAW_CRUSH_FAIL "+Names[Index]+": "+message);
            SessionState.SetInt(Key+"Checks",SessionState.GetInt(Key+"Checks",0)+1);
            File.AppendAllText(Root+"/checks.txt","PASS "+Names[Index]+": "+message+"\n");Debug.Log("ORAL_JAW_CRUSH_PASS "+Names[Index]+": "+message);
        }
        static FieldInfo Field(object owner,string name)
        {
            for(var type=owner.GetType();type!=null;type=type.BaseType){var f=type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}
            throw new MissingFieldException(owner.GetType().Name,name);
        }
        static void Set(object owner,string name,object value)=>Field(owner,name).SetValue(owner,value);
        static void Silence(LevelManager level)
        {
            level.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn){b.enabled=false;b.StopAllCoroutines();}
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
        }
        static void Clear(LevelManager level)
        {
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())if(m!=null)m.DespawnForStageTransfer();
            foreach(var p in Object.FindObjectsOfType<Projectile>())p.RemoveForItem();
            foreach(var c in Object.FindObjectsOfType<Collectable>())c.DissolveInAcidLake();
        }
        static void Place(Character player,Vector2 p)
        {
            var runtime=OralStageRuntime.Instance;runtime.TeleportPlayer(p,OralMapGeometry.ZoneAt(runtime.Map,p)==OralZone.Teeth?OralZone.Teeth:OralZone.Tongue);player.Move(Vector2.zero);Physics2D.SyncTransforms();
        }
        static IEnumerator Checks(LevelManager level)
        {
            var preferences=GamePreferences.Current.Copy();preferences.pauseOnFocusLoss=false;preferences.muteOnFocusLoss=false;GamePreferences.Apply(preferences,false);
            Application.runInBackground=true;Time.timeScale=1;RunScore.Exclude("Oral jaw crush integration QA");
            Silence(level);level.PlayerCharacter.CollectableCollider.enabled=false;
            Check(level.PlayerCharacter.Blueprint==AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/"+Blueprints[Index]+" Character Blueprint.asset"),"actual selected blueprint initialized in fresh gameplay scene");
            Check(StageProgression.Ensure(level).BeginDirectOral(true),"stage zero entry begins");
            float until=Time.realtimeSinceStartup+120;
            while((OralStageRuntime.Instance==null||StageProgression.Ensure(level).Travelling)&&Time.realtimeSinceStartup<until)yield return null;
            Check(OralStageRuntime.Instance!=null&&OralStageRuntime.Instance.Ready,"oral runtime ready");
            Silence(level);Clear(level);var player=level.PlayerCharacter;var map=FiniteStomachMap.Instance;var events=OralFieldEvents.Instance;var visual=Object.FindObjectOfType<OralMapVisual>();
            events.AutoSchedule=false;foreach(var c in events.Cavities){c.Healed=true;c.Active=false;}
            var crown=OralMapGeometry.ToothWalkCenter(map,1);var gap=OralMapGeometry.ToothSafeGap(map,1);
            var body=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
            var originalScale=body.transform.localScale;var originalRotation=body.transform.localRotation;
            var originalCollider=player.GetComponentInChildren<BoxCollider2D>();var colliderSize=originalCollider.size;
            Check(JawCrushVisual.ActiveCount==0,"fresh gameplay has no lingering squash proxies");
            Place(player,crown);Set(player,"isInvincible",false);player.GainHealth(9999);float hp=player.CurrentHealth;
            Check(events.ForceEvent(OralEventKind.Jaw),"real jaw event starts for player flatten proof");
            bool warned=false,saw=false,paused=false;int frames=0;float maxCompression=0;Sprite before=body.sprite;
            yield return Record("01-warning-player-crush",()=>events.CurrentEvent!=OralEventKind.None||JawCrushVisual.ForActor(player)!=null,()=>
            {
                if(!warned&&events.Elapsed>.55f&&events.Elapsed<1.8f){WarningChecks(visual,map);shotSize=5;Frame("warning");shotSize=2.8f;warned=true;}
                var crush=JawCrushVisual.ForActor(player);
                if(crush!=null){maxCompression=Mathf.Max(maxCompression,crush.Compression);saw=true;CheckProxyOnce(crush,body,ref frames);if(crush.Compression>.8f&&!paused){Frame("player-flattened");paused=true;}}
            });
            Check(warned&&saw&&maxCompression>.75f,"warning then visible cartoon flatten occur on actual player hit");
            Check(player.CurrentHealth<hp&&player.IsAlive,"actual jaw damage is nonfatal");
            Check(!body.forceRenderingOff&&body.enabled,"living original renderer restored after squash");
            Check((body.transform.localScale-originalScale).sqrMagnitude<.000001f&&Quaternion.Angle(body.transform.localRotation,originalRotation)<.001f,"squash leaves original transform unchanged");
            Check(originalCollider.size==colliderSize,"visual squash does not change hitbox dimensions");
            Check(visual.JawWarningFills.All(r=>!r.enabled)&&visual.JawWarningLines.All(r=>!r.enabled),"all warning fill and outline disappear on release");
            yield return AnimationChecks(player,body,map);
            yield return ImmunityChecks(level,player,events,map,gap,crown);
            if(Index==0){yield return MonsterChecks(level,events,map);yield return PauseAndCancelChecks(level,player,events,map);}
            // A lethal actual jaw hit must still render squash even though normal death stops scaled time.
            Clear(level);Place(player,crown);player.GainHealth(9999);Set(player,"currentHealth",1f);Set(player,"isInvincible",false);
            ((PointBar)Field(player,"healthBar").GetValue(player)).Setup(1,0,player.MaxHealth);
            bool deathCallback=false;player.OnDeath.AddListener(()=>deathCallback=true);
            Check(events.ForceEvent(OralEventKind.Jaw),"lethal player jaw trial begins");
            bool deathCrush=false;float deathUntil=Time.realtimeSinceStartup+10;
            while(player.IsAlive&&Time.realtimeSinceStartup<deathUntil)yield return null;
            Check(!player.IsAlive,"real jaw damage causes player death");
            var death=JawCrushVisual.ForActor(player);deathCrush=death!=null;
            if(death!=null){yield return new WaitForSecondsRealtime(.14f);Frame("player-lethal-flatten");}
            Check(deathCrush,"lethal player hit creates crush visual while death pipeline runs");
            Check(!Object.FindObjectsOfType<SpriteRenderer>().Any(r=>r.name=="Character run exit"&&r.enabled),"lethal crush never duplicates upright normal exit animation");
            float exitUntil=Time.realtimeSinceStartup+18;
            while((!deathCallback||JawCrushVisual.ForActor(player)!=null)&&Time.realtimeSinceStartup<exitUntil)yield return null;
            Check(deathCallback,"crush death invokes normal result callback after fade");
            Check(JawCrushVisual.ForActor(player)==null,"dead player proxy finishes without scaled-time leak");
            Check(!body.enabled||!body.forceRenderingOff,"finished corpse does not expose a duplicate normal player sprite");
            SessionState.SetBool(Key+"Done",true);
        }
        static void CheckProxyOnce(JawCrushVisual crush,SpriteRenderer body,ref int once)
        {
            if(once++>0)return;
            typeof(JawCrushVisual).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(crush,null);
            Check(crush.OriginalRenderer==body,"proxy references actual character renderer");
            Check(crush.ProxyRenderers.Count>0&&crush.ProxyRenderers.All(r=>r!=null),"proxy mesh renderers exist");
            Check(body.forceRenderingOff,"only original rendering is hidden during squash");
            Check(body.sprite!=null,"original animator retains its current sprite");
        }
        static IEnumerator AnimationChecks(Character player,SpriteRenderer body,FiniteStomachMap map)
        {
            Place(player,OralMapGeometry.TongueCenter(map));
            var frames=new HashSet<Sprite>();float until=Time.time+.7f;
            while(Time.time<until){player.Move(Vector2.right);frames.Add(body.sprite);yield return null;}
            player.Move(Vector2.zero);Check(frames.Count>1,"existing walk sprite animation resumes after squash");
            Check(!body.forceRenderingOff,"walking renderer remains visible");
            Check(player.TryDash(),"existing dash remains usable after squash");yield return new WaitForSeconds(.7f);player.Move(Vector2.zero);
            Check(!body.forceRenderingOff&&body.sprite!=null,"dash ends with visible valid original sprite");
        }
        static IEnumerator ImmunityChecks(LevelManager level,Character player,OralFieldEvents events,FiniteStomachMap map,Vector2 gap,Vector2 crown)
        {
            foreach(bool invincible in new[]{false,true})
            {
                Place(player,invincible?crown:gap);player.GainHealth(9999);Set(player,"isInvincible",invincible);float hp=player.CurrentHealth;bool seen=false;
                Check(events.ForceEvent(OralEventKind.Jaw),"jaw starts for "+(invincible?"invulnerable player":"safe gap"));
                while(events.CurrentEvent!=OralEventKind.None){seen|=JawCrushVisual.ForActor(player)!=null;yield return null;}
                Check(Mathf.Approximately(player.CurrentHealth,hp)&&!seen,(invincible?"invulnerable player":"safe gap")+" receives neither damage nor squash");
            }
            Set(player,"isInvincible",false);
            var inward=(OralMapGeometry.TongueCenter(map)-crown).normalized;Vector2 jumpFrom=crown;bool found=false;
            for(float distance=0;distance<8;distance+=.1f){var p=crown+inward*distance;if(OralMapGeometry.IsJawDanger(map,p)&&OralMapGeometry.TryJump(map,p,inward,out _)){jumpFrom=p;found=true;break;}}
            Check(found,"warned crown has a valid inward edge jump departure");
            Place(player,jumpFrom);player.GainHealth(9999);float airborneHP=player.CurrentHealth;bool airSquash=false;
            Check(events.ForceEvent(OralEventKind.Jaw),"airborne immunity trial begins");
            while(events.Elapsed<2.12f)yield return null;
            player.Move(Vector2.zero);Set(player,"lookDirection",inward);Set(player,"currentDashCharges",1);
            Check(player.TryDash()&&OralStageRuntime.Instance.IsAirborne,"real dash jump starts before jaw impact");
            while(events.CurrentEvent!=OralEventKind.None){airSquash|=JawCrushVisual.ForActor(player)!=null;yield return null;}
            player.Move(Vector2.zero);
            Check(Mathf.Approximately(player.CurrentHealth,airborneHP)&&!airSquash,"airborne player receives no damage or squash");
        }
        static void WarningChecks(OralMapVisual visual,FiniteStomachMap map)
        {
            Check(visual.JawWarningFillCount==14&&visual.JawWarningCount==14,"fourteen per-tooth fill and outline warnings visible");
            Check(visual.JawWarningFillOpacity>=.219f&&visual.JawWarningFillOpacity<=.321f,"warning fill remains translucent");
            Check(visual.JawWarningFillMaterial.GetFloat("_Mode")==9,"color hatch shader selected");
            Check(visual.JawWarningFillMaterial.GetFloat("_StripeWidth")>.1f,"hatch has visible world-scale spacing");
            for(int i=0;i<14;i++)
            {
                var polygon=OralMapGeometry.ToothDamagePolygon(map,i);var mesh=visual.JawWarningFills[i].GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
                Check(vertices.Length==polygon.Length&&vertices.Select((v,j)=>Vector2.Distance(v,polygon[j])<.001f).All(b=>b),"fill uses exact damage polygon "+i);
                float area=0;for(int t=0;t<triangles.Length;t+=3)area+=Mathf.Abs(Cross(vertices[triangles[t+1]]-vertices[triangles[t]],vertices[triangles[t+2]]-vertices[triangles[t]]))*.5f;
                float expected=0;for(int j=0;j<polygon.Length;j++)expected+=Cross(polygon[j],polygon[(j+1)%polygon.Length]);expected=Mathf.Abs(expected)*.5f;
                Check(Mathf.Abs(area-expected)<.001f,"triangle area exactly fills tooth without overlap "+i);
                for(int g=0;g<13;g++){var point=OralMapGeometry.ToothSafeGap(map,g);bool inside=false;for(int t=0;t<triangles.Length;t+=3)inside|=InTriangle(point,vertices[triangles[t]],vertices[triangles[t+1]],vertices[triangles[t+2]]);Check(!inside,"safe gap "+g+" excluded from fill "+i);}
            }
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static bool InTriangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c){float x=Cross(b-a,p-a),y=Cross(c-b,p-b),z=Cross(a-c,p-c);return(x>=0&&y>=0&&z>=0)||(x<=0&&y<=0&&z<=0);}
        sealed class MonsterProbe
        {
            public int Pool;public MonsterBlueprint Blueprint;public Monster Actor;public SpriteRenderer Body;public float Health;public bool SawProxy,SawFlatten,SawCorpse;public JawCrushVisual Proxy;public Vector3 Scale;
        }
        static MonsterProbe SpawnProbe(LevelManager level,int pool,MonsterBlueprint blueprint,Vector2 position)
        {
            var monster=level.EntityManager.SpawnMonster(pool,position,blueprint,0,false);
            Check(monster!=null,"actual monster pool spawns "+blueprint.name);
            monster.transform.position=position;monster.enabled=false;
            foreach(var collider in monster.GetComponentsInChildren<Collider2D>())collider.enabled=false;
            var rb=monster.GetComponent<Rigidbody2D>();if(rb!=null){rb.velocity=Vector2.zero;rb.bodyType=RigidbodyType2D.Kinematic;rb.position=position;}
            monster.GetComponent<MonsterNeedleBarrier>()?.ResetBarrier();Physics2D.SyncTransforms();
            Check(monster.BodyRenderer!=null&&monster.BodyRenderer.sprite!=null,"actual monster body sprite valid "+blueprint.name);
            return new MonsterProbe{Pool=pool,Blueprint=blueprint,Actor=monster,Body=monster.BodyRenderer,Health=monster.HP,Scale=monster.BodyRenderer.transform.localScale};
        }
        static IEnumerator MonsterChecks(LevelManager level,OralFieldEvents events,FiniteStomachMap map)
        {
            Clear(level);var player=level.PlayerCharacter;var probes=new List<MonsterProbe>();var seen=new HashSet<MonsterBlueprint>();
            int slot=0;
            for(int pool=0;pool<level.CurrentLevelBlueprint.monsters.Length;pool++)
            {
                var group=level.CurrentLevelBlueprint.monsters[pool];
                foreach(var bp in group.monsterBlueprints)
                {
                    if(bp==null||!seen.Add(bp)||bp is BossMonsterBlueprint||bp is MiniBossMonsterBlueprint||bp is EliteMonsterBlueprint)continue;
                    // One representative of every stage-one special blueprint, plus all six oral normals.
                    if(!OralMonsterRoster.IsBlueprint(bp)&&bp.GetType()==typeof(MeleeMonsterBlueprint))continue;
                    var p=OralMapGeometry.ToothWalkCenter(map,slot++%14);
                    probes.Add(SpawnProbe(level,pool,bp,p));
                }
            }
            Check(probes.Count(p=>OralMonsterRoster.IsBlueprint(p.Blueprint))==6,"all six stage-zero normal species included");
            Check(probes.Any(p=>p.Actor is SniperMonster),"stage-one sniper special included");
            Check(probes.Any(p=>p.Actor is ExplodingMonster),"stage-one exploding special included");
            var elitePosition=OralMapGeometry.ToothWalkCenter(map,1)+Vector2.left*.7f;
            var elite=level.EntityManager.SpawnOverchargeElite(elitePosition);
            Check(elite!=null&&elite.Blueprint is EliteMonsterBlueprint,"stage-zero elite created through normal elite spawn API");
            elite.transform.position=elitePosition;elite.enabled=false;
            foreach(var c in elite.GetComponentsInChildren<Collider2D>())c.enabled=false;
            var eliteRb=elite.GetComponent<Rigidbody2D>();if(eliteRb!=null){eliteRb.velocity=Vector2.zero;eliteRb.bodyType=RigidbodyType2D.Kinematic;eliteRb.position=elite.Position;}
            var eliteProbe=new MonsterProbe{Pool=-1,Blueprint=elite.Blueprint,Actor=elite,Body=elite.BodyRenderer,Health=elite.HP,Scale=elite.BodyRenderer.transform.localScale};probes.Add(eliteProbe);
            var template=probes.First(p=>OralMonsterRoster.IsBlueprint(p.Blueprint));
            var shielded=SpawnProbe(level,template.Pool,template.Blueprint,OralMapGeometry.ToothWalkCenter(map,12));
            (shielded.Actor.GetComponent<MonsterNeedleBarrier>()??shielded.Actor.gameObject.AddComponent<MonsterNeedleBarrier>()).Arm();
            var safe=SpawnProbe(level,template.Pool,template.Blueprint,OralMapGeometry.ToothSafeGap(map,11));
            Place(player,OralMapGeometry.ToothSafeGap(map,1));Set(player,"isInvincible",true);
            Check(events.ForceEvent(OralEventKind.Jaw),"actual jaw starts with all normal and special monster probes");
            bool shieldSquash=false,safeSquash=false,stored=false;shotSize=5.5f;
            yield return Record("02-monster-crush",()=>events.CurrentEvent!=OralEventKind.None||JawCrushVisual.ActiveCount>0,()=>
            {
                foreach(var probe in probes)
                {
                    var effect=JawCrushVisual.ForActor(probe.Actor);
                    if(effect==null)effect=probe.Proxy;
                    if(effect==null)continue;
                    probe.Proxy=effect;probe.SawProxy=true;probe.SawFlatten|=effect.Compression>.65f;probe.SawCorpse|=effect.IsCorpse;
                }
                shieldSquash|=JawCrushVisual.ForActor(shielded.Actor)!=null;safeSquash|=JawCrushVisual.ForActor(safe.Actor)!=null;
                if(!stored&&probes.Any(p=>p.SawFlatten)){Frame("monsters-flattened");stored=true;}
            });
            shotSize=2.8f;
            foreach(var probe in probes)
            {
                if(probe.Actor is TrapMonster)
                {
                    Check(probe.Actor.IsAlive&&Mathf.Approximately(probe.Actor.HP,probe.Health)&&!probe.SawProxy,"original dormant trap immunity retained without squash");
                    continue;
                }
                Check(probe.SawProxy&&probe.SawFlatten,"actual jaw visibly flattens "+probe.Blueprint.name);
                Check(probe.Body==null||!probe.Body.forceRenderingOff,"monster source render suppression released "+probe.Blueprint.name);
                if(probe==eliteProbe){Check(probe.Actor.IsAlive&&Mathf.Abs(probe.Actor.HP-(probe.Health-probe.Actor.SpawnMaxHealth*.12f))<.01f,"elite retains original twelve-percent jaw damage rule");Check(!probe.SawCorpse,"surviving elite uses recoverable squash");}
                else Check(!probe.Actor.IsAlive&&probe.SawCorpse,"normal or special lethal hit keeps detached corpse squash "+probe.Blueprint.name);
            }
            Check(shielded.Actor.IsAlive&&Mathf.Approximately(shielded.Actor.HP,shielded.Health)&&!shieldSquash,"shield-blocked monster receives no squash without damage");
            Check(safe.Actor.IsAlive&&Mathf.Approximately(safe.Actor.HP,safe.Health)&&!safeSquash,"monster in safe seam receives no damage or squash");
            // Reuse every involved ordinary pool after death, not a detached fabricated sprite.
            foreach(var probe in probes.Where(p=>p.Pool>=0))
            {
                var respawn=SpawnProbe(level,probe.Pool,probe.Blueprint,OralMapGeometry.TongueCenter(map));
                yield return null;
                var seaweed=respawn.Actor.GetComponent<SeaweedTrapVisual>();
                bool visible=seaweed!=null?new[]{seaweed.Rear,seaweed.Front}.Any(r=>r!=null&&r.enabled&&!r.forceRenderingOff):respawn.Body.enabled&&!respawn.Body.forceRenderingOff;
                Check(respawn.Actor.IsAlive&&visible,"pool respawn is alive and actual body layers are visible "+probe.Blueprint.name);
                Check(JawCrushVisual.ForActor(respawn.Actor)==null,"pool respawn has no previous crush binding "+probe.Blueprint.name);
                respawn.Actor.DespawnForStageTransfer();
            }
            var quick=SpawnProbe(level,template.Pool,template.Blueprint,OralMapGeometry.ToothWalkCenter(map,1));
            Check(JawCrushVisual.ApplyDamage(quick.Actor,quick.Health+1),"controlled lethal jaw damage starts pool-return regression");
            var detached=JawCrushVisual.ForActor(quick.Actor);Check(detached!=null&&detached.IsCorpse,"lethal pool target has corpse snapshot");
            quick.Actor.DespawnForStageTransfer();
            var reused=SpawnProbe(level,template.Pool,template.Blueprint,OralMapGeometry.TongueCenter(map));
            Check(reused.Actor==quick.Actor,"Unity monster pool immediately reuses exact same actor");
            yield return null;
            Check(detached!=null&&detached.IsPlaying&&detached.ProxyRenderers.Any(r=>r!=null&&r.enabled),"detached flattened corpse remains visible after immediate pool reuse");
            Check(reused.Actor.IsAlive&&reused.Body.enabled&&!reused.Body.forceRenderingOff,"reused actor remains visible while old corpse picture is still playing");
            Check(JawCrushVisual.ForActor(reused.Actor)==null,"old corpse no longer binds to reused living actor");
            yield return new WaitForSeconds(1.2f);
            Check(detached==null||!detached.IsPlaying,"detached corpse finishes and releases resources");
            Clear(level);yield return ActiveTrapLayerCheck(level,events,map,probes.First(p=>p.Actor is TrapMonster));Set(player,"isInvincible",false);
        }
        static IEnumerator ActiveTrapLayerCheck(LevelManager level,OralFieldEvents events,FiniteStomachMap map,MonsterProbe original)
        {
            var source=(TrapMonsterBlueprint)original.Blueprint;string before=JsonUtility.ToJson(source);
            var fixture=Object.Instantiate(source);fixture.name="QA active damageable trap fixture";fixture.hideFlags=HideFlags.DontSave;
            fixture.useArrowEscapeMiniGame=false;fixture.tickDamage=0;fixture.bindDuration=30;fixture.bindPlayerOnActivate=false;fixture.spawnNearPlayerForTest=false;
            var player=level.PlayerCharacter;var position=OralMapGeometry.ToothWalkCenter(map,1);Place(player,position);Set(player,"isInvincible",true);
            var probe=SpawnProbe(level,original.Pool,fixture,position);
            typeof(TrapMonster).GetMethod("ActivateTrap",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(probe.Actor,new object[]{player});
            yield return new WaitForSeconds(.2f);
            Check(((TrapMonster)probe.Actor).IsActive&&probe.Actor.HP>0,"isolated cloned trap fixture becomes damageable through real activation");
            fixture.useArrowEscapeMiniGame=true;float immuneHP=probe.Actor.HP;
            Check(!JawCrushVisual.ApplyDamage(probe.Actor,immuneHP+1)&&Mathf.Approximately(probe.Actor.HP,immuneHP)&&JawCrushVisual.ForActor(probe.Actor)==null,"active arrow-escape trap rule blocks damage and squash");
            fixture.useArrowEscapeMiniGame=false;
            var seaweed=probe.Actor.GetComponent<SeaweedTrapVisual>();Check(seaweed!=null&&seaweed.Rear.enabled&&seaweed.Front.enabled,"active trap has two real visible body layers");
            float hp=probe.Actor.HP;bool crushed=false,twoLayers=false;shotSize=2.8f;
            Check(events.ForceEvent(OralEventKind.Jaw),"jaw starts for active damageable multi-layer trap fixture");
            yield return Record("03-active-trap-crush",()=>events.CurrentEvent!=OralEventKind.None||JawCrushVisual.ActiveCount>0,()=>{
                var effect=JawCrushVisual.ForActor(probe.Actor);if(effect==null)return;
                twoLayers|=effect.ProxyRenderers.Count>=2;
                if(effect.Compression>.7f&&!crushed){crushed=true;Frame("active-trap-flattened");}
            });
            Check(probe.Actor.HP<hp&&crushed&&twoLayers,"actual lethal jaw damage flattens both active seaweed layers");
            probe.Actor.DespawnForStageTransfer();yield return null;
            Check(!seaweed.Rear.forceRenderingOff&&!seaweed.Front.forceRenderingOff,"active trap pool return releases both layer render flags");
            Check(JsonUtility.ToJson(source)==before,"original trap blueprint immunity and gameplay values remain unchanged");
            Object.Destroy(fixture);Clear(level);
        }
        static IEnumerator PauseAndCancelChecks(LevelManager level,Character player,OralFieldEvents events,FiniteStomachMap map)
        {
            Place(player,OralMapGeometry.ToothWalkCenter(map,1));player.GainHealth(9999);Set(player,"isInvincible",false);
            Check(events.ForceEvent(OralEventKind.Jaw),"pause and cancellation trial begins");
            float until=Time.realtimeSinceStartup+10;
            while(JawCrushVisual.ForActor(player)==null&&Time.realtimeSinceStartup<until)yield return null;
            var effect=JawCrushVisual.ForActor(player);Check(effect!=null,"actual hit starts squash before pause");
            yield return null;level.SetRunFlowPaused(true);float elapsed=effect.Elapsed;float compression=effect.Compression;
            yield return new WaitForSecondsRealtime(.35f);
            Check(effect!=null&&Mathf.Abs(effect.Elapsed-elapsed)<.001f&&Mathf.Abs(effect.Compression-compression)<.001f,"living squash animation freezes with gameplay pause");
            level.SetRunFlowPaused(false);var original=effect.OriginalRenderer;
            // Component disable is the same release path used by scene/actor teardown.
            effect.enabled=false;yield return null;
            Check(!original.forceRenderingOff,"disabling squash releases original render flag immediately");
            Check(effect.ProxyRenderers.All(r=>r==null||!r.enabled||!r.gameObject.activeInHierarchy),"disabled squash leaves no visible proxy");
            events.CancelAll();yield return null;
            Check(JawCrushVisual.ActiveCount==0,"event cancellation leaves no active crush state");
            events.Configure(level);events.AutoSchedule=false;foreach(var cavity in events.Cavities){cavity.Healed=true;cavity.Active=false;}
            Check(OralFieldEvents.Instance==events,"event director reconfigures after teardown test");
        }
        static IEnumerator Record(string name,Func<bool> active,Action step)
        {
            string dir=Output+"/"+name;Directory.CreateDirectory(dir);foreach(var old in Directory.GetFiles(dir,"*.png"))File.Delete(old);
            File.WriteAllText(dir+"/times.csv","frame,game_seconds\n");int oldRate=Time.captureFramerate;Time.captureFramerate=20;float start=Time.time,timeout=Time.realtimeSinceStartup+70;int frame=0;
            do{yield return null;step?.Invoke();Frame(name+"/"+frame.ToString("D4"));File.AppendAllText(dir+"/times.csv",frame+","+(Time.time-start).ToString("F4",CultureInfo.InvariantCulture)+"\n");frame++;}while(active()&&Time.realtimeSinceStartup<timeout);
            Time.captureFramerate=oldRate;Check(!active(),"recorded event and squash finish within timeout");
        }
        static void Frame(string name)
        {
            var camera=Camera.main;var player=OralStageRuntime.Instance?.Level.PlayerCharacter;if(camera==null||player==null)return;
            var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();var orders=overlays.Select(c=>c.sortingOrder).ToArray();var flags=overlays.Select(c=>c.overrideSorting).ToArray();
            var target=camera.targetTexture;var active=RenderTexture.active;var position=camera.transform.position;float zoom=camera.orthographicSize;var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D image=null;
            try{
                camera.transform.position=new Vector3(player.Position.x,player.Position.y,position.z);camera.orthographicSize=shotSize;
                // Coroutine captures run before Unity LateUpdate. Refresh the same real
                // presentation ownership that the normal rendering phase executes.
                foreach(var crush in Object.FindObjectsOfType<JawCrushVisual>())if(crush.enabled)typeof(JawCrushVisual).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(crush,null);
                foreach(var bars in Object.FindObjectsOfType<PlayerCombatBars>())typeof(PlayerCombatBars).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bars,null);
                camera.targetTexture=rt;foreach(var c in overlays){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.1f;c.overrideSorting=true;c.sortingOrder=Mathf.Clamp(32100+c.sortingOrder,32100,32760);}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
            }finally{
                for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];overlays[i].sortingOrder=orders[i];overlays[i].overrideSorting=flags[i];}
                camera.targetTexture=target;camera.transform.position=position;camera.orthographicSize=zoom;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)Object.Destroy(image);Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
