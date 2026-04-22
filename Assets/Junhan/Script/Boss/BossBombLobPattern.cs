using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class BossBombLobPattern : BossPatternBase
    {
        [Header("Bomb Lob Settings")]
        [SerializeField] private GameObject warningCirclePrefab;
        [SerializeField] private float warningDuration = 1f;
        [SerializeField] private float explosionRadius = 2f;
        [SerializeField] private float explosionDamage = 20f;
        [SerializeField] private int bombCountPhase1 = 1;
        [SerializeField] private int bombCountPhase2 = 3;
        [SerializeField] private float randomOffsetRadius = 1.5f;

        protected override IEnumerator ExecutePattern()
        {
            int bombCount = bossController.CurrentPhase == 1 ? bombCountPhase1 : bombCountPhase2;

            for (int i = 0; i < bombCount; i++)
            {
                Vector2 targetPosition = (Vector2)bossController.PlayerCharacter.transform.position + Random.insideUnitCircle * randomOffsetRadius;
                yield return StartCoroutine(SpawnBombWarningAndExplode(targetPosition));
            }
        }

        private IEnumerator SpawnBombWarningAndExplode(Vector2 targetPosition)
        {
            GameObject warning = null;

            if (warningCirclePrefab != null)
            {
                warning = Instantiate(warningCirclePrefab, targetPosition, Quaternion.identity);
                warning.transform.localScale = Vector3.one * explosionRadius;
            }

            yield return new WaitForSeconds(warningDuration);

            if (warning != null)
            {
                Destroy(warning);
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(targetPosition, explosionRadius);
            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent<IDamageable>(out IDamageable damageable))
                {
                    if (damageable == bossController.PlayerCharacter)
                    {
                        bossController.PlayerCharacter.TakeDamage(explosionDamage);
                    }
                }
            }
        }
    }
}