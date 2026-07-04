using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 전설증강 '헝그리정신'의 실제 런타임 컨트롤러입니다.
    /// 경험치/골드를 먹지 않는 시간이 길수록 스택을 쌓고,
    /// 픽업을 먹는 순간 누적 보너스를 전부 제거합니다.
    ///
    /// Character.cs를 직접 크게 수정하지 않고,
    /// 기존 public 메서드 AddDamageMultiplier / AddRangeBoost / AddAttackSpeed를 사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HungrySpiritController : MonoBehaviour
    {
        private Character playerCharacter;

        private float stackInterval;
        private int maxStacks;
        private float damageBonusPerStack;
        private float rangeBonusPerStack;
        private float attackSpeedBonusPerStack;
        private bool debugLog;

        private float noPickupTimer;
        private int currentStacks;

        private float appliedDamageBonus;
        private float appliedRangeBonus;
        private float appliedAttackSpeedBonus;

        private bool subscribed;

        public int CurrentStacks => currentStacks;

        public void Configure(
            Character playerCharacter,
            float stackInterval,
            int maxStacks,
            float damageBonusPerStack,
            float rangeBonusPerStack,
            float attackSpeedBonusPerStack,
            bool debugLog)
        {
            RemoveAllAppliedBonuses();

            this.playerCharacter = playerCharacter;
            this.stackInterval = Mathf.Max(0.1f, stackInterval);
            this.maxStacks = Mathf.Max(1, maxStacks);
            this.damageBonusPerStack = Mathf.Max(0f, damageBonusPerStack);
            this.rangeBonusPerStack = Mathf.Max(0f, rangeBonusPerStack);
            this.attackSpeedBonusPerStack = Mathf.Max(0f, attackSpeedBonusPerStack);
            this.debugLog = debugLog;

            noPickupTimer = 0f;
            currentStacks = 0;

            if (!subscribed)
            {
                HungrySpiritPickupSignal.OnExpOrCoinPickedUp += HandleExpOrCoinPickedUp;
                subscribed = true;
            }

            if (debugLog)
            {
                Debug.Log("[헝그리정신] 컨트롤러 설정 완료", this);
            }
        }

        private void Update()
        {
            if (playerCharacter == null)
            {
                return;
            }

            if (currentStacks >= maxStacks)
            {
                return;
            }

            noPickupTimer += Time.deltaTime;

            while (noPickupTimer >= stackInterval && currentStacks < maxStacks)
            {
                noPickupTimer -= stackInterval;
                SetStacks(currentStacks + 1);
            }
        }

        private void HandleExpOrCoinPickedUp(Character collector)
        {
            if (collector == null || collector != playerCharacter)
            {
                return;
            }

            if (currentStacks > 0 && debugLog)
            {
                Debug.Log($"[헝그리정신] 픽업 획득으로 스택 초기화 | 기존 스택={currentStacks}", this);
            }

            noPickupTimer = 0f;
            SetStacks(0);
        }

        private void SetStacks(int newStackCount)
        {
            newStackCount = Mathf.Clamp(newStackCount, 0, maxStacks);

            if (newStackCount == currentStacks)
            {
                return;
            }

            int deltaStacks = newStackCount - currentStacks;
            currentStacks = newStackCount;

            ApplyStackDelta(deltaStacks);

            if (debugLog)
            {
                Debug.Log(
                    $"[헝그리정신] 스택 변경 | 현재={currentStacks}/{maxStacks} | " +
                    $"공격력 +{appliedDamageBonus * 100f:0.#}% | " +
                    $"사거리 +{appliedRangeBonus * 100f:0.#}% | " +
                    $"공속 +{appliedAttackSpeedBonus * 100f:0.#}%",
                    this);
            }
        }

        private void ApplyStackDelta(int deltaStacks)
        {
            if (playerCharacter == null || deltaStacks == 0)
            {
                return;
            }

            float damageDelta = damageBonusPerStack * deltaStacks;
            float rangeDelta = rangeBonusPerStack * deltaStacks;
            float attackSpeedDelta = attackSpeedBonusPerStack * deltaStacks;

            playerCharacter.AddDamageMultiplier(damageDelta);
            playerCharacter.AddRangeBoost(rangeDelta);
            playerCharacter.AddAttackSpeed(attackSpeedDelta);

            appliedDamageBonus += damageDelta;
            appliedRangeBonus += rangeDelta;
            appliedAttackSpeedBonus += attackSpeedDelta;
        }

        private void RemoveAllAppliedBonuses()
        {
            if (playerCharacter == null)
            {
                appliedDamageBonus = 0f;
                appliedRangeBonus = 0f;
                appliedAttackSpeedBonus = 0f;
                currentStacks = 0;
                return;
            }

            if (!Mathf.Approximately(appliedDamageBonus, 0f))
            {
                playerCharacter.AddDamageMultiplier(-appliedDamageBonus);
            }

            if (!Mathf.Approximately(appliedRangeBonus, 0f))
            {
                playerCharacter.AddRangeBoost(-appliedRangeBonus);
            }

            if (!Mathf.Approximately(appliedAttackSpeedBonus, 0f))
            {
                playerCharacter.AddAttackSpeed(-appliedAttackSpeedBonus);
            }

            appliedDamageBonus = 0f;
            appliedRangeBonus = 0f;
            appliedAttackSpeedBonus = 0f;
            currentStacks = 0;
        }

        private void OnDisable()
        {
            RemoveAllAppliedBonuses();
        }

        private void OnDestroy()
        {
            if (subscribed)
            {
                HungrySpiritPickupSignal.OnExpOrCoinPickedUp -= HandleExpOrCoinPickedUp;
                subscribed = false;
            }

            RemoveAllAppliedBonuses();
        }
    }
}