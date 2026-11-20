using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class FieldDayControlsSmoke
    {
        const string KeyName = "FieldDayControlsQA";
        static readonly bool RewardProofOnly = Environment.GetCommandLineArgs().Contains("-rewardProofOnly");
        static readonly bool PracticeRoster = Environment.GetCommandLineArgs().Contains("-practiceRosterProof");
        static string Folder => RewardProofOnly ? "reward-proof" : PracticeRoster ? "practice-roster" : "field-day";
        static string Output => "Library/GStarUIQA/"+Folder;
        static bool started;
        static double deadline;
        static Keyboard keyboard;
        static int frame;
        static string characterFolder;
        static FieldDayControlsSmoke() { if (SessionState.GetBool(KeyName,false)) Attach(); }

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"/report.txt","");
            SessionState.SetBool(KeyName,true);
            SessionState.SetString(KeyName+"result","");
            SessionState.SetString(KeyName+"data",Path.GetFullPath(Output+"/isolated-"+Guid.NewGuid().ToString("N")));
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position = new Rect(0,0,1280,750);
            Attach();
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if (!SessionState.GetBool(KeyName,false)) return;
            GStarDemo.TestOverride = true;
            GStarDemo.TestDataDirectory = SessionState.GetString(KeyName+"data","");
            GStarSession.TestDirectory = GStarLeaderboard.TestDirectory = GStarDemo.TestDataDirectory;
            GStarSession.ReloadForTests(); GStarLeaderboard.ReloadForTests();
        }

        static void Attach()
        {
            deadline = EditorApplication.timeSinceStartup + (PracticeRoster ? 1200 : 480);
            started = false;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string msg,string stack,LogType type)
        {
            if (type!=LogType.Assert && type!=LogType.Exception && type!=LogType.Error) return;
            if (!SessionState.GetBool(KeyName,false)) return;
            File.AppendAllText(Output+"/report.txt",msg+"\n"+stack+"\n");
            SessionState.SetString(KeyName+"result","FAIL");
        }
        static void Tick()
        {
            if (!SessionState.GetBool(KeyName,false)) return;
            if (EditorApplication.timeSinceStartup>deadline) SessionState.SetString(KeyName+"result","TIMEOUT");
            var result = SessionState.GetString(KeyName+"result","");
            if (result.Length>0)
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(KeyName,false);
                File.WriteAllText(Output+"/result.txt",result);
                EditorApplication.Exit(result=="PASS"?0:1);
                return;
            }
            if (started || !EditorApplication.isPlaying || ApothecaryUI.Instance==null) return;
            started=true;
            var host=new GameObject("G-Star practice QA");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<GStarUITestHost>().StartCoroutine(Drive());
        }
        static void Check(bool ok,string message)
        {
            if (!ok) throw new Exception("Field day booth QA: "+message);
            File.AppendAllText(Output+"/report.txt","PASS "+message+"\n");
        }
        static IEnumerator Wait(Func<bool> condition,string label,int max=90)
        {
            float limit = Time.realtimeSinceStartup+max;
            while (!condition() && Time.realtimeSinceStartup<limit) yield return null;
            Check(condition(),label);
        }
        static void Capture(string id) => GStarUISmoke.Capture(Folder+"/"+id,960,540);
        static void CaptureDisplay(string name)
        {
            // In an interactive Game View, also request the untouched display frame.
            if (RewardProofOnly && !Application.isBatchMode)
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/"+name+"-display.png"));
        }

        static IEnumerator Film(int count)
        {
            for (int i=0;i<count;i++)
            {
                yield return null;
                Capture($"{characterFolder}/{frame++:0000}");
                yield return null; // Two 24 fps simulation frames per capture, independent of disk latency.
            }
        }
        static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        static void Mark(string name) => File.AppendAllText(Output+"/"+characterFolder+"/markers.txt",name+" "+frame+"\n");
        static IEnumerator FilmUntil(Func<bool> reached, int maximumFrames, string description)
        {
            for (int i=0; i<maximumFrames && !reached(); i++) yield return Film(1);
            Check(reached(),characterFolder+" "+description);
        }
        static Button FindButton(string title) => ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.name==title);

        static void CheckSignature(LevelManager level)
        {
            var player=level.PlayerCharacter;
            var parent=(SyringeSpecialAugmentAbility.SpecialAugmentType)StartingNeedleSelection.DefaultId(player.Blueprint);
            var manager=Object.FindObjectOfType<AbilityManager>();
            var ability=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).First(w=>w.Type==parent);
            var runtime=manager.Ver4;
            Check(ability.Owned && Enumerable.Range(0,3).All(i=>runtime.Progress.Count(parent.ToString(),i)==1),
                player.Blueprint.name+" actual signature parent and 1/1/1 originals");
            int acquired=runtime.CaptureRunSceneConditionalAugmentIds().Count;
            StartingNeedleSelection.Apply(manager,player.Blueprint);
            Check(acquired==runtime.CaptureRunSceneConditionalAugmentIds().Count,"signature starter is idempotent");
        }

        static IEnumerator CheckGuideRoster()
        {
            var config = ApothecaryUI.Instance.Config;
            var roster = config.characters;
            string[] keys = {"Ashi","Ari","Hyuki","Shini"};
            foreach (var key in keys)
            {
                var ui = ApothecaryUI.Instance;
                ui.Show("prepare");
                yield return null;
                int index = Array.FindIndex(roster, c => c != null && OctoberArt.CharacterKey(c) == key);
                Check(index >= 0, key+" is in the booth roster");
                FindButton("Choice 0 "+index).onClick.Invoke();
                if (key=="Hyuki")
                {
                    var pierce = config.weapons.First(w=>w.Type==SyringeSpecialAugmentAbility.SpecialAugmentType.Pierce);
                    StartingNeedleSelection.Set(pierce,roster[index]);
                }
                ui.StartRun();
                yield return Wait(()=>GStarGaugeIntro.IsOpen, key+" first guide");
                var level = Object.FindObjectOfType<LevelManager>();
                Check(OctoberArt.CharacterKey(level.PlayerCharacter.Blueprint)==key, key+" is actually on the field");
                CheckSignature(level);
                if (key=="Hyuki")
                {
                    var manager = Object.FindObjectOfType<AbilityManager>();
                    Check(manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).Any(w=>w.Owned && w.Type==SyringeSpecialAugmentAbility.SpecialAugmentType.Pierce),
                        "custom lobby needle and Hyuki's signature coexist");
                    StartingNeedleSelection.Set(config.weapons.First(w => w.Type==SyringeSpecialAugmentAbility.SpecialAugmentType.IceNeedle),roster[index]);
                }
                yield return null;
                Capture("gauge-guide-"+key);
                // Leave this QA-only visit at its pending reservation. Nothing is scored or transferred.
                Check(GStarSession.CancelPendingAttempt() && GStarSession.ResetVisitor(),key+" rehearsal costs no attempt");
                Object.Destroy(StageEntryLoading.Instance.gameObject);
                yield return null;
                Time.timeScale=1;
                UnityEngine.SceneManagement.SceneManager.LoadScene(0);
                yield return Wait(()=>!GStarGaugeIntro.IsOpen && ApothecaryUI.Instance!=null && ApothecaryUI.Instance.Page=="main","fresh visitor after "+key);
            }
        }

        static void CheckCardCopy(AbilityCard card, AbilitySelectionDialog dialog, AbilityManager manager)
        {
            var fields = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var offered = (Ability)typeof(AbilityCard).GetField("ability",fields).GetValue(card);
            var description = (TMPro.TextMeshProUGUI)typeof(AbilityCard).GetField("descriptionText",fields).GetValue(card);
            var sample = new GameObject("Booth text QA").AddComponent<Ver4AugmentOffer>();
            int parents = 0, originals = 0, nobles = 0, numerics = 0;
            void Verify(Ver4RewardKind kind, Ability source, int option=0)
            {
                var grade = kind==Ver4RewardKind.Original ? AugmentUpgradeGrade.Original
                    : kind==Ver4RewardKind.LegendaryAbility ? AugmentUpgradeGrade.Legendary : AugmentUpgradeGrade.Common;
                float amount = kind==Ver4RewardKind.Numeric ? manager.Ver4.Balance.NumericValue(grade,option) : 0f;
                sample.Configure(manager.Ver4,kind,grade,source,option,amount,source.Name,"full detail belongs in the run book");
                card.Init(dialog,sample,0);
                Canvas.ForceUpdateCanvases();
                description.ForceMeshUpdate();
                string brief = AugmentBrief.For(sample);
                if (description.text!=brief || brief.Length<3 || brief.Length>60 || description.isTextOverflowing || description.textInfo.lineCount>4)
                    throw new Exception("Booth augment copy does not fit: "+source.Name+" "+brief);
            }
            foreach (var parent in manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true))
            {
                Verify(Ver4RewardKind.NewSpecial,parent); parents++;
                for (int i=0;i<3;i++)
                {
                    Verify(Ver4RewardKind.Original,parent,i); originals++;
                    for (int rank=0;rank<3;rank++)
                        if (AugmentBrief.Original((int)parent.Type*3+i,rank).Length>52)
                            throw new Exception("Booth original exceeds the two-sentence budget");
                }
            }
            foreach (var noble in manager.GetComponentsInChildren<SyringeLegendaryAugmentAbility>(true).Where(n=>n.AvailableAsNoble))
            { Verify(Ver4RewardKind.LegendaryAbility,noble); nobles++; }
            var firstParent=manager.GetComponentInChildren<SyringeSpecialAugmentAbility>(true);
            for(int i=0;i<Ver4AugmentCatalog.NumericNames.Length;i++)
            { Verify(Ver4RewardKind.Numeric,firstParent,i); numerics++; }
            card.Init(dialog,offered,0);
            Object.Destroy(sample.gameObject);
            Check(parents==Ver4AugmentCatalog.ParentNames.Length && originals==Ver4AugmentCatalog.OriginalNames.Length && nobles>0 && numerics==9,
                $"live augment panel compact copy fits ({parents} parents / {originals} originals / {nobles} nobles / {numerics} stats)");
        }

        static IEnumerator CheckRewardChain(LevelManager level)
        {
            var dialog = level.EntityManager.AbilitySelectionDialog;
            var manager = Object.FindObjectOfType<AbilityManager>();
            var quest = level.PlayerCharacter.GetComponent<PrescriptionRuntime>();
            var before = quest.Capture();
            var card = new Func<AbilityCard>(() => dialog.GetComponentsInChildren<AbilityCard>().First());
            dialog.RequestLevelReward(2);
            dialog.RequestLevelReward(3);
            Check(dialog.MenuOpen && Time.timeScale == 0, "earned level choices use one pause");
            quest.Restore(new PrescriptionState {tag=0, quests=new[]{0,1,3}, progress=new[]{150f,1f,25f}});
            yield return new WaitForSecondsRealtime(.60f);
            CheckCardCopy(card(), dialog, manager);
            yield return new WaitForSecondsRealtime(.60f);
            CaptureDisplay("augment-level");
            Capture("augment-level");
            card().Selected();
            yield return Wait(()=>!dialog.SelectionAnimating && quest.Claimed && manager.LegendaryRewardContext,"completed quest chains after first choice",8);
            Check(dialog.MenuOpen && Time.timeScale==0,"quest cards stay in the same pause");
            yield return new WaitForSecondsRealtime(.60f);
            CaptureDisplay("augment-noble");
            Capture("augment-noble");
            card().Selected();
            yield return Wait(()=>!dialog.SelectionAnimating && !manager.LegendaryRewardContext && dialog.MenuOpen,"second earned level follows the noble choice",8);
            Check(Time.timeScale==0, "third choice still owns the original pause");
            yield return new WaitForSecondsRealtime(.60f);
            card().Selected();
            yield return Wait(()=>!dialog.SelectionAnimating && !dialog.MenuOpen,"all three choices resolved",8);
            Check(Time.timeScale>0, "last choice restores gameplay");
            quest.Restore(before);
        }

        static IEnumerator CheckCriticalSoundBurst()
        {
            var audio=GameAudioManager.Instance;
            var source=(AudioSource)typeof(GameAudioManager).GetField("sfxSource",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(audio);
            yield return new WaitForSecondsRealtime(.10f);
            source.Stop();
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.CriticalHit);
            Check(source.isPlaying,"first critical plays at once");
            source.Stop();
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.CriticalHit);
            Check(!source.isPlaying,"a simultaneous critical is coalesced");
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.PlayerDash);
            Check(source.isPlaying,"dash audio still plays inside a critical burst");
            source.Stop();
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.BossAppear);
            Check(source.isPlaying,"boss warning still plays inside a critical burst");
            source.Stop();
            yield return new WaitForSecondsRealtime(.10f);
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.CriticalHit);
            Check(source.isPlaying,"a later critical plays independently");
            source.Stop();
        }

        static IEnumerator CheckPracticeToOral()
        {
            ApothecaryUI.Instance.StartRun();
            yield return Wait(()=>GStarGaugeIntro.IsOpen,"festival gauge guide opens before control practice");
            var level=Object.FindObjectOfType<LevelManager>();
            CheckSignature(level);
            var rehearsalPlayer=level.PlayerCharacter;
            int originalId=rehearsalPlayer.GetInstanceID();
            Check(StageProgression.Ensure(level).StageNumber==1&&level.IsRunFlowPaused&&!GStarAnalytics.IsRecording&&GStarSession.PendingAttempt,"practice has no real combat or scoring");
            keyboard=InputSystem.AddDevice<Keyboard>("Booth test keyboard");
            var controls=rehearsalPlayer.GetComponent<PlayerInput>();
            if(controls!=null && controls.user.valid) InputUser.PerformPairingWithDevice(keyboard,controls.user);
            keyboard.MakeCurrent();
            float fieldTime=level.CurrentLevelTime;
            yield return new WaitForSecondsRealtime(.35f);
            Check(Time.timeScale==0 && level.CurrentLevelTime==fieldTime && !GStarControlPractice.Active && !GStarAnalytics.IsRecording,
                "guide freezes the field and consumes no scored time");
            Capture("gauge-guide-"+OctoberArt.CharacterKey(rehearsalPlayer.Blueprint));
            Keys(Key.Space);
            yield return null;yield return null;yield return null;
            Check(GStarGaugeIntro.IsOpen && !rehearsalPlayer.Skills.Active,"held guide input cannot trigger a skill");
            Keys();
            yield return Wait(()=>GStarControlPractice.Active && !GStarGaugeIntro.IsOpen,"guide release begins stomach control practice");
            var practice=GStarControlPractice.Instance;
            characterFolder = OctoberArt.CharacterKey(rehearsalPlayer.Blueprint);
            if (characterFolder=="Ashi") yield return CheckCriticalSoundBurst();
            Directory.CreateDirectory(Output+"/"+characterFolder);
            File.WriteAllText(Output+"/"+characterFolder+"/markers.txt","");
            frame = 0;
            Time.captureFramerate=24;
            Mark("movement");
            var origin = rehearsalPlayer.Position;
            yield return Film(6);
            Check(practice.Step==0 && practice.CompletedDirections==0,"standing still leaves all four directions open");
            var book=ApothecaryUI.Instance;
            book.OpenRunBook();
            Keys(Key.Tab);
            yield return Film(2);
            Check(book.Page=="hud" && Time.timeScale>0,"neither the hidden HUD button nor an early Tab steals the rehearsal pause");
            Keys();
            yield return Film(1);
            var cross=(RectTransform)practice.transform.Find("머리 위 방향 키");
            var caps=cross.Cast<RectTransform>().ToDictionary(cap=>cap.GetComponentInChildren<TMPro.TMP_Text>().text);
            Check(caps.Count==4 && caps["W"].anchoredPosition.y>caps["A"].anchoredPosition.y &&
                caps["S"].anchoredPosition.y<caps["D"].anchoredPosition.y &&
                caps["A"].anchoredPosition.x<caps["W"].anchoredPosition.x &&
                caps["D"].anchoredPosition.x>caps["S"].anchoredPosition.x,"WASD caps form a cross");
            var directions = new[]{Key.W,Key.S,Key.A,Key.D};
            for (int i=0; i<directions.Length; i++)
            {
                int expected = i+1;
                Keys(directions[i]);
                yield return FilmUntil(()=>practice.CompletedDirections==expected,15,"accepted distinct direction "+directions[i]);
                Check(!caps[directions[i].ToString()].gameObject.activeSelf &&
                    caps.Values.Count(cap=>cap.gameObject.activeSelf)==4-expected,"only the pressed key disappears");
                yield return Film(2);
                if (i==0)
                {
                    Check(Vector2.Distance(origin,rehearsalPlayer.Position)>.05f,"movement inputs move the actual character");
                    Keys();
                    yield return Film(2);
                    Keys(Key.W);
                    yield return Film(3);
                    Check(practice.CompletedDirections==1 && practice.Step==0,"repeat direction does not remove another key");
                }
                Keys();
                yield return Film(2);
            }
            Check(practice.Step==1 && practice.DashUses==0,"four directions unlock dash");
            var overhead=practice.transform.Find("머리 위 조작 키").GetComponentInChildren<TMPro.TMP_Text>();
            Check(overhead.isActiveAndEnabled && overhead.text=="Shift","dash keycap above the player uses the dash binding");
            Mark("dash");
            yield return Film(3);
            for(int i=1;i<=2;i++)
            {
                int expected = i;
                var direction = i==1 ? Key.A : Key.D;
                Keys(direction);
                yield return Film(1);
                Keys(direction,Key.LeftShift);
                yield return FilmUntil(()=>practice.DashUses==expected,10,"registered real dash "+i);
                Keys();
                yield return FilmUntil(()=>!rehearsalPlayer.IsDashing,20,"dash movement ended");
                if(i==1)
                {
                    yield return Film(5);
                    Check(practice.Step==1,"a single dash cannot unlock skill");
                    yield return FilmUntil(()=>rehearsalPlayer.CurrentDashCharges>0,90,"second dash recharged");
                }
            }
            yield return FilmUntil(()=>practice.Step==2 && practice.Target!=null,20,"live skill target spawned");
            Check(overhead.isActiveAndEnabled && overhead.text=="Space","active skill keycap above the player uses the active binding");
            var target=practice.Target;
            Check(target.IsAlive && target.Position.x>rehearsalPlayer.Position.x,"live target is on the player's right");
            Mark("target");
            yield return Film(8);
            Check(practice.Step==2 && target.IsAlive && !practice.SkillUsed,"target waits for active skill input");
            yield return FilmUntil(()=>rehearsalPlayer.Skills.CanActivate,40,"skill can be activated");
            Mark("active");
            Keys(Key.Space);
            yield return FilmUntil(()=>practice.SkillUsed,25,"real Space activated the chosen character's skill");
            Keys();
            if(rehearsalPlayer.Skills.IsAri && target!=null && target.IsAlive)
            {
                yield return Film(4);
                yield return FilmUntil(()=>rehearsalPlayer.CurrentDashCharges>0,80,"Ari's attack dash is available");
                Keys(Key.D);
                yield return Film(1);
                Keys(Key.D,Key.LeftShift);
                yield return Film(2);
                Keys();
            }
            yield return FilmUntil(()=>practice.Step==3,150,"actual target death unlocks the character status lesson");
            Check(target==null || !target.IsAlive,"training target was killed");
            Mark("status-key");
            Check(overhead.isActiveAndEnabled && overhead.text=="Tab","journal keycap shown over the character");
            yield return Film(4);
            Keys(Key.Tab);
            yield return FilmUntil(()=>practice.Step==4 && book.Page=="run",20,"Tab opened the live character record");
            var bookText=book.GetComponentsInChildren<TMPro.TMP_Text>().Where(t=>t.isActiveAndEnabled).ToArray();
            Check(bookText.Any(t=>t.text.Contains("다시 눌러 닫기") && t.GetComponentInParent<Canvas>().enabled) &&
                bookText.Any(t=>t.name=="Record character name" && t.text.Contains(rehearsalPlayer.DisplayName)),
                "the real selected character and the close prompt are visible on the book");
            Mark("status-open");
            float pausedAt=Time.time;
            yield return Film(4);
            Check(Time.timeScale==0 && Time.time==pausedAt && practice.Step==4,"holding Tab keeps the book open and rehearsal time paused");
            Keys(Key.Space); // hold the previous action while closing; a fresh press should be required
            yield return Film(2);
            Keys(Key.Space,Key.Tab);
            yield return FilmUntil(()=>practice.Step==5 && book.Page=="hud",20,"second Tab closed the book and advanced to departure");
            Check(Time.timeScale>0 && overhead.isActiveAndEnabled && overhead.text=="Space","closing restores time and shows the departure key");
            var lines=practice.GetComponentsInChildren<TMPro.TMP_Text>().Select(t=>t.text).ToArray();
            Check(lines.Contains("튜토리얼 완료!") && lines.Contains("이제 구강으로 출발해 볼까요?"),"both completion lines are visible");
            Check(!GStarAnalytics.IsRecording && GStarSession.PendingAttempt,"training kill creates no scored run");
            Mark("complete");
            yield return Film(4);
            Check(!practice.Complete,"held Space cannot confirm departure");
            Keys();
            float waitingUntil=Time.time+5;
            while(Time.time<waitingUntil && GStarControlPractice.Active) yield return null;
            Check(GStarControlPractice.Active && !practice.Complete && !StageEntryLoading.IsLoading,"visitors can read the departure prompt beyond the old timeout");
            Keys(Key.Space);
            yield return null;
            yield return null;
            Time.captureFramerate=0;
            Keys();
            Mark("loading");
            yield return Wait(()=>StageEntryLoading.IsLoading && StageEntryLoading.Instance.GetComponent<Canvas>().enabled &&
                StageEntryLoading.Instance.GetComponentsInChildren<UnityEngine.UI.Image>().Count(image =>
                    image.name == "Existing scene monster" && image.sprite != null && image.sprite.name.StartsWith("OralFood"))==3,
                "all three crevices show the actual oral stage food silhouettes");
            Capture(characterFolder+"/loading");
            yield return Wait(()=>GStarSession.AttemptStarted && !StageEntryLoading.IsLoading && !GStarControlPractice.Active,"first scored run starts");
            level=Object.FindObjectOfType<LevelManager>();
            Check(level.PlayerCharacter.GetInstanceID()!=originalId && !level.PlayerCharacter.Skills.Active&&level.PlayerCharacter.Skills.CooldownRemaining<=0,"fresh player has no rehearsal cooldown");
            CheckSignature(level);
            Check(StageProgression.Ensure(level).StageNumber==0&&GStarSession.StageId==0&&GStarSession.RemainingAttempts==1,"stage zero is the first scored attempt");
            TutorialGuide.QueueStageEvent("acid-secretion"); TutorialGuide.QueueBloodClot();
            yield return new WaitForSecondsRealtime(.5f);
            Check(!TutorialGuide.IsOpen && Time.timeScale>0 && GStarAnalytics.IsRecording,"discoveries never pause the booth run");
            Capture(characterFolder+"/oral");
            InputSystem.RemoveDevice(keyboard);
            keyboard=null;
        }

        static IEnumerator Drive()
        {
            Application.runInBackground=true;
            var prefs=GamePreferences.Current.Copy();
            prefs.pauseOnFocusLoss=false; prefs.muteOnFocusLoss=false; prefs.keyboard=KeyboardBindingValues.Defaults();
            GamePreferences.Apply(prefs,false);
            yield return Wait(()=>ApothecaryUI.Instance.Page=="main","new visitor sees title");
            ApothecaryUI.Instance.Show("prepare");
            yield return null;
            var options = Environment.GetCommandLineArgs();
            if (RewardProofOnly)
            {
                var selected = ApothecaryUI.Instance.Config.characters.First(c=>OctoberArt.CharacterKey(c)=="Shini");
                CrossSceneData.CharacterBlueprint=selected;
                Check(StageEntryLoading.Begin(selected,null,0),"direct scored test departure");
                yield return Wait(()=>GStarSession.AttemptStarted && !StageEntryLoading.IsLoading,"oral field ready");
                yield return CheckRewardChain(Object.FindObjectOfType<LevelManager>());
                SessionState.SetString(KeyName+"result","PASS");
                yield break;
            }
            if (options.Contains("-fourSignatureGuides"))
            {
                yield return CheckGuideRoster();
                ApothecaryUI.Instance.Show("prepare");
                yield return null;
            }
            if (!options.Contains("-skipMovieProof") || options.Contains("-movieProofOnly"))
                yield return PreparationSkillMovieSmoke.CheckAll();
            if (options.Contains("-movieProofOnly"))
            {
                SessionState.SetString(KeyName+"result","PASS");
                yield break;
            }
            Check(GStarSession.RemainingAttempts==2,"fresh participant receives two attempts");
            int requested = Array.IndexOf(options,"-practiceCharacter");
            if (requested>=0 && requested+1<options.Length)
            {
                int index=Array.FindIndex(ApothecaryUI.Instance.Config.characters, c=>
                    c!=null && string.Equals(OctoberArt.CharacterKey(c),options[requested+1],StringComparison.OrdinalIgnoreCase));
                Check(index>=0,"requested character exists");
                FindButton("Choice 0 "+index).onClick.Invoke();
            }
            if (PracticeRoster)
            {
                foreach (var name in new[]{"Ashi","Ari","Hyuki","Shini"})
                {
                    int index = Array.FindIndex(ApothecaryUI.Instance.Config.characters,c=>c!=null && OctoberArt.CharacterKey(c)==name);
                    Check(index>=0,name+" available in departure preparation");
                    FindButton("Choice 0 "+index).onClick.Invoke();
                    yield return CheckPracticeToOral();
                    Check(characterFolder==name,name+" verified on the actual field");
                    Object.FindObjectOfType<LevelManager>().GameOver();
                    yield return Wait(()=>ApothecaryUI.Instance.Page=="result",name+" completed the practice-to-result path");
                    Check(GStarSession.FinishVisitor() && GStarSession.ResetVisitor(),"next isolated visitor can start a fresh practice");
                    Time.timeScale=1;
                    UnityEngine.SceneManagement.SceneManager.LoadScene(0);
                    yield return Wait(()=>ApothecaryUI.Instance!=null && ApothecaryUI.Instance.Page=="main","back to fresh title");
                    ApothecaryUI.Instance.Show("prepare");
                    yield return null;
                }
                SessionState.SetString(KeyName+"result","PASS");
                yield break;
            }
            yield return CheckPracticeToOral();
            var level=Object.FindObjectOfType<LevelManager>();
            yield return GStarCombatFeedbackSmoke.CheckField(level);
            yield return CheckRewardChain(level);
            level.GameOver();
            yield return Wait(()=>ApothecaryUI.Instance.Page=="result","actual end callback opens score panel");
            FindButton("GStar result next").onClick.Invoke();
            yield return null;
            Check(ApothecaryUI.Instance.Page=="gstarNext"&&GStarSession.RemainingAttempts==1,"all stages available for second departure");
            Capture("second-challenge");
            FindButton("GStar challenge 1").onClick.Invoke();
            yield return Wait(()=>GStarSession.AttemptStarted&&!StageEntryLoading.IsLoading,"second departure loads selected field directly");
            level=Object.FindObjectOfType<LevelManager>();
            CheckSignature(level);
            Check(StageProgression.Ensure(level).StageNumber==1&&!GStarControlPractice.Active&&GStarSession.RemainingAttempts==0,"second choice is stage one without repeating practice");
            level.GameOver();
            Check(!GStarSession.TryBeginAttempt(0,out _),"third scored departure is blocked");
            SessionState.SetString(KeyName+"result","PASS");
        }
    }
}
