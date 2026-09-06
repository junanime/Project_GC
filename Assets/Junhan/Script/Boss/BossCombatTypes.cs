using System;

namespace Vampire
{
    /// <summary>
    /// 크리피커피 보스 코어가 현재 가지고 있는 기본 속성입니다.
    ///
    /// Single Core:
    /// - Red / Yellow / Blue 중 하나
    ///
    /// Dual Core:
    /// - Red | Yellow = Orange
    /// - Red | Blue = Purple
    /// - Yellow | Blue = Green
    ///
    /// Rainbow Core:
    /// - All
    /// </summary>
    [Flags]
    public enum BossCoreTrait
    {
        None = 0,
        Red = 1 << 0,
        Yellow = 1 << 1,
        Blue = 1 << 2,
        All = Red | Yellow | Blue
    }

    /// <summary>
    /// 현재 보스 코어의 페이즈 형태입니다.
    /// </summary>
    public enum BossCoreMode
    {
        Single = 0,
        Dual = 1,
        Rainbow = 2
    }

    /// <summary>
    /// 패턴이 요구하는 코어 속성과 현재 코어를 비교하는 방식입니다.
    /// </summary>
    public enum BossPatternCoreMatchMode
    {
        /// <summary>
        /// 요구 속성 중 하나라도 현재 코어에 포함되어 있으면 사용 가능합니다.
        /// 일반적인 Red / Yellow / Blue 패턴에 사용합니다.
        /// </summary>
        Any = 0,

        /// <summary>
        /// 요구 속성이 전부 현재 코어에 포함되어 있어야 사용 가능합니다.
        /// 이후 Dual 전용, Rainbow 전용 패턴을 만들 때 사용합니다.
        /// </summary>
        All = 1
    }

    /// <summary>
    /// 보스가 받은 피해의 출처입니다.
    /// 이후 압력 게이지, 보스 자폭, 보스 전용 증강/아이템을 구분할 때 사용합니다.
    /// </summary>
    public enum BossDamageSourceType
    {
        Player = 0,
        BossSelf = 1,
        Environment = 2
    }

    public static class BossCoreTraitUtility
    {
        public static bool Matches(
            BossCoreTrait activeTraits,
            BossCoreTrait requiredTraits,
            BossPatternCoreMatchMode matchMode)
        {
            // None은 코어 제한이 없는 중립 패턴으로 취급합니다.
            if (requiredTraits == BossCoreTrait.None)
            {
                return true;
            }

            if (activeTraits == BossCoreTrait.None)
            {
                return false;
            }

            if (matchMode == BossPatternCoreMatchMode.All)
            {
                return (activeTraits & requiredTraits) == requiredTraits;
            }

            return (activeTraits & requiredTraits) != 0;
        }

        public static bool IsSingle(BossCoreTrait traits)
        {
            return traits == BossCoreTrait.Red ||
                   traits == BossCoreTrait.Yellow ||
                   traits == BossCoreTrait.Blue;
        }

        public static bool IsDual(BossCoreTrait traits)
        {
            return traits == (BossCoreTrait.Red | BossCoreTrait.Yellow) ||
                   traits == (BossCoreTrait.Red | BossCoreTrait.Blue) ||
                   traits == (BossCoreTrait.Yellow | BossCoreTrait.Blue);
        }
    }
}
