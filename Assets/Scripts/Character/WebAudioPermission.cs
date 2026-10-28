using System.Runtime.InteropServices;
using UnityEngine;

namespace Vampire
{
    public static class WebAudioPermission
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void WebAudio_InstallGestureResume();
        [DllImport("__Internal")] static extern int WebAudio_IsRunning();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => WebAudio_InstallGestureResume();
        public static bool Ready => WebAudio_IsRunning() != 0;
#else
        public static bool Ready => true;
#endif
    }
}
