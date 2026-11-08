using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        bool stageChoiceOpen;
        void CloseStageChoice(){if(starting)return;stageChoiceOpen=false;Render();}
        void BuildStageChoice()
        {
            foreach(var button in content.GetComponentsInChildren<Button>())button.interactable=false;
            var overlay=Rect("Temporary stage choice",content,0,0,1,1);
            var dim=overlay.gameObject.AddComponent<Image>();dim.color=new Color(0,0,0,.62f);dim.raycastTarget=true;
            var panel=Rect("Stage choice paper",overlay,.23f,.23f,.77f,.77f);
            panel.gameObject.AddComponent<Image>().color=Color.clear;
            var paper=StitchPatchTheme.Attach(panel,StitchPatchTheme.Coral,true,true);paper.radius=20;paper.inset=9;
            Label(panel,"출전할 스테이지 선택",.07f,.79f,.93f,.95f,29).alignment=TextAlignmentOptions.Center;
            Label(panel,"현재 고른 캐릭터 · 장비 · 기본 능력치로 시작합니다",.06f,.68f,.94f,.80f,17).alignment=TextAlignmentOptions.Center;
            var one=ActionButton(panel,"스테이지 1 · 위장",.07f,.43f,.48f,.65f,()=>LaunchStage(1),true);
            var two=ActionButton(panel,"스테이지 2 · 대장",.52f,.43f,.93f,.65f,()=>LaunchStage(2),true,StageProgression.StageTwoEnabled);
            StitchPatchTheme.Button(one.GetComponent<ApothecaryButtonFeedback>(),true);StitchPatchTheme.Button(two.GetComponent<ApothecaryButtonFeedback>(),false);
            Label(panel,"일반 진행 · 랭킹 집계",.07f,.32f,.48f,.43f,16).alignment=TextAlignmentOptions.Center;
            Label(panel,StageProgression.StageTwoEnabled?"임시 직접 입장 · 랭킹 제외":"웹 데모는 스테이지 1까지만 공개",.51f,.32f,.95f,.43f,16).alignment=TextAlignmentOptions.Center;
            var cancel=ActionButton(panel,"돌아가기",.33f,.075f,.67f,.24f,CloseStageChoice);
            StitchPatchTheme.Button(cancel.GetComponent<ApothecaryButtonFeedback>(),false);
        }
    }
}
