using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class KeyboardBindingsSmoke
    {
        const string Session="KeyboardBindingsSmoke";
        static bool started; static double deadline; static int checks;
        static string Output=>Path.GetFullPath("Library/KeyboardBindingsProof");
        static KeyboardBindingsSmoke(){if(SessionState.GetBool(Session,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Session,true);SessionState.SetBool(Session+"Done",false);SessionState.SetBool(Session+"Failed",false);
            SessionState.SetString(Session+"Prefs",PlayerPrefs.GetString(GamePreferences.SaveKey,""));
            SessionState.SetInt(Session+"EditorInput",(int)InputSystem.settings.editorInputBehaviorInPlayMode);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Resize(1600,900);Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+300;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string m,string stack,LogType type){if(type==LogType.Exception||type==LogType.Assert){Debug.LogError("KEYBOARD_EXCEPTION "+m+"\n"+stack);SessionState.SetBool(Session+"Failed",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Session,false))return;
            if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("KEYBOARD_TIMEOUT");SessionState.SetBool(Session+"Failed",true);}
            if(SessionState.GetBool(Session+"Done",false)||SessionState.GetBool(Session+"Failed",false)) {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                string saved=SessionState.GetString(Session+"Prefs","");if(saved.Length==0)PlayerPrefs.DeleteKey(GamePreferences.SaveKey);else PlayerPrefs.SetString(GamePreferences.SaveKey,saved);PlayerPrefs.Save();
                InputSystem.settings.editorInputBehaviorInPlayMode=(InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt(Session+"EditorInput",0);
                bool failed=SessionState.GetBool(Session+"Failed",false);SessionState.SetBool(Session,false);
                Debug.Log($"KEYBOARD_FINISHED checks={SessionState.GetInt(Session+"Count",0)} failed={failed}");EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(started||!EditorApplication.isPlaying||level?.PlayerCharacter==null||ApothecaryUI.Instance==null)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static void Check(bool yes,string label){if(!yes)throw new Exception("KEYBOARD_FAIL "+label);checks++;SessionState.SetInt(Session+"Count",checks);Debug.Log("KEYBOARD_PASS "+label);}
        static Button Button(string name)=>Object.FindObjectsOfType<Button>().Single(b=>b.name==name);
        static void Click(string name)
        {
            var b=Button(name);Canvas.ForceUpdateCanvases();var corners=new Vector3[4];((RectTransform)b.transform).GetWorldCorners(corners);
            var ev=new PointerEventData(EventSystem.current){position=(corners[0]+corners[2])*.5f,button=PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(ev,hits);
            Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"raycast "+name);
            ExecuteEvents.Execute(b.gameObject,ev,ExecuteEvents.pointerClickHandler);
        }
        static void Resize(int w,int h)=>EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,w,h+21);
        static IEnumerator Frame(){yield return null;yield return new WaitForEndOfFrame();}
        static void Capture(string name){var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name+".png",t.EncodeToPNG());Object.Destroy(t);}
        static IEnumerator Press(Keyboard kb,Key key){InputSystem.QueueStateEvent(kb,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(kb,new KeyboardState());yield return null;yield return null;}
        static GamePreferenceValues Draft(ApothecaryUI ui)=>(GamePreferenceValues)typeof(ApothecaryUI).GetField("settingsDraft",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui);
        static IEnumerator Checks(LevelManager level)
        {
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            while(StageEntryLoading.IsLoading)yield return null;
            yield return new WaitForSecondsRealtime(2);
            RunScore.Exclude("Keyboard binding verification");
            var prefs=GamePreferences.Defaults();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            if(TutorialGuide.IsOpen)TutorialGuide.Instance.Close();
            if(level.EntityManager.AbilitySelectionDialog.MenuOpen)level.EntityManager.AbilitySelectionDialog.Close();
            level.SetRunFlowPaused(true);Time.timeScale=1;
            var ui=ApothecaryUI.Instance;ui.Show("hud");
            var kb=InputSystem.AddDevice<Keyboard>();
            // A keyboard arriving after scene load is supported by the binding paths as well.
            var player=Object.FindObjectOfType<PlayerInput>();
            Check(player!=null&&player.GetComponent<KeyboardBindingDriver>()!=null,"runtime driver installed");
            var move=player.actions.FindAction("Player/Move");
            Check(move.bindings.Any(b=>b.effectivePath=="<Gamepad>/leftStick"),"gamepad movement unchanged");
            yield return Press(kb,Key.Escape);Check(ui.Page=="settings","Esc opens settings");
            Click("Settings category 2");yield return Frame();
            Check(GameInput.EditingKeyboard&&!EventSystem.current.sendNavigationEvents,"editing suppresses keyboard UI submit/navigation");
            Capture("01-keyboard-default");
            yield return Press(kb,Key.Tab);Check(ui.Page=="settings"&&ui.Tab==2,"physical Tab selects key without opening journal");
            yield return Press(kb,Key.Space);Check(ui.Page=="settings"&&ui.Tab==2,"physical Space selects key without submitting");
            Click("Keyboard key I");yield return Frame();Click("Keyboard group 1");yield return Frame();Click("Keyboard assign MoveUp");yield return Frame();
            Check(GameObject.Find("Keyboard conflict confirm")!=null,"replacement requires conflict confirmation");Capture("02-replace-confirmation");
            Click("Keyboard conflict cancel");yield return Frame();Check(Draft(ui).keyboard.Get(GameKeyAction.MoveUp)==KeyCode.W,"conflict cancel keeps old key");
            Click("Keyboard assign MoveUp");yield return Frame();Click("Keyboard conflict confirm");yield return Frame();
            Check(Draft(ui).keyboard.Get(GameKeyAction.MoveUp)==KeyCode.I&&GamePreferences.Current.keyboard.Get(GameKeyAction.MoveUp)==KeyCode.W,"draft is separate from live controls");
            Click("Settings resume");yield return Frame();Check(GamePreferences.Current.keyboard.Get(GameKeyAction.MoveUp)==KeyCode.W&&!GameInput.EditingKeyboard,"back discards and restores input");
            yield return Press(kb,Key.Escape);Click("Settings category 2");yield return Frame();
            var draft=Draft(ui);
            draft.keyboard.Assign(GameKeyAction.MoveUp,0,KeyCode.I);draft.keyboard.Assign(GameKeyAction.Active,0,KeyCode.R);
            draft.keyboard.Assign(GameKeyAction.Dash,0,KeyCode.F);draft.keyboard.Assign(GameKeyAction.Interact,0,KeyCode.G);
            draft.keyboard.Assign(GameKeyAction.Overcharge,0,KeyCode.H);draft.keyboard.Assign(GameKeyAction.Journal,0,KeyCode.B);
            draft.keyboard.Assign(GameKeyAction.Slot1,0,KeyCode.T);
            draft.keyboard.Assign(GameKeyAction.Slot2,0,KeyCode.Y);
            Click("Settings apply");yield return Frame();
            Check(GamePreferences.Read().keyboard.Get(GameKeyAction.Active)==KeyCode.R,"custom bindings persist in settings save");
            Check(move.bindings.Any(b=>b.effectivePath=="<Keyboard>/i")&&!move.bindings.Any(b=>b.effectivePath=="<Keyboard>/w"),"movement overrides replaced W with I");
            Check(move.bindings.Any(b=>b.effectivePath=="<Keyboard>/upArrow"),"movement secondary preserved");
            var item=player.actions.FindAction("Player/North Powerup");
            Check(item.bindings.Any(b=>b.effectivePath=="<Keyboard>/t")&&!item.bindings.Any(b=>b.effectivePath=="<Keyboard>/z"),"inventory action rebound");
            Check(item.bindings.Any(b=>b.effectivePath=="<Gamepad>/buttonNorth"),"inventory controller binding preserved");
            foreach(var e in player.actionEvents.Where(e=>e.actionName.Contains("Powerup")))Check(e.GetPersistentMethodName(0)=="UseInputItem","performed-only callback "+e.actionName);
            Click("Settings resume");yield return Frame();
            // Switch the player to the injected device; avoid relying on editor hardware pairing.
            var mouse=Mouse.current??InputSystem.AddDevice<Mouse>();
            player.SwitchCurrentControlScheme("Keyboard&Mouse",kb,mouse);
            InputSystem.QueueStateEvent(kb,new KeyboardState(Key.I));yield return null;yield return null;
            Debug.Log($"KEYBOARD_DIAG enabled={move.enabled} value={move.ReadValue<Vector2>()} page={ui.Page} active={player.inputIsActive} scheme={player.currentControlScheme} kb={kb.deviceId}/{kb.iKey.isPressed} controls={string.Join(",",move.controls.Select(c=>c.path+":"+c.device.deviceId))} mask={player.actions.bindingMask}");
            Check(move.ReadValue<Vector2>().y>.9f,"real remapped movement action");
            InputSystem.QueueStateEvent(kb,new KeyboardState(Key.W));yield return null;yield return null;Check(move.ReadValue<Vector2>()==Vector2.zero,"old movement key inactive");
            InputSystem.QueueStateEvent(kb,new KeyboardState());yield return null;
            int dashCharges=level.PlayerCharacter.CurrentDashCharges;
            yield return Press(kb,Key.F);Check(level.PlayerCharacter.CurrentDashCharges<dashCharges,"custom dash key spends a charge");
            while(level.PlayerCharacter.IsDashing)yield return null;
            var slot=level.EntityManager.Inventory.Slots[1];int initialCount=slot.Count;
            for(int i=0;i<3;i++){var fake=new GameObject("Key input consumable proof").AddComponent<KeyboardProbeCollectable>();slot.AddItem(fake);}
            yield return Press(kb,Key.Y);Check(slot.Count==initialCount+2,"one consumable per custom key press including release");
            yield return Press(kb,Key.X);Check(slot.Count==initialCount+2,"old consumable key inactive");
            yield return Press(kb,Key.Digit2);Check(slot.Count==initialCount+1,"secondary consumable key works");
            InputSystem.QueueStateEvent(kb,new KeyboardState(Key.R));yield return null;yield return null;Check(GameInput.Held(GameKeyAction.Active),"remapped held active input");
            InputSystem.QueueStateEvent(kb,new KeyboardState());yield return null;yield return null;Check(!GameInput.Held(GameKeyAction.Active),"held active stops on release");
            while(level.PlayerCharacter.Skills.IsCutin)yield return null;
            Check(level.PlayerCharacter.Skills.CooldownRemaining>0||level.PlayerCharacter.Skills.Active,"custom active key activated the character skill");
            var probe=new GameObject("Keyboard semantic proof").AddComponent<KeyboardInputProbe>();
            yield return Press(kb,Key.G);Check(probe.interactions==1&&probe.secondBlocked,"interaction remapped and consumed once per frame");
            yield return Press(kb,Key.E);Check(probe.interactions==1,"old interaction key inactive");
            yield return Press(kb,Key.H);Check(probe.overcharges==1,"overcharge action remapped");
            probe.transform.position=level.PlayerCharacter.transform.position+Vector3.right*2;
            PixelInteractionPrompt.Show(probe,true);yield return Frame();
            Check(probe.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="G"),"world interaction prompt displays saved key");
            Object.Destroy(probe.gameObject);
            Check(ConsumableHelp.Keys(0).Contains("T")&&GameInput.ActiveSkillKeyLabel=="R","HUD labels follow saved keys");
            yield return Press(kb,Key.B);Check(ui.Page=="run","custom journal shortcut");
            yield return Press(kb,Key.B);Check(ui.Page=="hud","custom journal closes");
            yield return Press(kb,Key.Escape);Click("Settings category 2");yield return Frame();
            yield return Press(kb,Key.Y);Check(slot.Count==initialCount+1,"editing key does not consume inventory");
            Click("Keyboard key R");yield return Frame();Capture("03-keyboard-custom");
            Click("Keyboard unassign");yield return Frame();Click("Settings apply");yield return Frame();
            Check(GamePreferences.Current.keyboard.Get(GameKeyAction.Active)==KeyCode.R,"cannot save missing mandatory action");
            Click("Settings reset");yield return Frame();Check(Draft(ui).keyboard.Get(GameKeyAction.Active)==KeyCode.Space,"reset changes draft only");
            Click("Settings resume");yield return Frame();Check(GamePreferences.Current.keyboard.Get(GameKeyAction.Active)==KeyCode.R,"reset can be canceled");
            yield return Press(kb,Key.Escape);Click("Settings category 2");yield return Frame();Click("Settings reset");yield return Frame();Click("Settings apply");yield return Frame();
            Check(GamePreferences.Current.keyboard.Get(GameKeyAction.Active)==KeyCode.Space,"reset and apply restore defaults");
            Resize(1024,576);yield return new WaitForSecondsRealtime(.5f);yield return Frame();Capture("04-keyboard-compact");
            Check(!Object.FindObjectsOfType<TMP_Text>().Any(t=>t.gameObject.activeInHierarchy&&t.isTextOverflowing&&t.transform.IsChildOf(Button("Settings apply").transform.parent)),"settings labels fit compact layout");
            Click("Settings resume");yield return Frame();
            GamePlatform.PreviewTouchControls=true;yield return null;
            Check(GamePreferences.Current.keyboard.Get(GameKeyAction.Active)==KeyCode.Space,"mobile preview does not change keyboard save");GamePlatform.PreviewTouchControls=false;
            var invalid=KeyboardBindingValues.Defaults();invalid.primary=null;invalid.Sanitize();Check(invalid.ValidationError()==null,"invalid save repaired");
            var before=GamePreferences.Current.Copy();var copy=before.Copy();copy.keyboard.Assign(GameKeyAction.Active,0,KeyCode.R);Check(before.keyboard.Get(GameKeyAction.Active)==KeyCode.Space,"settings deep copy includes key arrays");
            InputSystem.RemoveDevice(kb);SessionState.SetBool(Session+"Done",true);
        }
    }
    public sealed class KeyboardProbeCollectable : Collectable
    {
        protected override void Awake() { }
        protected override void OnCollected() { }
    }
    [DefaultExecutionOrder(-5000)]
    public sealed class KeyboardInputProbe : MonoBehaviour
    {
        public int interactions,overcharges;public bool secondBlocked;
        void Update() {
            if(GameInput.Pressed(GameKeyAction.Interact)){if(GameInput.TryConsumeInteraction(KeyCode.E))interactions++;secondBlocked=!GameInput.TryConsumeInteraction(KeyCode.E);}
            if(GameInput.Pressed(GameKeyAction.Overcharge))overcharges++;
        }
    }
}
