using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class PoisonStatus : MonoBehaviour
    {
        private Monster targetMonster;
        private Coroutine poisonCoroutine;

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

            if (poisonCoroutine != null)
            {
                StopCoroutine(poisonCoroutine);
            }

            poisonCoroutine = StartCoroutine(PoisonRoutine(duration, tickInterval, tickDamage));
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

            poisonCoroutine = null;
        }

        private void OnDisable()
        {
            if (poisonCoroutine != null)
            {
                StopCoroutine(poisonCoroutine);
                poisonCoroutine = null;
            }
        }
    }
}