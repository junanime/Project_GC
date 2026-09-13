using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class BossRadialBurstPattern : BossPatternBase
    {
        [Header("Radial Burst Settings")]
        [Tooltip("원형 탄막에 사용할 보스 탄환 프리팹입니다.")]
        [SerializeField] private GameObject bulletPrefab;

        [Tooltip("원형 탄막의 기본 탄속입니다. 실제 탄속은 보스 현재 페이즈의 Projectile Speed Multiplier가 곱해집니다.")]
        [SerializeField] private float bulletSpeed = 6f;

        [Tooltip("원형 탄막의 기본 데미지입니다. 실제 데미지는 보스 현재 페이즈의 Damage Multiplier가 곱해집니다.")]
        [SerializeField] private float bulletDamage = 8f;

        [Tooltip("1페이즈에서 한 원에 발사할 탄환 수입니다.")]
        [SerializeField] private int bulletCountPhase1 = 12;

        [Tooltip("2페이즈에서 한 원에 발사할 탄환 수입니다.")]
        [SerializeField] private int bulletCountPhase2 = 20;

        [Tooltip("3페이즈에서 한 원에 발사할 탄환 수입니다.")]
        [SerializeField] private int bulletCountPhase3 = 24;

        [Tooltip("1페이즈에서 원형 탄막을 몇 겹 발사할지 정합니다.")]
        [SerializeField] private int ringCountPhase1 = 1;

        [Tooltip("2페이즈에서 원형 탄막을 몇 겹 발사할지 정합니다.")]
        [SerializeField] private int ringCountPhase2 = 2;

        [Tooltip("3페이즈에서 원형 탄막을 몇 겹 발사할지 정합니다.")]
        [SerializeField] private int ringCountPhase3 = 2;

        [Tooltip("원형 탄막을 여러 겹 발사할 때 다음 원 발사까지 기다리는 시간입니다.")]
        [SerializeField] private float ringInterval = 0.25f;

        [Header("Repeat Angle Offset / 반복 각도 보정")]
        [Tooltip("같은 원형 탄막 패턴이 다시 실행될 때마다 시작 각도를 몇 도씩 틀지 정합니다. 예: 20이면 첫 번째 0도, 두 번째 20도, 세 번째 40도 기준으로 발사됩니다.")]
        [SerializeField] private float patternRepeatAngleOffsetStep = 20f;

        [Tooltip("한 번의 패턴 안에서 여러 겹의 원형 탄막을 발사할 때, 다음 원을 몇 도 틀어서 발사할지 정합니다. 기존 코드의 ring * 8f 값을 인스펙터에서 조절할 수 있게 뺀 값입니다.")]
        [SerializeField] private float ringAngleOffsetStep = 8f;

        [Tooltip("체크하면 패턴이 반복될 때마다 각도 보정값이 누적됩니다. 끄면 기존처럼 매번 같은 각도로 발사됩니다.")]
        [SerializeField] private bool accumulateRepeatAngleOffset = true;

        [Tooltip("체크하면 보스 오브젝트가 비활성화되었다가 다시 켜질 때 반복 각도 누적값을 0으로 초기화합니다.")]
        [SerializeField] private bool resetRepeatIndexOnDisable = true;

        [Header("Visual Sorting")]
        [Tooltip("체크하면 탄환 SpriteRenderer의 Sorting Order를 강제로 설정합니다.")]
        [SerializeField] private bool forceBulletSortingOrder = true;

        [Tooltip("탄환의 화면 표시 순서입니다.")]
        [SerializeField] private int bulletSortingOrder = 550;

        [Header("Debug")]
        [Tooltip("체크하면 발사 로그를 Console에 출력합니다.")]
        [SerializeField] private bool debugShot = false;

        private int repeatPatternIndex = 0;

        private void OnDisable()
        {
            if (resetRepeatIndexOnDisable)
            {
                repeatPatternIndex = 0;
            }
        }

        protected override IEnumerator ExecutePattern()
        {
            int bulletCount = GetPhaseBulletCount();
            int ringCount = GetPhaseRingCount();

            int currentRepeatIndex = repeatPatternIndex;

            if (accumulateRepeatAngleOffset)
            {
                repeatPatternIndex++;
            }

            float baseAngleOffset = GetRepeatBaseAngleOffset(currentRepeatIndex);

            for (int ring = 0; ring < ringCount; ring++)
            {
                float ringAngleOffset = baseAngleOffset + (ring * ringAngleOffsetStep);

                FireRadialBurst(bulletCount, ringAngleOffset);

                if (ring < ringCount - 1)
                {
                    yield return new WaitForSeconds(ringInterval);
                }
            }
        }

        private float GetRepeatBaseAngleOffset(int repeatIndex)
        {
            if (!accumulateRepeatAngleOffset)
            {
                return 0f;
            }

            return Mathf.Repeat(repeatIndex * patternRepeatAngleOffsetStep, 360f);
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

        private int GetPhaseRingCount()
        {
            if (bossController.CurrentPhase >= 3)
            {
                return ringCountPhase3;
            }

            if (bossController.CurrentPhase == 2)
            {
                return ringCountPhase2;
            }

            return ringCountPhase1;
        }

        private void FireRadialBurst(int bulletCount, float angleOffset)
        {
            if (bossController != null && bossController.UsesFiveCoreSkills && !bossController.FiveCoreSkills.CanContinue(this)) return;
            if (bulletPrefab == null)
            {
                Debug.LogWarning("[BossRadialBurstPattern] Bullet Prefab이 비어 있습니다.");
                return;
            }

            if (bulletCount <= 0)
            {
                Debug.LogWarning("[BossRadialBurstPattern] Bullet Count가 0 이하입니다.");
                return;
            }

            Vector3 spawnPosition = bossController.AttackOriginPosition;

            for (int i = 0; i < bulletCount; i++)
            {
                float angle = angleOffset + (360f / bulletCount) * i;
                Vector2 direction = AngleToDirection(angle);

                SpawnBullet(spawnPosition, direction);
            }

            if (debugShot)
            {
                Debug.Log(
                    $"[BossRadialBurstPattern] Fired {bulletCount} bullets / " +
                    $"phase={bossController.CurrentPhase} / " +
                    $"repeatIndex={repeatPatternIndex} / " +
                    $"angleOffset={angleOffset:0.0}"
                );
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
                Debug.LogWarning("[BossRadialBurstPattern] Bullet Prefab 루트에 BossSimpleBullet이 없어 자동 추가했습니다.");
            }

            float finalSpeed = bossController.GetModifiedProjectileSpeed(bulletSpeed);
            float finalDamage = bossController.GetModifiedDamage(bulletDamage);

            simpleBullet.Init(direction, finalSpeed, finalDamage);
        }

        private Vector2 AngleToDirection(float angleDegrees)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        }
    }
}
