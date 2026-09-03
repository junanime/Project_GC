using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 공격속도 디버퍼 특수 몬스터 전용 블루프린트입니다.
    /// RangedMonsterBlueprint를 그대로 쓰면 스폰 위치 보정, 발사 위치 fallback, 디버프 연출 관리가 애매해서
    /// SniperMonster처럼 특수 몬스터 전용 설정을 따로 분리했습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Attack Speed Debuffer Monster",
        menuName = "Blueprints/Monsters/Attack Speed Debuffer Monster",
        order = 12)]
    public class AttackSpeedDebufferMonsterBlueprint : MonsterBlueprint
    {
        [Header("Debuffer Projectile")]
        [Tooltip("공격속도 감소 디버프를 적용하는 투사체 프리팹입니다. AttackSpeedDownProjectile이 붙어 있어야 합니다.")]
        public GameObject projectilePrefab;

        [Tooltip("투사체가 맞출 대상 레이어입니다. 보통 Player 레이어를 넣습니다.")]
        public LayerMask targetLayer;

        [Tooltip("디버프 투사체 속도입니다.")]
        public float projectileSpeed = 4f;

        [Tooltip("이 거리 안에 플레이어가 있으면 디버프 투사체를 발사합니다.")]
        public float attackRange = 6f;

        [Tooltip("처음 스폰된 뒤 첫 공격까지 기다리는 시간입니다.")]
        public float firstAttackDelay = 0.8f;

        [Tooltip("디버프 투사체를 발사한 뒤 다음 발사까지의 쿨타임입니다.")]
        public float attackCooldown = 2.2f;

        [Header("Sniper Style Spawn")]
        [Tooltip("체크하면 스폰 위치를 플레이어 기준 일정 거리로 보정합니다.")]
        public bool enforceSpawnDistance = true;

        [Tooltip("플레이어와 유지할 스폰 거리입니다. 저격수처럼 화면 바깥/근처 외곽에 배치하고 싶으면 7~9를 추천합니다.")]
        public float spawnDistanceFromPlayer = 7f;

        [Header("Movement")]
        [Tooltip("체크하면 스폰 후 움직이지 않고 제자리에서 디버프 탄환만 발사합니다.")]
        public bool freezePositionAfterSpawn = true;

        [Tooltip("freezePositionAfterSpawn이 꺼져 있을 때, 플레이어와 너무 멀어지면 천천히 접근하는 거리입니다.")]
        public float desiredDistanceFromPlayer = 5f;
    }
}