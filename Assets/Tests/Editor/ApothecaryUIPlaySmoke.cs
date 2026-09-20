using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

namespace Vampire.Tests.Editor
{
    // End-to-end scene test, exercising the same buttons as a player. Saves are restored on exit.
    [InitializeOnLoad]
    public static class ApothecaryUIPlaySmoke
    {
        const string Key="ApothecarySmoke";
        static string Output=>Path.GetFullPath("Library/ApothecaryUIProof");
        static double deadline,next;
        static int step;
        static bool failed;
        static ApothecaryButtonFeedback feedback;
        static int beforeBuy;
        [Serializable] class Save { public string key;public bool exists;public int value; }
        [Serializable] class Backup {public Save[] values;public bool equippedExists;public string equipped;public bool settingsExists;public string settings;}
        static string BackupPath=>Path.GetFullPath("Library/ApothecarySaveBackup.json");
        static ApothecaryUIPlaySmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void RefreshAndRun(){Vampire.EditorTools.ApothecaryUIInstaller.Install();Run();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            Check(config!=null&&config.characters.Length>0&&config.items.Length>=2&&config.relics.Length>0,"Catalog valid");
            var keys=new[]{"LobbySilverCoins","Coins","Apothecary.ReducedMotion"}.Concat(config.relics.Select(r=>"RelicUnlocked_"+r.relicId)).Concat(config.characters.Select(c=>"LobbyUnlock.Character."+c.name)).ToArray();
            var backup=new Backup{values=keys.Select(k=>new Save{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray(),equippedExists=PlayerPrefs.HasKey("EquippedRelicId"),equipped=PlayerPrefs.GetString("EquippedRelicId")};
            backup.settingsExists=PlayerPrefs.HasKey(GamePreferences.SaveKey);backup.settings=PlayerPrefs.GetString(GamePreferences.SaveKey);
            File.WriteAllText(BackupPath,JsonUtility.ToJson(backup));
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            SetGameViewSize();Attach();EditorApplication.EnterPlaymode();
        }
        static void SetGameViewSize(int width=1280,int height=720)
        {
            // Match the landscape layout in editor and headless screenshot capture.
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(type).position=new Rect(0,0,width,height+21);
        }
        static void Attach()
        {
            deadline=EditorApplication.timeSinceStartup+180;next=0;step=0;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type)
        {
            if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert) {failed=true;SessionState.SetBool(Key+"Failed",true);}
        }
        static void Finish()
        {
            if(File.Exists(BackupPath))
            {
                var b=JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath));
                foreach(var s in b.values){if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);}
                if(b.equippedExists)PlayerPrefs.SetString("EquippedRelicId",b.equipped);else PlayerPrefs.DeleteKey("EquippedRelicId");
                if(b.settingsExists)PlayerPrefs.SetString(GamePreferences.SaveKey,b.settings);else PlayerPrefs.DeleteKey(GamePreferences.SaveKey);
                PlayerPrefs.Save();
            }
            SessionState.SetBool(Key,false);
            Debug.Log("[ApothecarySmoke] FINISHED failed="+SessionState.GetBool(Key+"Failed",false));
            EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
                else if(!EditorApplication.isPlayingOrWillChangePlaymode)Finish();return;
            }
            if(EditorApplication.timeSinceStartup>deadline||failed)
            {SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);return;}
            if(!EditorApplication.isPlaying || ApothecaryUI.Instance==null || EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.65;
            try
            {
                var ui=ApothecaryUI.Instance;
                switch(step++)
                {
                    case 0:
                        // Synthetic pointer events must not race the desktop mouse outside the hidden Game View.
                        EventSystem.current.GetComponent<InputSystemUIInputModule>().enabled=false;
                        var testPreferences=GamePreferences.Current.Copy();testPreferences.reducedMotion=false;testPreferences.pauseOnFocusLoss=false;testPreferences.muteOnFocusLoss=false;GamePreferences.Apply(testPreferences,false);
                        SilverWallet.Set(5000);LobbyLoadoutData.Clear();
                        RelicSaveData.Unlock(ui.Config.relics[0].relicId);RelicSaveData.Equip(ui.Config.relics[0].relicId);
                        Check(ui.Page=="main","Starts on main");break;
                    case 1: Check(!HasText("실버"),"Main has no silver");Capture("01-main");Click("게임 시작");break;
                    case 2:
                        Check(ui.Page=="prepare","Start directly opens preparation");Check(HasText("실버"),"Preparation shows silver");Capture("02-preparation-relic");
                        feedback=Button("출전하기").GetComponent<ApothecaryButtonFeedback>();feedback.OnPointerEnter(new PointerEventData(EventSystem.current));break;
                    case 3:
                        Check(feedback.IsHighlighted,"Mouse hover highlights");Check(feedback.visual.localScale.x>1,"Hover enlarges visual");Capture("03-deploy-hover");
                        feedback.OnPointerDown(new PointerEventData(EventSystem.current));break;
                    case 4:
                        Check(feedback.IsPressed&&feedback.visual.localScale.x<1,"Press contracts");Capture("04-deploy-pressed");
                        feedback.OnPointerUp(new PointerEventData(EventSystem.current));feedback.OnPointerExit(new PointerEventData(EventSystem.current));
                        var touch=new ExtendedPointerEventData(EventSystem.current){pointerType=UIPointerType.Touch};
                        feedback.OnPointerEnter(touch);feedback.OnPointerDown(touch);Check(feedback.IsPressed,"Touch press feedback");feedback.OnPointerUp(touch);
                        Check(!feedback.IsHighlighted&&!feedback.IsPressed,"Touch release clears hover");
                        feedback.OnPointerDown(touch);feedback.OnPointerExit(touch);Check(!feedback.IsPressed,"Drag off cancels press");
                        Click("아이템");break;
                    case 5: Capture("05-items-before-purchase");beforeBuy=SilverWallet.Silver;ClickPrefix("구매하기");break;
                    case 6:
                        Check(LobbyLoadoutData.SelectedCarryItems.Count==1,"First item purchased once");Check(SilverWallet.Silver<beforeBuy,"Silver deducted");
                        Check(!Button("구매 완료").interactable,"Same item cannot be purchased twice");
                        ui.GetType().GetField("selection",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(ui,1);
                        ui.GetType().GetMethod("Render",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(ui,null);break;
                    case 7:ClickPrefix("구매하기");break;
                    case 8:
                        Check(LobbyLoadoutData.SelectedCarryItems.Count==2,"Two distinct carry items");Check(!Button("구매 불가 · 2 / 2").interactable,"Full purchase disabled");
                        Check(Button("출전하기").interactable,"Deploy stays available");Capture("06-items-full");
                        int balance=SilverWallet.Silver;string reason;Check(!LobbyLoadoutData.TryBuyAndEquip(ui.Config.items[0],ui.Config.items[0].silverCost,out reason)&&balance==SilverWallet.Silver,"Rejected purchase preserves money");
                        ui.Show("unlock",0);break;
                    case 9: Capture("07-unlock-characters");Click("유물");break;
                    case 10: Capture("08-unlock-relics");Click("아이템");break;
                    case 11: Capture("09-unlock-items");Click("증강");break;
                    case 12: Capture("10-unlock-augments");ui.Show("settings");break;
                    case 13: Capture("11-settings");ui.Show("prepare",1);break;
                    case 14:
                        Check(LobbyLoadoutData.SelectedCarryItems.Count==2,"Loadout survives navigation");Click("출전하기");next+=2;break;
                    case 15:
                        Check(ui.Page=="hud","Game scene entered");Check(CrossSceneData.StartingLobbyItems.Length==0,"Items consumed once by game applier");
                        Check(LobbyLoadoutData.SelectedCarryItems.Count==0,"Lobby loadout cleared on launch");
                        var player=UnityEngine.Object.FindObjectOfType<LevelManager>().PlayerCharacter;
                        Check(player.DisplayName=="아시","Actual Ashi spawns");
                        float expectedHp=ui.Config.characters[0].hp+ui.Config.items.Take(2).Sum(i=>i.maxHpBoost);
                        if(ui.Config.relics[0].effectType==RelicBlueprint.RelicEffectType.MaxHealth)expectedHp+=ui.Config.relics[0].effectValue;
                        Check(Mathf.Abs(player.MaxHealth-expectedHp)<.01f,"Equipped relic and item HP reach the player");
                        Check(player.DamageMultiplier>=1+ui.Config.items.Take(2).Sum(i=>i.atkDamageBoost)-.001f,"Purchased damage reaches the player");
                        ui.OpenRunBook();break;
                    case 16:Check(ui.Page=="run"&&Time.timeScale==0,"TAB pauses");Capture("12-tab-status");Click("유물");break;
                    case 17: Capture("13-tab-relics");Click("아이템");break;
                    case 18: Capture("14-tab-items");Click("도감");break;
                    case 19: Capture("15-tab-augments");ui.CloseRunBook();Check(Time.timeScale>0,"Close restores time");
                        UnityEngine.Object.FindObjectOfType<LevelManager>().LevelPassed(null);Check(ui.Page=="result","Level completion opens result");break;
                    case 20:Capture("16-clear");ApothecaryUI.TryShowResult(false);break;
                    case 21:Check(!HasText("실버"),"Result has no silver balance");Capture("17-failure");Click("출전 준비");break;
                    case 22:
                        Check(ui.Page=="prepare","Result returns to preparation");Check(LobbyLoadoutData.SelectedCarryItems.Count==0,"Consumables not reused on retry");
                        SilverWallet.Set(0);ui.Show("prepare",1);break;
                    case 23:
                        Check(!Button("실버 부족").interactable,"Unaffordable purchase is disabled");
                        int emptyBalance=SilverWallet.Silver;string error;
                        Check(!LobbyLoadoutData.TryBuyAndEquip(ui.Config.items[0],ui.Config.items[0].silverCost,out error)&&SilverWallet.Silver==emptyBalance,"Unaffordable purchase preserves wallet");
                        Capture("18-insufficient-silver");
                        SetGameViewSize(1920,900);break;
                    case 24: Capture("19-mobile-wide");ValidateHitTargets();SetGameViewSize(1280,800);break;
                    case 25: Capture("20-tablet");ValidateHitTargets();Click("출전하기");next+=2;break;
                    case 26:UnityEngine.Object.FindObjectOfType<LevelManager>().GameOver();Check(ui.Page=="result","Player death opens result");break;
                    case 27:Click("메인으로");break;
                    case 28:Check(ui.Page=="main"&&!HasText("실버"),"Failure returns to main without balance");Click("설정");break;
                    case 29:Capture("21-settings-graphics");ValidateHitTargets();Click("사운드");break;
                    case 30:
                        var sliders=ui.GetComponentsInChildren<Slider>();Check(sliders.Length==4,"Four independent sound categories");
                        var sliderRect=(RectTransform)sliders[0].transform;
                        var sliderTouch=new ExtendedPointerEventData(EventSystem.current){pointerType=UIPointerType.Touch,position=RectTransformUtility.WorldToScreenPoint(null,sliderRect.TransformPoint(new Vector3(sliderRect.rect.xMax-2,0,0))),button=PointerEventData.InputButton.Left};
                        sliders[0].value=0;ExecuteEvents.Execute(sliders[0].gameObject,sliderTouch,ExecuteEvents.pointerDownHandler);
                        Check(sliders[0].value>.95f,"Touch on sound slider changes value");ExecuteEvents.Execute(sliders[0].gameObject,sliderTouch,ExecuteEvents.pointerUpHandler);
                        sliders[0].value=.7f;sliders[1].value=.23f;sliders[2].value=.42f;sliders[3].value=.61f;
                        Check(Mathf.Abs(AudioListener.volume-.7f)<.001f,"Master audio preview");
                        var audio=GameAudioManager.Instance;
                        Check(Mathf.Abs(audio.MusicVolume-.23f)<.001f&&Mathf.Abs(audio.EffectsVolume-.42f)<.001f&&Mathf.Abs(audio.UIVolume-.61f)<.001f,"Audio sources independently follow categories");
                        Click("적용");Check(Mathf.Abs(GamePreferences.Read().music-.23f)<.001f,"Applied sound is persisted");break;
                    case 31:Capture("22-settings-sound");Click("편의 기능");break;
                    case 32:Capture("23-settings-accessibility");Click("기본값");break;
                    case 33:Click("뒤로");Check(Mathf.Abs(GamePreferences.Current.music-.23f)<.001f,"Cancel restores applied settings after default preview");break;
                    case 34:Click("설정");break;
                    case 35:Click(GamePreferences.Current.displayMode==0?"전체 화면":"창 모드");break;
                    case 36:Click("적용");Check(HasText("되돌리기"),"Display changes require confirmation");break;
                    case 37:Capture("24-settings-display-confirm");Click("되돌리기");Check(GamePreferences.Current.displayMode==GamePreferences.Read().displayMode,"Display revert restores saved mode");break;
                    case 38:Click(GamePreferences.Current.displayMode==0?"전체 화면":"창 모드");break;
                    case 39:Click("적용");ui.GetType().GetField("displayDeadline",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(ui,Time.unscaledTime-1);break;
                    case 40:Check(!HasText("되돌리기"),"Display confirmation times out safely");Click("뒤로");break;
                    case 41:Click("게임 시작");break;
                    case 42:Click("출전하기");next+=2;break;
                    case 43:Check(Mathf.Abs(GamePreferences.Current.music-.23f)<.001f,"Settings survive scene transition");ui.OpenRunBook();break;
                    case 44:Click("설정");break;
                    case 45:Check(Time.timeScale==0,"In-run settings retain pause");Click("뒤로");Check(ui.Page=="run"&&Time.timeScale==0,"Settings return to paused book");break;
                    case 46:ui.CloseRunBook();Check(Time.timeScale>0,"Gameplay resumes after settings");SessionState.SetBool(Key+"Done",true);break;
                }
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
        }
        static bool HasText(string text)=>ApothecaryUI.Instance.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains(text));
        static Button Button(string text)=>ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.gameObject.name=="Button "+text);
        static void Click(string text){Activate(Button(text));}
        static void ClickPrefix(string text){Activate(ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.gameObject.name.StartsWith("Button "+text)));}
        static void Activate(Button button)
        {
            Check(button.interactable,"Button active: "+button.name);
            Canvas.ForceUpdateCanvases();
            var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position),button=PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
            Check(hits.Count>0&&hits[0].gameObject==button.gameObject,"Pointer hits actual target: "+button.name);
            ExecuteEvents.Execute(button.gameObject,e,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject,e,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject,e,ExecuteEvents.pointerClickHandler);
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception("[ApothecarySmoke] FAIL "+message);Debug.Log("[ApothecarySmoke] PASS "+message);}
        static void ValidateHitTargets()
        {
            foreach(var button in ApothecaryUI.Instance.GetComponentsInChildren<Button>())
            {
                Rect r=((RectTransform)button.transform).rect;
                Check(r.width>=48 && r.height>=44,"Touch target remains usable: "+button.name);
            }
        }
        static void Capture(string name)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
        }
    }
}
