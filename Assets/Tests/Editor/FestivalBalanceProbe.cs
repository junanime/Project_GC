using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Control-strategy probes are reproducible mechanical checks, not estimates of human skill.
    public static class FestivalBalanceProbe
    {
        public static IEnumerator Run()
        {
            string output="Library/FestivalQA/balance.csv";
            File.WriteAllText(output,"strategy,character,seed,seconds,success,kills,hp,peak,specials,pathLength,meanFrameMs,p95FrameMs\n");
            var config=FestivalDemo.Catalog;
            for(int strategy=0;strategy<3;strategy++)
            for(int repeat=0;repeat<(strategy==0?1:2);repeat++)
            foreach(var preset in FestivalDemo.Settings.presets)
            {
                Time.timeScale=1;SceneManager.LoadScene(0);yield return null;yield return null;
                while(ApothecaryUI.Instance==null)yield return null;
                int seed=27001+repeat*1009+Array.IndexOf(FestivalDemo.Settings.presets,preset)*37;
                UnityEngine.Random.InitState(seed);
                var selected=config.characters.First(c=>OctoberArt.CharacterKey(c)==preset.character);
                CrossSceneData.CharacterBlueprint=selected;CrossSceneData.ClearStartingLobbyItems();
                if(!StageEntryLoading.Begin(selected))throw new Exception("Probe loading failed");
                while(StageEntryLoading.IsLoading)yield return null;
                yield return null;
                var level=Object.FindObjectOfType<LevelManager>();var hero=level.PlayerCharacter;
                float nextThink=0,nextDash=2,nextSkill=1,path=0;
                int peak=0;Vector2 direction=Vector2.right,last=hero.transform.position;
                var frames=new List<float>();float wallDeadline=Time.realtimeSinceStartup+180;
                while(!level.IsLevelEnded)
                {
                    if(Time.realtimeSinceStartup>wallDeadline)throw new Exception("Probe timer stalled");
                    float t=level.CurrentLevelTime;
                    var enemies=level.EntityManager.LivingMonsters.Where(m=>m!=null&&m.HP>0).ToArray();
                    Vector2 pos=hero.transform.position;
                    Monster nearest=enemies.OrderBy(m=>((Vector2)m.transform.position-pos).sqrMagnitude).FirstOrDefault();
                    float distance=nearest==null?100:Vector2.Distance(pos,nearest.transform.position);
                    if(t>=nextThink)
                    {
                        nextThink=t+(strategy==0?1.2f:strategy==1?.3f:.12f);
                        if(strategy==0)
                        {
                            // Novice-like probe: large, delayed turns, no threat prediction.
                            direction=new Vector2(Mathf.Cos(t*.3f),Mathf.Sin(t*.3f));
                            if(distance<1)direction=(pos-(Vector2)nearest.transform.position).normalized;
                        }
                        else if(strategy==1)
                        {
                            direction=distance<5?(pos-(Vector2)nearest.transform.position).normalized:new Vector2(Mathf.Cos(t*.45f),Mathf.Sin(t*.45f));
                        }
                        else
                        {
                            float best=float.NegativeInfinity;Vector2 chosen=direction;
                            for(int i=0;i<24;i++)
                            {
                                float angle=i*Mathf.PI*2/24;Vector2 d=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                                Vector2 target=pos+d*2.5f,safe=FiniteStomachMap.SafeSpawn(target,.6f);
                                float score=-Vector2.Distance(safe,target)*20+Vector2.Dot(d,direction)*.35f;
                                foreach(var enemy in enemies)
                                {
                                    Vector2 threat=enemy.transform.position;
                                    float sq=(threat-target).sqrMagnitude;
                                    score-=3f/(.15f+sq);
                                    // A clear segment matters more than an empty endpoint behind a crowd.
                                    float along=Mathf.Clamp01(Vector2.Dot(threat-pos,target-pos)/6.25f);
                                    float lane=(threat-Vector2.Lerp(pos,target,along)).sqrMagnitude;
                                    if(lane<1)score-=(1-lane)*5;
                                }
                                if(score>best){best=score;chosen=d;}
                            }
                            direction=chosen;
                        }
                    }
                    hero.Move(direction);
                    hero.LookDirection=nearest!=null?((Vector2)nearest.transform.position-pos).normalized:direction;
                    if(t>=nextDash && (strategy==0?t>12:strategy==1?distance<2:distance<2.5f))
                    {hero.TryDash();nextDash=t+(strategy==0?14:strategy==1?5:2.2f);}
                    if(t>=nextSkill && (strategy==0?t>15:distance<4.5f))
                    {hero.Skills.TryActivate();nextSkill=t+(strategy==0?20:strategy==1?4:.75f);}
                    path+=Vector2.Distance(pos,last);last=pos;peak=Mathf.Max(peak,enemies.Length);
                    if(t>2)frames.Add(Time.unscaledDeltaTime*1000);
                    yield return null;
                }
                hero.Move(Vector2.zero);frames.Sort();var data=RunScore.Current.Data;
                int specials=level.GetComponent<FestivalRunController>().SpecialSpawned;
                if(peak>FestivalDemo.Settings.populationLimit)throw new Exception("Probe population cap exceeded: "+peak);
                if(data.seconds>8&&specials==0)throw new Exception("Probe has no special enemies");
                if(hero.CurrentLevel!=1)throw new Exception("Festival leveled up");
                string row=string.Join(",",strategy,preset.character,seed,data.seconds.ToString("F2",CultureInfo.InvariantCulture),data.success,data.normal+data.special+data.elite,hero.CurrentHealth.ToString("F1",CultureInfo.InvariantCulture),peak,specials,path.ToString("F1",CultureInfo.InvariantCulture),frames.Count>0?frames.Average().ToString("F2",CultureInfo.InvariantCulture):"0",frames.Count>0?frames[(int)((frames.Count-1)*.95f)].ToString("F2",CultureInfo.InvariantCulture):"0");
                File.AppendAllText(output,row+"\n");Debug.Log("FESTIVAL_BALANCE "+row);
            }
        }
    }
}
