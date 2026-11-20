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
            if (ownedCount == OriginalAugmentProgress.MaxEffectSelections) return "최대 3단계에 도달했습니다.";
            string full = OriginalAugmentDescription.Effect(index, ownedCount);
            int rank = ownedCount + 1;
            switch (index)
            {
                case 10: return $"관통할 때마다 탄속 +{5 + 5 * rank}% · 최대 {2 + rank}회";
                case 11: return $"앞서 관통한 적 1마리당 추가 피해 {6 * ownedCount}% → {6 * rank}% · 최대 5마리";
                case 13: return "적에게 거는 감속 +5%p";
                case 20: return $"귀환 침이 적을 밀어내는 거리 {2 * ownedCount} → {2 * rank}";
                case 21: return $"대시 종점에서 쏘는 침 {OriginalCombatRules.FormationEndpointCount(ownedCount)} → {OriginalCombatRules.FormationEndpointCount(rank)}발";
                case 22: return $"서로 다른 진의 침이 충돌하면 침 피해의 {UnityEngine.Mathf.RoundToInt(100 * OriginalCombatRules.FormationCollisionDamage(rank))}%로 폭발";
                case 23: return $"침술진 1회 추가 발동 확률 {UnityEngine.Mathf.RoundToInt(100 * OriginalCombatRules.FormationRepeatChance(ownedCount))}% → {UnityEngine.Mathf.RoundToInt(100 * OriginalCombatRules.FormationRepeatChance(rank))}%";
                case 32: return $"거리에 따른 최대 피해 보너스의 {rank * 10}%를 발사 즉시 획득";
                case 36: return ownedCount == 0 ? "침이 앞뒤 2방향에서 앞뒤·좌우 4방향으로 발사" : full;
                case 37: return $"번갈아 강화되는 방향의 침 피해 +{UnityEngine.Mathf.RoundToInt(100 * OriginalCombatRules.AlternatingBonus(rank))}%";
                case 38: return $"반대 극성으로 낙인을 터뜨리면 침 피해의 {40 + 20 * ownedCount}% 추가";
                case 47: return $"처치 시 3중첩 초과분당 추가 경험치 구슬 확률 +{5 + 5 * rank}%p";
                case 50: return $"발아 처치 시 회복 장판 유지 {1f + .5f * rank:0.#}초";
                case 51: return $"화상 중첩당 지속 피해 보너스 +{12 * ownedCount}% → +{12 * rank}%";
                case 52: return $"화상 지속 {3f * (1f + .12f * ownedCount):0.##}초 → {3f * (1f + .12f * rank):0.##}초";
                case 53: return $"최대 화상 중첩 {3 + ownedCount} → {3 + rank}";
                case 54: return "냉기 1중첩당 감속 +5%p";
                case 55: return ownedCount == 0 ? "빙결이 깨지면 폭발 · 쇄빙 피해 +15%" : "쇄빙 폭발 피해 +15%";
                case 56: return ownedCount == 0 ? "빙결이 깨진 자리에 냉기 장판 1.5초 생성" : full;
                case 57: return $"공격 속도 보너스 +{OriginalCombatRules.WindSpeedBonusPercent(ownedCount)}% → +{OriginalCombatRules.WindSpeedBonusPercent(rank)}%";
                case 58: return $"침 피해 보너스 +{OriginalCombatRules.WindDamageBonusPercent(ownedCount)}% → +{OriginalCombatRules.WindDamageBonusPercent(rank)}%";
                case 59: return $"질풍침 추가 발사 수 {ownedCount} → {rank}발";
                default:
                    // Use the catalog's signed per-selection change; technical notes stay in the run book.
                    int note = full.IndexOf(" (", StringComparison.Ordinal);
                    return note >= 0 ? full.Substring(0, note) : full;
            }
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
