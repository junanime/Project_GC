using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class MobileHudSmoke
    {
        const string Key="MobileHudSmoke20261006";
        static bool started;
        static double deadline;
        static MobileHudSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            Resize(1280,720);Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+240;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string message,string stack,LogType type)
        {
            if(type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);
        }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){Time.timeScale=1;EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log("[MobileHudSmoke] FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(started||!EditorApplication.isPlaying||level==null||level.PlayerCharacter==null||StageEntryLoading.IsLoading)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static void Check(bool ok,string label)
        {
            if(!ok)throw new Exception("[MobileHudSmoke] FAIL "+label);
            Debug.Log("[MobileHudSmoke] PASS "+label);
        }
        static void Resize(int w,int h)=>EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,w,h+21);
        static void Capture(string name)
        {
            Directory.CreateDirectory(Path.GetFullPath("../work"));
            typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"mobile-"+name+".png"});
        }
        static Rect Bounds(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var min=RectTransformUtility.WorldToScreenPoint(null,corners[0]);var max=RectTransformUtility.WorldToScreenPoint(null,corners[2]);
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        static PointerEventData Touch(MobileTouchControl control,int id,Vector2 local)
        {
            return new PointerEventData(EventSystem.current){pointerId=id,position=RectTransformUtility.WorldToScreenPoint(null,control.transform.TransformPoint(local))};
        }
        static IEnumerator Checks(LevelManager level)
        {
            var preferences=GamePreferences.Current.Copy();preferences.pauseOnFocusLoss=false;preferences.muteOnFocusLoss=false;GamePreferences.Apply(preferences,false);
            if(level.EntityManager.AbilitySelectionDialog.MenuOpen)level.EntityManager.AbilitySelectionDialog.Close();
            if(TutorialGuide.IsOpen)TutorialGuide.Instance.Close();
            level.SetRunFlowPaused(true);Time.timeScale=1;
            var character=level.PlayerCharacter;
            RunScore.Exclude("Mobile HUD and map zoom verification");
            character.AddMaxHealthBonus(10000);character.GainHealth(10000);
            Check(!Object.FindObjectsOfType<Button>().Any(b=>b.name=="Active skill Space"||b.name=="HUD dash"),"desktop skills remain keyboard only");
            var mobile=character.GetComponent<MobileGameplayControls>()??character.gameObject.AddComponent<MobileGameplayControls>();
            typeof(MobileGameplayControls).GetField("simulateInEditor",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(mobile,true);
            yield return null;yield return null;
            ApothecaryUI.Instance.Show("hud");Time.timeScale=1;
            yield return new WaitForSecondsRealtime(.4f);
            var controls=Object.FindObjectsOfType<MobileTouchControl>();
            var move=controls.Single(c=>c.name=="MOVE");
            Check(controls.Count(c=>c.Joystick)==1&&!controls.Any(c=>c.name=="AIM / CHARGE"),"one left joystick, no right joystick");
            var knob=move.Knob.GetComponent<MobileControlDisc>();
            Check(knob!=null&&knob.color.r>.8f&&knob.color.g<.2f&&knob.color.a<.7f,"red translucent round joystick handle");
            move.OnPointerDown(Touch(move,11,new Vector2(move.Radius,0)));
            Check(move.Knob.anchoredPosition.x>move.Radius*.99f,"handle follows right drag");
            Check(MobileGameplayInput.ChargeHeld&&MobileGameplayInput.Aim.x>.99f,"single stick preserves charge and movement aim");
            Check(!Object.FindObjectsOfType<Button>().Any(b=>b.name=="HUD interact"||b.name=="Passive skill status"),"interaction and passive HUD remain hidden");
            var dash=Object.FindObjectsOfType<Button>().Single(b=>b.name=="HUD dash");
            Check(dash.GetComponentInChildren<SharedDashIcon>()!=null,"original shared dash icon restored");
            int charges=character.CurrentDashCharges;
            var beforeDash=character.transform.position;
            dash.onClick.Invoke();yield return null;
            Check(character.CurrentDashCharges==charges-1&&character.IsDashing,"mobile tap consumes one charge and starts actual dash");
            Check(!dash.interactable,"dash button disabled during dash");
            yield return new WaitForSecondsRealtime(character.DashDuration+.1f);
            Check(Vector3.Distance(beforeDash,character.transform.position)>.1f,"dash moves character while joystick is held");
            Check(move.Knob.anchoredPosition.x>0,"dash touch preserves joystick gesture");
            charges=character.CurrentDashCharges;Time.timeScale=0;yield return null;
            dash.onClick.Invoke();
            Check(character.CurrentDashCharges==charges&&!dash.interactable,"paused dash cannot consume charges");
            Time.timeScale=1;yield return null;
            move.OnPointerDown(Touch(move,11,new Vector2(move.Radius,0)));
            var active=Object.FindObjectsOfType<Button>().Single(b=>b.name=="Active skill Space");
            Check(active.GetComponent<ShiniSkillButton>()!=null,"Shini hold handler retained");
            active.onClick.Invoke();yield return null;
            Check(character.Skills.Active||character.Skills.CooldownRemaining>0||character.Skills.IsSummoning,"mobile tap casts actual active skill");
            move.OnDrag(Touch(move,22,new Vector2(-move.Radius,0)));
            Check(move.Knob.anchoredPosition.x>0,"second finger cannot steal movement");
            move.OnDrag(Touch(move,11,new Vector2(-move.Radius,move.Radius)));
            Check(move.Knob.anchoredPosition.x<0&&move.Knob.anchoredPosition.y>0&&move.Knob.anchoredPosition.magnitude<=move.Radius+.1f,"diagonal follows and clamps inside rim");
            Capture("joystick-drag");
            move.OnPointerUp(Touch(move,11,Vector2.zero));
            Check(move.Knob.anchoredPosition==Vector2.zero&&!MobileGameplayInput.ChargeHeld,"release centers handle and clears charge");
            var slots=Object.FindObjectsOfType<InventorySlot>();
            Check(slots.Length==4,"four original item slots retained");
            Check(slots.All(s=>s.GetComponent<Button>()!=null),"existing item buttons retained");
            var inventory=(RectTransform)slots[0].transform.parent;
            Check(inventory.anchorMin==new Vector2(.5f,0),"item tray at bottom center");
            var gear=Object.FindObjectsOfType<Button>().Single(b=>b.name=="HUD journal gear");
            foreach(var size in new[]{new Vector2(1280,720),new Vector2(1560,720),new Vector2(1024,768)})
            {
                Resize((int)size.x,(int)size.y);yield return new WaitForSecondsRealtime(.35f);Canvas.ForceUpdateCanvases();
                var bounds=Bounds(inventory);
                Check(bounds.xMin>=Screen.safeArea.xMin&&bounds.xMax<=Screen.safeArea.xMax&&bounds.yMin>=Screen.safeArea.yMin&&bounds.yMax<=Screen.safeArea.yMax,"items inside safe area "+Screen.width);
                Check(!bounds.Overlaps(Bounds((RectTransform)move.transform)),"items clear movement stick "+Screen.width);
                var gearBounds=Bounds((RectTransform)gear.transform);
                Check(gearBounds.xMin>=Screen.safeArea.xMin&&gearBounds.xMax<=Screen.safeArea.xMax&&gearBounds.yMax<=Screen.safeArea.yMax,"gear inside safe area");
                Check(!bounds.Overlaps(gearBounds),"tray clears gear");
                var activeBounds=Bounds((RectTransform)active.transform);
                Check(activeBounds.xMax<=Screen.safeArea.xMax&&activeBounds.yMin>=Screen.safeArea.yMin,"active skill inside safe area");
                Check(!bounds.Overlaps(activeBounds)&&!activeBounds.Overlaps(Bounds((RectTransform)move.transform)),"active skill clears inventory and joystick");
                var dashBounds=Bounds((RectTransform)dash.transform);
                Check(dashBounds.xMax<=Screen.safeArea.xMax&&dashBounds.yMin>=Screen.safeArea.yMin,"dash inside safe area");
                Check(activeBounds.xMax<dashBounds.xMin&&Mathf.Abs(activeBounds.yMin-dashBounds.yMin)<1,"active left of dash on same row with a gap");
                Check(!bounds.Overlaps(dashBounds)&&!dashBounds.Overlaps(Bounds((RectTransform)move.transform)),"dash clears inventory and joystick");
                Capture("layout-"+Screen.width+"x"+Screen.height);
            }
            gear.onClick.Invoke();yield return null;Canvas.ForceUpdateCanvases();
            var zoom=Object.FindObjectOfType<RunBookMapZoom>();Check(zoom!=null,"Tab map zoom exists");
            var center=Bounds((RectTransform)zoom.transform).center;
            zoom.OnScroll(new PointerEventData(EventSystem.current){position=center,scrollDelta=Vector2.up});
            Check(zoom.Zoom>1,"mouse wheel zooms while paused");float wheel=zoom.Zoom;
            zoom.OnPointerDown(new PointerEventData(EventSystem.current){pointerId=41,position=center-Vector2.right*30});
            zoom.OnPointerDown(new PointerEventData(EventSystem.current){pointerId=42,position=center+Vector2.right*30});
            zoom.OnDrag(new PointerEventData(EventSystem.current){pointerId=42,position=center+Vector2.right*90});
            Check(zoom.Zoom>wheel,"two-finger spread zooms in");float spread=zoom.Zoom;
            zoom.OnDrag(new PointerEventData(EventSystem.current){pointerId=42,position=center+Vector2.right*10});
            Check(zoom.Zoom<spread,"two-finger pinch zooms out");
            zoom.OnPointerUp(new PointerEventData(EventSystem.current){pointerId=41});zoom.OnPointerUp(new PointerEventData(EventSystem.current){pointerId=42});
            zoom.Gesture(Vector2.zero,Vector2.zero,100);Check(zoom.Zoom==5,"maximum zoom");Capture("tab-map-zoom");
            zoom.Gesture(Vector2.zero,Vector2.zero,.001f);Check(zoom.Zoom==1,"full map minimum zoom");
            ApothecaryUI.Instance.CloseRunBook();yield return null;
            move.OnPointerDown(Touch(move,11,Vector2.right*move.Radius));Time.timeScale=0;yield return null;yield return null;
            Check(move.Knob.anchoredPosition==Vector2.zero&&!move.gameObject.activeInHierarchy,"pause clears gesture and hides controls");
            Time.timeScale=1;level.SetRunFlowPaused(false);
            var progression=StageProgression.Ensure(level);
            Check(StageProgression.StageTwoEnabled,"new colon stage enabled");
            Check(progression.FinalBossDefeated(character.transform.position,100)&&progression.Portal!=null,"stage one boss leaves escape portal");
            Check(!progression.TryEnter(character,progression.Portal)&&!level.IsLevelEnded&&progression.Capsule!=null,"stage one leaves pursuit hole and amphibious capsule");
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
