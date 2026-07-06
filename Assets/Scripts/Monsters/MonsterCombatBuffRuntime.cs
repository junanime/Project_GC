using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 버퍼 몬스터가 주변 몬스터에게 부여한 전투 버프를 관리합니다.
    ///
    /// 이동속도 버프는 Monster.moveSpeed를 직접 조절하고,
    /// 피해 감소 버프는 Monster.TakeDamage()에서 ModifyIncomingDamage()를 호출해 적용합니다.
    ///
    /// 풀링으로 몬스터가 재사용될 때 버프가 남지 않도록 OnDisable에서 이동속도를 원복합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MonsterCombatBuffRuntime : MonoBehaviour
    {
        private Monster monster;

        private bool moveSpeedBuffActive;
        private float baseMoveSpeedBeforeBuff;
        private float moveSpeedBonusRatio;
        private float moveSpeedBuffRemainingTime;

        private bool damageReductionBuffActive;
        private float damageReductionRatio;
        private float damageReductionBuffRemainingTime;

        private void Awake()
        {
            monster = GetComponent<Monster>();
        }

        private void Update()
        {
            TickMoveSpeedBuff();
            TickDamageReductionBuff();
        }

        public void ApplyMoveSpeedBuff(
            float bonusRatio,
            float duration,
            string symbol,
            Color color,
            float iconOrbitRadius,
            float iconYOffset,
            float iconOrbitSpeed,
            int iconSortingOrder,
            float iconFontSize)
        {
            if (monster == null)
            {
                monster = GetComponent<Monster>();
            }

            if (monster == null)
            {
                return;
            }

            if (!moveSpeedBuffActive)
            {
                baseMoveSpeedBeforeBuff = monster.moveSpeed;
                moveSpeedBuffActive = true;
            }

            moveSpeedBonusRatio = Mathf.Max(0f, bonusRatio);
            moveSpeedBuffRemainingTime = Mathf.Max(0.1f, duration);

            monster.moveSpeed = baseMoveSpeedBeforeBuff * (1f + moveSpeedBonusRatio);

            MonsterSupportBuffVisual visual =
                MonsterSupportBuffVisual.GetOrCreate(transform, "Move Speed Buff Visual");

            visual.Play(
                symbol,
                color,
                moveSpeedBuffRemainingTime,
                iconOrbitRadius,
                iconYOffset,
                iconOrbitSpeed,
                iconSortingOrder,
                iconFontSize);
        }

        public void ApplyDamageReductionBuff(
            float reductionRatio,
            float duration,
            string symbol,
            Color color,
            float iconOrbitRadius,
            float iconYOffset,
            float iconOrbitSpeed,
            int iconSortingOrder,
            float iconFontSize)
        {
            damageReductionRatio = Mathf.Clamp01(reductionRatio);
            damageReductionBuffRemainingTime = Mathf.Max(0.1f, duration);
            damageReductionBuffActive = true;

            MonsterSupportBuffVisual visual =
                MonsterSupportBuffVisual.GetOrCreate(transform, "Damage Reduction Buff Visual");

            visual.Play(
                symbol,
                color,
                damageReductionBuffRemainingTime,
                iconOrbitRadius,
                iconYOffset,
                iconOrbitSpeed,
                iconSortingOrder,
                iconFontSize);
        }

        public float ModifyIncomingDamage(float incomingDamage)
        {
            if (!damageReductionBuffActive)
            {
                return incomingDamage;
            }

            float multiplier = 1f - Mathf.Clamp01(damageReductionRatio);
            return incomingDamage * multiplier;
        }

        private void TickMoveSpeedBuff()
        {
            if (!moveSpeedBuffActive)
            {
                return;
            }

            moveSpeedBuffRemainingTime -= Time.deltaTime;

            if (moveSpeedBuffRemainingTime > 0f)
            {
                return;
            }

            if (monster != null)
            {
                monster.moveSpeed = baseMoveSpeedBeforeBuff;
            }

            moveSpeedBuffActive = false;
            moveSpeedBonusRatio = 0f;
            moveSpeedBuffRemainingTime = 0f;
        }

        private void TickDamageReductionBuff()
        {
            if (!damageReductionBuffActive)
            {
                return;
            }

            damageReductionBuffRemainingTime -= Time.deltaTime;

            if (damageReductionBuffRemainingTime > 0f)
            {
                return;
            }

            damageReductionBuffActive = false;
            damageReductionRatio = 0f;
            damageReductionBuffRemainingTime = 0f;
        }

        private void OnDisable()
        {
            if (moveSpeedBuffActive && monster != null)
            {
                monster.moveSpeed = baseMoveSpeedBeforeBuff;
            }

            moveSpeedBuffActive = false;
            damageReductionBuffActive = false;
        }
    }
}