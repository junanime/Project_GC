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
    /// <summary>Real play-mode integration checks and deterministic live-camera event recordings.</summary>
    [InitializeOnLoad]
    public static class OralStageSmoke
    {
        const string Key = "OralStageSmoke", Output = "Library/OralStageProof";
        static bool started; static double deadline; static int checks,deferredFailures;
        static bool CaptureEnabled => Environment.GetCommandLineArgs().Contains("-oralCapture=1");
        static bool captureShot, followPlayer, normalTrial; static Vector2 focus; static float shotSize;
        [Serializable] class Pref { public string key; public bool had; public int value; }
        [Serializable] class Backup { public Pref[] prefs; }
        static OralStageSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            AssetDatabase.ImportAsset("Assets/Resources/OralMap/Monsters.png",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Output); Restore();
            File.WriteAllText(Output + "/result.txt", "RUNNING");
            File.WriteAllText(Output + "/checks.txt", ""); File.WriteAllText(Output + "/errors.txt", "");
            File.WriteAllText(Output + "/capture-method.txt", "Actual Unity play-mode Camera.Render frames; scripted event triggers, player positioning and test damage. Camera overview deliberately zooms out. PNG sequences are unedited gameplay renders; timestamps are recorded per frame.\n");
            var keys = new[] { "Coins", "LobbySilverCoins" };
            File.WriteAllText(Output + "/prefs.json", JsonUtility.ToJson(new Backup { prefs = keys.Select(k => new Pref { key = k, had = PlayerPrefs.HasKey(k), value = PlayerPrefs.GetInt(k) }).ToArray() }));
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key + "Failed", false);
            CrossSceneData.ClearPendingRunSceneTransfer();
            CrossSceneData.CharacterBlueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems = Array.Empty<MerchantItemBlueprint>();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity"); Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started = false; deadline = EditorApplication.timeSinceStartup + 1800;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string text, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            File.AppendAllText(Output + "/errors.txt", text + "\n" + stack + "\n"); SessionState.SetBool(Key + "Failed", true);
        }
        static void Restore()
        {
            var path = Output + "/prefs.json"; if (!File.Exists(path)) return;
            foreach (var p in JsonUtility.FromJson<Backup>(File.ReadAllText(path)).prefs)
                if (p.had) PlayerPrefs.SetInt(p.key, p.value); else PlayerPrefs.DeleteKey(p.key);
            PlayerPrefs.Save(); File.Delete(path);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) { File.AppendAllText(Output + "/errors.txt", "Deadline\n"); SessionState.SetBool(Key + "Failed", true); }
            if (SessionState.GetBool(Key + "Done", false) || SessionState.GetBool(Key + "Failed", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Restore(); Time.timeScale = 1; SessionState.SetBool(Key, false);
                var failed = SessionState.GetBool(Key + "Failed", false);
                File.WriteAllText(Output + "/result.txt", (failed ? "FAIL" : "PASS") + " checks=" + checks); EditorApplication.Exit(failed ? 1 : 0); return;
            }
            if (!EditorApplication.isPlaying) return;
            var guide = Object.FindObjectOfType<TutorialGuide>(); if (guide != null) { guide.Close(); guide.enabled = false; }
            if(normalTrial)
            {
                var choice=Object.FindObjectOfType<AbilitySelectionDialog>();
                if(choice!=null&&choice.MenuOpen){choice.Close();File.AppendAllText(Output+"/checks.txt","INFO normal trial uses existing skip/close on level-up selection\n");}
            }
            var level = Object.FindObjectOfType<LevelManager>();
            if (started || level == null || !level.InitialFieldObjectsReady || level.PlayerCharacter == null) return;
            started = true; level.StartCoroutine(Checks(level));
        }
        static void Check(bool value, string message)
        {
            if (!value) throw new Exception("ORAL_STAGE_FAIL " + message);
            checks++; File.AppendAllText(Output + "/checks.txt", "PASS " + message + "\n"); Debug.Log("ORAL_STAGE_PASS " + message);
        }
        static void RecordCheck(bool value,string message)
        {
            if(value){Check(true,message);return;}
            deferredFailures++;File.AppendAllText(Output+"/errors.txt","ASSERTION FAILED: "+message+"\n");
            File.AppendAllText(Output+"/checks.txt","FAIL "+message+"\n");
        }
        static FieldInfo Field(object obj, string name)
        {
            for (var t = obj.GetType(); t != null; t = t.BaseType)
            { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f; }
            throw new MissingFieldException(obj.GetType().Name, name);
        }
        static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
        static void Place(Character player, Vector2 at)
        {
            var oral = OralStageRuntime.Instance;
            if (oral != null && oral.Ready)
            {
                var zone = OralMapGeometry.ZoneAt(oral.Map, at);
                oral.TeleportPlayer(at, zone == OralZone.Teeth ? OralZone.Teeth : OralZone.Tongue);
                Physics2D.SyncTransforms(); return;
            }
            player.transform.position = at; var rb = player.GetComponent<Rigidbody2D>(); rb.position = at; rb.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }
        static void Silence(LevelManager level)
        {
            level.enabled = false;
            foreach (var b in Object.FindObjectsOfType<MonoBehaviour>())
                if (b is StageEventDirector || b is TimedSpecialMonsterSpawner || b is AcidToadSpawn || b is NPCSpawner || b is SnailBossDebugSpawn)
                { b.enabled = false; b.StopAllCoroutines(); }
            foreach (var a in Object.FindObjectsOfType<Ability>()) { a.enabled = false; a.StopAllCoroutines(); }
        }
        static void Clear(LevelManager level)
        {
            foreach (var m in level.EntityManager.LivingMonsters.ToArray()) if (m != null) m.DespawnForStageTransfer();
            foreach (var p in Object.FindObjectsOfType<Projectile>()) p.RemoveForItem();
            foreach (var c in Object.FindObjectsOfType<Collectable>()) c.DissolveInAcidLake();
        }
        static Monster Spawn(LevelManager level, Vector2 position)
        {
            var groups=level.CurrentLevelBlueprint.monsters;
            for(int i=0;i<groups.Length;i++)
            {
                var bp=groups[i].monsterBlueprints.FirstOrDefault(OralMonsterRoster.IsBlueprint);
                if(bp!=null)return level.EntityManager.SpawnMonster(i,position,bp);
            }
            throw new Exception("No oral monster pool found");
        }
        static IEnumerator Checks(LevelManager level)
        {
            checks = 0; deferredFailures=0; var prefs = GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss = false; prefs.muteOnFocusLoss = false;
            GamePreferences.Apply(prefs, false); Application.runInBackground = true; Time.timeScale = 1;
            RunScore.Exclude("Stage 0 integration and event capture QA");
            Silence(level); Set(level.PlayerCharacter, "isInvincible", true); level.PlayerCharacter.CollectableCollider.enabled = false;
            var prescription = playerPrescription(level);
            var priorPrescription = prescription != null ? JsonUtility.ToJson(prescription.Capture()) : "";
            var priorPrescriptionEnabled = prescription != null && prescription.enabled;
            File.AppendAllText(Output + "/capture-method.txt", "Initial gameplay camera orthographic size=" + Camera.main.orthographicSize + "\n");
            var stageOne = FiniteStomachMap.Instance.WorldRect;
            var stageOneHP=level.CurrentLevelBlueprint.monsters.SelectMany(g=>g.monsterBlueprints).Where(b=>b!=null&&b.GetType()==typeof(MeleeMonsterBlueprint)).Select(b=>b.hp).ToArray();
            Check(StageProgression.Ensure(level).BeginDirectOral(true), "real stage-zero transition starts");
            float until = Time.realtimeSinceStartup + 120;
            while ((OralStageRuntime.Instance == null || StageProgression.Ensure(level).Travelling) && Time.realtimeSinceStartup < until) yield return null;
            Check(OralStageRuntime.Instance != null && StageProgression.Ensure(level).StageNumber == 0, "stage-zero runtime and progression active");
            var initialLayout=level.GetComponent<FieldObjectLayout>();
            Check(initialLayout.Slots.Count(s=>s.kind==FieldObjectLayout.Kind.BloodClot)==3,"three blood clots placed");
            Check(initialLayout.Slots.Count(s=>s.kind==FieldObjectLayout.Kind.EliteAltar)==3,"three elite interactables placed");
            var oralNormals=level.CurrentLevelBlueprint.monsters.SelectMany(g=>g.monsterBlueprints).Where(OralMonsterRoster.IsBlueprint).ToArray();
            for(int i=0;i<oralNormals.Length;i++)Check(Mathf.Abs(oralNormals[i].hp-stageOneHP[Mathf.Min(i,stageOneHP.Length-1)]*.9f)<.001f,"oral normal HP is 90 percent of stage one "+i);
            var eliteProbe=level.EntityManager.SpawnOverchargeElite(level.PlayerCharacter.Position+Vector2.right*4);
            Check(eliteProbe!=null && eliteProbe.Blueprint is EliteMonsterBlueprint eb && OralMonsterRoster.IsBlueprint(eb.sourceNormalBlueprint),"elite uses oral normal source");
            bool specialSpawned=false;
            for(int i=0;i<level.CurrentLevelBlueprint.monsters.Length;i++) {
                var group=level.CurrentLevelBlueprint.monsters[i];
                var sniper=group.monsterBlueprints.FirstOrDefault(b=>b is SniperMonsterBlueprint);
                if(sniper==null)continue;
                var probe=level.EntityManager.SpawnMonster(i,level.PlayerCharacter.Position+Vector2.left*5,sniper,0,false);
                specialSpawned=probe is SniperMonster;break;
            }
            Check(specialSpawned,"stage-one sniper special spawns through the oral stage path");
            Silence(level); Clear(level); var map = FiniteStomachMap.Instance; var runtime = OralStageRuntime.Instance; var player = level.PlayerCharacter;
            var events = OralFieldEvents.Instance; Check(events != null, "six-event director installed"); events.AutoSchedule = false;
            var monsterArt=Resources.Load<Texture2D>("OralMap/Monsters");
            File.AppendAllText(Output+"/checks.txt","INFO monster atlas readable="+(monsterArt!=null&&monsterArt.isReadable)+" filter="+(monsterArt!=null?monsterArt.filterMode.ToString():"missing")+"\n");
            Check(monsterArt!=null&&monsterArt.isReadable&&monsterArt.filterMode==FilterMode.Point,"oral monster atlas is readable and uses point filtering for trimmed sprites");
            if (prescription != null)
            {

                Check(prescription.enabled, "oral stage enables blood-clot prescriptions");
            }
            float ratio = map.WorldRect.width * map.WorldRect.height / (stageOne.width * stageOne.height);
            Check(Mathf.Abs(ratio - 1f / 6f) < .0001f, "map rectangle area is one sixth of stage one: " + ratio.ToString("F6"));
            var center = OralMapGeometry.TongueCenter(map); var radii = OralMapGeometry.TongueRadii(map);
            Check(OralMapGeometry.ZoneAt(map, center) == OralZone.Tongue, "tongue center is walkable");
            var teeth = OralMapGeometry.ToothCenters(map);
            var south=new Vector2(center.x,OralMapGeometry.SouthFloor(map));
            Check(Mathf.Approximately(OralMapGeometry.SouthWallWidth,Resources.Load<StomachMapDefinition>("ColonMap/Stage2").wallWidth),"south wall matches stage two thickness");
            Check(OralMapGeometry.ZoneAt(map,south+Vector2.up*.5f)==OralZone.Tongue,"tongue root reaches south wall face");
            Check(OralMapGeometry.ZoneAt(map,south-Vector2.up*.5f)!=OralZone.Tongue,"south wall is not walkable");
            for(int i=0;i<12;i++)Check(OralMapGeometry.ZoneAt(map,Vector2.Lerp(center,south+Vector2.up*.6f,i/11f))==OralZone.Tongue,"continuous tongue root approach "+i);
            Check(teeth.Count() >= 3, "multiple accessible lower teeth exist");
            Check(Mathf.Approximately(OralMapGeometry.ToothScale,26.4f*2/3),"teeth reduced to two thirds of prior scale");
            Check(Enumerable.Range(0,teeth.Length).Count(i=>OralMapGeometry.ToothKind(i)==OralToothKind.Incisor)==4,"four central incisors");
            Check(Enumerable.Range(0,teeth.Length).Count(i=>OralMapGeometry.ToothKind(i)==OralToothKind.Canine)==2,"paired canines next to incisors");
            for(int i=0;i<teeth.Length;i++) {
                Check(OralMapGeometry.ToothContour(i)!=null,"anatomical tooth contour "+i);
                if(i>0){var seam=OralMapGeometry.ToothSafeGap(map,i-1);Check(OralMapGeometry.TeethDistance(map,seam,out _)>=OralMapGeometry.ToothFootRadius,"continuous flat-crown seam "+i);Check(!OralMapGeometry.IsJawDanger(map,seam),"seam is outside jaw damage "+i);}
            }
            Check(events.Cavities.Count == 3 && events.Cavities.Select(c => c.ToothIndex).Distinct().Count() == 3, "exactly three distinct initial cavities");
            Check(events.Cavities.All(c => OralMapGeometry.ZoneAt(map, c.Position) == OralZone.Teeth), "all treatment targets are on reachable lower teeth");
            int clamped = 0;
            for (int y = 0; y < 24; y++) for (int x = 0; x < 32; x++)
            {
                Vector2 point = map.UVToWorld(new Vector2(x / 31f, y / 23f));
                var safe = OralMapGeometry.Clamp(map, point, .3f); var zone = OralMapGeometry.ZoneAt(map, safe);
                Check(zone == OralZone.Tongue || zone == OralZone.Teeth, "clamp stays on walkable surface " + clamped++);
            }
            Place(player, center); Focus(map.WorldRect.center, map.WorldRect.height * .56f);
            yield return Clip("01-map-overview", 3, () => true);
            CaptureMapDesign(map);
            if(Environment.GetCommandLineArgs().Contains("-oralJawDesign=1")) {
                yield return JawDesignChecks(map,player,events);
                SessionState.SetBool(Key+"Done",true);yield break;
            }
            if(Environment.GetCommandLineArgs().Contains("-oralDesignOnly=1")){SessionState.SetBool(Key+"Done",true);yield break;}
            yield return JumpChecks(player, map, runtime, center, radii);
            Place(player, center); Check(!events.ForceEvent(OralEventKind.Jaw), "jaw refuses to trigger while player is on tongue");
            Place(player, teeth.First()); Focus(player.transform.position, 4.5f); yield return null;
            var crushed = Spawn(level, teeth.First()+Vector2.right*.8f); crushed.enabled = false;
            var crushBody = crushed.GetComponent<Rigidbody2D>(); crushBody.constraints = RigidbodyConstraints2D.FreezeAll;
            crushed.transform.position=teeth.First()+Vector2.right*.8f;crushBody.position=crushed.transform.position;Physics2D.SyncTransforms();
            Check(events.JawThreatens(crushed.Position),"jaw test target is physically inside a tooth crown");
            bool sawCrushedDeath=false;Action<Monster> onCrushedDeath=m=>{if(m!=crushed)return;sawCrushedDeath=true;crushBody.constraints=RigidbodyConstraints2D.FreezeRotation;m.enabled=true;File.AppendAllText(Output+"/checks.txt","INFO real jaw target Died event at "+events.Elapsed+"\n");};
            Monster.Died+=onCrushedDeath;
            File.AppendAllText(Output+"/checks.txt","INFO jaw target "+crushed.Blueprint.name+" pos="+crushed.Position+" HP="+crushed.HP+" barrier="+MonsterNeedleBarrier.Blocks(crushed)+" suspended="+crushed.IsFieldRuntimeSuspended+"\n");
            Set(player, "isInvincible", false); float healthBeforeJaw = player.CurrentHealth;
            Check(events.ForceEvent(OralEventKind.Jaw), "jaw triggers on lower teeth");
            Check(!events.ForceEvent(OralEventKind.Sneeze), "second large event is rejected during jaw");
            yield return EventClip("03-jaw", events, 18);
            Check(player.CurrentHealth < healthBeforeJaw, "jaw deals real player damage");
            Monster.Died-=onCrushedDeath;
            Check(sawCrushedDeath, "jaw crushes a real monster (death event before pooled reference can be reused)");
            if (crushed != null) { crushBody.constraints = RigidbodyConstraints2D.FreezeRotation; crushed.enabled = true; }
            Set(player, "isInvincible", true);
            foreach (var kind in new[] { OralEventKind.Food, OralEventKind.Water, OralEventKind.Coffee, OralEventKind.Soda, OralEventKind.Sneeze })
            {
                Clear(level); Place(player, center); Focus(center, 5); followPlayer = kind != OralEventKind.Food;
                if (kind != OralEventKind.Food) for (int i = 0; i < 5; i++) Spawn(level, center + new Vector2((i - 2) * 1.3f, 3.2f));
                Check(events.ForceEvent(kind), kind + " starts on tongue");
                bool sawCoffeeBefore = false, sawCoffeeAfter = false, sawBubbles = false;
                Vector2 startPosition = player.Position;
                yield return Clip("04-" + kind.ToString().ToLowerInvariant(), 35, () => events.CurrentEvent != OralEventKind.None, () =>
                {
                    if (kind == OralEventKind.Coffee)
                    {
                        if (events.Elapsed > .5f && events.Elapsed < 1.8f) sawCoffeeBefore |= Object.FindObjectsOfType<CoffeeMonsterBuffRuntime>().Length == 0;
                        if (events.Elapsed > 2.5f) sawCoffeeAfter |= Object.FindObjectsOfType<CoffeeMonsterBuffRuntime>().Length > 0;
                    }
                    if (kind == OralEventKind.Soda) sawBubbles |= Object.FindObjectsOfType<MonsterNeedleBarrier>().Any(b=>b.SodaTint&&b.IsProtected);
                });
                Check(events.CurrentEvent == OralEventKind.None, kind + " completes and releases event lock");
                if (kind == OralEventKind.Coffee)
                {
                    File.AppendAllText(Output + "/checks.txt", "INFO coffee before=" + sawCoffeeBefore + " during=" + sawCoffeeAfter + " same-frame remaining=" + Object.FindObjectsOfType<CoffeeMonsterBuffRuntime>().Length + "\n");
                    yield return null; // Unity Destroy executes at the end of the frame.
                    var buffs = Object.FindObjectsOfType<CoffeeMonsterBuffRuntime>();
                    foreach(var b in buffs) File.AppendAllText(Output + "/checks.txt", "INFO remaining coffee initialized=" + Field(b,"initialized").GetValue(b) + " event=" + Field(b,"eventExpireTime").GetValue(b) + " skill=" + Field(b,"skillExpireTime").GetValue(b) + " now=" + Time.time + "\n");
                    Check(sawCoffeeBefore, "coffee does not buff before pour");
                    Check(sawCoffeeAfter, "coffee applies actual buff components after pour");
                    Check(buffs.Length == 0, "coffee removes event buffs after expiry and deferred destruction");
                }
                if (kind == OralEventKind.Soda) Check(sawBubbles, "soda contact grants colored three-hit shields");
                if (kind == OralEventKind.Food) Check(level.EntityManager.LivingMonsters.Count >= 12, "food impact creates actual combat monsters");
                if (kind == OralEventKind.Water || kind == OralEventKind.Sneeze) Check(Vector2.Distance(startPosition, player.Position) > 1 && OralMapGeometry.ZoneAt(map, player.Position) == OralZone.Tongue, kind + " displaces player without chasm fall");
                Check(!runtime.OnCeiling && player.IsAlive, kind + " returns control on lower field");
            }
            yield return TreatmentChecks(level, events, map);
            Clear(level); Place(player, center); Focus(center, 5);
            var lowerMonster=Spawn(level,center+Vector2.left*3);
            var layout=level.GetComponent<FieldObjectLayout>();
            var lowerColliders=layout.SpawnedObjects.Where(p=>p!=null&&p.activeInHierarchy).SelectMany(p=>p.GetComponentsInChildren<Collider2D>(true)).Where(c=>c.GetComponentInParent<Monster>()==null).ToArray();
            var lowerEnabled=lowerColliders.Select(c=>c.enabled).ToArray(); bool sawPropsHidden=false;
            Check(events.ForceEvent(OralEventKind.Ceiling), "tongue flick starts");
            float began = Time.time; bool sawCombat = false, sawProjectile = false, sawLowerHidden = false; float lastHit = -10, combatBegan = -1, combatTimer=0;
            ThrownConsumableBomb lowerBomb=null,roofBomb=null; float lowerAge=-1; Vector2 lowerBombPosition=default; bool sawRoofBombAdvance=false;
            var bombIcon=Object.FindObjectsOfType<InventorySlot>(true).First(s=>s.IsBomb).IconImage.sprite;
            var fullHitbox=player.GetComponentsInChildren<BoxCollider2D>(true).FirstOrDefault(c=>c.gameObject.name=="Full Hitbox");
            Vector2 fullHitboxOffset=fullHitbox!=null?fullHitbox.offset:default;
            Vector2 fullHitboxSize=fullHitbox!=null?fullHitbox.size:default;
            var originalBody=player.GetComponentInChildren<SpriteAnimator>(true).GetComponent<SpriteRenderer>();
            Vector2 hurtboxPivot=fullHitbox!=null?(Vector2)fullHitbox.transform.InverseTransformPoint(originalBody.transform.position):default;
            bool hurtboxOriginalFlip=originalBody.flipY;
            Vector2 combatPlayerStart=default, combatEnemyStart=default; Monster movingEnemy=null;
            var weapon=Object.FindObjectsOfType<ProjectileAbility>().FirstOrDefault(a=>a.Owned);
            Check(weapon!=null,"equipped projectile weapon exists for ceiling combat test");
            level.enabled=true;
            yield return Clip("06-ceiling", 45, () => events.CurrentEvent != OralEventKind.None, () =>
            {
                if(events.CeilingPhase==0&&events.Elapsed>1.1f&&lowerBomb==null)
                    lowerBomb=ThrownConsumableBomb.Launch(player,level.EntityManager,player.Position+Vector2.left*3,1,bombIcon);
                if (events.CeilingPhase == 2)
                {
                    sawCombat = true;
                    if(combatBegan<0)
                    {
                        combatBegan=Time.time; combatTimer=level.CurrentLevelTime; combatPlayerStart=player.Position;
                        movingEnemy=events.CeilingMonsters.FirstOrDefault(m=>m!=null&&m.HP>0); combatEnemyStart=movingEnemy!=null?movingEnemy.Position:default;
                        sawLowerHidden=lowerMonster!=null&&lowerMonster.IsFieldRuntimeSuspended;
                        sawPropsHidden=lowerColliders.All(c=>c==null||!c.gameObject.activeInHierarchy||!c.enabled);
                        foreach(var c in lowerColliders)if(c!=null&&c.enabled&&c.gameObject.activeInHierarchy)File.AppendAllText(Output+"/checks.txt","VISIBLE PROP "+c.name+" parent="+c.transform.root.name+"\n");
                        if(lowerBomb!=null){lowerAge=(float)Field(lowerBomb,"age").GetValue(lowerBomb);lowerBombPosition=lowerBomb.transform.position;}
                        roofBomb=ThrownConsumableBomb.Launch(player,level.EntityManager,player.Position+Vector2.right*2,1,bombIcon);
                        weapon.enabled=true; Set(weapon,"timeSinceLastAttack",100f);
                    }
                    player.Move(Time.time-combatBegan<1.8f?Vector2.right:Vector2.zero);
                    sawProjectile|=Object.FindObjectsOfType<Projectile>().Any(p=>p.gameObject.activeInHierarchy);
                    if(roofBomb!=null)sawRoofBombAdvance|=(float)Field(roofBomb,"age").GetValue(roofBomb)>.1f;
                    if (Time.time-combatBegan>2.8f && Time.time - lastHit > .8f)
                    {
                        if(lastHit<0)
                        {
                            Check(level.IsRunFlowPaused && Mathf.Abs(level.CurrentLevelTime-combatTimer)<.01f,"ceiling combat pauses stage timer");
                            Check(Vector2.Distance(player.Position,combatPlayerStart)>.3f,"player movement works during paused ceiling timer");
                            Check(sawProjectile,"equipped weapon emits actual projectiles during ceiling combat");
                            Check(movingEnemy==null||movingEnemy.HP<=0||Vector2.Distance(movingEnemy.Position,combatEnemyStart)>.05f,"ceiling enemy pursuit remains active");
                            Check(lowerBomb!=null&&lowerBomb.GetComponent<SpriteRenderer>().forceRenderingOff&&Mathf.Abs((float)Field(lowerBomb,"age").GetValue(lowerBomb)-lowerAge)<.001f&&Vector2.Distance(lowerBomb.transform.position,lowerBombPosition)<.001f,"lower bomb hides and freezes at its exact flight frame during ceiling combat");
                            Check(sawRoofBombAdvance,"new ceiling bomb flies while main level timer is paused");
                            var body=player.GetComponentInChildren<SpriteAnimator>(true).GetComponent<SpriteRenderer>();
                            File.AppendAllText(Output+"/checks.txt","INFO roof hurtbox="+(fullHitbox!=null?fullHitbox.bounds.center.ToString():"missing")+" body="+body.bounds.center+" visible="+PromptSpriteGeometry.WorldBounds(body).center+" offset="+(fullHitbox!=null?fullHitbox.offset.ToString():"missing")+"\n");
                            Vector2 expectedOffset=fullHitboxOffset;
                            if(body.flipY!=hurtboxOriginalFlip)expectedOffset.y=2*hurtboxPivot.y-expectedOffset.y;
                            if(fullHitbox!=null)expectedOffset+=(Vector2)fullHitbox.transform.InverseTransformPoint(body.transform.position)-hurtboxPivot;
                            RecordCheck(fullHitbox!=null&&(fullHitbox.offset-expectedOffset).sqrMagnitude<.00001f&&(fullHitbox.size-fullHitboxSize).sqrMagnitude<.00001f&&fullHitbox.bounds.Intersects(PromptSpriteGeometry.WorldBounds(body)),"inverted player hurtbox mirrors its original stable shape and overlaps the visible body");
                            weapon.enabled=false; weapon.StopAllCoroutines();
                        }
                        var target = events.CeilingMonsters.FirstOrDefault(m => m != null && m.HP > 0);
                        if (target != null) target.TakeDamage(999999);
                        lastHit = Time.time;
                    }
                }
            });
            player.Move(Vector2.zero); level.enabled=false; weapon.enabled=false;
            yield return null;
            Check(lowerBomb==null||!lowerBomb.GetComponent<SpriteRenderer>().forceRenderingOff,"lower bomb becomes visible again after ceiling return");
            Check(fullHitbox!=null&&(fullHitbox.offset-fullHitboxOffset).sqrMagnitude<.00001f,"player damage collider offset restores after ceiling return");
            Check(sawCombat, "ceiling actually reaches inverted combat phase");
            Check(sawLowerHidden && lowerMonster!=null && !lowerMonster.IsFieldRuntimeSuspended,"lower-field enemy suspends and restores across ceiling event");
            foreach(var pair in lowerColliders.Select((c,i)=>new{c,i}))if(pair.c!=null&&pair.c.enabled!=lowerEnabled[pair.i])File.AppendAllText(Output+"/checks.txt","RESTORE MISMATCH "+pair.c.name+"\n");
            Check(sawPropsHidden&&lowerColliders.Select((c,i)=>c==null||c.enabled==lowerEnabled[i]).All(v=>v),"lower props disable collision on ceiling and restore exact enabled states");
            Check(events.CurrentEvent == OralEventKind.None && !runtime.OnCeiling, "real event enemy kills release ceiling and return to tongue");
            Check(OralMapGeometry.ZoneAt(map, player.transform.position) == OralZone.Tongue, "ceiling return landing is on tongue");
            Check(player.IsAlive, "player remains alive after all event flows");
            yield return NormalGameplay(level,events,player,center);
            Set(level,"levelTime",320f); int beforeSchedule=level.EntityManager.LivingMonsters.Count;
            var schedule=new LevelBossSchedule();schedule.InitializeLevel(level);
            typeof(LevelBossSchedule).GetMethod("OnTick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(schedule,null);
            Check(Object.FindObjectOfType<BossController>()==null&&Object.FindObjectOfType<BossMonster>()==null&&Object.FindObjectOfType<SnailBossRuntime>()==null&&Object.FindObjectOfType<FoodBossRuntime>()==null,"legacy boss schedule cannot spawn a stomach boss in late oral stage");
            Check(GameAudioManager.Instance==null||Field(GameAudioManager.Instance,"currentOverrideBgm").GetValue(GameAudioManager.Instance).ToString()!="Boss","oral stage does not start legacy boss music");
            Set(level,"levelTime",OralSpecialSpawnProfile.Duration); yield return null; yield return null;
            Check(runtime.ExitReady,"six-minute oral exit becomes available");
            var originalPlayer = player; var originalInventory = level.PlayerInventory; var originalSkills = player.Skills;
            Check(StageProgression.Ensure(level).BeginOralToStomach(), "completed oral stage can return to real stage one");
            until = Time.realtimeSinceStartup + 120;
            while (StageProgression.Ensure(level).Travelling && Time.realtimeSinceStartup < until) yield return null;
            Check(StageProgression.Ensure(level).StageNumber == 1 && !map.IsOral, "stage-one map restored after oral stage");
            Check(OralStageRuntime.Instance == null && OralFieldEvents.Instance == null && level.GetComponent<OralMapVisual>() == null, "oral runtime event and render components cleaned up");
            Check((map.WorldRect.size - stageOne.size).sqrMagnitude < .001f, "stage-one dimensions restored");
            Check(level.PlayerCharacter == originalPlayer && level.PlayerInventory == originalInventory && player.Skills == originalSkills && Mathf.Approximately(player.CurrentHealth, player.MaxHealth) && level.GetComponent<StageGrowthReset>().ResetCount == 1, "stage transition preserves player identity and restores departure health and growth");
            Check(level.InitialFieldObjectsReady && !level.IsRunFlowPaused, "stage-one field and gameplay resume after return");
            Check(prescription==null||prescription.enabled==priorPrescriptionEnabled,"stage-one prescription enabled state restores");
            Check(StageProgression.Ensure(level).BeginDirectOral(),"oral stage can be entered again after teardown");
            until=Time.realtimeSinceStartup+120;
            while(StageProgression.Ensure(level).Travelling&&Time.realtimeSinceStartup<until)yield return null;
            runtime=OralStageRuntime.Instance; events=OralFieldEvents.Instance; events.AutoSchedule=false; Silence(level);
            Place(player,OralMapGeometry.TongueCenter(map));
            Check(events.ForceEvent(OralEventKind.Soda),"event runs before actual death cleanup test");
            Set(player,"isInvincible",false); player.TakeAcidLakeDamage(1,true);
            for(int i=0;i<8;i++)yield return null;
            Check(!player.IsAlive&&events.CurrentEvent==OralEventKind.None&&events.Cavities.Count==0,"actual death cancels event and cavity state");
            if(CaptureEnabled){Directory.CreateDirectory(Output+"/08-death-cleanup");Focus(player.Position,4.6875f);Frame("08-death-cleanup/0000");}
            yield return null;
            captureShot = false; if(deferredFailures>0)SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key + "Done", true);
        }
        static PrescriptionRuntime playerPrescription(LevelManager level)=>level.PlayerCharacter.GetComponent<PrescriptionRuntime>();
        static IEnumerator NormalGameplay(LevelManager level,OralFieldEvents events,Character player,Vector2 center)
        {
            Clear(level); Place(player,center); player.GainHealth(99999);
            Set(player,"isInvincible",false); player.CollectableCollider.enabled=true;
            foreach(var ability in Object.FindObjectsOfType<Ability>()) if(ability.Owned) ability.enabled=true;
            level.enabled=true; events.AutoSchedule=true; captureShot=false; followPlayer=false; normalTrial=true;
            File.AppendAllText(Output+"/capture-method.txt","07-normal-gameplay: actual default camera, damage enabled, real spawning/attacks, scripted movement inputs for 25 seconds, level-up selections skipped using the existing close action. Not a human balance trial.\n");
            float began=Time.time; float lastDash=Time.time;
            yield return Clip("07-normal-gameplay",25,()=>player.IsAlive,()=>
            {
                float t=Time.time-began; player.Move(new Vector2(Mathf.Cos(t*.6f),Mathf.Sin(t*.6f)));
                if(Time.time-lastDash>4){player.TryDash();lastDash=Time.time;}
            });
            normalTrial=false; player.Move(Vector2.zero); events.AutoSchedule=false; events.CancelAll(); Silence(level); Set(player,"isInvincible",true);
            Check(player.IsAlive,"25-second normal camera gameplay trial survives with real damage enabled");
            Check(level.EntityManager.LivingMonsters.All(m=>m==null||!(m is BossMonster)&&!(m is MiniBossMonster)),"oral field permits ordinary and special monsters but excludes bosses");
        }
        static IEnumerator JumpChecks(Character player, FiniteStomachMap map, OralStageRuntime runtime, Vector2 center, Vector2 radii)
        {
            Vector2 from = default, landing = default, direction = default; bool found = false;
            for (int i = 0; i < 32 && !found; i++)
            {
                float a = Mathf.PI * i / 31f; var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                for (float s = .78f; s < 1.02f && !found; s += .015f)
                {
                    var p = center + Vector2.Scale(dir, radii) * s;
                    if (OralMapGeometry.ZoneAt(map, p) == OralZone.Tongue && OralMapGeometry.TryJump(map, p, dir, out var end))
                    { from = p; landing = end; direction = dir; found = true; }
                }
            }
            Check(found, "tongue edge offers a real chasm jump");
            Check(OralMapGeometry.ZoneAt(map, landing) == OralZone.Teeth, "jump landing belongs to lower teeth");
            Place(player, from); Set(player, "lookDirection", direction); Set(player, "moveDirection", Vector2.zero); Set(player, "currentDashCharges", 1);
            Focus((from + landing) / 2f, 5); yield return null;
            Check(player.TryDash(), "normal player dash input starts chasm jump");
            yield return Clip("02-dash-jump", 2, () => true);
            Check(!runtime.IsJumping && OralMapGeometry.ZoneAt(map, player.transform.position) == OralZone.Teeth, "dash jump completes safely on teeth");
            yield return new WaitForSeconds(player.EffectiveDashRechargeTime + .1f);
            Set(player,"lookDirection",-direction); Set(player,"currentDashCharges",1);
            Check(OralMapGeometry.TryJump(map,player.Position,-direction,out var back)&&OralMapGeometry.ZoneAt(map,back)==OralZone.Tongue,"return jump offers a safe tongue landing");
            Check(player.TryDash(),"normal dash starts teeth-to-tongue return jump");
            yield return new WaitForSeconds(1f);
            Check(!runtime.IsJumping&&OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Tongue,"reverse jump completes safely on tongue");
        }
        static IEnumerator TreatmentChecks(LevelManager level, OralFieldEvents events, FiniteStomachMap map)
        {
            Clear(level); var cavity = events.Cavities.First(); var player = level.PlayerCharacter;
            Place(player, cavity.Position); Focus(cavity.Position, 4.5f); yield return null;
            Check(events.TryStartTreatment(0), "nearby cavity treatment interaction begins");
            yield return new WaitForSeconds(1.5f); float progress = cavity.Progress;
            Check(progress > 0 && progress < 1, "treatment accumulates gradually");
            Place(player, OralMapGeometry.TongueCenter(map)); yield return new WaitForSeconds(.7f);
            Check(Mathf.Abs(cavity.Progress - progress) < .05f, "treatment progress persists and pauses outside radius");
            Place(player, cavity.Position);
            yield return Clip("05-cavity-treatment", 24, () => !cavity.Healed);
            Check(cavity.Healed && !cavity.Active, "treatment heals and closes cavity spawn source");
        }
        static IEnumerator EventClip(string name, OralFieldEvents events, float limit)
        {
            yield return Clip(name, limit, () => events.CurrentEvent != OralEventKind.None);
            Check(events.CurrentEvent == OralEventKind.None, name + " exits cleanly within deadline");
        }
        static void Focus(Vector2 at, float size) { focus = at; shotSize = size; captureShot = true; followPlayer=false; }
        static IEnumerator Clip(string name, float limit, Func<bool> keepGoing, Action step = null)
        {
            string dir = Output + "/" + name; Directory.CreateDirectory(dir); File.WriteAllText(dir + "/times.csv", "frame,game_seconds\n");
            foreach(var old in Directory.GetFiles(dir,"*.png")) File.Delete(old);
            float start = Time.time; int frame = 0;
            do
            {
                step?.Invoke(); yield return null;
                if (CaptureEnabled)
                {
                    Frame(name + "/" + frame.ToString("D4"));
                    File.AppendAllText(dir + "/times.csv", frame + "," + (Time.time - start).ToString("F4", CultureInfo.InvariantCulture) + "\n");
                }
                if(!CaptureEnabled&&(frame==0||frame==24))Frame(name+"/review-"+frame);
                frame++; yield return new WaitForSeconds(1f / 12f);
            } while (keepGoing() && Time.time - start < limit);
        }
        static IEnumerator JawDesignChecks(FiniteStomachMap map,Character player,OralFieldEvents events)
        {
            var visual=Object.FindObjectOfType<OralMapVisual>();
            var warnings=visual.JawWarningLines;
            Check(!visual.UpperJawVisible,"upper jaw hidden during normal play");
            Check(warnings.Length==OralMapGeometry.ToothCount&&warnings.All(r=>!r.enabled),"per-tooth warnings hidden during normal play");
            Place(player,OralMapGeometry.ToothWalkCenter(map,0));
            Check(events.ForceEvent(OralEventKind.Jaw),"real jaw event starts on lower teeth");
            while(events.Elapsed<1)yield return null;yield return null;
            Check(!visual.UpperJawVisible&&warnings.All(r=>r.enabled),"warning appears before hidden upper jaw descends");
            for(int i=0;i<warnings.Length;i++) {
                var polygon=OralMapGeometry.ToothDamagePolygon(map,i);
                Check(warnings[i].loop&&warnings[i].positionCount==polygon.Length,"warning traces one tooth damage boundary "+i);
                for(int j=0;j<polygon.Length;j++)Check(((Vector2)warnings[i].GetPosition(j)-polygon[j]).sqrMagnitude<.000001f,"visible warning matches actual damage boundary "+i+"/"+j);
                if(i<warnings.Length-1)Check(!events.JawThreatens(OralMapGeometry.ToothSafeGap(map,i)),"warning leaves safe seam "+i);
            }
            CaptureMapDesign(map,"02-warning-");
            while(events.Elapsed<2.16f)yield return null;yield return null;
            Check(visual.UpperJawVisible,"upper jaw top-view artwork descends as one attached piece");
            CaptureMapDesign(map,"03-descending-");
            while(events.Elapsed<2.48f)yield return null;yield return null;
            Check(events.JawAmount>.99f,"upper teeth finish closing on lower teeth");
            CaptureMapDesign(map,"04-closed-");
            float deadline=Time.realtimeSinceStartup+10;
            while(events.CurrentEvent!=OralEventKind.None&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;
            Check(events.CurrentEvent==OralEventKind.None&&!visual.UpperJawVisible&&warnings.All(r=>!r.enabled),"upper jaw and warning disappear after release");
        }
        static void CaptureMapDesign(FiniteStomachMap map,string prefix="")
        {
            var camera=Camera.main;var position=camera.transform.position;float zoom=camera.orthographicSize;
            var canvases=Object.FindObjectsOfType<Canvas>().Where(c=>c.enabled).ToArray();
            var target=camera.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1800,1440,24);Texture2D image=null;
            try {
                foreach(var canvas in canvases)canvas.enabled=false;
                camera.targetTexture=rt;
                var centers=new[]{map.WorldRect.center+Vector2.up*3,OralMapGeometry.ArchCenter(map)+Vector2.up*map.WorldRect.height*.355f,new Vector2(map.WorldRect.center.x,OralMapGeometry.SouthFloor(map)+4)};
                var sizes=new[]{40f,18f,22f};var names=new[]{"map-design","front-teeth-detail","tongue-root-detail"};
                for(int i=0;i<centers.Length;i++) {
                    camera.transform.position=new Vector3(centers[i].x,centers[i].y,position.z);camera.orthographicSize=sizes[i];camera.Render();RenderTexture.active=rt;
                    image=new Texture2D(1800,1440,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1800,1440),0,0);image.Apply();File.WriteAllBytes(Output+"/"+prefix+names[i]+".png",image.EncodeToPNG());Object.Destroy(image);image=null;
                }
            } finally {
                foreach(var canvas in canvases)if(canvas!=null)canvas.enabled=true;
                camera.targetTexture=target;camera.transform.position=position;camera.orthographicSize=zoom;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)Object.Destroy(image);
            }
        }
        static void Frame(string name)
        {
            var camera = Camera.main; if (camera == null) throw new Exception("Capture camera missing");
            var overlays = Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras = overlays.Select(c => c.worldCamera).ToArray(); var distances = overlays.Select(c => c.planeDistance).ToArray();
            var orders = overlays.Select(c => c.sortingOrder).ToArray(); var flags = overlays.Select(c => c.overrideSorting).ToArray();
            var target = camera.targetTexture; var active = RenderTexture.active; var position = camera.transform.position; float zoom = camera.orthographicSize;
            var rt = RenderTexture.GetTemporary(1280, 720, 24); Texture2D image = null;
            try
            {
                if (captureShot) { var center=followPlayer&&OralStageRuntime.Instance!=null?(Vector2)OralStageRuntime.Instance.Level.PlayerCharacter.Position:focus; camera.transform.position = new Vector3(center.x, center.y, position.z); camera.orthographicSize = shotSize; }
                camera.targetTexture = rt;
                foreach (var c in overlays) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = camera.nearClipPlane + .1f; c.overrideSorting = true; c.sortingOrder = Mathf.Clamp(32100 + c.sortingOrder, 32100, 32760); }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < overlays.Length; i++) { overlays[i].renderMode = RenderMode.ScreenSpaceOverlay; overlays[i].worldCamera = cameras[i]; overlays[i].planeDistance = distances[i]; overlays[i].sortingOrder = orders[i]; overlays[i].overrideSorting = flags[i]; }
                camera.targetTexture = target; camera.transform.position = position; camera.orthographicSize = zoom; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt); if (image != null) Object.Destroy(image); Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
