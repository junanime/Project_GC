using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 부식침 상태이상.
    /// 대상에게 받는 피해 증가 스택을 부여하고,
    /// 상태가 유지되는 동안 몬스터 머리 위에 보라색 소용돌이 표시를 생성합니다.
    /// </summary>
    public class CorrosionStatus : MonoBehaviour
    {
        private readonly List<float> stackExpireTimes = new List<float>();

        private float normalBonusPerStack = 0.15f;
        private float bossBonusPerStack = 0.05f;
        private int maxStacks = 3;
        private bool isBossTarget = false;

        private CorrosionStatusIcon iconInstance;

        public int CurrentStacks
        {
            get
            {
                RemoveExpiredStacks();
                return stackExpireTimes.Count;
            }
        }

        public void Apply(
            float duration,
            float normalBonusPerStack,
            float bossBonusPerStack,
            int maxStacks,
            bool isBossTarget)
        {
            this.normalBonusPerStack = Mathf.Max(0f, normalBonusPerStack);
            this.bossBonusPerStack = Mathf.Max(0f, bossBonusPerStack);
            this.maxStacks = Mathf.Max(1, maxStacks);
            this.isBossTarget = isBossTarget;

            RemoveExpiredStacks();

            stackExpireTimes.Add(Time.time + Mathf.Max(0.1f, duration));
            stackExpireTimes.Sort();

            while (stackExpireTimes.Count > this.maxStacks)
            {
                stackExpireTimes.RemoveAt(0);
            }

            EnsureIconExists();
        }

        public float GetDamageTakenMultiplier()
        {
            RemoveExpiredStacks();

            if (stackExpireTimes.Count <= 0)
            {
                return 1f;
            }

            float bonusPerStack = isBossTarget ? bossBonusPerStack : normalBonusPerStack;
            return 1f + bonusPerStack * stackExpireTimes.Count;
        }

        private void Update()
        {
            RemoveExpiredStacks();

            if (stackExpireTimes.Count <= 0)
            {
                CleanupIcon();
                Destroy(this);
            }
        }

        private void RemoveExpiredStacks()
        {
            float now = Time.time;

            for (int i = stackExpireTimes.Count - 1; i >= 0; i--)
            {
                if (stackExpireTimes[i] <= now)
                {
                    stackExpireTimes.RemoveAt(i);
                }
            }
        }

        private void EnsureIconExists()
        {
            if (iconInstance != null)
            {
                return;
            }

            GameObject iconObject = new GameObject("Corrosion Status Icon");
            iconObject.transform.SetParent(transform, false);
            iconObject.transform.localPosition = CalculateIconLocalOffset();
            iconObject.transform.localRotation = Quaternion.identity;
            iconObject.transform.localScale = Vector3.one;

            iconInstance = iconObject.AddComponent<CorrosionStatusIcon>();
        }

        private Vector3 CalculateIconLocalOffset()
        {
            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            float yOffset = 0.9f;

            if (spriteRenderer != null)
            {
                // bounds.extents.y는 월드 기준이지만, 현재 프로젝트 몬스터 스케일에서는 머리 위 표시 위치 잡기에 충분합니다.
                // 몬스터 크기가 크게 다른 경우 이 값을 인스펙터화하거나 몬스터별 오프셋 컴포넌트로 확장하면 됩니다.
                yOffset = spriteRenderer.bounds.extents.y + 0.35f;
            }

            return new Vector3(0f, yOffset, 0f);
        }

        private void CleanupIcon()
        {
            if (iconInstance != null)
            {
                Destroy(iconInstance.gameObject);
                iconInstance = null;
            }
        }

        private void OnDestroy()
        {
            CleanupIcon();
        }
    }
}