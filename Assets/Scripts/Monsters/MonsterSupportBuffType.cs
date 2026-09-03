namespace Vampire
{
    /// <summary>
    /// 특수 몬스터 버퍼가 사용할 버프 종류입니다.
    /// 이동속도 증가형과 받는 피해 감소형을 같은 MonsterSupportCaster 코드에서 처리하기 위해 분리했습니다.
    /// </summary>
    public enum MonsterSupportBuffType
    {
        MoveSpeed,
        DamageReduction
    }
}