using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Isolated visitor and actual shipped rooms, with shortened room timers for repeatable coverage.
    [InitializeOnLoad]
    public static class BloodClotBoothSmoke
    {
        const string Key = "BloodClotBoothSmoke", Output = "Library/GStarUIQA/blood-clot-booth";
        static bool started;
        static Keyboard keyboard;
        static bool ordinaryLoopEntry;
        static double deadline;
        static BloodClotBoothSmoke() { if (SessionState.GetBool(Key,false)) Attach(); }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"/report.txt","");
            File.WriteAllText(Output+"/input.txt","");
            SessionState.SetString(Key+"data",Path.GetFullPath(Output+"/test-"+Guid.NewGuid().ToString("N")));
            SessionState.SetString(Key+"result","");
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            Attach();
            EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if (!SessionState.GetBool(Key,false)) return;
            GStarDemo.TestOverride = true;
            GStarDemo.TestDataDirectory = SessionState.GetString(Key+"data","");
            GStarSession.TestDirectory = GStarLeaderboard.TestDirectory = GStarDemo.TestDataDirectory;
            GStarSession.ReloadForTests(); GStarLeaderboard.ReloadForTests();
            CrossSceneData.CharacterBlueprint = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").characters.First(c=>OctoberArt.CharacterKey(c)=="Ashi");
        }
        static void Attach()
        {
            started=false; deadline=EditorApplication.timeSinceStartup+360;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log; Application.logMessageReceived+=Log;
        }
        static void Log(string message,string stack,LogType type)
        {
            if (type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert) return;
            File.AppendAllText(Output+"/report.txt",message+"\n"+stack+"\n");
            SessionState.SetString(Key+"result","FAIL");
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key,false)) return;
            var result=SessionState.GetString(Key+"result","");
            if (EditorApplication.timeSinceStartup>deadline && result=="") result="TIMEOUT";
            if (result!="")
            {
                if (EditorApplication.isPlaying) { SessionState.SetString(Key+"result",result); EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key,false);
                File.WriteAllText(Output+"/result.txt",result);
                EditorApplication.Exit(result=="PASS"?0:1); return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if (started || !EditorApplication.isPlaying || level?.PlayerCharacter==null || StageEntryLoading.IsLoading || !level.InitialFieldObjectsReady || ApothecaryUI.Instance==null) return;
            started=true;
            level.StartCoroutine(Drive(level));
        }
        static void Check(bool okay,string note)
        {
            if (!okay) throw new Exception("Blood clot booth QA: "+note);
            File.AppendAllText(Output+"/report.txt","PASS "+note+"\n");
        }
        static IEnumerator Wait(Func<bool> condition,string note)
        {
            float until=Time.realtimeSinceStartup+14f;
            while (!condition() && Time.realtimeSinceStartup<until) yield return null;
            Check(condition(),note);
        }
        static FieldInfo Field(object target,string name)
        {
            for (Type t=target.GetType();t!=null;t=t.BaseType)
            {
                var field=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                if (field!=null) return field;
            }
            throw new MissingFieldException(name);
        }
        static void Set(object target,string name,object value) => Field(target,name).SetValue(target,value);
        static void Move(Character player,Vector3 p)
        {
            player.Move(Vector2.zero);
            player.transform.position=p;
            player.GetComponent<Rigidbody2D>().position=p;
            Physics2D.SyncTransforms();
        }
        static IEnumerator NormalLoopEntry(BloodClotMiniStagePortal portal, MiniStageDirector director)
        {
            var key = GameInput.ResolveKeyboardKey(GamePreferences.Current.keyboard.Get(GameKeyAction.Interact));
            void Sample(string phase)
            {
                var window = EditorWindow.focusedWindow;
                File.AppendAllText(Output+"/input.txt",
                    $"{phase} frame={Time.frameCount} editor={window?.GetType().Name} focused={Application.isFocused} page={ApothecaryUI.Instance.Page} scale={Time.timeScale}" +
                    $" keyboard={Keyboard.current?.deviceId}/{keyboard.deviceId} enabled={keyboard.enabled} update={InputState.currentUpdateType} held={keyboard[key].isPressed} down={keyboard[key].wasPressedThisFrame}" +
                    $" action={GameInput.Pressed(GameKeyAction.Interact)} blocked={GameInput.KeyboardBlocked}/{TutorialGuide.BlocksMenuInput} owner={InteractionFocus.Selected?.name} can={portal.CanInteract} reserved={portal.Reserved} rolling={director.IsTransitioning}\n");
            }
            void AfterInput() => Sample("after input");
            var priorSettings=InputSystem.settings;
            InputSettings testSettings=null;
            try
            {
                for (int mode=0;mode<3 && !director.IsTransitioning;mode++)
                {
                    if (mode==1)
                    {
                        EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
                        yield return null;
                    }
                    if (mode==2)
                    {
                        // Batch editors can route an unfocused synthetic keyboard into the Editor stream.
                        // A transient clone emulates a focused game view and never saves project settings.
                        testSettings=Object.Instantiate(priorSettings);
                        testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                        testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                        InputSystem.settings=testSettings;
                        if(!keyboard.enabled)InputSystem.EnableDevice(keyboard);
                    }
                    keyboard.MakeCurrent();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    yield return null;
                    Sample("before ordinary key mode="+mode);
                    InputSystem.onAfterUpdate+=AfterInput;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
                    for(int n=0;n<3;n++){yield return null; Sample("after ordinary Update");}
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    yield return null;
                    InputSystem.onAfterUpdate-=AfterInput;
                }
                ordinaryLoopEntry=director.IsTransitioning;
                File.AppendAllText(Output+"/report.txt","Ordinary Update E entry = "+ordinaryLoopEntry+"\n");
            }
            finally
            {
                InputSystem.onAfterUpdate-=AfterInput;
                if(testSettings!=null){InputSystem.settings=priorSettings;Object.Destroy(testSettings);}
            }
        }

        static IEnumerator Interact(Component owner)
        {
            // The batch editor does not reliably deliver a queued key during a dynamic Game update.
            // Drive the bound key and the focused object's real Update together, as October6CombatSmoke does.
            if (!keyboard.enabled) InputSystem.EnableDevice(keyboard);
            keyboard.MakeCurrent();
            var key = GameInput.ResolveKeyboardKey(GamePreferences.Current.keyboard.Get(GameKeyAction.Interact));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
            InputSystem.Update();
            Check(GameInput.Pressed(GameKeyAction.Interact) && InteractionFocus.IsFocused(owner),
                $"actual input {key} reaches {owner.name}; raw={keyboard[key].wasPressedThisFrame}, blocked={GameInput.KeyboardBlocked}, guide={TutorialGuide.BlocksMenuInput}, focus={InteractionFocus.Selected}");
            owner.SendMessage("Update",SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            InputSystem.Update();
            yield return null;
        }
        static void CheckRunBoundary(LevelManager level, MiniStageDirector director, string action, Func<bool> canInteract, Action interact)
        {
            // The next-attempt button resumes time while the previous scene still exists.
            // Exercise both guards independently with the player standing in the actual trigger.
            var overlay = StageEntryLoading.CreateStageTransfer(CrossSceneData.CharacterBlueprint);
            try
            {
                interact();
                Check(StageEntryLoading.IsLoading && !canInteract() && !director.IsTransitioning,
                    action + " ignores the previous scene during loading");
            }
            finally { overlay.CompleteStageTransfer(); }
            try
            {
                Set(level, "levelEnded", true);
                interact();
                Check(!StageEntryLoading.IsLoading && !canInteract() && !director.IsTransitioning,
                    action + " ignores a finished run even with time resumed");
            }
            finally { Set(level, "levelEnded", false); }
        }

        static void StandAtTrigger(Character actor, Component portal)
        {
            var trigger=portal.GetComponents<Collider2D>().First(c=>c.isTrigger);
            Move(actor,trigger.bounds.center+Vector3.left*trigger.bounds.extents.x*.90f);
        }

        static void CheckGuideLayout(BloodClotGuideHUD hud)
        {
            Canvas.ForceUpdateCanvases();
            var labels = hud.GetComponentsInChildren<TMPro.TextMeshProUGUI>(false);
            foreach (var text in labels.Where(t=>t.name=="제목" || t.name=="현재 행동"))
            {
                text.ForceMeshUpdate();
                var rect = text.rectTransform.rect;
                var mesh = text.textBounds;
                Check(mesh.min.x >= rect.xMin - 2f && mesh.max.x <= rect.xMax + 2f,
                    text.name+" fits its live bounds: "+text.text);
            }
            var button=hud.GetComponentsInChildren<Button>(false).FirstOrDefault(b=>b.name=="과충전 시작");
            if (button==null) return;
            var heading=labels.First(t=>t.name=="제목");
            var a=new Vector3[4];var b=new Vector3[4];
            heading.rectTransform.GetWorldCorners(a);
            button.GetComponent<RectTransform>().GetWorldCorners(b);
            Check(a[2].x<=b[0].x,"charge button has a separate clickable region");
        }
        static void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                GStarUISmoke.Capture("blood-clot-booth/"+name,1280,720);
        }
        static IEnumerator Drive(LevelManager level)
        {
            var prefs=GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss=false; GamePreferences.Apply(prefs,false);
            Application.runInBackground=true;
            RunScore.Exclude("Blood clot booth smoke");
            var ui=ApothecaryUI.Instance;
            var actor=level.PlayerCharacter;
            keyboard=InputSystem.AddDevice<Keyboard>("Blood clot booth test keyboard");
            var input=actor.GetComponent<PlayerInput>();
            if (input != null && input.user.valid) InputUser.PerformPairingWithDevice(keyboard,input.user);
            keyboard.MakeCurrent();
            var entities=level.EntityManager;
            var director=Object.FindObjectOfType<MiniStageDirector>();
            var hud=director.GetComponent<BloodClotGuideHUD>();
            var travel=BloodClotTravel.Ensure(actor);
            foreach(var scheduler in Object.FindObjectsOfType<StageEventDirector>()) scheduler.enabled=false;
            foreach(var needle in Object.FindObjectsOfType<SyringeDartAbility>()) needle.enabled=false;
            var rooms=(MiniStageRoomBase[])Field(director,"roomPrefabs").GetValue(director);
            Set(director,"useForcedRoomIndex",true);
            Time.timeScale=1;
            var origin=actor.transform.position;
            int visited=0;
            for (int index=0;index<rooms.Length;index++)
            {
                if (!MiniStageDirector.RoomEnabled(rooms[index])) continue;
                level.SetRunFlowPaused(true);
                Set(director,"forcedRoomIndex",index);
                Move(actor,origin);
                director.OpenEntrancePortal(origin+Vector3.right*.9f);
                var portal=(BloodClotMiniStagePortal)Field(director,"activeEntrancePortal").GetValue(director);
                var charge=portal.GetComponent<BloodClotOvercharge>();
                typeof(BloodClotOvercharge).GetProperty("Kind").SetValue(charge,BloodClotOvercharge.Challenge.Kills);
                yield return Wait(()=>hud.Heading.Contains("추가 보상") && hud.Detail.Contains("25"),"field offers the actual 25-kill mission");
                if (visited==0)
                {
                    // Read the first visible frame after the scaler has applied its real safe area.
                    yield return null; yield return null;
                    CheckGuideLayout(hud);
                    Capture("01-field-offer");
                    ui.OpenRunBook(); yield return null; yield return null;
                    Check(!hud.Visible && ui.Page=="run","Tab journal hides charge cue");
                    ui.CloseRunBook();
                    yield return Wait(()=>hud.Visible,"closing Tab restores charge cue");
                }
                StandAtTrigger(actor,portal);
                yield return new WaitForSeconds(.16f);
                string entryKey = $"[{GameInput.Label(GameKeyAction.Interact)}] 일반 입장";
                yield return Wait(()=>portal.CanInteract && hud.Detail.Contains(entryKey) && charge.AtEntrance,"ordinary entry key has real portal focus");
                bool enhanced=rooms[index] is MiniStageSniperRoom;
                if (enhanced)
                {
                    var button=hud.GetComponentsInChildren<Button>(false).Single(b=>b.name=="과충전 시작");
                    button.onClick.Invoke(); Check(charge.Started,"real overcharge button starts the displayed clot");
                    // The spawned elite must not knock the stand-in player off the trigger during UI assertions.
                    foreach (var m in entities.LivingMonsters.Where(m => m != null && !(m is BloodClotMonster)).ToArray())
                        m.gameObject.SetActive(false);
                    charge.Advance(4);
                    yield return Wait(()=>hud.Heading.Contains("4/25"),"live charge progress");
                    var overlap=new GameObject("Neighbouring unopened clot");
                    overlap.transform.position=actor.transform.position;
                    overlap.AddComponent<BloodClotOvercharge>();
                    yield return new WaitForSeconds(.15f);
                    Check(BloodClotOvercharge.NearestGuide(actor.transform.position)==charge && hud.Heading.Contains("4/25"),"active ring wins over a closer unused clot");
                    Object.Destroy(overlap);
                    Capture("02-field-progress");
                    Move(actor,origin+Vector3.left*3);
                    yield return Wait(()=>hud.Detail.Contains("집계가 멈춰"),"leaving circle explains stalled progress");
                    Move(actor,origin);charge.Advance(charge.Target);
                    yield return Wait(()=>hud.Heading.Contains("과충전 성공"),"completed charge announces enhanced entrance");
                    Capture("03-field-ready");
                }
                StandAtTrigger(actor,portal);
                yield return new WaitForSeconds(.18f);
                yield return Wait(()=>charge.AtEntrance && portal.CanInteract,"the displayed entry action is focused");
                if (visited==0)
                {
                    CheckRunBoundary(level, director, "entry", () => portal.CanInteract, () => director.EnterMiniStageFromPortal(portal));
                    yield return NormalLoopEntry(portal,director);
                }
                if (!director.IsTransitioning) yield return Interact(portal);
                yield return Wait(()=>director.CurrentRoom!=null,"room created through portal");
                var room=director.CurrentRoom;
                if (room is MiniStageCollectionRoom) Set(room,"timeLimit",2f);
                if (room is MiniStageFallingFoodRoom) Set(room,"roomDuration",1.6f);
                if (room is MiniStageExplodingRushRoom)
                {
                    Set(room,"totalMonsterCountMin",2);Set(room,"totalMonsterCountMax",2);
                    Set(room,"delayBetweenMonstersMin",.1f);Set(room,"delayBetweenMonstersMax",.1f);
                }
                yield return Wait(()=>room.RoomStarted && !director.IsTransitioning,"room begins after landing");
                yield return Wait(()=>hud.Heading.Contains(room.QuestName) && !travel.HoldingPose && !travel.Visual.gameObject.activeSelf,room.QuestName+" idle and live objective");
                Check(hud.Detail==room.QuestHint && !room.ReturnInteractable.IsUnlocked,"objective matches actual state, exit locked");
                if (enhanced)
                {
                    var visible = (SpriteRenderer)Field(room.ReturnInteractable,"portalRenderer").GetValue(room.ReturnInteractable);
                    Check(visible.sprite == (Sprite)Field(room.ReturnInteractable,"lockedSprite").GetValue(room.ReturnInteractable),
                        "enhanced return portal visibly sealed until reward");
                }
                CheckGuideLayout(hud);
                Capture("room-"+index);
                if (room is MiniStageBonusChestRoom bonus)
                {
                    var fake=bonus.GetComponentsInChildren<MiniStageMysteryChestInteractable>().Single(c=>c.Outcome==MiniStageMysteryChestOutcome.Fake);
                    bonus.NotifyMysteryChestSelected(fake);
                    Check(room.EndedWithoutReward,"empty chest ends without promising an upgrade");
                }
                else
                {
                    if (room is MiniStageSniperRoom || room is MiniStageExplodingRushRoom)
                    {
                        yield return new WaitForSeconds(.4f);
                        foreach(var m in entities.LivingMonsters.Where(m=>m!=null && m.IsMiniStageOwned && m.HP>0).ToArray())
                            m.StartCoroutine(m.Killed(false));
                    }
                    yield return Wait(()=>ui.Page=="skillReward","success opens real skill choice");
                    yield return null;yield return null;
                    Check(!hud.Visible && !room.ReturnInteractable.IsUnlocked,"skill reward hides guide and keeps exit locked");
                    var choice=ui.GetComponentsInChildren<Button>(false).First(b=>b.interactable && b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("Lv.")));
                    choice.onClick.Invoke();
                    if(enhanced)
                    {
                        yield return Wait(()=>room.RewardChest!=null && hud.Heading.Contains("보상 상자"),"extra chest guides before return");
                        Check(!room.ReturnInteractable.IsUnlocked && hud.PointerVisible,"enhanced exit awaits bonus reward");
                        Capture("04-reward-chest");
                        yield return Wait(()=>room.RewardChest.CanInteract,"bonus chest opens after appearing");
                        room.RewardChest.OpenChest();
                        Check(!room.ReturnInteractable.IsUnlocked,"opening animation cannot prematurely unlock exit");
                        yield return Wait(()=>entities.AbilitySelectionDialog.MenuOpen || room.ReturnInteractable.IsUnlocked,"chest revealed or offered bonus choice");
                        if (entities.AbilitySelectionDialog.MenuOpen)
                        {
                            yield return null;yield return null;
                            Check(!hud.Visible && !room.ReturnInteractable.IsUnlocked,"bonus chooser hides guide and blocks return");
                            entities.AbilitySelectionDialog.Close();
                        }
                    }
                }
                var exit=room.ReturnInteractable;
                yield return Wait(()=>exit.IsUnlocked && room.CanReturnFrom(exit),"exit only announces when usable");
                Check(exit.ShowingOpeningPulse,"newly opened portal emits a single visual pulse");
                var solidBefore=exit.GetComponent<BloodClotObstacle>().Solid.bounds.size;
                yield return new WaitForSeconds(MiniStageReturnInteractable.OpeningPulseDuration+.07f);
                Check(!exit.ShowingOpeningPulse && exit.GetComponent<BloodClotObstacle>().Solid.bounds.size==solidBefore,"opening settles without resizing collision");
                Move(actor,exit.transform.position+Vector3.left*4);
                yield return Wait(()=>hud.PointerVisible && hud.Heading.Contains("열렸"),"portal arrow after eligibility");
                if(visited==0) Capture("05-return-arrow");
                // Contact the exit from outside its solid core, using the return trigger's actual dimensions.
                StandAtTrigger(actor,exit);
                yield return Wait(()=>exit.CanInteract && hud.Detail.Contains("원래 필드로"),"return action advertised only in actual range");
                var keycap=exit.transform.Find("Pixel E Interaction Prompt");
                var bound=exit.transform.Find("Bound interaction key");
                Check((keycap!=null && keycap.GetComponent<SpriteRenderer>().enabled) || (bound!=null && bound.gameObject.activeInHierarchy),
                    "live interaction keycap above return portal");
                if(visited==0)
                {
                    Capture("06-return-key");
                    CheckRunBoundary(level, director, "return", () => exit.CanInteract, () => director.ReturnToFieldFromInteractable(exit));
                }
                yield return Interact(exit);
                yield return Wait(()=>!director.IsInsideMiniStage && !director.IsTransitioning && !travel.HoldingPose,"return lands in idle without a movement key");
                if (visited==0)
                {
                    travel.HoldForInteraction();
                    yield return new WaitForSeconds(BloodClotTravel.LandingSettleDuration+.08f);
                    Check(travel.HoldingPose && travel.Visual.gameObject.activeSelf && travel.IsDamageProtected,
                        "reserved story pose persists until explicit release");
                    travel.ReleasePose();
                }
                visited++;
            }
            Check(visited==6,"all six enabled room prefabs exercised");
            Check(ordinaryLoopEntry,"a queued E entered via the normal player loop");
            SessionState.SetString(Key+"result","PASS");
        }
    }
}
