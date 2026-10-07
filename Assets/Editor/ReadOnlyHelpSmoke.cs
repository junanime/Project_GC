using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vampire.Editor
{
    [InitializeOnLoad]
    public static class ReadOnlyHelpSmoke
    {
        const string Key="ReadOnlyHelpSmoke";
        static int step, passed;
        static bool done, failed;
        static double next, deadline;
        static HudTooltip tooltip;
        static int[] counts;
        static float cooldown;
        [Serializable] class Catalog { public TutorialGuide.Entry[] entries; }
        [Serializable] class Saved { public string key; public bool exists; public int value; }
        [Serializable] class Backup { public Saved[] values; }
        static string Output=>Path.GetFullPath("Library/ReadOnlyHelpProof");
        static ReadOnlyHelpSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);RestorePrefs();
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys=catalog.entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins"});
            File.WriteAllText(Output+"/prefs-backup.json",JsonUtility.ToJson(new Backup{values=keys.Select(k=>new Saved{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            step=passed=0;done=failed=false;next=0;deadline=EditorApplication.timeSinceStartup+180;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)failed=true;}
        static void RestorePrefs()
        {
            string path=Output+"/prefs-backup.json";if(!File.Exists(path))return;
            foreach(var s in JsonUtility.FromJson<Backup>(File.ReadAllText(path)).values)
                if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);
            PlayerPrefs.Save();File.Move(path,Output+"/prefs-restored-"+DateTime.UtcNow.Ticks+".json");
        }
        static void Check(bool ok,string text){if(!ok)throw new Exception("HELP_FAIL "+text);passed++;Debug.Log("HELP_PASS "+text);}
        static void Capture(string name)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Output,name+".png"),texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);
        }
        static Button Named(string name)=>UnityEngine.Object.FindObjectsOfType<Button>().First(b=>b.name==name);
        static void Click(string text)=>UnityEngine.Object.FindObjectsOfType<Button>().First(b=>b.GetComponentsInChildren<TMP_Text>().Any(t=>t.text==text)).onClick.Invoke();
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)failed=true;
            if(failed||done)
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                RestorePrefs();SessionState.SetBool(Key,false);Debug.Log($"HELP_FINISHED passed={passed} failed={failed}");EditorApplication.Exit(failed?1:0);return;
            }
            if(!EditorApplication.isPlaying||ApothecaryUI.Instance==null||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.65;
            try
            {
                var ui=ApothecaryUI.Instance;var level=UnityEngine.Object.FindObjectOfType<LevelManager>();
                switch(step++)
                {
                    case 0:
                        var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
                        CrossSceneData.CharacterBlueprint=ui.Config.characters.First(c=>c.skills!=null&&c.skills.kind==CharacterSkillDefinition.SkillKind.Shini);
                        CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
                        SceneManager.LoadScene(1);Time.timeScale=0;next+=2;break;
                    case 1:
                        Check(level!=null&&TutorialGuide.Instance!=null,"game scene and guide ready");level.enabled=false;
                        if(TutorialGuide.IsOpen)TutorialGuide.Instance.Close();Time.timeScale=0;
                        EventSystem.current.GetComponent<InputSystemUIInputModule>().enabled=false;
                        if(level.EntityManager.AbilitySelectionDialog.MenuOpen)level.EntityManager.AbilitySelectionDialog.Close();
                        Time.timeScale=0;
                        foreach(var slot in level.PlayerInventory.Slots)
                            for(int i=0;i<3;i++)slot.AddItem(new GameObject("Read-only help sample").AddComponent<HelpSampleItem>());
                        counts=level.PlayerInventory.Slots.Select(s=>s.Count).ToArray();cooldown=level.PlayerCharacter.Skills.CooldownRemaining;
                        Check(level.PlayerInventory.Slots.All(s=>s.IconImage.rectTransform.sizeDelta==new Vector2(76,76)),"four icons use equal centered square bounds");
                        Check(level.PlayerInventory.Slots.All(s=>s.CountRect.sizeDelta==new Vector2(30,28)),"counts stay within their slots");
                        tooltip=Named("Active skill R").GetComponent<HudTooltip>();
                        tooltip.OnPointerEnter(new ExtendedPointerEventData(EventSystem.current){pointerType=UIPointerType.Touch});break;
                    case 2:
                        Capture("01-hud");
                        Check(GameObject.Find("Read only HUD help")==null,"touch does not open hover card");
                        tooltip.OnPointerEnter(new PointerEventData(EventSystem.current));break;
                    case 3:
                        Check(GameObject.Find("Read only HUD help")!=null,"mouse opens active skill help without clicking");
                        Check(level.PlayerCharacter.Skills.CooldownRemaining==cooldown,"hover does not cast active skill");
                        Check(!GameObject.Find("Read only HUD help").GetComponent<CanvasGroup>().blocksRaycasts,"tooltip never captures gameplay input");
                        Capture("02-skill-hover");tooltip.OnPointerExit(null);
                        Check(GameObject.Find("Read only HUD help")==null,"exit hides help immediately");
                        Time.timeScale=1;Named("Skill help without casting").onClick.Invoke();Check(ui.Page=="run"&&Time.timeScale==0,"help button opens paused status");break;
                    case 4:
                        Named("Active skill slot").onClick.Invoke();Check(level.PlayerCharacter.Skills.CooldownRemaining==cooldown,"status skill click only reads description");break;
                    case 5:
                        foreach(var data in ui.Config.characters.Where(c=>c.skills!=null))
                            foreach(bool active in new[]{false,true})
                            {
                                typeof(ApothecaryUI).GetMethod("ShowSkillDetails",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,new object[]{data,active});
                                Canvas.ForceUpdateCanvases();
                                var panel=(GameObject)typeof(ApothecaryUI).GetField("skillDetails",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui);
                                foreach(var label in panel.GetComponentsInChildren<TMP_Text>())label.ForceMeshUpdate();
                                Check(panel.GetComponentsInChildren<TMP_Text>().All(t=>!t.isTextOverflowing),data.name+(active?" active":" passive")+" description fits");
                            }
                        typeof(ApothecaryUI).GetMethod("ShowSkillDetails",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,new object[]{level.PlayerCharacter.Blueprint,true});
                        break;
                    case 6:
                        Canvas.ForceUpdateCanvases();
                        Check(GameObject.Find("Skill details").GetComponentsInChildren<TMP_Text>().All(t=>!t.isTextOverflowing),"long skill description fits panel");
                        Capture("03-active-description");Click("닫기");Click("소모품");break;
                    case 7:
                        Check(ui.Tab==4&&Time.timeScale==0,"consumables view remains paused");Capture("04-consumables");
                        Check(counts.SequenceEqual(level.PlayerInventory.Slots.Select(s=>s.Count)),"reading preserves item counts");Click("안내");break;
                    case 8:
                        Check(ui.Tab==5,"guide tab opens");Capture("05-guide");
                        var entry=TutorialGuide.Instance.Entries.First(e=>e.id=="mechanic/blood-clot");
                        string key=TutorialGuide.SeenKey(entry.id);bool had=PlayerPrefs.HasKey(key);int value=PlayerPrefs.GetInt(key);
                        Check(TutorialGuide.Instance.Review(entry.id),"existing animation can be reviewed");
                        Check(PlayerPrefs.HasKey(key)==had&&PlayerPrefs.GetInt(key)==value,"manual review preserves first discovery flags");
                        Check(Time.timeScale==0,"review keeps game paused");break;
                    case 9:
                        Capture("06-guide-replay");TutorialGuide.Instance.Close();
                        Check(Time.timeScale==0&&ui.Page=="run","closing review returns to paused book");break;
                    case 10:
                        ui.CloseRunBook();Check(Time.timeScale==1&&ui.Page=="hud","closing book restores gameplay");Time.timeScale=0;
                        tooltip=level.PlayerInventory.Slots[0].GetComponent<HudTooltip>();tooltip.OnPointerEnter(new PointerEventData(EventSystem.current));break;
                    case 11:
                        Check(GameObject.Find("Read only HUD help")!=null,"consumable hover is available");
                        Check(counts.SequenceEqual(level.PlayerInventory.Slots.Select(s=>s.Count)),"consumable hover does not use items");Capture("07-item-hover");
                        Check(HelpSampleItem.Uses==0,"no consumable effect fired while reading");
                        tooltip.OnPointerExit(null);done=true;break;
                }
            }
            catch(Exception e){Debug.LogException(e);failed=true;}
        }
    }
    public sealed class HelpSampleItem : Collectable
    {
        public static int Uses;
        protected override void OnCollected(){Uses++;}
    }
}
