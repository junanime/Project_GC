using System.Linq;
using TMPro;
using UnityEngine;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
#if UNITY_EDITOR
        public static RunScoreData WkuPreviewData;
#endif
        // Official artwork is loaded unchanged; text and controls remain real game UI.
        void WkuPromoHeader()
        {
            ImageAt(content,null,.035f,.835f,.965f,.965f,false).color=Color.white;
            ImageAt(content,OctoberArt.Get("WkuPromo/OfficialSignature"),.055f,.85f,.335f,.95f);
            var title=Label(content,"게임콘텐츠학과",.36f,.884f,.76f,.947f,31);title.alignment=TextAlignmentOptions.MidlineLeft;
            var sub=Label(content,"찾아가는 고등학교 게임 체험 · 24시간의 사투",.36f,.842f,.945f,.888f,18);sub.alignment=TextAlignmentOptions.MidlineLeft;
        }
        void WkuResultArt(RunScoreData data)
        {
            WkuPromoHeader();
            Color accent=passed?new Color(.08f,.36f,.29f):new Color(.57f,.18f,.25f);
            ImageAt(content,null,.035f,.685f,.965f,.815f,false).color=accent;
            Label(content,passed?"생존 성공!":"생존 실패 · 도전 종료",.07f,.719f,.93f,.80f,42).color=Color.white;
            Label(content,passed?"45초를 버텨낸 여러분, 멋진 도전이었어요!":"끝까지 도전한 여러분, 다음 생존에 도전해 보세요!",.07f,.687f,.93f,.727f,19).color=Color.white;
            var card=Rect("WKU character patch",content,.045f,.285f,.26f,.66f);
            StitchPatchTheme.Attach(card,accent,true,true);
            var party=ImageAt(card,OctoberArt.Get(passed?"WkuPromo/PartySuccess":"WkuPromo/PartyFailure"),.025f,.15f,.975f,.97f);
            party.name="WKU shared party illustration";
            Label(card,"선택 · "+(character?.name??""),.025f,.015f,.975f,.17f,18);
            Label(content,$"{FestivalLeaderboard.Score(data):N0}점",.28f,.565f,.69f,.66f,43);
            Label(content,$"생존 {data?.seconds??0:0.0} / 45초  ·  처치 {stats?.MonstersKilled??((data?.normal??0)+(data?.special??0)):N0}",.275f,.505f,.695f,.565f,22);

            var promo=Rect("WKU game homepage",content,.725f,.28f,.95f,.665f);
            StitchPatchTheme.Attach(promo,StitchPatchTheme.Thread,true,true);
            Label(promo,"게임 공식 홈페이지",.03f,.855f,.97f,.98f,22);
            var qr=ImageAt(promo,OctoberArt.Get("WkuPromo/HomepageQr"),.5f,.5f,.5f,.5f,false);
            qr.rectTransform.sizeDelta=new Vector2(148,148);qr.rectTransform.anchoredPosition=new Vector2(0,0);
            qr.name="WKU homepage QR 148px";
            Label(promo,"Steam 출시 소식 보기",.025f,.12f,.975f,.25f,15);
            Label(promo,"24hours-gstar.pages.dev",.025f,.015f,.975f,.12f,14);
            Label(content,"원광대학교 게임콘텐츠학과",.05f,.175f,.95f,.23f,25);
            Label(content,"게임을 즐기는 오늘, 게임을 만드는 내일!",.22f,.115f,.78f,.175f,22);

        }
    }
}





