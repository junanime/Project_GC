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
            Check(StageProgression.Ensure(level).BeginDirectOral(),"stage-zero real transition begins");
            float timeout=Time.realtimeSinceStartup+120;
            while((OralStageRuntime.Instance==null||StageProgression.Ensure(level).Travelling)&&Time.realtimeSinceStartup<timeout)yield return null;
            var runtime=OralStageRuntime.Instance;var map=FiniteStomachMap.Instance;var player=level.PlayerCharacter;var events=OralFieldEvents.Instance;
            Check(runtime!=null&&runtime.Ready&&map.IsOral,"oral runtime installed and ready");
            Silence(level);events.AutoSchedule=false;
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
                Check(OralMapGeometry.CanJumpToTooth(i)==(OralMapGeometry.ToothKind(i)==OralToothKind.Molar),"jump allowed only before canine "+i);
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
            Check(!OralMapGeometry.TryJump(map,front,Vector2.up,out _),"direct tongue-to-incisor jump rejected");
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
            Check(OralMapGeometry.CanJumpToTooth(OralMapGeometry.NearestTooth(map,jumpTo)),"computed landing is before canine");
            Place(player,jumpFrom);Set(player,"lookDirection",jumpDirection);player.Move(Vector2.zero);Set(player,"currentDashCharges",1);
            Focus((jumpFrom+jumpTo)*.5f,7.5f);yield return null;
            Check(player.TryDash()&&runtime.IsJumping,"normal dash input starts tooth jump");
            yield return Clip("03-jump-to-molar",1.8f,()=>true);
            Check(!runtime.IsJumping&&OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Teeth,"dash lands on flat molar surface");

            yield return SlideTrial(level,runtime,map,player,false,"04-slide-to-tongue");
            yield return SlideTrial(level,runtime,map,player,true,"05-slide-pause-check");
            Place(player,OralMapGeometry.ToothWalkCenter(map,4));yield return null;
            var inward=(center-player.Position).normalized;player.Move(inward);
            timeout=Time.realtimeSinceStartup+8;
            while(!runtime.IsSliding&&Time.realtimeSinceStartup<timeout){player.Move(inward);yield return null;}
            Check(runtime.IsSliding,"third inner-edge entry begins slide before cancellation");
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
        static IEnumerator SlideTrial(LevelManager level,OralStageRuntime runtime,FiniteStomachMap map,Character player,bool pause,string name)
        {
            var tongue=OralMapGeometry.TongueCenter(map);Place(player,OralMapGeometry.ToothWalkCenter(map,4));
            var origin=player.Position;var inward=(tongue-origin).normalized;
            Focus(Vector2.Lerp(origin,tongue,.22f),8.7f);yield return null;
            bool sawSlide=false,sawLowSlide=false,sawHop=false,sawLanding=false,paused=false,checkedHopHud=false;
            int priorCaptureRate=Time.captureFramerate;if(CaptureEnabled)Time.captureFramerate=20;
            float started=Time.realtimeSinceStartup,finish=0;
            string dir=Output+"/"+name;Directory.CreateDirectory(dir);File.WriteAllText(dir+"/times.csv","frame,game_seconds\n");
            int frame=0;float nextFrame=0,gameStart=Time.time;
            do
            {
                if(!sawSlide)player.Move(inward);else player.Move(Vector2.zero);
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
            Check(sawSlide,"walking past inner canine slope automatically slides "+name);
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
