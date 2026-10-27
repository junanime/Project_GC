using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
    // Real combat/physics, imperfect controllers. These are NOT human success-rate estimates.
    public static class FestivalBalanceSessions
    {
        static string F(float n)=>n.ToString("F3",CultureInfo.InvariantCulture);
        static int Env(string key,int fallback)=>int.TryParse(Environment.GetEnvironmentVariable(key),out int n)?n:fallback;
        static string RunKey(string line)=>string.Join(",",line.Split(',').Take(3));
        static HashSet<string> PrepareOutput(string folder,string output,string timeline,HashSet<string> expected)
        {
            const string header="profile,character,seed,seconds,success,normal,special,hp,maxHp,peak,snipers,acidSpores,dashes,actives,noble,pathLength,damageTaken,damageAt10,damageAt20,damageAt30,damageAt40,healsUsed,bombsUsed,nobleType";
            const string spawnHeader="profile,character,seed,kind,seconds,distance";
            string settings=JsonUtility.ToJson(FestivalDemo.Settings,true);
            var done=new HashSet<string>(StringComparer.Ordinal);
            if(Env("FESTIVAL_BALANCE_RESUME",0)==1)
            {
                if(!File.Exists(output)||!File.Exists(timeline)||File.ReadAllText(folder+"/settings.json").Trim()!=settings.Trim())
                    throw new Exception("Cannot resume: missing output or changed gameplay settings");
                var rows=File.ReadAllLines(output);
                if(rows.Length==0||rows[0]!=header)throw new Exception("Unexpected balance CSV schema");
                foreach(string row in rows.Skip(1).Where(s=>!string.IsNullOrWhiteSpace(s)))
                {
                    string key=RunKey(row);
                    if(row.Split(',').Length!=24||!expected.Contains(key)||!done.Add(key))
                        throw new Exception("Incomplete, duplicate or unexpected completed run: "+key);
                }
                var spawns=File.ReadAllLines(timeline);
                if(spawns.Length==0||spawns[0]!=spawnHeader)throw new Exception("Unexpected spawn CSV schema");
                // A stopped battle can have spawn rows but no completed result. Replay it once.
                var retained=spawns.Skip(1).Where(row=>done.Contains(RunKey(row))).ToArray();
                if(retained.Any(row=>row.Split(',').Length!=6))throw new Exception("Incomplete retained spawn event");
                File.WriteAllLines(timeline,new[]{spawnHeader}.Concat(retained));
                Debug.Log("FESTIVAL_BALANCE_RESUMED completed="+done.Count);
            }
            else
            {
                if(File.Exists(output)||File.Exists(timeline))throw new Exception("Use a new balance tag or explicitly resume existing results");
                File.WriteAllText(folder+"/settings.json",settings);
                File.WriteAllText(output,header+"\n");File.WriteAllText(timeline,spawnHeader+"\n");
            }
            return done;
        }
        static Vector2 KeyboardDirection(Vector2 direction)
        {
            if(direction.sqrMagnitude<.0001f)return Vector2.zero;
            float angle=Mathf.Round(Mathf.Atan2(direction.y,direction.x)/(Mathf.PI/4))*(Mathf.PI/4);
            return new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
        }
        public static IEnumerator Run()
        {
            int repeats=Env("FESTIVAL_BALANCE_REPEATS",10),first=Env("FESTIVAL_BALANCE_FIRST_PROFILE",0),last=Env("FESTIVAL_BALANCE_LAST_PROFILE",1);
            string folder="Library/FestivalQA/Balance-"+(Environment.GetEnvironmentVariable("FESTIVAL_BALANCE_TAG")??"final");
            Directory.CreateDirectory(folder);
            string output=folder+"/runs.csv",timeline=folder+"/spawns.csv";
            int offset=Env("FESTIVAL_BALANCE_SEED_OFFSET",0);
            var expected=new HashSet<string>(from profile in Enumerable.Range(first,last-first+1)
                from repeat in Enumerable.Range(0,repeats) from preset in FestivalDemo.Settings.presets
                select profile+","+preset.character+","+(627001+repeat*1009+offset),StringComparer.Ordinal);
            var completed=PrepareOutput(folder,output,timeline,expected);
            float previousCapture=Time.captureDeltaTime;int previousRate=Application.targetFrameRate;
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            try
            {
                // Fixed simulation steps remove editor hitches as a difficulty variable.
                // Both profiles use identical game stats and spawn rules.
                Time.captureDeltaTime=1f/30;Application.targetFrameRate=-1;
                foreach(int profile in Enumerable.Range(first,last-first+1).OrderByDescending(p=>p))
                for(int repeat=0;repeat<repeats;repeat++)
                foreach(var preset in FestivalDemo.Settings.presets)
                {
                    int seed=627001+repeat*1009+offset;
                    string key=profile+","+preset.character+","+seed;
                    if(completed.Contains(key))continue;
                    Time.timeScale=1;SceneManager.LoadScene(0);yield return null;yield return null;
                    while(ApothecaryUI.Instance==null)yield return null;
                    var random=new System.Random(seed+profile*7919);UnityEngine.Random.InitState(seed);
                    var selected=FestivalDemo.Catalog.characters.First(c=>OctoberArt.CharacterKey(c)==preset.character);
                    CrossSceneData.CharacterBlueprint=selected;CrossSceneData.ClearStartingLobbyItems();
                    if(!StageEntryLoading.Begin(selected))throw new Exception("Probe loading failed");
                    while(StageEntryLoading.IsLoading)yield return null;
                    yield return FestivalDemoSmoke.ContinueGaugeGuide();
                    Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;
                    var level=Object.FindObjectOfType<LevelManager>();var hero=level.PlayerCharacter;
                    if(Object.FindObjectsOfType<FinalBossSummonInteractable>().Length!=0 ||
                        Object.FindObjectsOfType<CurrencyJar>().Length!=0 ||
                        Object.FindObjectOfType<StageEventDirector>().FestivalEventKind=="Gold")
                        throw new Exception("Festival has a boss altar/currency placement/event");
                    bool mid=profile==1;
                    float nextThink=0,nextDash=mid?1:7,nextSkill=mid?.5f:10,path=0,damage=0,priorHp=hero.CurrentHealth,releaseFlame=0,aimUntil=0,nextChestCheck=0;
                    float[] milestones=new float[4];int milestone=0,peak=0,dashes=0,actives=0,healsUsed=0,bombsUsed=0;
                    float nextConsumable=mid?1:5;
                    var slots=level.EntityManager.Inventory.Slots;
                    Vector2 direction=Vector2.right,lastPos=hero.transform.position,flameAim=Vector2.right;
                    float deadline=Time.realtimeSinceStartup+240;
                    Action<Monster,float> spawned=(m,t)=>
                    {
                        if(!(m is SniperMonster)&&!(m is ExplodingMonster))throw new Exception("Disallowed festival special: "+m.GetType().Name);
                        float distance=Vector2.Distance(hero.transform.position,m.transform.position);
                        if(m is SniperMonster&&(distance<3.79f||distance>5.21f))throw new Exception("Sniper relocated outside scheduled ring");
                        File.AppendAllText(timeline,$"{profile},{preset.character},{seed},{m.GetType().Name},{F(t)},{F(distance)}\n");
                    };
                    FestivalRunController.SpecialAppeared+=spawned;
                    try
                    {
                        while(!level.IsLevelEnded)
                        {
                            if(Time.realtimeSinceStartup>deadline)throw new Exception("Probe timer stalled");
                            float t=level.CurrentLevelTime;Vector2 pos=hero.transform.position;
                            var enemies=level.EntityManager.LivingMonsters.Where(m=>m!=null&&m.HP>0).ToArray();
                            if(enemies.Any(m=>RunScore.IsSpecial(m)&&!(m is SniperMonster)&&!(m is ExplodingMonster)))
                                throw new Exception("Unexpected special in the actual spawn pool");
                            // Local threats only. No offscreen knowledge or projectile prediction.
                            var visible=enemies.Where(m=>((Vector2)m.transform.position-pos).sqrMagnitude<42).ToArray();
                            Monster nearest=visible.OrderBy(m=>((Vector2)m.transform.position-pos).sqrMagnitude).FirstOrDefault();
                            float distance=nearest==null?100:Vector2.Distance(pos,nearest.transform.position);
                            if(t>=nextThink)
                            {
                                if(mid&&hero.Skills.IsShini&&nearest!=null)
                                    flameAim=KeyboardDirection((Vector2)nearest.transform.position-pos);
                                nextThink=t+(mid?.30f:.85f)+(float)random.NextDouble()*(mid?.15f:.4f);
                                float angle=(float)random.NextDouble()*Mathf.PI*2;Vector2 wandering=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                                if(!mid)direction=distance<1.4f?(pos-(Vector2)nearest.transform.position).normalized:Vector2.Lerp(direction,wandering,.35f).normalized;
                                else
                                {
                                    Vector2[] fuel = hero.Skills.IsShini && hero.Skills.Breath.Fuel < 12 ? VisibleFuel(hero.Skills.Breath,pos) : Array.Empty<Vector2>();
                                    float best=float.NegativeInfinity;Vector2 chosen=direction;
                                    for(int i=0;i<8;i++)
                                    {
                                        float a=i*Mathf.PI*2/8;Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                                        Vector2 target=pos+d*1.6f,safe=FiniteStomachMap.SafeSpawn(target,.5f);
                                        float score=-Vector2.Distance(safe,target)*12+Vector2.Dot(d,direction)*.6f;
                                        foreach(var enemy in visible)
                                        {
                                            Vector2 threat=enemy.transform.position;score-=1.6f/(.35f+(threat-target).sqrMagnitude);
                                            float along=Mathf.Clamp01(Vector2.Dot(threat-pos,target-pos)/2.56f);
                                            float lane=(threat-Vector2.Lerp(pos,target,along)).sqrMagnitude;
                                            if(lane<.5f)score-=(.5f-lane)*3;
                                        }
                                        score+=(float)random.NextDouble()*.5f;
                                        if(fuel.Length>0)score+=2f/(1f+fuel.Min(p=>(p-target).sqrMagnitude));
                                        if(score>best){best=score;chosen=d;}
                                    }
                                    direction=chosen;
                                }
                            }
                            if(t>=nextSkill&&nearest!=null&&distance<4.5f)
                            {
                                bool activated;
                                if(hero.Skills.IsShini)
                                {
                                    flameAim=KeyboardDirection((Vector2)nearest.transform.position-pos);hero.LookDirection=flameAim;
                                    activated=hero.Skills.Breath.Press(false);
                                    if(activated){releaseFlame=t+(mid?3.8f:1.2f);aimUntil=t+(mid?1.5f:.8f);}
                                }
                                else activated=hero.Skills.TryActivate();
                                if(activated){actives++;nextSkill=t+(hero.Skills.IsShini?(mid?5:8):hero.Skills.EffectiveCooldown)+(mid?.6f:3)*(float)random.NextDouble();}
                                else nextSkill=t+(mid?.4f:1.3f);
                            }
                            if(hero.Skills.IsShini&&hero.Skills.Breath.Busy)
                            {
                                if(t>=releaseFlame)hero.Skills.Breath.ReleasePointer();
                                if(mid && nearest!=null && distance>.8f)
                                {
                                    // A human holding fire continues facing targets until a close threat
                                    // forces retreat; don't deliberately spend the rest of the fuel backwards.
                                    hero.Move(Vector2.zero);hero.LookDirection=flameAim;
                                }
                                else if(!mid&&t<aimUntil&&distance>1.1f){hero.Move(Vector2.zero);hero.LookDirection=flameAim;}
                                else hero.Move(KeyboardDirection(direction));
                            }
                            else hero.Move(KeyboardDirection(direction));
                            bool ariAttack=hero.Skills.IsAri&&hero.Skills.Active&&distance<3;
                            if(t>=nextDash&&(distance<(mid?1.9f:1)||ariAttack))
                            {
                                // Aim an available transformed dash at a nearby enemy, then resume
                                // evasive walking between charges rather than walking into the crowd.
                                if (ariAttack && hero.CurrentDashCharges > 0)
                                    hero.Move(KeyboardDirection((Vector2)nearest.transform.position-pos));
                                if(hero.TryDash()){dashes++;nextDash=t+(ariAttack?(mid?.8f:2):(mid?2.8f:7));}
                                else nextDash=t+.3f;
                            }
                            if(mid&&t>nextChestCheck)
                            {
                                nextChestCheck=t+.25f;
                                var chest=Object.FindObjectsOfType<Chest>().FirstOrDefault(c=>c.CanInteract&&Vector2.Distance(pos,c.transform.position)<1);
                                if(chest!=null)chest.OpenChest();
                            }
                            if(t>=nextConsumable)
                            {
                                nextConsumable=t+(mid?.6f:5f);
                                foreach(var slot in slots)
                                {
                                    if(slot.Count==0)continue;
                                    if(slot.CollectableType is HealthCollectable && hero.MaxHealth-hero.CurrentHealth>=30 && (mid||hero.CurrentHealth<hero.MaxHealth*.4f))
                                    {slot.UseItem();healsUsed++;}
                                    else if(slot.IsBomb&&nearest!=null&&(visible.Length>8||distance<1.3f))
                                    {if(slot.ThrowBomb(nearest.transform.position))bombsUsed++;}
                                }
                            }
                            var dialog=level.EntityManager.AbilitySelectionDialog;
                            if(dialog.MenuOpen)
                            {
                                var button=dialog.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(b=>b.interactable&&b.gameObject.activeInHierarchy);
                                if(button!=null)button.onClick.Invoke();
                            }
                            damage+=Mathf.Max(0,priorHp-hero.CurrentHealth);priorHp=hero.CurrentHealth;
                            while(milestone<4&&t>=(milestone+1)*10)milestones[milestone++]=damage;
                            path+=Vector2.Distance(pos,lastPos);lastPos=pos;peak=Mathf.Max(peak,enemies.Length);
                            yield return null;
                        }
                        while(milestone<4)milestones[milestone++]=damage;
                        hero.Move(Vector2.zero);var data=RunScore.Current.Data;var run=level.GetComponent<FestivalRunController>();
                        if(Object.FindObjectsOfType<Coin>().Length!=0 || Object.FindObjectsOfType<CurrencyJarDrop>().Length!=0)
                            throw new Exception("Festival leaked a currency pickup");
                        if(peak>FestivalDemo.Settings.populationLimit||hero.CurrentLevel!=1)throw new Exception("Population/XP invariant failed");
                        if(data.success&&(run.SnipersSpawned!=2||run.AcidSporesSpawned!=5))throw new Exception("Missed scheduled specials");
                        string row=string.Join(",",profile,preset.character,seed,F(data.seconds),data.success,data.normal,data.special,F(hero.CurrentHealth),F(hero.MaxHealth),peak,run.SnipersSpawned,run.AcidSporesSpawned,dashes,actives,hero.GetComponent<PrescriptionRuntime>().Claimed,F(path),F(damage),F(milestones[0]),F(milestones[1]),F(milestones[2]),F(milestones[3]),healsUsed,bombsUsed,run.StartingNobleType);
                        File.AppendAllText(output,row+"\n");completed.Add(key);Debug.Log("FESTIVAL_BALANCE "+row);
                    }
                    finally {FestivalRunController.SpecialAppeared-=spawned;}
                }
                if(!completed.SetEquals(expected))throw new Exception("Missing balance sessions at completion");
            }
            finally {Time.captureDeltaTime=previousCapture;Application.targetFrameRate=previousRate;}
        }
        static Vector2[] VisibleFuel(ShiniEmberRuntime breath,Vector2 origin)
        {
            var drops=(IEnumerable)typeof(ShiniEmberRuntime).GetField("drops",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(breath);
            var points=new System.Collections.Generic.List<Vector2>();
            foreach(var drop in drops)
            {
                Vector2 point=(Vector3)drop.GetType().GetField("position").GetValue(drop);
                if((point-origin).sqrMagnitude<16)points.Add(point);
            }
            return points.ToArray();
        }
    }
}
