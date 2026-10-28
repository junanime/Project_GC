using System;
using UnityEngine;
namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        FinalBossSummonInteractable bossChoiceOwner;Action<FoodBossKind> bossChosen;Action bossChoiceCancelled;
        public bool ShowBossChoice(FinalBossSummonInteractable owner,Action<FoodBossKind> choose,Action cancel)
        {
            if(level==null||level.IsLevelEnded||Page!="hud"||Time.timeScale<=0)return false;
            bossChoiceOwner=owner;bossChosen=choose;bossChoiceCancelled=cancel;previousTime=Time.timeScale;ownsPause=true;Time.timeScale=0;Show("bossChoice");return true;
        }
        public void CancelBossChoice(FinalBossSummonInteractable owner){if(Page=="bossChoice"&&bossChoiceOwner==owner)CloseBossChoice();}
        public void CloseBossChoice(FoodBossKind? selected=null)
        {
            var choose=bossChosen;var cancel=bossChoiceCancelled;bossChosen=null;bossChoiceCancelled=null;bossChoiceOwner=null;
            if(ownsPause&&level!=null&&!level.IsLevelEnded)Time.timeScale=previousTime;ownsPause=false;Show("hud");
            if(selected.HasValue)choose?.Invoke(selected.Value);else cancel?.Invoke();
        }
        void BuildBossChoice()
        {
            OctoberFrame("보스 선택 · 테스트");Label(content,"상대할 보스를 선택하세요",.2f,.76f,.8f,.84f,26);
            string[] concepts={"롤케이크 달팽이","데니쉬롤 소라게","레몬 마들렌 가리비","탕후루 헤라클레스","우동 말미잘"};
            for(int i=0;i<5;i++)
            {
                var kind=(FoodBossKind)i;float x=.09f+i*.166f;
                var button=ActionButton(content,"",x,.31f,x+.155f,.72f,()=>CloseBossChoice(kind));var visual=button.transform.Find("Visual");
                ImageAt(visual,FoodBossArt.Walk(kind,false,0),.05f,.35f,.95f,.94f);
                Label(visual,FoodBossArt.Names[i],.04f,.18f,.96f,.34f,23).color=FoodBossArt.Warnings[i]*.65f;
                Label(visual,concepts[i],.03f,.04f,.97f,.18f,13);
                StitchPatchTheme.Button(button.GetComponent<ApothecaryButtonFeedback>(),false);
            }
            Label(content,"선택 소환한 판은 랭킹에 등록되지 않습니다.",.2f,.22f,.8f,.29f,17);
            ActionButton(content,"취소 / ESC",.37f,.10f,.63f,.20f,()=>CloseBossChoice());
        }
    }
}
