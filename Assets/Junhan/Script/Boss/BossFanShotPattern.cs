using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class BossFanShotPattern : BossPatternBase
    {
        [Header("Fan Shot Settings")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private float bulletSpeed = 8f;
        [SerializeField] private float bulletDamage = 10f;
        [SerializeField] private int bulletCountPhase1 = 7;
        [SerializeField] private int bulletCountPhase2 = 11;
        [SerializeField] private float totalSpreadAngle = 90f;
        [SerializeField] private int burstCountPhase1 = 1;
        [SerializeField] private int burstCountPhase2 = 2;
        [SerializeField] private float burstInterval = 0.25f;

        [Header("Spawn Position")]
        [SerializeField] private float muzzleOffsetFromBoss = 0.9f;

        [Header("Visual Sorting")]
        [SerializeField] private bool forceBulletSortingOrder = true;
        [SerializeField] private int bulletSortingOrder = 550;

        [Header("Debug")]
        [SerializeField] private bool debugShot = false;

        protected override IEnumerator ExecutePattern()
        {
            int bulletCount = bossController.CurrentPhase == 1 ? bulletCountPhase1 : bulletCountPhase2;
            int burstCount = bossController.CurrentPhase == 1 ? burstCountPhase1 : burstCountPhase2;

            for (int burst = 0; burst < burstCount; burst++)
            {
                FireFanShot(bulletCount);
                yield return new WaitForSeconds(burstInterval);
            }
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

            Vector3 bossPosition = bossController.BossCenterPosition;

            Vector2 baseDirection =
                ((Vector2)bossController.PlayerCharacter.transform.position - (Vector2)bossPosition).normalized;

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

                Vector3 spawnPosition = bossPosition + (Vector3)(direction.normalized * muzzleOffsetFromBoss);
                SpawnBullet(spawnPosition, direction);
            }

            if (debugShot)
            {
                Debug.Log($"[BossFanShotPattern] Fired {bulletCount} bullets");
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

            simpleBullet.Init(direction, bulletSpeed, bulletDamage);
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