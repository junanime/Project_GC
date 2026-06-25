using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vampire
{
    public class SyringeDartAbility : ProjectileAbility
    {
        [Header("Syringe Dart Stats")]
        [SerializeField] protected UpgradeableProjectileCount projectileCount;

        [Tooltip("침 여러 발을 연속으로 쏠 때 각 침 사이의 발사 간격입니다.")]
        [SerializeField] protected float syringeDelay = 0.08f;

        [Header("Syringe Range")]
        [Tooltip("기본 침 최대 사거리입니다. Shuriken.prefab의 Max Distance 대신 이 값을 기준으로 사용합니다.")]
        [SerializeField] private float baseSyringeMaxDistance = 6f;

        [Header("Spread Settings")]
        [Tooltip("여러 발 발사 시 각 침 사이의 기본 각도입니다.")]
        [SerializeField] private float angleBetweenProjectiles = 8f;

        [Tooltip("여러 발 발사 시 전체 부채꼴이 최대로 벌어질 수 있는 각도입니다.")]
        [SerializeField] private float maxTotalSpreadAngle = 120f;

        [Header("Active Special Augments")]
        [Tooltip("독침 특수증강을 테스트용으로 강제 활성화합니다.")]
        [SerializeField] private bool poisonEnabled = false;

        [Tooltip("섬유침 활성화 여부입니다. 침이 지나간 자리에 피해 선을 남깁니다.")]
        [SerializeField] private bool fiberNeedleEnabled = false;

        [Tooltip("부식침 활성화 여부입니다. 적에게 받는 피해 증가 스택을 부여합니다.")]
        [SerializeField] private bool corrosionNeedleEnabled = false;

        [Tooltip("압력침 활성화 여부입니다. 침이 멀리 날아갈수록 피해가 증가합니다.")]
        [SerializeField] private bool pressureNeedleEnabled = false;

        [Tooltip("표식침 활성화 여부입니다. 첫 피격 시 표식, 다음 피격 시 추가 피해를 줍니다.")]
        [SerializeField] private bool markNeedleEnabled = false;
        [Tooltip("양극침 활성화 여부입니다. 침을 전방과 후방 180도 대칭 방향으로 나누어 발사합니다.")]
        [SerializeField] private bool bipolarNeedleEnabled = false;
        [Tooltip("소화액낭침 활성화 여부입니다. 침에 맞은 적이 죽으면 소화액 웅덩이를 생성합니다.")]
        [SerializeField] private bool digestiveAcidSacNeedleEnabled = false;

        [Tooltip("공복침 활성화 여부입니다. 침 적중 시 일정 시간 공격속도 스택을 얻습니다.")]
        [SerializeField] private bool hungerNeedleEnabled = false;

        [Tooltip("장내균침 활성화 여부입니다. 침 적중 시 장내균 스택을 쌓고, 일정 스택 이상에서 사망 시 추가 경험치를 생성합니다.")]
        [SerializeField] private bool gutBacteriaNeedleEnabled = false;
        [Tooltip("폭발침 특수증강을 테스트용으로 강제 활성화합니다.")]
        [SerializeField] private bool explosionEnabled = false;

        [Tooltip("유도침 특수증강을 테스트용으로 강제 활성화합니다.")]
        [SerializeField] private bool homingEnabled = false;

        [Tooltip("관통침 특수증강을 테스트용으로 강제 활성화합니다.")]
        [SerializeField] private bool pierceEnabled = false;

        [Tooltip("꿀침 특수증강을 테스트용으로 강제 활성화합니다.")]
        [SerializeField] private bool honeyEnabled = false;

        [Tooltip("모기침 특수증강을 테스트용으로 강제 활성화합니다.")]
        [SerializeField] private bool mosquitoEnabled = false;

        [Tooltip("침귀환 특수증강을 테스트용으로 강제 활성화합니다.")]
        [SerializeField] private bool returnNeedleEnabled = false;

        [Tooltip("침술진 특수증강을 테스트용으로 강제 활성화합니다. 플레이 시작 전에 체크하면 Init 시 대쉬 추가와 컨트롤러 생성까지 자동 적용됩니다.")]
        [SerializeField] private bool acupunctureFormationEnabled = false;

        [Header("Active Legendary Augments")]
        [Tooltip("생명연소 전설증강을 테스트용으로 강제 활성화합니다.")]
        [SerializeField] private bool lifeBurnEnabled = false;

        [Tooltip("분신배양 전설증강을 테스트용으로 보유 처리합니다. 실제 분신 생성은 분신배양 전설증강/컨트롤러 쪽 구현을 따릅니다.")]
        [SerializeField] private bool cloneLegendaryTaken = false;
        [Tooltip("신경차단 전설증강 활성화 여부입니다. 일정 주기로 화면 안 적을 정지시킵니다.")]
        [SerializeField] private bool neuralBlockEnabled = false;

        [Tooltip("독 전염 전설증강 활성화 여부입니다. 독에 걸린 적 처치 시 주변 적에게 독을 전염시킵니다.")]
        [SerializeField] private bool poisonContagionEnabled = false;

        [Tooltip("장기압착 전설증강 활성화 여부입니다. 일정 주기로 적 밀집 구역에 압착장을 생성합니다.")]
        [SerializeField] private bool organCompressionEnabled = false;
        [Tooltip("고슴도침 전설증강을 테스트용으로 강제 활성화합니다. 플레이 시작 전에 체크하면 Init 시 침 결계 컨트롤러까지 자동 생성됩니다.")]
        [SerializeField] private bool hedgehogNeedleEnabled = false;

        [Tooltip("대물침 전설증강을 테스트용으로 강제 활성화합니다.")]
        [SerializeField] private bool heavySnipeEnabled = false;

        [Tooltip("이기어침 전설증강을 테스트용으로 강제 활성화합니다. 플레이 시작 전에 체크하면 Init 시 조종 침 컨트롤러까지 자동 생성됩니다.")]
        [SerializeField] private bool cursorControlEnabled = false;

        [Header("Poison Settings")]
        [SerializeField] private float poisonDuration = 3f;
        [SerializeField] private float poisonTickInterval = 0.5f;
        [SerializeField] private float poisonTickDamage = 2f;

        [Header("Explosion Settings")]
        [SerializeField] private float explosionRadius = 1.5f;
        [SerializeField] private float explosionDamage = 2f;

        [Tooltip("폭발침 특수증강을 먹었을 때 폭발이 발생할 확률입니다. 1이면 100% 확률로 폭발합니다.")]
        [SerializeField, Range(0f, 1f)] private float specialExplosionChance = 1f;

        [Header("Homing Settings")]
        [SerializeField] private float homingRange = 6f;
        [SerializeField] private float homingLerpSpeed = 8f;
        [Header("Fiber Needle / 섬유침 Settings")]
        [Tooltip("섬유침이 남긴 선이 유지되는 시간입니다.")]
        [SerializeField] private float fiberTrailLifetime = 2f;

        [Tooltip("섬유침 선 위에 있는 적이 1초마다 받는 피해입니다.")]
        [SerializeField] private float fiberTrailDamagePerSecond = 2f;

        [Tooltip("섬유침 피해가 들어가는 간격입니다. 0.5이면 0.5초마다 damagePerSecond의 절반 피해가 들어갑니다.")]
        [SerializeField] private float fiberTrailTickInterval = 0.5f;

        [Tooltip("섬유침 선의 두께입니다.")]
        [SerializeField] private float fiberTrailWidth = 0.12f;

        [Tooltip("침이 이 거리 이상 이동할 때마다 섬유 선분을 하나 생성합니다. 낮을수록 선이 촘촘하지만 오브젝트가 많아집니다.")]
        [SerializeField] private float fiberTrailMinSegmentDistance = 0.25f;

        [Tooltip("섬유침 선의 색상입니다.")]
        [SerializeField] private Color fiberTrailColor = new Color(0.75f, 1f, 0.95f, 0.75f);
        [Header("Digestive Acid Sac Needle / 소화액낭침 Settings")]
        [Tooltip("소화액낭침 대상이 죽었을 때 생성되는 소화액 웅덩이 유지 시간입니다.")]
        [SerializeField] private float digestiveAcidPuddleLifetime = 3f;

        [Tooltip("소화액 웅덩이 반경입니다.")]
        [SerializeField] private float digestiveAcidPuddleRadius = 1.2f;

        [Tooltip("소화액 웅덩이 위 적이 1초마다 받는 피해입니다.")]
        [SerializeField] private float digestiveAcidPuddleDamagePerSecond = 2f;

        [Tooltip("소화액 웅덩이 피해 간격입니다. 0.5이면 0.5초마다 damagePerSecond의 절반 피해가 들어갑니다.")]
        [SerializeField] private float digestiveAcidPuddleTickInterval = 0.5f;

        [Tooltip("소화액 웅덩이 색상입니다.")]
        [SerializeField] private Color digestiveAcidPuddleColor = new Color(0.6f, 1f, 0.15f, 0.75f);

        [Header("Hunger Needle / 공복침 Settings")]
        [Tooltip("공복침 적중 스택 지속 시간입니다.")]
        [SerializeField] private float hungerStackDuration = 3f;

        [Tooltip("공복침 1스택당 공격속도 증가량입니다. 0.04이면 1스택당 4% 증가입니다.")]
        [SerializeField] private float hungerAttackSpeedBonusPerStack = 0.04f;

        [Tooltip("공복침 최대 중첩 수입니다.")]
        [SerializeField] private int hungerMaxStacks = 8;

        [Tooltip("체크하면 공복침 스택 로그를 출력합니다.")]
        [SerializeField] private bool debugHungerNeedle = false;

        [Header("Gut Bacteria Needle / 장내균침 Settings")]
        [Tooltip("장내균침 스택 지속 시간입니다.")]
        [SerializeField] private float gutBacteriaStackDuration = 6f;

        [Tooltip("추가 경험치가 생성되기 위해 필요한 장내균 스택 수입니다.")]
        [SerializeField] private int gutBacteriaRequiredStacks = 3;

        [Tooltip("장내균 최대 중첩 수입니다.")]
        [SerializeField] private int gutBacteriaMaxStacks = 5;

        [Tooltip("조건 달성 후 몬스터 사망 시 추가 생성되는 경험치 구슬 개수입니다.")]
        [SerializeField] private int gutBacteriaBonusGemCount = 1;

        [Tooltip("추가 생성되는 경험치 구슬 종류입니다. White1=1, Blue2=2, Green10=10, Red50=50 경험치입니다.")]
        [SerializeField] private GemType gutBacteriaBonusGemType = GemType.White1;

        [Tooltip("추가 경험치 구슬이 사망 위치 주변에 흩어지는 반경입니다.")]
        [SerializeField] private float gutBacteriaBonusGemSpawnRadius = 0.35f;

        [Tooltip("체크하면 장내균침 스택/보상 로그를 출력합니다.")]
        [SerializeField] private bool debugGutBacteria = false;
        [Header("Corrosion Needle / 부식침 Settings")]
        [Tooltip("부식침 스택 지속 시간입니다.")]
        [SerializeField] private float corrosionDuration = 3f;

        [Tooltip("일반 몬스터에게 부식 1스택당 적용되는 받는 피해 증가량입니다. 0.15이면 15%입니다.")]
        [SerializeField] private float corrosionDamageTakenBonusPerStack = 0.15f;

        [Tooltip("보스 몬스터에게 부식 1스택당 적용되는 받는 피해 증가량입니다. 0.05이면 5%입니다.")]
        [SerializeField] private float corrosionBossDamageTakenBonusPerStack = 0.05f;

        [Tooltip("부식 최대 중첩 수입니다.")]
        [SerializeField] private int corrosionMaxStacks = 3;

        [Header("Pressure Needle / 압력침 Settings")]
        [Tooltip("침이 1유닛 이동할 때마다 증가하는 피해량입니다. 0.08이면 거리 1당 8% 증가합니다.")]
        [SerializeField] private float pressureDamageBonusPerDistance = 0.08f;

        [Tooltip("압력침 최대 피해 증가량입니다. 0.5이면 최대 50% 증가입니다.")]
        [SerializeField] private float pressureMaxDamageBonus = 0.5f;

        [Header("Mark Needle / 표식침 Settings")]
        [Tooltip("표식 지속 시간입니다.")]
        [SerializeField] private float markDuration = 4f;

        [Tooltip("표식이 있는 적을 다시 맞혔을 때 추가로 더해지는 피해 배율입니다. 0.5이면 현재 피해의 50%가 추가됩니다.")]
        [SerializeField] private float markBonusDamageMultiplier = 0.5f;
        [Header("Bipolar Needle / 양극침 Settings")]
        [Tooltip("양극침 획득 시 추가되는 발사체 개수입니다. 기본값 1이면 현재 총 침 개수에 +1이 적용됩니다.")]
        [SerializeField] private int bipolarNeedleBonusProjectileCount = 1;

        [Tooltip("반대 방향 침이 몇 도 뒤쪽으로 발사될지 정합니다. 180이면 완전한 전후방 대칭입니다.")]
        [SerializeField] private float bipolarNeedleBackAngleOffset = 180f;

        [Tooltip("총 발사체 수가 홀수일 때 남는 1발을 전방에 줄지 여부입니다. 체크하면 전방이 1발 더 많아집니다.")]
        [SerializeField] private bool bipolarNeedleFrontGetsExtraProjectile = true;

        [Tooltip("체크하면 양극침 발사 로그를 출력합니다.")]
        [SerializeField] private bool debugBipolarNeedle = false;
        [Header("Pierce Settings")]
        [Tooltip("관통침 기본 관통 횟수. 2라면 첫 적중 이후 추가로 2번 더 관통 가능.")]
        [SerializeField] private int pierceCount = 2;

        [Header("Honey Needle Settings")]
        [Tooltip("꿀침 둔화 지속 시간")]
        [SerializeField] private float honeyDuration = 2.5f;

        [Tooltip("0.6이면 몬스터의 체감 이동속도가 약 60% 수준으로 감소합니다. 1이면 둔화 없음.")]
        [SerializeField] private float honeySlowMultiplier = 0.6f;

        [Header("Mosquito Needle Settings")]
        [Tooltip("모기침 적중 1회당 회복량")]
        [SerializeField] private float mosquitoHealPerHit = 1f;

        [Tooltip("대상 이름/컴포넌트 이름에 Boss가 들어가면 회복량에 곱해지는 배율")]
        [SerializeField] private float mosquitoBossHealMultiplier = 2f;

        [Header("Return Needle / 침귀환 Settings")]
        [Tooltip("귀환 중 침 속도 배율")]
        [SerializeField] private float returnNeedleSpeedMultiplier = 1.25f;

        [Tooltip("귀환 경로에서 주는 피해 배율. 0.7이면 현재 침 데미지의 70%")]
        [SerializeField] private float returnNeedleDamageMultiplier = 0.7f;

        [Tooltip("플레이어와 이 거리 이하로 가까워지면 귀환 완료로 판단하고 사라집니다.")]
        [SerializeField] private float returnNeedleArriveDistance = 0.45f;

        [Tooltip("귀환 상태로 유지될 수 있는 최대 시간")]
        [SerializeField] private float returnNeedleMaxDuration = 2.5f;

        [Header("Acupuncture Formation / 침술진 Settings")]
        [Tooltip("침술진 획득 시 추가되는 대쉬 횟수")]
        [SerializeField] private int acupunctureFormationBonusDashCharges = 1;

        [Tooltip("대쉬 시작 위치에 생성되는 분신 지속 시간")]
        [SerializeField] private float acupunctureFormationLifetime = 0.35f;

        [Tooltip("분신이 360도로 발사하는 침 개수")]
        [SerializeField] private int acupunctureFormationNeedleCount = 12;

        [Tooltip("분신이 발사하는 침의 데미지 배율. 0.55면 현재 침 데미지의 55%")]
        [SerializeField] private float acupunctureFormationDamageMultiplier = 0.55f;

        [Tooltip("분신 시각 크기")]
        [SerializeField] private float acupunctureFormationVisualScale = 1f;

        private AcupunctureFormationController acupunctureFormationController;
        private bool acupunctureFormationBonusDashApplied = false;

        [Header("Legendary - Life Burn / HP 1")]
        [SerializeField] private float lifeBurnDamageMultiplier = 3f;
        [SerializeField] private int lifeBurnBonusProjectiles = 10;
        [SerializeField] private float lifeBurnBonusRange = 3f;

        [Header("Legendary - Hedgehog Needle / 고슴도침")]

        [Tooltip("플레이어 주변을 도는 침 개수")]
        [SerializeField] private int hedgehogNeedleCount = 8;

        [Tooltip("플레이어와 회전 침 사이 거리. 이 거리가 실드 접촉 판정 범위가 됩니다.")]
        [SerializeField] private float hedgehogOrbitRadius = 1.25f;

        [Tooltip("회전 속도")]
        [SerializeField] private float hedgehogRotationSpeed = 180f;

        [Tooltip("적이 실드에 닿았을 때 같은 대상에게 다시 반격 침을 발사할 수 있기까지의 시간")]
        [SerializeField] private float hedgehogTouchFireCooldown = 0.35f;

        [Tooltip("고슴도침이 발사하는 반격 침의 데미지 배율. 0.45면 기본 침 데미지의 45%")]
        [SerializeField] private float hedgehogDamageMultiplier = 0.45f;

        private HedgehogNeedleController hedgehogNeedleController;

        [Header("Legendary - Heavy Snipe / 대물침")]
        [Tooltip("true면 우클릭으로 차지, false면 좌클릭으로 차지합니다. 마우스가 없으면 Space 키로 차지합니다.")]
        [SerializeField] private bool useRightMouseForHeavySnipe = true;

        [Tooltip("100% 풀차지까지 걸리는 시간")]
        [SerializeField] private float heavyMaxChargeTime = 1.5f;

        [Tooltip("체크하면 대물침이 100% 충전되는 순간 자동으로 발사됩니다. 끄면 버튼을 떼기 전까지 최대 충전 상태를 유지합니다.")]
        [SerializeField] private bool heavyFireAutomaticallyOnFullCharge = true;

        [Header("Heavy Snipe Damage By Charge")]
        [Tooltip("0% 차지 피해 배율. 1이면 현재 데미지 100%")]
        [SerializeField] private float heavyDamageAt0 = 1f;

        [Tooltip("30% 차지 피해 배율. 1.3이면 현재 데미지 130%")]
        [SerializeField] private float heavyDamageAt30 = 1.3f;

        [Tooltip("50% 차지 피해 배율. 1.7이면 현재 데미지 170%")]
        [SerializeField] private float heavyDamageAt50 = 1.7f;

        [Tooltip("70% 차지 피해 배율. 2.0이면 현재 데미지 200%")]
        [SerializeField] private float heavyDamageAt70 = 2f;

        [Tooltip("100% 차지 피해 배율. 3.0이면 현재 데미지 300%")]
        [SerializeField] private float heavyDamageAt100 = 3f;

        [Header("Heavy Snipe Speed By Charge")]
        [Tooltip("0% 차지 대물침 속도 배율. 기본 침 속도에 곱해집니다. 기본침/다른 증강에는 영향 없음.")]
        [SerializeField] private float heavySpeedAt0 = 1.1f;

        [Tooltip("30% 차지 대물침 속도 배율. 기본 침 속도에 곱해집니다.")]
        [SerializeField] private float heavySpeedAt30 = 1.35f;

        [Tooltip("50% 차지 대물침 속도 배율. 기본 침 속도에 곱해집니다.")]
        [SerializeField] private float heavySpeedAt50 = 1.65f;

        [Tooltip("70% 차지 대물침 속도 배율. 기본 침 속도에 곱해집니다.")]
        [SerializeField] private float heavySpeedAt70 = 2.0f;

        [Tooltip("100% 차지 대물침 속도 배율. 기본 침 속도에 곱해집니다.")]
        [SerializeField] private float heavySpeedAt100 = 2.5f;

        [Header("Heavy Snipe Pierce By Charge")]
        [SerializeField] private int heavyPierceAt0 = 0;
        [SerializeField] private int heavyPierceAt30 = 5;
        [SerializeField] private int heavyPierceAt50 = 10;
        [SerializeField] private int heavyPierceAt70 = 20;

        [Tooltip("100% 직전까지 사용할 최대 관통 수. 100% 풀차지 순간에는 이 값을 쓰지 않고 무제한 관통 처리합니다.")]
        [SerializeField] private int heavyPierceAt99 = 40;

        [Header("Heavy Snipe Size By Charge")]
        [Tooltip("0% 차지 침 크기")]
        [SerializeField] private float heavySizeAt0 = 1f;

        [Tooltip("30% 차지 침 크기")]
        [SerializeField] private float heavySizeAt30 = 1.25f;

        [Tooltip("50% 차지 침 크기")]
        [SerializeField] private float heavySizeAt50 = 1.55f;

        [Tooltip("70% 차지 침 크기")]
        [SerializeField] private float heavySizeAt70 = 1.85f;

        [Tooltip("100% 차지 침 크기")]
        [SerializeField] private float heavySizeAt100 = 2.4f;

        [Header("Heavy Snipe Range By Charge")]
        [Tooltip("0% 차지 추가 사거리")]
        [SerializeField] private float heavyRangeBonusAt0 = 0f;

        [Tooltip("30% 차지 추가 사거리")]
        [SerializeField] private float heavyRangeBonusAt30 = 1.5f;

        [Tooltip("50% 차지 추가 사거리")]
        [SerializeField] private float heavyRangeBonusAt50 = 3f;

        [Tooltip("70% 차지 추가 사거리")]
        [SerializeField] private float heavyRangeBonusAt70 = 5f;

        [Tooltip("100% 차지 추가 사거리")]
        [SerializeField] private float heavyRangeBonusAt100 = 8f;

        [Header("Heavy Snipe Knockback")]
        [SerializeField] private float heavyKnockbackMultiplierAt0 = 1f;
        [SerializeField] private float heavyKnockbackMultiplierAt100 = 2f;

        [Header("Heavy Snipe Charge Preview / 대물침 차징 표시")]
        [Tooltip("체크하면 대물침 차징 중 캐릭터 앞에 커지는 침 이미지를 표시합니다.")]
        [SerializeField] private bool showHeavyChargePreview = true;

        [Tooltip("차징 중 보여줄 침 스프라이트입니다. 비워두면 Projectile Prefab 안의 SpriteRenderer 스프라이트를 자동으로 사용합니다.")]
        [SerializeField] private Sprite heavyChargePreviewSprite;

        [Tooltip("차징 미리보기 침의 회전 보정 각도입니다. 현재 침 이미지가 왼쪽을 향한 원본이면 180부터 테스트하세요.")]
        [SerializeField] private float heavyChargePreviewAngleOffset = 180f;

        [Tooltip("차징 미리보기 침의 전체 크기 배율입니다. 1이면 실제 발사체 시각 크기와 최대한 맞춥니다.")]
        [SerializeField] private float heavyChargePreviewScaleMultiplier = 1f;

        [Tooltip("체크하면 Projectile Prefab 내부 SpriteRenderer의 자식 위치/회전/스케일까지 복사해서 차징 프리뷰 크기를 실제 발사체와 맞춥니다.")]
        [SerializeField] private bool heavyChargePreviewMatchProjectileVisualTransform = true;

        [Tooltip("차징 시작 시 침이 플레이어 중심 기준 조준 방향 앞쪽에 얼마나 떨어져서 나타날지입니다.")]
        [SerializeField] private float heavyChargePreviewStartForwardOffset = 1.1f;

        [Tooltip("차징이 진행될수록 침이 조준 반대 방향으로 얼마나 뒤로 당겨질지입니다. 활시위를 당기는 느낌을 조절합니다.")]
        [SerializeField] private float heavyChargePreviewPullBackDistance = 0.75f;

        [Tooltip("조준 방향 기준 좌우 보정값입니다. 보통 0으로 두면 됩니다.")]
        [SerializeField] private float heavyChargePreviewSideOffset = 0f;

        [Tooltip("차징 중 침 주변에 붙일 VFX 프리팹입니다. 비워두면 VFX 없이 침 이미지만 표시됩니다.")]
        [SerializeField] private GameObject heavyChargePreviewVfxPrefab;

        [Tooltip("차징 미리보기 침의 투명도입니다.")]
        [SerializeField, Range(0f, 1f)] private float heavyChargePreviewAlpha = 0.9f;

        [Tooltip("차징 미리보기 침이 기존 투사체보다 앞에 보이도록 더해줄 Sorting Order 값입니다.")]
        [SerializeField] private int heavyChargePreviewSortingOrderBonus = 30;

        [Header("Heavy Snipe Debug")]
        [SerializeField] private bool debugHeavySnipe = true;

        private bool isHeavyCharging = false;
        private float heavyChargeTimer = 0f;
        private GameObject heavyChargePreviewObject;
        private Transform heavyChargePreviewVisualTransform;
        private SpriteRenderer heavyChargePreviewRenderer;
        private GameObject heavyChargePreviewVfxObject;

        // 이기어침 + 대물침 조합에서 사용하는 현재 차지율
        private float cursorHeavyChargeRatio = 0f;
        [Header("Neural Block / 신경차단 Settings")]
        [Tooltip("신경차단 발동 주기입니다.")]
        [SerializeField] private float neuralBlockInterval = 12f;

        [Tooltip("신경차단으로 몬스터가 멈추는 시간입니다.")]
        [SerializeField] private float neuralBlockFreezeDuration = 1f;

        [Tooltip("화면 가장자리 밖 몬스터까지 살짝 포함할 여유값입니다.")]
        [SerializeField] private float neuralBlockScreenPadding = 0.08f;

        [Tooltip("체크하면 신경차단 로그를 출력합니다.")]
        [SerializeField] private bool debugNeuralBlock = false;

        [Header("Poison Contagion / 독 전염 Settings")]
        [Tooltip("독 전염 범위입니다. 1이면 처치된 몬스터 기준 약 1칸 범위입니다.")]
        [SerializeField] private float poisonContagionRadius = 1f;

        [Tooltip("전염된 독의 지속시간 배율입니다. 1이면 원래 독 지속시간과 같습니다.")]
        [SerializeField] private float poisonContagionDurationMultiplier = 1f;

        [Tooltip("전염된 독의 피해 배율입니다. 1이면 원래 독 피해와 같습니다.")]
        [SerializeField] private float poisonContagionDamageMultiplier = 1f;

        [Tooltip("체크하면 독 전염 로그를 출력합니다.")]
        [SerializeField] private bool debugPoisonContagion = false;

        [Header("Organ Compression / 장기압착 Settings")]
        [Tooltip("장기압착장 생성 주기입니다.")]
        [SerializeField] private float organCompressionInterval = 20f;

        [Tooltip("적 밀집 구역을 계산할 때 사용할 반경입니다.")]
        [SerializeField] private float organCompressionClusterSearchRadius = 2.5f;

        [Tooltip("장기압착장의 실제 피해/흡입 반경입니다.")]
        [SerializeField] private float organCompressionFieldRadius = 2f;

        [Tooltip("장기압착장 유지 시간입니다.")]
        [SerializeField] private float organCompressionFieldDuration = 4f;

        [Tooltip("장기압착장 피해 간격입니다.")]
        [SerializeField] private float organCompressionDamageTickInterval = 0.5f;

        [Tooltip("장기압착장 1틱당 피해량입니다.")]
        [SerializeField] private float organCompressionDamagePerTick = 3f;

        [Tooltip("장기압착장이 적을 중앙으로 끌어당기는 속도입니다.")]
        [SerializeField] private float organCompressionPullSpeed = 2.5f;

        [Tooltip("화면 가장자리 밖 몬스터까지 살짝 포함할 여유값입니다.")]
        [SerializeField] private float organCompressionScreenPadding = 0.08f;

        [Tooltip("체크하면 장기압착 로그를 출력합니다.")]
        [SerializeField] private bool debugOrganCompression = false;
        private NeuralBlockController neuralBlockController;
        private OrganCompressionController organCompressionController;

        [Header("Legendary - Cursor Controlled Needle / 이기어침")]
        [Tooltip("마우스 포인트를 따라가는 속도. 높을수록 더 즉각적으로 따라갑니다.")]
        [SerializeField] private float cursorNeedleFollowSpeed = 8f;

        [Tooltip("이기어침 피해 판정 반경")]
        [SerializeField] private float cursorNeedleHitRadius = 0.45f;

        [Tooltip("이기어침 피해 배율. 1이면 현재 침 데미지 100%")]
        [SerializeField] private float cursorNeedleDamageMultiplier = 1f;

        [Tooltip("같은 적에게 다시 피해를 줄 수 있기까지의 시간")]
        [SerializeField] private float cursorNeedleDamageInterval = 0.25f;

        [Tooltip("마우스를 따라다니는 이기어침 시각 크기")]
        [SerializeField] private float cursorNeedleVisualScale = 1.2f;

        [Tooltip("유도침을 보유 중일 때 이기어침의 피해 판정 반경 증가량")]
        [SerializeField] private float cursorNeedleHomingHitRadiusBonus = 0.35f;

        [Header("Cursor Needle Back Display / 이기어침 등 뒤 전시")]
        [Tooltip("플레이어 중심 기준 등 뒤 전시 위치")]
        [SerializeField] private Vector2 cursorNeedleBackDisplayOffset = new Vector2(0f, 0.9f);

        [Tooltip("등 뒤에 전시되는 침 사이 간격")]
        [SerializeField] private float cursorNeedleBackDisplaySpacing = 0.35f;

        [Tooltip("등 뒤에 전시되는 침의 아치 높이")]
        [SerializeField] private float cursorNeedleBackDisplayArcHeight = 0.25f;

        [Tooltip("등 뒤에 전시되는 침 크기")]
        [SerializeField] private float cursorNeedleBackDisplayScale = 0.8f;

        private CursorControlledNeedleController cursorControlledNeedleController;

        public GameObject ProjectilePrefab => projectilePrefab;
        public LayerMask MonsterLayer => monsterLayer;

        public override void Init(AbilityManager abilityManager, EntityManager entityManager, Character playerCharacter)
        {
            base.Init(abilityManager, entityManager, playerCharacter);

            // 인스펙터에서 체크해둔 테스트용 증강 중,
            // 컨트롤러 생성이나 대쉬 횟수 추가가 필요한 증강들을 실제 런타임 상태로 동기화합니다.
            ApplyInspectorForcedAugmentRuntimeSetup();
        }

        private void ApplyInspectorForcedAugmentRuntimeSetup()
        {
            if (acupunctureFormationEnabled)
            {
                EnableAcupunctureFormationAugment();
            }

            if (hedgehogNeedleEnabled)
            {
                EnableHedgehogNeedleLegendary();
            }

            if (cursorControlEnabled)
            {
                EnableCursorControlLegendary();
            }
            if (neuralBlockEnabled)
            {
                EnableNeuralBlockLegendary();
            }

            if (poisonContagionEnabled)
            {
                EnablePoisonContagionLegendary();
            }

            if (organCompressionEnabled)
            {
                EnableOrganCompressionLegendary();
            }
        }

        protected override void Update()
        {
            ApplyInspectorForcedAugmentRuntimeSetup();

            // 이기어침이 활성화되면 기본 자동 공격은 멈춘다.
            // 단, 대물침도 같이 보유 중이면 우클릭 차지로 이기어침 자체를 강화한다.
            if (cursorControlEnabled)
            {
                HandleCursorControlModeUpdate();
                return;
            }

            // 대물침만 보유한 상태:
            // - 평소에는 기존 기본 자동 공격 유지
            // - 우클릭을 누르거나 차징 중일 때만 기본 공격을 멈추고 대물침 차지 처리
            if (heavySnipeEnabled)
            {
                bool commandHeld = IsHeavySnipeCommandHeld();

                if (commandHeld || isHeavyCharging)
                {
                    HandleHeavySnipeUpdate();
                    return;
                }

                HideHeavySnipeChargePreview();
                base.Update();
                return;
            }

            HideHeavySnipeChargePreview();
            base.Update();
        }

        private void OnDisable()
        {
            DestroyHeavySnipeChargePreview();
        }

        protected override void Attack()
        {
            StartCoroutine(LaunchSyringes());
        }

        protected IEnumerator LaunchSyringes()
        {
            int totalProjectileCount = GetEffectiveProjectileCount();

            Vector2 baseDirection = playerCharacter.LookDirection;

            if (baseDirection == Vector2.zero)
            {
                baseDirection = Vector2.right;
            }

            if (bipolarNeedleEnabled)
            {
                yield return LaunchBipolarSyringes(baseDirection, totalProjectileCount);
                yield break;
            }

            timeSinceLastAttack -= totalProjectileCount * syringeDelay;

            for (int i = 0; i < totalProjectileCount; i++)
            {
                Vector2 spreadDirection = GetSpreadDirection(baseDirection, i, totalProjectileCount);
                LaunchSyringeProjectile(spreadDirection);

                yield return new WaitForSeconds(syringeDelay);
            }
        }
        private IEnumerator LaunchBipolarSyringes(Vector2 baseDirection, int totalProjectileCount)
        {
            if (baseDirection == Vector2.zero)
            {
                baseDirection = Vector2.right;
            }

            baseDirection.Normalize();

            totalProjectileCount = Mathf.Max(1, totalProjectileCount);

            int frontCount;
            int backCount;

            if (bipolarNeedleFrontGetsExtraProjectile)
            {
                frontCount = Mathf.CeilToInt(totalProjectileCount * 0.5f);
                backCount = totalProjectileCount - frontCount;
            }
            else
            {
                backCount = Mathf.CeilToInt(totalProjectileCount * 0.5f);
                frontCount = totalProjectileCount - backCount;
            }

            // 최소 2발 이상일 때는 반드시 전방/후방에 1발씩은 배치한다.
            if (totalProjectileCount >= 2)
            {
                frontCount = Mathf.Max(1, frontCount);
                backCount = Mathf.Max(1, backCount);
            }

            Vector2 backDirection = RotateVector(baseDirection, bipolarNeedleBackAngleOffset);

            int pairCount = Mathf.Max(frontCount, backCount);

            // 기존 한 방향 연사와 전체 쿨타임 감각이 크게 달라지지 않도록
            // 실제 발사체 총량 기준으로 시간 보정을 유지한다.
            timeSinceLastAttack -= totalProjectileCount * syringeDelay;

            if (debugBipolarNeedle)
            {
                Debug.Log(
                    $"[양극침] 발사 | Total={totalProjectileCount} | " +
                    $"Front={frontCount} | Back={backCount} | " +
                    $"BackAngle={bipolarNeedleBackAngleOffset}",
                    this
                );
            }

            for (int i = 0; i < pairCount; i++)
            {
                if (i < frontCount)
                {
                    Vector2 frontSpreadDirection = GetSpreadDirection(baseDirection, i, frontCount);
                    LaunchSyringeProjectile(frontSpreadDirection);
                }

                if (i < backCount)
                {
                    Vector2 backSpreadDirection = GetSpreadDirection(backDirection, i, backCount);
                    LaunchSyringeProjectile(backSpreadDirection);
                }

                yield return new WaitForSeconds(syringeDelay);
            }
        }
        private void LaunchSyringeProjectile(Vector2 direction)
        {
            Vector2 spawnPosition = GetProjectileSpawnPosition(direction);

            Projectile projectile = entityManager.SpawnProjectile(
                projectileIndex,
                spawnPosition,
                GetEffectiveDamage(),
                GetEffectiveKnockback(),
                GetEffectiveSpeed(),
                monsterLayer
            );

            if (projectile == null)
            {
                return;
            }

            if (playerCharacter != null)
            {
                projectile.transform.localScale = Vector3.one * GetPlayerProjectileSizeMultiplier();

                // Shuriken.prefab의 Max Distance를 곱해서 쓰지 않고,
                // SyringeDartAbility의 baseSyringeMaxDistance를 기준으로 명확하게 세팅한다.
                projectile.maxDistance = GetEffectiveSyringeMaxDistance();
            }

            if (projectile is SyringeProjectile syringeProjectile)
            {
                syringeProjectile.ConfigureSpecials(BuildSpecialRuntime());
            }
            else
            {
                Debug.LogWarning(
                    $"[SyringeDartAbility] Spawned projectile is '{projectile.GetType().Name}', not 'SyringeProjectile'. " +
                    "Projectile Prefab 연결을 다시 확인하세요."
                );
            }

            projectile.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);
            projectile.Launch(direction);
        }

        private void HandleCursorControlModeUpdate()
        {
            // 이기어침만 있을 때는 기본 공격도, 대물침 차지도 하지 않는다.
            if (!heavySnipeEnabled)
            {
                cursorHeavyChargeRatio = 0f;
                isHeavyCharging = false;
                heavyChargeTimer = 0f;
                HideHeavySnipeChargePreview();
                return;
            }

            bool commandHeld = IsHeavySnipeCommandHeld();

            if (commandHeld)
            {
                if (!isHeavyCharging)
                {
                    isHeavyCharging = true;
                    heavyChargeTimer = 0f;

                    if (debugHeavySnipe)
                    {
                        Debug.Log("[이기어침+대물침] 이기어침 충전 시작");
                    }
                }

                heavyChargeTimer += Time.deltaTime;
                cursorHeavyChargeRatio = Mathf.Clamp01(heavyChargeTimer / Mathf.Max(0.01f, heavyMaxChargeTime));

                // 풀차지 이후에도 누르고 있으면 100% 상태 유지
                if (cursorHeavyChargeRatio >= 1f)
                {
                    heavyChargeTimer = heavyMaxChargeTime;
                    cursorHeavyChargeRatio = 1f;
                }

                UpdateHeavySnipeChargePreview(cursorHeavyChargeRatio, GetAimDirectionFromMouseOrLookDirection());
            }
            else
            {
                if (isHeavyCharging && debugHeavySnipe)
                {
                    HeavySnipeChargeStats releaseStats = CalculateHeavySnipeChargeStats(cursorHeavyChargeRatio);

                    Debug.Log(
                        $"[이기어침+대물침] 충전 해제 | " +
                        $"차지 {cursorHeavyChargeRatio * 100f:0}% | " +
                        $"데미지 {releaseStats.damageMultiplier * 100f:0}% | " +
                        $"크기 x{releaseStats.sizeMultiplier:0.00}"
                    );
                }

                isHeavyCharging = false;
                heavyChargeTimer = 0f;
                cursorHeavyChargeRatio = 0f;
                HideHeavySnipeChargePreview();
            }
        }

        private void HandleHeavySnipeUpdate()
        {
            timeSinceLastAttack += Time.deltaTime;

            bool commandHeld = IsHeavySnipeCommandHeld();
            float effectiveCooldown = GetEffectiveCooldown();

            // 아직 차징 중이 아닐 때
            if (!isHeavyCharging)
            {
                // 우클릭을 누르고 있고, 공격 쿨타임이 준비됐을 때만 차징 시작
                if (commandHeld && timeSinceLastAttack >= effectiveCooldown)
                {
                    isHeavyCharging = true;
                    heavyChargeTimer = 0f;

                    if (debugHeavySnipe)
                    {
                        Debug.Log("[대물침] 차지 시작 - 기본 공격 정지");
                    }
                }

                // 우클릭을 누르고 있지만 쿨타임이 아직이면 기본 공격도 하지 않고 대기
                // 우클릭을 안 누른 상태는 Update()에서 base.Update()로 빠지므로 여기로 오지 않음
                return;
            }

            // 차징 중
            heavyChargeTimer += Time.deltaTime;

            float chargeRatio = Mathf.Clamp01(heavyChargeTimer / Mathf.Max(0.01f, heavyMaxChargeTime));
            bool reachedFullCharge = heavyChargeTimer >= heavyMaxChargeTime;

            if (reachedFullCharge)
            {
                heavyChargeTimer = heavyMaxChargeTime;
                chargeRatio = 1f;
            }

            UpdateHeavySnipeChargePreview(chargeRatio, GetAimDirectionFromMouseOrLookDirection());

            bool shouldFire = !commandHeld || (heavyFireAutomaticallyOnFullCharge && reachedFullCharge);

            if (shouldFire)
            {
                float finalChargeRatio = reachedFullCharge ? 1f : chargeRatio;

                FireHeavySnipe(finalChargeRatio);

                timeSinceLastAttack = 0f;
                isHeavyCharging = false;
                heavyChargeTimer = 0f;
                HideHeavySnipeChargePreview();

                if (debugHeavySnipe)
                {
                    Debug.Log(
                        $"[대물침] 발사 완료 | " +
                        $"차지 {finalChargeRatio * 100f:0}% - 기본 공격 재개"
                    );
                }
            }
        }

        private bool IsHeavySnipeCommandHeld()
        {
            if (Mouse.current != null)
            {
                if (useRightMouseForHeavySnipe)
                {
                    return Mouse.current.rightButton.isPressed;
                }

                return Mouse.current.leftButton.isPressed;
            }

            if (Keyboard.current != null)
            {
                return Keyboard.current.spaceKey.isPressed;
            }

            return false;
        }

        private void FireHeavySnipe(float chargeRatio)
        {
            if (playerCharacter == null || entityManager == null)
            {
                return;
            }

            HeavySnipeChargeStats stats = CalculateHeavySnipeChargeStats(chargeRatio);
            Vector2 aimDirection = GetAimDirectionFromMouseOrLookDirection();

            SyringeSpecialRuntime runtime = BuildHeavySnipeRuntime(stats);

            Vector2 spawnPosition = GetHeavySnipeChargePreviewPosition(chargeRatio, aimDirection);

            Projectile projectile = entityManager.SpawnProjectile(
                projectileIndex,
                spawnPosition,
                GetEffectiveDamage() * stats.damageMultiplier,
                GetEffectiveKnockback() * stats.knockbackMultiplier,
                GetEffectiveSpeed() * stats.speedMultiplier,
                monsterLayer
            );

            if (projectile == null)
            {
                return;
            }

            projectile.transform.localScale =
                Vector3.one *
                GetPlayerProjectileSizeMultiplier() *
                stats.sizeMultiplier;

            // 대물침도 기본 사거리는 SyringeDartAbility에서 관리하고,
            // 차지 사거리 보너스는 runtime.rangeBonus로 SyringeProjectile에서 더해진다.
            projectile.maxDistance = GetEffectiveSyringeMaxDistance();

            if (projectile is SyringeProjectile syringeProjectile)
            {
                syringeProjectile.ConfigureSpecials(runtime);
            }

            projectile.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);
            projectile.Launch(aimDirection);

            if (debugHeavySnipe)
            {
                Debug.Log(
                    $"[대물침] 발사 | " +
                    $"차지 {chargeRatio * 100f:0}% | " +
                    $"속도 배율 x{stats.speedMultiplier:0.00} | " +
                    $"크기 x{stats.sizeMultiplier:0.00} | " +
                    $"유도 {(runtime.homingEnabled ? "켜짐" : "꺼짐")} | " +
                    $"관통 {(stats.unlimitedPierce ? "무제한" : stats.pierceCount.ToString())}"
                );
            }
        }

        private SyringeSpecialRuntime BuildHeavySnipeRuntime(HeavySnipeChargeStats stats)
        {
            SyringeSpecialRuntime runtime = BuildSpecialRuntime();

            // 대물침은 전설 차지샷이므로 침귀환과 무관하게 기존 차지샷으로 작동한다.
            runtime.returnNeedleEnabled = false;
            runtime.returnNeedleSpeedMultiplier = 0f;
            runtime.returnNeedleDamageMultiplier = 0f;
            runtime.returnNeedleArriveDistance = 0f;
            runtime.returnNeedleMaxDuration = 0f;

            // 핵심 수정:
            // 유도침을 보유하고 있어도 대물침은 발사 시점의 조준 방향 그대로 직선 비행한다.
            runtime.homingEnabled = false;
            runtime.homingRange = 0f;
            runtime.homingLerpSpeed = 0f;

            runtime.pierceEnabled = true;
            runtime.pierceCount = stats.unlimitedPierce ? int.MaxValue : stats.pierceCount;
            runtime.rangeBonus += stats.rangeBonus;

            return runtime;
        }

        private Vector2 GetAimDirectionFromMouseOrLookDirection()
        {
            if (Mouse.current != null && Camera.main != null && playerCharacter != null)
            {
                Vector3 mouseScreenPosition = Mouse.current.position.ReadValue();
                Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
                mouseWorldPosition.z = playerCharacter.CenterTransform.position.z;

                Vector2 direction = (Vector2)mouseWorldPosition - (Vector2)playerCharacter.CenterTransform.position;

                if (direction.sqrMagnitude > 0.0001f)
                {
                    return direction.normalized;
                }
            }

            if (playerCharacter != null && playerCharacter.LookDirection != Vector2.zero)
            {
                return playerCharacter.LookDirection.normalized;
            }

            return Vector2.right;
        }

        private HeavySnipeChargeStats CalculateHeavySnipeChargeStats(float chargeRatio)
        {
            chargeRatio = Mathf.Clamp01(chargeRatio);

            HeavySnipeChargeStats stats = new HeavySnipeChargeStats();

            stats.damageMultiplier = EvaluateChargeFloat(
                chargeRatio,
                heavyDamageAt0,
                heavyDamageAt30,
                heavyDamageAt50,
                heavyDamageAt70,
                heavyDamageAt100
            );

            stats.speedMultiplier = EvaluateChargeFloat(
                chargeRatio,
                heavySpeedAt0,
                heavySpeedAt30,
                heavySpeedAt50,
                heavySpeedAt70,
                heavySpeedAt100
            );

            stats.sizeMultiplier = EvaluateChargeFloat(
                chargeRatio,
                heavySizeAt0,
                heavySizeAt30,
                heavySizeAt50,
                heavySizeAt70,
                heavySizeAt100
            );

            stats.rangeBonus = EvaluateChargeFloat(
                chargeRatio,
                heavyRangeBonusAt0,
                heavyRangeBonusAt30,
                heavyRangeBonusAt50,
                heavyRangeBonusAt70,
                heavyRangeBonusAt100
            );

            stats.knockbackMultiplier = Mathf.Lerp(
                heavyKnockbackMultiplierAt0,
                heavyKnockbackMultiplierAt100,
                chargeRatio
            );

            if (chargeRatio >= 0.999f)
            {
                stats.unlimitedPierce = true;
                stats.pierceCount = int.MaxValue;
            }
            else
            {
                stats.unlimitedPierce = false;
                stats.pierceCount = EvaluateChargeInt(
                    chargeRatio,
                    heavyPierceAt0,
                    heavyPierceAt30,
                    heavyPierceAt50,
                    heavyPierceAt70,
                    heavyPierceAt99
                );
            }

            return stats;
        }

        private void UpdateHeavySnipeChargePreview(float chargeRatio, Vector2 aimDirection)
        {
            if (!showHeavyChargePreview || playerCharacter == null)
            {
                HideHeavySnipeChargePreview();
                return;
            }

            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                aimDirection = Vector2.right;
            }

            aimDirection.Normalize();

            EnsureHeavySnipeChargePreview();

            if (heavyChargePreviewObject == null || heavyChargePreviewRenderer == null)
            {
                return;
            }

            HeavySnipeChargeStats stats = CalculateHeavySnipeChargeStats(chargeRatio);

            Vector2 previewPosition = GetHeavySnipeChargePreviewPosition(chargeRatio, aimDirection);
            heavyChargePreviewObject.transform.position = previewPosition;

            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            heavyChargePreviewObject.transform.rotation =
                Quaternion.Euler(0f, 0f, angle + heavyChargePreviewAngleOffset);

            float previewScale =
                GetPlayerProjectileSizeMultiplier() *
                stats.sizeMultiplier *
                Mathf.Max(0.01f, heavyChargePreviewScaleMultiplier);

            heavyChargePreviewObject.transform.localScale = Vector3.one * previewScale;

            Color color = heavyChargePreviewRenderer.color;
            color.a = heavyChargePreviewAlpha;
            heavyChargePreviewRenderer.color = color;

            if (!heavyChargePreviewObject.activeSelf)
            {
                heavyChargePreviewObject.SetActive(true);
            }
        }

        private Vector2 GetHeavySnipeChargePreviewPosition(float chargeRatio, Vector2 aimDirection)
        {
            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                aimDirection = Vector2.right;
            }

            aimDirection.Normalize();

            Vector2 basePosition = GetPlayerCenterPosition();
            float pullRatio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(chargeRatio));

            float forwardDistance =
                heavyChargePreviewStartForwardOffset -
                heavyChargePreviewPullBackDistance * pullRatio;

            Vector2 sideDirection = new Vector2(-aimDirection.y, aimDirection.x);

            return
                basePosition +
                aimDirection * forwardDistance +
                sideDirection * heavyChargePreviewSideOffset;
        }

        private Vector2 GetPlayerCenterPosition()
        {
            if (playerCharacter != null && playerCharacter.CenterTransform != null)
            {
                return playerCharacter.CenterTransform.position;
            }

            if (playerCharacter != null)
            {
                return playerCharacter.transform.position;
            }

            return Vector2.zero;
        }

        private void EnsureHeavySnipeChargePreview()
        {
            if (heavyChargePreviewObject != null && heavyChargePreviewRenderer != null)
            {
                EnsureHeavyChargePreviewVfx();
                return;
            }

            DestroyHeavySnipeChargePreview();

            Sprite previewSprite = GetHeavyChargePreviewSprite();

            if (previewSprite == null)
            {
                return;
            }

            heavyChargePreviewObject = new GameObject("Heavy Snipe Charge Preview");

            GameObject visualObject = new GameObject("Visual");
            heavyChargePreviewVisualTransform = visualObject.transform;
            heavyChargePreviewVisualTransform.SetParent(heavyChargePreviewObject.transform);

            heavyChargePreviewRenderer = visualObject.AddComponent<SpriteRenderer>();
            heavyChargePreviewRenderer.sprite = previewSprite;

            SpriteRenderer sourceRenderer = GetProjectilePrefabSpriteRenderer();

            if (sourceRenderer != null)
            {
                ApplyProjectileVisualTransformToPreview(sourceRenderer);

                heavyChargePreviewRenderer.flipX = sourceRenderer.flipX;
                heavyChargePreviewRenderer.flipY = sourceRenderer.flipY;
                heavyChargePreviewRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                heavyChargePreviewRenderer.sortingOrder =
                    sourceRenderer.sortingOrder + Mathf.Max(1, heavyChargePreviewSortingOrderBonus);
                heavyChargePreviewRenderer.color = sourceRenderer.color;
            }
            else
            {
                heavyChargePreviewVisualTransform.localPosition = Vector3.zero;
                heavyChargePreviewVisualTransform.localRotation = Quaternion.identity;
                heavyChargePreviewVisualTransform.localScale = Vector3.one;

                heavyChargePreviewRenderer.sortingOrder = heavyChargePreviewSortingOrderBonus;
            }

            EnsureHeavyChargePreviewVfx();

            heavyChargePreviewObject.SetActive(false);
        }

        private void ApplyProjectileVisualTransformToPreview(SpriteRenderer sourceRenderer)
        {
            if (heavyChargePreviewVisualTransform == null)
            {
                return;
            }

            if (!heavyChargePreviewMatchProjectileVisualTransform || sourceRenderer == null || projectilePrefab == null)
            {
                heavyChargePreviewVisualTransform.localPosition = Vector3.zero;
                heavyChargePreviewVisualTransform.localRotation = Quaternion.identity;
                heavyChargePreviewVisualTransform.localScale = Vector3.one;
                return;
            }

            Transform root = projectilePrefab.transform;
            Transform source = sourceRenderer.transform;

            if (source == root)
            {
                // SpriteRenderer가 투사체 루트에 직접 붙어 있으면,
                // 실제 발사 시 projectile.transform.localScale로 덮어쓰기 때문에
                // 프리뷰의 내부 Visual은 1배율로 둔다.
                heavyChargePreviewVisualTransform.localPosition = Vector3.zero;
                heavyChargePreviewVisualTransform.localRotation = Quaternion.identity;
                heavyChargePreviewVisualTransform.localScale = Vector3.one;
                return;
            }

            heavyChargePreviewVisualTransform.localPosition = root.InverseTransformPoint(source.position);
            heavyChargePreviewVisualTransform.localRotation = Quaternion.Inverse(root.rotation) * source.rotation;
            heavyChargePreviewVisualTransform.localScale = GetRelativeScaleToRoot(source, root);
        }

        private Vector3 GetRelativeScaleToRoot(Transform child, Transform root)
        {
            if (child == null || root == null || child == root)
            {
                return Vector3.one;
            }

            Vector3 result = Vector3.one;
            Transform current = child;

            while (current != null && current != root)
            {
                result = Vector3.Scale(current.localScale, result);
                current = current.parent;
            }

            return result;
        }

        private void EnsureHeavyChargePreviewVfx()
        {
            if (heavyChargePreviewObject == null)
            {
                return;
            }

            if (heavyChargePreviewVfxPrefab == null || heavyChargePreviewVfxObject != null)
            {
                return;
            }

            heavyChargePreviewVfxObject = Instantiate(
                heavyChargePreviewVfxPrefab,
                heavyChargePreviewObject.transform
            );

            heavyChargePreviewVfxObject.transform.localPosition = Vector3.zero;
            heavyChargePreviewVfxObject.transform.localRotation = Quaternion.identity;
            heavyChargePreviewVfxObject.transform.localScale = Vector3.one;
        }

        private Sprite GetHeavyChargePreviewSprite()
        {
            if (heavyChargePreviewSprite != null)
            {
                return heavyChargePreviewSprite;
            }

            SpriteRenderer sourceRenderer = GetProjectilePrefabSpriteRenderer();

            if (sourceRenderer != null)
            {
                return sourceRenderer.sprite;
            }

            return null;
        }

        private SpriteRenderer GetProjectilePrefabSpriteRenderer()
        {
            if (projectilePrefab == null)
            {
                return null;
            }

            SpriteRenderer[] renderers = projectilePrefab.GetComponentsInChildren<SpriteRenderer>(true);

            SpriteRenderer firstRendererWithSprite = null;
            SpriteRenderer firstEnabledRenderer = null;

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];

                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                if (firstRendererWithSprite == null)
                {
                    firstRendererWithSprite = renderer;
                }

                if (renderer.enabled && firstEnabledRenderer == null)
                {
                    firstEnabledRenderer = renderer;
                }

                if (
                    renderer.gameObject.name.IndexOf("Visual", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    renderer.gameObject.name.IndexOf("Needle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    renderer.gameObject.name.IndexOf("Syringe", System.StringComparison.OrdinalIgnoreCase) >= 0
                )
                {
                    return renderer;
                }
            }

            if (firstEnabledRenderer != null)
            {
                return firstEnabledRenderer;
            }

            return firstRendererWithSprite;
        }

        private void HideHeavySnipeChargePreview()
        {
            if (heavyChargePreviewObject != null)
            {
                heavyChargePreviewObject.SetActive(false);
            }
        }

        private void DestroyHeavySnipeChargePreview()
        {
            if (heavyChargePreviewObject != null)
            {
                Destroy(heavyChargePreviewObject);
            }

            heavyChargePreviewObject = null;
            heavyChargePreviewVisualTransform = null;
            heavyChargePreviewRenderer = null;
            heavyChargePreviewVfxObject = null;
        }

        private float EvaluateChargeFloat(
            float chargeRatio,
            float valueAt0,
            float valueAt30,
            float valueAt50,
            float valueAt70,
            float valueAt100)
        {
            if (chargeRatio <= 0.3f)
            {
                return Mathf.Lerp(valueAt0, valueAt30, Mathf.InverseLerp(0f, 0.3f, chargeRatio));
            }

            if (chargeRatio <= 0.5f)
            {
                return Mathf.Lerp(valueAt30, valueAt50, Mathf.InverseLerp(0.3f, 0.5f, chargeRatio));
            }

            if (chargeRatio <= 0.7f)
            {
                return Mathf.Lerp(valueAt50, valueAt70, Mathf.InverseLerp(0.5f, 0.7f, chargeRatio));
            }

            return Mathf.Lerp(valueAt70, valueAt100, Mathf.InverseLerp(0.7f, 1f, chargeRatio));
        }

        private int EvaluateChargeInt(
            float chargeRatio,
            int valueAt0,
            int valueAt30,
            int valueAt50,
            int valueAt70,
            int valueAt99)
        {
            float evaluatedValue;

            if (chargeRatio <= 0.3f)
            {
                evaluatedValue = Mathf.Lerp(valueAt0, valueAt30, Mathf.InverseLerp(0f, 0.3f, chargeRatio));
            }
            else if (chargeRatio <= 0.5f)
            {
                evaluatedValue = Mathf.Lerp(valueAt30, valueAt50, Mathf.InverseLerp(0.3f, 0.5f, chargeRatio));
            }
            else if (chargeRatio <= 0.7f)
            {
                evaluatedValue = Mathf.Lerp(valueAt50, valueAt70, Mathf.InverseLerp(0.5f, 0.7f, chargeRatio));
            }
            else
            {
                evaluatedValue = Mathf.Lerp(valueAt70, valueAt99, Mathf.InverseLerp(0.7f, 0.999f, chargeRatio));
            }

            return Mathf.Max(0, Mathf.RoundToInt(evaluatedValue));
        }

        private SyringeSpecialRuntime BuildSpecialRuntime()
        {
            float additionalPierceFromCharacter = GetPublicFloatProperty(playerCharacter, "AdditionalPierce", 0f);
            int bonusPierce = pierceEnabled ? Mathf.RoundToInt(additionalPierceFromCharacter) : 0;
            int totalPierceCount = pierceEnabled ? Mathf.Max(0, pierceCount + bonusPierce) : 0;

            float antibioticBombChance = 0f;

            if (playerCharacter != null)
            {
                antibioticBombChance = Mathf.Max(0f, playerCharacter.AntibioticBombChance);
            }

            float finalExplosionChance = 0f;

            if (explosionEnabled)
            {
                finalExplosionChance = Mathf.Max(finalExplosionChance, specialExplosionChance);
            }

            if (antibioticBombChance > 0f)
            {
                finalExplosionChance = Mathf.Max(finalExplosionChance, antibioticBombChance);
            }

            SyringeSpecialRuntime runtime = new SyringeSpecialRuntime
            {
                slowChance = playerCharacter != null ? playerCharacter.SlowChance : 0f,
                burnChance = playerCharacter != null ? playerCharacter.BurnChance : 0f,
                thermometerEnabled = playerCharacter != null && playerCharacter.HasThermometer,
                reflectCount = playerCharacter != null ? playerCharacter.MouthwashCount : 0,

                poisonEnabled = poisonEnabled,
                poisonDuration = poisonDuration,
                poisonTickInterval = poisonTickInterval,
                poisonTickDamage = poisonTickDamage,

                explosionEnabled = finalExplosionChance > 0f,
                explosionRadius = explosionRadius,
                explosionDamage = explosionDamage,
                explosionChance = finalExplosionChance,

                homingEnabled = homingEnabled,
                homingRange = homingRange,
                homingLerpSpeed = homingLerpSpeed,

                // 관통침 증강을 먹었을 때만 기본 침이 관통한다.
                // 기본 pierceCount가 2여도 pierceEnabled가 false면 관통하지 않는다.
                pierceEnabled = pierceEnabled && totalPierceCount > 0,
                pierceCount = totalPierceCount,

                honeyEnabled = honeyEnabled,
                honeyDuration = honeyDuration,
                honeySlowMultiplier = honeySlowMultiplier,

                mosquitoEnabled = mosquitoEnabled,
                mosquitoHealPerHit = mosquitoHealPerHit,
                mosquitoBossHealMultiplier = mosquitoBossHealMultiplier,

                // 침귀환은 그대로 유지.
                // 복귀 중에는 SyringeProjectile의 IsReturnMode 로직에 의해 관통 처리된다.
                returnNeedleEnabled = returnNeedleEnabled,
                returnNeedleSpeedMultiplier = returnNeedleSpeedMultiplier,
                returnNeedleDamageMultiplier = returnNeedleDamageMultiplier,
                returnNeedleArriveDistance = returnNeedleArriveDistance,
                returnNeedleMaxDuration = returnNeedleMaxDuration,
                fiberEnabled = fiberNeedleEnabled,
                fiberTrailLifetime = fiberTrailLifetime,
                fiberTrailDamagePerSecond = fiberTrailDamagePerSecond,
                fiberTrailTickInterval = fiberTrailTickInterval,
                fiberTrailWidth = fiberTrailWidth,
                fiberTrailMinSegmentDistance = fiberTrailMinSegmentDistance,
                fiberTrailColor = fiberTrailColor,

                corrosionEnabled = corrosionNeedleEnabled,
                corrosionDuration = corrosionDuration,
                corrosionDamageTakenBonusPerStack = corrosionDamageTakenBonusPerStack,
                corrosionBossDamageTakenBonusPerStack = corrosionBossDamageTakenBonusPerStack,
                corrosionMaxStacks = corrosionMaxStacks,

                pressureEnabled = pressureNeedleEnabled,
                pressureDamageBonusPerDistance = pressureDamageBonusPerDistance,
                pressureMaxDamageBonus = pressureMaxDamageBonus,

                markEnabled = markNeedleEnabled,
                markDuration = markDuration,
                markBonusDamageMultiplier = markBonusDamageMultiplier,
                digestiveAcidSacEnabled = digestiveAcidSacNeedleEnabled,
                digestiveAcidPuddleLifetime = digestiveAcidPuddleLifetime,
                digestiveAcidPuddleRadius = digestiveAcidPuddleRadius,
                digestiveAcidPuddleDamagePerSecond = digestiveAcidPuddleDamagePerSecond,
                digestiveAcidPuddleTickInterval = digestiveAcidPuddleTickInterval,
                digestiveAcidPuddleColor = digestiveAcidPuddleColor,

                hungerNeedleEnabled = hungerNeedleEnabled,
                hungerStackDuration = hungerStackDuration,
                hungerAttackSpeedBonusPerStack = hungerAttackSpeedBonusPerStack,
                hungerMaxStacks = hungerMaxStacks,
                debugHungerNeedle = debugHungerNeedle,

                gutBacteriaEnabled = gutBacteriaNeedleEnabled,
                gutBacteriaStackDuration = gutBacteriaStackDuration,
                gutBacteriaRequiredStacks = gutBacteriaRequiredStacks,
                gutBacteriaMaxStacks = gutBacteriaMaxStacks,
                gutBacteriaBonusGemCount = gutBacteriaBonusGemCount,
                gutBacteriaBonusGemType = gutBacteriaBonusGemType,
                gutBacteriaBonusGemSpawnRadius = gutBacteriaBonusGemSpawnRadius,
                debugGutBacteria = debugGutBacteria,
                healingBlocked = lifeBurnEnabled,

                rangeBonus = lifeBurnEnabled ? lifeBurnBonusRange : 0f
            };

            return runtime;
        }

        public Vector2 GetSpreadDirection(Vector2 baseDirection, int projectileIndex, int totalCount)
        {
            if (baseDirection == Vector2.zero)
            {
                baseDirection = Vector2.right;
            }

            baseDirection.Normalize();

            if (totalCount <= 1)
            {
                return baseDirection;
            }

            float totalSpreadAngle = angleBetweenProjectiles * (totalCount - 1);
            totalSpreadAngle = Mathf.Min(totalSpreadAngle, maxTotalSpreadAngle);

            float actualAngleStep = totalSpreadAngle / (totalCount - 1);
            float startAngle = -totalSpreadAngle * 0.5f;
            float angleOffset = startAngle + (actualAngleStep * projectileIndex);

            return RotateVector(baseDirection, angleOffset);
        }

        private Vector2 RotateVector(Vector2 vector, float angleDegrees)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            return new Vector2(
                vector.x * cos - vector.y * sin,
                vector.x * sin + vector.y * cos
            ).normalized;
        }

        public float GetEffectiveDamage()
        {
            float multiplier = lifeBurnEnabled ? lifeBurnDamageMultiplier : 1f;

            if (playerCharacter != null)
            {
                multiplier *= playerCharacter.DamageMultiplier;
            }

            return damage.Value * multiplier;
        }

        public float GetEffectiveKnockback()
        {
            return knockback.Value;
        }

        public float GetEffectiveSpeed()
        {
            float multiplier = 1f;

            if (playerCharacter != null)
            {
                multiplier *= playerCharacter.ProjectileSpeedMultiplier;
            }

            return speed.Value * multiplier;
        }

        public float GetEffectiveCooldown()
        {
            float attackSpeedMultiplier = 1f;

            if (playerCharacter != null)
            {
                attackSpeedMultiplier = Mathf.Max(0.01f, playerCharacter.AttackSpeedMultiplier);
            }

            if (hungerNeedleEnabled && playerCharacter != null)
            {
                attackSpeedMultiplier *= HungerNeedleRuntime.GetAttackSpeedMultiplier(playerCharacter);
            }

            return cooldown.Value / Mathf.Max(0.01f, attackSpeedMultiplier);
        }

        public int GetEffectiveProjectileCount()
        {
            int totalCount = projectileCount.Value;

            if (playerCharacter != null)
            {
                totalCount += playerCharacter.AdditionalProjectiles;
            }

            if (lifeBurnEnabled)
            {
                totalCount += lifeBurnBonusProjectiles;
            }

            if (bipolarNeedleEnabled)
            {
                totalCount += Mathf.Max(0, bipolarNeedleBonusProjectileCount);
            }

            return Mathf.Max(1, totalCount);
        }

        public float GetEffectiveSyringeMaxDistance()
        {
            return Mathf.Max(0.1f, baseSyringeMaxDistance) * GetPlayerRangeMultiplier();
        }

        public SyringeSpecialRuntime GetCurrentSpecialRuntime()
        {
            return BuildSpecialRuntime();
        }

        public int GetActiveSpecialAugmentCount()
        {
            int count = 0;

            if (poisonEnabled) count++;
            if (explosionEnabled) count++;
            if (homingEnabled) count++;
            if (pierceEnabled) count++;
            if (honeyEnabled) count++;
            if (mosquitoEnabled) count++;
            if (returnNeedleEnabled) count++;
            if (acupunctureFormationEnabled) count++;
            if (fiberNeedleEnabled) count++;
            if (corrosionNeedleEnabled) count++;
            if (pressureNeedleEnabled) count++;
            if (markNeedleEnabled) count++;
            if (bipolarNeedleEnabled) count++;
            if (digestiveAcidSacNeedleEnabled) count++;
            if (hungerNeedleEnabled) count++;
            if (gutBacteriaNeedleEnabled) count++;

            return count;
        }

        public float GetCloneDamage()
        {
            // 분신도 현재 플레이어와 같은 기본 침 데미지를 사용한다.
            // 분신 피해 증가 일반 증강은 SyringeCloneController 쪽에서 별도로 곱해진다.
            return GetEffectiveDamage();
        }

        public float GetAcupunctureFormationDamage()
        {
            // 침술진은 특수/전설 증강 효과를 제외하고,
            // 일반 스탯 기반 데미지 강화만 반영한다.
            float multiplier = 1f;

            if (playerCharacter != null)
            {
                multiplier *= playerCharacter.DamageMultiplier;
            }

            return damage.Value * multiplier;
        }

        public float GetAcupunctureFormationKnockback()
        {
            return knockback.Value;
        }

        public float GetAcupunctureFormationSpeed()
        {
            // 침술진은 특수 증강 효과 없이 일반 투사체 속도 강화만 반영한다.
            float multiplier = 1f;

            if (playerCharacter != null)
            {
                multiplier *= playerCharacter.ProjectileSpeedMultiplier;
            }

            return speed.Value * multiplier;
        }

        public int GetAcupunctureFormationProjectileCount()
        {
            // 생명연소 같은 전설 추가 투사체는 제외하고,
            // 기본 무기 성장 + 일반 추가 투사체만 반영한다.
            int totalCount = projectileCount.Value;

            if (playerCharacter != null)
            {
                totalCount += playerCharacter.AdditionalProjectiles;
            }

            return Mathf.Max(1, totalCount);
        }

        public float GetAcupunctureFormationProjectileSizeMultiplier()
        {
            return GetPlayerProjectileSizeMultiplier();
        }

        public float GetAcupunctureFormationMaxDistance()
        {
            return Mathf.Max(0.1f, baseSyringeMaxDistance) * GetPlayerRangeMultiplier();
        }

        public float GetCloneKnockback()
        {
            return GetEffectiveKnockback();
        }

        public float GetCloneSpeed()
        {
            return GetEffectiveSpeed();
        }

        public float GetCloneCooldown()
        {
            return GetEffectiveCooldown();
        }

        public int GetCloneProjectileCount()
        {
            // 분신도 현재 플레이어와 같은 발사체 개수를 사용한다.
            return GetEffectiveProjectileCount();
        }

        public float GetEffectiveProjectileSizeMultiplier()
        {
            return GetPlayerProjectileSizeMultiplier();
        }

        public float GetEffectiveRangeMultiplier()
        {
            return GetPlayerRangeMultiplier();
        }

        private float GetPlayerProjectileSizeMultiplier()
        {
            return GetPublicFloatProperty(playerCharacter, "ProjectileSizeMultiplier", 1f);
        }

        private float GetPlayerRangeMultiplier()
        {
            return GetPublicFloatProperty(playerCharacter, "RangeMultiplier", 1f);
        }

        private float GetPublicFloatProperty(object target, string propertyName, float defaultValue)
        {
            if (target == null)
            {
                return defaultValue;
            }

            PropertyInfo propertyInfo = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public
            );

            if (propertyInfo == null)
            {
                return defaultValue;
            }

            object value = propertyInfo.GetValue(target);

            if (value is float floatValue)
            {
                return floatValue;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            return defaultValue;
        }

        public void EnablePoisonAugment() => poisonEnabled = true;

        public void EnableExplosionAugment() => explosionEnabled = true;

        public void EnableHomingAugment() => homingEnabled = true;
        public void EnableFiberNeedleAugment() => fiberNeedleEnabled = true;

        public void EnableCorrosionNeedleAugment() => corrosionNeedleEnabled = true;

        public void EnablePressureNeedleAugment() => pressureNeedleEnabled = true;

        public void EnableMarkNeedleAugment() => markNeedleEnabled = true;
        public void EnableDigestiveAcidSacNeedleAugment() => digestiveAcidSacNeedleEnabled = true;

        public void EnableHungerNeedleAugment() => hungerNeedleEnabled = true;

        public void EnableGutBacteriaNeedleAugment() => gutBacteriaNeedleEnabled = true;

        public bool HasDigestiveAcidSacNeedleAugment() => digestiveAcidSacNeedleEnabled;

        public bool HasHungerNeedleAugment() => hungerNeedleEnabled;

        public bool HasGutBacteriaNeedleAugment() => gutBacteriaNeedleEnabled;
        public void EnableBipolarNeedleAugment() => bipolarNeedleEnabled = true;
        public bool HasBipolarNeedleAugment() => bipolarNeedleEnabled;
        public bool HasFiberNeedleAugment() => fiberNeedleEnabled;

        public bool HasCorrosionNeedleAugment() => corrosionNeedleEnabled;

        public bool HasPressureNeedleAugment() => pressureNeedleEnabled;

        public bool HasMarkNeedleAugment() => markNeedleEnabled;
       
        public void EnablePierceAugment()
        {
            pierceEnabled = true;
            pierceCount = Mathf.Max(2, pierceCount);
        }

        public void EnableHoneyAugment() => honeyEnabled = true;

        public void EnableMosquitoAugment() => mosquitoEnabled = true;

        public void EnableReturnNeedleAugment() => returnNeedleEnabled = true;

        public void EnableAcupunctureFormationAugment()
        {
            acupunctureFormationEnabled = true;

            if (!acupunctureFormationBonusDashApplied && playerCharacter != null)
            {
                playerCharacter.AddDashCharge(acupunctureFormationBonusDashCharges);
                acupunctureFormationBonusDashApplied = true;
            }

            if (acupunctureFormationController != null)
            {
                return;
            }

            if (playerCharacter == null || entityManager == null)
            {
                Debug.LogWarning("[침술진] playerCharacter 또는 entityManager가 없어 침술진 컨트롤러를 생성하지 못했습니다.");
                return;
            }

            acupunctureFormationController = AcupunctureFormationController.Create(
                playerCharacter,
                entityManager,
                this,
                acupunctureFormationLifetime,
                acupunctureFormationNeedleCount,
                acupunctureFormationDamageMultiplier,
                acupunctureFormationVisualScale
            );

            Debug.Log("[침술진] 특수 증강 활성화. 대쉬 횟수 +1, 대쉬 시작 위치에 침술진을 생성합니다.");
        }

        public void AddPierceCount(int amount)
        {
            pierceCount += amount;
            pierceCount = Mathf.Max(0, pierceCount);

            if (pierceCount > 0)
            {
                pierceEnabled = true;
            }
        }

        public bool HasPoisonAugment() => poisonEnabled;
        public bool HasExplosionAugment() => explosionEnabled;
        public bool HasHomingAugment() => homingEnabled;
        public bool HasPierceAugment() => pierceEnabled;
        public bool HasHoneyAugment() => honeyEnabled;
        public bool HasMosquitoAugment() => mosquitoEnabled;
        public bool HasReturnNeedleAugment() => returnNeedleEnabled;
        public bool HasAcupunctureFormationAugment() => acupunctureFormationEnabled;

        public void EnableLifeBurnLegendary() => lifeBurnEnabled = true;

        public bool HasLifeBurnLegendary() => lifeBurnEnabled;

        public void MarkCloneLegendaryTaken() => cloneLegendaryTaken = true;

        public bool HasCloneLegendary() => cloneLegendaryTaken;
        public void EnableNeuralBlockLegendary()
        {
            if (neuralBlockEnabled && neuralBlockController != null)
            {
                ConfigureNeuralBlockController();
                return;
            }

            neuralBlockEnabled = true;

            neuralBlockController = GetComponent<NeuralBlockController>();

            if (neuralBlockController == null)
            {
                neuralBlockController = gameObject.AddComponent<NeuralBlockController>();
            }

            ConfigureNeuralBlockController();
        }

        private void ConfigureNeuralBlockController()
        {
            if (neuralBlockController == null)
            {
                return;
            }

            neuralBlockController.Configure(
                neuralBlockInterval,
                neuralBlockFreezeDuration,
                neuralBlockScreenPadding,
                monsterLayer,
                debugNeuralBlock
            );
        }

        public bool HasNeuralBlockLegendary()
        {
            return neuralBlockEnabled;
        }

        public void EnablePoisonContagionLegendary()
        {
            poisonContagionEnabled = true;

            PoisonContagionRuntime.Enable(
                poisonContagionRadius,
                poisonContagionDurationMultiplier,
                poisonContagionDamageMultiplier,
                monsterLayer,
                debugPoisonContagion
            );
        }

        public bool HasPoisonContagionLegendary()
        {
            return poisonContagionEnabled;
        }

        public void EnableOrganCompressionLegendary()
        {
            if (organCompressionEnabled && organCompressionController != null)
            {
                ConfigureOrganCompressionController();
                return;
            }

            organCompressionEnabled = true;

            organCompressionController = GetComponent<OrganCompressionController>();

            if (organCompressionController == null)
            {
                organCompressionController = gameObject.AddComponent<OrganCompressionController>();
            }

            ConfigureOrganCompressionController();
        }

        private void ConfigureOrganCompressionController()
        {
            if (organCompressionController == null)
            {
                return;
            }

            organCompressionController.Configure(
                organCompressionInterval,
                organCompressionClusterSearchRadius,
                organCompressionFieldRadius,
                organCompressionFieldDuration,
                organCompressionDamageTickInterval,
                organCompressionDamagePerTick,
                organCompressionPullSpeed,
                organCompressionScreenPadding,
                monsterLayer,
                debugOrganCompression
            );
        }

        public bool HasOrganCompressionLegendary()
        {
            return organCompressionEnabled;
        }
        public void EnableHedgehogNeedleLegendary()
        {
            hedgehogNeedleEnabled = true;

            if (hedgehogNeedleController != null)
            {
                return;
            }

            if (playerCharacter == null || entityManager == null)
            {
                Debug.LogWarning("[고슴도침] playerCharacter 또는 entityManager가 없어 고슴도침을 생성하지 못했습니다.");
                return;
            }

            hedgehogNeedleController = HedgehogNeedleController.Create(
                playerCharacter,
                entityManager,
                this,
                hedgehogNeedleCount,
                hedgehogOrbitRadius,
                hedgehogRotationSpeed,
                hedgehogTouchFireCooldown,
                hedgehogDamageMultiplier
            );

            Debug.Log("[고슴도침] 접촉 반격형 침 결계 생성 완료");
        }

        public bool HasHedgehogNeedleLegendary()
        {
            return hedgehogNeedleEnabled;
        }

        public void EnableHeavySnipeLegendary()
        {
            if (heavySnipeEnabled)
            {
                return;
            }

            heavySnipeEnabled = true;
            isHeavyCharging = false;
            heavyChargeTimer = 0f;
            cursorHeavyChargeRatio = 0f;
            HideHeavySnipeChargePreview();

            Debug.Log("[대물침] 전설 증강 활성화.");
        }

        public bool HasHeavySnipeLegendary()
        {
            return heavySnipeEnabled;
        }

        public void EnableCursorControlLegendary()
        {
            cursorControlEnabled = true;

            if (cursorControlledNeedleController != null)
            {
                return;
            }

            if (playerCharacter == null || entityManager == null)
            {
                Debug.LogWarning("[이기어침] playerCharacter 또는 entityManager가 없어 이기어침을 생성하지 못했습니다.");
                return;
            }

            cursorControlledNeedleController = CursorControlledNeedleController.Create(
                playerCharacter,
                entityManager,
                this,
                cursorNeedleFollowSpeed,
                cursorNeedleHitRadius,
                cursorNeedleDamageMultiplier,
                cursorNeedleDamageInterval,
                cursorNeedleVisualScale,
                cursorNeedleHomingHitRadiusBonus,
                cursorNeedleBackDisplayOffset,
                cursorNeedleBackDisplaySpacing,
                cursorNeedleBackDisplayArcHeight,
                cursorNeedleBackDisplayScale
            );

            Debug.Log("[이기어침] 전설 증강 활성화. 기본 자동 공격을 중지하고, 마우스 포인트를 따라다니는 조종 침을 생성했습니다.");
        }

        public bool HasCursorControlLegendary()
        {
            return cursorControlEnabled;
        }

        // 이기어침 + 대물침 조합용 공개 메서드들
        public float GetCursorHeavyChargeRatio()
        {
            if (!cursorControlEnabled || !heavySnipeEnabled)
            {
                return 0f;
            }

            return Mathf.Clamp01(cursorHeavyChargeRatio);
        }

        public bool IsCursorHeavyChargeActive()
        {
            return cursorControlEnabled && heavySnipeEnabled && cursorHeavyChargeRatio > 0.001f;
        }

        public float GetCursorNeedleHeavyDamageMultiplier()
        {
            if (!cursorControlEnabled || !heavySnipeEnabled)
            {
                return 1f;
            }

            return CalculateHeavySnipeChargeStats(cursorHeavyChargeRatio).damageMultiplier;
        }

        public float GetCursorNeedleHeavySizeMultiplier()
        {
            if (!cursorControlEnabled || !heavySnipeEnabled)
            {
                return 1f;
            }

            return CalculateHeavySnipeChargeStats(cursorHeavyChargeRatio).sizeMultiplier;
        }

        public float GetCursorNeedleHeavyKnockbackMultiplier()
        {
            if (!cursorControlEnabled || !heavySnipeEnabled)
            {
                return 1f;
            }

            return CalculateHeavySnipeChargeStats(cursorHeavyChargeRatio).knockbackMultiplier;
        }

        public bool IsCursorNeedleHeavyPierceUnlimited()
        {
            if (!cursorControlEnabled || !heavySnipeEnabled)
            {
                return false;
            }

            return CalculateHeavySnipeChargeStats(cursorHeavyChargeRatio).unlimitedPierce;
        }

        public int GetCursorNeedleHeavyPierceBonus()
        {
            if (!cursorControlEnabled || !heavySnipeEnabled)
            {
                return 0;
            }

            HeavySnipeChargeStats stats = CalculateHeavySnipeChargeStats(cursorHeavyChargeRatio);

            if (stats.unlimitedPierce)
            {
                return int.MaxValue;
            }

            return stats.pierceCount;
        }

        private struct HeavySnipeChargeStats
        {
            public float damageMultiplier;
            public float speedMultiplier;
            public int pierceCount;
            public bool unlimitedPierce;
            public float sizeMultiplier;
            public float rangeBonus;
            public float knockbackMultiplier;
        }
    }
}