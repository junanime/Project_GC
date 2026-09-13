using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class BossFanShotPattern : BossPatternBase
    {
        [Header("Fan Shot Settings")]
        [Tooltip("부채꼴 탄막에 사용할 보스 탄환 프리팹입니다.")]
        [SerializeField] private GameObject bulletPrefab;

        [Tooltip("부채꼴 탄막의 기본 탄속입니다. 실제 탄속은 보스 현재 페이즈의 Projectile Speed Multiplier가 곱해집니다.")]
        [SerializeField] private float bulletSpeed = 8f;

        [Tooltip("부채꼴 탄막의 기본 데미지입니다. 실제 데미지는 보스 현재 페이즈의 Damage Multiplier가 곱해집니다.")]
        [SerializeField] private float bulletDamage = 10f;

        [Tooltip("1페이즈에서 한 번에 발사할 탄환 수입니다.")]
        [SerializeField] private int bulletCountPhase1 = 7;

        [Tooltip("2페이즈에서 한 번에 발사할 탄환 수입니다.")]
        [SerializeField] private int bulletCountPhase2 = 11;

        [Tooltip("3페이즈에서 한 번에 발사할 탄환 수입니다.")]
        [SerializeField] private int bulletCountPhase3 = 13;

        [Tooltip("플레이어 방향을 중심으로 탄환이 퍼질 전체 각도입니다. 90이면 -45도부터 +45도까지 발사됩니다.")]
        [SerializeField] private float totalSpreadAngle = 90f;

        [Tooltip("1페이즈에서 부채꼴 발사를 몇 번 반복할지 정합니다.")]
        [SerializeField] private int burstCountPhase1 = 1;

        [Tooltip("2페이즈에서 부채꼴 발사를 몇 번 반복할지 정합니다.")]
        [SerializeField] private int burstCountPhase2 = 2;

        [Tooltip("3페이즈에서 부채꼴 발사를 몇 번 반복할지 정합니다.")]
        [SerializeField] private int burstCountPhase3 = 2;

        [Tooltip("연속 발사 사이의 간격입니다.")]
        [SerializeField] private float burstInterval = 0.25f;

        [Header("Spawn Position")]
        [Tooltip("보스 중심에서 탄환을 얼마나 앞쪽에 생성할지 정합니다.")]
        [SerializeField] private float muzzleOffsetFromBoss = 0.9f;

        [Header("Visual Sorting")]
        [Tooltip("체크하면 탄환 SpriteRenderer의 Sorting Order를 강제로 설정합니다.")]
        [SerializeField] private bool forceBulletSortingOrder = true;

        [Tooltip("탄환의 화면 표시 순서입니다.")]
        [SerializeField] private int bulletSortingOrder = 550;

        [Header("Debug")]
        [Tooltip("체크하면 발사 로그를 Console에 출력합니다.")]
        [SerializeField] private bool debugShot = false;

        protected override IEnumerator ExecutePattern()
        {
            int bulletCount = GetPhaseBulletCount();
            int burstCount = GetPhaseBurstCount();

            for (int burst = 0; burst < burstCount; burst++)
            {
                FireFanShot(bulletCount);

                if (burst < burstCount - 1)
                {
                    yield return new WaitForSeconds(burstInterval);
                }
            }
        }

        private int GetPhaseBulletCount()
        {
            if (bossController.CurrentPhase >= 3)
            {
                return bulletCountPhase3;
            }

            if (bossController.CurrentPhase == 2)
            {
                return bulletCountPhase2;
            }

            return bulletCountPhase1;
        }

        private int GetPhaseBurstCount()
        {
            if (bossController.CurrentPhase >= 3)
            {
                return burstCountPhase3;
            }

            if (bossController.CurrentPhase == 2)
            {
                return burstCountPhase2;
            }

            return burstCountPhase1;
        }

        private void FireFanShot(int bulletCount)
        {
            if (bulletPrefab == null)
            {
                Debug.LogWarning("[BossFanShotPattern] Bullet Prefab이 비어 있습니다.");
                return;
            }

            if (bossController == null || bossController.PlayerCharacter == null)
            {
                return;
            }

            Vector3 bossPosition = bossController.AttackOriginPosition;
            Vector2 baseDirection = ((Vector2)bossController.PlayerCharacter.transform.position - (Vector2)bossPosition).normalized;

            if (baseDirection == Vector2.zero)
            {
                baseDirection = Vector2.right;
            }

            float startAngle = -totalSpreadAngle * 0.5f;
            float angleStep = bulletCount > 1 ? totalSpreadAngle / (bulletCount - 1) : 0f;

            for (int i = 0; i < bulletCount; i++)
            {
                float angle = startAngle + angleStep * i;
                Vector2 direction = RotateVector(baseDirection, angle);

                Vector3 spawnPosition = bossController.GetProjectileSpawnPosition((Vector3)(direction.normalized * muzzleOffsetFromBoss));

                SpawnBullet(spawnPosition, direction);
            }

            if (debugShot)
            {
                Debug.Log($"[BossFanShotPattern] Fired {bulletCount} bullets / phase={bossController.CurrentPhase}");
            }
        }

        private void SpawnBullet(Vector3 spawnPosition, Vector2 direction)
        {
            GameObject bullet = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);

            if (direction != Vector2.zero)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                bullet.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (forceBulletSortingOrder)
            {
                SpriteRenderer[] renderers = bullet.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (SpriteRenderer renderer in renderers)
                {
                    renderer.sortingOrder = bulletSortingOrder;
                }
            }

            BossSimpleBullet simpleBullet = bullet.GetComponent<BossSimpleBullet>();

            if (simpleBullet == null)
            {
                simpleBullet = bullet.GetComponentInChildren<BossSimpleBullet>();
            }

            if (simpleBullet == null)
            {
                simpleBullet = bullet.AddComponent<BossSimpleBullet>();
                Debug.LogWarning("[BossFanShotPattern] Bullet Prefab 루트에 BossSimpleBullet이 없어 자동 추가했습니다.");
            }

            float finalSpeed = bossController.GetModifiedProjectileSpeed(bulletSpeed);
            float finalDamage = bossController.GetModifiedDamage(bulletDamage);

            simpleBullet.Init(direction, finalSpeed, finalDamage);
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
    }
}
