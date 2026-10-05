using System;

namespace Vampire
{
    // Selection count belongs to this effect, not its weapon or lifetime unlock history.
    // Previewing/rerolling a card never consumes its first-selection explanation.
    public static class OriginalAugmentDescription
    {
        public static string Card(int index, int ownedCount, int parentLevel)
        {
            return Ver4AugmentCatalog.OriginalNames[index] + "\n" + Effect(index, ownedCount) +
                $"\n이 항목 {ownedCount} → {ownedCount + 1}/3\n오리지널 Lv.{parentLevel} → {parentLevel + 1}/9";
        }

        public static string Effect(int index, int ownedCount)
        {
            if (index < 0 || index >= Ver4AugmentCatalog.OriginalNames.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (ownedCount < 0 || ownedCount >= OriginalAugmentProgress.MaxEffectSelections)
                throw new ArgumentOutOfRangeException(nameof(ownedCount));
            bool first = ownedCount == 0;
            switch (index)
            {
                case 10: return first
                    ? "관통마다 최초 탄속의 10% 가속\n서로 다른 적 최대 5회 · 상한 +50%"
                    : ownedCount == 1 ? "관통당 가속 10% → 15% · 상한 +75%" : "관통당 가속 15% → 20% · 상한 +100%";
                case 20: return first ? "귀환 침 적중 시 적을 바깥으로 밀침\n밀치기 거리 약 2"
                    : ownedCount == 1 ? "밀치기 거리 약 2 → 4" : "밀치기 거리 약 4 → 6";
                case 21: return first ? "대쉬가 끝나는 위치에도 침술진 생성"
                    : ownedCount == 1 ? "모든 침술진 피해 +25%" : "모든 침술진 피해 +25% (총 +50%)";
                case 22: return first ? "서로 다른 진의 침이 충돌하면 폭발\n반경 1 · 침 피해의 100%"
                    : ownedCount == 1 ? "충돌 폭발 피해 100% → 125%" : "충돌 폭발 피해 125% → 150%";
                case 23: return first ? "20% 확률로 0.2초 뒤 침술진 1회 추가\n추가 발동은 연쇄하지 않음"
                    : ownedCount == 1 ? "추가 발동 확률 20% → 31%" : "추가 발동 확률 31% → 40%";
                case 36: return first ? "앞뒤에 더해 좌우로도 침 발사"
                    : ownedCount == 1 ? "앞뒤 침 피해 +25%" : "좌우 침 피해 +25%";
                case 37: return first ? "발사마다 앞뒤 강화 방향 교대 · 피해 +20%\n좌우 발사에도 각각 적용"
                    : ownedCount == 1 ? "강화 방향 추가 피해 20% → 35%" : "강화 방향 추가 피해 35% → 50%";
                case 38: return first ? "음·양 낙인 4초 · 반대 극성 적중 시 소모\n침 피해의 40% 추가 피해"
                    : ownedCount == 1 ? "낙인 추가 피해 40% → 60%" : "낙인 추가 피해 60% → 80%";
                case 50: return first ? "발아로 처치하면 회복 장판 생성 · 1.5초"
                    : ownedCount == 1 ? "회복 장판 유지시간 1.5초 → 2초" : "회복 장판 유지시간 2초 → 2.5초";
                case 53: return ownedCount == 2 ? "화상 최대 중첩 상한 해제"
                    : first ? "최대 화상 중첩 +1\n3회 강화 시 중첩 상한 해제" : "최대 화상 중첩 +1";
                case 54: return first ? "냉기 스택당 감속 +5%p\n4번째 적중 확정 빙결 유지" : "냉기 스택당 감속 +5%p";
                case 55: return first ? "쇄빙 폭발 개방 · 폭발 피해 +15%" : "쇄빙 폭발 피해 +15%";
                case 56: return first ? "쇄빙 위치에 냉기 장판 생성 · 1.5초"
                    : ownedCount == 1 ? "냉기 장판 유지시간 1.5초 → 2초" : "냉기 장판 유지시간 2초 → 2.5초";
                default:
                    // Existing purely numeric originals already describe only their upgrade.
                    return Ver4AugmentCatalog.OriginalDescriptions[index];
            }
        }
    }
}
