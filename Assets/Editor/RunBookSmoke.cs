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
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Vampire.Editor
{
    [InitializeOnLoad]
    public static class RunBookSmoke
    {
        const string Key="RunBookSmoke";
        static bool started;static int checks;static double deadline;
        static string Output=>Path.GetFullPath("Library/RunBookProof");
        [Serializable]class Catalog{public TutorialGuide.Entry[] entries;}
        static RunBookSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            RunBookContentInstaller.Install();Directory.CreateDirectory(Output);
            var entries=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text).entries;
            var types=Enum.GetValues(typeof(SyringeSpecialAugmentAbility.SpecialAugmentType)).Cast<SyringeSpecialAugmentAbility.SpecialAugmentType>().ToArray();
            string[] keys=entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins"})
                .Concat(types.Select(t=>"LobbyUnlock.Needle."+t)).Concat(types.Select(StartingNeedleSelection.EnabledKey)).ToArray();
            SessionState.SetString(Key+"Keys",string.Join("|",keys));
            foreach(string k in keys){SessionState.SetBool(Key+k+"Exists",PlayerPrefs.HasKey(k));SessionState.SetInt(Key+k,PlayerPrefs.GetInt(k));}
            foreach(var entry in entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(entry.id),1);
            foreach(var type in types){PlayerPrefs.SetInt("LobbyUnlock.Needle."+type,1);PlayerPrefs.SetInt(StartingNeedleSelection.EnabledKey(type),1);}
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;checks=0;deadline=EditorApplication.timeSinceStartup+300;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string m,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)Debug.LogError("RUNBOOK_TIMEOUT");
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                foreach(string k in SessionState.GetString(Key+"Keys","").Split('|')){if(SessionState.GetBool(Key+k+"Exists",false))PlayerPrefs.SetInt(k,SessionState.GetInt(Key+k,0));else PlayerPrefs.DeleteKey(k);}
                PlayerPrefs.Save();bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);Debug.Log($"RUNBOOK_FINISHED checks={checks} failed={failed}");EditorApplication.Exit(failed?1:0);return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null&&Object.FindObjectOfType<LevelManager>()?.PlayerCharacter!=null)
            {started=true;ApothecaryUI.Instance.StartCoroutine(CheckRun());}
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception("RUNBOOK_FAIL "+message);checks++;Debug.Log("RUNBOOK_PASS "+message);}
        static Button Button(string name)=>Object.FindObjectsOfType<Button>().First(b=>b.name==name);
        static TMP_Text Text(string name)=>Object.FindObjectsOfType<TMP_Text>().First(t=>t.name==name);
        static void Click(string name)
        {
            var button=Button(name);Canvas.ForceUpdateCanvases();
            var position=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
            var ev=new PointerEventData(EventSystem.current){position=position,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(ev,hits);
            Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"click target "+name);
            ExecuteEvents.Execute(button.gameObject,ev,ExecuteEvents.pointerClickHandler);
        }
        static void Capture(string file)
        {var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+file+".png",image.EncodeToPNG());Object.Destroy(image);}
        static IEnumerator Frame(){yield return null;yield return new WaitForEndOfFrame();}
        static IEnumerator CheckRun()
        {
            yield return new WaitForSecondsRealtime(1);
            var ui=ApothecaryUI.Instance;var level=Object.FindObjectOfType<LevelManager>();var player=level.PlayerCharacter;var manager=Object.FindObjectOfType<AbilityManager>();
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            level.enabled=false;foreach(var ability in manager.GetComponentsInChildren<Ability>(true))ability.enabled=false;
            if(TutorialGuide.IsOpen)TutorialGuide.Instance.Close();
            if(level.EntityManager.AbilitySelectionDialog.MenuOpen)level.EntityManager.AbilitySelectionDialog.Close();
            Time.timeScale=1;ui.OpenRunBook();yield return Frame();
            Check(ui.Page=="run"&&Time.timeScale==0,"TAB pauses run");
            Check(GameObject.Find("Exploration record backplate")!=null,"approved backplate applied");
            Check(Text("Record section 2").text=="무기","codex renamed weapon");
            var backdrop=(Image)typeof(ApothecaryUI).GetField("fullScreenBackdrop",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
            Check(backdrop.color.r==0&&backdrop.color.g==0&&backdrop.color.b==0&&Mathf.Approximately(backdrop.color.a,.68f),"black translucent full screen backdrop");
            Check(Object.FindObjectsOfType<TMP_Text>().Count(t=>t.name.StartsWith("Map legend "))==13,"thirteen current map legend labels");
            Check(Object.FindObjectsOfType<Image>().Where(i=>i.name.StartsWith("Map legend icon ")).All(i=>i.sprite!=null),"all legend sprites available");
            Check(Object.FindObjectsOfType<Image>().Where(i=>i.name.StartsWith("Stat icon ")).All(i=>i.sprite!=null),"twelve stat sprites available");
            var raw=GameObject.Find("Live exploration map").GetComponent<RawImage>();Check(raw.texture==ExplorationMapSystem.Instance.FullMapTexture&&raw.uvRect==ExplorationMapSystem.Instance.BookMapUV,"real explored map and matching UV");
            Capture("01-empty");
            player.Relics.Equip(ui.Config.relics.First(r=>r!=null));
            var runtime=OctoberItemRuntime.Get(player);
            foreach(var item in ui.Config.items.Where(i=>i!=null).Take(13))runtime.Give(item);
            foreach(var parent in manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).Take(12))if(!parent.Owned)manager.AcquireVer4Ability(parent);
            if(TutorialGuide.IsOpen)TutorialGuide.Instance.Close();
            var ownedParent=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).First(a=>a.Owned&&StartingNeedleSelection.Owned(a)&&StartingNeedleSelection.Enabled(a));
            var offerObject=new GameObject("Record test original");var offer=offerObject.AddComponent<Ver4AugmentOffer>();
            offer.Configure(manager.Ver4,Ver4RewardKind.Original,AugmentUpgradeGrade.Original,ownedParent,0,0,"test","test");
            Check(manager.Ver4.Apply(offer),"acquire original through actual runtime");Object.Destroy(offerObject);
            for(int i=0;i<2;i++)
            {
                var numericObject=new GameObject("Record test numeric");var numeric=numericObject.AddComponent<Ver4AugmentOffer>();
                numeric.Configure(manager.Ver4,Ver4RewardKind.Numeric,AugmentUpgradeGrade.Common,ownedParent,3,.10f,"test","test");
                Check(manager.Ver4.Apply(numeric),"acquire numeric through actual runtime "+i);Object.Destroy(numericObject);
            }
            string before=JsonUtility.ToJson(UnityEngine.Random.state);var entries=RunBookData.Weapons(manager,ui.Config);
            Check(before==JsonUtility.ToJson(UnityEngine.Random.state),"reading weapons does not roll rewards");
            Check(entries.Count>=12&&entries.All(e=>!string.IsNullOrEmpty(e.body)&&e.icon!=null),"owned weapons with descriptions and icons");
            Check(entries.Any(e=>e.badge=="오리지널 1/9"&&e.body.Contains("1/3")),"original acquisitions shown");
            Check(entries.Any(e=>e.body.Contains("공격 속도 +20%")),"cumulative numeric upgrades shown");
            ui.Show("run");yield return Frame();
            Check(Text("Record count 1").text=="13종","actual acquired item count");
            Check(Text("Record detail 0").text==player.Relics.Equipped.Description,"actual run relic description");
            Capture("02-populated");
            var cooldown=player.Skills.CooldownRemaining;int itemCount=runtime.State.owned.Count;
            Click("Record 0 slot 0");yield return Frame();Check(Text("Record detail 0").text==player.Relics.Equipped.Description,"relic click preserves its matching lower detail");
            Click("Record 2 slot 0");yield return Frame();
            var scroll=GameObject.Find("Record description scroll 2").GetComponent<ScrollRect>();
            Canvas.ForceUpdateCanvases();float beforeScroll=scroll.content.anchoredPosition.y;
            scroll.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-4)});yield return Frame();
            Check(scroll.content.rect.height>scroll.viewport.rect.height&&scroll.content.anchoredPosition.y>beforeScroll,"long acquired weapon description scrolls within lower box");Capture("05-description-scroll");
            Click("Record 1 slot 1");yield return Frame();
            var second=ui.Config.items.Where(i=>i!=null&&runtime.Has(i.octoberId)).ElementAt(1);
            Check(Text("Record detail title 1").text==second.itemName&&Text("Record detail 1").text==second.description,"item click updates its own lower detail");
            Click("Record 2 slot 1");yield return Frame();
            Check(Text("Record detail title 2").text==entries[1].title,"weapon click updates lower detail");
            Check(Text("Record detail title 1").text==second.itemName,"independent item selection preserved");
            var feedback=Button("Record 2 slot 1").GetComponent<ApothecaryButtonFeedback>();
            Check(feedback.chosen&&feedback.sparkle!=null&&feedback.IsHighlighted,"selected weapon keeps existing gold orbit feedback");
            yield return new WaitForSecondsRealtime(.3f);Capture("03-selected");
            Click("Record 1 next");yield return Frame();Check(Text("Record 1 page").text=="2/2","item pagination");
            Click("Record 1 slot 0");yield return Frame();Check(Text("Record detail title 1").text==ui.Config.items.Where(i=>i!=null&&runtime.Has(i.octoberId)).ElementAt(10).itemName,"page two item selectable");
            Click("Record 2 next");yield return Frame();Check(Text("Record 2 page").text.StartsWith("2/"),"weapon pagination");
            Click("Record 2 slot 0");yield return Frame();Check(Text("Record detail title 2").text==entries[10].title,"page two weapon selectable");Capture("04-next-page");
            Check(runtime.State.owned.Count==itemCount&&player.Skills.CooldownRemaining==cooldown,"reading never uses items or skills");
            Click("Active skill slot");yield return Frame();Check(GameObject.Find("Skill details")!=null&&player.Skills.CooldownRemaining==cooldown,"active skill reads without casting");
            Object.FindObjectsOfType<Button>().First(b=>b.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="닫기")).onClick.Invoke();yield return Frame();
            Click("Record settings");yield return Frame();Check(ui.Page=="settings"&&Time.timeScale==0,"settings button keeps pause");ui.Back();yield return Frame();Check(ui.Page=="run"&&Time.timeScale==0,"settings returns to record");
            Click("Record consumables");yield return Frame();Check(ui.Tab==4,"consumable help retained");
            Object.FindObjectsOfType<Button>().First(b=>b.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="안내")).onClick.Invoke();yield return Frame();Check(ui.Tab==5,"guide retained");
            Object.FindObjectsOfType<Button>().First(b=>b.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="탐험 기록")).onClick.Invoke();yield return Frame();Check(ui.Tab==0&&GameObject.Find("Exploration record backplate")!=null,"back from guide to new layout");
            foreach(var label in Object.FindObjectsOfType<TMP_Text>().Where(t=>t.name.StartsWith("Record")||t.name.StartsWith("Stat")||t.name.StartsWith("Map legend")))
            {label.ForceMeshUpdate();if(!label.name.StartsWith("Record detail "))Check(!label.isTextOverflowing,"text fits "+label.name);}
            Click("Record close");yield return Frame();Check(ui.Page=="hud"&&Time.timeScale==1,"close restores gameplay");
            var reward=level.EntityManager.AbilitySelectionDialog;reward.Open();yield return Frame();Time.timeScale=0;
            ui.OpenRunBook();yield return Frame();Check(ui.Page=="run"&&reward.MenuOpen,"record opens over an active level-up reward");
            Click("Record close");yield return Frame();Check(Time.timeScale==0&&reward.MenuOpen&&reward.GetComponent<CanvasGroup>().alpha==1,"closing restores reward without unpausing");reward.Close();
            Time.timeScale=1;ui.OpenRunBook();yield return Frame();
            var oldRect=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position;
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1024,789);
            yield return new WaitForSecondsRealtime(.6f);yield return Frame();Capture("06-window-fit");
            Check(GameObject.Find("Exploration record backplate").GetComponent<RectTransform>().rect.width==1280,"layout retains proportions when window changes");
            Click("Record close");yield return Frame();Check(Time.timeScale==1,"close remains clickable after resize");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=oldRect;
            Time.timeScale=0;SessionState.SetBool(Key+"Done",true);
        }
    }
}
