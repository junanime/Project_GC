using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class PrescriptionRemakePlaySmoke
    {
        const string Key="PrescriptionRemakeSmoke";
        static double deadline;
        static bool started;
        static int checks;
        static PrescriptionRemakePlaySmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            PrescriptionRemakeInstaller.Install();
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            deadline=EditorApplication.timeSinceStartup+240;started=false;checks=0;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Check(bool ok,string message)
        {if(!ok)throw new Exception("[PrescriptionSmoke] FAIL "+message);checks++;Debug.Log("[PrescriptionSmoke] PASS "+message);}
        static FieldInfo Field(object obj,string name)
        {for(var t=obj.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null)return f;}throw new MissingFieldException(name);}
        static object Get(object obj,string name)=>Field(obj,name).GetValue(obj);
        static void Set(object obj,string name,object value)=>Field(obj,name).SetValue(obj,value);
        static void Invoke(object obj,string name,params object[] args)
        {for(var t=obj.GetType();t!=null;t=t.BaseType){var m=t.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(m!=null){m.Invoke(obj,args);return;}}throw new MissingMethodException(name);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
            if(SessionState.GetBool(Key+"Failed",false)||SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){Time.timeScale=1;EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log("[PrescriptionSmoke] FINISHED failed="+failed+" checks="+checks);EditorApplication.Exit(failed?1:0);return;
            }
            if(!EditorApplication.isPlaying||started)return;
            var level=Object.FindObjectOfType<LevelManager>();
            if(level==null||level.PlayerCharacter==null||level.PlayerCharacter.Skills==null||ApothecaryUI.Instance==null)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static IEnumerator WaitFor(Func<bool> condition, bool clearLevelUps=false)
        {
            float end=Time.realtimeSinceStartup+12;
            while(!condition())
            {
                if(clearLevelUps)
                {
                    var dialog=Object.FindObjectOfType<EntityManager>()?.AbilitySelectionDialog;
                    if(dialog!=null && dialog.MenuOpen)dialog.GetComponentsInChildren<AbilityCard>().First().Selected();
                }
                if(Time.realtimeSinceStartup>end)throw new Exception("Condition timeout");
                yield return null;
            }
            yield return null;
        }
        static void Capture(string name)
        {
            Directory.CreateDirectory(Path.GetFullPath("../work"));
            typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"prescription-"+name+".png"});
        }
        static IEnumerator Checks(LevelManager level)
        {
            yield return null;
            var player=level.PlayerCharacter;var skills=player.Skills;var q=player.GetComponent<PrescriptionRuntime>();
            var ui=ApothecaryUI.Instance;var entities=level.EntityManager;var director=Object.FindObjectOfType<MiniStageDirector>();
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            if(entities.AbilitySelectionDialog.MenuOpen)entities.AbilitySelectionDialog.Close();
            Time.timeScale=1;
            player.AddMaxHealthBonus(10000);player.GainHealth(10000);
            level.SetRunFlowPaused(true);
            Check(q!=null && skills!=null,"Player owns prescription and skill progression");
            foreach(var test in new[]{0,1,2})
            {q.StartPrescription(test,99);Check(q.Capture().quests.All(x=>x/4==test),"Three quests share tag "+test);}
            q.StartPrescription(1,42);
            var goals=q.Capture().quests;
            q.Record((PrescriptionRuntime.Goal)goals[0],1000);
            yield return null;
            ui.GetComponentsInChildren<Button>().First(b=>b.name=="Prescription toggle").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.35f);Capture("hud");
            var toggle=ui.GetComponentsInChildren<Button>().First(b=>b.name=="Prescription toggle");toggle.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.35f);Check(ui.GetComponentsInChildren<TMPro.TextMeshProUGUI>().All(t=>!t.text.Contains("<s>")),"Fold hides quest rows");
            ui.GetComponentsInChildren<Button>().First(b=>b.name=="Prescription toggle").onClick.Invoke();yield return new WaitForSecondsRealtime(.35f);
            Check(ui.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t=>t.text.Contains("<s>")),"Unfold preserves completed strikethrough");
            var originalDefinition=player.Blueprint.skills;
            foreach(CharacterSkillDefinition.SkillKind kind in Enum.GetValues(typeof(CharacterSkillDefinition.SkillKind)))
            {
                var definition=Object.Instantiate(originalDefinition);definition.kind=kind;player.Blueprint.skills=definition;
                skills.RestoreLevels(1,1);
                for(int i=0;i<4;i++){Check(skills.TryUpgrade(false),kind+" passive upgrade");Check(skills.TryUpgrade(true),kind+" active upgrade");}
                Check(!skills.TryUpgrade(false)&&!skills.TryUpgrade(true),kind+" level 5 cap");
                Check(Mathf.Abs(skills.PassivePower-2.8561f)<.001f,kind+" compound power");
                Check(!string.IsNullOrEmpty(skills.UpgradeDescription(true)),kind+" numerical preview");
                player.Blueprint.skills=originalDefinition;Object.Destroy(definition);
            }
            skills.RestoreLevels(3,4);var snapshot=player.CaptureRunSceneState();
            skills.RestoreLevels(1,1);q.StartPrescription(0,1);player.RestoreRunSceneState(snapshot);
            Check(skills.PassiveLevel==3&&skills.ActiveLevel==4&&q.Completed==1&&q.Tag==1,"Character snapshot restores quest progress and both skill levels");
            player.RestoreRunSceneState(snapshot);Check(skills.PassiveLevel==3,"Repeated restore does not compound levels");
            skills.RestoreLevels(1,1);
            var prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/MiniStages"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Select(g=>g.GetComponent<MiniStageRoomBase>()).Where(MiniStageDirector.RoomEnabled).ToArray();
            Check(prefabs.Length>=6,"Six supported room prefabs available");
            bool checkedBoundary=false;
            foreach(var prefab in prefabs)
            {
                bool enhanced=!(prefab is MiniStageCollectionRoom);
                Set(director,"roomPrefabs",new[]{prefab});Set(director,"useForcedRoomIndex",true);Set(director,"forcedRoomIndex",0);
                director.OpenEntrancePortal(player.transform.position);
                var portal=(BloodClotMiniStagePortal)Get(director,"activeEntrancePortal");var charge=portal.GetComponent<BloodClotOvercharge>();
                player.transform.position=portal.transform.position;player.GetComponent<Rigidbody2D>().position=portal.transform.position;
                if(enhanced)
                {
                    Check(charge.Begin(),"Overcharge starts for "+prefab.name);
                    if(!checkedBoundary)
                    {
                        checkedBoundary=true;
                        Set(charge,"<Kind>k__BackingField",BloodClotOvercharge.Challenge.Kills);
                        var farEnemy=entities.SpawnOverchargeElite((Vector2)portal.transform.position+Vector2.right*20);
                        Check(farEnemy!=null,"Overcharge uses existing elite blueprint");
                        farEnemy.transform.position=portal.transform.position+Vector3.right*20;
                        float beforeKill=charge.Progress;
                        farEnemy.StartCoroutine(farEnemy.Killed());
                        Check(charge.Progress==beforeKill+1,"Player inside counts an enemy killed outside the ring");
                        player.transform.position=portal.transform.position+Vector3.right*15;
                        var nearEnemy=entities.SpawnOverchargeElite(portal.transform.position);
                        nearEnemy.transform.position=portal.transform.position;
                        beforeKill=charge.Progress;nearEnemy.StartCoroutine(nearEnemy.Killed());
                        Check(charge.Progress==beforeKill,"Player outside does not count enemy killed inside ring");
                        Set(charge,"<Kind>k__BackingField",BloodClotOvercharge.Challenge.Stay);
                        yield return new WaitForSeconds(.15f);
                        Check(charge.Progress==beforeKill,"Stay progress pauses outside");
                        player.transform.position=portal.transform.position;player.GetComponent<Rigidbody2D>().position=portal.transform.position;
                        yield return new WaitForSeconds(.15f);
                        Check(charge.Progress>beforeKill,"Stay progress resumes inside using elapsed time");
                        float beforePause=charge.Progress;Time.timeScale=0;
                        yield return new WaitForSecondsRealtime(.15f);
                        Check(charge.Progress==beforePause,"Pause freezes overcharge time");Time.timeScale=1;
                        Set(charge,"<Kind>k__BackingField",BloodClotOvercharge.Challenge.Kills);
                    }
                    charge.Advance(10);
                    yield return null;Capture("overcharge");float progress=charge.Progress;
                    player.transform.position+=Vector3.right*15;charge.Advance(10);
                    Check(charge.Progress==progress,"Leaving ring pauses without reset");
                    player.transform.position=portal.transform.position;charge.Advance(1000);
                    Check(charge.Enhanced,"Reentry continues and completes charge");
                }
                director.EnterMiniStageFromPortal(portal);
                yield return WaitFor(()=>director.IsInsideMiniStage,true);
                var room=(MiniStageRoomBase)Get(director,"currentRoom");
                Check(room.Enhanced==enhanced,"Portal difficulty transfers to "+prefab.name);
                if(room is MiniStageBonusChestRoom)
                {
                    var chests=room.GetComponentsInChildren<MiniStageMysteryChestInteractable>();
                    Check(chests.All(c=>c.Outcome==MiniStageMysteryChestOutcome.TrueReward),"All enhanced mystery chests win");
                    ((MiniStageBonusChestRoom)room).NotifyMysteryChestSelected(chests[0]);
                    ((MiniStageBonusChestRoom)room).NotifyMysteryChestSelected(chests[1]);
                    Check((bool)Get(room,"selectionMade"),"Mystery selection locks after one choice");
                }
                if(room is MiniStageSniperRoom)
                {
                    var snipers=Object.FindObjectsOfType<SniperMonster>().Where(m=>m.IsMiniStageOwned&&m.HP>0).ToArray();
                    var positions=snipers.Select(s=>s.transform.position).ToArray();
                    Check(snipers.Length>0,"Sniper room spawned actors");
                    Invoke(snipers[0],"FireSniperProjectile",(Vector2)player.transform.position);
                    yield return new WaitForSeconds(.3f);
                    Check(snipers.Where((s,i)=>Vector3.Distance(s.transform.position,positions[i])>.1f).Any(),"Firing relocates sniper formation");
                }
                if(room is MiniStageFallingFoodRoom)
                {
                    Invoke(room,"SpawnOneStrike");
                    Check(Object.FindObjectsOfType<MiniStageFallingFoodStrike>().Any(s=>s.Explodes),"Enhanced food arms explosion");
                    yield return new WaitForSeconds(.3f);Capture("food");
                }
                if(room is MiniStageExplodingRushRoom)
                {
                    for(int i=0;i<4;i++)Invoke(room,"SpawnOneExplodingMonster");
                    var monsters=((IEnumerable)Get(room,"spawnedMonsters")).Cast<Monster>().ToArray();
                    Check(monsters.Length>=4,"Rush spawns mixed wave");
                    Check(monsters[3].GetComponentInChildren<SpriteRenderer>().color.g<.9f,"Every fourth bomber has fast visual marker");
                }
                if(!room.RoomCleared)Invoke(room,"CompleteRoom");
                yield return WaitFor(()=>ui.Page=="skillReward",true);
                Check(Time.timeScale==0,"Skill reward pauses combat");
                Object.FindObjectOfType<PauseMenu>()?.PlayPause();
                Check(Time.timeScale==0 && ui.Page=="skillReward","Legacy pause button cannot bypass skill reward");
                int before=skills.PassiveLevel;
                Capture("skill-choice");
                Invoke(ui,"FinishSkillReward",(object)(bool?)false);
                Invoke(ui,"FinishSkillReward",(object)(bool?)false);
                Check(skills.PassiveLevel==before+1 || before==5,"Reward click is consumed once");
                if(before==5)Invoke(ui,"FinishSkillReward",skills.ActiveLevel<5?(object)(bool?)true:null);
                yield return null;
                if(enhanced)
                {
                    var chest=(Chest)Get(room,"activeRewardChest");Check(chest!=null,"Enhanced room adds level-up chest");
                    chest.OpenChest(true);
                    yield return WaitFor(()=>entities.AbilitySelectionDialog.MenuOpen);
                    entities.AbilitySelectionDialog.GetComponentsInChildren<AbilityCard>().First().Selected();
                    yield return null;
                }
                else Check(Get(room,"activeRewardChest")==null,"Normal room gives skill choice without level-up chest");
                Check(room.CanReturnFrom(room.ReturnInteractable),"Reward completion unlocks return");
                director.ReturnToFieldFromInteractable(room.ReturnInteractable);
                yield return WaitFor(()=>!director.IsInsideMiniStage,true);
                level.SetRunFlowPaused(true);skills.RestoreLevels(1,1);
            }
            // Fully upgraded characters receive recovery, never a dead-end selection screen.
            skills.RestoreLevels(5,5);bool completed=false;ui.RequestSkillReward(()=>completed=true);
            yield return WaitFor(()=>ui.Page=="skillReward");Invoke(ui,"FinishSkillReward",new object[]{null});
            Check(completed&&ui.Page=="hud","Maxed skills recover and exit reward UI");
            q.StartPrescription(0,7);foreach(int goal in q.Capture().quests)q.Record((PrescriptionRuntime.Goal)goal,1000);
            Check(q.TryClaim(entities.AbilitySelectionDialog),"Completed prescription opens legendary reward");
            Check(!q.TryClaim(entities.AbilitySelectionDialog),"Legendary reward cannot be claimed twice");
            var offers=(IList)Get(entities.AbilitySelectionDialog,"displayedAbilities");
            Check(offers.Count==3&&offers.Cast<Ver4AugmentOffer>().All(o=>o.Kind==Ver4RewardKind.LegendaryAbility),"Exactly three legendary candidates");
            yield return new WaitForSecondsRealtime(.3f);
            Check(!((RectTransform)Get(ui,"prescriptionHud")).gameObject.activeSelf,"Prescription hides behind legendary choice modal");
            Check(!((RectTransform)Get(ui,"content")).gameObject.activeSelf,"HUD buttons cannot cover legendary choices");
            Capture("legendary");
            var sampleCard=entities.AbilitySelectionDialog.GetComponentsInChildren<AbilityCard>().First();
            var sampleName=(TMPro.TextMeshProUGUI)Get(sampleCard,"nameText");
            var sampleDescription=(TMPro.TextMeshProUGUI)Get(sampleCard,"descriptionText");
            foreach(var source in Object.FindObjectsOfType<SyringeLegendaryAugmentAbility>(true))
            {
                sampleName.text="전설 증강: "+source.Name;
                sampleDescription.text=source.Description+"\n비전 처방전 보상 · 강화 불가";
                sampleName.ForceMeshUpdate(true);sampleDescription.ForceMeshUpdate(true);
                Check(!sampleName.isTextTruncated && !sampleDescription.isTextTruncated,"Legendary text fits: "+source.Name);
            }
            entities.AbilitySelectionDialog.Close();
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
