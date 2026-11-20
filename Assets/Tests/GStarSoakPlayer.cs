#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Vampire.Tests
{
    public sealed class GStarSoakPlayer : MonoBehaviour
    {
        static readonly string[] Args=Environment.GetCommandLineArgs();
        static string Arg(string key,string fallback)
        {
            int i=Array.IndexOf(Args,key);return i>=0 && i+1<Args.Length?Args[i+1]:fallback;
        }
        string folder;int stage,seconds;
        bool ended;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(!Args.Contains("-qaSoakStage"))return;
            if(Application.productName!=GStarLatePlaySoak.QAProduct)
            {
                Debug.LogError("Native QA uses a separate product and visitor data directory.");
                Application.Quit(2);return;
            }
            var host=new GameObject("Native late-play qualification");DontDestroyOnLoad(host);host.AddComponent<GStarSoakPlayer>();
        }
        IEnumerator Start()
        {
            folder=Path.GetFullPath(Arg("-qaSoakOutput",Path.Combine(Application.persistentDataPath,"qualification")));
            Directory.CreateDirectory(folder);
            stage=int.Parse(Arg("-qaSoakStage","0"));seconds=Mathf.Clamp(int.Parse(Arg("-qaSoakSeconds","120")),24,540);
            Application.logMessageReceived+=OnLog;
            File.WriteAllText(Path.Combine(folder,"native-stage"+stage+"-result.txt"),"RUNNING");
            Application.runInBackground=true;
            if(stage<0 || stage>2 || !GStarDemo.Enabled)throw new InvalidOperationException("Expected G-Star and a real stage ID.");
            yield return Wait(()=>ApothecaryUI.Instance!=null,"native main menu");
            var lobby=ApothecaryUI.Instance;lobby.Show("main");
            if(!GStarSession.ResetVisitor())throw new InvalidOperationException("Could not create QA visitor.");
            lobby.Show("prepare");
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;
            GamePreferences.Apply(prefs,false,false);QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            var ash=lobby.Config.characters.First(c=>OctoberArt.CharacterKey(c)=="Ashi");
            UnityEngine.Random.InitState(63841+stage);
            float begin=Time.realtimeSinceStartup;
            if(!StageEntryLoading.Begin(ash,null,stage))throw new InvalidOperationException("Native booth entry failed.");
            LevelManager level=null;
            yield return Wait(()=>!StageEntryLoading.IsLoading && GStarSession.AttemptStarted && (level=FindObjectOfType<LevelManager>())!=null &&
                level.InitialFieldObjectsReady && level.PlayerCharacter!=null && !level.IsRunFlowPaused && Time.timeScale>0,"native live field");
            File.WriteAllText(Path.Combine(folder,"native-stage"+stage+"-environment.txt"),Application.unityVersion+" / "+SystemInfo.graphicsDeviceName+
                "\nactual window="+Screen.width+"x"+Screen.height+"\nload_seconds="+(Time.realtimeSinceStartup-begin)+"\nQA data="+Application.persistentDataPath+"\n");
            yield return GStarLatePlaySoak.Play(level,stage,seconds,63841,folder);
            yield return null;
            Finish(0,"PASS");
        }
        static IEnumerator Wait(Func<bool> condition,string description)
        {
            float until=Time.realtimeSinceStartup+180;
            while(!condition()){if(Time.realtimeSinceStartup>until)throw new TimeoutException(description);yield return null;}
        }
        void OnLog(string message,string stack,LogType type)
        {
            if(type!=LogType.Exception && type!=LogType.Error && type!=LogType.Assert)return;
            File.AppendAllText(Path.Combine(folder,"native-stage"+stage+"-errors.txt"),message+"\n"+stack+"\n");Finish(1,"FAIL");
        }
        void Finish(int code,string result)
        {
            if(ended)return;ended=true;Application.logMessageReceived-=OnLog;
            File.WriteAllText(Path.Combine(folder,"native-stage"+stage+"-result.txt"),result);Application.Quit(code);
        }
    }
}
#endif
