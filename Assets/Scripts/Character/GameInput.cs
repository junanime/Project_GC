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
        private static readonly Dictionary<KeyCode,Key> Keys = new Dictionary<KeyCode,Key>();
        public static bool GetKeyDown(KeyCode code)
        {
            var keyboard=Keyboard.current;
            if(keyboard==null || code==KeyCode.None) return false;
            var key=ResolveKeyboardKey(code);
            return key!=Key.None && keyboard[key].wasPressedThisFrame;
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
