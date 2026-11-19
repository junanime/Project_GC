using System;
using NeedleType = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;

namespace Vampire
{
    // Booth-facing copy. The full source descriptions and actual acquired values stay in the run book.
    public static class AugmentBrief
    {
        static readonly string[] parents = {
            "맞힌 적을 중독시켜 지속 피해를 줍니다.",
            "침이 적중한 자리에서 터져 주변 적도 맞힙니다.",
            "날아가는 침이 적을 향해 휘어집니다.",
            "침이 적을 뚫고 뒤에 있는 적까지 맞힙니다.",
            "침에 맞은 적이 느려집니다.",
            "침으로 적을 맞히면 체력을 회복합니다.",
            "날아간 침이 돌아오며 적을 다시 맞힙니다.",
            "대시 기회가 늘고 출발점에서 침이 퍼져 나갑니다.",
            "침이 지나간 자리에 적을 베는 실이 남습니다.",
            "적을 거듭 맞힐수록 그 적이 받는 피해가 커집니다.",
            "침이 멀리 날아갈수록 더 큰 피해를 줍니다.",
            "침으로 표식을 남기고 다시 맞혀 추가 피해를 줍니다.",
            "앞뒤로 침을 동시에 쏩니다.",
            "침에 맞은 적이 죽으면 피해를 주는 소화액을 남깁니다.",
            "침을 연달아 맞히면 공격 속도가 빨라집니다.",
            "장내균이 쌓인 적을 처치하면 경험치가 더 나옵니다.",
            "적에게 쌓인 씨앗이 싹터 주변 적까지 공격합니다.",
            "일정 확률로 적에게 불을 붙여 지속 피해를 줍니다.",
            "적을 느리게 하고 얼립니다. 보스는 느려집니다.",
            "침을 더 자주 쏩니다. 침의 위력과 비행 속도도 오릅니다.",
            "적중한 자리에서 충격파가 퍼져 주변 적을 밀어냅니다."
        };

        static readonly string[] originals = {
            "독이 더 큰 피해를 줍니다.", "독이 더 오래 남습니다.", "독이 더 자주 피해를 줍니다.",
            "폭발 범위가 넓어집니다.", "폭발 피해가 커집니다.", "폭발 중심에 있는 적이 더 큰 피해를 받습니다.",
            "더 멀리 있는 적을 감지합니다.", "침이 적을 향해 더 빠르게 방향을 틉니다.", "추적 중인 적을 맞히면 더 큰 피해를 줍니다.",
            "침이 적 하나를 더 관통합니다.", "적을 관통할 때마다 침이 빨라집니다.", "앞서 관통한 적이 많을수록 다음 타격이 강해집니다.",
            "맞은 적의 둔화가 더 오래갑니다.", "맞은 적을 더 느리게 만듭니다.", "둔화된 적이 더 큰 피해를 받습니다.",
            "흡혈로 회복하는 체력이 늘어납니다.", "받는 피해가 줄어듭니다.", "최대 체력이 늘어납니다.",
            "되돌아오는 침이 더 큰 피해를 줍니다.", "날아간 침이 더 빠르게 돌아옵니다.", "되돌아오는 침이 적을 바깥으로 밀어냅니다.",
            "대시가 끝난 자리에도 침술진이 생깁니다.", "서로 다른 침술진의 침이 만나면 폭발합니다.", "침술진이 일정 확률로 한 번 더 발동합니다.",
            "침이 남긴 실이 더 오래 남습니다.", "실에 닿은 적에게 더 큰 피해를 줍니다.", "침이 남기는 실의 폭이 넓어집니다.",
            "적에게 쌓인 부식이 더 오래갑니다.", "부식 한 겹당 적이 받는 피해가 커집니다.", "부식을 더 많이 쌓을 수 있습니다.",
            "침이 멀리 날아갈 때 피해가 더 크게 증가합니다.", "멀리서 줄 수 있는 최대 피해가 커집니다.", "침을 쏘는 순간부터 거리 피해 보너스를 얻습니다.",
            "표식이 더 오래 남습니다.", "표식을 터뜨릴 때 더 큰 피해를 줍니다.", "터뜨린 표식이 일정 확률로 다시 생깁니다.",
            "앞뒤에 더해 좌우로도 침을 쏩니다.", "쏠 때마다 강해지는 침의 방향이 바뀝니다.", "반대 극성의 침을 맞히면 낙인이 터집니다.",
            "소화액 웅덩이가 더 오래 남습니다.", "소화액 웅덩이가 넓어집니다.", "소화액 웅덩이가 더 큰 피해를 줍니다.",
            "침 적중으로 얻은 공격 속도가 더 오래갑니다.", "적중이 쌓일 때마다 공격 속도가 더 오릅니다.", "공격 속도 효과를 더 많이 쌓을 수 있습니다.",
            "한 적에게 장내균을 더 많이 쌓을 수 있습니다.", "한 번 맞혀도 장내균이 두 겹 쌓일 수 있습니다.", "장내균이 많이 쌓인 적일수록 추가 경험치를 얻기 쉽습니다.",
            "씨앗에서 뻗는 가지가 늘어납니다.", "씨앗에서 뻗은 가지가 더 큰 피해를 줍니다.", "가지로 적을 처치하면 회복 지대가 생깁니다.",
            "겹쳐 붙은 화상이 더 큰 피해를 줍니다.", "화상이 더 오래갑니다.", "한 적에게 화상을 더 많이 쌓을 수 있습니다.",
            "냉기가 쌓일 때마다 적이 더 느려집니다.", "빙결이 깨질 때 폭발합니다.", "빙결이 깨진 자리에 냉기 지대가 생깁니다.",
            "질풍침을 더 자주 쏩니다.", "질풍침의 피해가 커집니다.", "질풍침을 한 발 더 쏩니다.",
            "충격파가 더 넓게 퍼집니다.", "충격파가 더 큰 피해를 줍니다.", "충격파가 적을 더 멀리 밀어냅니다."
        };

        static readonly string[] nobles = {
            "최대 체력이 1이 되고 회복이 막힙니다. 침의 위력·발사 수·사거리가 늘어납니다.",
            "분신이 따라다니며 내 침으로 함께 공격합니다.",
            "깃털침이 몸 주위를 돌며 닿은 적을 공격합니다.",
            "공격을 길게 눌러 크고 강한 관통침을 쏩니다.",
            "기본 공격이 8자로 날며 적을 치는 깃털로 바뀝니다.",
            "주기적으로 화면 안의 적을 잠시 멈춥니다.",
            "적이 몰린 곳에 인장을 펼쳐 끌어모으며 피해를 줍니다.",
            "주기적으로 날갯바람이 주변 적을 밀치고 피해를 줍니다.",
            "피격 없이 버티면 공격을 한 번 막는 보호막이 세 겹까지 쌓입니다.",
            "경험치와 골드를 줍지 않으면 침이 강하고 빨라지며 사거리가 늘어납니다.",
            "침을 모아 가까운 적에게 부채꼴로 쏩니다. 대시하면 한 발 장전합니다."
        };

        public static string Parent(NeedleType type) => parents[(int)type];

        public static string Noble(string key)
        {
            int index = Array.IndexOf(PhoenixNobleTheme.Keys, key);
            return index >= 0 ? nobles[index] : null;
        }

        public static string Original(int index, int ownedCount)
        {
            if (ownedCount > 0)
            {
                switch (index)
                {
                    case 20: return "되돌아오는 침이 적을 더 멀리 밀어냅니다.";
                    case 21: return "대시 종점의 침술진이 더 많은 침을 쏩니다.";
                    case 22: return "침술진끼리 일으키는 폭발이 더 강해집니다.";
                    case 23: return "침술진이 한 번 더 발동할 확률이 커집니다.";
                    case 36: return ownedCount == 1 ? "앞뒤로 쏘는 침이 더 강해집니다." : "좌우로 쏘는 침이 더 강해집니다.";
                    case 37: return "번갈아 강화되는 방향의 침이 더 강해집니다.";
                    case 38: return "반대 극성으로 터뜨린 낙인이 더 큰 피해를 줍니다.";
                    case 50: return "발아로 만든 회복 지대가 더 오래 남습니다.";
                    case 55: return "빙결이 깨질 때의 폭발이 더 강해집니다.";
                    case 56: return "빙결이 깨진 자리의 냉기 지대가 더 오래 남습니다.";
                }
            }
            return originals[index];
        }

        public static string For(Ability ability)
        {
            if (ability is Ver4AugmentOffer offer)
            {
                switch (offer.Kind)
                {
                    case Ver4RewardKind.Original: return Original((int)offer.Parent.Type * 3 + offer.Option, offer.OwnedOriginalCount);
                    case Ver4RewardKind.Numeric: return Ver4AugmentCatalog.NumericDescription(offer.Option,offer.Amount).Replace(" (공유 침)","");
                    case Ver4RewardKind.NewSpecial: return Parent(offer.Parent.Type);
                    case Ver4RewardKind.LegendaryAbility: return For(offer.Source);
                }
            }
            if (ability is SyringeSpecialAugmentAbility parent) return Parent(parent.Type);
            if (ability is SyringeLegendaryAugmentAbility noble) return Noble(noble.Type.ToString()) ?? noble.Description;
            return ability.Description;
        }
    }
}
