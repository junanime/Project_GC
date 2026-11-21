#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public static partial class OralCeilingLemonSmoke
    {
        // Complements the paired late-window pressure benchmark with first-spawn runs:
        // actual damage, contact, rewards, deaths and the selected lobby item.
        static IEnumerator DayThreeNatural(LevelManager level)
        {
            int round=SessionState.GetInt(Key+"NaturalRound",0);
            bool familiar=(round&1)==1;
            int seed=round<2?204831:795041;
            string output=Environment.GetCommandLineArgs().First(a=>a.StartsWith("-oralOutput=")).Substring(12);
            string directory=Path.Combine(output,(familiar?"returning-":"first-")+seed);
            Directory.CreateDirectory(directory);
            var progression=StageProgression.Ensure(level);
            UnityEngine.Random.InitState(seed);
            Check(progression.BeginDirectOral(true),"initial mouth entry with lobby loadout");
            while(progression.Travelling || StageEntryLoading.IsLoading || OralStageRuntime.Instance==null)yield return null;
            var runtime=OralStageRuntime.Instance;
            var player=level.PlayerCharacter;var stats=UnityEngine.Object.FindObjectOfType<StatsManager>();
            var dialog=level.EntityManager.AbilitySelectionDialog;var center=OralMapGeometry.TongueCenter(runtime.Map);
            string log=Path.Combine(directory,"play.csv");
            File.WriteAllText(log,"elapsed,hp,damage_taken,kills,spawned,level,nearby,dashes,skill_inputs,score\n");
            File.WriteAllText(Path.Combine(directory,"build.txt"),"아시 · 출전장비 비타민 C / 실제 보상창 선택\n");
            float nextSample=0,nextDash=3,nextSkill=7,nextChoice=0,previousHealth=player.CurrentHealth,totalDamage=0,paused=0;
            int dashStart=player.Skills.DashSequence,skillInputs=0;
            float reach=0;
            var near=new List<Monster>();
            var routes=new List<OralGuardRoute>();
            for(int tooth=0;tooth<OralMapGeometry.ToothCount;tooth++)
                if(OralGuardRoute.TryCreate(runtime.Map,tooth,out var landingRoute))routes.Add(landingRoute);
            var gems=new List<ExpGem>();Vector2 move=Vector2.right;
            float nextThink=0,healthBefore=player.CurrentHealth;
            float trapAt=-1,trapNext=0,trapSeconds=0;
            int traps=0,trapPresses=0;
            bool beginnerWrong=false;
            var trapBind=player.GetComponent<PlayerTrapBindRuntime>();
            var arrows=new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right};
            Time.captureFramerate=25;
            using(var keys=new GStarLatePlaySoak.PlaybackKeyboard(player))
            {
                while (player.IsAlive && !level.IsLevelEnded && level.CurrentLevelTime<550)
                {
                    float t=level.CurrentLevelTime;reach=t;
                    totalDamage+=Mathf.Max(0,previousHealth-player.CurrentHealth);previousHealth=player.CurrentHealth;
                    if(dialog.MenuOpen || level.IsRunFlowPaused || Time.timeScale<=0 || ApothecaryUI.Instance?.Page=="skillReward")
                    {
                        keys.Set(Vector2.zero,false,false,false);
                        if(Time.realtimeSinceStartup>=nextChoice)
                        {
                            if(dialog.MenuOpen && !dialog.SelectionAnimating)
                            {
                                var cards=dialog.GetComponentsInChildren<AbilityCard>().Where(c=>c.gameObject.activeInHierarchy).ToList();
                                if(cards.Count>0)
                                {
                                    var candidates=cards;
                                    if(familiar)
                                    {
                                        var safeCards=cards.Where(c=>!((Ability)Field(c,"ability").GetValue(c)).name.Contains("Life Burn")).ToList();
                                        if(safeCards.Count>0)candidates=safeCards;
                                    }
                                    var card=familiar?candidates.OrderByDescending(c=>c.SelectionGrade).First():cards[0];
                                    var ability=(Ability)Field(card,"ability").GetValue(card);
                                    File.AppendAllText(Path.Combine(directory,"build.txt"),FormattableString.Invariant($"{t:F1} {ability?.name} [{card.SelectionGrade}]\n"));
                                    card.Selected();
                                }
                            }
                            else if(ApothecaryUI.Instance?.Page=="skillReward")
                            {
                                var b=ApothecaryUI.Instance.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(x=>x.isActiveAndEnabled&&x.interactable);
                                if(b!=null){File.AppendAllText(Path.Combine(directory,"build.txt"),FormattableString.Invariant($"{t:F1} {b.name}\n"));b.onClick.Invoke();}
                            }
                            nextChoice=Time.realtimeSinceStartup+.2f;
                        }
                        paused+=Time.unscaledDeltaTime;CheckPauseBound(paused,dialog.MenuOpen,level.IsRunFlowPaused);
                        yield return null;continue;
                    }
                    paused=0;Time.timeScale=2;
                    bool dash=t>=nextDash,skill=t>=nextSkill;
                    if(t>=nextThink)
                    {
                        nextThink=t+.25f;near.Clear();Vector2 avoid=Vector2.zero;
                        foreach(var m in level.EntityManager.LivingMonsters)
                        {
                            if(m==null || !m.IsAlive || m.IsFieldRuntimeSuspended || !m.gameObject.activeInHierarchy)continue;
                            Vector2 away=player.Position-m.Position;float sq=away.sqrMagnitude;
                            if(sq<16)near.Add(m);
                            if(sq<4)avoid+=away.normalized/Mathf.Max(.4f,sq);
                        }
                        var goal=center+new Vector2(Mathf.Sin(t*(familiar ? .18f : .13f))*(familiar ? 6 : 4),Mathf.Sin(t*.22f)*3.25f);
                        if(familiar && near.Count<6)
                        {
                            StageActorRegistry.CopyActiveGems(gems);float best=9;
                            foreach(var gem in gems)
                            {
                                if(gem.IsBeingCollected)continue;
                                Vector2 p=gem.transform.position;float d=(p-player.Position).sqrMagnitude;
                                if(d<best){best=d;goal=p;}
                            }
                        }
                        move=(goal-player.Position).normalized;
                        if(familiar && near.Count>=6 && OralMapGeometry.ZoneAt(runtime.Map,player.Position)==OralZone.Tongue)
                        {
                            // A returning player can run along the open edge of a crowd. Direct vector sums
                            // cancel out inside a horseshoe and make a center-loop bot run into the pack.
                            float best=float.NegativeInfinity;Vector2 safe=move;
                            for(int sector=0;sector<12;sector++)
                            {
                                float radians=sector*Mathf.PI/6;
                                var direction=new Vector2(Mathf.Cos(radians),Mathf.Sin(radians));
                                var probe=player.Position+direction*2.5f;
                                if(OralMapGeometry.TongueDistance(runtime.Map,probe,out _)<.35f)continue;
                                float value=Vector2.Dot(direction,move)*.8f;
                                foreach(var m in near)
                                {
                                    float distance=(probe-m.Position).sqrMagnitude;
                                    value-=1.0f/Mathf.Max(.20f,distance);
                                }
                                if(value>best){best=value;safe=direction;}
                            }
                            move=safe;
                        }
                        else if(familiar && avoid.sqrMagnitude>.2f)move=(move*.62f+avoid.normalized*.85f).normalized;
                    }
                    var taste=OralFieldEvents.Instance.Tastes;
                    var current=OralMapGeometry.ZoneAt(runtime.Map,player.Position);
                    bool spicy=taste!=null&&taste.Active&&taste.Kind==OralTasteKind.Spicy;
                    if(familiar && !runtime.IsAirborne && (spicy || current==OralZone.Teeth))
                    {
                        OralGuardRoute route=default;float best=float.MaxValue;
                        foreach(var r in routes)
                        {
                            float dd=(r.Launch-player.Position).sqrMagnitude;if(dd<best){best=dd;route=r;}
                        }
                        if(best<float.MaxValue)
                        {
                            if(current==OralZone.Tongue && spicy)
                            {
                                var direction=(route.Landing-route.Launch).normalized;
                                bool ready=OralMapGeometry.TryJump(runtime.Map,player.Position,direction,out _);
                                move=ready?direction:(route.Launch-player.Position).normalized;
                                dash=ready && player.CurrentDashCharges>0 && t>=nextDash;
                            }
                            else if(current==OralZone.Teeth)
                            {
                                if(spicy){move=Vector2.zero;dash=false;}
                                else{move=(route.Launch-player.Position).normalized;dash=t>=nextDash && OralMapGeometry.TryJump(runtime.Map,player.Position,move,out _);}
                            }
                        }
                    }
                    if(dash)nextDash=t+(familiar ? 3.3f : 8.5f);
                    if(skill){skillInputs++;nextSkill=t+(familiar?7:12);}
                    if(player.IsTrapBound)
                    {
                        if(trapBind==null)trapBind=player.GetComponent<PlayerTrapBindRuntime>();
                        if(trapAt<0){trapAt=t;traps++;trapNext=t+(familiar ? .75f : 2.4f);beginnerWrong=!familiar;}
                        move=Vector2.zero;dash=false;skill=false;
                        var trap=trapBind?.CurrentTrap;
                        int target=trap!=null?trap.EscapeTarget:-1;
                        if(target>=0 && t>=trapNext)
                        {
                            move=arrows[beginnerWrong?(target+1)%4:target];beginnerWrong=false;
                            trapNext=t+(familiar ? .45f : .95f);trapPresses++;
                        }
                    }
                    else if(trapAt>=0){trapSeconds+=t-trapAt;trapAt=-1;}
                    keys.Set(move,dash,skill,false);
                    if(t>=nextSample)
                    {
                        File.AppendAllText(log,FormattableString.Invariant($"{t:F1},{player.CurrentHealth:F1},{totalDamage:F1},{stats.MonstersKilled},{level.NormalMonstersSpawned},{player.CurrentLevel},{near.Count},{player.Skills.DashSequence-dashStart},{skillInputs},{RunScore.Current.Data.Total}\n"));
                        nextSample=t+6;
                    }
                    yield return null;
                }
            }
            Time.captureFramerate=0;Time.timeScale=1;
            totalDamage+=Mathf.Max(0,previousHealth-player.CurrentHealth);
            if(trapAt>=0)trapSeconds+=reach-trapAt;
            File.WriteAllText(Path.Combine(directory,"result.txt"),FormattableString.Invariant($"survived={player.IsAlive}\nstart_hp={healthBefore:F0}\nelapsed={reach:F1}\nhp={player.CurrentHealth:F1}\nhp_lost={totalDamage:F1}\nkills={stats.MonstersKilled}\nnormal={level.NormalMonstersSpawned}\nlevel={player.CurrentLevel}\ndash={player.Skills.DashSequence-dashStart}\nskill_inputs={skillInputs}\nscore={RunScore.Current.Data.Total}\ntraps={traps}\ntrap_key_inputs={trapPresses}\nseconds_bound={trapSeconds:F1}\n"));
            Check(reach>25 && stats.MonstersKilled>0,"actual unprotected session finished");
        }
    }
}
#endif
