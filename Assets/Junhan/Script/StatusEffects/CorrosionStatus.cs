using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // 부식침 상태이상
    // 대상에게 받는 피해 증가 스택을 부여한다.
    public class CorrosionStatus : MonoBehaviour
    {
        private readonly List<float> stackExpireTimes = new List<float>();

        private float normalBonusPerStack = 0.15f;
        private float bossBonusPerStack = 0.05f;
        private int maxStacks = 3;
        private bool isBossTarget = false;

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
    }
}