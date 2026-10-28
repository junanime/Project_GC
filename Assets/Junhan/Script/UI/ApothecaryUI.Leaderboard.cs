using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        int rankingRevision = -1;
        TextMeshProUGUI runScoreStatus;
        void UpdateRanking()
        {
            if (Page == "main")
            {
                QaLeaderboard.Refresh();
                if (rankingRevision != QaLeaderboard.Revision) { rankingRevision = QaLeaderboard.Revision; dirty = true; }
            }
            if (Page == "result" && runScoreStatus != null && RunScore.Current != null)
                runScoreStatus.text = $"이번 탐험 {RunScore.Current.Data.Total:N0}점  ·  {RunScore.Current.Submission}";
        }
        void RankingPanel()
        {
            QaLeaderboard.LoadCache();
            // Fits left of the title actors; no buttons are covered.
            var panel = Rect("QA leaderboard",content,.022f,.055f,.252f,.49f);
            StitchPatchTheme.Attach(panel,StitchPatchTheme.Thread,true);
            Label(content,"탐험 랭킹",.037f,.442f,.235f,.481f,22).fontStyle=FontStyles.Bold;
            var rows=QaLeaderboard.VisibleRows(QaLeaderboard.Cache,QaLeaderboard.PlayerId);
            for(int i=0;i<10;i++)
            {
                var entry=i<rows.Count?rows[i]:null;
                float y=.408f-i*.029f;
                bool mine=entry!=null&&entry.playerId==QaLeaderboard.PlayerId;
                ImageAt(content,null,.035f,y,.239f,y+.027f,false).color=mine?new Color(.65f,.9f,.79f):new Color(.94f,.85f,.71f,i%2==0?.55f:.25f);
                var rank=Label(content,entry!=null?entry.rank.ToString():"—",.038f,y,.065f,y+.027f,12);
                string name=entry!=null?entry.name+(mine?" (나)":""):i==9&&QaLeaderboard.Cache.mine==null?"내 기록 없음":"";
                var label=Label(content,name,.067f,y,.183f,y+.027f,12);label.richText=false;label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Ellipsis;label.alignment=TextAlignmentOptions.MidlineLeft;
                Label(content,entry!=null?entry.score.ToString("N0"):"",.185f,y,.234f,y+.027f,12).alignment=TextAlignmentOptions.MidlineRight;
            }
            Label(content,QaLeaderboard.Status,.034f,.108f,.24f,.139f,10);
            ActionButton(content,"이름 변경",.04f,.069f,.143f,.106f,()=>Show("rankingName"));
            ActionButton(content,string.IsNullOrEmpty(QaLeaderboard.AccountNotice)?"점수 안내":"계정 알림",.15f,.069f,.235f,.106f,()=>Show("rankingInfo"));
        }
        void RankingName()
        {
            OctoberFrame("탐험가 이름");
            Label(content,"랭킹에 표시할 이름",.23f,.61f,.77f,.68f,25);
            var box=ImageAt(content,null,.30f,.48f,.70f,.58f,false);box.color=new Color(1,.97f,.9f);box.raycastTarget=true;
            var inputSeam=StitchPatchTheme.Attach(box.transform,StitchPatchTheme.Mint);inputSeam.inset=5;
            var field=box.gameObject.AddComponent<TMP_InputField>();
            var text=Label(box.transform,QaLeaderboard.Nickname,.04f,.03f,.96f,.97f,26);text.richText=false;
            field.textViewport=box.rectTransform;field.textComponent=text;field.characterLimit=12;field.text=QaLeaderboard.Nickname;
            Label(content,"한글·영문·숫자·_·- / 2~12자\n" + (SteamRuntime.IsSteamBuild ? "랭킹 기록은 현재 Steam 계정에 연결됩니다." : "QA 익명 계정은 이 기기·브라우저에 저장됩니다."),.24f,.32f,.76f,.45f,18);
            var note=Label(content,"",.25f,.25f,.75f,.31f,17);
            bool saving=false;
            ActionButton(content,"저장",.52f,.14f,.73f,.23f,async()=>
            {
                if(saving)return;saving=true;
                bool ok=await QaLeaderboard.SetNickname(field.text);
                saving=false;
                if(this==null)return;
                if(ok)Show("main");else if(note!=null)note.text="이름 형식이나 인터넷 연결을 확인해 주세요.";
            },true);
            ActionButton(content,"뒤로",.27f,.14f,.48f,.23f,()=>Show("main"));
        }
        void RankingInfo()
        {
            OctoberFrame("랭킹 안내");
            var text=Label(content,string.IsNullOrEmpty(QaLeaderboard.AccountNotice)?
                "QA 시즌 1 · 한 판 개인 최고기록\n\n일반 몬스터 30점 / 특수 몬스터 50점\n엘리트 100점 / 미니보스 500점 / 보스 1,500점\n미니스테이지 최초 입장 100점 / 성공 300점\n혈전 과충전 완료 200점 / 탐험 성공 1,000점\n\n위산 처치·테스트 단축키 사용 기록은 제외됩니다.\n오프라인으로 출전한 판은 온라인 순위에 등록되지 않습니다.":QaLeaderboard.AccountNotice,.18f,.27f,.82f,.75f,23);
            text.richText=false;
            ActionButton(content,"뒤로",.39f,.14f,.61f,.23f,()=>Show("main"));
        }
    }
}
