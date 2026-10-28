using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        internal DemoCompletionDialogue BuildDemoCompletionDialogue()
        {
            var holder=Rect("웹 데모 · 탐험 완료",root,0,0,1,1);
            holder.SetAsLastSibling();
            var dim=holder.gameObject.AddComponent<Image>();dim.color=new Color(0,0,0,.22f);dim.raycastTarget=true;
            var view=holder.gameObject.AddComponent<DemoCompletionDialogue>();
            var panel=Rect("Demo dialogue paper",holder,.04f,.035f,.96f,.34f);
            var patch=StitchPatchTheme.Attach(panel,StitchPatchTheme.Coral,true);patch.radius=22;patch.inset=9;
            view.Message=Label(panel,DemoCompletionDialogue.CompletionMessage,.04f,.42f,.96f,.88f,32);
            view.Message.enableAutoSizing=true;view.Message.fontSizeMin=22;view.Message.fontSizeMax=32;
            view.Message.enableWordWrapping=true;
            var next=ActionButton(panel,"메인화면으로",.64f,.07f,.95f,.34f,view.Confirm);
            StitchPatchTheme.Button(next.GetComponent<ApothecaryButtonFeedback>(),false);
            view.ConfirmButton=next.GetComponent<Button>();
            return view;
        }

        internal CapsuleDoctorDialogue BuildCapsuleDialogue()
        {
            var holder=Rect("한의사 · 첫 보스 튜토리얼",root,0,0,1,1);
            var dim=holder.gameObject.AddComponent<Image>();dim.color=new Color(0,0,0,.16f);dim.raycastTarget=true;
            var view=holder.gameObject.AddComponent<CapsuleDoctorDialogue>();
            var panel=Rect("Dialogue paper",holder,.02f,.025f,.98f,.34f);var patch=StitchPatchTheme.Attach(panel,StitchPatchTheme.Coral,true);patch.radius=22;patch.inset=9;
            var tag=Rect("Speaker",holder,.065f,.30f,.25f,.38f);var tagArt=StitchPatchTheme.Attach(tag,StitchPatchTheme.Coral,true);tagArt.button=true;
            Label(tag,"한의사",.04f,.06f,.96f,.94f,28).color=Color.white;
            view.Message=Label(panel,"",.045f,.25f,.69f,.83f,28);view.Message.alignment=TextAlignmentOptions.MidlineLeft;view.Message.enableWordWrapping=true;
            view.Counter=Label(panel,"1 / 2",.03f,.02f,.13f,.18f,18);view.Counter.color=new Color(.5f,.45f,.4f);
            var portrait=ImageAt(holder,Resources.Load<Sprite>("AmphibiousCapsule/Doctor"),.67f,.13f,.96f,.66f);portrait.preserveAspect=true;portrait.raycastTarget=false;
            var next=ActionButton(panel,"다음",.80f,.04f,.97f,.25f,()=>{});StitchPatchTheme.Button(next.GetComponent<ApothecaryButtonFeedback>(),false);
            view.NextButton=next.GetComponent<Button>();view.ButtonLabel=next.GetComponentInChildren<TMP_Text>();return view;
        }
    }
}
