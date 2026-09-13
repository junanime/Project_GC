using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class PoisonStatus : MonoBehaviour
    {
        private Monster targetMonster;
        private Coroutine poisonCoroutine;
        private SyringeAugmentVfx poisonVisual;

        private void ReleaseVisual()
        {
            if (poisonVisual != null) poisonVisual.Release();
            poisonVisual = null;
            if (targetMonster != null) targetMonster.OnKilled.RemoveListener(OnTargetKilled);
        }

        private void OnTargetKilled(Monster monster) { ReleaseVisual(); }

        private void LateUpdate()
        {
            if (poisonVisual != null && (targetMonster == null || targetMonster.HP <= 0f)) ReleaseVisual();
        }

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

            if (poisonVisual == null && targetMonster.HP > 0f && targetMonster.gameObject.activeInHierarchy)
            {
                var renderer = SyringeAugmentVfx.FindTarget(targetMonster);
                if (renderer != null)
                    poisonVisual = SyringeAugmentVfx.Play("Poison", renderer.bounds.center, renderer);
                targetMonster.OnKilled.RemoveListener(OnTargetKilled);
                targetMonster.OnKilled.AddListener(OnTargetKilled);
            }

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
                    ReleaseVisual();
                    yield break;
                }

                targetMonster.TakeDamage(tickDamage);
                elapsed += tickInterval;
            }

            poisonActive = false;
            poisonCoroutine = null;
            ReleaseVisual();
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
            ReleaseVisual();
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
            ReleaseVisual();
            TryTriggerContagion();
        }
    }
}
