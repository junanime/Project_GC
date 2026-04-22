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
            if (bulletPrefab == null || bossController.PlayerCharacter == null)
                return;

            Vector2 baseDirection = (bossController.PlayerCharacter.transform.position - transform.position).normalized;
            if (baseDirection == Vector2.zero)
                baseDirection = Vector2.right;

            float startAngle = -totalSpreadAngle * 0.5f;
            float angleStep = bulletCount > 1 ? totalSpreadAngle / (bulletCount - 1) : 0f;

            for (int i = 0; i < bulletCount; i++)
            {
                float angle = startAngle + angleStep * i;
                Vector2 direction = RotateVector(baseDirection, angle);

                GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
                BossSimpleBullet simpleBullet = bullet.GetComponent<BossSimpleBullet>();

                if (simpleBullet != null)
                {
                    simpleBullet.Init(direction, bulletSpeed, bulletDamage);
                }
            }
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