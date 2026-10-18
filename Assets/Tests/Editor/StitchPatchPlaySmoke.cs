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
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
    [InitializeOnLoad] public static class StitchPatchPlaySmoke
    {
        const string Key="StitchPatchSmoke";
        static bool started;static int checks;static double deadline;
        [Serializable] class Save {public string key;public bool exists;public int value;}
        [Serializable] class Backup {public Save[] values;}
        static StitchPatchPlaySmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            File.WriteAllText("Library/StitchPatchSave.json",JsonUtility.ToJson(new Backup{values=config.characters.Select(c=>"LobbyUnlock.Character."+c.name).Select(k=>new Save{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var c in config.characters)PlayerPrefs.SetInt("LobbyUnlock.Character."+c.name,1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){deadline=EditorApplication.timeSinceStartup+300;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("Stitch smoke timed out");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                var save=JsonUtility.FromJson<Backup>(File.ReadAllText("Library/StitchPatchSave.json"));foreach(var v in save.values){if(v.exists)PlayerPrefs.SetInt(v.key,v.value);else PlayerPrefs.DeleteKey(v.key);}PlayerPrefs.Save();
                SessionState.SetBool(Key,false);Debug.Log("[StitchPatch] FINISHED checks="+checks+" failed="+SessionState.GetBool(Key+"Failed",false));EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null){started=true;var host=new GameObject("Stitch QA");Object.DontDestroyOnLoad(host);host.AddComponent<StitchPatchSmokeHost>().StartCoroutine(Checks());}
        }
        static void Check(bool ok,string name){if(!ok)throw new Exception("[StitchPatch] FAIL "+name);checks++;Debug.Log("[StitchPatch] PASS "+name);}
        static object Get(object o,string f)=>o.GetType().GetField(f,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
        static void Click(string name){var b=ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(x=>x.name==name);Check(b.IsInteractable(),"Button enabled "+name);b.onClick.Invoke();}
        static void Capture(string n)=>typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"stitch-"+n+".png"});
        static IEnumerator Settle(){yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();}
        static void Decorations(ApothecaryUI ui)
        {
            if(ui.Page=="run"||ui.Page=="settings")
            {var outer=ui.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Book outer stitch");var close=ui.GetComponentsInChildren<Button>().First(b=>b.name=="Record close");Check(outer.GetSiblingIndex()<close.transform.GetSiblingIndex(),"Close button renders above decorative corner");}
            foreach(var state in ui.GetComponentsInChildren<StitchPatchButton>())
            {
                var visual=state.feedback.visual;
                var skins=visual.GetComponentsInChildren<StitchPatchGraphic>().Where(g=>g.transform.parent==visual&&g.fill).ToArray();
                Check(!state.feedback.body.enabled&&skins.Length==1,"Exactly one replacement frame: "+state.name);
                Check(skins[0].transform.GetSiblingIndex()==0,"Frame stays behind icons and labels: "+state.name);
            }
            if(ui.Page=="run")
            {
                Check(!ui.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Description stitch"),"No extra description box over printed compartments");
                Check(ui.GetComponentsInChildren<StitchPatchGraphic>().Where(g=>g.transform.parent.name=="Inventory stitch").Count()==3,"One continuous seam per inventory column");
                foreach(var b in ui.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Record ")&&b.name.Contains("slot ")))
                {
                    Check(!b.GetComponentsInChildren<StitchPatchGraphic>().Any(),"Inventory cells keep a single plain outline");
                    var r=(RectTransform)b.transform;Check(r.anchorMin.y>=.205f,"Inventory cell stays above description compartment");
                }
            }
            Check(ui.GetComponentsInChildren<StitchPatchGraphic>().All(g=>!g.raycastTarget),"Appliques never block clicks");
            foreach(var f in ui.GetComponentsInChildren<ApothecaryButtonFeedback>())Check(f.sparkle!=null,"Gold selection orbit retained "+f.name);
        }
        static IEnumerator Checks()
        {
            Application.runInBackground=true;var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            yield return Settle();var ui=ApothecaryUI.Instance;Check(ui.GetComponentsInChildren<StitchPatchButton>().Length==4,"All title buttons share stitch skin");Capture("main");
            Click("Button 게임 시작");yield return Settle();Check(ui.Page=="prepare","Title starts preparation");
            for(int n=0;n<ui.Config.characters.Length;n++)
            {
                Click("Choice 0 "+n);yield return Settle();string kind=OctoberArt.CharacterKey(ui.SelectedCharacter);
                Check(ui.SelectedCharacter==ui.Config.characters[n],"Character click changes selection "+kind);
                var motifs=ui.GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("Motif ")).ToArray();Check(motifs.Length==8&&motifs.All(i=>i.sprite==StitchPatchTheme.Motif(kind)),"Only the eight designated appliques use "+kind);
                Check(ui.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="Silver currency dock"),"Independent silver dock exists");
                Check(ui.GetComponentsInChildren<Image>().Any(i=>i.name=="Silver foil coin"&&i.sprite!=null),"Silver coin art present");
                Decorations(ui);Capture("prepare-"+kind);
            }
            Click("Choice 0 0");yield return Settle();
            foreach(var tab in new[]{"무기","유물","아이템","캐릭터"}){Click("Button "+tab);yield return Settle();Check(ui.GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("Motif ")).All(i=>i.sprite==StitchPatchTheme.Motif("Ashi")),"Appliques survive tab "+tab);}
            Click("Button ‹  뒤로");yield return Settle();Check(ui.Page=="main","Back leaves preparation");
            Click("Button 설정");yield return Settle();Check(ui.Page=="settings","Title opens settings");Capture("settings-lobby");
            for(int t=0;t<4;t++){Click("Settings category "+t);yield return Settle();Check(ui.Tab==t,"Settings category switches "+t);}
            Click("Settings resume");yield return Settle();Check(ui.Page=="main","Settings returns to main");
            CrossSceneData.CharacterBlueprint=ui.Config.characters[0];yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game/Level 1.unity");
            LevelManager level=null;while(level==null||level.PlayerCharacter==null||level.CurrentLevelTime<1){level=Object.FindObjectOfType<LevelManager>();yield return null;}
            ui=ApothecaryUI.Instance;var dialog=level.EntityManager.AbilitySelectionDialog;if(dialog.MenuOpen)dialog.Close();
            ui.OpenRunBook();yield return Settle();Check(ui.Page=="run"&&Time.timeScale==0,"Journal pauses world");Decorations(ui);Capture("tab");
            Click("Settings bookmark");yield return Settle();Check(ui.Page=="settings"&&Time.timeScale==0,"Settings bookmark retains pause");Capture("settings-game");
            for(int t=0;t<4;t++){Click("Settings category "+t);yield return Settle();Check(ui.Tab==t,"In-game settings category "+t);}
            Click("Character bookmark");yield return Settle();Check(ui.Page=="run","Character bookmark returns to record");ui.CloseRunBook();yield return Settle();
            dialog.Open(false);yield return new WaitForSecondsRealtime(1.2f);yield return Settle();var cards=dialog.GetComponentsInChildren<AbilityCard>();
            Check(cards.All(c=>!c.GetComponentsInChildren<Image>().Any(i=>i.name.StartsWith("Training leaf"))),"No leaves on augment corners");
            Check(cards.All(c=>c.GetComponentsInChildren<StitchPatchGraphic>().Any(g=>g.stars&&g.twinkle)),"Each regular card has subtle star twinkle");
            Check(cards.All(c=>!c.GetComponentsInChildren<IdentityShape>().Any(g=>g.name=="Glossy training frame"&&g.enabled)),"Old augment frame is disabled beneath replacement");
            Capture("augments");
            var reroll=(Button)Get(dialog,"rerollButton");var skip=(Button)Get(dialog,"skipButton");
            foreach(var b in new[]{reroll,skip})
            {var f=b.GetComponent<ApothecaryButtonFeedback>();Check(f.borderOnly&&!f.useThemeStates,"Reward button preserves color and gold orbit");Check(b.GetComponent<StitchPatchButton>()!=null,"Reward button stitch skin");}
            reroll.onClick.Invoke();yield return Settle();Check(!reroll.interactable,"Reroll still consumes allowance");skip.onClick.Invoke();yield return Settle();Check(!dialog.MenuOpen,"Skip closes without selecting");
            dialog.Open(false);yield return new WaitForSecondsRealtime(1.2f);yield return Settle();cards=dialog.GetComponentsInChildren<AbilityCard>();
            var manager=Object.FindObjectOfType<AbilityManager>();var source=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).First();
            int index=0;foreach(AugmentUpgradeGrade grade in Enum.GetValues(typeof(AugmentUpgradeGrade)))
            {
                var offer=new GameObject("Stitch grade preview").AddComponent<Ver4AugmentOffer>();offer.Configure(manager.Ver4,Ver4RewardKind.Numeric,grade,source,0,1,"테스트","침 피해량 +8%");cards[index%3].Init(dialog,offer,0);yield return Settle();
                var stars=cards[index%3].GetComponentsInChildren<StitchPatchGraphic>().First(g=>g.stars);Check(stars.accent==StitchPatchTheme.CardAccent(grade),"Stars match border "+grade);index++;if(index%3==0)Capture("grades-"+index);
            }
            dialog.Close();yield return Settle();
            typeof(MobileGameplayInput).GetProperty("Active").SetValue(null,true);ui.Show("hud");ui.OpenRunBook();yield return Settle();Check(ui.Page=="run","Mobile journal opens");Capture("tab-mobile");Click("Settings bookmark");yield return Settle();Capture("settings-mobile");ui.CloseRunBook();typeof(MobileGameplayInput).GetProperty("Active").SetValue(null,false);
            SessionState.SetBool(Key+"Done",true);
        }
    }
    public sealed class StitchPatchSmokeHost:MonoBehaviour{}
}
