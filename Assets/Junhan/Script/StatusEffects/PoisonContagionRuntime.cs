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
            PoisonContagionRuntime.durationMultiplier =
                Mathf.Max(0.05f, durationMultiplier);
            PoisonContagionRuntime.damageMultiplier =
                Mathf.Max(0.05f, damageMultiplier);
            monsterLayer = layer;
            debugLog = debug;
        }

        public static void Disable()
        {
            Enabled = false;
        }

        /// <summary>
        /// 기존 호출부 호환용.
        /// sourceCharacter가 없는 이전 코드도 컴파일되도록 유지한다.
        /// </summary>
        public static void TrySpreadFrom(
            Monster sourceMonster,
            float sourceDuration,
            float sourceTickInterval,
            float sourceTickDamage)
        {
            TrySpreadFrom(
                sourceMonster,
                sourceDuration,
                sourceTickInterval,
                sourceTickDamage,
                null
            );
        }

        /// <summary>
        /// 독 전염을 주변 몬스터에게 퍼뜨린다.
        /// 퍼진 독의 실제 틱 피해는 PoisonStatus가 처리하며,
        /// 피해 출처는 "독 전염"으로 고정해서 기록한다.
        /// </summary>
        public static void TrySpreadFrom(
            Monster sourceMonster,
            float sourceDuration,
            float sourceTickInterval,
            float sourceTickDamage,
            Character sourceCharacter)
        {
            if (!Enabled)
            {
                return;
            }

            if (sourceMonster == null)
            {
                return;
            }

            Vector2 sourcePosition =
                sourceMonster.CenterTransform != null
                    ? (Vector2)sourceMonster.CenterTransform.position
                    : (Vector2)sourceMonster.transform.position;

            int appliedCount = 0;

            Collider2D[] hits =
                Physics2D.OverlapCircleAll(
                    sourcePosition,
                    contagionRadius,
                    monsterLayer
                );

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];

                if (hit == null)
                {
                    continue;
                }

                Monster targetMonster =
                    hit.GetComponentInParent<Monster>();

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

                if (targetMonster.HP <= 0f)
                {
                    continue;
                }

                PoisonStatus poisonStatus =
                    targetMonster.GetComponent<PoisonStatus>();

                if (poisonStatus == null)
                {
                    poisonStatus =
                        targetMonster.gameObject.AddComponent<PoisonStatus>();
                }

                poisonStatus.Apply(
                    sourceDuration * durationMultiplier,
                    sourceTickInterval,
                    sourceTickDamage * damageMultiplier,
                    sourceCharacter,
                    "독 전염"
                );

                appliedCount++;
            }

            if (debugLog && appliedCount > 0)
            {
                Debug.Log(
                    $"[독 전염] {sourceMonster.name} 기준 주변 {appliedCount}마리에게 독 전염"
                );
            }
        }
    }
}
