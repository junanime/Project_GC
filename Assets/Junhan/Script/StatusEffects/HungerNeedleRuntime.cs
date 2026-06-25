using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 공복침 런타임.
    /// 침 적중 시 플레이어별 공복 스택을 쌓고,
    /// SyringeDartAbility.GetEffectiveCooldown()에서 공격속도 배율로 사용합니다.
    /// </summary>
    public static class HungerNeedleRuntime
    {
        private class HungerState
        {
            public readonly List<float> expireTimes = new List<float>();

            public float bonusAttackSpeedPerStack = 0.04f;
            public int maxStacks = 8;
            public bool debugLog = false;
        }

        private static readonly Dictionary<int, HungerState> stateByCharacterId = new Dictionary<int, HungerState>();

        public static void RegisterHit(
            Character playerCharacter,
            float stackDuration,
            float bonusAttackSpeedPerStack,
            int maxStacks,
            bool debugLog)
        {
            if (playerCharacter == null)
            {
                return;
            }

            int id = playerCharacter.GetInstanceID();

            if (!stateByCharacterId.TryGetValue(id, out HungerState state))
            {
                state = new HungerState();
                stateByCharacterId[id] = state;
            }

            state.bonusAttackSpeedPerStack = Mathf.Max(0f, bonusAttackSpeedPerStack);
            state.maxStacks = Mathf.Max(1, maxStacks);
            state.debugLog = debugLog;

            RemoveExpiredStacks(state);

            state.expireTimes.Add(Time.time + Mathf.Max(0.1f, stackDuration));
            state.expireTimes.Sort();

            while (state.expireTimes.Count > state.maxStacks)
            {
                state.expireTimes.RemoveAt(0);
            }

            if (state.debugLog)
            {
                Debug.Log(
                    $"[공복침] 적중 스택 증가 | Stacks={state.expireTimes.Count}/{state.maxStacks} | " +
                    $"AttackSpeedBonus={(GetAttackSpeedMultiplier(playerCharacter) - 1f) * 100f:0.#}%"
                );
            }
        }

        public static float GetAttackSpeedMultiplier(Character playerCharacter)
        {
            if (playerCharacter == null)
            {
                return 1f;
            }

            int id = playerCharacter.GetInstanceID();

            if (!stateByCharacterId.TryGetValue(id, out HungerState state))
            {
                return 1f;
            }

            RemoveExpiredStacks(state);

            if (state.expireTimes.Count <= 0)
            {
                return 1f;
            }

            return 1f + state.bonusAttackSpeedPerStack * state.expireTimes.Count;
        }

        private static void RemoveExpiredStacks(HungerState state)
        {
            if (state == null)
            {
                return;
            }

            float now = Time.time;

            for (int i = state.expireTimes.Count - 1; i >= 0; i--)
            {
                if (state.expireTimes[i] <= now)
                {
                    state.expireTimes.RemoveAt(i);
                }
            }
        }
    }
}