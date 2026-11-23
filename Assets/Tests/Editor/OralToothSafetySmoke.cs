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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    /// <summary>Real play-mode integration checks and deterministic live-camera event recordings.</summary>
    [InitializeOnLoad]
    public static class OralToothSafetySmoke
    {
        const string Key = "OralToothSafetySmoke", Output = "Library/OralToothSafetyProof";
        static bool started; static double deadline; static int checks,deferredFailures;
        static bool CaptureEnabled => Environment.GetCommandLineArgs().Contains("-oralCapture=1");
        static bool captureShot, followPlayer, normalTrial; static Vector2 focus; static float shotSize;
        [Serializable] class Pref { public string key; public bool had; public int value; }
        [Serializable] class Backup { public Pref[] prefs; }
        static OralToothSafetySmoke() { if (SessionState.GetBool(Key, false)) Attach(); }

        public static void Run()
        {
            AssetDatabase.ImportAsset("Assets/Resources/OralMap/Monsters.png",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Output); Restore();
            File.WriteAllText(Output + "/result.txt", "RUNNING");
            File.WriteAllText(Output + "/checks.txt", ""); File.WriteAllText(Output + "/errors.txt", "");
            File.WriteAllText(Output + "/capture-method.txt", "Actual Unity play-mode Camera.Render frames; scripted event triggers, player positioning and test damage. Camera overview deliberately zooms out. PNG sequences are unedited gameplay renders; timestamps are recorded per frame. Capture uses Time.captureFramerate=20 (real runtime/physics updates, no interpolated frames), player-follow framing, and the existing PlayerCombatBars layout refreshed for that camera before Camera.Render.\n");
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
                Restore(); Time.timeScale = 1; Time.captureFramerate = 0; SessionState.SetBool(Key, false);
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
            if (!value) throw new Exception("ORAL_TOOTH_SAFETY_FAIL " + message);
            checks++; File.AppendAllText(Output + "/checks.txt", "PASS " + message + "\n"); Debug.Log("ORAL_TOOTH_SAFETY_PASS " + message);
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
            checks=0;
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;
            GamePreferences.Apply(prefs,false);Application.runInBackground=true;Time.timeScale=1;
            RunScore.Exclude("Stage 0 tooth safety integration QA and actual gameplay capture");
            Silence(level);Set(level.PlayerCharacter,"isInvincible",true);
            level.PlayerCharacter.CollectableCollider.enabled=false;
            Check(StageProgression.Ensure(level).BeginDirectOral(true),"stage-zero real transition begins");
            float timeout=Time.realtimeSinceStartup+120;
            while((OralStageRuntime.Instance==null||StageProgression.Ensure(level).Travelling)&&Time.realtimeSinceStartup<timeout)yield return null;
            var runtime=OralStageRuntime.Instance;var map=FiniteStomachMap.Instance;var player=level.PlayerCharacter;var events=OralFieldEvents.Instance;
            Check(runtime!=null&&runtime.Ready&&map.IsOral,"oral runtime installed and ready");
            Silence(level);events.AutoSchedule=false;
            if (Environment.GetCommandLineArgs().Contains("-oralFrontTraversal"))
            {
                var controls=GamePreferences.Current.Copy();
                controls.keyboard=KeyboardBindingValues.Defaults();
                GamePreferences.Apply(controls,false);
                Clear(level);
                foreach (var cavity in events.Cavities) { cavity.Healed=true; cavity.Active=false; }
                yield return FrontPassages(runtime, map, player, events);
                captureShot=false;
                SessionState.SetBool(Key+"Done",true);
                yield break;
            }
            var layout=level.GetComponent<FieldObjectLayout>();
            Check(layout.Ready&&layout.Slots.Count==20,"initial field contains complete twenty-object layout");
            // Inspect actual instantiated objects before controlled test combat removes monsters.
            Check(layout.SpawnedObjects.Count==20&&layout.SpawnedObjects.All(o=>o!=null&&o.activeInHierarchy),"all twenty field objects were instantiated and active");
            int[] initialCounts={6,2,3,3,4,2};
            for(int kind=0;kind<initialCounts.Length;kind++)Check(layout.Slots.Count(p=>(int)p.kind==kind)==initialCounts[kind],"actual initial field object count for kind "+kind);
            Clear(level);
            Check(events.Cavities.Count==3,"three cavity targets retained");
            foreach(var c in events.Cavities){c.Healed=true;c.Active=false;}
            // Stop only incidental cavity combat for controlled collision/HP observations.
            var center=OralMapGeometry.TongueCenter(map);var radii=OralMapGeometry.TongueRadii(map);
            yield return LayoutSeedChecks(map,center);
            for(int i=0;i<OralMapGeometry.ToothCount;i++)
            {
                var p=OralMapGeometry.ToothWalkCenter(map,i);
                Check(OralMapGeometry.ZoneAt(map,p)==OralZone.Teeth,"flat tooth top is walkable "+i);
                Check(OralMapGeometry.IsJawDanger(map,p),"crown center is in jaw damage region "+i);
                Check(OralMapGeometry.CanJumpToTooth(i),"direct dash allowed at tooth "+i);
                Check(OralMapGeometry.ToothDamagePolygon(map,i).Length>=3,"tooth has distinct damage polygon "+i);
            }
            for(int i=0;i<OralMapGeometry.ToothCount-1;i++)
            {
                var gap=OralMapGeometry.ToothSafeGap(map,i);
                Check(OralMapGeometry.ZoneAt(map,gap)==OralZone.Teeth,"inter-tooth seam remains walkable "+i);
                Check(!events.JawThreatens(gap),"inter-tooth seam is outside every damage region "+i);
                Place(player,gap);yield return new WaitForFixedUpdate();
                Check((player.Position-gap).sqrMagnitude<.12f*.12f,"normal player collision preserves safe seam "+i);
            }
            for(int y=0;y<12;y++)for(int x=0;x<16;x++)
            {
                var safe=OralMapGeometry.Clamp(map,map.UVToWorld(new Vector2(x/15f,y/11f)),.5f);
                Check(OralMapGeometry.Contains(map,safe),"spawn clamp lands on a supported surface "+x+","+y);
            }
            var front=OralMapGeometry.Clamp(map,center+Vector2.up*radii.y,.4f,OralZone.Tongue);
            Check(OralMapGeometry.TryJump(map,front,Vector2.up,out var frontLanding) &&
                OralMapGeometry.ToothKind(OralMapGeometry.NearestTooth(map,frontLanding))==OralToothKind.Incisor,"direct tongue-to-incisor jump accepted");
            Place(player,center);Check(!events.ForceEvent(OralEventKind.Jaw),"jaw does not trigger while player is on tongue");
            var visual=Object.FindObjectOfType<OralMapVisual>();
            Check(!visual.UpperJawVisible,"upper jaw hidden during idle");

            int escapeTooth=1;var escapeStart=OralMapGeometry.ToothWalkCenter(map,escapeTooth);var escapeGap=OralMapGeometry.ToothSafeGap(map,escapeTooth);
            Place(player,escapeStart);Set(player,"isInvincible",false);player.GainHealth(99999);
            float hp=player.CurrentHealth;bool reachedGap=false,sawSeparateWarnings=false,sawUpper=false;
            Focus((escapeStart+escapeGap)*.5f,6.8f);yield return null;
            Check(events.ForceEvent(OralEventKind.Jaw),"jaw starts for escape trial on lower crown");
            yield return Clip("01-gap-escape",5.2f,()=>events.CurrentEvent!=OralEventKind.None,()=>
            {
                sawUpper|=visual.UpperJawVisible;
                if(events.Elapsed>.2f&&events.Elapsed<1.8f)
                {
                    var lines=visual.JawWarningLines;
                    sawSeparateWarnings|=visual.JawWarningCount==OralMapGeometry.ToothCount&&lines.All(l=>l.enabled&&l.loop);
                }
                if(events.Elapsed>.35f)
                {
                    var delta=escapeGap-player.Position;
                    if(delta.magnitude<.12f){player.Move(Vector2.zero);player.GetComponent<Rigidbody2D>().velocity=Vector2.zero;reachedGap=true;}
                    else player.Move(delta.normalized*Mathf.Clamp01(delta.magnitude/.6f));
                }
            });
            player.Move(Vector2.zero);
            Check(sawSeparateWarnings,"fourteen separate closed tooth warnings appear");
            Check(reachedGap&&!events.JawThreatens(player.Position),"actual movement escapes crown into safe tooth seam");
            Check(Mathf.Approximately(player.CurrentHealth,hp),"safe seam prevents real jaw damage");
            Check(sawUpper&&!visual.UpperJawVisible,"upper jaw descends and hides after event");
            Check(events.CurrentEvent==OralEventKind.None,"escape jaw event releases lock");
            yield return Clip("01-gap-escape-idle",.7f,()=>true);

            Place(player,escapeStart);hp=player.CurrentHealth;Focus(escapeStart,6.8f);
            Check(events.ForceEvent(OralEventKind.Jaw),"jaw starts for stationary crown damage trial");
            bool sawClosed=false;
            yield return Clip("02-crown-hit",5.2f,()=>events.CurrentEvent!=OralEventKind.None,()=>{player.Move(Vector2.zero);sawClosed|=events.JawAmount>.98f;});
            Check(player.CurrentHealth<hp&&player.IsAlive,"standing on warned tooth causes actual nonfatal player damage");
            Check(sawClosed,"upper jaw reaches fully closed state");
            Set(player,"isInvincible",true);player.GainHealth(99999);

            Vector2 jumpFrom=default,jumpTo=default,jumpDirection=default;bool found=false;
            for(int j=0;j<360&&!found;j++)
            {
                float a=j*Mathf.Deg2Rad;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                for(float s=.8f;s<1.02f&&!found;s+=.01f)
                {
                    var p=center+Vector2.Scale(dir,radii)*s;
                    if(OralMapGeometry.ZoneAt(map,p)==OralZone.Tongue&&OralMapGeometry.TryJump(map,p,dir,out var end))
                    {jumpFrom=p;jumpTo=end;jumpDirection=dir;found=true;}
                }
            }
            Check(found,"tongue edge has reachable molar jump");
            Check(OralMapGeometry.ZoneAt(map,jumpTo)==OralZone.Teeth,"computed landing lies on a tooth");
            Place(player,jumpFrom);Set(player,"lookDirection",jumpDirection);player.Move(Vector2.zero);Set(player,"currentDashCharges",1);
            Focus((jumpFrom+jumpTo)*.5f,7.5f);yield return null;
            Check(player.TryDash()&&runtime.IsJumping,"normal dash input starts tooth jump");
            yield return Clip("03-jump-to-molar",1.8f,()=>true);
            Check(!runtime.IsJumping&&OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Teeth,"dash lands on flat molar surface");

            yield return FrontPassages(runtime, map, player, events);
            yield return SlideTrial(level,runtime,map,player,false,"04-finale-return");
            yield return SlideTrial(level,runtime,map,player,true,"05-slide-pause-check");
            Place(player,OralMapGeometry.ToothWalkCenter(map,4));yield return null;
            runtime.ReturnToTongueForSwallow();
            Check(runtime.IsSliding,"scripted finale can still return a player before cancellation");
            var sprite=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
            runtime.TeleportPlayer(center,OralZone.Tongue);player.Move(Vector2.zero);yield return null;
            Check(!runtime.IsSliding&&runtime.SlideVisualHeight==0,"teleport cancels slide and hop height");
            Check(OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Tongue,"cancelled slide leaves actor on valid tongue surface");

            Check(StageProgression.Ensure(level).BeginOralToStomach(),"stage exit begins after repeated jump slide and jaw events");
            timeout=Time.realtimeSinceStartup+120;
            while(StageProgression.Ensure(level).Travelling&&Time.realtimeSinceStartup<timeout)yield return null;
            Check(OralStageRuntime.Instance==null&&OralFieldEvents.Instance==null&&!map.IsOral,"stage exit removes all oral runtime state");
            Check(player.IsAlive&&level.PlayerCharacter==player,"stage exit retains original living player");
            captureShot=false;SessionState.SetBool(Key+"Done",true);
        }
        static IEnumerator LayoutSeedChecks(FiniteStomachMap map,Vector2 start)
        {
            int[] counts={6,2,3,3,4,2};
            var seeds=new[]{341562277,0,1,int.MaxValue}.Concat(Enumerable.Range(0,124).Select(i=>unchecked(1732050807+i*104729))).ToArray();
            foreach(int seed in seeds)
            {
                var plan=FieldObjectLayout.Generate(map,start,seed);
                Check(plan.Count==20,"complete twenty-prop oral layout for seed "+seed);
                for(int kind=0;kind<counts.Length;kind++)Check(plan.Count(p=>(int)p.kind==kind)==counts[kind],"required prop count "+kind+" for seed "+seed);
                Check(plan.All(p=>OralMapGeometry.ZoneAt(map,p.position)==OralZone.Tongue&&map.FloorClearance(p.position)>=1.499f&&Vector2.Distance(p.position,start)>=2.999f),"all props stay on safe tongue floor away from spawn for seed "+seed);
                bool separated=true;
                for(int i=0;i<plan.Count;i++)for(int j=0;j<i;j++)
                {
                    var a=plan[i];var b=plan[j];bool pair=a.partner==j||b.partner==i;
                    float gap=pair?4:8;
                    if(!pair&&a.kind==b.kind)gap=a.kind==FieldObjectLayout.Kind.BloodClot?16:a.kind==FieldObjectLayout.Kind.Merchant||a.kind==FieldObjectLayout.Kind.EliteAltar?13:8;
                    if(pair&&(a.kind==FieldObjectLayout.Kind.BloodClot||a.kind==FieldObjectLayout.Kind.EliteAltar||b.kind==FieldObjectLayout.Kind.BloodClot||b.kind==FieldObjectLayout.Kind.EliteAltar))gap=6;
                    separated&=Vector2.Distance(a.position,b.position)>=gap*.55f-.001f;
                }
                Check(separated,"all original per-kind prop separation constraints retained for seed "+seed);
                if(seed==341562277)
                {
                    var repeated=FieldObjectLayout.Generate(map,start,seed);
                    Check(plan.Zip(repeated,(a,b)=>a.kind==b.kind&&(a.position-b.position).sqrMagnitude<.000001f).All(x=>x),"previously failing seed rerolls deterministically without losing props");
                }
                yield return null;
            }
        }
        static int PickClearFrontRoute(OralStageRuntime runtime,FiniteStomachMap map)
        {
            // Record an unobstructed entrance in this live layout: booths and their
            // interaction prompts should not hide the actor or the mint floor marker.
            var layout=runtime.Level.GetComponent<FieldObjectLayout>();
            var obstacles=layout.SpawnedObjects.Where(o=>o!=null && o.activeInHierarchy).Select(o=>(Vector2)o.transform.position)
                .Concat(Object.FindObjectsOfType<InteractableEventObject>().Select(o=>(Vector2)o.transform.position))
                .Concat(Object.FindObjectsOfType<BloodClotMiniStagePortal>().Select(o=>(Vector2)o.transform.position)).ToArray();
            var routes=new List<(int Tooth,Vector2 Launch,Vector2 Landing)>();
            for(int i=0;i<OralMapGeometry.ToothCount;i++)
                if(OralMapGeometry.TryGetCrossing(map,i,out var launch,out var landing)) routes.Add((i,launch,landing));
            int best=-1;
            float clearance=-1;
            foreach(var route in routes)
            {
                if(OralMapGeometry.ToothKind(route.Tooth)!=OralToothKind.Incisor ||
                    Vector2.Dot((route.Landing-route.Launch).normalized,Vector2.up)<.9999f) continue;
                var start=route.Launch+new Vector2(-.7f,-3.5f);
                if(routes.Any(other=>(other.Launch-start).sqrMagnitude<(route.Launch-start).sqrMagnitude)) continue;
                Vector2 span=route.Landing-start;
                float minimum=float.PositiveInfinity;
                foreach(var obstacle in obstacles)
                {
                    var closest=start+span*Mathf.Clamp01(Vector2.Dot(obstacle-start,span)/span.sqrMagnitude);
                    minimum=Mathf.Min(minimum,Vector2.Distance(closest,obstacle));
                }
                if(minimum>clearance){clearance=minimum;best=route.Tooth;}
            }
            Check(best>=0,"unobstructed camera route selected among the vertical incisor crossings: "+best+
                " (nearest prop "+clearance.ToString("F1",CultureInfo.InvariantCulture)+" units)");
            return best;
        }

        static IEnumerator FrontPassages(OralStageRuntime runtime, FiniteStomachMap map, Character player, OralFieldEvents events)
        {
            // Exercise all normal entry points. In particular the thin front crowns
            // need adequate contact clearance on arrival and a viable reverse jump.
            int frontTooth=PickClearFrontRoute(runtime,map);
            var tongue=OralMapGeometry.TongueCenter(map);
            Check(!OralMapGeometry.TryJump(map,tongue,Vector2.up,out _),"ordinary dash from tongue center cannot cross the chasm");
            for(int tooth=0; tooth<OralMapGeometry.ToothCount; tooth++)
            {
                Check(OralMapGeometry.TryGetCrossing(map,tooth,out var launch,out var landing),"real outbound route to tooth "+tooth);
                Check(OralMapGeometry.NearestTooth(map,landing)==tooth &&
                    OralMapGeometry.TeethDistance(map,landing,out _)>=OralMapGeometry.ToothFootRadius-.005f,
                    "landing has room for the player's feet on tooth "+tooth);
                Check(Vector2.Dot((landing-launch).normalized,KeyboardDirection(landing-launch))>.9999f,
                    "the published route follows a WASD bearing at tooth "+tooth);
                Place(player,launch);
                if(tooth==frontTooth)
                {
                    StartFrontClip(runtime,"gif-outbound",launch,landing);
                    yield return new WaitForSeconds(.35f);
                }
                if(tooth==frontTooth) yield return PressDash(runtime,player,UnityEngine.InputSystem.Key.W,Vector2.up,OralZone.Teeth);
                else yield return DashAcross(runtime,player,landing,OralZone.Teeth,"outbound "+tooth);
                Check(OralMapGeometry.NearestTooth(map,player.Position)==tooth,"actual arrival on requested tooth "+tooth);
                if(tooth==frontTooth)
                {
                    yield return new WaitForSeconds(.35f);
                    EndFrontClip(runtime);
                    StartFrontClip(runtime,"gif-return",launch,landing);
                }

                if(OralMapGeometry.ToothKind(tooth)!=OralToothKind.Molar)
                {
                    var inward=(launch-player.Position).normalized;
                    var start=player.Position;
                    var until=Time.time+1.4f;
                    bool alwaysSupported=true;
                    while(Time.time<until)
                    {
                        player.Move(inward);
                        yield return new WaitForFixedUpdate();
                        alwaysSupported &= !runtime.IsAirborne && OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Teeth;
                    }
                    player.Move(Vector2.zero);
                    Check(alwaysSupported && OralMapGeometry.NearestTooth(map,player.Position)==tooth,
                        "holding toward the tongue cannot walk off the front rim "+tooth);
                    var rim=player.Position;
                    Check(OralMapGeometry.TeethDistance(map,rim,out _)<=.35f && Vector2.Dot(rim-start,inward)>-.035f,
                        "tested the inner lip rather than the middle of the crown "+tooth);
                    yield return new WaitForSeconds(OralStageRuntime.SlideSeconds+.2f);
                    Check(!runtime.IsAirborne && (player.Position-rim).sqrMagnitude<.04f &&
                        OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Teeth,"front rim remains stable after releasing input "+tooth);
                }
                if(tooth==frontTooth) yield return PressDash(runtime,player,UnityEngine.InputSystem.Key.S,Vector2.down,OralZone.Tongue);
                else yield return DashAcross(runtime,player,launch,OralZone.Tongue,"return "+tooth);
                if(tooth==frontTooth)
                {
                    yield return new WaitForSeconds(.35f);
                    EndFrontClip(runtime);
                }
            }
            Check(OralMapGeometry.TryGetCrossing(map,frontTooth,out var gate,out var destination),"front escape route available for spicy warning");
            var across=(destination-gate).normalized;
            var lateral=new Vector2(-across.y,across.x);
            Place(player,gate-across*3.5f+lateral*.7f);
            Check(!OralMapGeometry.TryJump(map,player.Position,destination-player.Position,out _),"approach starts outside the legal dash band");
            Check(events.ForceEvent(OralEventKind.Spicy),"start real spicy warning near front teeth");
            yield return null; yield return null;
            var lines=runtime.GetComponentsInChildren<LineRenderer>();
            var arrows=lines.Where(r=>r.name=="매운맛 · 대피 방향").ToArray();
            var entry=lines.Single(r=>r.name=="매운맛 · 도약 지점");
            var exit=lines.Single(r=>r.name=="매운맛 · 착지 지점");
            Check(entry.enabled && exit.enabled && Vector2.Distance(GateCenter(entry),gate)<.12f &&
                Vector2.Distance(GateCenter(exit),destination)<.12f,"paired mint crescents mark actual tongue launch and front landing");
            Check(ArrowsPointTo(arrows,player.Position,gate),"both warning arrows lead to the launch from outside dash range");
            FrameFrontRoute(gate-across*3.5f,destination);
            Frame("front-01-approach");
            StartFrontClip(runtime,"gif-spicy",gate-across*3.5f,destination);
            yield return new WaitForSeconds(.40f);
            var approachDeadline=Time.time+6f;
            while(Vector2.Distance(player.Position,gate)>.22f && Time.time<approachDeadline)
            {
                var delta=gate-player.Position;
                player.Move(delta.normalized*Mathf.Clamp01(delta.magnitude/.5f));
                yield return new WaitForFixedUpdate();
            }
            player.Move(Vector2.zero);
            player.GetComponent<Rigidbody2D>().velocity=Vector2.zero;
            Check(Vector2.Distance(player.Position,gate)<.35f,"walking the indicated route reaches its dash band");
            yield return null; yield return null;
            var keyDirection=KeyboardDirection(destination-gate);
            Check(OralMapGeometry.TryJump(map,player.Position,keyDirection,out var markedLanding) &&
                OralMapGeometry.NearestTooth(map,markedLanding)==frontTooth && Vector2.Distance(GateCenter(exit),markedLanding)<.12f,
                "offset launch advertises the actual keyboard landing on the front tooth");
            Check(ArrowsPointTo(arrows,player.Position,markedLanding),"both warning arrows face the reachable front tooth in dash range");
            Frame("front-02-ready");
            Set(player,"isInvincible",false);
            player.GainHealth(9999);
            var hp=player.CurrentHealth;
            yield return DashAcross(runtime,player,destination,OralZone.Teeth,"spicy front escape");
            yield return null;
            Check(arrows.All(a=>!a.enabled) && !entry.enabled && !exit.enabled,"escape guidance clears on safe front arrival");
            while(events.Tastes.Active && !events.Tastes.SpicyActive) yield return null;
            yield return new WaitForSeconds(1.4f);
            Check(OralMapGeometry.NearestTooth(map,player.Position)==frontTooth && player.CurrentHealth==hp &&
                events.Tastes.BurnTicks==0 && !runtime.IsSliding,"stationary front tooth protects against the real spicy pulse");
            Frame("front-03-safe");
            EndFrontClip(runtime);
            while(events.Tastes.Active) yield return null;
            Set(player,"isInvincible",true);
            // The finale still owns its explicit forced retrieval, independently of normal walking.
            runtime.ReturnToTongueForSwallow();
            Check(runtime.IsSliding,"scripted finale still returns the player from a front tooth");
            yield return new WaitForSeconds(OralStageRuntime.SlideSeconds+.2f);
            Check(!runtime.IsSliding && OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Tongue,"scripted return finishes on the tongue");

            var arc=runtime.GetComponentsInChildren<LineRenderer>()
                .Where(r=>r.name.StartsWith("협곡 점프 안내 ",StringComparison.Ordinal)).OrderBy(r=>r.name).ToArray();
            Check(arc.Length==3,"three preview segments are available");
            Place(player,gate);
            Set(player,"currentDashCharges",1);
            player.Move(keyDirection);
            yield return null; yield return null;
            Check(arc.All(line=>line.enabled),"a charged dash toward a reachable tooth shows its preview");
            player.Move(-keyDirection);
            yield return null; yield return null;
            Check(arc.All(line=>!line.enabled),"looking away from the crossing clears every preview segment");
            Place(player,gate);
            player.Move(keyDirection);
            Set(player,"currentDashCharges",0);
            yield return null; yield return null;
            Check(arc.All(line=>!line.enabled),"preview stays off while the player has no dash charge");
            Set(player,"currentDashCharges",1);
            Check(player.TryDash() && runtime.IsJumping,"begin an actual crossing to verify airborne guidance");
            player.Move(Vector2.zero);
            yield return null; yield return null;
            Check(arc.All(line=>!line.enabled),"preview clears while airborne");
            yield return new WaitForSeconds(.70f);
        }

        static Coroutine frontRecording;
        static int priorFrontCaptureRate;

        static void FrameFrontRoute(Vector2 a,Vector2 b)
        {
            // Leave the middle of the screenshot to the real alert and item belt.
            float size=Mathf.Max(5.8f,Mathf.Abs(a.y-b.y)*.5f+2.2f);
            Focus((a+b)*.5f+Vector2.right*(size*.68f),size);
            followPlayer=false;
        }

        static void StartFrontClip(OralStageRuntime runtime,string name,Vector2 a,Vector2 b)
        {
            if (!Environment.GetCommandLineArgs().Contains("-oralFrontGif")) return;
            var directory=Path.Combine(Output,name);
            Directory.CreateDirectory(directory);
            foreach (string oldFrame in Directory.GetFiles(directory,"*.png")) File.Delete(oldFrame);
            priorFrontCaptureRate=Time.captureFramerate;
            Time.captureFramerate=15;
            FrameFrontRoute(a,b);
            frontRecording=runtime.StartCoroutine(RecordFrontFrames(name));
        }

        static IEnumerator RecordFrontFrames(string name)
        {
            for(int frame=0;;frame++)
            {
                Frame(name+"/"+frame.ToString("D4"));
                yield return null;
            }
        }

        static void EndFrontClip(OralStageRuntime runtime)
        {
            if(frontRecording==null) return;
            runtime.StopCoroutine(frontRecording);
            frontRecording=null;
            Time.captureFramerate=priorFrontCaptureRate;
        }

        static Vector2 GateCenter(LineRenderer ring) =>
            (ring.GetPosition(0)+ring.GetPosition(ring.positionCount-1))*.5f;

        static bool ArrowsPointTo(LineRenderer[] arrows,Vector2 player,Vector2 target)
        {
            var expected=(target-player).normalized;
            return arrows.Length==2 && arrows.All(a=>a.enabled && a.positionCount==3 &&
                Vector2.Dot(((Vector2)a.GetPosition(1)-player).normalized,expected)>.998f);
        }

        static Vector2 KeyboardDirection(Vector2 raw)
        {
            var angle=Mathf.Round(Mathf.Atan2(raw.y,raw.x)/(Mathf.PI*.25f))*Mathf.PI*.25f;
            return new Vector2(Mathf.Round(Mathf.Cos(angle)),Mathf.Round(Mathf.Sin(angle))).normalized;
        }

        static IEnumerator PressDash(OralStageRuntime runtime,Character player,UnityEngine.InputSystem.Key directionKey,Vector2 direction,OralZone goal)
        {
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            view.Focus();
            yield return null;
            var prior=InputSystem.settings;
            var synthetic=Object.Instantiate(prior);
            synthetic.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            synthetic.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings=synthetic;
            var keyboard=InputSystem.AddDevice<Keyboard>("Front tooth crossing keyboard");
            var mouse=Mouse.current;
            var extraMouse=mouse==null ? InputSystem.AddDevice<Mouse>("Front tooth QA mouse") : null;
            try
            {
                var input=player.GetComponent<PlayerInput>();
                Check(input!=null && input.isActiveAndEnabled,"live character owns enabled input component");
                input.SwitchCurrentControlScheme("Keyboard&Mouse",keyboard,mouse!=null?mouse:extraMouse);
                if(!keyboard.enabled) InputSystem.EnableDevice(keyboard);
                keyboard.MakeCurrent();
                var move=input.actions.FindAction("Player/Move",true);
                // Wait for the editor's normal input/PlayerLoop, as for a real
                // keyboard at the booth. No direct Move or TryDash on this path.
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                yield return null; yield return null;
                Set(player,"currentDashCharges",1);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(directionKey));
                for (int frame=0;frame<6 && Vector2.Dot(player.EffectiveMoveDirection,direction)<.95f;frame++) yield return null;
                Check(Vector2.Dot(player.EffectiveMoveDirection,direction)>.95f,
                    $"live direction {directionKey} reaches the character (focus={Application.isFocused}, device={keyboard.enabled}, raw={keyboard[directionKey].isPressed}, map={input.currentActionMap?.name}, scheme={input.currentControlScheme}, playerInput={input.inputIsActive}, move={move.enabled}/{move.ReadValue<Vector2>()}, actor={player.EffectiveMoveDirection})");
                var key=GameInput.ResolveKeyboardKey(GamePreferences.Current.keyboard.Get(GameKeyAction.Dash));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(directionKey,key));
                for(int frame=0;frame<6 && !runtime.IsJumping;frame++)yield return null;
                Check(runtime.IsJumping,"live "+directionKey+" and dash key starts crossing");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                yield return new WaitForSeconds(.70f);
                Check(!runtime.IsAirborne && OralMapGeometry.ZoneAt(runtime.Map,player.Position)==goal,"live keyboard lands on "+goal);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if(extraMouse!=null)InputSystem.RemoveDevice(extraMouse);
                InputSystem.settings=prior;
                Object.Destroy(synthetic);
            }
        }

        static IEnumerator DashAcross(OralStageRuntime runtime,Character player,Vector2 target,OralZone expected,string name)
        {
            var direction=KeyboardDirection(target-player.Position);
            player.Move(direction);
            Set(player,"currentDashCharges",1);
            Check(player.TryDash() && runtime.IsJumping,"manual dash starts a crossing "+name);
            player.Move(Vector2.zero);
            yield return new WaitForSeconds(.70f);
            Check(!runtime.IsAirborne && OralMapGeometry.ZoneAt(runtime.Map,player.Position)==expected,
                "manual dash settles on the intended surface "+name);
        }

        static IEnumerator SlideTrial(LevelManager level,OralStageRuntime runtime,FiniteStomachMap map,Character player,bool pause,string name)
        {
            var tongue=OralMapGeometry.TongueCenter(map);Place(player,OralMapGeometry.ToothWalkCenter(map,4));
            var origin=player.Position;
            Focus(Vector2.Lerp(origin,tongue,.22f),8.7f);yield return null;
            bool sawSlide=false,sawLowSlide=false,sawHop=false,sawLanding=false,paused=false,checkedHopHud=false;
            int priorCaptureRate=Time.captureFramerate;if(CaptureEnabled)Time.captureFramerate=20;
            float started=Time.realtimeSinceStartup,finish=0;
            string dir=Output+"/"+name;Directory.CreateDirectory(dir);File.WriteAllText(dir+"/times.csv","frame,game_seconds\n");
            int frame=0;float nextFrame=0,gameStart=Time.time;
            runtime.ReturnToTongueForSwallow();
            do
            {
                player.Move(Vector2.zero);
                if(runtime.IsSliding)
                {
                    sawSlide=true;
                    if(runtime.SlideProgress>.1f&&runtime.SlideProgress<.5f)sawLowSlide|=runtime.SlideVisualHeight<.08f;
                    sawHop|=runtime.SlideVisualHeight>.2f;
                    if(!checkedHopHud&&runtime.SlideVisualHeight>.6f){CheckCombatBarsFollowSprite(player);checkedHopHud=true;}
                    Check(!player.TryDash(),"dash cannot interrupt automatic slide "+name+" "+frame);
                    if(pause&&!paused&&runtime.SlideProgress>.15f)
                    {
                        paused=true;level.SetRunFlowPaused(true);float progress=runtime.SlideProgress;var p=player.Position;
                        yield return new WaitForSecondsRealtime(.4f);
                        Check(Mathf.Abs(runtime.SlideProgress-progress)<.005f&&(player.Position-p).sqrMagnitude<.001f,"slide freezes during gameplay pause");
                        level.SetRunFlowPaused(false);
                    }
                }
                else if(sawSlide&&!sawLanding){sawLanding=true;finish=Time.time;}
                yield return null;
                if(Time.time>=nextFrame&&CaptureEnabled)
                {
                    Frame(name+"/"+frame.ToString("D4"));File.AppendAllText(dir+"/times.csv",frame+","+(Time.time-gameStart).ToString("F4",CultureInfo.InvariantCulture)+"\n");frame++;nextFrame=Time.time+.045f;
                }
            }while(Time.realtimeSinceStartup-started<45&&(!sawLanding||Time.time-finish<.5f));
            Time.captureFramerate=priorCaptureRate;
            player.Move(Vector2.zero);
            Check(sawSlide,"scripted swallow return starts from canine "+name);
            Check(sawLowSlide&&sawHop,"low continuous slide is followed by visible small hop "+name);
            Check(sawLanding&&!runtime.IsSliding&&runtime.SlideVisualHeight==0,"slide and hop finish with restored visual offset "+name);
            Check(checkedHopHud,"visible sprite and combat bar separation checked during hop peak "+name);
            CheckCombatBarsFollowSprite(player);
            Check(OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Tongue,"slide lands on tongue "+name);
            Check(Vector2.Distance(origin,player.Position)>2,"slide covers actual world distance "+name);
            if(pause)Check(paused,"pause test executed during slide");
        }
        static void CheckCombatBarsFollowSprite(Character player)
        {
            var bars=player.GetComponent<PlayerCombatBars>();
            if(bars==null)bars=player.GetComponentInChildren<PlayerCombatBars>();
            Check(bars!=null&&bars.HealthRect!=null&&bars.DashRect!=null,"live player combat bars exist");
            typeof(PlayerCombatBars).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bars,null);
            var renderer=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
            var bounds=(Bounds)typeof(PlayerCombatBars).GetMethod("VisibleBounds",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{renderer.sprite});
            var surface=(RectTransform)bars.HealthRect.parent;
            float top=float.NegativeInfinity,bottom=float.PositiveInfinity;
            foreach(var p in new[]{new Vector3(bounds.min.x,bounds.min.y),new Vector3(bounds.max.x,bounds.min.y),new Vector3(bounds.min.x,bounds.max.y),new Vector3(bounds.max.x,bounds.max.y)})
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,Camera.main.WorldToScreenPoint(renderer.transform.TransformPoint(p)),null,out var screen);
                top=Mathf.Max(top,screen.y);bottom=Mathf.Min(bottom,screen.y);
            }
            Check(bars.HealthRect.anchoredPosition.y-bars.HealthRect.rect.height*.5f>top+.25f,"health bar remains above opaque character pixels during travel");
            Check(bars.DashRect.anchoredPosition.y+bars.DashRect.rect.height*.5f<bottom-.25f,"dash bar remains below opaque character pixels during travel");
        }
        static void Focus(Vector2 at, float size) { focus = at; shotSize = size; captureShot = true; followPlayer=true; }
        static IEnumerator Clip(string name,float limit,Func<bool> keepGoing,Action step=null)
        {
            string dir=Output+"/"+name;Directory.CreateDirectory(dir);File.WriteAllText(dir+"/times.csv","frame,game_seconds\n");
            foreach(var old in Directory.GetFiles(dir,"*.png"))File.Delete(old);
            int priorCaptureRate=Time.captureFramerate;if(CaptureEnabled)Time.captureFramerate=20;
            float began=Time.time,next=began,deadline=Time.realtimeSinceStartup+limit*10+20;int frame=0;
            do {
                step?.Invoke();yield return null;
                if(Time.time>=next) {
                    if(CaptureEnabled||frame==0) {
                        Frame(name+"/"+frame.ToString("D4"));
                        File.AppendAllText(dir+"/times.csv",frame+","+(Time.time-began).ToString("F4",CultureInfo.InvariantCulture)+"\n");
                    }
                    frame++;next=Time.time+.045f;
                }
            } while(keepGoing()&&Time.time-began<limit&&Time.realtimeSinceStartup<deadline);
            Time.captureFramerate=priorCaptureRate;
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
                foreach(var bars in Object.FindObjectsOfType<PlayerCombatBars>())typeof(PlayerCombatBars).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bars,null);
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
