using System;
using UnityEngine;

namespace Vampire
{
    // Presentation keys only. Serialized ability IDs and damage accounting keys stay unchanged.
    public static class PhoenixNobleTheme
    {
        public static readonly string[] Keys = { "LifeBurn", "CloneCulture", "HedgehogNeedle", "HeavySnipe", "CursorControl", "NeuralBlock", "OrganCompression", "GastricPeristalsisWave", "MucosalFortress", "HungrySpirit", "NeedleShotgun" };
        static readonly string[] Names = { "봉심연소", "쌍봉분신", "봉익윤무", "천공봉침", "유영봉우", "봉명진혼", "봉인흡진", "봉황파동", "삼중봉익", "고고한 봉황", "산개봉우" };
        static readonly string[] OldNames = { "생명연소", "분신배양", "고슴도침", "대물침", "이기어침", "신경차단", "장기압착", "위산 연동파", "점막 요새", "헝그리정신", "샷건침" };
        static readonly string[] Descriptions =
        {
            "봉황의 심장을 태워 최대 체력이 1이 되고 회복할 수 없습니다. 대신 침의 피해량·발사 수·사거리가 크게 증가합니다.",
            "봉황의 잔영으로 분신을 만듭니다. 분신은 플레이어를 따라다니며 현재 침 능력을 바탕으로 함께 공격합니다.",
            "무지개 봉황 깃털침이 침 끝을 바깥으로 향한 채 톱니바퀴처럼 회전합니다. 닿은 적에게 직접 피해를 줍니다. 흩날리는 잔깃은 시각 효과입니다.",
            "봉황의 기운을 침 끝에 모아 발사합니다. 공격 버튼을 오래 누를수록 피해량·크기·속도·관통력이 증가합니다.",
            "기본 자동 공격 대신 봉황 깃털이 플레이어 위에서 8자 궤도를 비행하며 적을 공격합니다.",
            "봉황의 울림으로 일정 주기마다 화면 안의 적을 잠시 정지시킵니다. 울림이 지속되는 동안 새로 등장한 적에게도 적용됩니다.",
            "일정 주기마다 적이 밀집한 곳에 봉황의 인장을 펼칩니다. 인장 안의 적을 중앙으로 끌어당기며 지속 피해를 줍니다.",
            "일정 주기마다 머리 위에 황금 봉황이 나타나 원형 에너지파를 방출합니다. 주변 적에게 피해를 주고 밀어냅니다.",
            "피해를 받지 않고 버티면 봉황의 날개 보호막이 쌓입니다. 최대 3개까지 보유하며 각 보호막이 피격 1회를 막습니다.",
            "경험치·골드를 줍지 않는 동안 봉황의 기운이 쌓여 공격력·사거리·공격속도가 증가합니다. 픽업 획득 시 누적 효과가 초기화됩니다.",
            "침을 봉황 깃털처럼 짧은 사거리의 부채꼴로 일제 발사합니다. 최대 4발까지 장전하며 대쉬하면 즉시 1발 장전합니다."
        };
        public static string NameFor(string key) { int i = Array.IndexOf(Keys, key); return i < 0 ? key : Names[i]; }
        public static string DescriptionFor(string key) { int i = Array.IndexOf(Keys, key); return i < 0 ? null : Descriptions[i]; }
        public static string KeyForLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            label = label.Trim();
            if (label == "생명 연소") label = "생명연소";
            if (label == "복제 배양") label = "분신배양";
            if (label == "아귀") label = "헝그리정신";
            if (label == "산탄침") label = "샷건침";
            int i = Array.IndexOf(OldNames, label);
            if (i < 0) i = Array.IndexOf(Names, label);
            return i < 0 ? null : Keys[i];
        }
        public static string DisplayLabel(string label) { string key = KeyForLabel(label); return key == null ? label : NameFor(key); }
        public static string RefreshText(string text)
        {
            if (text == null) return null;
            for (int i = 0; i < OldNames.Length; i++) text = text.Replace(OldNames[i], Names[i]);
            return text;
        }
        public static void RefreshCatalog(ApothecaryUIConfig config)
        {
            if (config == null || config.augments == null) return;
            foreach (var item in config.augments)
            {
                string key = KeyForLabel(item.title);
                if (key == null) continue;
                item.title = NameFor(key);
                item.description = DescriptionFor(key);
                item.icon = PhoenixNobleArt.Get(key) ?? item.icon;
            }
        }
    }
}
