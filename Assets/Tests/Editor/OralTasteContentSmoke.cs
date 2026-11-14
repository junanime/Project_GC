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
    public static class OralTasteContentSmoke
    {
        const string Key="OralTasteContentSmoke", Root="Library/OralTasteContentProof";
        static readonly string[] Names={"ashi","ari","hyuki","shini"};
        static readonly string[] Blueprints={"Main","Ari","Hyuki","Shini"};
        static int Index=>SessionState.GetInt(Key+"Index",0);
        static string Output=>Root+"/"+Index+"-"+Names[Index];
        static bool started; static double deadline; static float shotSize=5f;
        [Serializable] sealed class Pref { public string key; public bool had; public int value; }
        [Serializable] sealed class Backup { public Pref[] prefs; }
        static OralTasteContentSmoke(){if(SessionState.GetBool(Key,false))Attach();}
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
            if(!condition)throw new Exception("ORAL_TASTE_CONTENT_FAIL "+Names[Index]+": "+message);
            SessionState.SetInt(Key+"Checks",SessionState.GetInt(Key+"Checks",0)+1);
            File.AppendAllText(Root+"/checks.txt","PASS "+Names[Index]+": "+message+"\n");Debug.Log("ORAL_TASTE_CONTENT_PASS "+Names[Index]+": "+message);
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
        static IEnumerator Checks(LevelManager level)
        {
            var preferences=GamePreferences.Current.Copy();preferences.pauseOnFocusLoss=false;preferences.muteOnFocusLoss=false;GamePreferences.Apply(preferences,false);
            Application.runInBackground=true;Time.timeScale=1;RunScore.Exclude("Stage zero taste and ambient content integration QA");Silence(level);
            Check(StageProgression.Ensure(level).BeginDirectOral(true),"stage zero entry");
            while(OralStageRuntime.Instance==null||StageProgression.Ensure(level).Travelling)yield return null;
            Silence(level);Clear(level);var player=level.PlayerCharacter;var map=FiniteStomachMap.Instance;var events=OralFieldEvents.Instance;
            events.AutoSchedule=false;foreach(var cavity in events.Cavities){cavity.Healed=true;cavity.Active=false;}
            var center=OralMapGeometry.TongueCenter(map);Place(player,center);Set(player,"isInvincible",true);
            var taste=OralTasteEvents.Instance;var ambient=OralAmbientContent.Instance;
            Check(taste!=null&&ambient!=null,"both production content controllers installed");
            if(Environment.GetCommandLineArgs().Contains("-oralDensity")){
                yield return Retakes(level,events,player,center);
                yield return Density(level,events,player,center);
                SessionState.SetBool(Key+"Done",true);yield break;
            }
            if(Environment.GetCommandLineArgs().Contains("-oralQuick")){
                yield return CharacterQuick(level,events,player,center);
                SessionState.SetBool(Key+"Done",true);yield break;
            }
            var group=level.CurrentLevelBlueprint.monsters.Select((g,i)=>new{g,i}).First(x=>x.g.monsterBlueprints.Any(b=>b!=null&&OralMonsterRoster.IsBlueprint(b)));
            var bp=group.g.monsterBlueprints.First(b=>b!=null&&OralMonsterRoster.IsBlueprint(b));
            Func<Vector2,Monster> spawn=p=>level.EntityManager.SpawnMonster(group.i,OralMapGeometry.Clamp(map,p,.5f,OralZone.Tongue),bp,0,false);

            Check(taste.Begin(OralTasteKind.Sweet),"sweet honeycomb begins");Check(taste.HoneyPositions.Count>0,"honeycomb pieces placed");
            foreach(var p in taste.HoneyPositions){spawn(p+Vector2.right*.35f);spawn(p+Vector2.up*.35f);}
            float start=Time.time;yield return Record("01-honeycomb",()=>Time.time-start<9,()=>{});
            Check(taste.HoneyFedCount>0,"real monsters eat honey and gain capped buff");
            Check(level.EntityManager.LivingMonsters.Where(m=>m!=null&&m.HP>0).All(m=>OralMonsterRoster.IsBlueprint(m.Blueprint)),"honey affects original stage-zero monster roster");
            float beforeAge=taste.Age;level.SetRunFlowPaused(true);yield return new WaitForSecondsRealtime(.25f);Check(Mathf.Approximately(beforeAge,taste.Age),"taste age freezes during run-flow pause");level.SetRunFlowPaused(false);
            taste.Configure(level);Clear(level);Place(player,center);

            Check(taste.Begin(OralTasteKind.Salty),"salt-crystal wave begins");start=Time.time;yield return Record("02-salt-crystals",()=>Time.time-start<7,()=>{});
            Check(taste.SaltSpawnedCount>0&&taste.SaltSpawnedCount<=24,"salt special wave has bounded population");
            var saltBodies=level.EntityManager.LivingMonsters.Where(m=>m!=null&&m.HP>0).ToArray();Check(saltBodies.Length>0&&saltBodies.All(m=>m.BodyRenderer!=null&&m.BodyRenderer.sprite!=null),"salt wave uses valid sprite assets");
            taste.Configure(level);yield return null;
            Check(saltBodies.All(m=>m==null||!m.gameObject.activeInHierarchy||m.HP<=0),"salt cleanup despawns live owners before destroying runtime blueprint");Clear(level);Place(player,center);

            Check(taste.Begin(OralTasteKind.Sour),"sour lemon peel begins");Check(taste.LemonLength>.5f,"lemon peel receives gameplay size");
            Place(player,taste.LemonPosition-taste.LemonDirection*.35f);bool bounce=false;start=Time.time;
            yield return Record("03-lemon-peel",()=>Time.time-start<5,()=>{player.Move(taste.LemonDirection);bounce|=player.GetComponent<OralTasteBounce>()?.Active??false;});
            player.Move(Vector2.zero);Check(bounce,"player triggers actual lemon rebound");taste.Configure(level);yield return null;
            Check(!(player.GetComponent<OralTasteBounce>()?.Active??false),"cancel restores lemon bounce state");Place(player,center);
            var bouncedMonster=spawn(center+Vector2.up);var interruptedBounce=bouncedMonster.GetComponent<OralTasteBounce>()??bouncedMonster.gameObject.AddComponent<OralTasteBounce>();
            interruptedBounce.Begin(taste,center+Vector2.right);MiniStageRuntimeState.EnterMiniStage(null);level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
            yield return null;taste.CancelBounces();
            Check(bouncedMonster.IsFieldRuntimeSuspended,"lemon cancellation preserves global mini-stage suspension");
            MiniStageRuntimeState.ExitMiniStage(null);level.EntityManager.SetFieldMonsterRuntimeSuspended(false);Clear(level);

            Check(taste.Begin(OralTasteKind.Bitter),"bitter optional morsel begins");Place(player,taste.BitterPosition);Set(player,"currentHealth",player.MaxHealth*.45f);
            Check(taste.TryEatBitter(),"nearby interaction consumes bitter morsel");Check(!taste.TryEatBitter(),"bitter cannot be consumed twice");
            float health=player.CurrentHealth;start=Time.time;yield return Record("04-bitter-morsel",()=>Time.time-start<7,()=>{});
            Check(taste.BitterRewards==1&&player.CurrentHealth>health,"enduring bitter grants one healing reward");taste.Configure(level);Place(player,center);

            Check(taste.Begin(OralTasteKind.Spicy),"spicy B tongue coverage begins");
            captureFocus=map.WorldRect.center;shotSize=map.WorldRect.height*.53f;bool checkedCoverage=false,leftBurn=false,teethProtected=false;int ticksAtExit=-1;float activeSeconds=0;start=Time.time;
            Set(player,"currentHealth",player.MaxHealth);Set(player,"isInvincible",false);
            var teeth=OralMapGeometry.ToothCenters(map);var safeTooth=teeth.OrderBy(p=>Vector2.Distance(p,center)).First();
            float spicyWarning=taste.Duration-15f;float spicyRecording=taste.Duration+1f;
            yield return Record("05-spicy-tongue",()=>Time.time-start<spicyRecording,()=>{
                float age=Time.time-start;if(taste.SpicyActive){activeSeconds+=Time.deltaTime;if(!checkedCoverage){checkedCoverage=true;Check(taste.ContainsSpice(center),"tongue center belongs to spicy damage mask");Check(teeth.All(p=>!taste.ContainsSpice(p)),"every tooth excluded from spicy damage mask");}}
                if(age>spicyWarning+1.2f&&!leftBurn){leftBurn=true;ticksAtExit=taste.BurnTicks;Place(player,safeTooth);Check(taste.BurnRemaining>0,"leaving hazard retains burn duration");}
                if(age>spicyWarning+5f&&!teethProtected){teethProtected=true;Check(taste.BurnTicks>ticksAtExit,"burn ticks continue after leaving tongue");Check(taste.BurnRemaining<=.01f,"burn expires on safe teeth without refresh");Set(player,"isInvincible",true);}
            });
            Check(checkedCoverage&&Mathf.Abs(activeSeconds-15)<.2f,"spicy active coverage lasts exactly fifteen game seconds");Check(!taste.SpicyActive,"spicy field disappears after fifteen seconds");
            captureFocus=null;shotSize=5;Set(player,"isInvincible",true);taste.Configure(level);Place(player,center);

            // Ground content may coexist with a movement event, but pauses out of the lower arena.
            Check(taste.Begin(OralTasteKind.Sweet),"sweet restarts for overlap audit");spawn(center+Vector2.up);
            Check(events.ForceEvent(OralEventKind.Hiccup),"hiccup coexists with independent taste event");yield return new WaitForSeconds(1.8f);
            Check(events.Additions.HiccupHolding,"existing hiccup retains three-second hold");
            beforeAge=taste.Age;MiniStageRuntimeState.EnterMiniStage(null);yield return new WaitForSeconds(.3f);Check(Mathf.Approximately(beforeAge,taste.Age),"mini-stage freezes floor taste timeline");
            MiniStageRuntimeState.ExitMiniStage(null);while(events.CurrentEvent!=OralEventKind.None)yield return null;taste.Configure(level);Clear(level);

            var protectedMonster=spawn(center+Vector2.right*1.5f);protectedMonster.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
            var armor=protectedMonster.gameObject.AddComponent<OralTartarArmor>();armor.Arm(protectedMonster);var barrier=protectedMonster.GetComponent<MonsterNeedleBarrier>()??protectedMonster.gameObject.AddComponent<MonsterNeedleBarrier>();barrier.Arm();barrier.SodaTint=true;
            float hp=protectedMonster.HP;protectedMonster.TakeDamage(100);Check(Mathf.Approximately(hp,protectedMonster.HP),"stacked shields block body damage");
            var armorBounce=protectedMonster.GetComponent<OralTasteBounce>()??protectedMonster.gameObject.AddComponent<OralTasteBounce>();armorBounce.Begin(taste,protectedMonster.Position+Vector2.right*.4f);yield return new WaitForSeconds(.2f);
            Check(protectedMonster.GetComponentsInChildren<SpriteRenderer>().Count(r=>r.name.StartsWith("치석 갑옷 판")&&r.enabled)==7,"lemon bounce keeps all tartar plates visible");
            Check(Vector2.Distance(barrier.VisualRoot.position,PromptSpriteGeometry.WorldBounds(protectedMonster.BodyRenderer).center)<.05f,"bubble follows lifted body during lemon bounce");taste.CancelBounces();yield return null;
            Check(events.ForceEvent(OralEventKind.Hiccup),"stacked protected monster participates in hiccup");yield return new WaitForSeconds(1.8f);
            var lift=protectedMonster.GetComponent<OralHiccupLift>();
            Check(lift!=null&&lift.Active&&lift.Height>0,"protected body is lifted above ground");
            Check(barrier.VisualRoot.GetComponent<MeshRenderer>().forceRenderingOff,"ground bubble is hidden while lifted");
            Check(lift.VisualRoot.GetComponentsInChildren<MeshRenderer>().Any(m=>m.enabled&&!m.forceRenderingOff&&m.GetComponent<MeshFilter>()?.sharedMesh==barrier.VisualRoot.GetComponent<MeshFilter>().sharedMesh),"airborne copy retains actual bubble mesh");
            Check(lift.VisualRoot.GetComponentsInChildren<SpriteRenderer>().Count(r=>r.name.Contains("치석 갑옷 판")&&r.name.Contains("공중")&&r.enabled)>=7,"all seven tartar plates remain visible in airborne copy");
            while(events.CurrentEvent!=OralEventKind.None)yield return null;
            Check(!barrier.VisualRoot.GetComponent<MeshRenderer>().forceRenderingOff&&barrier.IsProtected&&armor.IsProtected,"hiccup landing restores both intact defense layers");
            yield return new WaitForSeconds(.3f);int rebuildCount=barrier.MeshRebuildCount;yield return new WaitForSeconds(.3f);
            Check(barrier.MeshRebuildCount==rebuildCount,"idle barrier avoids redundant mesh allocation");
            int fired=0;start=Time.time;shotSize=2.5f;captureFocus=protectedMonster.BodyHitbox.bounds.center;
            yield return Record("06-tartar-armor",()=>Time.time-start<8,()=>{
                if(fired<6&&Time.time-start>1+fired*.8f){
                    var aim=(Vector2)protectedMonster.BodyHitbox.bounds.center;
                    FireNeedle(level,aim-Vector2.right*.8f,Vector2.right,1<<protectedMonster.BodyHitbox.gameObject.layer);fired++;
                    File.AppendAllText(Output+"/needle-hits.txt","shot="+fired+" outer="+barrier.Hits+" inner="+armor.Hits+" hp="+protectedMonster.HP+" aim="+aim+"\n");
                }
            });
            captureFocus=null;shotSize=5;
            Check(fired==6&&!barrier.IsProtected&&!armor.IsProtected,"three soda needles then three tartar needles break both layers");Check(Mathf.Approximately(hp,protectedMonster.HP),"breaking hit does not leak damage through armor");
            protectedMonster.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeRotation;protectedMonster.DespawnForStageTransfer();var reused=spawn(center+Vector2.right);
            Check(!MonsterNeedleBarrier.Blocks(reused),"pooled monster does not retain old protection");Clear(level);Place(player,center);

            ambient.BeginBoulder();Check(ambient.RockActive,"rolling tartar boulder appears");
            var boulderTargets=new List<Monster>();for(int k=1;k<=3;k++){var obstacle=spawn(ambient.RockPosition+Vector2.right*(1+k*.8f));obstacle.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;boulderTargets.Add(obstacle);}
            Place(player,ambient.RockPosition-Vector2.right*.65f);bool slow=false,dashed=false,moved=false;Vector2 oldRock=ambient.RockPosition;start=Time.time;
            yield return Record("07-tartar-boulder",()=>Time.time-start<10,()=>{
                float age=Time.time-start;player.Move(age<2?Vector2.right:Vector2.zero);slow|=ambient.PlayerMoveMultiplier<.99f;
                if(age>2&&!dashed){dashed=true;Place(player,ambient.RockPosition-Vector2.right*.65f);player.Move(Vector2.right);player.TryDash();}
                moved|=Vector2.Distance(oldRock,ambient.RockPosition)>.5f;
            });player.Move(Vector2.zero);Check(moved,"rolling boulder moves in actual simulation");Check(slow,"walking push applies thirty percent movement reduction");Check(ambient.RockHits>0,"rolling boulder damages monsters in its path");
            foreach(var obstacle in boulderTargets)if(obstacle!=null)obstacle.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeRotation;Clear(level);
            ambient.ClearAll();yield return null;Check(Mathf.Approximately(ambient.PlayerMoveMultiplier,1),"boulder cleanup restores movement speed");Place(player,center);

            ambient.BeginGuard();Check(ambient.GuardActive,"classic biker guard appears");var goal=ambient.GuardGoal;Check(OralMapGeometry.ZoneAt(map,goal)==OralZone.Teeth,"guard goal is actual tooth walkable surface");
            captureFocus=(center+goal)*.5f;shotSize=Mathf.Max(5,Vector2.Distance(center,goal)*.7f);int prior=ambient.GuardCompleted;bool led=false;start=Time.time;
            yield return Record("08-biker-guard",()=>Time.time-start<15,()=>{if(Time.time-start>3&&!led){led=true;Place(player,goal);}});
            Check(ambient.GuardCompleted>prior,"guided guard completes tooth arrival");Check(ambient.GuardReward!=null&&ambient.GuardReward.CanInteract,"guard grants usable chest reward once");ambient.ClearAll();captureFocus=null;shotSize=5;Place(player,center);

            Check(taste.Begin(OralTasteKind.Spicy),"spicy begins for ceiling suspension audit");Check(events.ForceEvent(OralEventKind.Ceiling),"ceiling can begin alongside floor hazard");
            while(events.CeilingPhase<2)yield return null;float ceilingAge=taste.Age;yield return new WaitForSeconds(.25f);Check(Mathf.Approximately(ceilingAge,taste.Age),"floor hazard suspended during ceiling play");
            foreach(var m in events.CeilingMonsters.ToArray())m.TakeDamage(m.HP+1000);while(events.CurrentEvent!=OralEventKind.None)yield return null;
            taste.Configure(level);ambient.ClearAll();Clear(level);Place(player,center);
            Check(Mathf.Approximately(level.LevelDuration,600),"oral stage duration is ten minutes");
            events.AutoSchedule=true;var primaryKinds=new HashSet<OralEventKind>();var tasteKinds=new HashSet<OralTasteKind>();
            int oldRate=Time.captureFramerate;Time.captureFramerate=10;float soakStart=Time.time,lastRefresh=-30,ceilingSince=-1;int peakPopulation=0;
            while(Time.time-soakStart<600)
            {
                float elapsed=Time.time-soakStart;Set(level,"levelTime",elapsed);
                if(events.CurrentEvent!=OralEventKind.None)primaryKinds.Add(events.CurrentEvent);if(taste.Active)tasteKinds.Add(taste.Kind);
                if(events.OnCeiling){if(ceilingSince<0)ceilingSince=elapsed;if(elapsed-ceilingSince>3)foreach(var m in events.CeilingMonsters.ToArray())m.TakeDamage(m.HP+1000);}else ceilingSince=-1;
                if(elapsed-lastRefresh>30&&!events.OnCeiling){lastRefresh=elapsed;Clear(level);for(int k=0;k<8;k++)spawn(center+new Vector2((k%4-1.5f)*1.2f,1+k/4));Place(player,center);}
                peakPopulation=Mathf.Max(peakPopulation,level.EntityManager.LivingMonsters.Count(m=>m!=null&&m.HP>0));
                if(level.EntityManager.AbilitySelectionDialog.MenuOpen){level.EntityManager.AbilitySelectionDialog.Close();Time.timeScale=1;}
                yield return null;
            }
            Time.captureFramerate=oldRate;events.AutoSchedule=false;
            Check(primaryKinds.Count>=7,"ten-minute automatic schedule exercises seven or more primary events");
            Check(tasteKinds.Count==5,"ten-minute shuffled content schedule reaches all five tastes");
            Check(peakPopulation<=160,"overlap stress keeps bounded field population");
            File.WriteAllText(Output+"/soak.json","{\"simulatedSeconds\":600,\"controlledPopulation\":true,\"primaryKinds\":\""+string.Join(",",primaryKinds)+"\",\"tasteKinds\":\""+string.Join(",",tasteKinds)+"\",\"peakPopulation\":"+peakPopulation+"}");
            taste.Configure(level);ambient.ClearAll();events.CancelAll();yield return null;
            Check(Object.FindObjectsOfType<OralTasteEvents>().Length==0&&Object.FindObjectsOfType<OralAmbientContent>().Length==0,"stage teardown removes new runtimes and their effects");
            SessionState.SetBool(Key+"Done",true);
        }
        static IEnumerator Retakes(LevelManager level,OralFieldEvents events,Character player,Vector2 center)
        {
            var taste=events.Tastes;var ambient=events.Ambient;
            var group=level.CurrentLevelBlueprint.monsters.Select((g,i)=>new{g,i}).First(x=>x.g.monsterBlueprints.Any(b=>b!=null&&OralMonsterRoster.IsBlueprint(b)));
            var bp=group.g.monsterBlueprints.First(b=>b!=null&&OralMonsterRoster.IsBlueprint(b));
            Check(taste.Begin(OralTasteKind.Sweet),"final visible-sized honeycomb begins");foreach(var p in taste.HoneyPositions){level.EntityManager.SpawnMonster(group.i,p+Vector2.right*.35f,bp,0,false);level.EntityManager.SpawnMonster(group.i,p+Vector2.up*.35f,bp,0,false);}
            float start=Time.time;yield return Record("01-honeycomb",()=>Time.time-start<9,()=>{});Check(taste.HoneyFedCount>0,"final honeycomb retains feeding behavior");taste.Configure(level);Clear(level);Place(player,center);
            Check(taste.Begin(OralTasteKind.Sour),"final visible-sized lemon begins");
            var ashi=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            var ashiBody=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
            float ashiWidth=PromptSpriteGeometry.Bounds(ashi.idleSpriteSequence[0]).width*Mathf.Abs(ashiBody.transform.lossyScale.x);
            Check(Mathf.Abs(taste.LemonLength/ashiWidth-2.2f)<.01f,"lemon length is exactly 2.2 visible Ashi widths");
            Place(player,taste.LemonPosition-taste.LemonDirection*.35f);bool bounce=false;start=Time.time;
            yield return Record("03-lemon-peel",()=>Time.time-start<5,()=>{player.Move(taste.LemonDirection);bounce|=player.GetComponent<OralTasteBounce>()?.Active??false;});
            player.Move(Vector2.zero);Check(bounce,"final visible-sized lemon still triggers rebound");taste.Configure(level);Place(player,center);
            Check(taste.Begin(OralTasteKind.Bitter),"bitter visible interaction retake begins");Place(player,taste.BitterPosition);Set(player,"currentHealth",player.MaxHealth*.45f);float health=player.CurrentHealth;bool consumed=false;start=Time.time;
            yield return Record("04-bitter-morsel",()=>Time.time-start<8,()=>{if(Time.time-start>1.2f&&!consumed){consumed=taste.TryEatBitter();Check(consumed,"bitter consumed after visible prompt interval");}});
            Check(taste.BitterRewards==1&&player.CurrentHealth>health,"bitter retake includes delayed healing");taste.Configure(level);Place(player,center);
            Check(ambient.BeginGuard(),"guard close-up retake begins");var goal=ambient.GuardGoal;int prior=ambient.GuardCompleted;bool led=false;start=Time.time;shotSize=4;
            yield return Record("08-biker-guard",()=>Time.time-start<15,()=>{captureFocus=(ambient.GuardActive?ambient.GuardPosition:goal)+Vector2.up*.5f;if(Time.time-start>3&&!led){led=true;Place(player,goal);}});
            Check(ambient.GuardCompleted>prior&&ambient.GuardReward!=null,"guard close-up retains completed reward");ambient.ClearAll();taste.Configure(level);Clear(level);captureFocus=null;shotSize=5;Place(player,center);Set(player,"currentHealth",player.MaxHealth);
        }
        static IEnumerator CharacterQuick(LevelManager level,OralFieldEvents events,Character player,Vector2 center)
        {
            var taste=events.Tastes;int previousRate=Time.captureFramerate;Time.captureFramerate=10;
            Check(taste.Begin(OralTasteKind.Sour),"character lemon rebound begins");Place(player,taste.LemonPosition-taste.LemonDirection*.35f);bool seen=false;float start=Time.time;
            while(Time.time-start<4){player.Move(taste.LemonDirection);if(player.GetComponent<OralTasteBounce>()?.Active??false){if(!seen)Frame("quick-lemon");seen=true;}yield return null;}
            player.Move(Vector2.zero);Check(seen,"selected actual character performs lemon rebound");taste.Configure(level);yield return null;Check(!(player.GetComponent<OralTasteBounce>()?.Active??false)&&player.GetComponent<Rigidbody2D>().simulated,"character bounce restores physics");Place(player,center);
            Check(taste.Begin(OralTasteKind.Bitter),"character bitter challenge begins");Place(player,taste.BitterPosition);Set(player,"currentHealth",player.MaxHealth*.45f);float health=player.CurrentHealth;Check(taste.TryEatBitter(),"character bitter interaction succeeds");yield return new WaitForSeconds(5.5f);
            Check(taste.BitterRewards==1&&player.CurrentHealth>health,"character receives delayed bitter heal");Frame("quick-bitter");taste.Configure(level);Time.captureFramerate=previousRate;
        }
        static IEnumerator Density(LevelManager level,OralFieldEvents events,Character player,Vector2 center)
        {
            // No population clearing or manual spawning: use the production normal and special schedules.
            // Invincibility and disabled player attacks deliberately retain enemies for crowd stress.
            Time.captureFramerate=0;Time.timeScale=1;Set(level,"levelTime",90f);level.enabled=true;
            foreach(var spawner in Object.FindObjectsOfType<TimedSpecialMonsterSpawner>())spawner.enabled=true;
            foreach(var spawner in Object.FindObjectsOfType<AcidToadSpawn>())spawner.enabled=true;
            events.AutoSchedule=true;float start=Time.realtimeSinceStartup,ceilingAt=-1,lastSample=-10;int peak=0,armored=0,samples=0;var kinds=new HashSet<OralEventKind>();
            File.WriteAllText(Output+"/density.csv","wall_seconds,game_seconds,living_monsters,tartar_monsters,event\n");
            while(Time.realtimeSinceStartup-start<120){
                float elapsed=Time.realtimeSinceStartup-start;
                player.Move(new Vector2(Mathf.Cos(elapsed*.25f),Mathf.Sin(elapsed*.25f))*.7f);
                if(events.OnCeiling){if(ceilingAt<0)ceilingAt=elapsed;if(elapsed-ceilingAt>3)foreach(var m in events.CeilingMonsters.ToArray())m.TakeDamage(m.HP+1000);}else ceilingAt=-1;
                int living=level.EntityManager.LivingMonsters.Count(m=>m!=null&&m.HP>0);
                int layers=level.EntityManager.LivingMonsters.Count(m=>m!=null&&m.HP>0&&(m.GetComponent<OralTartarArmor>()?.IsProtected??false));
                peak=Mathf.Max(peak,living);armored=Mathf.Max(armored,layers);if(events.CurrentEvent!=OralEventKind.None)kinds.Add(events.CurrentEvent);
                if(elapsed-lastSample>=10){lastSample=elapsed;samples++;File.AppendAllText(Output+"/density.csv",elapsed.ToString("F2",CultureInfo.InvariantCulture)+","+level.CurrentLevelTime.ToString("F2",CultureInfo.InvariantCulture)+","+living+","+layers+","+events.CurrentEvent+"\n");Frame("density-"+samples.ToString("D2"));}
                if(level.EntityManager.AbilitySelectionDialog.MenuOpen){level.EntityManager.AbilitySelectionDialog.Close();Time.timeScale=1;}
                yield return null;
            }
            player.Move(Vector2.zero);level.enabled=false;events.AutoSchedule=false;
            Check(level.NormalMonstersSpawned>0,"production normal schedule spawns without fixture replenishment");
            Check(peak>=90,"actual live crowd reaches at least ninety monsters");Check(armored>0,"production ninety-second tartar spawn hook creates armor");
            File.WriteAllText(Output+"/density.json","{\"wallSeconds\":120,\"manualMonsterSpawns\":0,\"periodicPopulationClears\":0,\"invincible\":true,\"playerAttacks\":false,\"normalSpawned\":"+level.NormalMonstersSpawned+",\"peakPopulation\":"+peak+",\"peakTartarMonsters\":"+armored+",\"primaryKinds\":\""+string.Join(",",kinds)+"\"}");
        }
        static void FireNeedle(LevelManager level,Vector2 origin,Vector2 direction,int targetLayer)
        {
            var dart=SyringeAbilityResolver.FindOwnedOrFirst(Object.FindObjectOfType<AbilityManager>());int pool=level.EntityManager.AddPoolForProjectile(dart.ProjectilePrefab);
            var p=(SyringeProjectile)level.EntityManager.SpawnProjectile(pool,origin,10,0,3,targetLayer);Set(p,"useLaunchForwardOffset",false);p.Launch(direction);
        }
        static IEnumerator Record(string name,Func<bool> active,Action step)
        {
            string dir=Output+"/"+name;Directory.CreateDirectory(dir);foreach(var old in Directory.GetFiles(dir,"*.png"))File.Delete(old);
            File.WriteAllText(dir+"/times.csv","frame,game_seconds\n");int oldRate=Time.captureFramerate;Time.captureFramerate=15;float start=Time.time,timeout=Time.realtimeSinceStartup+180;int frame=0;
            do{yield return null;step?.Invoke();Frame(name+"/"+frame.ToString("D4"));File.AppendAllText(dir+"/times.csv",frame+","+(Time.time-start).ToString("F4",CultureInfo.InvariantCulture)+"\n");frame++;}while(active()&&Time.realtimeSinceStartup<timeout);
            Time.captureFramerate=oldRate;Check(!active(),"recorded event and squash finish within timeout");
        }
        static void Frame(string name)
        {
            var camera=Camera.main;var player=OralStageRuntime.Instance?.Level.PlayerCharacter;if(camera==null||player==null)return;
            var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();var orders=overlays.Select(c=>c.sortingOrder).ToArray();var flags=overlays.Select(c=>c.overrideSorting).ToArray();
            var target=camera.targetTexture;var active=RenderTexture.active;var position=camera.transform.position;float zoom=camera.orthographicSize;var rt=RenderTexture.GetTemporary(960,540,24);Texture2D image=null;
            try{
                var focus=captureFocus??(player.Position+Vector2.up*1.2f);camera.transform.position=new Vector3(focus.x,focus.y,position.z);camera.orthographicSize=shotSize;
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
