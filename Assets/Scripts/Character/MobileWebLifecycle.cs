using UnityEngine;
using UnityEngine.Scripting;

namespace Vampire
{
    // The browser orientation/visibility overlay blocks gameplay, not just its picture.
    [Preserve]
    public sealed class MobileWebLifecycle : MonoBehaviour
    {
        bool ownsPause;
        bool browserBlocked;
        float savedScale;

#if UNITY_WEBGL && PROJECT_GC_MOBILE_WEB && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var go=new GameObject("Mobile web lifecycle");
            DontDestroyOnLoad(go);go.AddComponent<MobileWebLifecycle>();
        }
#endif
        [Preserve]
        public void SetBrowserBlocked(string value)
        {
            bool blocked=value=="1";
            browserBlocked=blocked;
            if(blocked)
            {
                foreach(var control in FindObjectsOfType<MobileTouchControl>(true))control.Cancel();
                MobileGameplayInput.ClearGestures();
                var character=FindObjectOfType<Character>();
                if(character!=null)character.Move(Vector2.zero);
                if(!ownsPause&&Time.timeScale>0){savedScale=Time.timeScale;ownsPause=true;Time.timeScale=0;}
            }
            else if(ownsPause)
            {
                if(Time.timeScale==0)Time.timeScale=savedScale;
                ownsPause=false;
            }
        }
        void Update()
        {
            // Focus callbacks and scene initializers can restore time while the rotation overlay is up.
            if(browserBlocked&&Time.timeScale>0)
            {if(!ownsPause)savedScale=Time.timeScale;ownsPause=true;Time.timeScale=0;}
        }
    }
}
