using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Comparative Editor frame timings. Game time advances exactly 1/60 s per sample;
    // the stopwatch (not the simulation clock) records the cost of running and rendering it.
    [InitializeOnLoad]
    public static class GStarFieldStressBenchmark
    {
        const string Key = "GStarFieldStress";
        static readonly string[] Args = Environment.GetCommandLineArgs();
        static string Arg(string name, string fallback)
        {
            int i = Array.IndexOf(Args, name);
            return i >= 0 && i + 1 < Args.Length ? Args[i+1] : fallback;
        }
        static int Number(string name, int fallback, int min, int max) =>
            int.TryParse(Arg(name, ""), out int value) ? Mathf.Clamp(value,min,max) : fallback;
        static string Output => Path.GetFullPath(Arg("-perfOutput","Library/GStarFieldStress"));
        static int Stage => SessionState.GetInt(Key+"stage",0);
        static int Seed => Number("-perfSeed",63841,1,int.MaxValue-3);
        static bool allStages => !Args.Contains("-perfFilm") && Arg("-perfStage","all")=="all";
        static bool launched;
        static double timeout;
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;

        static GStarFieldStressBenchmark(){if(SessionState.GetBool(Key,false)) Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(FileFor("status.txt"),"RUNNING");
            File.WriteAllText(FileFor("errors.txt"),"");
            File.WriteAllText(FileFor("summary.csv"),"stage,case,frames,load_ms,median_ms,p95_ms,p99_ms,max_ms,over_33_ms,over_50_ms,gc_median_bytes,gc_p95_bytes,alive_start,alive_end,gems_start,gems_end,needles_peak_probe,compression_fields,echoes_emitted,game_seconds\n");
            SessionState.SetBool(Key,true);
            SessionState.SetInt(Key+"stage",Args.Contains("-perfFilm")?0:Number("-perfStage",0,0,2));
            SessionState.SetString(Key+"isolation",Guid.NewGuid().ToString("N"));
            Enter();
        }
        public static void BuildSoakPlayer()
        {
            string location=Path.GetFullPath(Arg("-perfPlayer","Builds/QA-LatePlay/24tu-late-play.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(location));
            string name=PlayerSettings.productName;
            bool oldDev=EditorUserBuildSettings.development;
            bool frameTiming=PlayerSettings.enableFrameTimingStats;
            try
            {
                PlayerSettings.productName=GStarLatePlaySoak.QAProduct;
                if(Args.Contains("-perfBuildProfile"))PlayerSettings.enableFrameTimingStats=true;
                GStarBuild.PrepareArt();
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                    locationPathName=location,target=BuildTarget.StandaloneWindows64,
                    options=BuildOptions.Development|BuildOptions.CompressWithLz4 });
                Require(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded,"QA player build");
            }
            finally{PlayerSettings.productName=name;PlayerSettings.enableFrameTimingStats=frameTiming;EditorUserBuildSettings.development=oldDev;}
        }
        static string FileFor(string name)=>Path.Combine(Output,name);
        static void Enter()
        {
            SessionState.SetInt(Key+"phase",0);SessionState.SetBool(Key+"done",false);SessionState.SetBool(Key+"failed",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,750);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            launched=false;
            timeout=EditorApplication.timeSinceStartup+300+Math.Max(300,Number("-perfFrames",240,60,216000)/4);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=OnLog;Application.logMessageReceived+=OnLog;
        }
        static void OnLog(string message,string stack,LogType kind)
        {
            if(!SessionState.GetBool(Key,false)|| (kind!=LogType.Error && kind!=LogType.Assert && kind!=LogType.Exception))return;
            File.AppendAllText(FileFor("errors.txt"),message+"\n"+stack+"\n");
            SessionState.SetBool(Key+"failed",true);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if(!SessionState.GetBool(Key,false))return;
            string dir=Path.Combine(Output,"booth-"+SessionState.GetString(Key+"isolation","")+"-"+Stage);
            GStarDemo.TestOverride=true;
            GStarDemo.TestDataDirectory=GStarSession.TestDirectory=GStarLeaderboard.TestDirectory=dir;
            GStarSession.ReloadForTests();GStarLeaderboard.ReloadForTests();
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>timeout && !SessionState.GetBool(Key+"failed",false))
                OnLog("Stress benchmark deadline exceeded at stage "+Stage,"",LogType.Error);
            bool failed=SessionState.GetBool(Key+"failed",false);
            if(failed || SessionState.GetBool(Key+"done",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                if(!failed && allStages && Stage<2){SessionState.SetInt(Key+"stage",Stage+1);Enter();return;}
                SessionState.SetBool(Key,false);File.WriteAllText(FileFor("status.txt"),failed?"FAIL":"PASS");
                EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;EditorApplication.Exit(failed?1:0);
                return;
            }
            if(!EditorApplication.isPlaying || launched)return;
            if(SessionState.GetInt(Key+"phase",0)==0)
            {
                var lobby=ApothecaryUI.Instance;if(lobby==null || lobby.Page!="main")return;
                lobby.Show("prepare");
                var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
                QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Application.runInBackground=true;
                UnityEngine.Random.InitState(Seed+Stage);
                var chosen=lobby.Config.characters.First(c=>OctoberArt.CharacterKey(c)=="Ashi");
                SessionState.SetString(Key+"start",Stopwatch.GetTimestamp().ToString(Inv));
                if(!StageEntryLoading.Begin(chosen,null,Stage))throw new InvalidOperationException("Direct booth entry failed");
                SessionState.SetInt(Key+"phase",1);
                return;
            }
            if(StageEntryLoading.IsLoading || !GStarSession.AttemptStarted)return;
            var level=Object.FindObjectOfType<LevelManager>();
            if(level==null || !level.InitialFieldObjectsReady || level.PlayerCharacter==null || level.IsRunFlowPaused || Time.timeScale<=0)return;
            launched=true;
            double loadMs=(Stopwatch.GetTimestamp()-long.Parse(SessionState.GetString(Key+"start","0"),Inv))*1000d/Stopwatch.Frequency;
            level.StartCoroutine(RunCases(level,loadMs));
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException("Benchmark: "+message);}
        static IEnumerator RunCases(LevelManager level,double loadMs)
        {
            Require(StageProgression.Ensure(level).StageNumber==Stage,"wrong map");
            if(Args.Contains("-perfSoak"))
            {
                yield return GStarLatePlaySoak.Play(level,Stage,Number("-perfSoakSeconds",120,24,540),Seed,Output);
                SessionState.SetBool(Key+"done",true);
                yield break;
            }
            var actor=level.PlayerCharacter;
            ColonContentSmoke.Set(actor,"isInvincible",true);
            actor.CollectableCollider.enabled=false;
            // Keep the controlled crowd count. Monster AI, damage, graphics and the item routines still run.
            level.enabled=false;
            foreach(var behavior in Object.FindObjectsOfType<MonoBehaviour>())
                if(behavior is TimedSpecialMonsterSpawner || behavior is NPCSpawner || behavior is AcidToadSpawn || behavior is StageEventDirector)
                {behavior.enabled=false;behavior.StopAllCoroutines();}
            foreach(var enemy in Object.FindObjectsOfType<Monster>())enemy.DespawnForStageTransfer();
            foreach(var item in Object.FindObjectsOfType<Collectable>())item.DissolveInAcidLake();
            yield return null;
            var map=FiniteStomachMap.Instance;
            var center=Stage==0?OralMapGeometry.TongueCenter(map):Stage==2?map.ClampToFloor(map.UVToWorld(new Vector2(.78f,.72f)),2):actor.Position;
            if(Stage==0)OralStageRuntime.Instance.TeleportPlayer(center,OralZone.Tongue);
            else ColonContentSmoke.Position(actor,center);
            Time.captureFramerate=60;
            var dart=Object.FindObjectOfType<SyringeDartAbility>();Require(dart!=null,"selected character has a live needle ability");
            var cam=Camera.main;Require(cam!=null,"game camera active");
            var opts=Args.Contains("-perfFilm")?"finale":Arg("-perfCase","both");
            File.WriteAllText(FileFor("environment-stage"+Stage+".txt"),"Unity "+Application.unityVersion+"\nGPU "+SystemInfo.graphicsDeviceName+"\nCPU "+SystemInfo.processorType+
                "\nrender 1280x720, real Camera.Render per sample, editor wall time\nseed "+(Seed+Stage)+"\nsimulation FPS "+Time.captureFramerate+"\n");
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);rt.Create();
            var previous=cam.targetTexture;cam.targetTexture=rt;
            try
            {
                if(opts=="fiber")yield return GStarFiberStress.Run(Output,Number("-perfFiberSegments",336,12,720),Number("-perfFiberTicks",180,70,420));
                if(opts=="normal" || opts=="both")yield return Case(level,dart,cam,center,false,loadMs);
                if(opts=="dense" || opts=="both")yield return Case(level,dart,cam,center,true,loadMs);
                if(Stage==0 && (Args.Contains("-perfOralFinale") || opts=="finale"))
                    yield return Case(level,dart,cam,center,true,loadMs,true);
            }
            finally{cam.targetTexture=previous;rt.Release();Object.Destroy(rt);Time.captureFramerate=0;}
            File.AppendAllText(FileFor("progress.txt"),"PASS stage "+Stage+"\n");
            SessionState.SetBool(Key+"done",true);
        }

        static IEnumerator Case(LevelManager level,SyringeDartAbility dart,Camera cam,Vector2 center,bool dense,double loadMs,bool oralFinale=false)
        {
            foreach(var m in Object.FindObjectsOfType<Monster>())m.DespawnForStageTransfer();
            foreach(var gem in Object.FindObjectsOfType<ExpGem>())gem.DissolveInAcidLake();
            foreach(var field in Object.FindObjectsOfType<OrganCompressionField>())Object.Destroy(field.gameObject);
            foreach(var projectile in Object.FindObjectsOfType<Projectile>())projectile.RemoveForItem();
            yield return null;
            var actor=level.PlayerCharacter; var entities=level.EntityManager;
            if(Stage==0)OralStageRuntime.Instance.TeleportPlayer(center,OralZone.Tongue);
            else ColonContentSmoke.Position(actor,center);
            int seed=Seed+Stage;
            UnityEngine.Random.InitState(seed);
            var distribution=new System.Random(seed);
            OralSwallowFinale finale=null;
            if(oralFinale)
            {
                finale=OralSwallowFinale.Instance;
                Require(finale!=null && finale.StartForTesting() && finale.Active && finale.Whirlpools.Count==47,"real 47-whirlpool finale opened");
            }
            int enemies=dense?Number("-perfMonsters",140,8,500):Number("-perfNormalMonsters",48,8,500);
            int gems=dense?Number("-perfGems",600,5,2000):Number("-perfNormalGems",90,5,2000);
            int fields=dense?Number("-perfFields",3,1,12):1;
            var roster=new List<(int Pool,MonsterBlueprint Type)>();
            if(Stage==2)foreach(var entry in ColonMonsterRoster.Active.Normals)roster.Add((ColonMonsterRoster.Active.PoolIndex,entry.blueprint));
            else
            {
                var groups=level.CurrentLevelBlueprint.monsters;
                for(int i=0;i<groups.Length;i++)
                    foreach(var proto in groups[i].monsterBlueprints)
                        if(proto!=null && proto.GetType()==typeof(MeleeMonsterBlueprint) && groups[i].monstersPrefab.GetComponent<Monster>().GetType()==typeof(MeleeMonster))
                            roster.Add((i,proto));
            }
            Require(roster.Count>0,"stage normal monster pool unavailable");
            for(int i=0;i<enemies;i++)
            {
                var entry=roster[i%roster.Count];
                Vector2 at=center+Offset(distribution,i);
                if(oralFinale && i%4==0)
                {
                    var well=finale.Whirlpools[(i/4)%finale.Whirlpools.Count];
                    at=well.Position+Bearing(73f*i)*(well.Radius*.65f);
                }
                var mob=entities.SpawnMonster(entry.Pool,at,entry.Type,0,false);
                Require(mob!=null,"a staged monster could not spawn");
                mob.ScaleHealthCapacity(Mathf.Max(1,100000/Mathf.Max(1,mob.SpawnMaxHealth)));
            }
            int frames=Number("-perfFrames",240,60,216000),warm=Number("-perfWarmup",90,30,3600);
            if(oralFinale)
            {
                // This production phase exists for 20 seconds, then intentionally clears the field.
                frames=Number("-perfFinaleFrames",Math.Min(540,frames),60,1000);
                warm=30;
            }
            for(int i=0;i<gems;i++)
            {
                Vector2 p=center+Offset(distribution,i);
                p=FiniteStomachMap.SafeSpawn(p,.3f,false);
                entities.SpawnExpGem(p,i%5==0?GemType.Blue2:GemType.White1,false);
            }
            for(int i=0;i<fields;i++)
            {
                var field=new GameObject("QA compression field").AddComponent<OrganCompressionField>();
                field.Init(center+Bearing(i*360f/fields)*3,3.4f,(frames+warm+120)/60f,.23f,2,.75f,~0,dart);
            }
            int capturedPeak=0;
            if(oralFinale)
            {
                // Staging the clock avoids timing the warning. Collision, motion and swallowing still
                // run through real Update/LateUpdate from the first survival frame onward.
                ColonContentSmoke.Set(finale,"age",OralSwallowFinale.SurvivalTime+.02f);
                Require(finale.SuppressMonsterSpawns,"finale locks further enemy spawns at the real cutoff");
            }
            if(oralFinale && Args.Contains("-perfFilm"))
            {
                yield return FilmLateOral(level,dart,center,finale);
                yield break;
            }
            string name=oralFinale?"oral-finale":dense?"dense":"normal";
            File.AppendAllText(FileFor("progress.txt"),"Spawned stage "+Stage+" "+name+"\n");
            int volleys=dense?5:2,period=dense?8:16,echoes=0,peakNeedles=0;
            var ticks=new double[frames];var allocations=new long[frames];
            int startMobs=0,startGems=0;
            bool gcAvailable;
            double frequency=1000d/Stopwatch.Frequency;
            using(var profile=GStarFrameMarkerProbe.Prepare(Output,Stage,name,frames))
            using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame"))
            {
                gcAvailable=gc.Valid;
                for(int i=-warm;i<frames;i++)
                {
                    // Outside the clocked interval. Probing the whole scene is expensive by itself.
                    if(i==0){startMobs=entities.LivingMonsters.Count;startGems=Object.FindObjectsOfType<ExpGem>().Length;}
                    if(i%47==0)peakNeedles=Math.Max(peakNeedles,Object.FindObjectsOfType<SyringeProjectile>().Length);
                    if(oralFinale)
                    {
                        capturedPeak=Math.Max(capturedPeak,finale.CapturedMonsterCount);
                        if(i==0 || i==frames/2 || i==frames-1)
                            CaptureField(cam.targetTexture,FileFor("oral-finale-world-"+i.ToString("D4")+".png"));
                        // One uninterrupted survival sample; prevent repeated core hits from ending the QA run.
                        if(actor.CurrentHealth<actor.MaxHealth*.51f)actor.GainHealth(actor.MaxHealth);
                        Require(finale.VorticesActive,"sample stays inside the production survival window");
                    }
                    if(i==0)profile?.Start();
                    long before=Stopwatch.GetTimestamp();
                    Vector2 goal=center+Bearing((i+warm)*.44f)*3.1f;
                    actor.Move((goal-actor.Position).normalized);
                    if((i+warm)%period==0)
                    {
                        for(int j=0;j<volleys;j++) dart.FireItemEcho(Bearing(97f*(i+warm)+j*360f/volleys),.5f);
                        if(i>=0)echoes+=volleys;
                    }
                    cam.Render();
                    yield return null;
                    if(i>=0){ticks[i]=(Stopwatch.GetTimestamp()-before)*frequency;allocations[i]=gcAvailable?gc.LastValue:-1;profile?.FrameCompleted();}
                    if(i%120==0)Require(actor.IsAlive && !level.IsLevelEnded && !level.IsRunFlowPaused && Time.timeScale>0,"play stalled or actor lost");
                }
            }
            actor.Move(Vector2.zero);
            int endMobs=entities.LivingMonsters.Count,endGems=Object.FindObjectsOfType<ExpGem>().Length;
            int activeFields=Object.FindObjectsOfType<OrganCompressionField>().Length;
            bool density=oralFinale?endMobs+finale.SwallowedMonsters>=enemies*.95f:endMobs>=enemies*.95f;
            if(oralFinale)
            {
                Require(capturedPeak>0 && finale.SwallowedMonsters>0 && finale.Whirlpools.Count==47,
                    "vortices captured and swallowed real monsters during live fire");
                File.WriteAllText(FileFor("oral-finale-checks.txt"),"whirlpools="+finale.Whirlpools.Count+"\ncaptured_peak="+capturedPeak+
                    "\nswallowed="+finale.SwallowedMonsters+"\nsurvivors="+endMobs+"\nneedles_peak_probe="+peakNeedles+"\nxp="+endGems+"\n");
            }
            Require(density && endGems>=gems*.92f && activeFields==fields && peakNeedles>0 && echoes>0,"load was not sustained: "+endMobs+" mobs, "+endGems+" gems, "+activeFields+" fields, "+peakNeedles+" needles");
            var csv=new StringBuilder("frame,frame_ms,gc_bytes\n",frames*24);
            for(int i=0;i<frames;i++)csv.Append(i).Append(',').Append(ticks[i].ToString("F3",Inv)).Append(',').Append(allocations[i]).Append('\n');
            File.WriteAllText(FileFor("frames-stage"+Stage+"-"+name+".csv"),csv.ToString());
            Array.Sort(ticks);Array.Sort(allocations);
            int p50=(int)Math.Ceiling(.5*frames)-1,p95=(int)Math.Ceiling(.95*frames)-1,p99=(int)Math.Ceiling(.99*frames)-1;
            var record=new object[]{Stage,name,frames,loadMs,ticks[p50],ticks[p95],ticks[p99],ticks[frames-1],ticks.Count(x=>x>33.333),ticks.Count(x=>x>50),allocations[p50],allocations[p95],startMobs,endMobs,startGems,endGems,peakNeedles,activeFields,echoes,frames/60d};
            File.AppendAllText(FileFor("summary.csv"),string.Join(",",record.Select(x=>x is double val?val.ToString("F3",Inv):x.ToString()))+"\n");
        }
        static IEnumerator FilmLateOral(LevelManager level,SyringeDartAbility dart,Vector2 center,OralSwallowFinale finale)
        {
            // This independent take never contributes to the matched timing CSVs. The production
            // HUD timer shows the corresponding real part of the ten-minute oral stage.
            ColonContentSmoke.Set(level,"levelTime",OralSwallowFinale.StartTime+OralSwallowFinale.SurvivalTime);
            level.enabled=true;
            string localFolder="field-film-"+Guid.NewGuid().ToString("N");
            string source=Path.Combine("Library/GStarUIQA",localFolder);
            string destination=FileFor("oral-finale-frames");
            Directory.CreateDirectory(source);Directory.CreateDirectory(destination);
            int frames=Number("-perfFilmFrames",60,36,84),shots=0,captured=0,firstMobs=level.EntityManager.LivingMonsters.Count;
            var player=level.PlayerCharacter;
            for(int t=0;t<frames*5;t++)
            {
                player.Move((center+Bearing(t*.8f)*2.5f-player.Position).normalized);
                if(t==48)player.Skills.TryActivate();
                if(t>0 && t%87==0)player.TryDash();
                if(t%9==0)for(int j=0;j<5;j++){dart.FireItemEcho(Bearing(t*11+j*72),.8f);shots++;}
                if(player.CurrentHealth<player.MaxHealth*.51f)player.GainHealth(player.MaxHealth);
                yield return null;
                captured=Math.Max(captured,finale.CapturedMonsterCount);
                if(t%5!=0)continue;
                string id=(t/5).ToString("D4");
                GStarUISmoke.Capture(localFolder+"/"+id,960,540);
                File.Copy(Path.Combine(source,id+".png"),Path.Combine(destination,id+".png"),true);
            }
            player.Move(Vector2.zero);
            Require(finale.VorticesActive && finale.Whirlpools.Count==47 && captured>0 && shots>0 && finale.SwallowedMonsters>0,"camera take captured the real collision and combat sequence");
            File.WriteAllText(FileFor("film-checks.txt"),"PASS\nframes="+frames+"\nfps=12\nwidth=960\nheight=540\nwhirlpools="+finale.Whirlpools.Count+
                "\ninitial_monsters="+firstMobs+"\nmax_simultaneous_capture="+captured+"\nswallowed="+finale.SwallowedMonsters+"\nechoes="+shots+"\n");
        }

        static void CaptureField(RenderTexture source,string output)
        {
            var previous=RenderTexture.active;
            var image=new Texture2D(source.width,source.height,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=source;
                image.ReadPixels(new Rect(0,0,source.width,source.height),0,0);
                image.Apply();
                File.WriteAllBytes(output,image.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;Object.Destroy(image);}
        }

        static Vector2 Offset(System.Random random,int index)
        {
            float radius=index%4==0?Mathf.Lerp(14,23,(float)random.NextDouble()):Mathf.Lerp(2.3f,10,(float)random.NextDouble());
            return radius*Bearing((float)random.NextDouble()*360);
        }
        static Vector2 Bearing(float angle)=>new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
    }
}
