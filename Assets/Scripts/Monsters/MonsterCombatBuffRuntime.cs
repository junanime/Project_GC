using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 버퍼 몬스터가 주변 몬스터에게 부여한 전투 버프를 관리합니다.
    ///
    /// 이동속도 버프는 Monster.moveSpeed를 직접 조절하고,
    /// 체력 버프는 최대/현재 체력을 같은 비율로 확대하며 갱신 시 회복하지 않습니다.
    /// 기존 피해 감소 API는 다른 호출자와의 호환을 위해 유지합니다.
    ///
    /// 풀링으로 몬스터가 재사용될 때 버프가 남지 않도록 OnDisable에서 이동속도를 원복합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MonsterCombatBuffRuntime : MonoBehaviour
    {
        private Monster monster;
        public bool HealthBuffActive {get;private set;} float healthRemaining;
        public void ApplyHealthBuff(float duration){if(monster==null)monster=GetComponent<Monster>();if(monster==null||monster.HP<=0)return;if(!HealthBuffActive){monster.ScaleHealthCapacity(1.25f);HealthBuffActive=true;}healthRemaining=duration;
            var visual=MonsterSupportBuffVisual.GetOrCreate(transform,"Health Buff Visual");visual.Play("+",new Color(1,.25f,.3f),duration,.25f,monster.Size.y+.15f,3,160,2.5f);
        }
        public void ClearBuffs(){if(HealthBuffActive&&monster!=null)monster.ScaleHealthCapacity(.8f);HealthBuffActive=false;healthRemaining=0;if(moveSpeedBuffActive&&monster!=null)monster.moveSpeed=baseMoveSpeedBeforeBuff;moveSpeedBuffActive=damageReductionBuffActive=false;}

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
            if (monster == null)
            {
                monster = GetComponent<Monster>();
            }

            if(monster!=null&&monster.IsFieldRuntimeSuspended)return;
            if(HealthBuffActive&&(healthRemaining-=Time.deltaTime)<=0){monster.ScaleHealthCapacity(.8f);HealthBuffActive=false;}
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
            ClearBuffs();
            if (moveSpeedBuffActive && monster != null)
            {
                monster.moveSpeed = baseMoveSpeedBeforeBuff;
            }

            moveSpeedBuffActive = false;
            damageReductionBuffActive = false;
        }
    }
}