using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 테스트 단계에서 사용하는 보스 파츠 종류입니다.
    /// </summary>
    public enum BossPartDamageTestType
    {
        Head = 0,
        Torso = 1,
        LeftArm = 2,
        RightArm = 3,
        LeftLeg = 4,
        RightLeg = 5,
        Custom = 6
    }

    /// <summary>
    /// 파츠별 독립 HP와 파괴를 테스트하기 위한 컴포넌트입니다.
    ///
    /// 핵심 규칙:
    /// - 각 파츠는 독립 HP를 가집니다.
    /// - 모든 파츠 HP의 합이 보스 표시 HP가 됩니다.
    /// - 일반 파츠 HP가 0이면 해당 파츠만 파괴됩니다.
    /// - Core 파츠 HP가 0이면 전체 보스가 즉시 사망합니다.
    /// - Monster를 상속하지 않으므로 일반 몬스터 처치 보상은 발생하지 않습니다.
    /// </summary>
    public sealed class BossPartDamageTestPart : IDamageable
    {
        [Header("파츠 정보")]

        [Tooltip("이 파츠의 종류입니다.")]
        [SerializeField]
        private BossPartDamageTestType partType =
            BossPartDamageTestType.Custom;

        [Tooltip(
            "Console과 Inspector에서 사용할 표시 이름입니다. " +
            "비워 두면 GameObject 이름을 사용합니다.")]
        [SerializeField]
        private string displayName;

        [Header("Core 설정")]

        [Tooltip(
            "체크하면 이 파츠가 보스의 핵심 Core가 됩니다. " +
            "Core의 HP가 0이 되면 다른 파츠의 남은 HP와 관계없이 보스가 즉시 사망합니다.")]
        [SerializeField]
        private bool isCore = false;

        [Tooltip(
            "체크하면 Torso 타입을 자동으로 Core로 취급합니다. " +
            "현재 테스트 보스에서는 켜두는 것을 권장합니다.")]
        [SerializeField]
        private bool autoTreatTorsoAsCore = true;

        [Header("체력")]

        [Tooltip(
            "이 파츠의 최대 체력입니다. " +
            "모든 파츠 Max Health의 합이 보스 전체 최대 체력이 됩니다.")]
        [SerializeField, Min(0.01f)]
        private float maxHealth = 30f;

        [Tooltip(
            "현재 파츠 체력입니다. " +
            "모든 파츠 Current Health의 합이 보스 현재 체력이 됩니다.")]
        [SerializeField]
        private float currentHealth;

        [Tooltip("현재 파츠가 파괴되었는지 표시합니다.")]
        [SerializeField]
        private bool isBroken;

        [Header("루트 연결")]

        [Tooltip(
            "전체 파츠 HP를 집계하는 RootController입니다. " +
            "비워 두면 부모 계층에서 자동 탐색합니다.")]
        [SerializeField]
        private BossPartDamageTestRootController rootController;

        [Header("피격 판정")]

        [Tooltip(
            "이 파츠가 피해 판정에 사용할 Collider2D입니다. " +
            "비워 두면 같은 GameObject에서 자동 탐색합니다.")]
        [SerializeField]
        private Collider2D[] hitColliders;

        [Tooltip("파츠 파괴 시 Collider를 비활성화합니다.")]
        [SerializeField]
        private bool disableCollidersWhenBroken = true;

        [Header("시각 처리")]

        [Tooltip(
            "이 파츠를 구성하는 SpriteRenderer입니다. " +
            "비워 두면 현재 GameObject와 자식에서 자동 탐색합니다.")]
        [SerializeField]
        private SpriteRenderer[] spriteRenderers;

        [Tooltip(
            "피격 순간 사용할 Material입니다. " +
            "비워 두면 Fallback Hit Color를 사용합니다.")]
        [SerializeField]
        private Material hitFlashMaterial;

        [Tooltip("Hit Flash 지속 시간입니다.")]
        [SerializeField, Min(0f)]
        private float hitFlashDuration = 0.08f;

        [Tooltip(
            "Hit Flash Material이 없을 때 사용할 피격 색상입니다.")]
        [SerializeField]
        private Color fallbackHitColor =
            new Color(1f, 0.35f, 0.35f, 1f);

        [Tooltip("파츠 파괴 시 SpriteRenderer를 숨깁니다.")]
        [SerializeField]
        private bool hideRenderersWhenBroken = true;

        [Header("디버그")]

        [Tooltip(
            "초기화, 피해, 파괴 및 Core 관련 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog = true;

        private Material[][] originalSharedMaterials;
        private Color[] originalColors;
        private Coroutine hitFlashCoroutine;

        public BossPartDamageTestType PartType => partType;

        public float CurrentHealth => currentHealth;

        public float MaxHealth => maxHealth;

        public bool IsBroken => isBroken;

        /// <summary>
        /// 명시적으로 Core이거나,
        /// Auto Treat Torso As Core가 켜진 Torso이면 Core입니다.
        /// </summary>
        public bool IsCore =>
            isCore ||
            (
                autoTreatTorsoAsCore &&
                partType == BossPartDamageTestType.Torso
            );

        private void Awake()
        {
            ResolveReferences();
            CacheOriginalVisualState();
            ResolveRootController();
        }

        private void OnEnable()
        {
            ResetPart();
        }

        /// <summary>
        /// SyringeProjectile 등의 실제 피해 진입점입니다.
        /// </summary>
        public override void TakeDamage(
            float damage,
            Vector2 knockback = default(Vector2),
            bool isCritical = false)
        {
            if (isBroken)
            {
                return;
            }

            ResolveRootController();

            // Core가 이미 파괴되어 전체 보스가 죽었다면
            // 추가 피해를 받지 않습니다.
            if (rootController != null &&
                rootController.IsBossDead)
            {
                return;
            }

            float appliedDamage =
                Mathf.Max(0f, damage);

            if (appliedDamage <= 0f)
            {
                return;
            }

            float healthBeforeDamage =
                currentHealth;

            currentHealth =
                Mathf.Max(
                    0f,
                    currentHealth - appliedDamage);

            float actualDamage =
                Mathf.Max(
                    0f,
                    healthBeforeDamage - currentHealth);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartTest] {GetPartLabel()} 피격 | " +
                    $"Damage={appliedDamage:0.##}, " +
                    $"ActualDamage={actualDamage:0.##}, " +
                    $"Critical={isCritical}, " +
                    $"HP={currentHealth:0.##}/{maxHealth:0.##}, " +
                    $"Core={IsCore}",
                    this);
            }

            // 파츠 HP가 변경되었음을 Root에 알립니다.
            if (rootController != null)
            {
                rootController.RegisterPart(this);

                rootController.NotifyPartHealthChanged(
                    this);
            }
            else if (debugLog)
            {
                Debug.LogWarning(
                    $"[BossPartTest] {GetPartLabel()} | " +
                    "BossPartDamageTestRootController를 찾지 못했습니다.",
                    this);
            }

            if (currentHealth <= 0f)
            {
                BreakPart();
                return;
            }

            PlayHitFlash();
        }

        /// <summary>
        /// 파츠 자체에는 개별 Knockback을 적용하지 않습니다.
        /// 이동은 보스 루트가 담당합니다.
        /// </summary>
        public override void Knockback(
            Vector2 knockback)
        {
        }

        /// <summary>
        /// 파츠를 최초 상태로 복구합니다.
        /// </summary>
        [ContextMenu("Reset Test Part")]
        public void ResetPart()
        {
            ResolveReferences();
            ResolveRootController();

            if (originalSharedMaterials == null ||
                originalColors == null ||
                originalColors.Length != spriteRenderers.Length)
            {
                CacheOriginalVisualState();
            }

            StopHitFlashAndRestore();

            currentHealth =
                Mathf.Max(0.01f, maxHealth);

            isBroken = false;

            SetDamageEnabled(true);
            SetVisualEnabled(true);

            RestoreOriginalVisualState();

            if (rootController != null)
            {
                rootController.RegisterPart(this);
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartTest] {GetPartLabel()} 초기화 | " +
                    $"HP={currentHealth:0.##}/{maxHealth:0.##}, " +
                    $"Core={IsCore}",
                    this);
            }
        }

        [ContextMenu("Apply 10 Test Damage")]
        private void ApplyTenTestDamage()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[BossPartTest] Play Mode에서 실행하세요.",
                    this);

                return;
            }

            TakeDamage(10f);
        }

        /// <summary>
        /// 파츠 HP가 0이 되었을 때 호출됩니다.
        ///
        /// 일반 파츠:
        /// - 해당 파츠만 파괴
        ///
        /// Core:
        /// - RootController가 전체 보스를 사망 상태로 전환
        /// </summary>
        private void BreakPart()
        {
            if (isBroken)
            {
                return;
            }

            isBroken = true;
            currentHealth = 0f;

            StopHitFlashAndRestore();

            ResolveRootController();

            // 먼저 Root에 알립니다.
            // Core라면 이 시점에 전체 보스 사망 처리가 시작됩니다.
            if (rootController != null)
            {
                rootController.RegisterPart(this);

                rootController.NotifyPartBroken(
                    this);
            }

            if (disableCollidersWhenBroken)
            {
                SetDamageEnabled(false);
            }

            if (hideRenderersWhenBroken)
            {
                SetVisualEnabled(false);
            }

            if (debugLog)
            {
                if (IsCore)
                {
                    Debug.Log(
                        $"[BossPartTest] ★ CORE 파괴 ★ | " +
                        $"{GetPartLabel()} 파괴 → 전체 보스 사망 요청",
                        this);
                }
                else
                {
                    Debug.Log(
                        $"[BossPartTest] {GetPartLabel()} 파괴 완료 | " +
                        "해당 파츠만 제거합니다.",
                        this);
                }
            }
        }

        /// <summary>
        /// RootController가 파츠 참조를 직접 지정할 때 사용합니다.
        /// </summary>
        public void SetRootController(
            BossPartDamageTestRootController controller)
        {
            rootController = controller;
        }

        /// <summary>
        /// 피해 Collider를 일괄 활성/비활성화합니다.
        /// </summary>
        public void SetDamageEnabled(
            bool enabled)
        {
            ResolveReferences();

            for (int i = 0;
                 i < hitColliders.Length;
                 i++)
            {
                if (hitColliders[i] != null)
                {
                    hitColliders[i].enabled =
                        enabled;
                }
            }
        }

        /// <summary>
        /// 파츠 SpriteRenderer를 일괄 활성/비활성화합니다.
        /// </summary>
        public void SetVisualEnabled(
            bool enabled)
        {
            ResolveReferences();

            for (int i = 0;
                 i < spriteRenderers.Length;
                 i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].enabled =
                        enabled;
                }
            }
        }

        /// <summary>
        /// 부모 계층에서 RootController를 탐색합니다.
        ///
        /// 이전 테스트에서 Root를 찾지 못하는 문제가 있었기 때문에
        /// 일반 부모 탐색 후 prefab 최상위에서도 한 번 더 찾습니다.
        /// </summary>
        private void ResolveRootController()
        {
            if (rootController != null)
            {
                return;
            }

            rootController =
                GetComponentInParent
                <
                    BossPartDamageTestRootController
                >(true);

            if (rootController != null)
            {
                return;
            }

            Transform topRoot =
                transform.root;

            if (topRoot != null)
            {
                rootController =
                    topRoot.GetComponentInChildren
                    <
                        BossPartDamageTestRootController
                    >(true);
            }
        }

        private void ResolveReferences()
        {
            if (hitColliders == null ||
                hitColliders.Length == 0)
            {
                hitColliders =
                    GetComponents<Collider2D>();
            }

            if (spriteRenderers == null ||
                spriteRenderers.Length == 0)
            {
                spriteRenderers =
                    GetComponentsInChildren
                    <
                        SpriteRenderer
                    >(true);
            }
        }

        private void CacheOriginalVisualState()
        {
            originalSharedMaterials =
                new Material[spriteRenderers.Length][];

            originalColors =
                new Color[spriteRenderers.Length];

            for (int i = 0;
                 i < spriteRenderers.Length;
                 i++)
            {
                SpriteRenderer renderer =
                    spriteRenderers[i];

                if (renderer == null)
                {
                    originalSharedMaterials[i] = null;
                    originalColors[i] = Color.white;
                    continue;
                }

                originalSharedMaterials[i] =
                    renderer.sharedMaterials;

                originalColors[i] =
                    renderer.color;
            }
        }

        private void PlayHitFlash()
        {
            if (!isActiveAndEnabled ||
                hitFlashDuration <= 0f)
            {
                return;
            }

            if (hitFlashCoroutine != null)
            {
                StopCoroutine(
                    hitFlashCoroutine);
            }

            hitFlashCoroutine =
                StartCoroutine(
                    HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            for (int i = 0;
                 i < spriteRenderers.Length;
                 i++)
            {
                SpriteRenderer renderer =
                    spriteRenderers[i];

                if (renderer == null)
                {
                    continue;
                }

                if (hitFlashMaterial != null)
                {
                    Material[] materials =
                        renderer.sharedMaterials;

                    for (int materialIndex = 0;
                         materialIndex < materials.Length;
                         materialIndex++)
                    {
                        materials[materialIndex] =
                            hitFlashMaterial;
                    }

                    renderer.sharedMaterials =
                        materials;
                }
                else
                {
                    renderer.color =
                        fallbackHitColor;
                }
            }

            yield return new WaitForSeconds(
                hitFlashDuration);

            RestoreOriginalVisualState();

            hitFlashCoroutine = null;
        }

        private void StopHitFlashAndRestore()
        {
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(
                    hitFlashCoroutine);

                hitFlashCoroutine = null;
            }

            RestoreOriginalVisualState();
        }

        private void RestoreOriginalVisualState()
        {
            if (spriteRenderers == null ||
                originalSharedMaterials == null ||
                originalColors == null)
            {
                return;
            }

            int count =
                Mathf.Min(
                    spriteRenderers.Length,
                    originalColors.Length);

            for (int i = 0;
                 i < count;
                 i++)
            {
                SpriteRenderer renderer =
                    spriteRenderers[i];

                if (renderer == null)
                {
                    continue;
                }

                if (i < originalSharedMaterials.Length &&
                    originalSharedMaterials[i] != null)
                {
                    renderer.sharedMaterials =
                        originalSharedMaterials[i];
                }

                renderer.color =
                    originalColors[i];
            }
        }

        private string GetPartLabel()
        {
            string resolvedName =
                string.IsNullOrWhiteSpace(displayName)
                    ? gameObject.name
                    : displayName;

            return
                $"{resolvedName} ({partType})";
        }
    }
}