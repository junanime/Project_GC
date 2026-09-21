using System;
using UnityEngine;

namespace Vampire
{
    [Serializable]
    public sealed class GamePreferenceValues
    {
        public float master = 1, music = .55f, effects = .8f, ui = .8f;
        public bool muted, reducedMotion, showFps, pauseOnFocusLoss = true, muteOnFocusLoss = true;
        public int quality = 2, frameLimit = 60;
        public bool vSync = true;
        public int displayMode, width, height;
        public GamePreferenceValues Copy() => JsonUtility.FromJson<GamePreferenceValues>(JsonUtility.ToJson(this));
    }

    // One saved transaction. Audio can be previewed; Back restores the saved snapshot.
    public static class GamePreferences
    {
        public const string SaveKey = "Apothecary.Settings.v2";
        static GamePreferenceValues current;
        public static event Action Changed;
        public static GamePreferenceValues Current => current ?? (current = Read());
        public static GamePreferenceValues Defaults() => new GamePreferenceValues {vSync=!Application.isMobilePlatform};
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { current=null; Changed=null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Apply(Read(),false);
            var go=new GameObject("User settings lifecycle");
            UnityEngine.Object.DontDestroyOnLoad(go);go.AddComponent<GamePreferenceLifecycle>();
        }
        public static GamePreferenceValues Read()
        {
            GamePreferenceValues value=null;
            if(PlayerPrefs.HasKey(SaveKey))
            {
                try { value=JsonUtility.FromJson<GamePreferenceValues>(PlayerPrefs.GetString(SaveKey)); }
                catch(ArgumentException) { /* A damaged settings entry must not block startup. */ }
            }
            if(value==null)
            {
                value=Defaults();value.master=PlayerPrefs.GetFloat("Apothecary.Volume",1);
                value.reducedMotion=PlayerPrefs.GetInt("Apothecary.ReducedMotion",0)!=0;
            }
            Sanitize(value);return value;
        }
        static float Volume(float value) => float.IsNaN(value)||float.IsInfinity(value)?1:Mathf.Clamp01(value);
        public static void Sanitize(GamePreferenceValues value)
        {
            value.master=Volume(value.master);value.music=Volume(value.music);value.effects=Volume(value.effects);value.ui=Volume(value.ui);
            value.quality=Mathf.Clamp(value.quality,0,2);value.displayMode=Mathf.Clamp(value.displayMode,0,1);
            if(value.frameLimit!=30&&value.frameLimit!=60&&value.frameLimit!=120&&value.frameLimit!=-1)value.frameLimit=60;
            if(value.width<640||value.height<360||value.width>7680||value.height>4320)value.width=value.height=0;
        }
        public static void PreviewAudio(GamePreferenceValues value)
        {
            current=Current.Copy();current.master=Volume(value.master);current.music=Volume(value.music);
            current.effects=Volume(value.effects);current.ui=Volume(value.ui);current.muted=value.muted;
            AudioListener.volume=current.muted?0:current.master;Changed?.Invoke();
        }
        public static void Apply(GamePreferenceValues value,bool save,bool changeDisplay=true)
        {
            current=value.Copy();Sanitize(current);
            int last=Mathf.Max(0,QualitySettings.names.Length-1);
            int quality=current.quality==0?0:current.quality==1?last/2:last;
            if(QualitySettings.GetQualityLevel()!=quality)QualitySettings.SetQualityLevel(quality,true);
            QualitySettings.vSyncCount=!Application.isMobilePlatform&&current.vSync?1:0;
            Application.targetFrameRate=current.frameLimit;
            AudioListener.volume=current.muted?0:current.master;
            if(changeDisplay&&!Application.isMobilePlatform)
            {
#if !UNITY_EDITOR
                var mode=current.displayMode==0?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
                int w=current.width>0?current.width:Display.main.systemWidth;
                int h=current.height>0?current.height:Display.main.systemHeight;
                if(Screen.width!=w||Screen.height!=h||Screen.fullScreenMode!=mode)Screen.SetResolution(w,h,mode);
#endif
            }
            if(save){PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(current));PlayerPrefs.Save();}
            Changed?.Invoke();
        }
    }

    public sealed class GamePreferenceLifecycle : MonoBehaviour
    {
        float pausedScale;
        bool ownsPause;
        void OnApplicationFocus(bool focus)
        {
            var preferences=GamePreferences.Current;
            if(!focus)
            {
                if(preferences.muteOnFocusLoss)AudioListener.volume=0;
                var level=FindObjectOfType<LevelManager>();
                if(preferences.pauseOnFocusLoss&&level!=null&&!level.IsLevelEnded&&Time.timeScale>0)
                {pausedScale=Time.timeScale;ownsPause=true;Time.timeScale=0;}
            }
            else
            {
                AudioListener.volume=preferences.muted?0:preferences.master;
                var level=FindObjectOfType<LevelManager>();
                if(ownsPause&&level!=null&&!level.IsLevelEnded&&Time.timeScale==0)Time.timeScale=pausedScale;
                ownsPause=false;
            }
        }
    }
}
