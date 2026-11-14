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
            var panel=Rect("Stage choice paper",overlay,.17f,.20f,.83f,.80f);
            panel.gameObject.AddComponent<Image>().color=Color.clear;
            var paper=StitchPatchTheme.Attach(panel,StitchPatchTheme.Coral,true,true);paper.radius=20;paper.inset=9;
            Label(panel,"출전할 스테이지 선택",.07f,.79f,.93f,.95f,29).alignment=TextAlignmentOptions.Center;
            Label(panel,"현재 고른 캐릭터 · 장비 · 기본 능력치로 시작합니다",.06f,.68f,.94f,.80f,17).alignment=TextAlignmentOptions.Center;
            var zero=ActionButton(panel,"스테이지 0 · 구강",.05f,.43f,.34f,.65f,()=>LaunchStage(0),true,StageProgression.StageZeroEnabled);
            var one=ActionButton(panel,"스테이지 1 · 위장",.355f,.43f,.645f,.65f,()=>LaunchStage(1),true);
            var two=ActionButton(panel,"스테이지 2 · 대장",.66f,.43f,.95f,.65f,()=>LaunchStage(2),true,StageProgression.StageTwoEnabled);
            StitchPatchTheme.Button(zero.GetComponent<ApothecaryButtonFeedback>(),false);
            StitchPatchTheme.Button(one.GetComponent<ApothecaryButtonFeedback>(),true);
            StitchPatchTheme.Button(two.GetComponent<ApothecaryButtonFeedback>(),false);
            Label(panel,"필드 이벤트 탐험 · 랭킹 제외",.04f,.30f,.35f,.43f,15).alignment=TextAlignmentOptions.Center;
            Label(panel,"일반 진행 · 랭킹 집계",.35f,.30f,.65f,.43f,15).alignment=TextAlignmentOptions.Center;
            Label(panel,StageProgression.StageTwoEnabled?"직접 입장 · 랭킹 제외":"웹 데모는 스테이지 1까지",.65f,.30f,.96f,.43f,15).alignment=TextAlignmentOptions.Center;
            var cancel=ActionButton(panel,"돌아가기",.33f,.075f,.67f,.24f,CloseStageChoice);
            StitchPatchTheme.Button(cancel.GetComponent<ApothecaryButtonFeedback>(),false);
        }
    }
}
