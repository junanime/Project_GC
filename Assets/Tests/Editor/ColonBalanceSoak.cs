using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
    // Real-time automated observer. Invulnerable for continuity; never a human clear-rate claim.
    [InitializeOnLoad]
    public static class ColonBalanceSoak
    {
        const string Key="ColonBalanceSoak",Output="D:/Project_GC/work/colon-balance-soak";
        static readonly string[] Characters={"Main","Ari","Hyuki","Shini"};
        static bool started;static double deadline;
        static ColonBalanceSoak(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/errors.txt","");File.WriteAllText(Output+"/characters.txt","");File.Delete(Output+"/result.txt");
            File.WriteAllText(Output+"/samples.csv","character,stage,field_seconds,wall_seconds,level,damage,cooldown,projectiles,crit,kills,gold,items,chests,choices,population,managed_mb\n");
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Round",0);SessionState.SetBool(Key+"Failed",false);
            SessionState.SetInt(Key+"Silver",PlayerPrefs.GetInt("LobbySilverCoins",0));SessionState.SetBool(Key+"HadSilver",PlayerPrefs.HasKey("LobbySilverCoins"));
            Next();
        }
        static void Next()
        {
            SessionState.SetBool(Key+"RoundDone",false);int round=SessionState.GetInt(Key+"Round",0);
            SessionState.SetBool(Key+"NeedsScene",true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+2300;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string t,string s,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",t+"\n"+s+"\n");SessionState.SetBool(Key+"Failed",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Output+"/errors.txt","Deadline\n");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"RoundDone",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                int next=SessionState.GetInt(Key+"Round",0)+1;bool fail=SessionState.GetBool(Key+"Failed",false);
                if(next<4&&!fail){SessionState.SetInt(Key+"Round",next);Next();return;}
                if(SessionState.GetBool(Key+"HadSilver",false))PlayerPrefs.SetInt("LobbySilverCoins",SessionState.GetInt(Key+"Silver",0));else PlayerPrefs.DeleteKey("LobbySilverCoins");PlayerPrefs.Save();
                SessionState.SetBool(Key,false);File.WriteAllText(Output+"/result.txt",fail?"FAIL":"PASS: four characters, both fields, real-time combat observer");EditorApplication.Exit(fail?1:0);return;
            }
            if(!EditorApplication.isPlaying)return;
            if(SessionState.GetBool(Key+"NeedsScene",false))
            {
                SessionState.SetBool(Key+"NeedsScene",false);
                int round=SessionState.GetInt(Key+"Round",0);
                CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/"+Characters[round]+" Character Blueprint.asset");
                CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();CrossSceneData.ClearPendingRunSceneTransfer();
                UnityEngine.SceneManagement.SceneManager.LoadScene("Level 1");return;
            }
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var level=Object.FindObjectOfType<LevelManager>();if(started||level==null||!level.InitialFieldObjectsReady||level.PlayerCharacter==null)return;
            started=true;level.StartCoroutine(Observe(level,SessionState.GetInt(Key+"Round",0)));
        }
        static float OfferValue(Ability a,int round)
        {
            if(!(a is Ver4AugmentOffer o))return 1;
            if(o.Kind==Ver4RewardKind.NewSpecial)return o.Parent.Type==SyringeSpecialAugmentAbility.SpecialAugmentType.Pierce?12:7;
            if(o.Kind==Ver4RewardKind.Original)return 6;
            if(o.Kind==Ver4RewardKind.LegendaryAbility)return 12;
            return (o.Option==0?8:o.Option==3?7:o.Option==8?6:o.Option==1?4:2)+(int)o.Grade*.2f;
        }
        static IEnumerator Observe(LevelManager level,int round)
        {
            var pref=GamePreferences.Current.Copy();pref.pauseOnFocusLoss=false;pref.muteOnFocusLoss=false;GamePreferences.Apply(pref,false);
            Application.runInBackground=true;Time.timeScale=1;RunScore.Exclude("Colon real-time balance observer");
            var actor=level.PlayerCharacter;ColonContentSmoke.Set(actor,"isInvincible",true);
            if(actor.Blueprint!=CrossSceneData.CharacterBlueprint)throw new Exception("Observer character differs from selected blueprint");
            File.AppendAllText(Output+"/characters.txt",Characters[round]+"="+actor.Blueprint.name+"\n");
            var stats=Object.FindObjectOfType<StatsManager>();var dart=Object.FindObjectOfType<SyringeDartAbility>();
            var map=FiniteStomachMap.Instance;int chests=0,choices=0,purchases=0;float began=Time.realtimeSinceStartup;
            Action<Chest> opened=c=>chests++;Chest.OnAnyChestOpened+=opened;
            try
            {
                for(int stage=1;stage<=2;stage++)
                {
                    if(stage==2){actor.Move(Vector2.zero);yield return ColonContentSmoke.Enter(level);}
                    float nextSample=0,nextSkill=0,nextDash=0,nextSearch=0,nextTarget=0;Vector2 waypoint=actor.transform.position;
                    Chest targetChest=null;MerchantNPC targetShop=null;var visited=new Dictionary<MerchantNPC,int>();
                    float lastDistance=float.MaxValue,stuckSince=Time.time;
                    while(level.CurrentLevelTime<885)
                    {
                        if(level.IsLevelEnded)throw new Exception("Observer ended unexpectedly");
                        var dialog=level.EntityManager.AbilitySelectionDialog;
                        if(dialog.MenuOpen)
                        {
                            actor.Move(Vector2.zero);
                            var offers=(List<Ability>)ColonContentSmoke.Field(dialog,"displayedAbilities").GetValue(dialog);
                            var selected=offers?.Where(a=>a!=null).OrderByDescending(a=>OfferValue(a,round)).FirstOrDefault();
                            if(selected!=null){selected.Select();choices++;dialog.Close();}
                            yield return new WaitForSecondsRealtime(.15f);continue;
                        }
                        var ui=ApothecaryUI.Instance;if(ui!=null&&ui.Page!="hud")
                        {
                            if(ui.Page=="supplyRelic"||ui.Page=="merchant"||ui.Page=="bossChoice")ui.Back();
                            else if(ui.Page=="skillReward")
                            {
                                var buttons=ui.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.interactable&&b.gameObject.activeInHierarchy).ToArray();
                                if(buttons.Length>0)buttons[0].onClick.Invoke();
                            }
                            else ui.Show("hud");
                            yield return new WaitForSecondsRealtime(.15f);continue;
                        }
                        if(actor.IsTrapBound)
                        {
                            var trap=Object.FindObjectsOfType<TrapMonster>().FirstOrDefault(t=>t.IsActive&&t.EscapeTarget>=0);
                            if(trap!=null){var method=typeof(TrapMonster).GetMethod("HandleArrowInput",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);method.Invoke(trap,new[]{Enum.ToObject(method.GetParameters()[0].ParameterType,trap.EscapeTarget)});}
                        }
                        var position=(Vector2)actor.transform.position;
                        if(Time.time>=nextSearch)
                        {
                            nextSearch=Time.time+.6f;
                            targetChest=Object.FindObjectsOfType<Chest>().Where(c=>c.CanInteract&&Vector2.Distance(c.transform.position,position)<14).OrderBy(c=>Vector2.Distance(c.transform.position,position)).FirstOrDefault();
                            targetShop=Object.FindObjectsOfType<MerchantNPC>().Where(n=>n.CanInteract&&(!visited.TryGetValue(n,out int gold)||stats.CoinsGained>=gold+25)&&Vector2.Distance(n.transform.position,position)<14).OrderBy(n=>Vector2.Distance(n.transform.position,position)).FirstOrDefault();
                        }
                        float cd=targetChest==null?float.MaxValue:Vector2.Distance(position,targetChest.transform.position),sd=targetShop==null?float.MaxValue:Vector2.Distance(position,targetShop.transform.position);
                        if(cd<sd)waypoint=targetChest.transform.position;else if(targetShop!=null)waypoint=targetShop.transform.position;
                        else if(Vector2.Distance(position,waypoint)<2||Time.time>=nextTarget)
                        {
                            for(int i=0;i<200;i++){var r=map.WorldRect;waypoint=new Vector2(UnityEngine.Random.Range(r.xMin,r.xMax),UnityEngine.Random.Range(r.yMin,r.yMax));if(map.NavigationDistance(waypoint,out _)>=12)break;}
                            nextTarget=Time.time+45;
                        }
                        if(cd<1.25f){targetChest.OpenChest();targetChest=null;}
                        if(sd<1.8f&&targetShop.TryOpenShop())
                        {
                            actor.Move(Vector2.zero);visited[targetShop]=stats.CoinsGained;
                            var item=targetShop.GetShopItems().Where(i=>!OctoberItemRuntime.Get(actor).Has(i.octoberId)&&RelicRuntime.Price(actor,i.cost)<=stats.CoinsGained).OrderBy(i=>i.cost).FirstOrDefault();
                            if(item!=null){MerchantUIManager.Instance.OnClickPurchaseItem(item,null);purchases++;}else MerchantUIManager.Instance.CloseShop();
                            targetShop=null;yield return new WaitForSecondsRealtime(.2f);continue;
                        }
                        var pickup=level.EntityManager.MagneticCollectables.Where(c=>c!=null&&!c.IsBeingCollected&&Vector2.Distance(c.transform.position,position)<3).OrderBy(c=>Vector2.Distance(c.transform.position,position)).FirstOrDefault();
                        if(pickup!=null)waypoint=pickup.transform.position;
                        float distance=Vector2.Distance(position,waypoint);if(distance<lastDistance-.05f)stuckSince=Time.time;lastDistance=distance;
                        if(Time.time-stuckSince>8){nextTarget=0;targetChest=null;targetShop=null;waypoint=position;stuckSince=Time.time;}
                        actor.Move(stage==2?ColonNavigation.Toward(position,waypoint):(waypoint-position).normalized);
                        if(level.CurrentLevelTime>=nextSkill){actor.Skills.TryActivate();nextSkill=level.CurrentLevelTime+6;}
                        if(level.CurrentLevelTime>=nextDash){actor.TryDash();nextDash=level.CurrentLevelTime+12;}
                        if(level.CurrentLevelTime>=nextSample)
                        {
                            nextSample=level.CurrentLevelTime+30;
                            File.AppendAllText(Output+"/samples.csv",$"{Characters[round]},{stage},{level.CurrentLevelTime:F1},{Time.realtimeSinceStartup-began:F1},{actor.CurrentLevel},{dart.GetEffectiveDamage():F3},{dart.GetEffectiveCooldown():F3},{dart.GetEffectiveProjectileCount()},{actor.CritChance:F3},{stats.MonstersKilled},{stats.CoinsGained},{OctoberItemRuntime.Get(actor).State.owned.Count},{chests},{choices},{level.EntityManager.LivingMonsters.Count},{UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()/1048576}\n");
                            File.WriteAllText(Output+"/status.txt",$"{Characters[round]} stage={stage} time={level.CurrentLevelTime:F1} wall={Time.realtimeSinceStartup-began:F1}");
                        }
                        yield return new WaitForSecondsRealtime(.15f);
                    }
                    var snapshot=actor.CaptureRunSceneState();File.WriteAllText(Output+"/"+Characters[round]+"-stage"+stage+"-player.json",JsonUtility.ToJson(snapshot,true));
                    var full = new RunSceneTransferSnapshot { CharacterState=snapshot, AbilityStates=Object.FindObjectOfType<AbilityManager>().CaptureRunSceneAbilities(), OctoberItems=OctoberItemRuntime.Get(actor).State, CoinsGained=stats.CoinsGained };
                    File.WriteAllText(Output+"/"+Characters[round]+"-stage"+stage+"-build.json",JsonUtility.ToJson(full,true));
                }
                SessionState.SetBool(Key+"RoundDone",true);
            }
            finally{Chest.OnAnyChestOpened-=opened;actor.Move(Vector2.zero);}
        }
    }
}
