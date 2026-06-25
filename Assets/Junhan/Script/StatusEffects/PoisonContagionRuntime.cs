using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 전설증강: 독 전염
    /// 독에 걸린 몬스터가 비활성화/사망할 때 주변 몬스터에게 독을 전염시키는 전역 런타임 설정.
    /// </summary>
    public static class PoisonContagionRuntime
    {
        public static bool Enabled { get; private set; }

        private static float contagionRadius = 1f;
        private static float durationMultiplier = 1f;
        private static float damageMultiplier = 1f;
        private static LayerMask monsterLayer;
        private static bool debugLog = false;

        public static void Enable(
            float radius,
            float durationMultiplier,
            float damageMultiplier,
            LayerMask layer,
            bool debug)
        {
            Enabled = true;
            contagionRadius = Mathf.Max(0.05f, radius);
            PoisonContagionRuntime.durationMultiplier = Mathf.Max(0.05f, durationMultiplier);
            PoisonContagionRuntime.damageMultiplier = Mathf.Max(0.05f, damageMultiplier);
            monsterLayer = layer;
            debugLog = debug;
        }

        public static void Disable()
        {
            Enabled = false;
        }

        public static void TrySpreadFrom(
            Monster sourceMonster,
            float sourceDuration,
            float sourceTickInterval,
            float sourceTickDamage)
        {
            if (!Enabled)
            {
                return;
            }

            if (sourceMonster == null)
            {
                return;
            }

            Vector2 sourcePosition = sourceMonster.CenterTransform != null
                ? (Vector2)sourceMonster.CenterTransform.position
                : (Vector2)sourceMonster.transform.position;

            int appliedCount = 0;

            Collider2D[] hits = Physics2D.OverlapCircleAll(sourcePosition, contagionRadius, monsterLayer);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];

                if (hit == null)
                {
                    continue;
                }

                Monster targetMonster = hit.GetComponentInParent<Monster>();

                if (targetMonster == null)
                {
                    continue;
                }

                if (targetMonster == sourceMonster)
                {
                    continue;
                }

                if (!targetMonster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                PoisonStatus poisonStatus = targetMonster.GetComponent<PoisonStatus>();

                if (poisonStatus == null)
                {
                    poisonStatus = targetMonster.gameObject.AddComponent<PoisonStatus>();
                }

                poisonStatus.Apply(
                    sourceDuration * durationMultiplier,
                    sourceTickInterval,
                    sourceTickDamage * damageMultiplier
                );

                appliedCount++;
            }

            if (debugLog && appliedCount > 0)
            {
                Debug.Log($"[독 전염] {sourceMonster.name} 기준 주변 {appliedCount}마리에게 독 전염");
            }
        }
    }
}