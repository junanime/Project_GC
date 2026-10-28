using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        KeyCode selectedKeyboardKey=KeyCode.Space;
        int keyboardGroup, keyboardSlot;
        GameKeyAction? pendingKeyAction;
        bool keyboardNavigationOwned, previousKeyboardNavigation;
        EventSystem keyboardEventSystem;
        static readonly Color KeyInk=new Color(.10f,.13f,.22f);

        void SyncKeyboardEditing()
        {
            bool editing=Page=="settings"&&Tab==2;
            GameInput.EditingKeyboard=editing;
            if(editing&&!keyboardNavigationOwned&&EventSystem.current!=null) {
                keyboardEventSystem=EventSystem.current;previousKeyboardNavigation=keyboardEventSystem.sendNavigationEvents;
                keyboardEventSystem.sendNavigationEvents=false;keyboardNavigationOwned=true;
                keyboardEventSystem.SetSelectedGameObject(null);
            } else if(!editing) ReleaseKeyboardEditing();
        }
        void ReleaseKeyboardEditing()
        {
            if(keyboardNavigationOwned&&keyboardEventSystem!=null)keyboardEventSystem.sendNavigationEvents=previousKeyboardNavigation;
            keyboardNavigationOwned=false;GameInput.EditingKeyboard=false;
        }
        bool UpdateKeyboardSelection()
        {
            SyncKeyboardEditing();
            if(!GameInput.EditingKeyboard)return false;
            if(GameInput.GetKeyDown(KeyCode.Escape)){CancelSettings();return true;}
            if(confirmingDisplay||pendingKeyAction.HasValue||Keyboard.current==null)return true;
            foreach(KeyCode code in Enum.GetValues(typeof(KeyCode)))
                if(KeyboardBindingValues.Assignable(code)&&GameInput.GetKeyDown(code)) { SelectKeyboardKey(code);break; }
            return true;
        }
        void SelectKeyboardKey(KeyCode key)
        {
            selectedKeyboardKey=key;
            if(settingsDraft.keyboard.Find(key,out _,out int slot))keyboardSlot=slot;
            message=key==KeyCode.Escape?"Esc는 설정 열기·뒤로 가기를 위해 고정됩니다.":"오른쪽에서 연결할 기능을 선택해 주세요.";
            Render();
        }
        void AssignKeyboardKey(GameKeyAction action)
        {
            if(!KeyboardBindingValues.Assignable(selectedKeyboardKey))return;
            var bindings=settingsDraft.keyboard;
            if(bindings.Get(action,keyboardSlot)==selectedKeyboardKey)return;
            bool elsewhere=bindings.Find(selectedKeyboardKey,out var old,out _)&&old!=action;
            if(elsewhere||bindings.Get(action,keyboardSlot)!=KeyCode.None){pendingKeyAction=action;Render();return;}
            FinishKeyboardAssignment(action);
        }
        void FinishKeyboardAssignment(GameKeyAction action)
        {
            settingsDraft.keyboard.Assign(action,keyboardSlot,selectedKeyboardKey);pendingKeyAction=null;
            message="키를 연결했습니다. 적용을 누르면 저장됩니다.";Render();
        }
        Button KeyControl(Transform parent,string name,string title,float x,float y,float r,float t,Action click,Color fill,int size=16)
        {
            var holder=Rect(name,parent,x,y,r,t);
            var body=holder.gameObject.AddComponent<KeyboardKeycapGraphic>();body.face=fill;body.raycastTarget=true;
            var button=holder.gameObject.AddComponent<Button>();button.targetGraphic=body;
            button.navigation=new Navigation{mode=Navigation.Mode.None};
            var colors=button.colors;colors.highlightedColor=new Color(1,.94f,.82f);colors.pressedColor=new Color(.85f,.79f,.69f);button.colors=colors;
            var label=Label(holder,title,.035f,.06f,.965f,.94f,size);label.color=fill.r>.9f&&fill.g<.7f?Color.white:KeyInk;label.enableWordWrapping=false;label.fontStyle=FontStyles.Bold;label.raycastTarget=false;
            button.onClick.AddListener(()=>{EventSystem.current?.SetSelectedGameObject(null);click();});return button;
        }
        void KeyboardSettings()
        {
            var bindings=settingsDraft.keyboard;
            Label(content,"키를 선택한 뒤 오른쪽에서 기능을 연결하세요.",.292f,.730f,.90f,.773f,18).alignment=TextAlignmentOptions.MidlineLeft;
            // A single uncluttered keyboard bed, separate from the existing stitched section border.
            var bed=RecordShape("Keyboard bed",content,.282f,.304f,.421f,.376f,new Color(.89f,.75f,.54f),true,10);bed.insetColor=new Color(.97f,.90f,.76f);
            void Key(KeyCode code,float unit,float row,float width=1,float height=1,string caption=null) {
                float x=.289f+unit*.0271f, y=.645f-row*.054f;
                Color color=new Color(1,.966f,.87f);
                if(bindings.Find(code,out var action,out _))color=(int)action<4?new Color(.72f,.89f,.80f):new Color(1,.86f,.57f);
                if(code==selectedKeyboardKey)color=new Color(1,.49f,.49f);
                KeyControl(content,"Keyboard key "+code,caption??KeyboardBindingValues.KeyLabel(code),x,y,x+width*.0271f-.0028f,y+height*.054f-.007f,()=>SelectKeyboardKey(code),color,width>1?12:14);
            }
            Key(KeyCode.Escape,0,-.92f,1,.76f);
            for(int i=0;i<12;i++)Key(KeyCode.F1+i,1.5f+i*1.09f,-.92f,1,.76f);
            KeyCode[][] rows={
                new[]{KeyCode.BackQuote,KeyCode.Alpha1,KeyCode.Alpha2,KeyCode.Alpha3,KeyCode.Alpha4,KeyCode.Alpha5,KeyCode.Alpha6,KeyCode.Alpha7,KeyCode.Alpha8,KeyCode.Alpha9,KeyCode.Alpha0,KeyCode.Minus,KeyCode.Equals},
                new[]{KeyCode.Q,KeyCode.W,KeyCode.E,KeyCode.R,KeyCode.T,KeyCode.Y,KeyCode.U,KeyCode.I,KeyCode.O,KeyCode.P,KeyCode.LeftBracket,KeyCode.RightBracket},
                new[]{KeyCode.A,KeyCode.S,KeyCode.D,KeyCode.F,KeyCode.G,KeyCode.H,KeyCode.J,KeyCode.K,KeyCode.L,KeyCode.Semicolon,KeyCode.Quote},
                new[]{KeyCode.Z,KeyCode.X,KeyCode.C,KeyCode.V,KeyCode.B,KeyCode.N,KeyCode.M,KeyCode.Comma,KeyCode.Period,KeyCode.Slash}
            };
            float[] starts={0,1.5f,1.85f,2.3f};
            for(int row=0;row<4;row++)for(int i=0;i<rows[row].Length;i++)Key(rows[row][i],starts[row]+i,row);
            Key(KeyCode.Backspace,13,0,2,1,"Back");Key(KeyCode.Tab,0,1,1.5f);Key(KeyCode.Backslash,13.5f,1,1.5f);
            Key(KeyCode.CapsLock,0,2,1.85f,1,"Caps");Key(KeyCode.Return,12.85f,2,2.15f);
            Key(KeyCode.LeftShift,0,3,2.3f);Key(KeyCode.RightShift,12.3f,3,2.7f);
            Key(KeyCode.LeftControl,0,4,1.7f);Key(KeyCode.LeftAlt,1.7f,4,1.6f);Key(KeyCode.Space,3.3f,4,6.4f);Key(KeyCode.RightAlt,9.7f,4,1.6f);Key(KeyCode.RightControl,11.3f,4,1.7f);
            Key(KeyCode.UpArrow,13,4);Key(KeyCode.LeftArrow,12,5);Key(KeyCode.DownArrow,13,5);Key(KeyCode.RightArrow,14,5);
            Label(content,"민트: 이동    노랑: 기능    코랄: 선택",.294f,.337f,.603f,.370f,13).alignment=TextAlignmentOptions.MidlineLeft;
            Label(content,"실제 키를 눌러 선택할 수도 있습니다.\n숫자패드 등 그림 밖의 키도 지원 · Esc는 고정",.294f,.263f,.698f,.324f,15).alignment=TextAlignmentOptions.MidlineLeft;
            Label(content,"선택한 키",.723f,.695f,.897f,.735f,18);
            SettingsButton(content,KeyboardBindingValues.KeyLabel(selectedKeyboardKey),.725f,.628f,.897f,.692f,()=>{},true).name="Selected keyboard key";
            string assigned=bindings.Find(selectedKeyboardKey,out var current,out _)?KeyboardBindingValues.Name(current):"연결된 기능 없음";
            Label(content,assigned,.718f,.589f,.903f,.625f,15);
            KeyControl(content,"Keyboard primary","기본 키",.716f,.539f,.806f,.582f,()=>{keyboardSlot=0;Render();},keyboardSlot==0?StitchPatchTheme.Mint:new Color(1,.96f,.87f),14);
            KeyControl(content,"Keyboard secondary","보조 키",.811f,.539f,.901f,.582f,()=>{keyboardSlot=1;Render();},keyboardSlot==1?StitchPatchTheme.Mint:new Color(1,.96f,.87f),14);
            string[] groups={"전투","이동","소모품","메뉴"};
            for(int i=0;i<4;i++){int g=i;KeyControl(content,"Keyboard group "+i,groups[i],.716f+i*.047f,.493f,.759f+i*.047f,.532f,()=>{keyboardGroup=g;Render();},keyboardGroup==i?new Color(1,.64f,.57f):new Color(1,.96f,.87f),12);}
            GameKeyAction[][] actions={
                new[]{GameKeyAction.Active,GameKeyAction.Dash,GameKeyAction.Interact,GameKeyAction.Overcharge},
                new[]{GameKeyAction.MoveUp,GameKeyAction.MoveDown,GameKeyAction.MoveLeft,GameKeyAction.MoveRight},
                new[]{GameKeyAction.Slot1,GameKeyAction.Slot2,GameKeyAction.Slot3,GameKeyAction.Slot4},
                new[]{GameKeyAction.Journal,GameKeyAction.Settings}
            };
            int rowIndex=0;foreach(var action in actions[keyboardGroup]) {
                var a=action;float y=.438f-rowIndex++*.055f;
                string text=KeyboardBindingValues.Name(a)+"  ["+KeyboardBindingValues.KeyLabel(bindings.Get(a,keyboardSlot))+"]";
                var button=SettingsButton(content,text,.716f,y,.901f,y+.047f,()=>AssignKeyboardKey(a),false,true,bindings.Get(a,keyboardSlot)==selectedKeyboardKey);button.name="Keyboard assign "+a;
                var feedback=button.GetComponent<ApothecaryButtonFeedback>();feedback.label.fontSizeMax=14;feedback.label.fontSizeMin=11;
                button.interactable=KeyboardBindingValues.Assignable(selectedKeyboardKey)&&a!=GameKeyAction.Settings;
            }
            var clear=KeyControl(content,"Keyboard unassign","할당 해제",.716f,.228f,.901f,.265f,()=>{
                if(bindings.Find(selectedKeyboardKey,out var a,out int slot)&&a!=GameKeyAction.Settings)bindings.Assign(a,slot,KeyCode.None);
                message="해제했습니다. 필수 기능에는 키가 하나 이상 필요합니다.";Render();
            },new Color(.90f,.87f,.82f),14);
            clear.interactable=selectedKeyboardKey!=KeyCode.Escape&&bindings.Find(selectedKeyboardKey,out _,out _);
        }
        void KeyboardConflict()
        {
            if(!pendingKeyAction.HasValue)return;
            var action=pendingKeyAction.Value;
            var shade=ImageAt(content,null,0,0,1,1,false);shade.color=new Color(0,0,0,.65f);shade.raycastTarget=true;
            Panel(content,.22f,.28f,.78f,.73f);
            Label(content,"키 연결을 변경할까요?",.26f,.61f,.74f,.68f,28);
            var b=settingsDraft.keyboard;
            string old=b.Find(selectedKeyboardKey,out var a,out _)?KeyboardBindingValues.Name(a):"없음";
            Label(content,$"{KeyboardBindingValues.KeyLabel(selectedKeyboardKey)} : {old} → {KeyboardBindingValues.Name(action)}\n{(keyboardSlot==0?"기본 키":"보조 키")} {KeyboardBindingValues.KeyLabel(b.Get(action,keyboardSlot))}을 교체합니다.\n다른 보조 키는 유지됩니다.",.27f,.43f,.73f,.59f,20);
            SettingsButton(content,"취소",.28f,.32f,.48f,.40f,()=>{pendingKeyAction=null;Render();}).name="Keyboard conflict cancel";
            SettingsButton(content,"변경",.52f,.32f,.72f,.40f,()=>FinishKeyboardAssignment(action),true).name="Keyboard conflict confirm";
        }
    }
}
