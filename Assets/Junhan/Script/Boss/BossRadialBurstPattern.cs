using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class BossRadialBurstPattern : BossPatternBase
    {
        [Header("Radial Burst Settings")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private float bulletSpeed = 6f;
        [SerializeField] private float bulletDamage = 8f;
        [SerializeField] private int bulletCountPhase1 = 12;
        [SerializeField] private int bulletCountPhase2 = 20;
        [SerializeField] private int ringCountPhase1 = 1;
        [SerializeField] private int ringCountPhase2 = 2;
        [SerializeField] private float ringInterval = 0.25f;

        protected override IEnumerator ExecutePattern()
        {
            int bulletCount = bossController.CurrentPhase == 1 ? bulletCountPhase1 : bulletCountPhase2;
            int ringCount = bossController.CurrentPhase == 1 ? ringCountPhase1 : ringCountPhase2;

            for (int ring = 0; ring < ringCount; ring++)
            {
                FireRadialBurst(bulletCount, ring * 8f);
                yield return new WaitForSeconds(ringInterval);
            }
        }

        private void FireRadialBurst(int bulletCount, float angleOffset)
        {
            if (bulletPrefab == null)
                return;

            for (int i = 0; i < bulletCount; i++)
            {
                float angle = angleOffset + (360f / bulletCount) * i;
                Vector2 direction = AngleToDirection(angle);

                GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
                BossSimpleBullet simpleBullet = bullet.GetComponent<BossSimpleBullet>();

                if (simpleBullet != null)
                {
                    simpleBullet.Init(direction, bulletSpeed, bulletDamage);
                }
            }
        }

        private Vector2 AngleToDirection(float angleDegrees)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        }
    }
}