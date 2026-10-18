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
    public static class CharacterHudSmoke
    {
        const string Key="CharacterHudSmoke";
        static bool started;static double deadline;static int checks;
        static string Output=>Path.GetFullPath("Library/CharacterHudProof");
        static CharacterHudSmoke(){if(SessionState.GetBool(Key,false))Attach();}
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
            if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("CHARACTER_HUD_TIMEOUT");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log($"CHARACTER_HUD_FINISHED checks={checks} failed={failed}");EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(started||!EditorApplication.isPlaying||level?.PlayerCharacter==null||ApothecaryUI.Instance==null)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static void Check(bool ok,string label){if(!ok)throw new Exception("CHARACTER_HUD_FAIL "+label);checks++;Debug.Log("CHARACTER_HUD_PASS "+label);}
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
            var originalPrefs=GamePreferences.Current.Copy();var prefs=originalPrefs.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            if(TutorialGuide.IsOpen)TutorialGuide.Instance.Close();
            if(level.EntityManager.AbilitySelectionDialog.MenuOpen)level.EntityManager.AbilitySelectionDialog.Close();
            level.SetRunFlowPaused(true);Time.timeScale=1;level.PlayerCharacter.AddMaxHealthBonus(100000);level.PlayerCharacter.GainHealth(100000);
            var player=level.PlayerCharacter;var original=player.Blueprint;
            var ui=ApothecaryUI.Instance;
            var quest=player.GetComponent<PrescriptionRuntime>();var originalQuest=quest.Capture();
            quest.Restore(new PrescriptionState{tag=0,quests=new[]{0,1,3},progress=new[]{150f,1f,12f}});
            var inventory=level.EntityManager.Inventory;
            foreach(var c in ui.Config.characters)
            {
                typeof(Character).GetField("characterBlueprint",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,c);
                typeof(ApothecaryUI).GetField("character",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui,c);
                CrossSceneData.CharacterBlueprint=c;ui.Show("hud");yield return Frame();yield return new WaitForSecondsRealtime(.5f);
                var view=Object.FindObjectOfType<PrescriptionScrollView>();
                Check(view.HeaderColor==PreparationIdentity.ColorFor(c),"header matches lobby active frame "+c.name);
                Check(view.Magnet.CharacterKey==OctoberArt.CharacterKey(c),"magnet character "+c.name);
                Check(view.Title.text.Contains("퀘스트")&&!view.Title.text.Contains("비전서"),"renamed quest "+c.name);
                Check(inventory.Slots.All(slot=>slot.GetComponent<InventoryHudCard>()!=null),"four live cards "+c.name);
                Check(inventory.Slots.All(slot=>slot.transform.Find("Card stock").GetComponent<TMP_Text>().text==slot.CountLabel),"stock labels match runtime "+c.name);
                foreach(var text in view.GetComponentsInChildren<TMP_Text>())
                {text.ForceMeshUpdate();Check(!text.isTextOverflowing,"quest text fits "+c.name+" "+text.name);}
                Check(!Object.FindObjectsOfType<Transform>().Any(t=>t.name=="Light wood medicine tray"&&t.gameObject.activeInHierarchy),"wood tray retired");
                yield return Frame();Capture(OctoberArt.CharacterKey(c)+"-open");
                var pin=view.Magnet.rectTransform.position;
                Click("Quest toggle");yield return new WaitForSecondsRealtime(.12f);
                Check(!view.Expanded,"fold requested "+c.name);
                yield return new WaitForSecondsRealtime(.35f);
                Check(!view.Expanded&&view.Reveal==0,"closed "+c.name);
                Check(view.Magnet.rectTransform.position==pin,"magnet stays fixed "+c.name);
                Check(!view.Claim.gameObject.activeInHierarchy,"closed claim hidden "+c.name);Capture(OctoberArt.CharacterKey(c)+"-closed");
                // Render the halfway geometry deterministically; slow editor frames may skip it in real time.
                view.enabled=false;
                typeof(PrescriptionScrollView).GetField("<Reveal>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(view,.5f);
                typeof(PrescriptionScrollView).GetMethod("ApplyReveal",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,null);
                yield return Frame();Capture(OctoberArt.CharacterKey(c)+"-folding");view.enabled=true;
                Click("Quest toggle");yield return new WaitForSecondsRealtime(.5f);
                Check(view.Expanded&&view.Reveal==1,"reopens "+c.name);
                view.SetExpanded(false);yield return new WaitForSecondsRealtime(.1f);view.SetExpanded(true);yield return new WaitForSecondsRealtime(.5f);
                Check(view.Reveal==1,"rapid reversal settles "+c.name);
            }
            var last=Object.FindObjectOfType<PrescriptionScrollView>();
            yield return new WaitForSecondsRealtime(4.2f);Check(last.Expanded,"no four second auto-close");
            ui.OpenRunBook();yield return Frame();Click("Record close");yield return Frame();
            Check(Object.FindObjectOfType<PrescriptionScrollView>().Expanded,"open state survives book");
            var mobile=player.gameObject.AddComponent<MobileGameplayControls>();
            typeof(MobileGameplayControls).GetField("simulateInEditor",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(mobile,true);
            foreach(var size in new[]{new Vector2Int(1560,720),new Vector2Int(1024,768)})
            {
                Resize(size.x,size.y);yield return new WaitForSecondsRealtime(.6f);ui.Show("hud");yield return Frame();
                var view=Object.FindObjectOfType<PrescriptionScrollView>();
                foreach(var slot in inventory.Slots)
                {
                    var box=Bounds((RectTransform)slot.transform);Check(Screen.safeArea.Contains(box.min)&&Screen.safeArea.Contains(box.max),"mobile card inside screen "+size.x);
                    var label=slot.transform.Find("Item caption").GetComponent<TMP_Text>();label.ForceMeshUpdate();Check(!label.isTextOverflowing,"mobile caption fits");
                    var ev=new PointerEventData(EventSystem.current){position=box.center,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(ev,hits);
                    Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<InventorySlot>()==slot,"mobile item hit target");
                }
                var note=Bounds(view.Rows[2].rectTransform);Check(Screen.safeArea.Contains(note.min)&&Screen.safeArea.Contains(note.max),"mobile quest inside screen");
                Capture("mobile-"+size.x);Click("Quest toggle");yield return new WaitForSecondsRealtime(.5f);Check(!view.Expanded,"mobile folds");Click("Quest toggle");yield return new WaitForSecondsRealtime(.5f);Check(view.Expanded,"mobile opens");
            }
            // Completion and reward use the same live runtime, and cannot be claimed twice.
            quest.Restore(new PrescriptionState{tag=0,quests=new[]{0,1,3},progress=new[]{150f,1f,25f}});
            yield return Frame();var reward=Object.FindObjectOfType<PrescriptionScrollView>();Check(reward.Claim.interactable,"completed reward enabled");
            Click("Noble reward claim");yield return Frame();Check(quest.Claimed&&level.EntityManager.AbilitySelectionDialog.MenuOpen,"quest reward opens existing noble selector");
            Check(!quest.TryClaim(level.EntityManager.AbilitySelectionDialog),"reward cannot be claimed twice");
            level.EntityManager.AbilitySelectionDialog.Close();Time.timeScale=1;ui.Show("hud");yield return Frame();
            Check(Object.FindObjectOfType<PrescriptionScrollView>().Title.text.Contains("완료"),"claimed title retained");
            quest.Restore(originalQuest);CrossSceneData.CharacterBlueprint=original;
            typeof(Character).GetField("characterBlueprint",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,original);
            GamePreferences.Apply(originalPrefs,false);SessionState.SetBool(Key+"Done",true);
        }
    }
}
