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
        [Range(0,1)] public float miniBossLegendaryChestChance = 1f;
        [Tooltip("임시: 제산 거품 폭주 생존 완료 보상. 다른 이벤트는 자동 지급하지 않음.")]
        public bool antacidCompletionLegendaryChest = true;
        [Range(1,3)] public int legendaryChoiceCount = 3;
        public bool legendaryReroll = false;
        public ChestBlueprint legendaryChest;
        public Sprite[] plannedIcons = new Sprite[5];
        [Tooltip("평범/희귀/영웅/전설/지존. 순서: 피해, 치확(%p), 치피, 공속, 탄속, 넉백, 사거리, 크기, 발사 수.")]
        public NumericRow[] numeric = {
            new NumericRow(.08f,.05f,.10f,.05f,.05f,.06f,.04f,.05f,1),
            new NumericRow(.12f,.10f,.18f,.08f,.08f,.10f,.07f,.08f,1),
            new NumericRow(.16f,.15f,.25f,.12f,.12f,.15f,.10f,.12f,2),
            new NumericRow(.24f,.20f,.40f,.18f,.18f,.23f,.15f,.18f,3),
            new NumericRow(.36f,.25f,.60f,.27f,.27f,.35f,.23f,.27f,4)
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
        public static readonly string[] ParentNames = {"독침","다이너마이트침","유도침","관통침","꿀침","모기침","침귀환","침술진","섬유침","부식침","압력침","표식침","양극침","소화액낭침","공복침","장내균침","목침","화염침","빙결침","질풍침","진동침"};
        public static readonly string[] OriginalNames = {"독성 증폭","잔류 독성","맹독 순환","폭발 확장","폭약 증량","폭심 집중","추적 시야","유도 선회","추적 정밀도","관통 연장","연속 투척","관통 가속","당분 점착","고농도 꿀막","끈적한 발밑","흡혈 증폭","흡혈 보호","생명 비축","귀환 강타","빠른 회수","귀환 추격","진법 확장","진법 응축","진법 사거리","섬유 연장","섬유 자극","섬유 확장","부식 잔류","부식 농축","부식 축적","압력 기울기","압력 한계","압력 잔향","표식 유지","표식 파열","연속 표식","후방 증침","양극 균형","후방 연장","낭액 잔류","낭액 확산","낭액 농축","공복 지속","공복 가속","공복 한계","균총 잔류","균총 증식","균총 수확","가지 증식","뿌리 성장","생명 장판","화상 농축","잔불 유지","화상 축적","냉기 침투","쇄빙 파열","잔류 냉기","질풍 연사","풍압 응축","바람 분기","진동 확장","진동 증폭","진동 반발"};
        public static readonly string[] OriginalDescriptions = {"독 틱 피해량 +12%","독 지속시간 +12%","독 피해 간격 -8%","폭발 반경 +15%","폭발 피해량 +15%","폭발 중심부 피해량 +15%","유도 탐지 범위 +14%","유도 회전 속도 +6%","유도 중인 대상에 대한 침 적중 피해량 +8%","추가 관통 횟수 +1회","공격 속도 +5%","관통할 때마다 침 피해량 +6%","감속 지속시간 +12%","감속 효과 +5%","감속 상태 적이 받는 피해량 +8%","흡혈 회복량 +5%","받는 피해량 -5%","최대 체력 +20","귀환하는 침에 적중한 적에게 추가 피해 +20%","귀환 속도 +12%","귀환하는 침에 적중한 적을 튕겨냄(거리 2 정도로 플레이어 반대 방향으로 날려보낸다.)","침술진에서 발사하는 침 수 +2개","침술진 발사 침의 피해량 +12%","침술진 발사 침의 사거리 +12%","궤적 섬유의 유지시간 +18%","섬유의 초당 피해량 +15%","섬유 선의 폭 +12%","부식 중첩 지속시간 +12%","중첩당 받는 피해 증가 효과 +10% (효과 크기 상대 증가)","최대 부식 중첩 수 +1","비행 거리당 피해 증가 효과 +12% (효과 크기 상대 증가)","거리 피해 증가 상한 +10% (상한값 상대 증가)","거리 피해 증가 상한에 도달한 침의 적중 넉백 +15%","표식 지속시간 +12%","표식 소모 시 추가 피해 효과 +12% (효과 크기 상대 증가)","표식 소모 직후 같은 적에게 표식을 다시 부여할 확률 +10%p","후방 발사 침 수 +1개","양극 발사로 생성된 전방·후방 침 피해량 +8%","후방 발사 침의 사거리 +12%","소화액 웅덩이 유지시간 +12%","소화액 웅덩이 반경 +12%","소화액 웅덩이 초당 피해량 +12%","적중 시 부여하는 공격 속도 버프 지속시간 +12%","중첩당 공격 속도 증가 효과 +10% (효과 크기 상대 증가)","공격 속도 버프의 최대 중첩 수 +1","장내균 중첩 유지시간 +12%","침 적중 시 추가 중첩 1개를 부여할 확률 +13%p","경험치 추가 드롭 조건을 충족한 적 처치 시 추가 구슬 1개를 더 얻을 확률 +10%p","씨앗 발아 시 가지 추가타 수 +1개","발아 가지의 피해량 +12%","발아 처치 시 회복 장판 생성. 1·2·3회 획득 시 유지시간 1.5·2.0·2.5초","화상 1중첩당 지속 피해량 +12%","화상 지속시간 +12%","최대 화상 중첩 수 +1. 이 항목 3회 완료 시 중첩 상한 해제","빙결 부여 확률 +5%p","쇄빙 폭발 피해량 +15%. 이 항목 최초 획득 시 쇄빙 폭발 개방","쇄빙 위치에 냉기 장판 개방. 1·2·3회 획득 시 장판 유지시간 1.5·2.0·2.5초","질풍침의 공격 속도 +8%","질풍침 피해량 +10%","질풍침 투사체 수 +1개","충격파 반경 +12%","충격파 피해량 +12%","충격파 넉백 +15%"};
        public static readonly string[] NumericNames = { "침 피해량", "치명타 확률", "치명타 피해량", "공격 속도", "투사체 속도", "넉백", "사거리", "투사체 크기", "투사체 수" };
        public static string NumericDescription(int stat,float amount) =>
            NumericNames[stat] + " +" + (stat==8 ? amount.ToString("0")+"개" : (amount*100).ToString("0.##")+(stat==1 ? "%p" : "%")) + " (공유 침)";
    }
}
