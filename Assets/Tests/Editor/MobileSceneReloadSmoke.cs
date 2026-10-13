using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class MobileSceneReloadSmoke
    {
        const string Key="MobileSceneReloadSmoke.Active";
        static bool started;
        static MobileSceneReloadSmoke(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(!EditorApplication.isPlaying||level==null||level.PlayerCharacter==null||started)return;
            started=true;var runner=new GameObject("Mobile reload checks").AddComponent<MobileReloadChecks>();
            Object.DontDestroyOnLoad(runner);runner.StartCoroutine(Checks());
        }
        static IEnumerator Checks()
        {
            GamePreferences.Current.pauseOnFocusLoss=false;
            SceneManager.sceneLoaded+=ForceTouchLayout;
            for(int pass=0;pass<3;pass++)
            {
                yield return null;yield return null;
                var level=Object.FindObjectOfType<LevelManager>();level.SetRunFlowPaused(true);
                var controls=level.PlayerCharacter.GetComponent<MobileGameplayControls>();
                if(controls==null)controls=level.PlayerCharacter.gameObject.AddComponent<MobileGameplayControls>();
                typeof(MobileGameplayControls).GetField("simulateInEditor",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(controls,true);
                Time.timeScale=1;
                yield return null;yield return null;
                var inventories=Object.FindObjectsOfType<Inventory>(true);
                var slots=Object.FindObjectsOfType<InventorySlot>(true);
                Debug.Log("[MobileReload] pass="+pass+" inventories="+inventories.Length+" slots="+slots.Length);
                foreach(var inv in inventories)Debug.Log("[MobileReload] inventory id="+inv.GetInstanceID()+" scene="+inv.gameObject.scene.name+" parent="+inv.transform.parent?.name);
                if(inventories.Length!=1||slots.Length!=4)SessionState.SetBool(Key+"Failed",true);
                if(inventories.Any(inv=>inv.transform.parent==null||inv.transform.parent.name!="Safe Area"))SessionState.SetBool(Key+"Failed",true);
                typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"mobile-reload-"+pass+".png"});
                if(pass<2){Time.timeScale=1;yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game/Level 1.unity",LoadSceneMode.Single);}
            }
            Debug.Log("[MobileReload] FINISHED failed="+SessionState.GetBool(Key+"Failed",false));
            SceneManager.sceneLoaded-=ForceTouchLayout;
            SessionState.SetBool(Key+"Done",true);
        }
        static void ForceTouchLayout(Scene scene,LoadSceneMode mode)
            =>typeof(MobileGameplayInput).GetProperty("Active").SetValue(null,true);
    }
    public sealed class MobileReloadChecks : MonoBehaviour { }
}
