using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class CharacterNoblePanelSmoke
    {
        const string Key = "CharacterNoblePanelSmoke";
        static bool started;
        static int checks;
        static double deadline;
        static CharacterNoblePanelSmoke() { if (SessionState.GetBool(Key,false)) Attach(); }
        public static void Run()
        {
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Fail",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1600,941);
            Directory.CreateDirectory("../work");
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+480;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string message,string trace,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Fail",true); }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Fail",true);
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Fail",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool fail=SessionState.GetBool(Key+"Fail",false);SessionState.SetBool(Key,false);
                Debug.Log("CHARACTER_NOBLE_FINISHED failed="+fail+" checks="+checks);
                EditorApplication.Exit(fail?1:0);return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {
                started=true;var host=new GameObject("Character noble QA").AddComponent<CharacterNoblePanelTestHost>();
                Object.DontDestroyOnLoad(host);host.StartCoroutine(Test());
            }
        }
        static void Check(bool ok,string message)
        {if(!ok)throw new Exception("CHARACTER_NOBLE_FAIL "+message);checks++;Debug.Log("CHARACTER_NOBLE_PASS "+message);}
        static object Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
        static void Invoke(object o,string n)=>o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);
        static void Capture(string n)=>typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"character-noble-"+n+".png"});
        static void CheckCard(AbilityCard card,string character)
        {
            Check(card.UsesNoblePanel,"noble layout "+character);
            Check(((Image)Field(card,"abilityImage")).enabled,"revealed artwork visible");
            Check(((RectTransform)card.transform).sizeDelta==new Vector2(360,540),"identical panel dimensions");
            Check(((Image)Field(card,"cardBackgroundImage")).sprite==CharacterNoblePanelArt.Frame,"identical common frame and four mint gems");
            var art=(Image)Field(card,"abilityImage");
            Check(art.sprite!=null&&AssetDatabase.GetAssetPath(art.sprite).Contains("/"+character+"/"),"only playing character art "+character);
            Check(art.sprite.rect.height>600&&art.sprite.rect.width>=760,"approved illustration window excludes baked labels");
            Check(Mathf.Abs(art.rectTransform.rect.width/art.rectTransform.rect.height-art.sprite.rect.width/art.sprite.rect.height)<.01f,"original art aspect ratio");
            foreach(string n in new[]{"nameText","descriptionText"})
            {
                var label=(TextMeshProUGUI)Field(card,n);label.ForceMeshUpdate();
                Check(!label.isTextTruncated&&label.textBounds.size.y<=label.rectTransform.rect.height+2&&label.textBounds.size.x<=label.rectTransform.rect.width+2,"complete live text fits: "+label.text);
            }
            var heading=card.transform.Find("Phoenix heading").GetComponent<TextMeshProUGUI>();
            heading.ForceMeshUpdate();Check(heading.text=="고귀 증강"&&!heading.isTextTruncated,"common noble heading");
        }
        static IEnumerator Test()
        {
            Application.runInBackground=true;
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            string[] characters={"Ashi","Ari","Hyuki","Shini"};
            Check(CharacterNoblePanelArt.Frame!=null,"shared frame loaded");
            foreach(string character in characters)
            {
                var blueprint=config.characters.First(c=>OctoberArt.CharacterKey(c)==character);
                CrossSceneData.CharacterBlueprint=blueprint;CrossSceneData.ClearStartingLobbyItems();Time.timeScale=1;
                Check(StageEntryLoading.Begin(blueprint),"start actual run "+character);
                while(StageEntryLoading.IsLoading)yield return null;
                yield return null;yield return null;
                var level=Object.FindObjectOfType<LevelManager>();var manager=Object.FindObjectOfType<AbilityManager>();
                Check(level.PlayerCharacter.Blueprint==blueprint,"actual player blueprint "+character);
                // Deliberately disagree with the lobby; art must follow the live run.
                CrossSceneData.CharacterBlueprint=config.characters.First(c=>OctoberArt.CharacterKey(c)!=character);
                Check(CharacterNoblePanelArt.PlayingCharacterKey==character,"lobby cannot leak another character's art");
                var dialog=level.EntityManager.AbilitySelectionDialog;
                if(dialog.MenuOpen)dialog.Close();level.SetRunFlowPaused(true);dialog.OpenLegendary();
                yield return new WaitForSecondsRealtime(.7f);
                var cards=dialog.GetComponentsInChildren<AbilityCard>();
                Check(cards.Length==3&&cards.All(c=>c.UsesNoblePanel),"real noble reward produces three character cards");
                Check(dialog.ScrollRevealPending&&!dialog.CanSelectRevealedCard,"reveal gates reward selection");
                Check(cards.All(c=>!((Image)Field(c,"abilityImage")).enabled),"sealed cards hide overframe artwork");
                Capture(character+"-sealed");
                cards[0].Selected();
                Check(dialog.MenuOpen&&dialog.ScrollRevealPending,"first click reveals without consuming reward");
                while(dialog.ScrollRevealPending)yield return null;
                yield return null;
                Check(dialog.CanSelectRevealedCard&&dialog.MenuOpen,"reveal finishes then enables selection");
                foreach(var card in cards)CheckCard(card,character);
                Capture(character+"-reward");
                cards[0].Selected();yield return null;
                Check(!dialog.MenuOpen,"real reward selection closes once");
                var abilities=manager.GetComponentsInChildren<SyringeLegendaryAugmentAbility>(true).Where(a=>a.AvailableAsNoble).OrderBy(a=>a.Type.ToString()).ToArray();
                Check(abilities.Length==10,"ten noble designs "+character);
                dialog.OpenLegendary();yield return new WaitForSecondsRealtime(.5f);
                cards=dialog.GetComponentsInChildren<AbilityCard>();
                for(int start=0;start<abilities.Length;start+=3)
                {
                    Invoke(dialog,"ClearScrollReveal");
                    for(int i=0;i<cards.Length;i++)cards[i].Init(dialog,abilities[(start+i)%abilities.Length],0);
                    yield return new WaitForSecondsRealtime(.5f);Canvas.ForceUpdateCanvases();
                    foreach(var card in cards)CheckCard(card,character);
                    Capture(character+"-"+start);
                }
                var regular=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).First();
                cards[0].Init(dialog,regular,0);yield return new WaitForSecondsRealtime(.5f);
                Check(!cards[0].UsesNoblePanel&&!cards[0].transform.Find("Phoenix heading").gameObject.activeSelf,"pool resets to regular style");
                cards[0].Init(dialog,abilities[0],0);yield return new WaitForSecondsRealtime(.5f);CheckCard(cards[0],character);
                dialog.Close();level.SetRunFlowPaused(false);CrossSceneData.CharacterBlueprint=blueprint;
            }
            SessionState.SetBool(Key+"Done",true);
        }
    }
    public sealed class CharacterNoblePanelTestHost : MonoBehaviour {}
}
