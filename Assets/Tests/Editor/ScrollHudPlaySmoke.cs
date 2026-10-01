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

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class ScrollHudPlaySmoke
    {
        const string Key="ScrollHudSmoke";
        static bool started;static int checks;static double deadline;
        static ScrollHudPlaySmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            Resize(1280,720);Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            deadline=EditorApplication.timeSinceStartup+240;started=false;checks=0;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Failed",false)||SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){Time.timeScale=1;SetMobile(false);EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log("[ScrollHudSmoke] FINISHED failed="+failed+" checks="+checks);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(!EditorApplication.isPlaying||started||level==null||level.PlayerCharacter==null||level.CurrentLevelTime<1)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static void Check(bool ok,string text){if(!ok)throw new Exception("[ScrollHudSmoke] FAIL "+text);checks++;Debug.Log("[ScrollHudSmoke] PASS "+text);}
        static object Get(object obj,string field)=>obj.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
        static void Resize(int width,int height)=>EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,width,height+21);
        static void Capture(string name)
        {
            Directory.CreateDirectory(Path.GetFullPath("../work"));
            typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"scroll-"+name+".png"});
        }
        static Rect ScreenRect(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);var canvas=rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        static void SetMobile(bool value)=>typeof(MobileGameplayInput).GetProperty("Active").SetValue(null,value);
        static IEnumerator Press(UnityEngine.InputSystem.Key key)
        {
            var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
        }
        static IEnumerator Checks(LevelManager level)
        {
            var preferences=GamePreferences.Current.Copy();preferences.pauseOnFocusLoss=false;preferences.muteOnFocusLoss=false;GamePreferences.Apply(preferences,false);
            var ui=ApothecaryUI.Instance;var dialog=level.EntityManager.AbilitySelectionDialog;var manager=Object.FindObjectOfType<AbilityManager>();
            if(dialog.MenuOpen)dialog.Close();level.PlayerCharacter.AddMaxHealthBonus(10000);level.PlayerCharacter.GainHealth(10000);
            level.SetRunFlowPaused(true);yield return null;
            var view=ui.GetComponentInChildren<PrescriptionScrollView>();var quest=level.PlayerCharacter.GetComponent<PrescriptionRuntime>();
            Check(view!=null&&!view.Expanded&&view.Reveal==0,"Vision scroll defaults to rolled state");
            Check(!ui.GetComponentsInChildren<Button>().Any(b=>b.name=="Mobile status"||b.name=="Mobile settings"||b.name.Contains("TAB")||b.name.Contains("ESC")),"PC HUD has no TAB/ESC touch buttons");
            Capture("pc-rolled");
            yield return Press(UnityEngine.InputSystem.Key.Tab);Check(ui.Page=="run","PC TAB still opens status");
            yield return Press(UnityEngine.InputSystem.Key.Tab);Check(ui.Page=="hud","PC TAB closes status");
            yield return Press(UnityEngine.InputSystem.Key.Escape);Check(ui.Page=="settings","PC ESC still opens settings");
            yield return Press(UnityEngine.InputSystem.Key.Escape);Check(ui.Page=="hud","PC ESC returns to gameplay");
            view=ui.GetComponentInChildren<PrescriptionScrollView>();
            Time.timeScale=0;view.Toggle.onClick.Invoke();yield return new WaitForSecondsRealtime(.10f);
            Check(view.Reveal>0&&view.Reveal<1,"Unrolling animates while game time is frozen");Capture("unrolling");
            yield return new WaitForSecondsRealtime(.25f);Time.timeScale=1;
            Check(view.Expanded&&view.Reveal==1,"Scroll finishes opening");
            for(int tag=0;tag<3;tag++)for(int seed=0;seed<8;seed++)
            {
                quest.StartPrescription(tag,seed);yield return null;
                foreach(var row in view.Rows)
                {
                    row.ForceMeshUpdate();
                    Check(row.textBounds.size.y<=row.rectTransform.rect.height+2&&row.textBounds.size.x<=row.rectTransform.rect.width+2,"Quest text fits tag "+tag+" seed "+seed);
                }
            }
            quest.StartPrescription(0,5);quest.Record((PrescriptionRuntime.Goal)quest.Capture().quests[0],10000);yield return null;
            Check(view.Rows.Any(r=>r.text.Contains("<s>")),"Completed quest uses strikethrough");Capture("pc-open");
            view.Toggle.onClick.Invoke();yield return new WaitForSecondsRealtime(.35f);
            Check(view.Reveal==0&&quest.Completed==1,"Folding preserves progress");
            Check(!ui.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.name.StartsWith("Scroll quest")),"Closed list neither renders nor receives pointer input");
            view.Toggle.onClick.Invoke();yield return new WaitForSecondsRealtime(.35f);
            foreach(var size in new[]{new Vector2(1024,768),new Vector2(1560,720),new Vector2(1280,720)})
            {
                Resize((int)size.x,(int)size.y);yield return new WaitForSecondsRealtime(.3f);
                var rect=ScreenRect(view.Dock);var window=(RectTransform)Get(view,"window");var paper=ScreenRect(window);
                Check(Mathf.Abs(rect.xMax-Screen.safeArea.xMax)<2,"Scroll arm meets safe right edge "+Screen.width);
                Check(rect.yMax<Screen.safeArea.yMax-35&&paper.yMin>0,"Scroll stays below experience bar and within screen");
                Capture("open-"+Screen.width+"x"+Screen.height);
            }
            SetMobile(true);ui.Show("hud");yield return null;
            Check(ui.GetComponentsInChildren<Button>().Any(b=>b.name=="Mobile status")&&ui.GetComponentsInChildren<Button>().Any(b=>b.name=="Mobile settings"),"Mobile simulation retains both navigation buttons");
            var mobileStatus=ui.GetComponentsInChildren<Button>().Single(b=>b.name=="Mobile status");mobileStatus.onClick.Invoke();Check(ui.Page=="run","Mobile status button functions");ui.CloseRunBook();
            var mobileSettings=ui.GetComponentsInChildren<Button>().Single(b=>b.name=="Mobile settings");mobileSettings.onClick.Invoke();Check(ui.Page=="settings","Mobile settings button functions");ui.Back();
            Capture("mobile-navigation");SetMobile(false);ui.Show("hud");yield return null;
            view=ui.GetComponentInChildren<PrescriptionScrollView>();
            foreach(int id in quest.Capture().quests)quest.Record((PrescriptionRuntime.Goal)id,10000);yield return null;
            Check(view.Claim.interactable&&view.Claim.GetComponentInChildren<TextMeshProUGUI>().text=="고귀 증강 선택","Completed prescription offers Noble reward");
            view.Claim.onClick.Invoke();yield return new WaitForSecondsRealtime(.6f);
            var offers=(List<Ability>)Get(dialog,"displayedAbilities");var cards=dialog.GetComponentsInChildren<AbilityCard>();
            Check(offers.Count==3&&offers.All(a=>a.Name.StartsWith("고귀 증강: ")),"All mechanic rewards are named Noble augments");
            Check(cards.All(c=>c.UsesNoblePanel),"Noble rewards use their dedicated liquid overframe art");
            Check(!view.gameObject.activeInHierarchy,"Prescription HUD hides behind reward modal");Capture("noble-reward");
            cards[0].Selected();yield return null;Check(quest.Claimed&&!dialog.MenuOpen,"Noble reward claim completes once");
            var parent=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true)[0];if(!parent.Owned)manager.AcquireVer4Ability(parent);
            dialog.Open(false);yield return new WaitForSecondsRealtime(.5f);cards=dialog.GetComponentsInChildren<AbilityCard>();
            var samples=new List<Ver4AugmentOffer>();
            foreach(var grade in new[]{AugmentUpgradeGrade.Legendary,AugmentUpgradeGrade.Supreme,AugmentUpgradeGrade.Original})
            {
                var sample=new GameObject("Grade preview").AddComponent<Ver4AugmentOffer>();samples.Add(sample);
                sample.Configure(manager.Ver4,grade==AugmentUpgradeGrade.Original?Ver4RewardKind.Original:Ver4RewardKind.Numeric,grade,parent,0,.12f,parent.Name,"공격력 +12%\n강화 등급: "+AugmentUpgradeOdds.DisplayName(grade));
                var card=cards[samples.Count-1];card.Init(dialog,sample,0);
                Check((bool)Get(card,"themedCard")&&((Image)Get(card,"cardBackgroundImage")).sprite==AugmentPanelTheme.Frame(grade),"Numeric/Original frame maps independently: "+grade);
            }
            Check(AugmentUpgradeOdds.DisplayName(AugmentUpgradeGrade.Legendary)=="전설","Numeric Legendary name stays unchanged");
            Check(AugmentPanelTheme.Frame(AugmentUpgradeGrade.Legendary)!=AugmentPanelTheme.Frame(AugmentUpgradeGrade.Supreme),"Legendary and Supreme have distinct source art");
            yield return new WaitForSecondsRealtime(.6f);Capture("legendary-supreme-original");dialog.Close();foreach(var sample in samples)Object.Destroy(sample.gameObject);
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
