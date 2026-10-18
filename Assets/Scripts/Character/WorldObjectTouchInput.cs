using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Vampire
{
    // Bomb aiming takes priority. Resolve object taps before weapon aiming and interaction consumers.
    [DefaultExecutionOrder(-90)]
    public sealed class WorldObjectTouchInput : MonoBehaviour
    {
        static int consumedFrame=-10;
        public static bool ConsumedPointer => consumedFrame==Time.frameCount;
        readonly List<RaycastResult> hits=new List<RaycastResult>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFrame(){consumedFrame=-10;}
        void Update()
        {
            if(!(GamePlatform.UsesTouchControls||MobileGameplayInput.Active)||Time.timeScale<=0||BombTargeting.BlocksPointerAim||StageEntryLoading.IsLoading)return;
            if(Touchscreen.current==null)return;
            foreach(var touch in Touchscreen.current.touches)
                if(touch.press.wasPressedThisFrame&&TryInteract(touch.position.ReadValue()))return;
        }
        public bool TryInteract(Vector2 screenPosition)
        {
            if(Time.timeScale<=0||BombTargeting.BlocksPointerAim||StageEntryLoading.IsLoading||Camera.main==null)return false;
            if(ApothecaryUI.Instance!=null&&ApothecaryUI.Instance.Page!="hud")return false;
            if(EventSystem.current!=null)
            {
                hits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screenPosition},hits);
                foreach(var hit in hits)if(hit.module is UnityEngine.UI.GraphicRaycaster)return false;
            }
            if(InteractionFocus.SelectAtScreenPoint(screenPosition,Camera.main))
            {consumedFrame=Time.frameCount;MobileGameplayInput.RequestInteraction();return true;}
            return false;
        }
    }
}
