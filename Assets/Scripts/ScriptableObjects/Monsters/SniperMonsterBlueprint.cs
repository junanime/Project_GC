using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(fileName = "Sniper Monster", menuName = "Blueprints/Monsters/Sniper Monster", order = 1)]
    public class SniperMonsterBlueprint : MonsterBlueprint
    {
        [Header("Sniper Monster - Projectile")]
        [Tooltip("저격수가 발사할 탄환 프리팹입니다. 일반 Projectile 기반 프리팹을 넣으면 됩니다.")]
        public GameObject projectilePrefab;

        [Tooltip("탄환이 맞출 대상 레이어입니다. 보통 Player 레이어를 넣습니다.")]
        public LayerMask targetLayer;

        [Tooltip("저격 탄환 속도입니다. 높을수록 피하기 어렵습니다.")]
        public float projectileSpeed = 18f;

        [Header("Takoyaki Recoil Visual")]
        public Sprite[] fireSprites;
        public float fireFrameTime = 0.09f;

        [Header("Sniper Monster - Attack Timing")]
        [Tooltip("레이저가 플레이어를 따라다니며 조준하는 시간입니다.")]
        public float aimDuration = 1.4f;

        [Tooltip("조준이 고정된 뒤 탄환이 발사되기까지의 시간입니다. 이 시간이 플레이어 회피 시간입니다.")]
        public float lockDuration = 0.35f;

        [Tooltip("한 번 발사한 뒤 다음 조준을 시작하기까지의 대기 시간입니다.")]
        public float attackCooldown = 1.8f;

        [Tooltip("스폰 직후 첫 조준까지의 대기 시간입니다.")]
        public float firstAttackDelay = 0.6f;

        [Header("Sniper Monster - Spawn")]
        [Tooltip("체크하면 스폰될 때 플레이어와 일정 거리를 유지하도록 위치를 보정합니다.")]
        public bool enforceSpawnDistance = true;

        [Tooltip("플레이어와 유지할 스폰 거리입니다.")]
        public float spawnDistanceFromPlayer = 9f;

        [Header("Sniper Monster - Laser Visual")]
        [Tooltip("조준 중 레이저 두께입니다.")]
        public float laserWidth = 0.04f;

        [Tooltip("플레이어를 따라다니는 중의 레이저 색상입니다.")]
        public Color aimingLaserColor = new Color(1f, 0f, 0f, 0.45f);

        [Tooltip("조준이 고정된 뒤의 레이저 색상입니다.")]
        public Color lockedLaserColor = new Color(1f, 0f, 0f, 0.95f);

        [Tooltip("레이저 정렬 순서입니다. 캐릭터보다 위에 보이게 하려면 높게 설정하세요.")]
        public int laserSortingOrder = 50;
    }
}
