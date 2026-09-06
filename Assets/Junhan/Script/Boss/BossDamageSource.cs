namespace Vampire
{
    /// <summary>
    /// 보스 파츠에 들어오는 피해의 출처를 구분합니다.
    ///
    /// 사용 예:
    /// - SyringeProjectile -> PlayerProjectile
    /// - 플레이어 장판/파동/특수능력 -> PlayerAbility
    /// - 보스 폭탄 자폭/미사일 역충돌 -> BossSelf
    /// - 필드 기믹/구조물 -> Environment
    /// - 전멸기 성공 보상 등 직접 피해 -> Scripted
    ///
    /// Player는 기존 BossController와의 하위호환용 별칭입니다.
    /// 신규 코드에서는 가능하면 PlayerProjectile / PlayerAbility를
    /// 구분해서 사용합니다.
    /// </summary>
    public enum BossDamageSourceType
    {
        /// <summary>
        /// 출처를 특정할 수 없는 기존/레거시 피해입니다.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// SyringeProjectile 등
        /// 플레이어가 직접 발사한 투사체 피해입니다.
        /// </summary>
        PlayerProjectile = 1,

        /// <summary>
        /// 기존 BossController 및 레거시 코드 호환용 값입니다.
        ///
        /// PlayerProjectile과 동일한 enum 값 1을 사용합니다.
        /// 따라서 기존 BossDamageSourceType.Player 코드도
        /// 플레이어 피해로 정상 처리됩니다.
        /// </summary>
        Player = PlayerProjectile,

        /// <summary>
        /// 플레이어의 장판, 파동, 특수능력,
        /// 증강에서 직접 발생한 피해입니다.
        /// </summary>
        PlayerAbility = 2,

        /// <summary>
        /// 보스 자신의 공격이 역이용되어
        /// 보스에게 들어가는 피해입니다.
        ///
        /// 예:
        /// - Bomb 자폭
        /// - 매립 Homing Missile 폭발
        /// - Charge로 미사일 충돌
        /// </summary>
        BossSelf = 3,

        /// <summary>
        /// 보스 자신이나 플레이어의 직접 공격이 아닌
        /// 필드/환경에서 발생한 피해입니다.
        ///
        /// 예:
        /// - 기둥
        /// - 필드 기믹
        /// - 특정 환경 오브젝트
        /// </summary>
        Environment = 4,

        /// <summary>
        /// 스크립트가 직접 적용하는 기믹 피해입니다.
        ///
        /// 예:
        /// - Pressure 역분사 성공 보상
        /// - Rainbow Annihilation 성공 시 Max HP 20% 피해
        /// </summary>
        Scripted = 5
    }

    /// <summary>
    /// 크리피커피 Core의 현재 피해 상태입니다.
    ///
    /// 실제 피해 배율은 BossPartDamageRules에서 관리합니다.
    /// </summary>
    public enum BossCoreDamageState
    {
        /// <summary>
        /// 살아남은 팔 등이 Core를 보호하고 있는 상태입니다.
        /// 현재 기본 피해 배율은 x1.0입니다.
        /// </summary>
        Guarded = 0,

        /// <summary>
        /// 특별히 노출되거나 그로기 상태가 아닌
        /// 일반 Core 상태입니다.
        /// 현재 기본 피해 배율은 x1.0입니다.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// 팔 패턴 사용 등으로 Core가 노출된 상태입니다.
        /// 현재 기본 피해 배율은 x1.5입니다.
        /// </summary>
        Open = 2,

        /// <summary>
        /// 보스가 기절/그로기 상태가 되어
        /// Core가 완전히 노출된 상태입니다.
        /// 현재 기본 피해 배율은 x2.0입니다.
        /// </summary>
        Groggy = 3
    }
}