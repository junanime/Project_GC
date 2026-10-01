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
    public static class OctoberIntegrationSmoke
    {
        const string Key="OctoberIntegrationSmoke";
        static string Output=>Path.GetFullPath("Library/OctoberIntegrationProof");
        static double deadline,next;
        static int step;
        static bool failed;
        static ApothecaryButtonFeedback feedback;
        static int beforeBuy;
        static bool gameplayStarted;
        [Serializable] class Save { public string key;public bool exists;public int value; }
        [Serializable] class Backup {public Save[] values;public bool equippedExists;public string equipped;public bool settingsExists;public string settings;}
        static string BackupPath=>Path.GetFullPath("Library/OctoberIntegrationSaveBackup.json");
        static OctoberIntegrationSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void RefreshAndRun(){Vampire.EditorTools.OctoberContentInstaller.Install();Run();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            Check(config!=null&&config.characters.Length>0&&config.items.Length>=2&&config.relics.Length>0,"Catalog valid");
            var keys=new[]{"LobbySilverCoins","Coins","Apothecary.ReducedMotion","October.StartingNeedle"}.Concat(config.relics.Select(r=>"RelicUnlocked_"+r.relicId)).Concat(config.characters.Select(c=>"LobbyUnlock.Character."+c.name)).Concat(config.characters.Select(StartingNeedleSelection.SaveKey)).ToArray();
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
            deadline=EditorApplication.timeSinceStartup+300;next=0;step=0;gameplayStarted=false;OctoberGameplayChecks.Done=false;
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
            Debug.Log("[OctoberIntegrationSmoke] FINISHED failed="+SessionState.GetBool(Key+"Failed",false));
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
                        EventSystem.current.GetComponent<InputSystemUIInputModule>().enabled=false;
                        var prefs=GamePreferences.Current.Copy();prefs.reducedMotion=false;prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
                        SilverWallet.Set(5000);LobbyLoadoutData.Clear();StartingNeedleSelection.Set(null);
                        Check(ui.Config.items.Length==66,"66 item catalog entries");
                        Check(ui.Config.items.All(i=>i.octoberId>0&&i.itemIcon!=null&&!string.IsNullOrWhiteSpace(i.description)),"All items have icon and effect");
                        foreach(var c in ui.Config.characters)for(int state=0;state<3;state++)Check(OctoberArt.Character(c,state)!=null,"Character art "+c.name+" state "+state);
                        Check(ui.GetComponentsInChildren<OctoberTitleMotion>().Length==5,"Four actors and interactive germ on title");break;
                    case 1:Capture("01-main");Click("게임 시작");break;
                    case 2:Capture("02-preparation-characters");feedback=Button("출전하기").GetComponent<ApothecaryButtonFeedback>();feedback.OnPointerEnter(new PointerEventData(EventSystem.current));break;
                    case 3:Check(feedback.IsHighlighted,"Hover feedback");feedback.OnPointerDown(new PointerEventData(EventSystem.current));break;
                    case 4:Check(feedback.IsPressed&&feedback.visual.localScale.x<1,"Pressed feedback");feedback.OnPointerUp(new PointerEventData(EventSystem.current));feedback.OnPointerExit(new PointerEventData(EventSystem.current));Click("아이템");break;
                    case 5:Capture("03-preparation-items");beforeBuy=SilverWallet.Silver;ClickPrefix("구매 · ");break;
                    case 6:Check(LobbyLoadoutData.SelectedCarryItems.Count==1&&SilverWallet.Silver==beforeBuy-ui.Config.items[0].silverCost,"One purchase, exact debit");SetSelection(ui,1);break;
                    case 7:ClickPrefix("구매 · ");break;
                    case 8:Check(LobbyLoadoutData.SelectedCarryItems.Count==2,"Two carry items");Click("캐릭터");break;
                    case 9:Activate(ui.GetComponentsInChildren<Button>().First(b=>b.name=="Choice 0 1"));break;
                    case 10:Check(LobbyLoadoutData.SelectedCarryItems.Count==2,"Items survive character changes");Activate(ui.GetComponentsInChildren<Button>().First(b=>b.name=="Choice 0 0"));ui.Show("prepare",1);break;
                    case 11:Capture("04-preparation-weapons");Click("유물");break;
                    case 12:Capture("05-preparation-relics");Click("출전하기");next+=3;break;
                    case 13:
                        Check(ui.Page=="hud","Gameplay loaded");
                        var player=UnityEngine.Object.FindObjectOfType<LevelManager>().PlayerCharacter;
                        var runtime=player.GetComponent<OctoberItemRuntime>();Check(runtime!=null&&runtime.Has(1)&&runtime.Has(2),"Carry item mechanics installed once");
                        float hp=player.MaxHealth;Check(!runtime.Give(ui.Config.items[1])&&player.MaxHealth==hp,"Duplicate item cannot apply twice");
                        Check(CrossSceneData.StartingLobbyItems.Length==0,"Carry queue consumed");
                        foreach(var item in ui.Config.items)runtime.Give(item);
                        Check(runtime.State.owned.Count==66,"All 66 effects registered without errors");
                        runtime.Healed(0,1000);Check(runtime.State.overflow<=player.MaxHealth*.25f+.01f,"Overheal storage is capped");
                        float charged=runtime.Experience(10);Check(charged==0&&runtime.State.delayedExp>=11.5f,"Experience mill defers and bonuses XP");
                        var restored=JsonUtility.FromJson<OctoberItemState>(JsonUtility.ToJson(runtime.State));Check(restored.owned.Count==66&&restored.delayedExp>0,"Transfer state retains owned mechanics and pending XP");
                        next+=8;break;
                    case 14:
                        Capture("06-live-all-items");
                        foreach(var pause in UnityEngine.Object.FindObjectsOfType<PauseMenu>(true))Check(!pause.gameObject.activeInHierarchy,"Legacy pause control hidden");
                        var dialog=UnityEngine.Object.FindObjectOfType<AbilitySelectionDialog>();
                        if(dialog!=null&&dialog.MenuOpen){Capture("13-training-panels");UnityEngine.Object.FindObjectsOfType<AbilityCard>().First().Selected();step=14;next+=.6;break;}
                        ui.OpenRunBook();break;
                    case 15:Check(ui.Page=="run"&&Time.timeScale==0,"TAB pauses gameplay");Capture("07-tab-map");Click("설정");break;
                    case 16:Capture("08-esc-settings");ui.Show("run",2);break;
                    case 17:Capture("09-run-inventory");ui.CloseRunBook();break;
                    case 18:
                        var list=ui.Config.items.Where(i=>(int)i.itemRarity>0).GroupBy(i=>i.itemRarity).Select(g=>g.First()).ToList();
                        UnityEngine.Object.FindObjectOfType<StatsManager>().IncreaseCoinsGained(999);
                        ui.ShowOctoberShop(list,10,UnityEngine.Object.FindObjectOfType<MerchantUIManager>());Time.timeScale=0;break;
                    case 19:Capture("10-merchant");ui.Show("hud");Time.timeScale=1;break;
                    case 20:
                        if(!gameplayStarted){gameplayStarted=true;ui.StartCoroutine(OctoberGameplayChecks.Run());}
                        if(!OctoberGameplayChecks.Done){step=20;break;}
                        ApothecaryUI.TryShowResult(false);break;
                    case 21:Capture("11-result-fail");ApothecaryUI.TryShowResult(true);break;
                    case 22:Capture("12-result-win");Check(ui.GetComponentsInChildren<Button>().Length==3,"Same three result actions");Click("준비화면");next+=2;break;
                    case 23:Check(ui.Page=="prepare","Returns to loadout");Check(LobbyLoadoutData.SelectedCarryItems.Count==0,"Used consumables not duplicated");SessionState.SetBool(Key+"Done",true);break;
                }
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
        }
        static void SetSelection(ApothecaryUI ui,int index)
        {ui.GetType().GetField("selection",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(ui,index);ui.GetType().GetMethod("Render",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(ui,null);}
        static bool HasText(string text)=>ApothecaryUI.Instance.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains(text));
        static Button Button(string text)=>ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.gameObject.name=="Button "+text);
        static void Click(string text){Activate(Button(text));}
        static void ClickPrefix(string text){Activate(ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.gameObject.name.StartsWith("Button "+text)||(b.name=="Selection action"&&b.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.StartsWith(text)))));}
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
        static void Check(bool condition,string message){if(!condition)throw new Exception("[OctoberIntegrationSmoke] FAIL "+message);Debug.Log("[OctoberIntegrationSmoke] PASS "+message);}
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
