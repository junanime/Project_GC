#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static partial class OralCeilingLemonSmoke
    {
        static IEnumerator CheckBoothEncounters(LevelManager level, OralFieldEvents events)
        {
            var oral = OralStageRuntime.Instance;
            var player = level.PlayerCharacter;
            var map = oral.Map;
            Clear(level);
            Set(player, "isInvincible", true);
            var center = OralMapGeometry.TongueCenter(map);
            var route = Enumerable.Range(0, OralMapGeometry.ToothCount)
                .Select(i => OralGuardRoute.TryCreate(map,i,out var r) ? (OralGuardRoute?)r : null)
                .First(r => r.HasValue).Value;
            Place(player,route.Launch);
            Check(events.ForceEvent(OralEventKind.Spicy), "whole tongue evacuation begins");
            yield return null; yield return null;
            var gates = oral.GetComponentsInChildren<LineRenderer>().Where(r => r.name == "매운맛 · 도약 지점" || r.name == "매운맛 · 착지 지점").ToArray();
            Check(gates.Length == 2 && gates.All(r => r.enabled && r.positionCount == 19), "both mint crescents mark a playable crossing");
            GStarUISmoke.Capture(FieldMedia + "/mint-crossing",1280,720);
            player.Move((route.Landing-route.Launch).normalized);
            Set(player,"currentDashCharges",1);
            Check(player.TryDash(),"dashing from marked tongue edge");
            yield return new WaitForSeconds(.9f);
            Check(OralMapGeometry.ZoneAt(map,player.Position) == OralZone.Teeth && gates.All(r => !r.enabled), "safe arrival hides route guidance");
            float safeHealth = player.CurrentHealth;
            float until = Time.realtimeSinceStartup + 40;
            while (events.Tastes.Active && Time.realtimeSinceStartup < until) yield return null;
            Check(!events.Tastes.Active && player.CurrentHealth == safeHealth, "tooth landing is safe through expiration");

            Clear(level); Place(player,center);
            Check(events.ForceEvent(OralEventKind.Ceiling),"start upside-down combat");
            until = Time.realtimeSinceStartup + 15;
            while (events.CeilingPhase != 2 && Time.realtimeSinceStartup < until) yield return null;
            Check(events.CeilingPhase == 2,"palate combat reached");
            foreach (var raw in new[] {Vector2.up,Vector2.down,Vector2.left,Vector2.right})
            {
                player.Move(raw);
                Check(Vector2.Dot(player.EffectiveMoveDirection,-raw) > .99f, "invert movement on palate " + raw);
            }
            player.SetMovementControlsReversed(true);
            Check(player.EffectiveMoveDirection == Vector2.left,"overlapping reversal cannot cancel palate inversion");
            player.SetMovementControlsReversed(false);
            Vector2 start = player.Position;
            player.Move(Vector2.right); Set(player,"currentDashCharges",1);
            Check(player.TryDash(),"directed palate dash starts");
            yield return new WaitForSeconds(.35f);
            Check(player.Position.x < start.x-.1f,"right input dashes left in the live arena");
            GStarUISmoke.Capture(FieldMedia + "/palate",1280,720);
            var bats = events.CeilingMonsters.Where(m => m != null).ToArray();
            until = Time.realtimeSinceStartup + 15;
            while (bats.Any(m=>m!=null && m.HP>0 && m.IsFieldRuntimeSuspended) && Time.realtimeSinceStartup < until) yield return null;
            foreach (var bat in bats) if (bat != null && bat.HP>0) bat.TakeDamage(bat.HP+1000);
            until = Time.realtimeSinceStartup + 15;
            while(events.CurrentEvent != OralEventKind.None && Time.realtimeSinceStartup < until) yield return null;
            Check(events.CurrentEvent == OralEventKind.None,"palate combat ends after all ten defeats");
            player.Move(Vector2.up);
            Check(player.EffectiveMoveDirection == Vector2.up,"ordinary movement returns after the descent");
            player.Move(Vector2.zero);
            yield return CheckCrowdedFreeze(level);
        }

        static IEnumerator CheckSandBackdrop(LevelManager level, string stage)
        {
            var owner = new GameObject("Sand backdrop proof");
            owner.transform.SetParent(level.PlayerCharacter.transform,false);
            var visual=owner.AddComponent<NeuralTimeStopVisual>();
            visual.Prepare(level.PlayerCharacter);
            yield return new WaitForSeconds(.7f);
            visual.Begin(1.5f);
            yield return null; yield return null;
            var sand=visual.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="Time stop sand haze");
            CaptureRim(sand,stage);
            Object.Destroy(owner);
            yield return null;
        }

        // Only the rim mesh changes between the two camera renders. Same scene, same instant.
        static void CaptureRim(MeshRenderer sand, string stage)
        {
            Check(sand.enabled,stage+" time sand renderer visible");
            GStarUISmoke.Capture(FieldMedia+"/sand-"+stage,1280,720);
            sand.enabled=false;
            GStarUISmoke.Capture(FieldMedia+"/sand-"+stage+"-without-rim",1280,720);
            sand.enabled=true;
        }

        static IEnumerator CheckCrowdedFreeze(LevelManager level)
        {
            var player=level.PlayerCharacter; var needle=Object.FindObjectOfType<SyringeDartAbility>();
            var center=OralMapGeometry.TongueCenter(OralStageRuntime.Instance.Map);
            Clear(level); Place(player,center);
            var pool=level.CurrentLevelBlueprint.monsters.Select((g,i)=>new {g,i})
                .First(row=>row.g.monsterBlueprints.Any(b=>b!=null && OralMonsterRoster.IsBlueprint(b)));
            var blueprint=pool.g.monsterBlueprints.First(b=>b!=null && OralMonsterRoster.IsBlueprint(b));
            var crowd=new List<Monster>();
            for(int i=0;i<64;i++)
            {
                float a=i*2.399963f;
                var at=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(1.8f+3.4f*(i%8)/7f);
                var foe=level.EntityManager.SpawnMonster(pool.i,at,blueprint,1000);
                if(foe!=null) crowd.Add(foe);
            }
            Check(crowd.Count==64, "64 normal enemies populate the pressure fixture");
            var shader=Resources.Load<Shader>("AugmentVfx/TimeSand");
            Check(shader!=null && shader.isSupported && !ShaderUtil.ShaderHasError(shader),"sandstorm shader compiles on the active GPU");
            var neural=needle.GetComponent<NeuralBlockController>() ?? needle.gameObject.AddComponent<NeuralBlockController>();
            neural.Configure(120,1.5f,.08f,0,false);
            Time.captureFramerate=0; Time.timeScale=1;
            var summon=typeof(NeuralBlockController).GetMethod("SummonThenBlock",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            neural.StartCoroutine((IEnumerator)summon.Invoke(neural,null));
            var visual=needle.GetComponent<NeuralTimeStopVisual>();
            float deadline=Time.realtimeSinceStartup+8;
            while(!neural.IsBlocking && Time.realtimeSinceStartup<deadline) yield return null;
            yield return null;
            Check(neural.IsBlocking && crowd.Any(m => m.GetComponent<NeuralBlockedMonsterStatus>()?.Active == true),
                "the hourglass freezes actual displayed enemies");
            var sand=visual.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="Time stop sand haze");
            var material=sand.sharedMaterial;
            float phase=material.GetFloat("_Phase");
            Check(sand.enabled && material.GetFloat("_Strength")>.6f && sand.GetComponent<MeshFilter>().sharedMesh.vertexCount==8,
                "sand draws only on the four outside strips");
            var grains=visual.GetComponentsInChildren<ParticleSystem>().Single(ps=>ps.name=="Sand suspended at frozen feet");
            Check(grains.particleCount>0 && grains.particleCount<=36,"visible foot grains stay within the shared particle budget");
            CaptureRim(sand,"oral");
            yield return null;
            Check(Mathf.Approximately(phase,material.GetFloat("_Phase")),"sand stops moving at the same time as enemies");
            while(neural.IsBlocking) yield return null;
            yield return new WaitForSeconds(.24f);
            Check(!visual.Visible && !sand.enabled,"sand clears and enemies resume");
            // The real freeze above validates combat. Isolate its graphics below so both arms
            // continue running identical AI, collisions, damage and keyboard input.
            neural.enabled=false;
            yield return CompareCrowdRendering(level,player,crowd,center,visual);
            Object.Destroy(neural); Clear(level); yield return null;
        }

        const int CrowdFrames = 160;
        sealed class CrowdSample
        {
            public string label;
            public bool effects;
            public readonly double[] frames = new double[CrowdFrames];
            public int dashFrames = -1, flashes, maxFlashes, minFlashes = int.MaxValue, actualHits, focusLoss, appFocusChanges, editorBusy;
            public double dashMs, processorMs, systemBusyPercent = double.NaN;
            public int gcCollections;
            public long managedBytes;
            public bool applicationFocus;
        }

#if UNITY_EDITOR_WIN
        // Win32 FILETIME values are three unsigned 64-bit tick counts. Sample only at each end.
        [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling=true)]
        static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
#endif

        static IEnumerator SettleCrowd(int minimumFrames, double minimumSeconds)
        {
            double start = Time.realtimeSinceStartupAsDouble;
            for (int i=0; i<minimumFrames || Time.realtimeSinceStartupAsDouble-start<minimumSeconds; i++)
                yield return null;
        }

        static void StartCrowdEffect(NeuralTimeStopVisual visual, List<Monster> crowd)
        {
            visual.Begin(1.5f);
            foreach (var monster in crowd) visual.MarkFrozenFeet(monster,1.5f);
        }

        // Keep native capture, PNG encoding, assertions and filesystem work outside timed frames.
        static IEnumerator CompareCrowdRendering(LevelManager level, Character player, List<Monster> crowd, Vector2 center, NeuralTimeStopVisual visual)
        {
            var gameView = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            gameView.Focus();
            var keyboard=InputSystem.AddDevice<Keyboard>("Crowded dash test keyboard");
            var controls=player.GetComponent<PlayerInput>();
            if(controls!=null && controls.user.valid)
                UnityEngine.InputSystem.Users.InputUser.PerformPairingWithDevice(keyboard,controls.user);
            keyboard.MakeCurrent();
            var dashKey=GameInput.ResolveKeyboardKey(GamePreferences.Current.keyboard.Get(GameKeyAction.Dash));
            var runs = new List<CrowdSample>();
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            try
            {
                StartCrowdEffect(visual,crowd);
                for(int j=0;j<16;j++)
                {
                    SyringeAugmentVfx.PlayRadius("Explosion",crowd[j].Position,1.1f);
                    crowd[j].TakePeriodicDamage(1,Vector2.zero);
                }
                // Warm particle pools, text, shaders, camera and audio after the comparison PNGs.
                yield return SettleCrowd(120,4);
                visual.Clear();
                for(int phase=0;phase<5;phase++)
                {
                    gameView.Focus();
                    yield return SettleCrowd(48,1.5);
                    var run=new CrowdSample { label=new[]{"A1","B1","A2","B2","A3"}[phase], effects=(phase%2)==1 };
                    runs.Add(run);
                    Check(level.EntityManager.LivingMonsters.Count==64 && crowd.All(m=>m!=null && m.IsAlive),
                        run.label+" begins with exactly 64 live enemies");
                    Place(player,center);
                    Set(player,"currentHealth",player.MaxHealth);
                    Set(player,"currentDashCharges",1);
                    for(int j=0;j<crowd.Count;j++)
                    {
                        float angle=j*2.399963f;
                        Vector2 at=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(1.8f+3.4f*(j%8)/7f);
                        var body=crowd[j].GetComponent<Rigidbody2D>();
                        if(body!=null){body.position=at;body.velocity=Vector2.zero;}
                        else crowd[j].transform.position=at;
                    }
                    Physics2D.SyncTransforms();
                    yield return null;
                    int beforeDash=player.Skills.DashSequence, queuedFrame=-1;
                    double queuedAt=0;
                    run.applicationFocus=Application.isFocused;
                    int gcBefore=GC.CollectionCount(0);
                    long memoryBefore=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                    var cpuBefore=process.TotalProcessorTime;
#if UNITY_EDITOR_WIN
                    bool systemClock=GetSystemTimes(out long idleBefore,out long kernelBefore,out long userBefore);
#endif
                    double last=Time.realtimeSinceStartupAsDouble;
                    for(int i=0;i<CrowdFrames;i++)
                    {
                        if(i==10 && run.effects)StartCrowdEffect(visual,crowd);
                        if(i%20==0)
                        {
                            int flashes=0;
                            for(int j=0;j<16;j++)
                            {
                                var target=crowd[j];float hp=target.HP;
                                if(run.effects && SyringeAugmentVfx.PlayRadius("Explosion",target.Position,1.1f)!=null) flashes++;
                                target.TakePeriodicDamage(1,Vector2.zero);
                                if(target.HP<hp) run.actualHits++;
                            }
                            run.flashes+=flashes;run.maxFlashes=Mathf.Max(run.maxFlashes,flashes);
                            run.minFlashes=Mathf.Min(run.minFlashes,flashes);
                        }
                        if(i==48)
                        {
                            queuedFrame=Time.frameCount;queuedAt=Time.realtimeSinceStartupAsDouble;
                            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.A,dashKey));
                        }
                        yield return null;
                        double now=Time.realtimeSinceStartupAsDouble;
                        run.frames[i]=(now-last)*1000;last=now;
                        if(EditorWindow.focusedWindow!=gameView)run.focusLoss++;
                        if(Application.isFocused!=run.applicationFocus)run.appFocusChanges++;
                        if(EditorApplication.isCompiling || EditorApplication.isUpdating)run.editorBusy++;
                        if(queuedFrame>=0 && run.dashFrames<0 && player.Skills.DashSequence!=beforeDash)
                        {
                            run.dashFrames=Time.frameCount-queuedFrame;run.dashMs=(now-queuedAt)*1000;
                            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                        }
                    }
                    run.processorMs=(process.TotalProcessorTime-cpuBefore).TotalMilliseconds;
#if UNITY_EDITOR_WIN
                    if(systemClock && GetSystemTimes(out long idleAfter,out long kernelAfter,out long userAfter))
                    {
                        long cpuTicks=kernelAfter-kernelBefore+userAfter-userBefore;
                        if(cpuTicks>0)run.systemBusyPercent=100d*(1d-(idleAfter-idleBefore)/(double)cpuTicks);
                    }
#endif
                    run.managedBytes=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()-memoryBefore;
                    run.gcCollections=GC.CollectionCount(0)-gcBefore;
                    visual.Clear();
                    Check(run.dashFrames>=0 && run.dashFrames<=2,run.label+" dispatched the real dash within two updates");
                    Check(run.actualHits==128 && (!run.effects || run.minFlashes>0 && run.maxFlashes<=4),
                        run.label+" delivered 128 matched hits and respected the shared flash budget");
                    Check(level.EntityManager.LivingMonsters.Count==64 && crowd.All(m=>m!=null && m.IsAlive) && player.IsAlive,
                        run.label+" ended with exactly the same living crowd");
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                visual.Clear();
            }
            string dir="Library/GStarUIQA/"+FieldMedia+"/";
            var raw=new System.Text.StringBuilder("block,frame,wall_ms\n");
            foreach(var run in runs)
                for(int i=0;i<run.frames.Length;i++)raw.AppendFormat(CultureInfo.InvariantCulture,"{0},{1},{2:F3}\n",run.label,i,run.frames[i]);
            File.WriteAllText(dir+"crowd-frames.csv",raw.ToString());
            foreach(bool effects in new[]{false,true})
            {
                var selected=runs.Where(s=>s.effects==effects).ToArray();
                string conditions=string.Format(CultureInfo.InvariantCulture,
                    "Unity Editor {0}, {1}, {2}x{3}, vsync={4}, targetFPS={5}; 64 active enemies in both arms; no frame capture inside timed blocks; A1 B1 A2 B2 A3. GPU times not sampled (project Frame Timing Stats disabled).",
                    Application.unityVersion,SystemInfo.graphicsDeviceName,Screen.width,Screen.height,QualitySettings.vSyncCount,Application.targetFrameRate);
                var lines=new List<string> { conditions };
                foreach(var run in selected)
                    lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} {1}; hits={2}; flashes={3}; dash={4} updates/{5:F2}ms; GameViewFocusLost={6}; appFocused={7}; appFocusTransitions={8}; editorBusy={9}; gc0={10}; managedDeltaKiB={11:F0}; processCPU/wall={12:F2} cores; systemBusy={13:F1}%",
                        run.label,FrameSummary(run.frames),run.actualHits,run.flashes,run.dashFrames,run.dashMs,run.focusLoss,run.applicationFocus,
                        run.appFocusChanges,run.editorBusy,run.gcCollections,run.managedBytes/1024d,run.processorMs/run.frames.Sum(),run.systemBusyPercent));
                lines.Add("Combined "+FrameSummary(selected.SelectMany(s=>s.frames).ToArray()));
                File.WriteAllLines(dir+(effects?"pressure.txt":"baseline.txt"),lines);
            }
            Check(runs.All(s=>s.focusLoss==0 && s.appFocusChanges==0 && s.editorBusy==0 && s.applicationFocus==runs[0].applicationFocus),
                "all compared blocks retained the same Game View, application focus and idle editor compilation state");
        }

        static string FrameSummary(double[] frames)
        {
            var sorted=frames.OrderBy(t=>t).ToArray();
            return string.Format(CultureInfo.InvariantCulture,"{0} frames, p50={1:F2}ms p95={2:F2}ms p99={3:F2}ms max={4:F2}ms, >33.3ms={5}, >50ms={6}",
                sorted.Length,sorted[(sorted.Length-1)/2],sorted[(int)Math.Ceiling(sorted.Length*.95)-1],
                sorted[(int)Math.Ceiling(sorted.Length*.99)-1],sorted[sorted.Length-1],sorted.Count(t=>t>33.3333),sorted.Count(t=>t>50));
        }
    }
}
#endif
