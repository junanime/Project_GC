using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Vampire
{
    public enum GameKeyAction { MoveUp, MoveDown, MoveLeft, MoveRight, Dash, Active, Interact, Overcharge, Journal, Settings, Slot1, Slot2, Slot3, Slot4 }

    [Serializable]
    public sealed class KeyboardBindingValues
    {
        public KeyCode[] primary;
        public KeyCode[] secondary;
        public const int Count = 14;
        public static KeyboardBindingValues Defaults() => new KeyboardBindingValues {
            primary = new[] { KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.LeftShift, KeyCode.Space, KeyCode.E, KeyCode.Q, KeyCode.Tab, KeyCode.Escape, KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V },
            secondary = new[] { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.RightShift, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 }
        };
        public KeyCode Get(GameKeyAction action, int slot = 0) => (slot == 0 ? primary : secondary)[(int)action];
        public bool Find(KeyCode key, out GameKeyAction action, out int slot)
        {
            for (int i=0; i<Count; i++) for (int j=0; j<2; j++)
                if (key != KeyCode.None && Get((GameKeyAction)i,j)==key) { action=(GameKeyAction)i; slot=j; return true; }
            action=default; slot=0; return false;
        }
        public void Assign(GameKeyAction action, int slot, KeyCode key)
        {
            if (action==GameKeyAction.Settings || key==KeyCode.Escape || (key!=KeyCode.None && !Assignable(key))) return;
            if (Find(key,out var old,out int oldSlot)) (oldSlot==0?primary:secondary)[(int)old]=KeyCode.None;
            (slot==0?primary:secondary)[(int)action]=key;
        }
        public string ValidationError()
        {
            for (int i=0; i<(int)GameKeyAction.Settings; i++)
                if (primary[i]==KeyCode.None && secondary[i]==KeyCode.None) return Name((GameKeyAction)i)+"에 키를 하나 이상 지정해 주세요.";
            return null;
        }
        public void Sanitize()
        {
            var defaults=Defaults();
            if(primary==null || primary.Length!=Count || secondary==null || secondary.Length!=Count) { primary=defaults.primary; secondary=defaults.secondary; return; }
            var used=new HashSet<KeyCode>{KeyCode.Escape};
            for(int i=0;i<Count;i++) for(int s=0;s<2;s++) {
                var list=s==0?primary:secondary; var k=list[i];
                if(i==(int)GameKeyAction.Settings) { list[i]=s==0?KeyCode.Escape:KeyCode.None; continue; }
                if(k!=KeyCode.None && (!Assignable(k)||!used.Add(k))) list[i]=KeyCode.None;
            }
            // A corrupt/incomplete save must not leave movement or combat inaccessible.
            if(ValidationError()!=null) { primary=defaults.primary; secondary=defaults.secondary; }
        }
        public static bool Assignable(KeyCode key) => key!=KeyCode.None && key!=KeyCode.Escape &&
            key!=KeyCode.LeftWindows && key!=KeyCode.RightWindows && key!=KeyCode.LeftCommand && key!=KeyCode.RightCommand &&
            GameInput.ResolveKeyboardKey(key)!=Key.None;
        public static string Name(GameKeyAction action) => new[] { "위로 이동", "아래로 이동", "왼쪽 이동", "오른쪽 이동", "대쉬", "액티브 스킬", "상호작용", "혈전 과충전", "탐험 기록", "설정 / 뒤로", "소모품 1", "소모품 2", "소모품 3", "소모품 4" }[(int)action];
        public static string KeyLabel(KeyCode key)
        {
            string name=key.ToString();
            if(name.StartsWith("Alpha")) return name.Substring(5);
            if(name.StartsWith("Keypad")) return "Num "+name.Substring(6);
            switch(key) {
                case KeyCode.None:return "미지정"; case KeyCode.Escape:return "Esc";
                case KeyCode.LeftShift:return "L Shift"; case KeyCode.RightShift:return "R Shift";
                case KeyCode.LeftControl:return "L Ctrl"; case KeyCode.RightControl:return "R Ctrl";
                case KeyCode.LeftAlt:return "L Alt"; case KeyCode.RightAlt:return "R Alt";
                case KeyCode.UpArrow:return "↑"; case KeyCode.DownArrow:return "↓"; case KeyCode.LeftArrow:return "←"; case KeyCode.RightArrow:return "→";
                case KeyCode.Return:return "Enter"; case KeyCode.BackQuote:return "`";
                case KeyCode.Minus:return "-"; case KeyCode.Equals:return "="; case KeyCode.LeftBracket:return "["; case KeyCode.RightBracket:return "]";
                case KeyCode.Backslash:return "\\"; case KeyCode.Semicolon:return ";"; case KeyCode.Quote:return "'";
                case KeyCode.Comma:return ","; case KeyCode.Period:return "."; case KeyCode.Slash:return "/";
                default:return name;
            }
        }
        public string Label(GameKeyAction action)
        {
            var a=Get(action);var b=Get(action,1);
            return a==KeyCode.None?KeyLabel(b):b==KeyCode.None?KeyLabel(a):KeyLabel(a)+" / "+KeyLabel(b);
        }
    }

    // Only the player's keyboard bindings are overridden. Pointer, touch and controller bindings stay intact.
    public sealed class KeyboardBindingDriver : MonoBehaviour
    {
        PlayerInput player;
        string applied;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() { SceneManager.sceneLoaded-=SceneLoaded; SceneManager.sceneLoaded+=SceneLoaded; }
        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach(var p in FindObjectsOfType<PlayerInput>(true))
                if(p.GetComponent<KeyboardBindingDriver>()==null)p.gameObject.AddComponent<KeyboardBindingDriver>();
        }
        void OnEnable() { player=GetComponent<PlayerInput>(); GamePreferences.Changed+=Refresh; Refresh(); }
        void Start()=>Refresh();
        void OnDisable()=>GamePreferences.Changed-=Refresh;
        void Refresh()
        {
            if(player==null||player.actions==null)return;
            string signature=JsonUtility.ToJson(GamePreferences.Current.keyboard);
            if(signature==applied)return;
            Apply(player.actions,GamePreferences.Current.keyboard);applied=signature;
        }
        public static void Apply(InputActionAsset asset, KeyboardBindingValues bindings)
        {
            var map=asset.FindActionMap("Player",false);if(map==null)return;
            var enabled=new List<InputAction>();foreach(var a in map.actions)if(a.enabled)enabled.Add(a);
            map.Disable();
            try {
                var move=map.FindAction("Move",false);
                if(move!=null) {
                    string[] directions={"up","down","left","right"};
                    for(int d=0;d<4;d++) { int slot=0; for(int i=0;i<move.bindings.Count;i++) {
                        var b=move.bindings[i];
                        if(b.isPartOfComposite && b.name==directions[d] && b.path.StartsWith("<Keyboard>/"))
                            move.ApplyBindingOverride(i,Path(bindings.Get((GameKeyAction)d,Mathf.Min(slot++,1))));
                    }}
                }
                string[] names={"North Powerup","East Powerup","South Powerup","West Powerup"};
                for(int n=0;n<4;n++) {
                    var a=map.FindAction(names[n],false);if(a==null)continue;int slot=0;
                    for(int i=0;i<a.bindings.Count;i++)if(a.bindings[i].path.StartsWith("<Keyboard>/"))
                        a.ApplyBindingOverride(i,Path(bindings.Get(GameKeyAction.Slot1+n,Mathf.Min(slot++,1))));
                }
            } finally { foreach(var a in enabled)a.Enable(); }
        }
        public static string Path(KeyCode code)
        {
            if(code==KeyCode.None)return "";
            var key=GameInput.ResolveKeyboardKey(code);
            // The Input System's control names (e.g. '1', 'upArrow') are authoritative.
            if(Keyboard.current!=null && key!=Key.None)return "<Keyboard>/"+Keyboard.current[key].name;
            string name=key.ToString();
            if(name.StartsWith("Digit"))name=name.Substring(5);
            return "<Keyboard>/"+char.ToLowerInvariant(name[0])+name.Substring(1);
        }
    }
}
