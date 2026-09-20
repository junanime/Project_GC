#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Vampire.Tests
{
    // Explicit development-player flag only; never runs in ordinary play or release builds.
    public sealed class ApothecaryPlayerSmoke : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Environment.GetCommandLineArgs().Contains("-apothecarySmoke"))new GameObject("Player verification").AddComponent<ApothecaryPlayerSmoke>();
        }
        IEnumerator Start()
        {
            bool passed=false,existed=PlayerPrefs.HasKey(GamePreferences.SaveKey);
            string saved=PlayerPrefs.GetString(GamePreferences.SaveKey);
            var original=GamePreferences.Current.Copy();
            try
            {
                yield return new WaitForSecondsRealtime(2);
                var ui=ApothecaryUI.Instance;Check(ui!=null&&ui.Page=="main","Player opens main UI");
                var values=original.Copy();values.displayMode=1;values.width=1280;values.height=720;values.quality=0;values.vSync=false;values.frameLimit=30;
                values.master=.7f;values.music=.2f;values.effects=.4f;values.ui=.6f;values.muted=false;
                GamePreferences.Apply(values,false);
                yield return new WaitForSecondsRealtime(2);
                Check(Screen.fullScreenMode==FullScreenMode.Windowed&&Screen.width==1280&&Screen.height==720,"Native window mode and resolution applied");
                Check(QualitySettings.GetQualityLevel()==0&&QualitySettings.vSyncCount==0&&Application.targetFrameRate==30,"Quality, VSync and frame cap applied");
                Check(Mathf.Abs(GameAudioManager.Instance.MusicVolume-.2f)<.001f&&Mathf.Abs(GameAudioManager.Instance.EffectsVolume-.4f)<.001f&&Mathf.Abs(GameAudioManager.Instance.UIVolume-.6f)<.001f,"Player audio categories independent");
                ui.Show("settings");
                var draft=(GamePreferenceValues)typeof(ApothecaryUI).GetField("settingsDraft",Private).GetValue(ui);
                draft.width=960;draft.height=540;
                typeof(ApothecaryUI).GetMethod("ApplySettings",Private).Invoke(ui,null);
                yield return new WaitForSecondsRealtime(2);
                Check(Screen.width==960&&Screen.height==540,"New display size previewed");
                typeof(ApothecaryUI).GetField("displayDeadline",Private).SetValue(ui,Time.unscaledTime-1);
                yield return new WaitForSecondsRealtime(2);
                Check(Screen.width==1280&&Screen.height==720,"Timed-out display preview restores native window size");
                draft=(GamePreferenceValues)typeof(ApothecaryUI).GetField("settingsDraft",Private).GetValue(ui);
                draft.displayMode=0;
                typeof(ApothecaryUI).GetMethod("ApplySettings",Private).Invoke(ui,null);
                yield return new WaitForSecondsRealtime(2);
                Check(Screen.fullScreenMode==FullScreenMode.FullScreenWindow,"Borderless fullscreen applied");
                typeof(ApothecaryUI).GetMethod("ConfirmDisplay",Private).Invoke(ui,null);
                Check(GamePreferences.Read().displayMode==0,"Confirmed display mode saved");
                passed=true;
            }
            finally
            {
                if(existed)PlayerPrefs.SetString(GamePreferences.SaveKey,saved);else PlayerPrefs.DeleteKey(GamePreferences.SaveKey);
                PlayerPrefs.Save();GamePreferences.Apply(original,false);
                Debug.Log("[ApothecaryPlayerSmoke] FINISHED passed="+passed);
                Application.Quit(passed?0:1);
            }
        }
        static void Check(bool condition,string message)
        {
            if(!condition)throw new Exception("[ApothecaryPlayerSmoke] FAIL "+message);
            Debug.Log("[ApothecaryPlayerSmoke] PASS "+message);
        }
    }
}
#endif
