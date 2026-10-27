using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using InputKey = UnityEngine.InputSystem.Key;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class FestivalDemoSmoke
    {
        const string Key = "FestivalDemoSmoke";
        const string Output = "Library/FestivalQA";
        static bool started;
        static double deadline;
        static int checks;
        static FestivalDemoSmoke() { if (SessionState.GetBool(Key,false)) Attach(); }
        public static void RunAndBuild()
        {
            Run();SessionState.SetBool(Key+"Build",true);
        }
        public static void RunLaunchAndBuild()
        {
            RunAndBuild();SessionState.SetBool(Key+"LaunchOnly",true);
        }
        public static void RunQaAndBuild()
        {
            RunAndBuild();SessionState.SetBool(Key+"QaOnly",true);
        }
        public static void RunQa()
        {
            Run();SessionState.SetBool(Key+"QaOnly",true);
        }
        public static void RunUiAndBuild()
        {
            RunAndBuild();SessionState.SetBool(Key+"UiOnly",true);
        }
        public static void RunUi()
        {
            Run();SessionState.SetBool(Key+"UiOnly",true);
        }
        public static void RunBalance()
        {
            Run();SessionState.SetBool(Key+"Balance",true);
        }
        public static void RunLaunch()
        {
            Run();SessionState.SetBool(Key+"LaunchOnly",true);
        }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"/errors.txt", "");
            File.WriteAllText(Output+"/combat.csv", "character,seconds,success,kills,peakEnemies,meanFrameMs,p95FrameMs,health,noble\n");
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Fail",false);
            SessionState.SetBool(Key+"Build",false);
            SessionState.SetBool(Key+"UiOnly",false);
            SessionState.SetBool(Key+"LaunchOnly",false);SessionState.SetBool(Key+"Balance",false);
            SessionState.SetBool(Key+"QaOnly",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,761);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+10800;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string trace,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            File.AppendAllText(Output+"/errors.txt",text+"\n"+trace+"\n");SessionState.SetBool(Key+"Fail",true);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Output+"/errors.txt","Timeout\n");SessionState.SetBool(Key+"Fail",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Fail",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Fail",false);SessionState.SetBool(Key,false);
                File.WriteAllText(Output+"/result.txt",$"failed={failed} checks={checks}\n");
                Debug.Log($"FESTIVAL_FINISHED failed={failed} checks={checks}");
                if(!failed&&SessionState.GetBool(Key+"Build",false)){SessionState.SetBool(Key+"Build",false);Vampire.Editor.FestivalDemoBuild.Run();}
                else EditorApplication.Exit(failed?1:0);
                return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {
                started=true;var host=new GameObject("Festival QA").AddComponent<FestivalDemoTestHost>();
                Object.DontDestroyOnLoad(host);host.StartCoroutine(Checks());
            }
        }
        static void Check(bool condition,string message)
        {
            if(!condition)throw new Exception("FESTIVAL_FAIL "+message);
            checks++;Debug.Log("FESTIVAL_PASS "+message);
        }
        static void Set(object target,string field,object value) => target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        static float Exp(Character hero,string field) => (float)typeof(Character).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hero);
        static List<Ability> Displayed(AbilitySelectionDialog dialog) => (List<Ability>)typeof(AbilitySelectionDialog).GetField("displayedAbilities",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(dialog);
        public static IEnumerator ContinueGaugeGuide()
        {
            if(FestivalGaugeTutorial.Instance!=null)FestivalGaugeTutorial.Instance.Dismiss();
            while(FestivalGaugeTutorial.IsOpen)yield return null;
            yield return null;yield return null;
        }
        static IEnumerator KeyboardStateFor(Keyboard keyboard,params InputKey[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            yield return null;yield return null;
        }
        static IEnumerator ActiveSkillInputChecks(LevelManager level)
        {
            var hero=level.PlayerCharacter;var skill=hero.Skills;
            var background=InputSystem.settings.backgroundBehavior;
            var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            var invincibleField=typeof(Character).GetField("isInvincible",BindingFlags.Instance|BindingFlags.NonPublic);
            bool invincible=(bool)invincibleField.GetValue(hero),paused=level.IsRunFlowPaused;
            int fuel=skill.Breath!=null?skill.Breath.Fuel:0;
            var mode=FestivalDemo.EnabledOverride;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>();
            level.SetRunFlowPaused(true);invincibleField.SetValue(hero,true);
            try
            {
                yield return KeyboardStateFor(keyboard,InputKey.R);
                Check(!skill.Active&&skill.CooldownRemaining==0,"old R key does not activate festival skill");
                yield return KeyboardStateFor(keyboard);
                yield return KeyboardStateFor(keyboard,InputKey.Space);
                Check(skill.Active,"physical Space press activates "+hero.Blueprint.name);
                if(skill.IsShini)
                {
                    Check(skill.Breath.Fuel==fuel-ShiniEmberRuntime.InitialCost,"Space starts Shini with one initial fuel charge");
                    yield return new WaitForSeconds(ShiniEmberRuntime.Preparation+ShiniEmberRuntime.ShortDuration+.6f);
                    Check(skill.Breath.Emitting&&skill.Breath.Fuel<fuel-ShiniEmberRuntime.InitialCost,"held Space sustains Shini beyond short cast and consumes fuel");
                    var dialog=level.EntityManager.AbilitySelectionDialog;
                    dialog.Open();int pausedFuel=skill.Breath.Fuel;
                    yield return KeyboardStateFor(keyboard);
                    yield return new WaitForSecondsRealtime(.1f);
                    Check(skill.Breath.Busy&&skill.Breath.Fuel==pausedFuel,"Space release is observed while reward freezes Shini");
                    dialog.Close();yield return new WaitForSeconds(.8f);
                    Check(!skill.Breath.Busy,"released Space does not latch Shini after reward closes");
                }
                else
                {
                    yield return KeyboardStateFor(keyboard);
                    Check(skill.Active&&skill.CooldownRemaining>0,"Space release preserves normal active duration and cooldown");
                }
                skill.EndRun();skill.Breath?.RestoreFuel(fuel);
                FestivalDemo.EnabledOverride=false;
                yield return KeyboardStateFor(keyboard,InputKey.R);
                Check(!skill.Active,"ordinary mode ignores the old R binding");
                yield return KeyboardStateFor(keyboard);
                yield return KeyboardStateFor(keyboard,InputKey.Space);
                Check(skill.Active,"ordinary mode also uses Space: "+hero.Blueprint.name);
                yield return KeyboardStateFor(keyboard);
            }
            finally
            {
                skill.EndRun();skill.Breath?.RestoreFuel(fuel);
                FestivalDemo.EnabledOverride=mode;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior=background;
                InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
                invincibleField.SetValue(hero,invincible);level.SetRunFlowPaused(paused);
            }
        }
        static void OriginalOfferChecks(Ver4AugmentRuntime runtime,bool checkOrdinaryMode)
        {
            // Only the starting character is necessarily unlocked in the ordinary save.
            if(checkOrdinaryMode)
            {
                FestivalDemo.EnabledOverride=false;
                var normal=runtime.CreateOffers(false,3,true);
                var original=normal.OfType<Ver4AugmentOffer>().FirstOrDefault(o=>o.Kind==Ver4RewardKind.Original);
                Check(original!=null&&runtime.HasEligibleOriginal,"normal mode retains original rewards");
                FestivalDemo.EnabledOverride=true;
                Check(!runtime.HasEligibleOriginal&&!runtime.Apply(original),"festival rejects original rewards even if already generated");
                foreach(var offer in normal)Object.Destroy(offer.gameObject);
            }
            bool valid=true;
            for(int i=0;i<32;i++)
            {
                var offers=runtime.CreateOffers(false,3,i%2==0);
                valid&=offers.Count==3&&offers.OfType<Ver4AugmentOffer>().All(o=>o.Kind!=Ver4RewardKind.Original&&o.Grade!=AugmentUpgradeGrade.Original);
                foreach(var offer in offers)Object.Destroy(offer.gameObject);
            }
            Check(valid,"festival normal and guaranteed offer pools exclude originals and retain three choices");
            FestivalDemo.EnabledOverride=null;
        }
        static IEnumerator ChestScheduleChecks(LevelManager level)
        {
            var hero=level.PlayerCharacter;
            Vector3 origin=hero.transform.position;
            hero.transform.position=FiniteStomachMap.SafeSpawn((Vector2)origin+Vector2.right*20,.7f);
            Physics2D.SyncTransforms();
            foreach(float due in new[]{15f,30f})
            {
                int count=Object.FindObjectsOfType<Chest>().Length;
                Set(level,"levelTime",due-.5f);
                yield return new WaitForSecondsRealtime(.1f);
                Check(Object.FindObjectsOfType<Chest>().Length==count,"no early item chest before "+due);
                level.SetRunFlowPaused(true);Set(level,"levelTime",due);
                yield return new WaitForSecondsRealtime(.1f);
                Check(Object.FindObjectsOfType<Chest>().Length==count,"paused run cannot spawn due chest at "+due);
                level.SetRunFlowPaused(false);
                var previous=new HashSet<Chest>(Object.FindObjectsOfType<Chest>());
                float spawnDeadline=Time.realtimeSinceStartup+1;
                while(Object.FindObjectsOfType<Chest>().Length==count&&Time.realtimeSinceStartup<spawnDeadline)yield return null;
                var added=Object.FindObjectsOfType<Chest>().Where(c=>!previous.Contains(c)).ToArray();
                if(added.Length!=1)Capture("chest-schedule-failure");
                Check(added.Length==1,$"exactly one scheduled item chest at {due}; added={added.Length}, time={level.CurrentLevelTime}, alive={hero.IsAlive}, paused={level.IsRunFlowPaused}, scale={Time.timeScale}, menu={level.EntityManager.AbilitySelectionDialog.MenuOpen}");
                float distance=Vector2.Distance(hero.transform.position,added[0].transform.position);
                Check(distance>=3.9f&&distance<=7.1f,"scheduled chest follows current player within four to seven units");
                yield return new WaitForSecondsRealtime(.1f);
                Check(Object.FindObjectsOfType<Chest>().Length==count+1,"due chest is not duplicated on following frames");
            }
            hero.transform.position=origin;Physics2D.SyncTransforms();
        }
        static IEnumerator SelectLevelReward(LevelManager level)
        {
            var dialog=level.EntityManager.AbilitySelectionDialog;
            float timer=level.CurrentLevelTime;
            var runtime=Object.FindObjectOfType<AbilityManager>().Ver4;
            int before=runtime.CaptureRunSceneConditionalAugmentIds().Count;
            yield return new WaitForSecondsRealtime(.35f);
            Check(Time.timeScale==0&&Mathf.Approximately(timer,level.CurrentLevelTime),"augment selection pauses combat timer");
            var cards=dialog.GetComponentsInChildren<AbilityCard>().Where(c=>c.gameObject.activeInHierarchy).ToArray();
            Check(cards.Length==3,"level-up offers three augment choices");
            Check(Displayed(dialog).OfType<Ver4AugmentOffer>().All(o=>o.Kind!=Ver4RewardKind.Original),"level-up panel excludes original augments");
            dialog.RerollAbilities();yield return new WaitForSecondsRealtime(.6f);
            Check(Displayed(dialog).Count==3&&Displayed(dialog).OfType<Ver4AugmentOffer>().All(o=>o.Kind!=Ver4RewardKind.Original),"reroll keeps three non-original choices");
            if(SessionState.GetBool(Key+"UiOnly",false))Capture(level.PlayerCharacter.Blueprint.name+"-levelup");
            var selectedOffers=Displayed(dialog);int selectedLevel=level.PlayerCharacter.CurrentLevel;
            cards[0].Selected();
            float deadline=Time.realtimeSinceStartup+5;
            while(dialog.SelectionAnimating&&Time.realtimeSinceStartup<deadline)yield return null;
            // Collected XP can open the next level reward on the same frame the old one closes.
            // Require a distinct offer set AND another real level, not merely any still-open menu.
            bool nextReward=dialog.MenuOpen&&!ReferenceEquals(selectedOffers,Displayed(dialog))&&level.PlayerCharacter.CurrentLevel>selectedLevel;
            bool resumed=!dialog.MenuOpen&&Time.timeScale>0;
            if(dialog.SelectionAnimating||(!resumed&&!nextReward))Capture("selection-resume-failure");
            Check(!dialog.SelectionAnimating&&(resumed||(nextReward&&Time.timeScale==0)),"selection completes and resumes combat or advances to a distinct queued level reward");
            Check(runtime.CaptureRunSceneConditionalAugmentIds().Count==before+1,"selected augment is actually acquired");
        }
        static IEnumerator ExperienceChecks(LevelManager level)
        {
            var hero=level.PlayerCharacter;
            float savedExp=Exp(hero,"currentExp"),savedNext=Exp(hero,"nextLevelExp");
            level.SetRunFlowPaused(true);
            Set(hero,"nextLevelExp",100000f);
            foreach(bool festival in new[]{true,false})
            {
                FestivalDemo.EnabledOverride=festival;
                foreach(GemType type in Enum.GetValues(typeof(GemType)))
                {
                    float before=Exp(hero,"currentExp");
                    level.EntityManager.SpawnExpGem((Vector2)hero.transform.position+Vector2.right*100,type,false).Use();
                    yield return new WaitForSecondsRealtime(.1f);
                    float expected=(float)type*(festival?0f:1f)*hero.ExperienceMultiplier;
                    Check(Mathf.Abs(Exp(hero,"currentExp")-before-expected)<.001f,$"{type} actual orb XP, festival={festival}: {expected}");
                }
            }
            FestivalDemo.EnabledOverride=null;
            Set(hero,"currentExp",savedExp);Set(hero,"nextLevelExp",savedNext);
            level.SetRunFlowPaused(false);
        }
        static IEnumerator Checks()
        {
            Application.runInBackground=true;
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            yield return null;yield return null;
            Check(FestivalDemo.Enabled,"festival branch defaults enabled");
            Check(!IntroMovie.Active&&Object.FindObjectsOfType<IntroMovie>(true).Length==0,"festival launch creates no intro movie");
            Check(!IntroMovie.BlocksMenuInput,"launch accepts input without skip or confirmation");
            Check(ApothecaryUI.Instance.Page=="festival","launch opens character selection directly");
            Check(Object.FindObjectsOfType<Button>().Count(b=>b.name.StartsWith("Festival character "))==4,"four character cards");
            Check(Object.FindObjectsOfType<TMP_Text>().Any(t=>t.text.Contains("Space 액티브")),"character selection explains the Space active key");
            Check(Application.targetFrameRate==60&&QualitySettings.vSyncCount==0,"PC starts with consistent sixty FPS cap");
            ApothecaryUI.Instance.Back();Check(ApothecaryUI.Instance.Page=="settings","Escape opens settings from festival selection");
            ApothecaryUI.Instance.Back();Check(ApothecaryUI.Instance.Page=="festival","settings returns to festival selection");
            Capture("selection");
            foreach(var preset in FestivalDemo.Settings.presets)
            {
                var card=Object.FindObjectsOfType<Button>().First(b=>b.name=="Festival character "+preset.character);
                card.onClick.Invoke();yield return null;yield return null;Capture("selection-"+preset.character);
            }
            if(SessionState.GetBool(Key+"QaOnly",false))
            {
                FestivalQaSmoke.DataChecks();
                FestivalQaSmoke.BufferChecks();
                var qaButton=ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="QA 데이터"));
                qaButton.onClick.Invoke();yield return null;yield return null;
                Check(ApothecaryUI.Instance.Page=="festivalQa","QA operator button opens summary");Capture("qa-summary");
                var export=ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="QA 내보내기"));
                export.onClick.Invoke();yield return null;
                Check(ApothecaryUI.Instance.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.StartsWith("완료:")),"operator export button creates ZIP");Capture("qa-export");
                ApothecaryUI.Instance.Back();Check(ApothecaryUI.Instance.Page=="festival","QA back returns to selection");
                yield return FestivalOctober27Checks.Run();
                ApothecaryUI.Instance.Show("festivalQa");yield return null;yield return null;Capture("qa-after-run");
                SessionState.SetBool(Key+"Done",true);yield break;
            }
            if(SessionState.GetBool(Key+"LaunchOnly",false)){SessionState.SetBool(Key+"Done",true);yield break;}
            StorageChecks();
            if(SessionState.GetBool(Key+"Balance",false))
            {
                if(Environment.GetEnvironmentVariable("FESTIVAL_BALANCE_VERIFY")=="1")
                    yield return FestivalOctober27Checks.Run();
                yield return FestivalBalanceSessions.Run();SessionState.SetBool(Key+"Done",true);yield break;
            }
            var config=FestivalDemo.Catalog;
            int savedSilver=SilverWallet.Silver;
            string equipped=RelicSaveData.GetEquippedRelicId();
            foreach(var preset in FestivalDemo.Settings.presets)
            {
                if(SessionState.GetBool(Key+"UiOnly",false))break;
                var blueprint=config.characters.First(c=>OctoberArt.CharacterKey(c)==preset.character);
                Check(FestivalDemo.Validate(blueprint)==null,"valid preset "+preset.character);
                CrossSceneData.CharacterBlueprint=blueprint;CrossSceneData.ClearStartingLobbyItems();
                float start=Time.realtimeSinceStartup;
                Check(StageEntryLoading.Begin(blueprint),"load "+preset.character);
                Check(!StageEntryLoading.Begin(blueprint),"duplicate load blocked");
                while(StageEntryLoading.IsLoading)
                {
                    var loadingLevel=Object.FindObjectOfType<LevelManager>();
                    if(loadingLevel!=null && loadingLevel.CurrentLevelTime!=0)throw new Exception("Timer advanced during loading");
                    yield return null;
                }
                Check(Time.realtimeSinceStartup-start>=3,"loading presentation lasts at least three seconds");
                yield return ContinueGaugeGuide();
                yield return null;
                var level=Object.FindObjectOfType<LevelManager>();
                var player=level.PlayerCharacter;
                var abilities=Object.FindObjectOfType<AbilityManager>();
                Check(player.Blueprint==blueprint,"character identity "+preset.character);
                var owned=abilities.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).Where(a=>a.Owned).Select(a=>(int)a.Type).OrderBy(i=>i).ToArray();
                Check(owned.SequenceEqual(preset.weapons.OrderBy(i=>i)),"exactly four starting effects "+preset.character);
                Check(player.Relics.Equipped.relicId==preset.relic,"preset relic "+preset.character);
                Check(player.GetComponent<OctoberItemRuntime>().State.owned.OrderBy(i=>i).SequenceEqual(preset.items.OrderBy(i=>i)),"preset items applied once");
                FestivalDemo.ApplyWeapons(abilities,blueprint);
                Check(abilities.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).Count(a=>a.Owned)==4,"starting effects are idempotent");
                Check(Object.FindObjectsOfType<StageEventDirector>().Sum(d=>d.FestivalScheduledEventCount)==1,"exactly one scheduled event");
                Check(level.LevelDuration==45&&player.Skills.EffectiveCooldown<=24,"45-second run and usable active cooldown");
                Check(RunScore.Current.Data.rules==FestivalDemo.Rules&&RunScore.Current.FestivalRunId!=null,"separate festival score session");
                float nextDash=3,nextActive=1;int peak=0;bool captured=false;
                var samples=new List<float>();
                while(!level.IsLevelEnded)
                {
                    float t=level.CurrentLevelTime;
                    var direction=new Vector2(Mathf.Cos(t*.45f),Mathf.Sin(t*.45f));
                    Monster nearest=null;float distance=float.MaxValue;
                    foreach(var monster in level.EntityManager.LivingMonsters)
                    {
                        if(monster==null)continue;
                        float d=((Vector2)(monster.transform.position-player.transform.position)).sqrMagnitude;
                        if(d<distance){distance=d;nearest=monster;}
                    }
                    if(nearest!=null&&distance<25)direction=((Vector2)(player.transform.position-nearest.transform.position)).normalized;
                    player.Move(direction);player.LookDirection=nearest!=null?((Vector2)(nearest.transform.position-player.transform.position)).normalized:direction;
                    if(t>=nextDash){player.TryDash();nextDash=t+5;}
                    if(t>=nextActive){player.Skills.TryActivate();nextActive=t+3;}
                    peak=Mathf.Max(peak,level.EntityManager.LivingMonsters.Count);
                    if(t>2)samples.Add(Time.unscaledDeltaTime*1000);
                    if(!captured&&t>=27){Capture(preset.character+"-combat");captured=true;}
                    if(level.EntityManager.AbilitySelectionDialog.MenuOpen)yield return SelectLevelReward(level);
                    yield return null;
                }
                player.Move(Vector2.zero);
                samples.Sort();
                var data=RunScore.Current.Data;
                int kills=data.normal+data.special+data.elite;
                File.AppendAllText(Output+"/combat.csv",$"{preset.character},{data.seconds:F2},{data.success},{kills},{peak},{(samples.Count>0?samples.Average():0):F2},{(samples.Count>0?samples[(int)((samples.Count-1)*.95f)]:0):F2},{player.CurrentHealth:F1},{player.GetComponent<PrescriptionRuntime>().Claimed}\n");
                Check(peak<=FestivalDemo.Settings.populationLimit,"population cap respected");
                int specials=level.GetComponent<FestivalRunController>().SpecialSpawned;
                Debug.Log("FESTIVAL_SPECIALS "+preset.character+" count="+specials);
                Check(data.seconds<8.2f||specials>0,"scheduled special enemies actually spawn");
                Check(data.seconds<=45&&RunScore.Current.Finished,"result freezes score and duration");
                Capture(preset.character+"-result");
                Time.timeScale=1;SceneManager.LoadScene(0);yield return null;yield return null;
                while(ApothecaryUI.Instance==null||ApothecaryUI.Instance.Page!="festival")yield return null;
            }
            // Deterministic lifecycle checks cover every preset regardless of bot survival.
            foreach(var preset in FestivalDemo.Settings.presets)
            {
                Time.timeScale=1;SceneManager.LoadScene(0);yield return null;yield return null;
                var chosen=config.characters.First(c=>OctoberArt.CharacterKey(c)==preset.character);
                CrossSceneData.CharacterBlueprint=chosen;StageEntryLoading.Begin(chosen);
                while(StageEntryLoading.IsLoading)yield return null;
                yield return ContinueGaugeGuide();
                yield return null;
                var proof=Object.FindObjectOfType<LevelManager>();var hero=proof.PlayerCharacter;
                var initialChests=Object.FindObjectsOfType<Chest>();
                Check(initialChests.Length==1&&initialChests[0].CanInteract&&Vector2.Distance(initialChests[0].transform.position,hero.transform.position)<=2.1f,"one usable item chest beside player at start: "+preset.character);
                Check(!proof.CurrentLevelBlueprint.chestBlueprint.abilityChest,"scheduled chest uses existing item loot");
                yield return ActiveSkillInputChecks(proof);
                OriginalOfferChecks(Object.FindObjectOfType<AbilityManager>().Ver4,preset==FestivalDemo.Settings.presets[0]);
                if(preset==FestivalDemo.Settings.presets[0])yield return ExperienceChecks(proof);
                int previousLevel=hero.CurrentLevel;
                var originals=Object.FindObjectOfType<AbilityManager>().Ver4;
                Check(originals.CaptureRunSceneConditionalAugmentIds().Count==12,"exactly twelve initial originals: "+preset.character);
                foreach(int id in preset.weapons)
                    for(int option=0;option<3;option++)
                        Check(originals.Progress.Count(((SyringeSpecialAugmentAbility.SpecialAugmentType)id).ToString(),option)==1,"original acquired once, not upgraded: "+id+"/"+option);
                hero.GainExp(100000);
                for(int i=0;i<20;i++)proof.EntityManager.SpawnExpGem((Vector2)hero.transform.position+Vector2.right*100,GemType.White1,false).Use();
                yield return new WaitForSecondsRealtime(.3f);
                Check(hero.CurrentLevel==previousLevel&&!proof.EntityManager.AbilitySelectionDialog.MenuOpen,"direct and pickup XP cannot level up: "+preset.character);
                var quest=hero.GetComponent<PrescriptionRuntime>();
                Check(quest.Claimed&&!proof.EntityManager.AbilitySelectionDialog.MenuOpen,"noble present without quest or picker: "+preset.character);
                var manager=Object.FindObjectOfType<AbilityManager>();
                var ownedNobles=manager.GetComponentsInChildren<SyringeLegendaryAugmentAbility>(true).Where(a=>a.Owned).ToArray();
                Check(ownedNobles.Length==1&&FestivalDemo.StartingNobleAllowed(ownedNobles[0]),"one eligible starting noble: "+preset.character);
                Check(!quest.TryAutomaticReward(),"quest cannot add another noble");
                if(preset==FestivalDemo.Settings.presets[0])
                {
                    yield return FestivalRosterChecks.Run(proof);
                    yield return ChestScheduleChecks(proof);
                }
                if(preset==FestivalDemo.Settings.presets[0])Check(proof.GetComponent<FestivalRunController>().SpecialSpawned>=3,"special waves survive real pool resolution and timed spawning");
                int chestsAtEnd=Object.FindObjectsOfType<Chest>().Length;
                Set(proof,"levelTime",44.95f);yield return new WaitForSecondsRealtime(.25f);
                Check(proof.IsLevelEnded&&RunScore.Current.Data.success&&Mathf.Approximately(RunScore.Current.Data.seconds,45),"timer expiry succeeds exactly at forty-five seconds");
                if(preset==FestivalDemo.Settings.presets[0])Check(Object.FindObjectsOfType<Chest>().Length==chestsAtEnd,"no extra chest at forty-five-second time over");
                chestsAtEnd=Object.FindObjectsOfType<Chest>().Length;
                yield return new WaitForSecondsRealtime(.1f);
                Check(Object.FindObjectsOfType<Chest>().Length==chestsAtEnd,"no extra chest after run ended");
                proof.GameOver();Check(RunScore.Current.Data.success,"late death cannot overwrite finished success");
            }
            Check(SilverWallet.Silver==savedSilver&&RelicSaveData.GetEquippedRelicId()==equipped,"ordinary currency and equipped relic unchanged");
            var ui=ApothecaryUI.Instance;
            var fields=ui.GetComponentsInChildren<TMP_InputField>();
            Check(fields.Length==2,"department and name entry fields");
            fields[0].text="컴퓨터공학과";fields[1].text="검증 참가자";
            ui.Show("festivalRanking");yield return null;Capture("ranking");
            ui.Back();yield return null;
            Check(ui.Page=="result","Escape returns from in-run ranking to result");
            fields=ui.GetComponentsInChildren<TMP_InputField>();
            Check(fields.Length==2&&fields[0].text=="컴퓨터공학과"&&fields[1].text=="검증 참가자","identity input survives ranking round trip");
            Check(fields.All(f=>f.placeholder!=null&&!f.placeholder.enabled),"restored text hides placeholder hints");
            fields[0].text="";Check(fields[0].placeholder.enabled,"clearing input restores placeholder hint");
            fields[0].text="컴퓨터공학과";
            Capture("result-input");
            string ledger=Path.GetFullPath(Output+"/ui-ledger-"+Guid.NewGuid().ToString("N")+".json");
            FestivalLeaderboard.TestFilePath=ledger;
            for(int i=0;i<35;i++)
                Check(FestivalLeaderboard.Save(new FestivalRecord{runId="qa-"+i,name="참가자"+i,department="검증학과",character="아시",score=50000-i*50,seconds=45,success=i<8,utc=i.ToString("D3")},out _),"UI fixture record "+i);
            var submit=ui.GetComponentsInChildren<Button>().First(b=>b.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="기록 등록"));
            submit.onClick.Invoke();yield return null;yield return null;
            Check(ui.Page=="festivalRanking","saving record immediately opens leaderboard");
            Check(ui.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Current festival record"),"just saved record pinned above ranked rows");
            Check(ui.GetComponentsInChildren<RectTransform>().Count(r=>r.name.StartsWith("Festival rank "))==36,"all records rendered, not a top ten cutoff");
            Check(ui.GetComponentsInChildren<RectTransform>().Count(r=>r.name.StartsWith("Medal "))>=3,"gold silver bronze medal marks");
            var rankingScroll=ui.GetComponentsInChildren<ScrollRect>().First(r=>r.name=="Festival ranking scroll");
            Capture("ranking-top");rankingScroll.verticalNormalizedPosition=0;yield return null;
            Check(rankingScroll.content.anchoredPosition.y>100,"leaderboard scroll reaches later participants");Capture("ranking-bottom");
            FestivalLeaderboard.TestFilePath=null;
            SessionState.SetBool(Key+"Done",true);
        }
        static void StorageChecks()
        {
            string path=Path.GetFullPath(Output+"/ledger-"+Guid.NewGuid().ToString("N")+".json");
            var record=new FestivalRecord{runId="one",name="검증 참가자",department="검증 학과",score=10,seconds=30,utc="1"};
            Check(FestivalLeaderboard.Save(record,out _,path),"first participant saved");
            Check(FestivalLeaderboard.Save(record,out _,path)&&FestivalLeaderboard.Read(path).Count==1,"duplicate run submission is idempotent");
            Check(FestivalLeaderboard.Save(new FestivalRecord{runId="two",name="다음 참가자",department="다른 학과",score=5,seconds=60,success=true,utc="2"},out _,path),"shared PC retains a second participant");
            Check(FestivalLeaderboard.Read(path).First().runId=="two","completion ranks ahead of noncompletion");
            Check(!FestivalLeaderboard.Save(new FestivalRecord{runId="bad",name="",department="학과"},out _,path),"blank identity rejected");
            File.WriteAllText(path,"invalid-json");
            Check(FestivalLeaderboard.Read(path).Count==1,"previous committed backup survives corrupt primary");
        }
        static void Capture(string name)
        {
            var camera=Camera.main;if(camera==null)return;
            int width=Mathf.Max(1,Screen.width),height=Mathf.Max(1,Screen.height);
            var canvases=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).OrderBy(c=>c.sortingOrder).ToArray();
            var oldCameras=canvases.Select(c=>c.worldCamera).ToArray();var oldDistances=canvases.Select(c=>c.planeDistance).ToArray();
            var oldLayers=canvases.Select(c=>c.sortingLayerID).ToArray();var oldOrders=canvases.Select(c=>c.sortingOrder).ToArray();
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var texture=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);Texture2D pixels=null;
            try
            {
                camera.targetTexture=texture;
                // Overlay UI normally renders after every world sorting layer. Preserve that
                // relationship when rendering the test screenshot through a camera.
                int topLayer=SortingLayer.layers.OrderBy(l=>l.value).Last().id;
                for(int i=0;i<canvases.Length;i++){var canvas=canvases[i];canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.1f;canvas.sortingLayerID=topLayer;canvas.sortingOrder=31000+i;}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=texture;
                pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());
            }
            finally
            {
                for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=oldCameras[i];canvases[i].planeDistance=oldDistances[i];canvases[i].sortingLayerID=oldLayers[i];canvases[i].sortingOrder=oldOrders[i];}
                camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(texture);if(pixels!=null)Object.Destroy(pixels);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
    public sealed class FestivalDemoTestHost:MonoBehaviour { }
}
