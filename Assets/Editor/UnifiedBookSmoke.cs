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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Vampire.Editor
{
    [InitializeOnLoad]
    public static class UnifiedBookSmoke
    {
        const string Key="UnifiedBookSmoke";
        static bool started;static double deadline;static int checks;
        static string Output=>Path.GetFullPath("Library/UnifiedBookProof");
        static UnifiedBookSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Resize(1280,720);Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+300;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string m,string stack,LogType type){if(type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("UNIFIED_BOOK_TIMEOUT");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log($"UNIFIED_BOOK_FINISHED checks={checks} failed={failed}");EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(started||!EditorApplication.isPlaying||level?.PlayerCharacter==null||ApothecaryUI.Instance==null)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static void Check(bool ok,string label){if(!ok)throw new Exception("UNIFIED_BOOK_FAIL "+label);checks++;Debug.Log("UNIFIED_BOOK_PASS "+label);}
        static Button Button(string name)=>Object.FindObjectsOfType<Button>().First(b=>b.name==name);
        static Rect Bounds(RectTransform rect){var c=new Vector3[4];rect.GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
        static void Click(string name)
        {
            var b=Button(name);Canvas.ForceUpdateCanvases();var pos=Bounds((RectTransform)b.transform).center;
            var ev=new PointerEventData(EventSystem.current){position=pos,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(ev,hits);
            Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"raycast "+name);
            ExecuteEvents.Execute(b.gameObject,ev,ExecuteEvents.pointerClickHandler);
        }
        static void Resize(int w,int h)=>EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,w,h+21);
        static IEnumerator Frame(){yield return null;yield return new WaitForEndOfFrame();}
        static void Capture(string name){var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name+".png",t.EncodeToPNG());Object.Destroy(t);}
        static IEnumerator Press(Keyboard keyboard,Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
        }
        static void CheckHud()
        {
            Check(!Object.FindObjectsOfType<Button>().Any(b=>new[]{"HUD dash","HUD interact","Active skill R","Passive skill status","HUD status","HUD settings"}.Contains(b.name)),"old HUD controls absent");
            var gear=Bounds((RectTransform)Button("HUD journal gear").transform);
            var xp=(PointBar)typeof(Character).GetField("expBar",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Object.FindObjectOfType<LevelManager>().PlayerCharacter);
            var bar=Bounds((RectTransform)xp.transform);
            Debug.Log($"HUD_BOUNDS xp={bar} gear={gear} screen={Screen.width}x{Screen.height}");
            Capture("00-layout-check");
            Check(((RectTransform)xp.transform).anchorMin.x==0&&bar.xMax<gear.xMin,"XP left anchor retained and clears gear");
            Check(gear.xMax<=Screen.safeArea.xMax+1&&gear.yMax<=Screen.safeArea.yMax+1,"gear inside safe area");
        }
        static IEnumerator Checks(LevelManager level)
        {
            yield return new WaitForSecondsRealtime(2);
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            if(TutorialGuide.IsOpen)TutorialGuide.Instance.Close();
            if(level.EntityManager.AbilitySelectionDialog.MenuOpen)level.EntityManager.AbilitySelectionDialog.Close();
            level.SetRunFlowPaused(true);Time.timeScale=1;level.PlayerCharacter.AddMaxHealthBonus(100000);level.PlayerCharacter.GainHealth(100000);
            var ui=ApothecaryUI.Instance;ui.Show("hud");yield return Frame();CheckHud();Capture("01-pc-hud");
            var keyboard=InputSystem.AddDevice<Keyboard>();
            yield return Press(keyboard,UnityEngine.InputSystem.Key.Tab);Check(ui.Page=="run"&&Time.timeScale==0,"TAB opens character tab and pauses");
            Check(((RectTransform)Button("Character bookmark").transform).rect.height>((RectTransform)Button("Settings bookmark").transform).rect.height,"selected character bookmark is taller");
            yield return Frame();Capture("02-character");
            Click("Active skill slot");yield return Frame();Check(GameObject.Find("Skill details")!=null,"skill details accessible inside book");
            Object.FindObjectsOfType<Button>().First(b=>b.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="닫기")).onClick.Invoke();yield return Frame();
            yield return Press(keyboard,UnityEngine.InputSystem.Key.Escape);Check(ui.Page=="settings"&&ui.Tab==0&&Time.timeScale==0,"ESC selects settings without resuming");
            Check(((RectTransform)Button("Settings bookmark").transform).rect.height>((RectTransform)Button("Character bookmark").transform).rect.height,"selected settings bookmark is taller");
            yield return Frame();Capture("03-settings");
            for(int i=0;i<4;i++){Click("Settings category "+i);yield return Frame();Check(ui.Tab==i,"settings category "+i);}
            Click("Settings category 0");yield return Frame();
            var slider=Object.FindObjectsOfType<Slider>().Single(s=>s.name=="Augment VFX opacity slider");float original=GamePreferences.Current.augmentVfxOpacity;slider.value=.35f;
            yield return Press(keyboard,UnityEngine.InputSystem.Key.Tab);Check(ui.Page=="run"&&Time.timeScale==0&&Mathf.Approximately(GamePreferences.Current.augmentVfxOpacity,original),"TAB restores character tab and cancels uncommitted preview");
            Click("Settings bookmark");yield return Frame();Click("Character bookmark");yield return Frame();Check(ui.Page=="run"&&Time.timeScale==0,"bookmark switches preserve pause");
            yield return Press(keyboard,UnityEngine.InputSystem.Key.Tab);Check(ui.Page=="hud"&&Time.timeScale==1,"TAB closes book and resumes");
            yield return Press(keyboard,UnityEngine.InputSystem.Key.Escape);Check(ui.Page=="settings"&&Time.timeScale==0,"ESC directly opens settings from gameplay");
            yield return Press(keyboard,UnityEngine.InputSystem.Key.Escape);Check(ui.Page=="hud"&&Time.timeScale==1,"ESC closes settings and resumes");
            Time.timeScale=0;ui.OpenSettings();Check(ui.Page=="hud","external pause not stolen");Time.timeScale=1;
            var mobile=level.PlayerCharacter.gameObject.AddComponent<MobileGameplayControls>();
            typeof(MobileGameplayControls).GetField("simulateInEditor",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(mobile,true);
            yield return Frame();ui.Show("hud");yield return Frame();
            foreach(var size in new[]{new Vector2Int(1560,720),new Vector2Int(1024,768)})
            {
                Resize(size.x,size.y);yield return new WaitForSecondsRealtime(.6f);yield return Frame();CheckHud();
                var controls=Object.FindObjectsOfType<MobileTouchControl>();Check(controls.Count(c=>c.Joystick)==1&&controls.Single(c=>c.Joystick).name=="MOVE","only left mobile joystick");
                Capture("04-mobile-"+size.x);Click("HUD journal gear");yield return Frame();Check(ui.Page=="run","touch gear opens unified book");
                Click("Settings bookmark");yield return Frame();
                foreach(var text in ui.GetComponentsInChildren<TMP_Text>()) {text.ForceMeshUpdate();Check(!text.isTextOverflowing,"settings text fits "+text.name);}
                Capture("05-mobile-settings-"+size.x);Click("Record close");yield return Frame();Check(Time.timeScale==1,"touch close resumes");
            }
            // Exercise the real field chest, including range eligibility and the original drop callback.
            var chest=Object.FindObjectsOfType<Chest>().First(c=>c.CanInteract);
            var player=level.PlayerCharacter;
            var otherChest=Object.FindObjectsOfType<Chest>().First(c=>c!=chest&&c.CanInteract);
            player.transform.position=chest.transform.position+Vector3.down*.65f;
            otherChest.transform.position=chest.transform.position+Vector3.left*.7f+Vector3.down*.65f;
            Physics2D.SyncTransforms();yield return new WaitForSecondsRealtime(.4f);yield return Frame();
            var input=ui.GetComponent<WorldObjectTouchInput>();
            var target=Camera.main.WorldToScreenPoint(chest.GetComponentInChildren<SpriteRenderer>().bounds.center);
            bool opened=false;Action<Chest> onOpen=c=>{if(c==chest)opened=true;};Chest.OnAnyChestOpened+=onOpen;
            Check(input.TryInteract(target),"touch selects nearby chest");
            yield return null;yield return null;
            Check(opened&&!chest.CanInteract&&otherChest.CanInteract,"object tap opens only the selected nearby chest");Chest.OnAnyChestOpened-=onOpen;
            Check(!input.TryInteract(new Vector2(5,Screen.height-5)),"empty field tap is not an interaction");
            Time.timeScale=0;Check(!input.TryInteract(target),"paused object tap ignored");Time.timeScale=1;
            InputSystem.RemoveDevice(keyboard);GamePreferences.Apply(prefs,false);SessionState.SetBool(Key+"Done",true);
        }
    }
}
