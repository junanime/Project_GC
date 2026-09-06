using System;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 보스 파츠에 실제로 적용된 피해 정보입니다.
    ///
    /// 향후 Pressure Gauge에서는
    /// DamageSource가 PlayerProjectile / PlayerAbility인 경우만
    /// 집계하도록 연결할 수 있습니다.
    /// </summary>
    public struct BossDamageEventData
    {
        public BossPartDamageTestPart Part { get; private set; }

        public BossDamageSourceType DamageSource { get; private set; }

        public float RawDamage { get; private set; }

        public float ModifiedDamage { get; private set; }

        public float ActualDamage { get; private set; }

        public bool IsCritical { get; private set; }

        public BossDamageEventData(
            BossPartDamageTestPart part,
            BossDamageSourceType damageSource,
            float rawDamage,
            float modifiedDamage,
            float actualDamage,
            bool isCritical)
        {
            Part = part;
            DamageSource = damageSource;
            RawDamage = rawDamage;
            ModifiedDamage = modifiedDamage;
            ActualDamage = actualDamage;
            IsCritical = isCritical;
        }
    }

    /// <summary>
    /// 보스 파츠 피해 규칙을 중앙에서 관리합니다.
    ///
    /// 현재 역할:
    /// - Core Normal / Guarded = x1.0
    /// - Core Open = x1.5
    /// - Core Groggy = x2.0
    /// - 피해 출처 기록
    ///
    /// 기존 BossPartDamageTestRootController의
    /// HP 집계 방식을 수정하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossPartDamageRules : MonoBehaviour
    {
        [Header("Core Damage State / 코어 피해 상태")]

        [Tooltip(
            "현재 Core의 피해 상태입니다. " +
            "Normal/Guarded는 기본 피해, Open은 1.5배, Groggy는 2배를 기본값으로 사용합니다."
        )]
        [SerializeField]
        private BossCoreDamageState coreDamageState =
            BossCoreDamageState.Normal;

        [Header("Core Damage Multipliers / 코어 피해 배율")]

        [Tooltip("Core가 Guarded 상태일 때 받는 피해 배율입니다.")]
        [SerializeField, Min(0f)]
        private float guardedDamageMultiplier = 1f;

        [Tooltip("Core가 Normal 상태일 때 받는 피해 배율입니다.")]
        [SerializeField, Min(0f)]
        private float normalDamageMultiplier = 1f;

        [Tooltip("Core가 Open 상태일 때 받는 피해 배율입니다.")]
        [SerializeField, Min(0f)]
        private float openDamageMultiplier = 1.5f;

        [Tooltip("Core가 Groggy 상태일 때 받는 피해 배율입니다.")]
        [SerializeField, Min(0f)]
        private float groggyDamageMultiplier = 2f;

        [Header("Damage Source Rule / 피해 출처 규칙")]

        [Tooltip(
            "체크하면 Core 상태 피해 배율을 플레이어가 발생시킨 피해에만 적용합니다. " +
            "BossSelf, Environment, Scripted 피해에는 별도의 원래 피해값을 유지합니다."
        )]
        [SerializeField]
        private bool coreMultiplierPlayerDamageOnly = true;

        [Header("Debug")]

        [Tooltip(
            "체크하면 Core 상태 변경과 보스 피해 출처를 Console에 출력합니다."
        )]
        [SerializeField]
        private bool debugLog = false;

        /// <summary>
        /// 실제 피해가 적용될 때 발생합니다.
        ///
        /// 향후 Pressure Gauge:
        /// eventData.DamageSource를 확인하여
        /// 플레이어 피해만 집계하면 됩니다.
        /// </summary>
        public event Action<BossDamageEventData> DamageApplied;

        public BossCoreDamageState CoreDamageState =>
            coreDamageState;

        public float CurrentCoreDamageMultiplier =>
            GetCurrentCoreDamageMultiplier();

        public void SetCoreDamageState(
            BossCoreDamageState newState)
        {
            if (coreDamageState == newState)
            {
                return;
            }

            BossCoreDamageState previousState =
                coreDamageState;

            coreDamageState =
                newState;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossDamageRules] Core State 변경 | " +
                    $"{previousState} -> {coreDamageState} | " +
                    $"Multiplier=x{GetCurrentCoreDamageMultiplier():0.##}",
                    this
                );
            }
        }

        /// <summary>
        /// 파츠와 피해 출처에 따라 최종 적용 예정 피해를 계산합니다.
        /// </summary>
        public float CalculateAppliedDamage(
            BossPartDamageTestPart part,
            BossDamageSourceType damageSource,
            float rawDamage)
        {
            float sanitizedDamage =
                Mathf.Max(0f, rawDamage);

            if (sanitizedDamage <= 0f)
            {
                return 0f;
            }

            if (part == null ||
                !part.IsCore)
            {
                return sanitizedDamage;
            }

            if (coreMultiplierPlayerDamageOnly &&
                !IsPlayerDamage(damageSource))
            {
                return sanitizedDamage;
            }

            return sanitizedDamage *
                   GetCurrentCoreDamageMultiplier();
        }

        /// <summary>
        /// 실제 HP 차감이 끝난 뒤 호출합니다.
        /// </summary>
        public void NotifyDamageApplied(
            BossPartDamageTestPart part,
            BossDamageSourceType damageSource,
            float rawDamage,
            float modifiedDamage,
            float actualDamage,
            bool isCritical)
        {
            BossDamageEventData eventData =
                new BossDamageEventData(
                    part,
                    damageSource,
                    rawDamage,
                    modifiedDamage,
                    actualDamage,
                    isCritical
                );

            DamageApplied?.Invoke(eventData);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossDamageRules] 피해 기록 | " +
                    $"Part={(part != null ? part.PartType.ToString() : "NULL")}, " +
                    $"Core={(part != null && part.IsCore)}, " +
                    $"Source={damageSource}, " +
                    $"Raw={rawDamage:0.##}, " +
                    $"Modified={modifiedDamage:0.##}, " +
                    $"Actual={actualDamage:0.##}, " +
                    $"State={coreDamageState}",
                    this
                );
            }
        }

        public static bool IsPlayerDamage(
            BossDamageSourceType damageSource)
        {
            return
                damageSource ==
                BossDamageSourceType.PlayerProjectile
                ||
                damageSource ==
                BossDamageSourceType.PlayerAbility;
        }

        private float GetCurrentCoreDamageMultiplier()
        {
            switch (coreDamageState)
            {
                case BossCoreDamageState.Guarded:
                    return Mathf.Max(
                        0f,
                        guardedDamageMultiplier
                    );

                case BossCoreDamageState.Open:
                    return Mathf.Max(
                        0f,
                        openDamageMultiplier
                    );

                case BossCoreDamageState.Groggy:
                    return Mathf.Max(
                        0f,
                        groggyDamageMultiplier
                    );

                case BossCoreDamageState.Normal:
                default:
                    return Mathf.Max(
                        0f,
                        normalDamageMultiplier
                    );
            }
        }

        [ContextMenu("Core State/Guarded")]
        private void DebugSetGuarded()
        {
            SetCoreDamageState(
                BossCoreDamageState.Guarded
            );
        }

        [ContextMenu("Core State/Normal")]
        private void DebugSetNormal()
        {
            SetCoreDamageState(
                BossCoreDamageState.Normal
            );
        }

        [ContextMenu("Core State/Open")]
        private void DebugSetOpen()
        {
            SetCoreDamageState(
                BossCoreDamageState.Open
            );
        }

        [ContextMenu("Core State/Groggy")]
        private void DebugSetGroggy()
        {
            SetCoreDamageState(
                BossCoreDamageState.Groggy
            );
        }
    }
}