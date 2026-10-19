using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class ItemFilterVisualSmoke
    {
        const string Key="ItemFilterVisualSmoke";
        static double deadline,next;
        static int step;
        static ItemFilterVisualSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            foreach(string k in new[]{"Coins","LobbySilverCoins"})
            {SessionState.SetBool(Key+k,PlayerPrefs.HasKey(k));SessionState.SetInt(Key+k,PlayerPrefs.GetInt(k));}
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            Directory.CreateDirectory("Library/ItemFilterProof");
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(type).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){deadline=EditorApplication.timeSinceStartup+90;step=0;next=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                foreach(string k in new[]{"Coins","LobbySilverCoins"})
                {if(SessionState.GetBool(Key+k,false))PlayerPrefs.SetInt(k,SessionState.GetInt(Key+k,0));else PlayerPrefs.DeleteKey(k);}
                PlayerPrefs.Save();SessionState.SetBool(Key,false);
                EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);return;
            }
            if(EditorApplication.timeSinceStartup>deadline){Fail(new Exception("UI test timeout"));return;}
            if(!EditorApplication.isPlaying||ApothecaryUI.Instance==null||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+1;
            try
            {
                var ui=ApothecaryUI.Instance;
                if(step==0){ui.Show("prepare",3);step++;return;}
                if(step<=5)
                {
                    string role=ItemOfferPolicy.RoleName((ItemOfferPolicy.Role)(step-1));
                    if(step>1)
                    {
                        var button=ui.GetComponentsInChildren<Button>().Single(b=>b.GetComponentsInChildren<TMP_Text>().Any(t=>t.text==role));
                        button.onClick.Invoke();
                    }
                    step++;return;
                }
                if(step==6)
                {
                    var text=ui.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Selection description");
                    text.ForceMeshUpdate();if(text.isTextOverflowing)throw new Exception("Description overflow");
                    var shot=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("Library/ItemFilterProof/utility.png",shot.EncodeToPNG());UnityEngine.Object.Destroy(shot);
                    ui.Show("prepare",3);
                    var button=ui.GetComponentsInChildren<Button>().Single(b=>b.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="전체"));button.onClick.Invoke();step++;return;
                }
                var capture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("Library/ItemFilterProof/all.png",capture.EncodeToPNG());UnityEngine.Object.Destroy(capture);
                Debug.Log("ITEM_FILTER_VISUAL_PASS: all five filters clicked; description fits; screenshots captured");
                SessionState.SetBool(Key+"Done",true);
            }
            catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){Debug.LogException(e);SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
    }
}
