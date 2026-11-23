using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace Vampire.Editor
{
    public static class WkuResultKeyboardChecks
    {
        static IEnumerator Press(Keyboard keyboard,params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
        }
        static void Type(TMP_InputField field,string value)
        {
            foreach(char c in value)field.ProcessEvent(new Event{type=EventType.KeyDown,character=c});field.ForceLabelUpdate();
        }
        public static IEnumerator Register(Action<bool,string> check,bool fromButton)
        {
            var ui=ApothecaryUI.Instance;var form=ui.GetComponentInChildren<WkuResultForm>();
            yield return null;yield return null;
            check(form.School.isFocused,"school input receives initial focus");
            check(form.School.caretWidth==2&&form.School.caretBlinkRate>0,"native insertion caret blinks");
            check(!form.Submit.interactable,"empty form cannot register");
            var keyboard=InputSystem.AddDevice<Keyboard>();
            var settings=InputSystem.settings;var oldBackground=settings.backgroundBehavior;var oldEditor=settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var module=EventSystem.current.currentInputModule;var original=module.inputOverride;
            var host=new GameObject("WKU synthetic IME input");var ime=host.AddComponent<Vampire.Tests.Editor.FestivalCompositionInput>();module.inputOverride=ime;
            try
            {
                yield return Press(keyboard,Key.Tab);check(form.Participant.isFocused,"Tab moves school to participant");
                yield return Press(keyboard,Key.LeftShift,Key.Tab);check(form.School.isFocused,"Shift Tab returns to school");
                Type(form.School,"검증");ime.composition="고";form.School.ForceLabelUpdate();form.ObserveComposition();
                check(form.School.textComponent.text.Contains("검증고")&&!form.School.textComponent.text.Contains("<u>"),"live school composition has no underline tags");
                yield return Press(keyboard,Key.Enter);
                check(form.School.isFocused&&ui.Page=="result","IME confirmation Enter does not move or submit");
                ime.composition="";Type(form.School,"고등학교");yield return null;yield return null;
                yield return Press(keyboard,Key.Enter);check(form.Participant.isFocused,"confirmed school Enter moves to name");
                check(!form.Submit.interactable,"school alone cannot register");
                Type(form.Participant,"가상참가자");yield return null;
                check(form.Submit.interactable,"two valid fields enable one registration button");
                if(fromButton)
                {
                    yield return Press(keyboard,Key.Tab);check(EventSystem.current.currentSelectedGameObject==form.Submit.gameObject,"Tab moves name to registration");
                    yield return Press(keyboard,Key.LeftShift,Key.Tab);check(form.Participant.isFocused,"Shift Tab returns registration to name");
                    yield return Press(keyboard,Key.Tab);
                }
                yield return Press(keyboard,Key.Enter);yield return null;
                check(ui.Page=="festivalRanking","Enter registers once and opens ranking");
                check(FestivalLeaderboard.Read().Count==1,"keyboard submission has no duplicate record");
                check(FestivalLeaderboard.Read().Single().schoolName=="검증고등학교","keyboard registration preserves full Hangul school");
            }
            finally
            {
                ime.composition="";module.inputOverride=original;Object.Destroy(host);InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior=oldBackground;settings.editorInputBehaviorInPlayMode=oldEditor;
            }
        }
    }
}
