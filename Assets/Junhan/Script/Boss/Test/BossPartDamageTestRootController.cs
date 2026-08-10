using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 테스트용 파츠 보스의 전체 HP를 집계합니다.
    ///
    /// 별도의 Root HP를 소유하지 않습니다.
    ///
    /// Boss Max HP
    /// = 모든 파츠 MaxHealth 합계
    ///
    /// Boss Current HP
    /// = 모든 파츠 CurrentHealth 합계
    ///
    /// 단, Core가 파괴되면
    /// 다른 파츠 HP와 관계없이 Boss Current HP를 0으로 강제합니다.
    /// </summary>
    public sealed class BossPartDamageTestRootController :
        MonoBehaviour
    {
        [Header("파츠 검색")]

        [Tooltip(
            "파츠를 검색할 기준 Transform입니다. " +
            "비워 두면 이 컴포넌트가 붙은 Transform을 사용합니다.")]
        [SerializeField]
        private Transform partsSearchRoot;

        [Tooltip(
            "Start 시 자식의 BossPartDamageTestPart를 자동으로 수집합니다.")]
        [SerializeField]
        private bool autoCollectParts = true;

        [Tooltip(
            "현재 등록된 파츠입니다. " +
            "런타임 확인용이며 자동으로 갱신됩니다.")]
        [SerializeField]
        private BossPartDamageTestPart[] parts;

        [Header("Core")]

        [Tooltip(
            "현재 감지된 Core 파츠입니다. " +
            "Torso의 Auto Treat Torso As Core가 켜져 있으면 자동으로 잡힙니다.")]
        [SerializeField]
        private BossPartDamageTestPart corePart;

        [Header("전체 HP - 런타임 확인")]

        [Tooltip(
            "모든 파츠 MaxHealth의 합입니다.")]
        [SerializeField]
        private float totalMaxHealth;

        [Tooltip(
            "모든 파츠 CurrentHealth의 합입니다. " +
            "Core가 파괴되면 0으로 강제됩니다.")]
        [SerializeField]
        private float currentBossHealth;

        [Tooltip("Core 파괴 등으로 전체 보스가 사망했는지 표시합니다.")]
        [SerializeField]
        private bool bossDead;

        [Tooltip("현재 파괴된 파츠 개수입니다.")]
        [SerializeField]
        private int brokenPartCount;

        [Header("전체 사망 처리")]

        [Tooltip(
            "보스 사망 시 모든 남은 파츠 Collider를 비활성화합니다.")]
        [SerializeField]
        private bool disableAllCollidersOnBossDeath = true;

        [Tooltip(
            "보스 사망 시 아직 살아 있던 파츠 Sprite까지 모두 숨깁니다. " +
            "현재 테스트에서는 남은 파츠 확인을 위해 끄는 것을 권장합니다.")]
        [SerializeField]
        private bool hideRemainingPartsOnBossDeath = false;

        [Header("디버그")]

        [Tooltip(
            "파츠 등록, HP 집계, Core 파괴 및 전체 사망 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog = true;

        private readonly List<BossPartDamageTestPart>
            registeredParts =
                new List<BossPartDamageTestPart>();

        private readonly HashSet<int>
            brokenPartIds =
                new HashSet<int>();

        public float TotalMaxHealth =>
            totalMaxHealth;

        public float CurrentBossHealth =>
            bossDead
                ? 0f
                : currentBossHealth;

        public float HealthNormalized =>
            totalMaxHealth > 0f
                ? Mathf.Clamp01(
                    CurrentBossHealth /
                    totalMaxHealth)
                : 0f;

        public bool IsBossDead =>
            bossDead;

        public int BrokenPartCount =>
            brokenPartCount;

        public int TotalPartCount =>
            registeredParts.Count;

        public BossPartDamageTestPart CorePart =>
            corePart;

        private void Awake()
        {
            ResetRuntimeState();

            if (autoCollectParts)
            {
                CollectParts();
            }
        }

        private void OnEnable()
        {
            ResetRuntimeState();
        }

        private void Start()
        {
            // 자식들의 Awake / OnEnable 순서에 의존하지 않도록
            // Start에서도 한 번 더 강제로 수집합니다.
            if (autoCollectParts)
            {
                CollectParts();
            }

            RecalculateBossHealth();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] 시작 완료 | " +
                    $"Parts={TotalPartCount}, " +
                    $"Core=" +
                    $"{(corePart != null ? corePart.PartType.ToString() : "NULL")}, " +
                    $"BossHP={CurrentBossHealth:0.##}/" +
                    $"{TotalMaxHealth:0.##}",
                    this);
            }
        }

        /// <summary>
        /// 하위 파츠를 자동 검색합니다.
        ///
        /// 일반 검색에서 0개가 나오면
        /// 이전 테스트의 hierarchy 연결 문제를 대비해
        /// 최상위 Transform에서 한 번 더 검색합니다.
        /// </summary>
        [ContextMenu("Collect Test Parts")]
        public void CollectParts()
        {
            registeredParts.Clear();
            brokenPartIds.Clear();

            Transform searchRoot =
                partsSearchRoot != null
                    ? partsSearchRoot
                    : transform;

            BossPartDamageTestPart[] foundParts =
                searchRoot.GetComponentsInChildren
                <
                    BossPartDamageTestPart
                >(true);

            // 잘못된 위치에 RootController가 붙어 있어도
            // 같은 prefab 최상위에서 한 번 더 찾아봅니다.
            if (foundParts.Length == 0 &&
                transform.root != null &&
                transform.root != searchRoot)
            {
                foundParts =
                    transform.root.GetComponentsInChildren
                    <
                        BossPartDamageTestPart
                    >(true);

                if (debugLog &&
                    foundParts.Length > 0)
                {
                    Debug.LogWarning(
                        "[BossPartRootTest] " +
                        "현재 Transform 아래에서 파츠를 찾지 못해 " +
                        "transform.root 기준으로 파츠를 다시 수집했습니다. " +
                        "가능하면 RootController를 BossPartDamageTestRoot에 붙이세요.",
                        this);
                }
            }

            for (int i = 0;
                 i < foundParts.Length;
                 i++)
            {
                RegisterPartInternal(
                    foundParts[i]);
            }

            SyncPartsArray();
            ResolveCorePart();
            RecalculateBossHealth();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] 파츠 수집 완료 | " +
                    $"Found={foundParts.Length}, " +
                    $"Registered={registeredParts.Count}, " +
                    $"Core=" +
                    $"{(corePart != null ? corePart.PartType.ToString() : "NULL")}",
                    this);
            }
        }

        /// <summary>
        /// 각 파츠가 자기 자신을 Root에 등록할 때 사용합니다.
        /// </summary>
        public void RegisterPart(
            BossPartDamageTestPart part)
        {
            if (part == null)
            {
                return;
            }

            bool added =
                RegisterPartInternal(part);

            if (!added)
            {
                return;
            }

            SyncPartsArray();
            ResolveCorePart();
            RecalculateBossHealth();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] 파츠 등록 | " +
                    $"Part={part.PartType}, " +
                    $"Core={part.IsCore}, " +
                    $"Registered={registeredParts.Count}",
                    this);
            }
        }

        private bool RegisterPartInternal(
            BossPartDamageTestPart part)
        {
            if (part == null)
            {
                return false;
            }

            if (registeredParts.Contains(part))
            {
                part.SetRootController(this);
                return false;
            }

            registeredParts.Add(part);

            part.SetRootController(this);

            if (part.IsBroken)
            {
                brokenPartIds.Add(
                    part.gameObject.GetInstanceID());
            }

            return true;
        }

        /// <summary>
        /// 파츠 HP가 변경될 때 호출됩니다.
        ///
        /// Root에 별도 Damage를 더하지 않고
        /// 모든 파츠 HP를 다시 합산합니다.
        /// </summary>
        public void NotifyPartHealthChanged(
            BossPartDamageTestPart part)
        {
            if (part == null ||
                bossDead)
            {
                return;
            }

            RegisterPart(part);

            RecalculateBossHealth();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] HP 재계산 | " +
                    $"HitPart={part.PartType}, " +
                    $"BossHP={CurrentBossHealth:0.##}/" +
                    $"{TotalMaxHealth:0.##}",
                    this);
            }
        }

        /// <summary>
        /// 파츠가 파괴됐을 때 호출됩니다.
        ///
        /// 일반 파츠는 해당 파츠만 파괴.
        /// Core이면 즉시 전체 보스 사망.
        /// </summary>
        public void NotifyPartBroken(
            BossPartDamageTestPart part)
        {
            if (part == null)
            {
                return;
            }

            RegisterPart(part);

            int instanceId =
                part.gameObject.GetInstanceID();

            brokenPartIds.Add(instanceId);

            brokenPartCount =
                brokenPartIds.Count;

            RecalculateBossHealth();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] 파츠 파괴 감지 | " +
                    $"Part={part.PartType}, " +
                    $"Core={part.IsCore}, " +
                    $"Broken={brokenPartCount}/" +
                    $"{TotalPartCount}, " +
                    $"합산HP={currentBossHealth:0.##}/" +
                    $"{totalMaxHealth:0.##}",
                    this);
            }

            if (part.IsCore)
            {
                HandleBossDeath(
                    part,
                    "Core 파괴");

                return;
            }

            // Core 설정이 잘못되었더라도
            // 모든 파츠 HP가 0이면 안전하게 사망 처리.
            if (currentBossHealth <= 0f)
            {
                HandleBossDeath(
                    part,
                    "모든 파츠 HP 소진");
            }
        }

        /// <summary>
        /// 모든 파츠의 HP를 합산합니다.
        /// </summary>
        public void RecalculateBossHealth()
        {
            float maxHealthSum = 0f;
            float currentHealthSum = 0f;

            for (int i = 0;
                 i < registeredParts.Count;
                 i++)
            {
                BossPartDamageTestPart part =
                    registeredParts[i];

                if (part == null)
                {
                    continue;
                }

                maxHealthSum +=
                    Mathf.Max(
                        0f,
                        part.MaxHealth);

                currentHealthSum +=
                    Mathf.Max(
                        0f,
                        part.CurrentHealth);
            }

            totalMaxHealth =
                Mathf.Max(
                    0f,
                    maxHealthSum);

            currentBossHealth =
                bossDead
                    ? 0f
                    : Mathf.Clamp(
                        currentHealthSum,
                        0f,
                        totalMaxHealth);
        }

        /// <summary>
        /// Core 파츠를 탐색합니다.
        /// </summary>
        private void ResolveCorePart()
        {
            corePart = null;

            int coreCount = 0;

            for (int i = 0;
                 i < registeredParts.Count;
                 i++)
            {
                BossPartDamageTestPart part =
                    registeredParts[i];

                if (part == null ||
                    !part.IsCore)
                {
                    continue;
                }

                coreCount++;

                if (corePart == null)
                {
                    corePart = part;
                }
            }

            if (debugLog &&
                coreCount == 0)
            {
                Debug.LogWarning(
                    "[BossPartRootTest] Core 파츠를 찾지 못했습니다. " +
                    "Torso의 Auto Treat Torso As Core 또는 Is Core를 확인하세요.",
                    this);
            }
            else if (debugLog &&
                     coreCount > 1)
            {
                Debug.LogWarning(
                    $"[BossPartRootTest] Core 파츠가 {coreCount}개입니다. " +
                    "현재 테스트에서는 Core를 하나만 사용하는 것을 권장합니다.",
                    this);
            }
        }

        /// <summary>
        /// Core 파괴 등에 의한 최종 사망 처리입니다.
        /// </summary>
        private void HandleBossDeath(
            BossPartDamageTestPart causePart,
            string reason)
        {
            if (bossDead)
            {
                return;
            }

            bossDead = true;

            // 실제 남은 파츠 내부 HP는 유지합니다.
            // 플레이어에게 보여줄 Boss HP만 0으로 강제합니다.
            currentBossHealth = 0f;

            if (disableAllCollidersOnBossDeath)
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

            if (hideRemainingPartsOnBossDeath)
            {
                for (int i = 0;
                     i < registeredParts.Count;
                     i++)
                {
                    BossPartDamageTestPart part =
                        registeredParts[i];

                    if (part != null)
                    {
                        part.SetVisualEnabled(false);
                    }
                }
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] ★ 보스 최종 사망 ★ | " +
                    $"Reason={reason}, " +
                    $"CausePart=" +
                    $"{(causePart != null ? causePart.PartType.ToString() : "NULL")}, " +
                    $"DisplayedBossHP=0/{totalMaxHealth:0.##}",
                    this);
            }
        }

        /// <summary>
        /// 테스트 전체를 다시 초기화합니다.
        /// </summary>
        [ContextMenu("Reset Whole Test Boss")]
        public void ResetWholeTestBoss()
        {
            bossDead = false;

            brokenPartIds.Clear();
            brokenPartCount = 0;

            if (registeredParts.Count == 0)
            {
                CollectParts();
            }

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

            ResolveCorePart();
            RecalculateBossHealth();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartRootTest] 전체 초기화 완료 | " +
                    $"BossHP={CurrentBossHealth:0.##}/" +
                    $"{TotalMaxHealth:0.##}",
                    this);
            }
        }

        private void ResetRuntimeState()
        {
            bossDead = false;

            brokenPartIds.Clear();
            brokenPartCount = 0;

            currentBossHealth = 0f;
            totalMaxHealth = 0f;
        }

        private void SyncPartsArray()
        {
            parts =
                registeredParts.ToArray();
        }
    }
}