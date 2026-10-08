using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Vampire
{
 public sealed class ShiniSkillButton:MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler,ISubmitHandler
 {
  public Func<CharacterSkillRuntime> Resolve;int pointer=int.MinValue;ShiniEmberRuntime held;
  public void OnPointerDown(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left||pointer!=int.MinValue)return;var s=Resolve?.Invoke();if(s!=null&&s.IsShini&&s.Breath!=null&&s.Breath.Press()){pointer=e.pointerId;held=s.Breath;}}
  public void OnPointerUp(PointerEventData e){if(e.pointerId==pointer)Release();}
  public void OnPointerExit(PointerEventData e){if(e.pointerId==pointer)Release();}
  public void OnSubmit(BaseEventData e){var s=Resolve?.Invoke();if(s!=null&&s.IsShini)s.TryActivate();}
  void Release(){if(held!=null)held.ReleasePointer();held=null;pointer=int.MinValue;}
  void OnDisable(){Release();}void OnApplicationFocus(bool f){if(!f)Release();}
 }
}
