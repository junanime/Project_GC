namespace Vampire
{
    /// <summary>
    /// 특수 몬스터 버퍼가 사용할 버프 종류입니다.
    /// 이동속도 증가형과 체력 증가형입니다. DamageReduction 이름은 기존 프리팹 직렬화 호환을 위해 유지합니다.
    /// </summary>
    public enum MonsterSupportBuffType
    {
        MoveSpeed,
        DamageReduction
    }
}