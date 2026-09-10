using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class PoisonStatus : MonoBehaviour
    {
        private Monster targetMonster;
        private Coroutine poisonCoroutine;

        private bool poisonActive = false;
        private bool contagionAlreadyTriggered = false;

        private float currentDuration = 0f;
        private float currentTickInterval = 0.5f;
        private float currentTickDamage = 1f;

        // 독 피해의 원인을 기억한다.
        // 기본값은 독침이며, 이후 독 전염 쪽에서 "독 전염"으로 넘겨줄 수 있다.
        private Character sourceCharacter;
        private string currentDamageSourceName = "독침";

        private void Awake()
        {
            targetMonster = GetComponent<Monster>() ?? GetComponentInParent<Monster>();
        }

        /// <summary>
        /// 기존 호출부 호환용 Apply.
        /// 아직 sourceCharacter를 넘기지 않는 코드가 있어도 컴파일 오류가 나지 않는다.
        /// 피해는 기본적으로 "독침"으로 기록한다.
        /// </summary>
        public void Apply(float duration, float tickInterval, float tickDamage)
        {
            Apply(
                duration,
                tickInterval,
                tickDamage,
                null,
                "독침"
            );
        }

        /// <summary>
        /// 독 피해를 적용한다.
        /// sourceCharacter를 전달하면 기존 전체 피해량(OnDealDamage)에도 포함되고,
        /// damageSourceName으로 AugmentDamageTracker에 기록된다.
        /// </summary>
        public void Apply(
            float duration,
            float tickInterval,
            float tickDamage,
            Character sourceCharacter,
            string damageSourceName)
        {
            if (targetMonster == null)
            {
                targetMonster = GetComponent<Monster>() ?? GetComponentInParent<Monster>();
            }

            if (targetMonster == null)
            {
                return;
            }

            currentDuration = Mathf.Max(0.05f, duration);
            currentTickInterval = Mathf.Max(0.05f, tickInterval);
            currentTickDamage = Mathf.Max(0f, tickDamage);

            this.sourceCharacter = sourceCharacter;
            currentDamageSourceName =
                string.IsNullOrWhiteSpace(damageSourceName)
                    ? "독침"
                    : damageSourceName;

            poisonActive = true;
            contagionAlreadyTriggered = false;

            if (poisonCoroutine != null)
            {
                StopCoroutine(poisonCoroutine);
            }

            poisonCoroutine = StartCoroutine(
                PoisonRoutine(
                    currentDuration,
                    currentTickInterval,
                    currentTickDamage
                )
            );
        }

        private IEnumerator PoisonRoutine(
            float duration,
            float tickInterval,
            float tickDamage)
        {
            float elapsed = 0f;

            // 기존 코드와 동일하게 독 적용 직후 첫 틱 피해가 들어간다.
            if (targetMonster != null &&
                targetMonster.gameObject.activeInHierarchy)
            {
                DealPoisonDamage(tickDamage);
            }

            while (elapsed < duration)
            {
                yield return new WaitForSeconds(tickInterval);

                if (targetMonster == null ||
                    !targetMonster.gameObject.activeInHierarchy)
                {
                    yield break;
                }

                DealPoisonDamage(tickDamage);
                elapsed += tickInterval;
            }

            poisonActive = false;
            poisonCoroutine = null;
        }

        /// <summary>
        /// 몬스터에게 실제로 넘기는 독 틱 피해값을
        /// 기존 전체 피해량과 증강별 피해량에 동일하게 기록한다.
        ///
        /// 현재 PoisonStatus의 실제 TakeDamage 값은 tickDamage 자체이므로
        /// 이것이 이 시스템에서의 최종 계산 피해량이다.
        /// </summary>
        private void DealPoisonDamage(float finalDamage)
        {
            if (finalDamage <= 0f)
            {
                return;
            }

            if (targetMonster == null ||
                !targetMonster.gameObject.activeInHierarchy)
            {
                return;
            }

            targetMonster.TakeDamage(finalDamage);

            // 기존 총 피해량 시스템에 포함
            if (sourceCharacter != null &&
                sourceCharacter.OnDealDamage != null)
            {
                sourceCharacter.OnDealDamage.Invoke(finalDamage);
            }

            // 가장 피해를 많이 준 증강 계산에 포함
            if (AugmentDamageTracker.Instance != null)
            {
                AugmentDamageTracker.Instance.RecordDamage(
                    currentDamageSourceName,
                    finalDamage
                );
            }
        }

        private void TryTriggerContagion()
        {
            if (!poisonActive)
            {
                return;
            }

            if (contagionAlreadyTriggered)
            {
                return;
            }

            contagionAlreadyTriggered = true;

            PoisonContagionRuntime.TrySpreadFrom(
                targetMonster,
                currentDuration,
                currentTickInterval,
                currentTickDamage,
                sourceCharacter
            );
        }

        private void OnDisable()
        {
            TryTriggerContagion();

            if (poisonCoroutine != null)
            {
                StopCoroutine(poisonCoroutine);
                poisonCoroutine = null;
            }

            poisonActive = false;
        }

        private void OnDestroy()
        {
            TryTriggerContagion();
        }
    }
}
