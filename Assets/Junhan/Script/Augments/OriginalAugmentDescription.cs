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

        public static string Current(int index, int count)
        {
            switch(index)
            {
                case 10: return $"관통당 최초 탄속의 {5+count*5}% 가속 · 최대 {2+count}회 · 상한 +{(5+count*5)*(2+count)}%";
                case 11: return $"이전 관통 대상당 피해 +{count*6}% · 최대 5중첩 · 상한 +{count*30}%";
                case 29: return $"최대 부식 중첩 {3+count}";
                case 32: return $"거리 피해 상한의 {count*10}%를 발사 시 예열 · 기존 피해 상한 유지";
                case 33: return $"표식 지속시간 +{count*20}%";
                case 44: return $"최대 공복 중첩 {8+count}";
                case 45: return $"최대 장내균 중첩 {5+count} · 기본 유지시간 6초";
                case 46: return $"적중 시 추가 중첩 1개 확률 {count*13}% · 최대 중첩 준수";
                case 47: return $"처치 시 3중첩 초과분당 추가 구슬 확률 {5+count*5}%p · 최대 100%\n기본 1개 + 추가 최대 1개";
                case 53: return $"최대 화상 중첩 {3+count} · 가장 오래된 중첩 갱신";
                default: return Ver4AugmentCatalog.OriginalDescriptions[index];
            }
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
                    ? "관통마다 최초 탄속의 10% 가속\n서로 다른 적 최대 3회 · 상한 +30%"
                    : ownedCount == 1 ? "관통당 가속 10% → 15% · 최대 3 → 4회 · 상한 +60%" : "관통당 가속 15% → 20% · 최대 4 → 5회 · 상한 +100%";
                case 11: return $"이전 관통 대상당 추가 피해 {ownedCount*6}% → {(ownedCount+1)*6}% · 최대 5중첩 · 상한 +{(ownedCount+1)*30}%";
                case 29: return $"최대 부식 중첩 {3+ownedCount} → {4+ownedCount}";
                case 32: return $"발사 시 거리 피해 상한의 {ownedCount*10}% → {(ownedCount+1)*10}% 예열 · 거리 증가분과 합산 · 기존 피해 상한 유지";
                case 33: return $"표식 지속시간 +{ownedCount*20}% → +{(ownedCount+1)*20}%";
                case 44: return $"최대 공복 중첩 {8+ownedCount} → {9+ownedCount}";
                case 45: return $"최대 장내균 중첩 {5+ownedCount} → {6+ownedCount} · 기본 유지시간 6초 유지";
                case 47: return $"처치 시 3중첩 초과분당 추가 구슬 확률 {(ownedCount==0 ? 0 : 5+ownedCount*5)}% → {10+ownedCount*5}% · 확률 상한 100% · 기본 1개 + 추가 최대 1개";
                case 20: return first ? "귀환 침 적중 시 적을 바깥으로 밀침\n밀치기 거리 약 2"
                    : ownedCount == 1 ? "밀치기 거리 약 2 → 4" : "밀치기 거리 약 4 → 6";
                case 21: return first ? "대쉬가 끝나는 위치에도 침술진 생성 · 침 3발"
                    : ownedCount == 1 ? "종점 침술진 발사 수 3발 → 5발" : "종점 침술진 발사 수 5발 → 7발";
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
                case 53: return $"최대 화상 중첩 {3+ownedCount} → {4+ownedCount} · 상한 도달 시 가장 오래된 중첩 갱신";
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
