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
    public static class OralCeilingLemonSmoke
    {
        const string Key="OralCeilingLemonSmoke", Root="Library/OralCeilingLemonProof";
        static readonly string[] Names={"ashi","ari","hyuki","shini"};
        static readonly string[] Blueprints={"Main","Ari","Hyuki","Shini"};
        static int Index=>SessionState.GetInt(Key+"Index",0);
        static string Output=>Root+"/"+Index+"-"+Names[Index];
        static bool started; static double deadline; static float shotSize=5f;
        [Serializable] sealed class Pref { public string key; public bool had; public int value; }
        [Serializable] sealed class Backup { public Pref[] prefs; }
        static OralCeilingLemonSmoke(){if(SessionState.GetBool(Key,false))Attach();}
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
            File.WriteAllText(Root+"/capture-method.txt","Actual Unity play-mode camera renders; actual CharacterBlueprint selected by -oralCharacter (default Ashi). Fixed 15 fps simulation during recorded sequences only. Events and actor positioning are controlled for reproducible QA. No sprite editing or interpolated frames.\n");
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
            started=false;deadline=EditorApplication.timeSinceStartup+1800;
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
                bool failed=SessionState.GetBool(Key+"Failed",false);
                string result=(failed?"FAIL":"PASS")+" checks="+SessionState.GetInt(Key+"Checks",0)+" character="+Names[Index]+" characterSessions=1";
                File.WriteAllText(Root+"/result.txt",result);File.WriteAllText(Output+"/result.txt",result);
                File.Copy(Root+"/checks.txt",Output+"/checks.txt",true);File.Copy(Root+"/errors.txt",Output+"/errors.txt",true);
                Restore();Time.timeScale=1;Time.captureFramerate=0;SessionState.SetBool(Key,false);
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
            if(!condition)throw new Exception("ORAL_CEILING_LEMON_FAIL "+Names[Index]+": "+message);
            SessionState.SetInt(Key+"Checks",SessionState.GetInt(Key+"Checks",0)+1);
            File.AppendAllText(Root+"/checks.txt","PASS "+Names[Index]+": "+message+"\n");Debug.Log("ORAL_CEILING_LEMON_PASS "+Names[Index]+": "+message);
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

        static Vector2? captureFocus;
        static Transform closeupTarget;
        static IEnumerator Checks(LevelManager level)
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            Application.runInBackground=true;Time.timeScale=1;RunScore.Exclude("Ceiling C02 and lemon spring controlled animation QA");Silence(level);
            Check(StageProgression.Ensure(level).BeginDirectOral(true),"stage zero entry");
            while(OralStageRuntime.Instance==null||StageProgression.Ensure(level).Travelling)yield return null;
            Silence(level);Clear(level);
            var player=level.PlayerCharacter;var map=FiniteStomachMap.Instance;var runtime=OralStageRuntime.Instance;var events=OralFieldEvents.Instance;var taste=events.Tastes;
            events.AutoSchedule=false;foreach(var c in events.Cavities){c.Healed=true;c.Active=false;}
            var tongue=OralMapGeometry.TongueCenter(map);var originalLanding=tongue+new Vector2(-3,2);Place(player,originalLanding);Set(player,"isInvincible",true);
            if(Environment.GetCommandLineArgs().Contains("-oralLemonOnly")){yield return SpringChecks(level);SessionState.SetBool(Key+"Done",true);yield break;}
            var mini=Object.FindObjectOfType<MiniStageDirector>();Check(mini!=null,"mini-stage director is available");mini.OpenEntrancePortal(originalLanding+Vector2.left*1.2f);yield return null;
            var portal=Object.FindObjectsOfType<BloodClotMiniStagePortal>().FirstOrDefault();Check(portal!=null,"a real opened blood-clot portal exists before ceiling entry");
            var portalRenderers=portal.GetComponentsInChildren<Renderer>(true);var portalHidden=portalRenderers.Select(r=>r.forceRenderingOff).ToArray();
            var portalColliders=portal.GetComponentsInChildren<Collider2D>(true);var portalEnabled=portalColliders.Select(c=>c.enabled).ToArray();
            float normalCameraSize=Camera.main.orthographicSize;
            var group=level.CurrentLevelBlueprint.monsters.Select((g,i)=>new{g,i}).First(x=>x.g.monsterBlueprints.Any(b=>b!=null&&OralMonsterRoster.IsBlueprint(b)));
            var blueprint=group.g.monsterBlueprints.First(b=>b!=null&&OralMonsterRoster.IsBlueprint(b));
            var lower=level.EntityManager.SpawnMonster(group.i,tongue+Vector2.left*3,blueprint,0,false);
            var lowerRenderers=lower.GetComponentsInChildren<SpriteRenderer>(true);var lowerHidden=lowerRenderers.Select(r=>r.forceRenderingOff).ToArray();
            var layout=level.GetComponent<FieldObjectLayout>();
            var lowerBoxes=layout.SpawnedObjects.Where(p=>p!=null&&p.activeInHierarchy).SelectMany(p=>p.GetComponentsInChildren<Collider2D>(true)).Where(c=>c.GetComponentInParent<Monster>()==null).ToArray();
            var lowerEnabled=lowerBoxes.Select(c=>c.enabled).ToArray();
            var playerBody=player.GetComponentInChildren<SpriteAnimator>(true).GetComponent<SpriteRenderer>();bool originalFlip=playerBody.flipY;
            var hurtbox=player.GetComponentsInChildren<BoxCollider2D>(true).FirstOrDefault(c=>c.name=="Full Hitbox");
            Check(hurtbox!=null,"player has the production damage collider");
            Vector2 hurtboxOffset=hurtbox!=null?hurtbox.offset:default,hurtboxSize=hurtbox!=null?hurtbox.size:default;
            var arena=OralCeilingArenaGeometry.Center(map);
            Check(OralCeilingArenaGeometry.Contains(map,arena),"C02 center is walkable");
            Check(OralCeilingArenaGeometry.Contains(map,arena+Vector2.Scale(OralCeilingArenaGeometry.HalfExtents,new Vector2(.8f,.8f))),"C02 preserves rounded-square corners rather than a circle");
            Check(!OralCeilingArenaGeometry.Contains(map,arena+OralCeilingArenaGeometry.HalfExtents),"C02 excludes the outer square corner");
            for(int i=0;i<16;i++){
                Vector2 direction=new Vector2(Mathf.Cos(i*Mathf.PI/8),Mathf.Sin(i*Mathf.PI/8));
                var clamped=OralCeilingArenaGeometry.Clamp(map,arena+direction*40,.25f);
                Check(OralCeilingArenaGeometry.Contains(map,clamped,.24f),"C02 rounded-square clamp retains inset at sample "+i);
            }
            Check(events.ForceEvent(OralEventKind.Ceiling),"C02 ceiling encounter begins");
            float began=Time.time,arrived=-1,combatTime=0,moveStarted=-1,lastKill=-1,oneLeftAt=-1;
            bool arrivedChecked=false,delayChecked=false,flightChecked=false,oneLeftChecked=false,sawUnfold=false,sawTurn=false,sawFlight=false,returned=false;
            bool allShadowsHidden=true,allInside=true;float firstFlightAge=-1;Vector2 playerAtArrival=default,flightOrigin=default;
            OralCeilingBatRuntime[] bats=null;OralCeilingBatRuntime sample=null;Quaternion foldedRotation=Quaternion.identity;float wingMin=999,wingMax=-999;int killed=0;
            var stageSet=new HashSet<string>();var ascent=new List<Vector2>();var descent=new List<Vector2>();
            File.WriteAllText(Output+"/ceiling-timeline.csv","game_seconds,phase,remaining,arrival_age,bat_stage,player_x,player_y\n");
            yield return Record("01-ceiling-c02-bats",()=>!returned&&Time.time-began<24,()=>{
                if(level.EntityManager.AbilitySelectionDialog.MenuOpen){level.EntityManager.AbilitySelectionDialog.Close();Time.timeScale=1;}
                var stun=player.GetComponent<PlayerMiniStageStunRuntime>();if(stun!=null&&stun.enabled)typeof(PlayerMiniStageStunRuntime).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stun,null);
                if(events.CeilingPhase==1)ascent.Add(player.Position);if(events.CeilingPhase==3)descent.Add(player.Position);
                if(events.CeilingPhase==2){
                    if(!arrivedChecked){
                        arrivedChecked=true;arrived=Time.time;combatTime=level.CurrentLevelTime;playerAtArrival=player.Position;level.enabled=true;
                        Check(level.IsRunFlowPaused,"ceiling combat owns the main-stage pause");
                        Check(OralCeilingArenaGeometry.Contains(map,runtime.ConstrainPlayer(arena+Vector2.one*50,.55f),.5f),"actual player constraint uses C02 boundary");
                        Check(events.CeilingMonsters.Count==10&&events.CeilingRemaining==10,"exactly ten ceiling bats spawn");
                        bats=events.CeilingMonsters.Select(m=>m.GetComponent<OralCeilingBatRuntime>()).ToArray();Check(bats.All(b=>b!=null),"all ten use the ceiling bat controller");sample=bats[0];foldedRotation=sample.Body.transform.localRotation;
                        Check(bats.All(b=>b.HangingVisual!=null&&b.HangingVisual.sprite!=null),"all ten have original M02 hanging art");closeupTarget=sample.Body.transform;
                        Check(lower.IsFieldRuntimeSuspended&&lowerRenderers.All(r=>r.forceRenderingOff),"lower monster hides and suspends");
                        Check(lowerBoxes.All(c=>c==null||!c.gameObject.activeInHierarchy||!c.enabled),"lower props stop intercepting ceiling combat");
                        Check(portalRenderers.All(r=>r.forceRenderingOff)&&portalColliders.All(c=>!c.enabled),"opened lower blood-clot portal hides and disables collisions");
                        Check(!mini.CanEnterFromPortal&&!portal.CanInteract,"ceiling rejects lower portal interaction");
                    }
                    float age=Time.time-arrived;
                    foreach(var bat in bats.Where(b=>b!=null&&b.Monster!=null&&b.Monster.IsAlive)){
                        allShadowsHidden&=bat.ShadowsHidden;allInside&=OralCeilingArenaGeometry.Contains(map,bat.Monster.Position);
                        stageSet.Add(bat.Stage.ToString());sawUnfold|=bat.Stage.ToString()=="Unfolding";sawTurn|=bat.Stage.ToString()=="Turning";
                    }
                    if(age>.75f&&!delayChecked){delayChecked=true;Check(bats.All(b=>b.Stage.ToString()=="Hanging"&&!b.IsFlying),"M02 stays hanging during first arrival second");Check(bats.All(b=>b.Monster.IsFieldRuntimeSuspended),"hanging bats do not pursue or contact the player");}
                    if(sample!=null&&sample.IsFlying){
                        if(!sawFlight){sawFlight=true;firstFlightAge=sample.ArrivalAge;flightOrigin=sample.Monster.Position;moveStarted=Time.time;}
                        float wing=sample.LeftWing.transform.localEulerAngles.z;wingMin=Mathf.Min(wingMin,wing);wingMax=Mathf.Max(wingMax,wing);
                        if(!flightChecked&&Time.time-moveStarted>1f){
                            flightChecked=true;Check(sample.ArrivalAge>=2f-.1f,"flight begins after delay, unfolding, and turn");
                            Check(Quaternion.Angle(foldedRotation,sample.Body.transform.localRotation)>170,"M01 body turns a full half rotation from hanging presentation");
                            Check(sample.LeftWing.enabled&&sample.RightWing.enabled,"both original M01 wings remain visible");
                            Check(!sample.Body.flipY&&playerBody.flipY!=originalFlip,"flying bats face opposite the inverted player");
                            Check(Mathf.Abs(sample.FlightOrientationDegrees)<.01f,"bat atlas orientation correction reaches upright flight");
                            Check(Vector2.Distance(flightOrigin,sample.Monster.Position)>.03f,"flying bat actually pursues");
                            Check(Mathf.Abs(level.CurrentLevelTime-combatTime)<.01f,"ceiling combat freezes main stage clock");
                        }
                    }
                    if(age>2.2f&&age<3.6f)player.Move(Vector2.right);else player.Move(Vector2.zero);
                    if(age>5f&&killed<9&&Time.time-lastKill>.22f){var target=events.CeilingMonsters.FirstOrDefault(m=>m!=null&&m.HP>0);if(target!=null){target.TakeDamage(target.HP+1000);killed++;lastKill=Time.time;}}
                    if(killed==9&&events.CeilingRemaining==1){
                        if(oneLeftAt<0)oneLeftAt=Time.time;
                        if(!oneLeftChecked&&Time.time-oneLeftAt>.45f){oneLeftChecked=true;Check(events.CeilingPhase==2&&runtime.OnCeiling,"nine defeats keep the player in the ceiling encounter");var target=events.CeilingMonsters.First(m=>m!=null&&m.HP>0);target.TakeDamage(target.HP+1000);killed++;}
                    }
                    File.AppendAllText(Output+"/ceiling-timeline.csv",(Time.time-began).ToString("F3",CultureInfo.InvariantCulture)+","+events.CeilingPhase+","+events.CeilingRemaining+","+(sample!=null?sample.ArrivalAge:0).ToString("F3",CultureInfo.InvariantCulture)+","+(sample!=null?sample.Stage.ToString():"none")+","+player.Position.x.ToString("F3",CultureInfo.InvariantCulture)+","+player.Position.y.ToString("F3",CultureInfo.InvariantCulture)+"\n");
                }
                returned=arrivedChecked&&events.CurrentEvent==OralEventKind.None;
            });
            closeupTarget=null;level.enabled=false;player.Move(Vector2.zero);yield return null;
            Check(arrivedChecked&&delayChecked&&flightChecked,"full arrival and flight sequence was observed");Check(sawUnfold&&sawTurn&&sawFlight,"M02 transitions through unfolding and turning into M01 flight");
            Check(allShadowsHidden,"bats never draw ground shadows during ceiling presentation");Check(allInside,"ceiling bats remain within C02 playable boundary");Check(wingMax-wingMin>1,"M01 wings animate while flying");
            Check(killed==10&&oneLeftChecked&&returned&&!runtime.OnCeiling,"tenth defeat restores tongue gameplay");
            Check(ascent.Select(p=>Mathf.RoundToInt(p.x*100)).Distinct().Count()>5,"ascent moves smoothly despite transition stun");
            Check(descent.Select(p=>Mathf.RoundToInt(p.x*100)).Distinct().Count()>5,"return moves smoothly despite transition stun");
            Check(Vector2.Distance(player.Position,originalLanding)<.12f,"ceiling return restores the original off-center tongue position");
            Check(playerBody.flipY==originalFlip,"player orientation restores after return");Check(hurtbox==null||hurtbox.offset==hurtboxOffset&&hurtbox.size==hurtboxSize,"player damage bounds restore exactly");
            Check(!lower.IsFieldRuntimeSuspended&&lowerRenderers.Select((r,i)=>r==null||r.forceRenderingOff==lowerHidden[i]).All(v=>v),"lower monster resumes original visible state");
            Check(lowerBoxes.Select((c,i)=>c==null||c.enabled==lowerEnabled[i]).All(v=>v),"lower props restore original collision states");Check(!level.IsRunFlowPaused,"ceiling return releases stage clock");
            Check(portalRenderers.Select((r,i)=>r.forceRenderingOff==portalHidden[i]).All(v=>v)&&portalColliders.Select((c,i)=>c.enabled==portalEnabled[i]).All(v=>v),"opened portal restores original visibility and collision state");
            Check(Mathf.Abs(Camera.main.orthographicSize-normalCameraSize)<.01f,"ceiling camera restores original gameplay zoom");
            File.WriteAllText(Output+"/ceiling-metrics.json","{\"batCount\":10,\"firstFlightAge\":"+firstFlightAge.ToString("F3",CultureInfo.InvariantCulture)+",\"observedBatStages\":\""+string.Join(",",stageSet)+"\"}");
            yield return SpringChecks(level);SessionState.SetBool(Key+"Done",true);
        }
        static IEnumerator SpringChecks(LevelManager level)
        {
            var player=level.PlayerCharacter;var map=FiniteStomachMap.Instance;var taste=OralFieldEvents.Instance.Tastes;var tongue=OralMapGeometry.TongueCenter(map);
            Clear(level);Place(player,tongue);yield return new WaitForSeconds(.2f);
            Check(taste.Begin(OralTasteKind.Sour),"whole-lemon spring begins");
            // The previous runtime used this size-dependent distance. Compare against
            // that baseline rather than assuming an unscaled Ashi sprite is two units.
            float priorDistance=Mathf.Max(2f,taste.LemonLength*1.7f),newDistance=taste.LemonBounceDistance;
            var body=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
            File.WriteAllText(Output+"/spring-configuration.json","{\"lemonLength\":"+taste.LemonLength.ToString("F6",CultureInfo.InvariantCulture)+",\"playerBodyBoundsWidth\":"+body.bounds.size.x.ToString("F6",CultureInfo.InvariantCulture)+",\"playerBodyScale\":"+body.transform.lossyScale.x.ToString("F6",CultureInfo.InvariantCulture)+",\"playerVisibleWidth\":"+(PromptSpriteGeometry.Bounds(body.sprite).width*Mathf.Abs(body.transform.lossyScale.x)).ToString("F6",CultureInfo.InvariantCulture)+",\"previousDistance\":"+priorDistance.ToString("F6",CultureInfo.InvariantCulture)+",\"newDistance\":"+newDistance.ToString("F6",CultureInfo.InvariantCulture)+"}");
            Check(taste.LemonVisual!=null,"whole-lemon metal spring visual is installed");Check(Mathf.Abs(newDistance-priorDistance*2f)<.01f,"unclipped recoil is twice the previous size-dependent distance");
            var launch=taste.LemonPosition;Place(player,launch+Vector2.right*1.2f);Vector2 expected=OralMapGeometry.Clamp(map,launch+taste.LemonDirection*taste.LemonBounceDistance,.55f,OralZone.Tongue);
            Check(Mathf.Abs(Vector2.Distance(launch,expected)-newDistance)<.1f,"spring fixture has unobstructed tongue space for the doubled recoil");
            bool bounced=false,landed=false;Vector2 actualLaunch=launch;float minExtension=1,maxExtension=1,actualDistance=0;int pulses=taste.LemonVisual.PulseCount;float began=Time.time;
            captureFocus=(launch+expected)*.5f+Vector2.up*.35f;shotSize=4.6f;
            yield return Record("02-whole-lemon-spring",()=>Time.time-began<5,()=>{
                var bounce=player.GetComponent<OralTasteBounce>();bool active=bounce!=null&&bounce.Active;
                if(!bounced&&active)actualLaunch=player.Position;
                player.Move(!bounced&&!active&&Time.time-began>.45f?Vector2.left:Vector2.zero);
                bounced|=active;minExtension=Mathf.Min(minExtension,taste.LemonVisual.Extension);maxExtension=Mathf.Max(maxExtension,taste.LemonVisual.Extension);
                if(bounced&&!active&&!landed){landed=true;actualDistance=Vector2.Distance(actualLaunch,player.Position);}
            });
            player.Move(Vector2.zero);captureFocus=null;
            Check(bounced&&landed,"actual player enters and completes spring recoil");Check(Mathf.Abs(actualDistance-priorDistance*2f)<.12f,"measured player recoil travels twice the prior distance");Check(minExtension<.95f&&maxExtension>1.05f,"metal spring visibly compresses and expands");Check(taste.LemonVisual.PulseCount>pulses,"spring pulse is triggered by the actual rebound");
            Check(Mathf.Abs(taste.LemonVisual.Extension-1)<.04f,"spring returns to its original extension");Check(player.GetComponent<Rigidbody2D>().simulated,"recoil restores player physics");
            var edge=OralMapGeometry.Clamp(map,tongue+taste.LemonDirection*50,.6f,OralZone.Tongue);Place(player,edge);
            var bounceMethod=typeof(OralTasteEvents).GetMethod("StartBounce",BindingFlags.Instance|BindingFlags.NonPublic);
            Check((bool)bounceMethod.Invoke(taste,new object[]{player.gameObject,edge}),"boundary recoil starts through production rebound path");
            yield return null;float frozenExtension=taste.LemonVisual.Extension;Vector2 frozenPosition=player.Position;
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.2f);
            Check(Vector2.Distance(frozenPosition,player.Position)<.001f&&Mathf.Abs(frozenExtension-taste.LemonVisual.Extension)<.001f,"pause freezes both spring and rebound");Time.timeScale=1;
            float stopAt=Time.time+2;while((player.GetComponent<OralTasteBounce>()?.Active??false)&&Time.time<stopAt)yield return null;
            Check(!(player.GetComponent<OralTasteBounce>()?.Active??false)&&OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Tongue,"edge rebound lands safely on the tongue");
            Check(Vector2.Distance(edge,player.Position)<=taste.LemonBounceDistance+.01f,"boundary clamp never extends recoil distance");
            taste.Configure(level);yield return null;Check(!(player.GetComponent<OralTasteBounce>()?.Active??false),"spring teardown clears temporary motion ownership");
            File.WriteAllText(Output+"/spring-metrics.json","{\"previousDistance\":"+priorDistance.ToString("F6",CultureInfo.InvariantCulture)+",\"newDistance\":"+newDistance.ToString("F6",CultureInfo.InvariantCulture)+",\"measuredDistance\":"+actualDistance.ToString("F6",CultureInfo.InvariantCulture)+",\"springMinExtension\":"+minExtension.ToString("F3",CultureInfo.InvariantCulture)+",\"springMaxExtension\":"+maxExtension.ToString("F3",CultureInfo.InvariantCulture)+"}");
        }
        static IEnumerator Record(string name,Func<bool> active,Action step)
        {
            string dir=Output+"/"+name;Directory.CreateDirectory(dir);foreach(var old in Directory.GetFiles(dir,"*.png"))File.Delete(old);
            File.WriteAllText(dir+"/times.csv","frame,game_seconds\n");int oldRate=Time.captureFramerate;Time.captureFramerate=15;float start=Time.time,timeout=Time.realtimeSinceStartup+180;int frame=0;
            string closeDir=Output+"/03-ceiling-bat-closeup";if(name=="01-ceiling-c02-bats"){Directory.CreateDirectory(closeDir);foreach(var old in Directory.GetFiles(closeDir,"*.png"))File.Delete(old);}
            do{yield return null;step?.Invoke();if(Index==0)Frame(name+"/"+frame.ToString("D4"));
                if(Index==0&&name=="01-ceiling-c02-bats"&&closeupTarget!=null&&closeupTarget.gameObject.activeInHierarchy){var previous=captureFocus;float previousSize=shotSize;captureFocus=closeupTarget.position;shotSize=1.6f;Frame("03-ceiling-bat-closeup/"+frame.ToString("D4"));captureFocus=previous;shotSize=previousSize;}
                File.AppendAllText(dir+"/times.csv",frame+","+(Time.time-start).ToString("F4",CultureInfo.InvariantCulture)+"\n");frame++;}while(active()&&Time.realtimeSinceStartup<timeout);
            Time.captureFramerate=oldRate;Check(!active(),"recorded sequence finishes within timeout");
        }
        static void Frame(string name)
        {
            var camera=Camera.main;var player=OralStageRuntime.Instance?.Level.PlayerCharacter;if(camera==null||player==null)return;
            var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();var orders=overlays.Select(c=>c.sortingOrder).ToArray();var flags=overlays.Select(c=>c.overrideSorting).ToArray();
            var target=camera.targetTexture;var active=RenderTexture.active;var position=camera.transform.position;float zoom=camera.orthographicSize;var rt=RenderTexture.GetTemporary(960,540,24);Texture2D image=null;
            try{
                if(captureFocus.HasValue){var focus=captureFocus.Value;camera.transform.position=new Vector3(focus.x,focus.y,position.z);camera.orthographicSize=shotSize;}
                // Coroutine captures run before Unity LateUpdate. Refresh the same real
                // presentation ownership that the normal rendering phase executes.
                foreach(var crush in Object.FindObjectsOfType<JawCrushVisual>())if(crush.enabled)typeof(JawCrushVisual).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(crush,null);
                foreach(var bars in Object.FindObjectsOfType<PlayerCombatBars>())typeof(PlayerCombatBars).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bars,null);
                camera.targetTexture=rt;foreach(var c in overlays){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.1f;c.overrideSorting=true;c.sortingOrder=Mathf.Clamp(32100+c.sortingOrder,32100,32760);}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image=new Texture2D(960,540,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
            }finally{
                for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];overlays[i].sortingOrder=orders[i];overlays[i].overrideSorting=flags[i];}
                camera.targetTexture=target;camera.transform.position=position;camera.orthographicSize=zoom;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)Object.Destroy(image);Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
