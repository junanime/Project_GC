using System;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 크리피커피의 Single / Dual / Rainbow Core 상태만 관리합니다.
    ///
    /// 이 스크립트는 패턴 자체를 실행하지 않습니다.
    /// BossController가 현재 ActiveTraits를 읽어 패턴 후보를 필터링하도록
    /// 다음 단계에서 연결합니다.
    ///
    /// 패턴 실행 중 Core Swap을 막기 위해 SetPatternLocked를 제공합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossCoreStateController : MonoBehaviour
    {
        [Header("Single Core")]
        [Tooltip("1페이즈 시작 시 사용할 첫 싱글코어 색입니다.")]
        [SerializeField]
        private BossCoreTrait startingSingleCore = BossCoreTrait.Red;

        [Tooltip("싱글코어가 유지되는 최소 시간입니다.")]
        [SerializeField, Min(0.1f)]
        private float singleCoreDurationMin = 10f;

        [Tooltip("싱글코어가 유지되는 최대 시간입니다.")]
        [SerializeField, Min(0.1f)]
        private float singleCoreDurationMax = 12f;

        [Header("Dual Core")]
        [Tooltip("듀얼코어가 유지되는 최소 시간입니다.")]
        [SerializeField, Min(0.1f)]
        private float dualCoreDurationMin = 12f;

        [Tooltip("듀얼코어가 유지되는 최대 시간입니다.")]
        [SerializeField, Min(0.1f)]
        private float dualCoreDurationMax = 15f;

        [Header("Rainbow Core")]
        [Tooltip("무지개코어에서 특정 색상 부분이 강화되기까지의 최소 대기시간입니다.")]
        [SerializeField, Min(0.1f)]
        private float rainbowHighlightIntervalMin = 7f;

        [Tooltip("무지개코어에서 특정 색상 부분이 강화되기까지의 최대 대기시간입니다.")]
        [SerializeField, Min(0.1f)]
        private float rainbowHighlightIntervalMax = 9f;

        [Tooltip("무지개코어의 특정 색상 강화가 유지되는 시간입니다.")]
        [SerializeField, Min(0.1f)]
        private float rainbowHighlightDuration = 3.5f;

        [Header("Runtime")]
        [Tooltip("현재 코어 모드입니다. 런타임 확인용입니다.")]
        [SerializeField]
        private BossCoreMode currentMode = BossCoreMode.Single;

        [Tooltip("현재 활성 코어 속성입니다. 런타임 확인용입니다.")]
        [SerializeField]
        private BossCoreTrait activeTraits = BossCoreTrait.Red;

        [Tooltip("3페이즈에서 현재 강화 발광 중인 속성입니다. None이면 평상 무지개 상태입니다.")]
        [SerializeField]
        private BossCoreTrait rainbowHighlightedTraits = BossCoreTrait.None;

        [Tooltip("패턴 실행 중 Core Swap을 보류하고 있는지 표시합니다.")]
        [SerializeField]
        private bool patternLocked;

        [Tooltip("교체 시간이 지났지만 패턴 때문에 Core Swap이 대기 중인지 표시합니다.")]
        [SerializeField]
        private bool swapPending;

        [Header("Debug")]
        [Tooltip("코어 변경 및 무지개 발광 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog = false;

        private bool initialized;
        private int currentPhase = 1;
        private float nextStateChangeTime;
        private float rainbowHighlightEndTime;

        public BossCoreMode CurrentMode => currentMode;
        public BossCoreTrait ActiveTraits => activeTraits;
        public BossCoreTrait RainbowHighlightedTraits => rainbowHighlightedTraits;
        public bool IsRainbowHighlightActive =>
            currentMode == BossCoreMode.Rainbow &&
            rainbowHighlightedTraits != BossCoreTrait.None;

        public event Action<BossCoreMode, BossCoreTrait> CoreChanged;
        public event Action<BossCoreTrait> RainbowHighlightChanged;

        public void Initialize(int phase)
        {
            initialized = true;
            SetPhase(phase, true);
        }

        public void SetPhase(int phase, bool forceImmediate = false)
        {
            currentPhase = Mathf.Clamp(phase, 1, 3);
            swapPending = false;
            rainbowHighlightedTraits = BossCoreTrait.None;

            switch (currentPhase)
            {
                case 1:
                    currentMode = BossCoreMode.Single;
                    activeTraits = NormalizeStartingSingle(startingSingleCore);
                    ScheduleNextSingleSwap();
                    break;

                case 2:
                    currentMode = BossCoreMode.Dual;
                    activeTraits = GetRandomDual(BossCoreTrait.None);
                    ScheduleNextDualSwap();
                    break;

                default:
                    currentMode = BossCoreMode.Rainbow;
                    activeTraits = BossCoreTrait.All;
                    ScheduleNextRainbowHighlight();
                    break;
            }

            if (forceImmediate)
            {
                patternLocked = false;
            }

            NotifyCoreChanged();
        }

        public void SetPatternLocked(bool locked)
        {
            patternLocked = locked;

            if (!patternLocked && swapPending)
            {
                swapPending = false;
                AdvanceCoreState();
            }
        }

        public bool IsTraitActive(BossCoreTrait traits)
        {
            return BossCoreTraitUtility.Matches(
                activeTraits,
                traits,
                BossPatternCoreMatchMode.Any);
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            if (currentMode == BossCoreMode.Rainbow)
            {
                UpdateRainbowState();
                return;
            }

            if (Time.time < nextStateChangeTime)
            {
                return;
            }

            if (patternLocked)
            {
                swapPending = true;
                return;
            }

            AdvanceCoreState();
        }

        private void AdvanceCoreState()
        {
            if (currentMode == BossCoreMode.Single)
            {
                activeTraits = GetRandomSingle(activeTraits);
                ScheduleNextSingleSwap();
                NotifyCoreChanged();
                return;
            }

            if (currentMode == BossCoreMode.Dual)
            {
                activeTraits = GetRandomDual(activeTraits);
                ScheduleNextDualSwap();
                NotifyCoreChanged();
            }
        }

        private void UpdateRainbowState()
        {
            if (rainbowHighlightedTraits != BossCoreTrait.None)
            {
                if (Time.time >= rainbowHighlightEndTime)
                {
                    rainbowHighlightedTraits = BossCoreTrait.None;
                    RainbowHighlightChanged?.Invoke(rainbowHighlightedTraits);
                    ScheduleNextRainbowHighlight();

                    if (debugLog)
                    {
                        Debug.Log("[BossCore] Rainbow Highlight 종료", this);
                    }
                }

                return;
            }

            if (Time.time < nextStateChangeTime)
            {
                return;
            }

            if (patternLocked)
            {
                swapPending = true;
                return;
            }

            swapPending = false;
            rainbowHighlightedTraits = GetRandomRainbowHighlight();
            rainbowHighlightEndTime = Time.time + Mathf.Max(0.1f, rainbowHighlightDuration);
            RainbowHighlightChanged?.Invoke(rainbowHighlightedTraits);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCore] Rainbow Highlight 시작 | {rainbowHighlightedTraits}",
                    this);
            }
        }

        private void ScheduleNextSingleSwap()
        {
            nextStateChangeTime =
                Time.time + RandomRangeSafe(singleCoreDurationMin, singleCoreDurationMax);
        }

        private void ScheduleNextDualSwap()
        {
            nextStateChangeTime =
                Time.time + RandomRangeSafe(dualCoreDurationMin, dualCoreDurationMax);
        }

        private void ScheduleNextRainbowHighlight()
        {
            nextStateChangeTime =
                Time.time + RandomRangeSafe(
                    rainbowHighlightIntervalMin,
                    rainbowHighlightIntervalMax);
        }

        private float RandomRangeSafe(float a, float b)
        {
            float min = Mathf.Max(0.1f, Mathf.Min(a, b));
            float max = Mathf.Max(min, Mathf.Max(a, b));
            return UnityEngine.Random.Range(min, max);
        }

        private BossCoreTrait NormalizeStartingSingle(BossCoreTrait value)
        {
            if (BossCoreTraitUtility.IsSingle(value))
            {
                return value;
            }

            return BossCoreTrait.Red;
        }

        private BossCoreTrait GetRandomSingle(BossCoreTrait exclude)
        {
            BossCoreTrait[] candidates =
            {
                BossCoreTrait.Red,
                BossCoreTrait.Yellow,
                BossCoreTrait.Blue
            };

            return GetRandomDifferent(candidates, exclude);
        }

        private BossCoreTrait GetRandomDual(BossCoreTrait exclude)
        {
            BossCoreTrait[] candidates =
            {
                BossCoreTrait.Red | BossCoreTrait.Yellow,
                BossCoreTrait.Red | BossCoreTrait.Blue,
                BossCoreTrait.Yellow | BossCoreTrait.Blue
            };

            return GetRandomDifferent(candidates, exclude);
        }

        private BossCoreTrait GetRandomRainbowHighlight()
        {
            BossCoreTrait[] candidates =
            {
                BossCoreTrait.Red,
                BossCoreTrait.Yellow,
                BossCoreTrait.Blue,
                BossCoreTrait.Red | BossCoreTrait.Yellow,
                BossCoreTrait.Red | BossCoreTrait.Blue,
                BossCoreTrait.Yellow | BossCoreTrait.Blue
            };

            return candidates[UnityEngine.Random.Range(0, candidates.Length)];
        }

        private BossCoreTrait GetRandomDifferent(
            BossCoreTrait[] candidates,
            BossCoreTrait exclude)
        {
            if (candidates == null || candidates.Length == 0)
            {
                return BossCoreTrait.None;
            }

            if (candidates.Length == 1)
            {
                return candidates[0];
            }

            BossCoreTrait selected = candidates[0];

            for (int attempt = 0; attempt < 8; attempt++)
            {
                selected = candidates[UnityEngine.Random.Range(0, candidates.Length)];

                if (selected != exclude)
                {
                    return selected;
                }
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != exclude)
                {
                    return candidates[i];
                }
            }

            return selected;
        }

        private void NotifyCoreChanged()
        {
            CoreChanged?.Invoke(currentMode, activeTraits);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCore] Core 변경 | Phase={currentPhase}, Mode={currentMode}, Traits={activeTraits}",
                    this);
            }
        }
    }
}
