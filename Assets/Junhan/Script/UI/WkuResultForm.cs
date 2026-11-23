using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Vampire
{
    // Own only form navigation. TMP still owns text, IME composition, selection and caret.
    [DefaultExecutionOrder(-10000)]
    public sealed class WkuResultForm:MonoBehaviour
    {
        public TMP_InputField School, Participant;
        public Button Submit;
        TextMeshProUGUI status;
        Func<bool> canRegister;
        int compositionFrame=-20, commandFrame=-20;
        bool awaitingEnterRelease;
        string Composition=>EventSystem.current?.currentInputModule?.input?.compositionString??"";
        public void Bind(TMP_InputField school,TMP_InputField participant,Button submit,TextMeshProUGUI note,Func<bool> allowed)
        {
            School=school;Participant=participant;Submit=submit;status=note;canRegister=allowed;
            foreach(var field in new[]{School,Participant})
            {
                ((WkuResultInputField)field).Form=this;
                field.caretWidth=2;field.caretBlinkRate=.85f;field.onFocusSelectAll=false;
                field.onValueChanged.AddListener(_=>Refresh());
                field.onSelect.AddListener(_=>Refresh());
                var nav=field.navigation;nav.mode=Navigation.Mode.None;field.navigation=nav;
            }
            var buttonNav=Submit.navigation;buttonNav.mode=Navigation.Mode.None;Submit.navigation=buttonNav;
            Refresh();if(School.interactable)StartCoroutine(InitialFocus());
        }
        IEnumerator InitialFocus(){yield return null;Focus(0);}
        public void ObserveComposition(){if(Composition.Length>0)compositionFrame=Time.frameCount;}
        void Update()
        {
            ObserveComposition();var keyboard=Keyboard.current;
            if(keyboard==null)return;
            if(!keyboard.enterKey.isPressed&&!keyboard.numpadEnterKey.isPressed)awaitingEnterRelease=false;
            if(keyboard.tabKey.wasPressedThisFrame)HandleTab(keyboard.shiftKey.isPressed);
            else if(keyboard.enterKey.wasPressedThisFrame||keyboard.numpadEnterKey.wasPressedThisFrame)HandleEnter();
        }
        public void HandleTab(bool reverse)
        {
            if(commandFrame==Time.frameCount||Composition.Length>0)return;
            commandFrame=Time.frameCount;
            int current=Index(),next=current;
            for(int i=0;i<3;i++){next=(next+(reverse?2:1))%3;if(next!=2||Submit.interactable)break;}
            Focus(next);
        }
        public void HandleEnter()
        {
            ObserveComposition();
            if(commandFrame==Time.frameCount||awaitingEnterRelease)return;
            commandFrame=Time.frameCount;awaitingEnterRelease=true;
            if(Composition.Length>0||Time.frameCount<=compositionFrame+1)return;
            int current=Index();
            if(current==0){if(Valid(School,40))Focus(1);else Focus(0);}
            else if(Valid(School,40)&&Valid(Participant,24)&&Submit.interactable)Submit.onClick.Invoke();
            else Focus(Valid(School,40)?1:0);
        }
        static bool Valid(TMP_InputField field,int max){int length=FestivalLeaderboard.Clean(field.text).Length;return length>0&&length<=max;}
        int Index()
        {
            var selected=EventSystem.current?.currentSelectedGameObject;
            return selected==Participant.gameObject?1:selected==Submit.gameObject?2:0;
        }
        public void Focus(int index)
        {
            if(!School.interactable)return;
            var target=index==0?School.gameObject:index==1?Participant.gameObject:Submit.gameObject;
            EventSystem.current.SetSelectedGameObject(target);
            if(index<2)
            {
                var field=index==0?School:Participant;field.ActivateInputField();field.MoveTextEnd(false);
            }
            Refresh();
        }
        void Refresh()
        {
            Submit.interactable=canRegister()!=false&&Valid(School,40)&&Valid(Participant,24);
            if(!School.interactable){status.text="행사 기록 등록 완료";return;}
            status.text=Index()==0?"고등학교 이름 입력 중 · Tab 다음 · Enter 확인":Index()==1?"이름 입력 중 · Tab 다음 · Enter 등록":"Enter 기록 등록 · Shift+Tab 이전";
        }
    }

    public sealed class WkuResultInputField:TMP_InputField
    {
        public WkuResultForm Form;
        public bool ProcessResultKey(Event key)
        {
            if(key.keyCode==KeyCode.Tab){Form?.HandleTab(key.shift);return true;}
            if(key.keyCode==KeyCode.Return||key.keyCode==KeyCode.KeypadEnter){Form?.HandleEnter();return true;}
            ProcessEvent(key);ForceLabelUpdate();return false;
        }
        public override void OnSubmit(BaseEventData data){Form?.HandleEnter();data.Use();}
        public override void OnUpdateSelected(BaseEventData data)
        {
            if(!isFocused)return;
            Form?.ObserveComposition();
            var key=new Event();
            while(Event.PopEvent(key))if(key.rawType==EventType.KeyDown)ProcessResultKey(key);
            ForceLabelUpdate();data.Use();
        }
    }
}
