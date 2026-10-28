using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vampire
{
    // Preserve serialized KeyCode fields while reading one Input System backend.
    // A phone without a keyboard simply has no key events; touch actions are separate.
    public static class GameInput
    {
        public static KeyCode ActiveSkillKey => GamePreferences.Current.keyboard.Get(GameKeyAction.Active);
        public static string ActiveSkillKeyLabel => Label(GameKeyAction.Active);
        public static bool EditingKeyboard { get; set; }
        public static bool TextEntryFocused {
            get {
                var selected=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
                return selected!=null && (selected.GetComponent<TMPro.TMP_InputField>()?.isFocused==true || selected.GetComponent<UnityEngine.UI.InputField>()?.isFocused==true);
            }
        }
        public static bool KeyboardBlocked => EditingKeyboard || TextEntryFocused;
        public static bool Pressed(GameKeyAction action) => !KeyboardBlocked &&
            (GetKeyDown(GamePreferences.Current.keyboard.Get(action)) || GetKeyDown(GamePreferences.Current.keyboard.Get(action,1)));
        public static bool Held(GameKeyAction action) => !KeyboardBlocked &&
            (GetKey(GamePreferences.Current.keyboard.Get(action)) || GetKey(GamePreferences.Current.keyboard.Get(action,1)));
        public static string Label(GameKeyAction action) => GamePreferences.Current.keyboard.Label(action);
        public static bool DebugPressed(KeyCode key) => !KeyboardBlocked && !GamePreferences.Current.keyboard.Find(key,out _,out _) && GetKeyDown(key);
        private static readonly Dictionary<KeyCode,Key> Keys = new Dictionary<KeyCode,Key>();
        private static int interactionFrame=-1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInteractionFrame(){interactionFrame=-1;EditingKeyboard=false;}

        // Opening a reward may enable an adjacent return portal during this same frame.
        // Reserve the press before callbacks run, across both field and mini-stage interactions.
        public static bool TryConsumeInteraction(KeyCode key)
        {
            if(interactionFrame==Time.frameCount)return false;
            if(!Pressed(GameKeyAction.Interact)&&!MobileGameplayInput.ConsumeInteraction())return false;
            interactionFrame=Time.frameCount;return true;
        }
        public static bool TryConsumeInteraction(KeyCode key, Component owner)
            => InteractionFocus.IsFocused(owner) && TryConsumeInteraction(key);
        public static bool GetKeyDown(KeyCode code)
        {
            var keyboard=Keyboard.current;
            if(keyboard==null || code==KeyCode.None) return false;
            var key=ResolveKeyboardKey(code);
            return key!=Key.None && keyboard[key].wasPressedThisFrame;
        }

        public static bool GetKey(KeyCode code)
        {
            var keyboard=Keyboard.current;
            if(keyboard==null || code==KeyCode.None)return false;
            var key=ResolveKeyboardKey(code);
            return key!=Key.None && keyboard[key].isPressed;
        }
        public static Key ResolveKeyboardKey(KeyCode code)
        {
            if(!Keys.TryGetValue(code,out var key))
            {
                string name=code.ToString();
                if(name.StartsWith("Alpha",StringComparison.Ordinal)) name="Digit"+name.Substring(5);
                else if(name.StartsWith("Keypad",StringComparison.Ordinal)) name="Numpad"+name.Substring(6);
                switch(name)
                {
                    case "Return":name="Enter";break;
                    case "UpArrow":name="UpArrow";break;
                    case "KeypadEnter":name="NumpadEnter";break;
                    case "LeftControl":name="LeftCtrl";break;
                    case "RightControl":name="RightCtrl";break;
                    case "LeftCommand":case "LeftApple":case "LeftWindows":name="LeftMeta";break;
                    case "RightCommand":case "RightApple":case "RightWindows":name="RightMeta";break;
                }
                if(!Enum.TryParse(name,true,out key)) key=Key.None;
                Keys[code]=key;
            }
            return key;
        }
    }
}
