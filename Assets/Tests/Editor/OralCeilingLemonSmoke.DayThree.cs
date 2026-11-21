#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static partial class OralCeilingLemonSmoke
    {
        // Same scene, seed, loadout and replay on each revision. No created monsters, XP drops,
        // field damage or forced upgrade rewards in the balance replay.
        static IEnumerator DayThreeField(LevelManager level)
        {
            string directory = Environment.GetCommandLineArgs().First(a=>a.StartsWith("-oralOutput=")).Substring(12);
            Directory.CreateDirectory(directory);
            var progression=StageProgression.Ensure(level);
            Check(progression.BeginDirectOral(),"real stage-zero loading");
            while (progression.Travelling || StageEntryLoading.IsLoading || OralStageRuntime.Instance == null) yield return null;
            var runtime=OralStageRuntime.Instance;
            var player=level.PlayerCharacter;
            var map=runtime.Map;
            var center=OralMapGeometry.TongueCenter(map);
            runtime.TeleportPlayer(center,OralZone.Tongue);
            Set(player,"isInvincible",true); // identical survivable patrol; damage difficulty is assessed separately.
            if (Environment.GetCommandLineArgs().Contains("-oralGemOnly"))
            {
                Silence(level);OralFieldEvents.Instance.AutoSchedule=false;
                yield return DayThreeWhirlpool(level,directory);
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("-oralEventsOnly"))
            {
                yield return DayThreeEvents(level,directory,null);
                yield break;
            }
            Set(level,"levelTime",225f);
            UnityEngine.Random.InitState(91237);
            var stats=Object.FindObjectOfType<StatsManager>();
            var dialog=level.EntityManager.AbilitySelectionDialog;
            var formation=Field(level,"waveFormation").GetValue(level);
            var formed=formation.GetType().GetProperty("FormationsReleased");
            bool capture = Environment.GetCommandLineArgs().Contains("-oralCapture");
            var media = new Dictionary<string,int>();
            float next=225,nextDash=228,nextSkill=232,nextSample=225,lastUp=225,longestLevel=0,idle=0,maxIdle=0;
            float closeSum=0,occupied=0;
            int sampleCount=0,prevLevel=player.CurrentLevel,upgrades=0,prevFormations=0,white=0,blue=0,green=0;
            string recording=null;
            float nextMedia=0, endRecording=0;
            var gems=new List<ExpGem>();
            var seen=new Dictionary<int,uint>();
            var gemType=typeof(ExpGem).GetField("gemType",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var visible=new Dictionary<int,List<float>>();
            foreach(int t in new[]{225,300,420,510})visible[t]=new List<float>();
            File.WriteAllText(Path.Combine(directory,"timeline.csv"),"elapsed,kills,natural_spawns,level,live,within_4,white,blue,green,formations\n");
            var speciesStats=new Dictionary<string,(float hp,float speed,float contact,int spawned)>();
            var previousGeneration=new Dictionary<int,uint>();
            float waited=0, lastC=0, pauseClock=0;
            int dashStart=player.Skills.DashSequence;
            Time.captureFramerate=25;
            using(var keyboard=new GStarLatePlaySoak.PlaybackKeyboard(player))
            {
                while(level.CurrentLevelTime < 550)
                {
                    Set(player,"currentHealth",player.MaxHealth);
                    Set(player,"isInvincible",true);
                    float t=level.CurrentLevelTime;
                    if(dialog.MenuOpen || level.IsRunFlowPaused || Time.timeScale<=0 || ApothecaryUI.Instance?.Page=="skillReward")
                    {
                        keyboard.Set(Vector2.zero,false,false,false);
                        if (dialog.MenuOpen && !dialog.SelectionAnimating && Time.realtimeSinceStartup>=pauseClock)
                        {
                            var card=dialog.GetComponentsInChildren<AbilityCard>().FirstOrDefault(x=>x.gameObject.activeInHierarchy);
                            if(card!=null){card.Selected();pauseClock=Time.realtimeSinceStartup+.22f;}
                        }
                        else if (ApothecaryUI.Instance!=null && ApothecaryUI.Instance.Page=="skillReward" && Time.realtimeSinceStartup>=pauseClock)
                        {
                            var button=ApothecaryUI.Instance.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(x=>x.isActiveAndEnabled&&x.interactable);
                            if(button!=null){button.onClick.Invoke();pauseClock=Time.realtimeSinceStartup+.25f;}
                        }
                        waited+=Time.unscaledDeltaTime;
                        CheckPauseBound(waited,dialog.MenuOpen,level.IsRunFlowPaused);
                        yield return null;continue;
                    }
                    waited=0;Time.timeScale=2f;
                    float elapsed=t-225;
                    var goal=center+new Vector2(Mathf.Sin(elapsed*.23f)*4.6f,Mathf.Sin(elapsed*.32f)*3.2f);
                    var dir=(goal-player.Position).normalized;
                    bool dash=t>=nextDash, skill=t>=nextSkill;
                    if(dash)nextDash=t+6.4f;
                    if(skill)nextSkill=t+8;
                    keyboard.Set(dir,dash,skill,false);
                    if(player.CurrentLevel>prevLevel){upgrades+=player.CurrentLevel-prevLevel;longestLevel=Mathf.Max(longestLevel,t-lastUp);lastUp=t;prevLevel=player.CurrentLevel;}
                    int releases=formed==null?0:(int)formed.GetValue(formation);
                    if(capture && releases>prevFormations && recording==null)
                    {
                        string type=(string)formation.GetType().GetProperty("LastFormation").GetValue(formation);
                        if(!media.ContainsKey(type)){recording=type;media[type]=0;nextMedia=0;endRecording=t+2.7f;}
                    }
                    prevFormations=releases;
                    if(recording!=null && t>=nextMedia)
                    {
                        Directory.CreateDirectory(Path.Combine(Output,recording));
                        captureFocus=player.Position;shotSize=6.65f;
                        Frame(recording+"/"+(media[recording]++).ToString("D4"));captureFocus=null;nextMedia=t+.115f;
                        if(t>endRecording)recording=null;
                    }
                    if(t>=nextSample)
                    {
                        nextSample=t+.25f;
                        int near=0;
                        foreach(var m in level.EntityManager.LivingMonsters)
                        {
                            if(m==null||!m.IsAlive)continue;
                            if((m.Position-player.Position).sqrMagnitude<=16)near++;
                            if(!OralMonsterRoster.IsBlueprint(m.Blueprint))continue;
                            var id=m.GetInstanceID();
                            if(previousGeneration.TryGetValue(id,out var previous)&&previous==m.SpawnGeneration)continue;
                            previousGeneration[id]=m.SpawnGeneration;
                            string palette=m.Blueprint.name+","+m.OralPaletteTier;
                            speciesStats.TryGetValue(palette,out var metric);
                            speciesStats[palette]=(m.SpawnMaxHealth,m.moveSpeed,m.ContactDamage,metric.spawned+1);
                        }
                        var dt=lastC==0?0:t-lastC;lastC=t;if(near==0)idle+=dt;else {occupied+=dt;idle=0;}
                        maxIdle=Mathf.Max(maxIdle,idle);closeSum+=near;sampleCount++;
                        visible[t<300?225:t<420?300:t<510?420:510].Add(near);
                        StageActorRegistry.CopyActiveGems(gems);
                        foreach(var g in gems)
                        {
                            int key=g.GetInstanceID();if(seen.TryGetValue(key,out var gen)&&gen==g.SpawnGeneration)continue;
                            seen[key]=g.SpawnGeneration;var value=(GemType)gemType.GetValue(g);
                            if(value==GemType.White1)white++;else if(value==GemType.Blue2)blue++;else if(value==GemType.Green10)green++;
                        }
                        if(t>=next)
                        {
                            File.AppendAllText(Path.Combine(directory,"timeline.csv"),FormattableString.Invariant($"{t:F1},{stats.MonstersKilled},{level.NormalMonstersSpawned},{player.CurrentLevel},{level.EntityManager.LivingMonsters.Count},{near},{white},{blue},{green},{releases}\n"));
                            next=t+6;
                        }
                    }
                    yield return null;
                }
            }
            Time.timeScale=1;Time.captureFramerate=0;
            var summary=FormattableString.Invariant($"elapsed={level.CurrentLevelTime:F2}\nkills={stats.MonstersKilled}\nnormal={level.NormalMonstersSpawned}\nlevel={player.CurrentLevel}\ndash={player.Skills.DashSequence-dashStart}\nlevel_ups={upgrades}\nmax_level_interval={longestLevel:F2}\nseconds_within4={occupied:F2}\nmax_empty_seconds={maxIdle:F2}\nmean_within4={closeSum/sampleCount:F2}\nwhite={white}\nblue={blue}\ngreen={green}\nformations={prevFormations}\n");
            foreach(var group in visible)if(group.Value.Count>0)summary+=FormattableString.Invariant($"near{group.Key}={group.Value.Average():F2}\n");
            File.WriteAllText(Path.Combine(directory,"field.txt"),summary);
            File.WriteAllLines(Path.Combine(directory,"species.csv"),new[]{"species,palette_index,spawn_hp,move_stat,contact_damage,observed_spawns"}.Concat(
                speciesStats.Select(p=>FormattableString.Invariant($"{p.Key},{p.Value.hp:F2},{p.Value.speed:F3},{p.Value.contact:F2},{p.Value.spawned}"))));
            Check(stats.MonstersKilled>15&&player.CurrentLevel>1&&level.NormalMonstersSpawned>30&&player.Skills.DashSequence>dashStart,"real patrol killed, collected and leveled");
            if(capture)yield return DayThreeEvents(level,directory,media);
        }

        static IEnumerator DayThreeEvents(LevelManager level, string directory, Dictionary<string,int> natural)
        {
            var oral=OralStageRuntime.Instance;var events=OralFieldEvents.Instance;var player=level.PlayerCharacter;
            var map=oral.Map;var center=OralMapGeometry.TongueCenter(map);
            Silence(level); events.AutoSchedule=false;
            typeof(OralFieldEvents).GetMethod("FinishEvent",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(events,null);
            events.Tastes.CancelAll();Clear(level);Place(player,center);
            Set(level,"levelTime",420f);
            yield return DayThreePalettes(level,directory,center);
            // Fill only missing shots after the independently measured patrol has finished.
            // These presentations still use normal rolls, the director's footprint test and live movement.
            var director=Field(level,"waveFormation").GetValue(level);
            var directorType=director.GetType();
            directorType.GetMethod("Reset").Invoke(director,null);
            string[] forms={"two-ranks","open-ring","split-chevron"};
            for(int shape=0;shape<forms.Length;shape++)
            {
                if(natural!=null && natural.ContainsKey(forms[shape]))continue;
                Clear(level);Place(player,center);
                Set(director,"formationIndex",shape);Set(director,"nextCohort",0f);
                for(int ticket=0;ticket<48 && (int)Field(director,"planned").GetValue(director)==0;ticket++)
                    level.SpawnMonsterFromCurrentSpawnTable();
                Check((int)Field(director,"planned").GetValue(director)>0,forms[shape]+" planned through live spawn table");
                captureFocus=center;shotSize=6.65f;Time.captureFramerate=15;
                Directory.CreateDirectory(Path.Combine(Output,forms[shape]));
                for(int frame=0;frame<39;frame++)
                {
                    int balance=(int)Field(level,"bonusWaveTenths").GetValue(level);
                    directorType.GetMethod("DispatchCohort").Invoke(director,new object[]{level,balance,3});
                    yield return null;Frame(forms[shape]+"/"+frame.ToString("D4"));
                }
                Time.captureFramerate=0;captureFocus=null;
                Check((string)directorType.GetProperty("LastFormation").GetValue(director)==forms[shape],forms[shape]+" released");
                File.AppendAllText(Path.Combine(directory,"directed-shots.txt"),forms[shape]+"\n");
            }
            Clear(level);Place(player,center);
            var buffs=new List<Monster>();
            for(int i=0;i<25;i++)
            {
                var m=events.SpawnFinaleMonster(center+new Vector2((i%5-2)*.88f,(i/5-2)*.84f));
                m.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;buffs.Add(m);
            }
            Check(events.ForceEvent(OralEventKind.Soda),"start carbonation on 25 ordinary actors");
            var late=events.SpawnFinaleMonster(center+Vector2.right*2.8f);
            var picks=(System.Collections.IDictionary)Field(events,"sodaTargets").GetValue(events);
            Check(picks.Count==10 && !picks.Contains(late),"exactly 10 of 25 selected once; late arrivals not coated");
            captureFocus=center;shotSize=3.2f;
            yield return CaptureDayThree("soda",6.8f);
            captureFocus=null;
            int armed=buffs.Count(m=>m.GetComponent<MonsterNeedleBarrier>()?.IsProtected==true);
            Check(armed==10 && late.GetComponent<MonsterNeedleBarrier>()?.IsProtected!=true,"actual sweep produces 10 shields and 15 unprotected");
            typeof(OralFieldEvents).GetMethod("FinishEvent",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(events,null);
            foreach(var m in buffs)m.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeRotation;
            Clear(level);
            OralGuardRoute route=default;
            bool found=false;
            for(int i=0;i<OralMapGeometry.ToothCount && !found;i++)
            {
                if(!OralGuardRoute.TryCreate(map,i,out var guide))continue;
                var heading=(guide.Landing-guide.Launch).normalized;
                if(!OralMapGeometry.TryJump(map,guide.Launch,heading,out var end) ||
                    !OralMapGeometry.TryJump(map,end,-heading,out _))continue;
                route=new OralGuardRoute(i,guide.Launch,end);found=true;
            }
            Check(found,"bidirectional grounded launch footprint");
            var dir=(route.Landing-route.Launch).normalized;
            Place(player,route.Launch);Set(player,"currentDashCharges",2);
            for(int side=0;side<2;side++)
            {
                var name=side==0?"dash-to-tooth":"dash-to-tongue";
                var position=side==0?route.Launch:route.Landing;
                var facing=side==0?dir:-dir;
                Place(player,position); Set(player,"currentDashCharges",2);
                player.Move(facing);yield return null;
                var cap=Resources.FindObjectsOfTypeAll<InteractionKeycapGraphic>().First(c=>c.gameObject.scene.isLoaded && c.name=="혀와 이빨 건너기");
                bool seen=false;
                Directory.CreateDirectory(Path.Combine(Output,name));
                Time.captureFramerate=15;
                for(int i=0;i<27;i++)
                {
                    if(i<9){Place(player,position);player.Move(facing);}
                    if(i==9){player.Move(facing);Check(player.TryDash(),name+" triggered normal dash");}
                    yield return null;
                    seen|=cap.gameObject.activeInHierarchy;
                    Frame(name+"/"+i.ToString("D4"));
                }
                Time.captureFramerate=0;
                Check(seen && OralMapGeometry.ZoneAt(map,player.Position)==(side==0?OralZone.Teeth:OralZone.Tongue),name+" shows Shift and lands");
            }
            yield return DayThreeWhirlpool(level,directory);
            File.WriteAllText(Path.Combine(directory,"mechanics.txt"),"25 eligible, 10 covered; 2 dash directions; 17 swallowed gems and an outside pickup checked");
        }

        static IEnumerator DayThreeWhirlpool(LevelManager level,string directory)
        {
            var oral=OralStageRuntime.Instance;var map=oral.Map;var player=level.PlayerCharacter;
            var center=OralMapGeometry.TongueCenter(map);
            Clear(level);Place(player,center);
            var swallow=OralSwallowFinale.Instance; Check(swallow.StartForTesting(),"start live swallow");
            Set(swallow,"age",20.05f);yield return null;
            var vortex=swallow.Whirlpools.OrderBy(w=>(w.Position-player.Position).sqrMagnitude).First();
            var outside=OralMapGeometry.ToothCenters(map).OrderByDescending(p=>swallow.Whirlpools.Min(w=>(w.Position-p).sqrMagnitude)).First();
            Place(player,outside);
            Set(player,"currentHealth",player.MaxHealth);
            var gems=new List<ExpGem>();var generations=new List<uint>();
            for(int i=0;i<17;i++)
            {
                var pos=vortex.Position+(Vector2)(Quaternion.Euler(0,0,i*360f/17)*Vector2.right)*vortex.Radius*.85f;
                var gem=level.EntityManager.SpawnExpGem(pos,i%4==0?GemType.Green10:GemType.Blue2,false);
                gems.Add(gem);generations.Add(gem.SpawnGeneration);
            }
            var probe=gems[0];int beforeLevel=player.CurrentLevel;float beforeExp=(float)Field(player,"currentExp").GetValue(player);
            probe.Collect();
            Check(probe.IsBeingCollected && !level.EntityManager.MagneticCollectables.Contains(probe),"vortex owns gem before overlapping contact or magnet");
            captureFocus=vortex.Position;shotSize=4.8f;
            yield return CaptureDayThree("swallow-gems",3.5f);
            captureFocus=null;
            Check(swallow.SwallowedGems>=17 && gems.Select((g,i)=>g.SpawnGeneration!=generations[i]||!g.gameObject.activeInHierarchy).All(x=>x),"all captured gems visibly spiral then return to pool");
            Check(beforeLevel==player.CurrentLevel && Mathf.Approximately(beforeExp,(float)Field(player,"currentExp").GetValue(player)),"captured experience gives no award");
            // Reused gems must be collectible again outside the active vortices.
            Place(player,outside);Set(player,"currentHealth",player.MaxHealth);
            int swallowedBeforeOutsidePickup=swallow.SwallowedGems;
            Vector2 position=player.Position+Vector2.right*.95f;
            Check(swallow.Whirlpools.All(w=>Vector2.Distance(position,w.Position)>w.Radius+1),"outside pickup and player have a clear flight path");
            var fresh=level.EntityManager.SpawnExpGem(position,GemType.Blue2,false);
            int old=gems.IndexOf(fresh);
            Check(old>=0 && fresh.SpawnGeneration!=generations[old] && fresh.enabled && !fresh.IsBeingCollected &&
                fresh.GetComponent<Collider2D>().enabled,"captured pooled gem has a fresh, collectible generation");
            fresh.Collect();
            Check(fresh.IsBeingCollected,"ordinary outside gem starts its normal flight");
            float deadline=Time.realtimeSinceStartup+15;
            int returnedAt=-1,awardedAt=-1;
            float expAtReturn=-1,after=beforeExp;
            // OnCollected returns the gem to the pool immediately; GainExp schedules the
            // character's XP coroutine, which may not run until the following frame.
            while(Time.realtimeSinceStartup<deadline)
            {
                after=(float)Field(player,"currentExp").GetValue(player);
                if(!fresh.gameObject.activeInHierarchy && returnedAt<0){returnedAt=Time.frameCount;expAtReturn=after;}
                if(after>beforeExp || player.CurrentLevel>beforeLevel)awardedAt=Time.frameCount;
                if(returnedAt>=0 && awardedAt>=0)break;
                yield return null;
            }
            string state=FormattableString.Invariant($"pooled={old} before={beforeExp:F2} at_pool={expAtReturn:F2} final={after:F2} return_frame={returnedAt} award_frame={awardedAt} level={beforeLevel}->{player.CurrentLevel} hp={player.CurrentHealth:F2} timescale={Time.timeScale:F2} status={fresh.IsBeingCollected} active={fresh.gameObject.activeInHierarchy} swallowed={swallow.SwallowedGems}");
            File.WriteAllText(Path.Combine(directory,"outside-gem.txt"),state);
            Check(returnedAt>=0 && awardedAt>=0 && swallow.SwallowedGems==swallowedBeforeOutsidePickup,"normal outside gem grants queued XP: "+state);
        }

        static IEnumerator DayThreePalettes(LevelManager level,string directory,Vector2 center)
        {
            var roster=level.CurrentLevelBlueprint.monsters;
            int group=Array.FindIndex(roster,g=>g.monsterBlueprints.Length==6 && OralMonsterRoster.IsBlueprint(g.monsterBlueprints[0]));
            Check(group>=0,"all six established oral species available");
            var proofs=new List<string>{"species,tier,hp,speed,contact"};
            var reusable=new List<Monster>();
            Set(level,"levelTime",10f);
            for(int roll=0;roll<20;roll++)
            {
                var defender=OralFieldEvents.Instance.SpawnFinaleMonster(center+Vector2.right*3);
                if(defender.Blueprint!=roster[group].monsterBlueprints[0] || defender.OralPaletteTier!=0)
                    throw new Exception("Oral field events bypassed the species timeline");
                defender.DespawnForStageTransfer();
            }
            Check(true,"cavities and field-event spawns respect the normal species timeline");
            var baseHP=new[]{16.1f,23.8f,46.2f,69.3f,92.4f,116.9f};
            var baseSpeeds=new[]{.69f,.79f,.66f,.78f,.92f,.85f};
            var h=new[]{1f,1.20f,1.50f};var v=new[]{1f,1.045f,1.09f};var a=new[]{1f,1.10f,1.22f};
            var unlocks=new[]{0,45,130,225,315,400};
            for(int kind=0;kind<6;kind++)
            {
                var bp=roster[group].monsterBlueprints[kind];
                int flat=level.CurrentLevelBlueprint.MonsterIndexMap.First(p=>p.Value.Item1==group && p.Value.Item2==kind).Key;
                var table=level.CurrentLevelBlueprint.monsterSpawnTable;
                float from=unlocks[kind]/600f,to=(unlocks[kind]+12)/600f;
                Check((kind==0 || table.GetProbability(Mathf.Max(0,from-.001f),flat)<.0001f)
                    && table.GetProbability(to,flat)>0,bp.name+" unlocks on its own timeline");
                Set(level,"levelTime",(float)unlocks[kind]+10);
                for(int repeat=0;repeat<8;repeat++)
                {
                    var fresh=level.EntityManager.SpawnMonster(group,center+Vector2.right*3,bp);
                    if(fresh.OralPaletteTier!=0)throw new Exception(bp.name+" introduced with an upgraded palette");
                    fresh.DespawnForStageTransfer();
                }
                Set(level,"levelTime",545f);
                var tierActors=new Monster[3];
                for(int roll=0;roll<160 && tierActors.Any(m=>m==null);roll++)
                {
                    var m=level.EntityManager.SpawnMonster(group,center+Vector2.up*3,bp);
                    int tier=m.OralPaletteTier;
                    if(tier<0||tier>2)throw new Exception("Missing oral palette for "+bp.name);
                    if(Mathf.Abs(m.SpawnMaxHealth-baseHP[kind]*h[tier])>.015f || Mathf.Abs(m.moveSpeed-baseSpeeds[kind]*v[tier])>.002f ||
                       Mathf.Abs(m.ContactDamage-(4+kind)*a[tier])>.012f)throw new Exception("Spawn stats mismatch or pooled multiplier accumulated: "+bp.name+"/"+tier+" actual="+m.SpawnMaxHealth+"/"+m.moveSpeed+"/"+m.ContactDamage);
                    float hp=m.SpawnMaxHealth,speed=m.moveSpeed;
                    OralMonsterRoster.ApplySpawn(m);
                    if(m.OralPaletteTier!=tier || Mathf.Abs(m.SpawnMaxHealth-hp)>.001 || Mathf.Abs(m.moveSpeed-speed)>.001)
                        throw new Exception("Duplicate tier application on "+bp.name);
                    if(tierActors[tier]!=null){m.DespawnForStageTransfer();continue;}
                    tierActors[tier]=m;
                    proofs.Add(FormattableString.Invariant($"{bp.name},{tier},{m.SpawnMaxHealth:F2},{m.moveSpeed:F3},{m.ContactDamage:F2}"));
                }
                Check(tierActors.All(m=>m!=null),bp.name+" base/secondary/strong all observed through normal rolls");
                foreach(var m in tierActors)
                {
                    if(kind!=1){m.DespawnForStageTransfer();continue;}
                    // All three real gummy bears pursue the same hero; keep positions apart to expose each skin.
                    var pos=center+new Vector2((m.OralPaletteTier-1)*1.5f,2.0f);
                    m.transform.position=pos;m.GetComponent<Rigidbody2D>().position=pos;reusable.Add(m);
                }
                yield return null;
            }
            var practiceEnemy=level.EntityManager.SpawnMonster(group,center+Vector2.right*3,roster[group].monsterBlueprints[0],0,true);
            Check(practiceEnemy!=null && practiceEnemy.OralPaletteTier==-1 &&
                Mathf.Approximately(practiceEnemy.SpawnMaxHealth,practiceEnemy.Blueprint.hp),"mini-stage actors retain their own stats");
            Check(ReferenceEquals(OralMonsterRoster.ExperienceTable(practiceEnemy.Blueprint,practiceEnemy),
                practiceEnemy.Blueprint.gemLootTable),"mini-stage XP uses the encounter table");
            practiceEnemy.DespawnForStageTransfer();
            var sniper=level.CurrentLevelBlueprint.MonsterIndexMap.Values.First(pair=>roster[pair.Item1].monsterBlueprints[pair.Item2] is SniperMonsterBlueprint);
            var special=level.EntityManager.SpawnMonster(sniper.Item1,center+Vector2.right*4,roster[sniper.Item1].monsterBlueprints[sniper.Item2]);
            Check(special!=null && special.OralPaletteTier==-1,"unrelated field specials keep their own combat profile");
            special.DespawnForStageTransfer();
            File.WriteAllLines(Path.Combine(directory,"palette-matrix.csv"),proofs);
            captureFocus=center+Vector2.up;shotSize=2.8f;
            yield return CaptureDayThree("oral-palettes",2.4f);
            captureFocus=null;
            foreach(var m in reusable)if(m.IsAlive)m.DespawnForStageTransfer();
            Set(level,"levelTime",420f);
        }

        static IEnumerator CaptureDayThree(string name,float seconds)
        {
            Directory.CreateDirectory(Path.Combine(Output,name));Time.captureFramerate=15;
            int frames=Mathf.CeilToInt(seconds*15f);
            for(int i=0;i<frames;i++){yield return null;Frame(name+"/"+i.ToString("D4"));}
            Time.captureFramerate=0;
        }

        static void CheckPauseBound(float seconds,bool menu,bool paused)
        {
            if(seconds>120)throw new Exception("Oral patrol awaits UI "+(ApothecaryUI.Instance?.Page)+" menu="+menu+" paused="+paused);
        }
    }
}
#endif
