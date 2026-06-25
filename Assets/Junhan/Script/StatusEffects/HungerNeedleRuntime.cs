using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 공복침 런타임.
    /// 침 적중 시 플레이어별 공복 스택을 쌓고,
    /// 일정 시간 동안 추가 적중이 없으면 모든 스택을 초기화합니다.
    ///
    /// 기존 방식:
    /// - 각 스택이 개별 만료시간을 가짐.
    ///
    /// 변경 방식:
    /// - 적중할수록 스택 증가.
    /// - 최대 스택 도달 후에도 계속 적중하면 상태 유지.
    /// - 마지막 적중 후 resetDelay 동안 적중이 없으면 전체 초기화.
    /// </summary>
    public static class HungerNeedleRuntime
    {
        private class HungerState
        {
            public int stackCount = 0;

            public float lastHitTime = -999f;
            public float resetDelay = 3f;

            public float bonusAttackSpeedPerStack = 0.04f;
            public int maxStacks = 8;
            public bool debugLog = false;
        }

        private static readonly Dictionary<int, HungerState> stateByCharacterId =
            new Dictionary<int, HungerState>();

        public static void RegisterHit(
            Character playerCharacter,
            float resetDelay,
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

            state.resetDelay = Mathf.Max(0.1f, resetDelay);
            state.bonusAttackSpeedPerStack = Mathf.Max(0f, bonusAttackSpeedPerStack);
            state.maxStacks = Mathf.Max(1, maxStacks);
            state.debugLog = debugLog;

            ResetIfExpired(state);

            state.stackCount = Mathf.Clamp(state.stackCount + 1, 0, state.maxStacks);
            state.lastHitTime = Time.time;

            if (state.debugLog)
            {
                Debug.Log(
                    $"[공복침] 적중 스택 갱신 | " +
                    $"Stacks={state.stackCount}/{state.maxStacks} | " +
                    $"ResetDelay={state.resetDelay:0.##}s | " +
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

            ResetIfExpired(state);

            if (state.stackCount <= 0)
            {
                return 1f;
            }

            return 1f + state.bonusAttackSpeedPerStack * state.stackCount;
        }

        public static int GetCurrentStacks(Character playerCharacter)
        {
            if (playerCharacter == null)
            {
                return 0;
            }

            int id = playerCharacter.GetInstanceID();

            if (!stateByCharacterId.TryGetValue(id, out HungerState state))
            {
                return 0;
            }

            ResetIfExpired(state);
            return state.stackCount;
        }

        public static void Clear(Character playerCharacter)
        {
            if (playerCharacter == null)
            {
                return;
            }

            int id = playerCharacter.GetInstanceID();

            if (stateByCharacterId.TryGetValue(id, out HungerState state))
            {
                state.stackCount = 0;
                state.lastHitTime = -999f;
            }
        }

        private static void ResetIfExpired(HungerState state)
        {
            if (state == null)
            {
                return;
            }

            if (state.stackCount <= 0)
            {
                return;
            }

            if (Time.time - state.lastHitTime >= state.resetDelay)
            {
                if (state.debugLog)
                {
                    Debug.Log("[공복침] 3초 동안 적중 없음. 공복 스택 전체 초기화.");
                }

                state.stackCount = 0;
                state.lastHitTime = -999f;
            }
        }
    }
}