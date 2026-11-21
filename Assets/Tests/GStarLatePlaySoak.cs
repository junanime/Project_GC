#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Vampire.Tests
{
    // Drives the live late-game spawning director, pickups, upgrade choices, physics, camera,
    // combat VFX and stage clock. The timing table excludes interactive reward pauses.
    public static class GStarLatePlaySoak
    {
        public const string QAProduct="24TU-GStar-LatePlay-QA";
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        static Vector2 Angle(float angle)=>(Vector2)(Quaternion.Euler(0,0,angle)*Vector2.right);
        static void Check(bool pass,string reason){if(!pass)throw new InvalidOperationException("Late play: "+reason);}
        public static IEnumerator Play(LevelManager level,int stage,int seconds,int seed,string directory)
        {
            Directory.CreateDirectory(directory);
            string file=Path.Combine(directory,"soak-stage"+stage+".csv");
            File.WriteAllText(file,"game_time,stage_time,active_frames,median_ms,p95_ms,p99_ms,alive_mobs,xp_on_floor,needles,ground_fields,kills,level,coins,dashes,echoes,managed_used_mb,total_allocated_mb\n");
            var setup=StageProgression.Ensure(level);
            var player=level.PlayerCharacter;
            var map=FiniteStomachMap.Instance;
            var dart=Object.FindObjectOfType<SyringeDartAbility>();
            var stats=Object.FindObjectOfType<StatsManager>();
            var dialog=level.EntityManager.AbilitySelectionDialog;
            Check(dart!=null && stats!=null && dialog!=null && setup.StageNumber==stage && GStarSession.AttemptStarted && GStarAnalytics.IsRecording,"scored field ready");
            var options=Environment.GetCommandLineArgs();
            bool forceFiber=options.Contains("-qaFiberEquipped");
            bool countPairs=options.Contains("-qaContactPairs");
            int fiberPeak=0;
            if(forceFiber)
            {
                var manager=Object.FindObjectOfType<AbilityManager>();
                var fiber=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true)
                    .First(a=>a.Type==SyringeSpecialAugmentAbility.SpecialAugmentType.FiberNeedle);
                Check(fiber.Owned || manager.AcquireVer4Ability(fiber),"fiber acquired by the production augment path");
                Check(fiber.Owned && dart.HasFiberNeedleAugment(),"production fiber projectile mode enabled");
                File.WriteAllText(Path.Combine(directory,"soak-stage"+stage+"-fiber.csv"),"seconds,segments,attached_bodies\n");
            }
            if(countPairs)File.WriteAllText(Path.Combine(directory,"soak-stage"+stage+"-pairs.csv"),
                "seconds,probe_count,method,category_a,category_b,unique_pairs\n");
            // Preserve real stage events, spawning and their pacing. Begin near the end of the field,
            // leaving the timed final encounter to the dedicated 47-vortex case.
            float endLimit=stage==0?OralSwallowFinale.StartTime-8:Mathf.Min(875,level.LevelDuration-12);
            float targetTime=Mathf.Clamp(seconds,24,Mathf.Max(24,endLimit-5));
            float start=Mathf.Max(2,endLimit-targetTime);
            typeof(LevelManager).GetField("levelTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(level,start);
            // No run is lost to unattended hero movement. Terrain effects and ranking systems
            // continue to execute and the actor stays in the real contact/attack loop.
            var invulnerability=typeof(Character).GetField("isInvincible",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            if(invulnerability==null)for(var t=typeof(Character).BaseType;t!=null && invulnerability==null;t=t.BaseType)
                invulnerability=t.GetField("isInvincible",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            Check(invulnerability!=null,"hero survival fixture");invulnerability.SetValue(player,true);
            Vector2 origin=stage==0?OralMapGeometry.TongueCenter(map):stage==2?map.ClampToFloor(map.UVToWorld(new Vector2(.78f,.72f)),2):player.Position;
            if(stage==0)OralStageRuntime.Instance.TeleportPlayer(origin,OralZone.Tongue);
            else{var body=player.GetComponent<Rigidbody2D>();body.position=origin;player.transform.position=origin;body.velocity=Vector2.zero;Physics2D.SyncTransforms();}
            UnityEngine.Random.InitState(seed+stage);
            var random=new System.Random(seed+stage);
            var arr=level.CurrentLevelBlueprint.monsters;
            var roster=new List<(int index,MonsterBlueprint type)>();
            if(stage==2)foreach(var entry in ColonMonsterRoster.Active.Normals)roster.Add((ColonMonsterRoster.Active.PoolIndex,entry.blueprint));
            else for(int i=0;i<arr.Length;i++)if(arr[i].monstersPrefab.GetComponent<Monster>().GetType()==typeof(MeleeMonster))
                foreach(var b in arr[i].monsterBlueprints)if(b!=null && b.GetType()==typeof(MeleeMonsterBlueprint))roster.Add((i,b));
            Check(roster.Count>0,"monster roster");
            for(int i=0;i<64;i++)
            {
                var enemy=roster[i%roster.Count];
                level.EntityManager.SpawnMonster(enemy.index,origin+Angle((float)random.NextDouble()*360)*(3+(float)random.NextDouble()*16),enemy.type,0,false);
            }
            for(int i=0;i<170;i++)
            {
                var point=origin+Angle((float)random.NextDouble()*360)*Mathf.Lerp(2,16,(float)random.NextDouble());
                level.EntityManager.SpawnExpGem(FiniteStomachMap.SafeSpawn(point,.3f),i%4==0?GemType.Blue2:GemType.White1,false);
            }
            Time.captureFramerate=0;Time.timeScale=1;Application.runInBackground=true;
            int killsAtStart=stats.MonstersKilled,levelAtStart=player.CurrentLevel,dashes=0,shots=0,skillUses=0,rewards=0,extraGemPulses=0;
            int maxCrowd=level.EntityManager.LivingMonsters.Count,needlePeak=0,sampleTotal=0;
            float nextShot=0,nextSkill=1,nextDash=3,nextGem=2,nextVfx=0,nextReport=8;
            int initialNaturalSpawns=level.NormalMonstersSpawned;
            string runId=GStarSession.RunId;
            long last=0;double timeout=Time.realtimeSinceStartupAsDouble+Math.Max(240,targetTime*7);
            var samples=new List<double>(5000);
            var chooserDelay=0d;
            double pauseStart=-1;bool loggedPause=false;
            float furthest=0;
            bool priorActive=false;
            int startDash=player.Skills.DashSequence;
            using(var marker=GStarFrameMarkerProbe.Native(directory,stage))
            using(var input=new PlaybackKeyboard(player))
            while(true)
            {
                float elapsed=level.CurrentLevelTime-start;
                double now=Time.realtimeSinceStartupAsDouble;
                Check(now<timeout,"live stage or a UI pause stalled");
                Check(setup.StageNumber==stage && player.IsAlive && !level.IsLevelEnded && GStarSession.RunId==runId,"same live player and scored run");
                bool stopped=Time.timeScale<=0 || level.IsRunFlowPaused || dialog.MenuOpen || ApothecaryUI.Instance.Page=="skillReward";
                marker?.SetPaused(stopped);
                if(stopped)
                {
                    last=0;
                    if(pauseStart<0)pauseStart=now;
                    if(!loggedPause && now-pauseStart>4)
                    {
                        var openPage=ApothecaryUI.Instance.Page;
                        File.AppendAllText(Path.Combine(directory,"soak-stage"+stage+"-pauses.txt"),"at "+elapsed.ToString("F1",Inv)+
                            "s page="+openPage+" scale="+Time.timeScale+" reward="+dialog.MenuOpen+" selecting="+dialog.SelectionAnimating+
                            " cutin="+player.Skills.IsCutin+" cards="+dialog.GetComponentsInChildren<AbilityCard>().Length+"\n");
                        loggedPause=true;
                    }
                    Check(now-pauseStart<45,"unresolved UI prompt: "+ApothecaryUI.Instance.Page);
                    bool choose=false;
                    if(now>chooserDelay)
                    {
                        if(dialog.MenuOpen && !dialog.SelectionAnimating)
                        {
                            choose=dialog.GetComponentsInChildren<AbilityCard>().Length>0;
                            if(choose)rewards++;
                        }
                        else if(ApothecaryUI.Instance.Page=="skillReward")
                        {
                            var button=ApothecaryUI.Instance.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.isActiveAndEnabled && b.IsInteractable());
                            if(button!=null){button.onClick.Invoke();rewards++;}
                        }
                        chooserDelay=now+.33;
                    }
                    input.Set(Vector2.zero,false,false,choose);
                    yield return null;continue;
                }
                pauseStart=-1;loggedPause=false;
                furthest=Mathf.Max(furthest,Vector2.Distance(origin,player.Position));
                dashes=player.Skills.DashSequence-startDash;
                bool active=player.Skills.Active;
                if(active && !priorActive)skillUses++;
                priorActive=active;
                long next=Stopwatch.GetTimestamp();
                if(last>0)samples.Add((next-last)*1000d/Stopwatch.Frequency);
                last=next;
                marker?.NativeFrame();
                if(elapsed>=nextReport || elapsed>=targetTime)
                {
                    int alive=level.EntityManager.LivingMonsters.Count,needles=Object.FindObjectsOfType<SyringeProjectile>().Length;
                    maxCrowd=Math.Max(maxCrowd,alive);needlePeak=Math.Max(needlePeak,needles);
                    marker?.WriteInterval(Math.Min((int)Math.Ceiling(elapsed),(int)nextReport));
                    if(forceFiber)
                    {
                        var activeFiber=Object.FindObjectsOfType<FiberTrailSegment>();
                        fiberPeak=Math.Max(fiberPeak,activeFiber.Length);
                        var bodies=new HashSet<Rigidbody2D>();
                        foreach(var segment in activeFiber)
                        {
                            var collider=segment.GetComponent<Collider2D>();
                            if(collider!=null && collider.attachedRigidbody!=null)bodies.Add(collider.attachedRigidbody);
                        }
                        File.AppendAllText(Path.Combine(directory,"soak-stage"+stage+"-fiber.csv"),
                            FormattableString.Invariant($"{elapsed:F2},{activeFiber.Length},{bodies.Count}\n"));
                    }
                    if(countPairs && (nextReport==48 || nextReport==64 || nextReport==88 || nextReport==112 || elapsed>=targetTime))
                        SamplePairs(directory,stage,elapsed);

                    if(samples.Count>0)
                    {
                        samples.Sort();int n=samples.Count;sampleTotal+=n;
                        var row=new object[]{elapsed,level.CurrentLevelTime,n,samples[(n-1)/2],samples[(int)Math.Ceiling(n*.95)-1],samples[(int)Math.Ceiling(n*.99)-1],
                            alive,Object.FindObjectsOfType<ExpGem>().Length,needles,Object.FindObjectsOfType<OrganCompressionField>().Length,stats.MonstersKilled,
                            player.CurrentLevel,stats.CoinsGained,dashes,shots,Profiler.GetMonoUsedSizeLong()/1048576d,Profiler.GetTotalAllocatedMemoryLong()/1048576d};
                        File.AppendAllText(file,string.Join(",",row.Select(v=>Convert.ToString(v,Inv)))+"\n");samples.Clear();
                    }
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
                    if(nextReport==8 || (nextReport<targetTime && nextReport+8>=targetTime))
                        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"native-stage"+stage+"-"+(int)nextReport+"s.png"));
#endif
                    nextReport+=8;last=0;
                    if(elapsed>=targetTime)break;
                }
                Vector2 goal=origin+new Vector2(Mathf.Sin(elapsed*.24f)*5,Mathf.Sin(elapsed*.39f)*3.5f);
                Vector2 direction=(goal-player.Position).normalized;
                if(player.CurrentHealth<player.MaxHealth*.6f)player.GainHealth(player.MaxHealth);
                if(elapsed>=nextShot)
                {
                    for(int j=0;j<5;j++){dart.FireItemEcho(Angle(elapsed*192f+j*72),2.6f);shots++;}
                    nextShot=elapsed+.27f;
                }
                if(elapsed>=nextGem)
                {
                    // Deposit near and far. The normal magnet, leveling and selection windows stay active.
                    level.EntityManager.SpawnExpGem(player.Position,GemType.Green10,false);
                    level.EntityManager.SpawnExpGem(FiniteStomachMap.SafeSpawn(player.Position+Angle(elapsed*39)*5,.4f),GemType.Blue2,false);
                    nextGem=elapsed+4;extraGemPulses++;
                }
                if(elapsed>=nextVfx)
                {
                    var field=new GameObject("Soak · compression").AddComponent<OrganCompressionField>();
                    field.Init(player.Position+Angle(elapsed*61)*2.8f,3.2f,5,.25f,6,.8f,~0,dart);
                    nextVfx=elapsed+2.6f;
                }
                bool dash=elapsed>=nextDash;
                if(dash)nextDash=elapsed+4.8f;
                bool cast=elapsed>=nextSkill;
                if(cast)nextSkill=elapsed+7;
                input.Set(direction,dash,cast,false);
                yield return null;
            }
            player.Move(Vector2.zero);
            int natural=level.NormalMonstersSpawned-initialNaturalSpawns,kills=stats.MonstersKilled-killsAtStart;
            string summary="start_stage_seconds="+start.ToString("F1",Inv)+"\nelapsed_stage_seconds="+(level.CurrentLevelTime-start).ToString("F1",Inv)+
                "\nactive_frame_samples="+sampleTotal+"\npeak_monsters="+maxCrowd+"\nnormal_spawns_from_field_director="+natural+"\nkills="+kills+"\npeak_needle_probe="+needlePeak+
                "\nreal_projectiles_fired="+shots+"\nreal_dash_uses="+dashes+"\nreal_active_skill_uses="+skillUses+"\nextra_xp_drop_pulses="+extraGemPulses+
                "\nmax_patrol_displacement="+furthest.ToString("F2",Inv)+"\ninitial_level="+levelAtStart+"\nfinal_level="+player.CurrentLevel+"\nreal_reward_choices="+rewards+"\n";
            if(forceFiber)
            {
                Check(fiberPeak>0,"actual projectiles left real fiber segments");
                summary+="forced_production_fiber_peak="+fiberPeak+"\n";
            }
            File.WriteAllText(Path.Combine(directory,"soak-stage"+stage+"-checks.txt"),summary);
            Check(sampleTotal>100 && maxCrowd>=40 && natural>0 && kills>0 && shots>50 && dashes>0 && skillUses>0 && furthest>1.4f && extraGemPulses>4 &&
                player.CurrentLevel>levelAtStart && rewards>0 && GStarAnalytics.IsRecording,"live AI, experience pickup, combat, reward choices and uninterrupted analytics: "+summary);
        }
        // Optional diagnostic only. Snapshot a bounded, stratified set of real colliders
        // without installing callbacks or changing their bodies, layers or masks.
        static void SamplePairs(string directory,int stage,float elapsed)
        {
            var tags=new Dictionary<Collider2D,string>();
            var groups=new Dictionary<string,List<Collider2D>>();
            string Tag(Collider2D c)
            {
                if(tags.TryGetValue(c,out var known))return known;
                string name;
                if(c.GetComponentInParent<FiberTrailSegment>()!=null)name="fiber";
                else if(c.GetComponentInParent<ExpGem>()!=null)name="xp";
                else if(c.GetComponentInParent<Coin>()!=null)name="coin";
                else if(c.GetComponentInParent<SyringeProjectile>()!=null)name="needle";
                else if(c.GetComponentInParent<Monster>()!=null)name="monster";
                else if(c.GetComponentInParent<Character>()!=null)name="player";
                else name="other-layer-"+c.gameObject.layer;
                return tags[c]=name;
            }
            foreach(var c in Object.FindObjectsOfType<Collider2D>())
            {
                var label=Tag(c);
                if(label.StartsWith("other"))continue;
                if(!groups.TryGetValue(label,out var group))groups[label]=group=new List<Collider2D>();
                group.Add(c);
            }
            var seen=new[]{new HashSet<ulong>(),new HashSet<ulong>(),new HashSet<ulong>()};
            var counts=new Dictionary<(int Method,string A,string B),int>();
            var list=new List<Collider2D>(128);var filter=new ContactFilter2D().NoFilter();filter.useTriggers=true;
            void Count(Collider2D lhs,Collider2D rhs,int method)
            {
                if(rhs==null || lhs==rhs)return;
                uint x=unchecked((uint)lhs.GetInstanceID()),y=unchecked((uint)rhs.GetInstanceID());
                if(!seen[method].Add(((ulong)Math.Min(x,y)<<32)|Math.Max(x,y)))return;
                string a=Tag(lhs), b=Tag(rhs);
                var k=string.CompareOrdinal(a,b)<=0?(method,a,b):(method,b,a);
                counts.TryGetValue(k,out int n);counts[k]=n+1;
            }
            int total=0;
            foreach(var g in groups)
            {
                var colliders=g.Value;
                int n=Math.Min(colliders.Count,g.Key=="fiber"?32:12);
                for(int i=0;i<n;i++)
                {
                    var col=colliders[(int)((long)i*colliders.Count/n)];
                    total++;list.Clear();col.GetContacts(filter,list);
                    foreach(var other in list)Count(col,other,0);
                    list.Clear();col.OverlapCollider(filter,list);
                    foreach(var other in list)
                    {
                        Count(col,other,1);
                        if(col.IsTouching(other))Count(col,other,2);
                    }
                }
            }
            string[] methodNames={"reported-contacts","shape-overlap","overlap-and-touching"};
            var rows=counts.Select(e=>FormattableString.Invariant($"{elapsed:F2},{total},{methodNames[e.Key.Method]},{e.Key.A},{e.Key.B},{e.Value}"));
            File.AppendAllLines(Path.Combine(directory,"soak-stage"+stage+"-pairs.csv"),rows);
        }

        public sealed class PlaybackKeyboard : IDisposable
        {
            readonly InputSettings original, testSettings;
            readonly Keyboard keys;
            readonly Mouse extraMouse;
            readonly List<Key> pressed=new List<Key>(6);
            int prior=-1;
            public PlaybackKeyboard(Character player)
            {
                var prefs=GamePreferences.Current.Copy();
                prefs.keyboard=KeyboardBindingValues.Defaults();prefs.pauseOnFocusLoss=false;
                GamePreferences.Apply(prefs,false,false);
                original=InputSystem.settings;testSettings=Object.Instantiate(original);
                testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
                testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                InputSystem.settings=testSettings;
                keys=InputSystem.AddDevice<Keyboard>("G-Star qualification keyboard");
                var mouse=Mouse.current;
                if(mouse==null){extraMouse=InputSystem.AddDevice<Mouse>("G-Star qualification mouse");mouse=extraMouse;}
                player.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Keyboard&Mouse",keys,mouse);
                keys.MakeCurrent();
            }
            public void Set(Vector2 dir,bool dash,bool skill,bool choose)
            {
                int mask=(dir.x>.35f?1:0)|(dir.x<-.35f?2:0)|(dir.y>.35f?4:0)|(dir.y<-.35f?8:0)|(dash?16:0)|(skill?32:0)|(choose?64:0);
                if(mask==prior)return;
                prior=mask;pressed.Clear();
                if((mask&1)!=0)pressed.Add(Key.D);
                if((mask&2)!=0)pressed.Add(Key.A);
                if((mask&4)!=0)pressed.Add(Key.W);
                if((mask&8)!=0)pressed.Add(Key.S);
                if(dash)pressed.Add(Key.LeftShift);
                if(skill)pressed.Add(Key.Space);
                if(choose)pressed.Add(Key.Digit1);
                InputSystem.QueueStateEvent(keys,new KeyboardState(pressed.ToArray()));
            }
            public void Dispose()
            {
                InputSystem.RemoveDevice(keys);
                if(extraMouse!=null)InputSystem.RemoveDevice(extraMouse);
                InputSystem.settings=original;Object.Destroy(testSettings);
            }
        }
    }
}
#endif
