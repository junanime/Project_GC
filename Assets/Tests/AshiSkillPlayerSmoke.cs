#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Vampire.Tests
{
    public sealed class AshiSkillPlayerSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Environment.GetCommandLineArgs().Contains("-ashiSkillsSmoke"))
            {
                var go=new GameObject("Ashi player verification");DontDestroyOnLoad(go);go.AddComponent<AshiSkillPlayerSmoke>();
            }
        }
        bool errors;
        void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors=true;}
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;
            bool passed=false;var original=GamePreferences.Current.Copy();
            try
            {
                var preferences=original.Copy();preferences.pauseOnFocusLoss=false;preferences.muteOnFocusLoss=false;GamePreferences.Apply(preferences,false);
                yield return new WaitForSecondsRealtime(2);
                var ui=ApothecaryUI.Instance;Check(ui!=null,"native main UI");
                Check(ui.GetComponentsInChildren<Button>().Any(b=>b.name=="Active skill slot"),"main skill slots");
                CrossSceneData.CharacterBlueprint=ui.Config.characters[0];CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
                SceneManager.LoadScene(1);yield return new WaitForSecondsRealtime(2);
                var level=FindObjectOfType<LevelManager>();level.enabled=false;var player=level.PlayerCharacter;var skill=player.Skills;
                foreach(var monster in FindObjectsOfType<Monster>())monster.gameObject.SetActive(false);
                var needle=FindObjectOfType<SyringeDartAbility>();needle.enabled=false;int count=needle.GetEffectiveProjectileCount();float speed=needle.GetEffectiveSpeed();
                Check(player.TryDash(),"native dash starts");yield return new WaitForSeconds(.4f);
                Check(skill.PassiveActive&&needle.GetEffectiveProjectileCount()==count+2,"native Marathon bonus");
                var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R));
                yield return new WaitForSecondsRealtime(.4f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                Check(skill.IsCutin&&Time.timeScale==0,"native R cut-in pauses world");
                var portrait=ApothecaryUI.Instance.GetComponentsInChildren<Image>().First(i=>i.name=="Ashi cut-in portrait");
                Check(portrait.sprite!=null&&portrait.color.a>0,"native cut-in art visible");
                yield return new WaitForSecondsRealtime(2);
                Check(skill.Active&&skill.CooldownRemaining>42,"native active duration and cooldown start");
                Check(needle.GetEffectiveProjectileCount()==(count+2)*2&&Mathf.Abs(needle.GetEffectiveSpeed()-speed*2)<.01f,"native active count and speed");
                ApothecaryUI.Instance.OpenRunBook();float remaining=skill.CooldownRemaining;yield return new WaitForSecondsRealtime(.4f);
                Check(Mathf.Abs(skill.CooldownRemaining-remaining)<.001f,"native TAB freezes timers");ApothecaryUI.Instance.CloseRunBook();
                skill.Tick(100);yield return null;
                Check(!skill.Active&&needle.GetEffectiveProjectileCount()==count,"native modifiers expire");
                var button=ApothecaryUI.Instance.GetComponentsInChildren<Button>().Single(b=>b.name=="Active skill R");
                ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
                Check(skill.IsCutin,"native pointer activation");
                yield return new WaitForSecondsRealtime(2.2f);Check(!errors,"no native runtime errors");passed=true;
            }
            finally
            {
                Time.timeScale=1;GamePreferences.Apply(original,false);Application.logMessageReceived-=Log;
                Debug.Log("[AshiSkillPlayerSmoke] FINISHED passed="+passed);Application.Quit(passed?0:1);
            }
        }
        static void Check(bool ok,string text){if(!ok)throw new Exception("[AshiSkillPlayerSmoke] FAIL "+text);Debug.Log("[AshiSkillPlayerSmoke] PASS "+text);}
    }
}
#endif
