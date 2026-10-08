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
            if(started||!EditorApplication.isPlaying||level==null||level.PlayerCharacter==null)return;
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
            character.AddMaxHealthBonus(10000);character.GainHealth(10000);
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
            var dash=Object.FindObjectsOfType<Button>().Single(b=>b.name=="HUD dash");
            Check(dash.GetComponentInChildren<SharedDashIcon>()!=null,"dash uses same glyph as reward panel");
            dash.onClick.Invoke();
            Check(move.Knob.anchoredPosition.x>0&&MobileGameplayInput.ChargeHeld,"dash does not release movement or charge");
            move.OnDrag(Touch(move,22,new Vector2(-move.Radius,0)));
            Check(move.Knob.anchoredPosition.x>0,"second finger cannot steal movement");
            move.OnDrag(Touch(move,11,new Vector2(-move.Radius,move.Radius)));
            Check(move.Knob.anchoredPosition.x<0&&move.Knob.anchoredPosition.y>0&&move.Knob.anchoredPosition.magnitude<=move.Radius+.1f,"diagonal follows and clamps inside rim");
            Capture("joystick-drag");
            move.OnPointerUp(Touch(move,11,Vector2.zero));
            Check(move.Knob.anchoredPosition==Vector2.zero&&!MobileGameplayInput.ChargeHeld,"release centers handle and clears charge");
            var slots=Object.FindObjectsOfType<InventorySlot>();
            Check(slots.Length==4,"four original item slots retained");
            Check(slots.All(s=>s.GetComponent<Button>().onClick.GetPersistentMethodName(0)=="UseItem"),"existing item tap bindings retained");
            var inventory=(RectTransform)slots[0].transform.parent;
            Check(inventory.Find("Light wood medicine tray")!=null&&inventory.anchorMin==new Vector2(.5f,0),"wooden item tray at bottom center");
            var interact=Object.FindObjectsOfType<Button>().Single(b=>b.name=="HUD interact");
            interact.onClick.Invoke();
            Check(MobileGameplayInput.ConsumeInteraction()&&!MobileGameplayInput.ConsumeInteraction(),"HUD interaction consumed once");
            foreach(var size in new[]{new Vector2(1280,720),new Vector2(1560,720),new Vector2(1024,768)})
            {
                Resize((int)size.x,(int)size.y);yield return new WaitForSecondsRealtime(.35f);Canvas.ForceUpdateCanvases();
                var bounds=Bounds(inventory);
                Check(bounds.xMin>=Screen.safeArea.xMin&&bounds.xMax<=Screen.safeArea.xMax&&bounds.yMin>=Screen.safeArea.yMin&&bounds.yMax<=Screen.safeArea.yMax,"items inside safe area "+Screen.width);
                Check(!bounds.Overlaps(Bounds((RectTransform)move.transform))&&!bounds.Overlaps(Bounds((RectTransform)interact.transform)),"items do not overlap movement or actions "+Screen.width);
                foreach(var button in new[]{dash,interact,Object.FindObjectsOfType<Button>().Single(b=>b.name=="Active skill R"),Object.FindObjectsOfType<Button>().Single(b=>b.name=="Passive skill status")})
                {
                    var b=Bounds((RectTransform)button.transform);
                    Check(b.xMin>=Screen.safeArea.xMin&&b.xMax<=Screen.safeArea.xMax&&b.yMin>=Screen.safeArea.yMin&&b.yMax<=Screen.safeArea.yMax,"action inside safe area "+button.name+" "+Screen.width);
                    Check(!bounds.Overlaps(b),"tray clears "+button.name);
                }
                var scroll=Object.FindObjectOfType<PrescriptionScrollView>();
                var status=Object.FindObjectsOfType<Button>().Single(b=>b.name=="HUD status");
                Check(Bounds((RectTransform)status.transform).yMax<=Bounds(scroll.Dock).yMin,"status below prescription");
                var mini=Object.FindObjectsOfType<RectTransform>().FirstOrDefault(r=>r.name=="MiniMapRoot");
                if(mini!=null)
                    Check(!Bounds(mini).Overlaps(Bounds((RectTransform)Object.FindObjectsOfType<Button>().Single(b=>b.name=="Active skill R").transform)),"minimap does not overlap skill button "+Screen.width);
                Capture("layout-"+Screen.width+"x"+Screen.height);
            }
            move.OnPointerDown(Touch(move,11,Vector2.right*move.Radius));Time.timeScale=0;yield return null;yield return null;
            Check(move.Knob.anchoredPosition==Vector2.zero&&!move.gameObject.activeInHierarchy,"pause clears gesture and hides controls");
            Time.timeScale=1;SessionState.SetBool(Key+"Done",true);
        }
    }
}
