using UnityEngine;
namespace Vampire
{
    [CreateAssetMenu(menuName = "24투/Ver4 Augment Balance")]
    public sealed class Ver4AugmentBalance : ScriptableObject
    {
        public AugmentUpgradeOdds odds = new AugmentUpgradeOdds();
        [Tooltip("임시: 신규 특수 본체 후보와 강화 후보가 모두 있을 때 본체 획득 카드 확률.")]
        [Range(0,1)] public float newSpecialChance = .25f;
        [Tooltip("임시: 미니보스 전설 전용 상자 드롭 확률.")]
        [Range(0,1)] public float miniBossLegendaryChestChance = 0f;
        [Tooltip("임시: 제산 거품 폭주 생존 완료 보상. 다른 이벤트는 자동 지급하지 않음.")]
        public bool antacidCompletionLegendaryChest = false;
        [Range(1,3)] public int legendaryChoiceCount = 3;
        public bool legendaryReroll = false;
        public ChestBlueprint legendaryChest;
        public Sprite[] plannedIcons = new Sprite[5];
        [Tooltip("평범/희귀/영웅/전설/지존. 순서: 피해, 치확(%p), 치피, 공속, 탄속, 넉백, 사거리, 크기, 발사 수.")]
        public NumericRow[] numeric = {
            new NumericRow(.08f,.05f,.10f,.05f,.05f,.06f,.04f,.05f,1),
            new NumericRow(.11f,.09f,.17f,.075f,.08f,.10f,.07f,.08f,1),
            new NumericRow(.15f,.14f,.23f,.11f,.12f,.15f,.10f,.12f,2),
            new NumericRow(.23f,.19f,.38f,.17f,.18f,.23f,.15f,.18f,3),
            new NumericRow(.34f,.23f,.57f,.25f,.27f,.35f,.23f,.27f,4)
        };
        [System.Serializable] public sealed class NumericRow
        {
            public float damage, criticalChance, criticalDamage, attackSpeed, projectileSpeed, knockback, range, size;
            public int projectiles;
            public NumericRow(float d,float c,float cd,float a,float p,float k,float r,float s,int count)
            { damage=d; criticalChance=c; criticalDamage=cd; attackSpeed=a; projectileSpeed=p; knockback=k; range=r; size=s; projectiles=count; }
            public float Value(int stat)
            {
                switch(stat) { case 0:return damage; case 1:return criticalChance; case 2:return criticalDamage;
                    case 3:return attackSpeed; case 4:return projectileSpeed; case 5:return knockback;
                    case 6:return range; case 7:return size; case 8:return projectiles;
                    default:throw new System.ArgumentOutOfRangeException(nameof(stat)); }
            }
        }
        public float NumericValue(AugmentUpgradeGrade grade,int stat)
        {
            if(!AugmentUpgradeOdds.IsNumeric(grade)) throw new System.ArgumentException("Original is not a numeric grade");
            int index=(int)grade; if(index>3) index--;
            return numeric[index].Value(stat);
        }
    }
    public static class Ver4AugmentCatalog
    {
        public static readonly string[] ParentNames = {"독침","다이너마이트침","유도침","관통침","꿀침","모기침","침귀환","침술진","섬유침","부식침","압력침","표식침","양극침","소화액낭침","공복침","장내균침","목침","화염침","빙결침","바람침","진동침"};
        public static readonly string[] OriginalNames = {"독성 증폭","잔류 독성","맹독 순환","폭발 확장","폭약 증량","폭심 집중","추적 시야","유도 선회","추적 정밀도","관통 연장","관통 가속","관통 강타","당분 점착","고농도 꿀막","끈적한 발밑","흡혈 증폭","흡혈 보호","생명 비축","귀환 강타","빠른 회수","귀환 추격","종점 개진","침진 충돌","연속 개진","섬유 연장","섬유 자극","섬유 확장","부식 잔류","부식 농축","부식 축적","압력 기울기","압력 한계","압력 예열","표식 유지","표식 파열","연속 표식","십자 발사","교대 강타","반극 낙인","낭액 잔류","낭액 확산","낭액 농축","공복 지속","공복 가속","공복 한계","균총 한계","균총 증식","균총 수확","가지 증식","뿌리 성장","생명 장판","화상 농축","잔불 유지","화상 축적","냉기 침투","쇄빙 파열","잔류 냉기","질풍 연사","풍압 응축","바람 분기","진동 확장","진동 증폭","진동 반발"};
        public static readonly string[] OriginalDescriptions = {"독 틱 피해량 +12%","독 지속시간 +12%","독 피해 간격 -8%","폭발 반경 +15%","폭발 피해량 +15%","폭발 중심부 피해량 +15%","유도 탐지 범위 +14%","유도 회전 속도 +6%","유도 중인 대상에 대한 침 적중 피해량 +8%","추가 관통 횟수 +1회","관통할 때마다 최초 탄속의 10·15·20% 가속. 서로 다른 적 최대 3·4·5회, 상한 +30·60·100%","이전에 관통한 적마다 침 피해량 +6·12·18%. 최대 5중첩, 상한 +30·60·90%","감속 지속시간 +12%","감속 효과 +5%","감속 상태 적이 받는 피해량 +8%","흡혈 회복량 +5%","받는 피해량 -5%","최대 체력 +20","귀환하는 침에 적중한 적에게 추가 피해 +20%","귀환 속도 +12%","귀환하는 침에 적중한 적을 튕겨냄(거리 2 정도로 플레이어 반대 방향으로 날려보낸다.)","대쉬 종점에도 침술진 생성. 발사 수 3·5·7발","서로 다른 진의 침이 충돌하면 반경 1 폭발. 침 1발 피해의 100·125·150%. 같은 진끼리는 충돌하지 않음","침술진이 20·31·40% 확률로 0.2초 뒤 1회 더 발동. 추가 발동은 연쇄하지 않음","궤적 섬유의 유지시간 +18%","섬유의 초당 피해량 +15%","섬유 선의 폭 +12%","부식 중첩 지속시간 +12%","중첩당 받는 피해 증가 효과 +10% (효과 크기 상대 증가)","최대 부식 중첩 수 +1","비행 거리당 피해 증가 효과 +12% (효과 크기 상대 증가)","거리 피해 증가 상한 +10% (상한값 상대 증가)","발사 시 거리 피해 보너스 상한의 10·20·30%를 미리 얻음. 거리 증가분과 합산하며 기존 상한 유지","표식 지속시간 +20%","표식 소모 시 추가 피해 효과 +12% (효과 크기 상대 증가)","표식 소모 직후 같은 적에게 표식을 다시 부여할 확률 +10%p","1회: 좌우 발사 추가. 2회: 앞뒤 침 피해 +25%. 3회: 좌우 침 피해 +25%","발사 묶음마다 앞뒤 강화 방향 교대. 강화 방향 피해 +20·35·50%. 좌우도 같은 규칙 적용","4초간 음·양 낙인 부여. 반대 극성 적중 시 소모하여 침 피해의 40·60·80% 추가 피해","소화액 웅덩이 유지시간 +12%","소화액 웅덩이 반경 +12%","소화액 웅덩이 초당 피해량 +12%","적중 시 부여하는 공격 속도 버프 지속시간 +12%","중첩당 공격 속도 증가 효과 +10% (효과 크기 상대 증가)","공격 속도 버프의 최대 중첩 수 +1","장내균 최대 중첩 5 → 6·7·8. 기본 유지시간 6초","침 적중 시 추가 중첩 1개를 부여할 확률 +13%p","처치 시 3중첩을 초과한 중첩당 추가 경험치 구슬 확률 10·15·20%p (최대 100%). 기본 1개 + 추가 최대 1개","씨앗 발아 시 가지 추가타 수 +1개","발아 가지의 피해량 +12%","발아 처치 시 회복 장판 생성. 1·2·3회 획득 시 유지시간 1.5·2.0·2.5초","화상 1중첩당 지속 피해량 +12%","화상 지속시간 +12%","최대 화상 중첩 3 → 4·5·6. 상한 도달 시 가장 오래된 중첩 갱신","냉기 스택당 감속 효과 +5%p (4번째 적중 확정 빙결 유지)","쇄빙 폭발 피해량 +15%. 이 항목 최초 획득 시 쇄빙 폭발 개방","쇄빙 위치에 냉기 장판 개방. 1·2·3회 획득 시 장판 유지시간 1.5·2.0·2.5초","질풍침의 공격 속도 +8%","질풍침 피해량 +10%","질풍침 투사체 수 +1개","충격파 반경 +12%","충격파 피해량 +12%","충격파 넉백 +15%"};
        public static readonly string[] NumericNames = { "침 피해량", "치명타 확률", "치명타 피해량", "공격 속도", "투사체 속도", "넉백", "사거리", "투사체 크기", "투사체 수" };
        public static string NumericDescription(int stat,float amount) =>
            NumericNames[stat] + " +" + (stat==8 ? amount.ToString("0")+"개" : (amount*100).ToString("0.##")+(stat==1 ? "%p" : "%")) + " (공유 침)";
    }
}
