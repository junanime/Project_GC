using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 테스트용 파츠 보스의 "실제 전체 HP"를 관리합니다.
    ///
    /// 핵심 규칙:
    /// - Root HP = 실제 보스 생명력
    /// - Part HP = 파츠 파괴 게이지
    /// - 파츠를 공격하면 Root HP와 해당 파츠 파괴 게이지가 동시에 감소
    /// - Root HP가 0이 되면 테스트 보스 최종 사망 상태
    ///
    /// 아직 BossMonster, BossController, 보상, 페이즈와는 연결하지 않습니다.
    /// </summary>
    public sealed class BossPartDamageTestRootController : MonoBehaviour
    {
        [Header("보스 전체 체력")]

        [Tooltip(
            "테스트용 보스의 실제 전체 최대 체력입니다. " +
            "이 값이 0이 되면 테스트 보스를 최종 사망 상태로 처리합니다.")]
        [SerializeField, Min(0.01f)]
        private float maxRootHealth = 100f;

        [Tooltip(
            "파츠를 통해 들어온 공격 피해를 전체 HP에 적용할 전역 배율입니다. " +
            "1이면 공격력 5가 Root HP에도 그대로 5 들어갑니다.")]
        [SerializeField, Range(0f, 2f)]
        private float partDamageToRootRatio = 1f;

        [Tooltip("현재 테스트용 보스의 실제 전체 체력입니다.")]
        [SerializeField]
        private float currentRootHealth;

        [Tooltip("전체 HP가 0이 되어 테스트 보스가 사망 상태인지 표시합니다.")]
        [SerializeField]
        private bool rootHealthDepleted;

        [Header("파츠 관리")]

        [Tooltip(
            "Start 시 자식 파츠를 한 번 더 검색합니다. " +
            "각 파츠의 자동 등록과 함께 이중 안전장치로 사용합니다.")]
        [SerializeField]
        private bool autoCollectPartsFromChildren = true;

        [Tooltip(
            "현재 Root에 등록된 파츠 목록입니다. " +
            "런타임에서 자동으로 갱신됩니다.")]
        [SerializeField]
        private BossPartDamageTestPart[] parts;

        [Tooltip("현재 파괴된 파츠 개수입니다.")]
        [SerializeField]
        private int brokenPartCount;

        [Header("최종 사망 테스트")]

        [Tooltip(
            "Root HP가 0이 되면 모든 파츠 Collider를 비활성화하여 " +
            "추가 피해를 막습니다.")]
        [SerializeField]
        private bool disableAllPartCollidersOnRootDeath = true;

        [Header("디버그")]

        [Tooltip(
            "파츠 등록, 전체 HP 변화, 파츠 파괴, 최종 사망 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog = true;

        private readonly List<BossPartDamageTestPart> registeredParts =
            new List<BossPartDamageTestPart>();

        private readonly HashSet<int> brokenPartIds =
            new HashSet<int>();

        public float CurrentRootHealth => currentRootHealth;
        public float MaxRootHealth => maxRootHealth;

        public float RootHealthNormalized =>
            maxRootHealth > 0f
                ? currentRootHealth / maxRootHealth
                : 0f;

        public bool RootHealthDepleted => rootHealthDepleted;

        public int BrokenPartCount => brokenPartCount;

        public int TotalPartCount => registeredParts.Count;

        private void OnEnable()
        {
            ResetRootState();
        }

        private void Start()
        {
            // Awake/OnEnable 시점의 실행 순서에 의존하지 않고
            // 모든 자식이 생성된 뒤 Start에서 한 번 더 수집합니다.
            if (autoCollectPartsFromChildren)
            {
                CollectParts();
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] 시작 완료 | " +
                    $"HP={currentRootHealth:0.##}/{maxRootHealth:0.##} | " +
                    $"Parts={TotalPartCount}",
                    this);
            }
        }

        /// <summary>
        /// 파츠가 자신의 OnEnable에서 Root에 직접 등록할 때 사용합니다.
        /// </summary>
        public void RegisterPart(
            BossPartDamageTestPart part)
        {
            if (part == null)
            {
                return;
            }

            if (registeredParts.Contains(part))
            {
                return;
            }

            registeredParts.Add(part);

            SyncPartsArray();

            if (rootHealthDepleted &&
                disableAllPartCollidersOnRootDeath)
            {
                part.SetDamageEnabled(false);
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] 파츠 등록 | " +
                    $"Part={part.PartType}, " +
                    $"Registered={registeredParts.Count}",
                    this);
            }
        }

        /// <summary>
        /// 하위 계층의 파츠를 직접 검색합니다.
        /// 파츠 스스로 등록하는 방식에 대한 안전장치입니다.
        /// </summary>
        [ContextMenu("Collect Test Parts")]
        public void CollectParts()
        {
            BossPartDamageTestPart[] foundParts =
                GetComponentsInChildren
                <
                    BossPartDamageTestPart
                >(true);

            for (int i = 0; i < foundParts.Length; i++)
            {
                RegisterPart(foundParts[i]);
            }

            SyncPartsArray();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] 파츠 수집 완료 | " +
                    $"Found={foundParts.Length}, " +
                    $"Registered={registeredParts.Count}",
                    this);
            }
        }

        /// <summary>
        /// 파츠 공격을 Root 실제 HP에 전달합니다.
        ///
        /// 중요:
        /// 여기에는 "파츠 HP에서 실제 감소한 값"이 아니라
        /// 공격 자체의 실제 Damage 값이 전달됩니다.
        ///
        /// 파츠 게이지가 2 남았는데 100 피해를 받더라도
        /// Root HP에는 100 피해가 적용될 수 있습니다.
        /// </summary>
        public bool NotifyPartDamaged(
            BossPartDamageTestPart part,
            float incomingDamage,
            float partRootDamageMultiplier)
        {
            if (rootHealthDepleted)
            {
                return true;
            }

            if (part == null)
            {
                return false;
            }

            if (incomingDamage <= 0f)
            {
                return false;
            }

            RegisterPart(part);

            float globalMultiplier =
                Mathf.Max(0f, partDamageToRootRatio);

            float localMultiplier =
                Mathf.Max(0f, partRootDamageMultiplier);

            float rootDamage =
                incomingDamage *
                globalMultiplier *
                localMultiplier;

            if (rootDamage <= 0f)
            {
                return false;
            }

            float previousHealth =
                currentRootHealth;

            currentRootHealth =
                Mathf.Max(
                    0f,
                    currentRootHealth - rootDamage);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] {part.PartType} -> Root 피해 | " +
                    $"AttackDamage={incomingDamage:0.##}, " +
                    $"GlobalRatio={globalMultiplier:0.##}, " +
                    $"PartMultiplier={localMultiplier:0.##}, " +
                    $"RootDamage={rootDamage:0.##}, " +
                    $"RootHP={previousHealth:0.##}" +
                    $"->{currentRootHealth:0.##}/" +
                    $"{maxRootHealth:0.##}",
                    this);
            }

            if (currentRootHealth <= 0f)
            {
                HandleRootHealthDepleted();
            }

            return rootHealthDepleted;
        }

        /// <summary>
        /// 파괴된 파츠를 한 번만 기록합니다.
        /// </summary>
        public void NotifyPartBroken(
            BossPartDamageTestPart part)
        {
            if (part == null)
            {
                return;
            }

            RegisterPart(part);

            int partId =
                part.gameObject.GetInstanceID();

            if (!brokenPartIds.Add(partId))
            {
                return;
            }

            brokenPartCount =
                brokenPartIds.Count;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] 파츠 파괴 감지 | " +
                    $"Part={part.PartType}, " +
                    $"Broken={brokenPartCount}/" +
                    $"{TotalPartCount}",
                    this);
            }
        }

        /// <summary>
        /// Root HP를 초기화합니다.
        /// 등록된 파츠 목록은 지우지 않습니다.
        /// </summary>
        [ContextMenu("Reset Root Test State")]
        public void ResetRootState()
        {
            currentRootHealth =
                Mathf.Max(0.01f, maxRootHealth);

            rootHealthDepleted = false;

            brokenPartCount = 0;

            brokenPartIds.Clear();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] Root 초기화 | " +
                    $"HP={currentRootHealth:0.##}/" +
                    $"{maxRootHealth:0.##}",
                    this);
            }
        }

        /// <summary>
        /// Root HP가 0이 된 경우입니다.
        /// 이번 테스트에서는 실제 BossMonster.Killed()는 호출하지 않습니다.
        /// </summary>
        private void HandleRootHealthDepleted()
        {
            if (rootHealthDepleted)
            {
                return;
            }

            rootHealthDepleted = true;
            currentRootHealth = 0f;

            if (disableAllPartCollidersOnRootDeath)
            {
                for (int i = 0;
                     i < registeredParts.Count;
                     i++)
                {
                    BossPartDamageTestPart part =
                        registeredParts[i];

                    if (part != null)
                    {
                        part.SetDamageEnabled(false);
                    }
                }
            }

            if (debugLog)
            {
                Debug.Log(
                    "[BossPartRootTest] ★ 테스트 보스 전체 HP 0 ★ | " +
                    "최종 사망 조건 도달. " +
                    "아직 BossMonster.Killed(), 보상, 페이즈는 호출하지 않습니다.",
                    this);
            }
        }

        /// <summary>
        /// 전체 테스트 보스를 수동 초기화합니다.
        /// </summary>
        [ContextMenu("Reset Whole Test Boss")]
        private void ResetWholeTestBoss()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[BossPartRootTest] Play Mode에서 실행하세요.",
                    this);

                return;
            }

            CollectParts();

            ResetRootState();

            for (int i = 0;
                 i < registeredParts.Count;
                 i++)
            {
                BossPartDamageTestPart part =
                    registeredParts[i];

                if (part != null)
                {
                    part.ResetPart();
                }
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] 전체 테스트 보스 복구 완료 | " +
                    $"HP={currentRootHealth:0.##}/{maxRootHealth:0.##}, " +
                    $"Parts={TotalPartCount}",
                    this);
            }
        }

        private void SyncPartsArray()
        {
            parts =
                registeredParts.ToArray();
        }
    }
}