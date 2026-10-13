using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
namespace Vampire
{
 public sealed class ShiniSkillButton:MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler,ISubmitHandler
 {
  public Func<CharacterSkillRuntime> Resolve;int pointer=int.MinValue;ShiniEmberRuntime held;
  public void OnPointerDown(PointerEventData e){if(held!=null&&!held.IsPointerHeld)Release();if(e.button!=PointerEventData.InputButton.Left||pointer!=int.MinValue)return;var s=Resolve?.Invoke();if(s!=null&&s.IsShini&&s.Breath!=null&&s.Breath.Press(false,PressControl(e))){pointer=e.pointerId;held=s.Breath;}}
  static ButtonControl PressControl(PointerEventData e)
  {
   if(!(e is ExtendedPointerEventData data))return null;
   if(data.device is Mouse mouse)return mouse.leftButton;
   if(data.device is Pen pen)return pen.tip;
   if(data.device is Touchscreen screen){foreach(var touch in screen.touches)if(touch.touchId.ReadValue()==data.touchId)return touch.press;}
   return data.control as ButtonControl;
  }
  public void OnPointerUp(PointerEventData e){if(e.pointerId==pointer)Release();}
  public void OnPointerExit(PointerEventData e){if(e.pointerId==pointer&&(held==null||!held.IsRewardPaused))Release();}
  public void OnSubmit(BaseEventData e){var s=Resolve?.Invoke();if(s!=null&&s.IsShini)s.TryActivate();}
  void Release(){if(held!=null)held.ReleasePointer();held=null;pointer=int.MinValue;}
  // The reward hides the HUD. Runtime polling still sees physical releases while this is disabled.
  void OnEnable(){if(held!=null&&!held.IsPointerHeld)Release();}
  void OnDisable(){if(held==null||!held.IsRewardPaused)Release();}
  void OnDestroy(){Release();}
  void OnApplicationFocus(bool f){if(!f)Release();}
 }
}
