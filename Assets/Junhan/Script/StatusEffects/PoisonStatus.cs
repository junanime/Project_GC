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

        private void Awake()
        {
            targetMonster = GetComponent<Monster>() ?? GetComponentInParent<Monster>();
        }

        public void Apply(float duration, float tickInterval, float tickDamage)
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

            poisonActive = true;
            contagionAlreadyTriggered = false;

            if (poisonCoroutine != null)
            {
                StopCoroutine(poisonCoroutine);
            }

            poisonCoroutine = StartCoroutine(PoisonRoutine(currentDuration, currentTickInterval, currentTickDamage));
        }

        private IEnumerator PoisonRoutine(float duration, float tickInterval, float tickDamage)
        {
            float elapsed = 0f;

            if (targetMonster != null && targetMonster.gameObject.activeInHierarchy)
            {
                targetMonster.TakeDamage(tickDamage);
            }

            while (elapsed < duration)
            {
                yield return new WaitForSeconds(tickInterval);

                if (targetMonster == null || !targetMonster.gameObject.activeInHierarchy)
                {
                    yield break;
                }

                targetMonster.TakeDamage(tickDamage);
                elapsed += tickInterval;
            }

            poisonActive = false;
            poisonCoroutine = null;
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
                currentTickDamage
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