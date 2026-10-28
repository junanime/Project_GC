using UnityEngine;

namespace Vampire
{
    public static class GamePlatform
    {
        // This separate QA build also handles iPad's desktop-style browser identity.
        public static bool UsesTouchControls => IsMobileWeb || Application.isMobilePlatform
#if UNITY_EDITOR
            || PreviewTouchControls
#endif
            ;
#if UNITY_EDITOR
        public static bool PreviewTouchControls;
#endif
        public static bool IsMobileWeb
        {
            get
            {
#if UNITY_WEBGL && PROJECT_GC_MOBILE_WEB && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
    }
}
