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
    /// 테스트용 보스 파츠입니다.
    ///
    /// 핵심:
    /// - Monster를 상속하지 않음
    /// - 파츠 HP는 "보스 생명력"이 아니라 "파괴 게이지"
    /// - 공격 피해는 Root 실제 HP에도 별도로 전달
    /// - 파괴 가능한 파츠만 HP 0에서 사라짐
    /// - Torso 등 파괴 불가 파츠는 최종 타격점으로 계속 유지 가능
    /// </summary>
    public sealed class BossPartDamageTestPart : IDamageable
    {
        [Header("파츠 정보")]

        [Tooltip("이 테스트 파츠의 종류입니다.")]
        [SerializeField]
        private BossPartDamageTestType partType =
            BossPartDamageTestType.Custom;

        [Tooltip(
            "Inspector와 Console에서 사용할 표시 이름입니다. " +
            "비워 두면 GameObject 이름을 사용합니다.")]
        [SerializeField]
        private string displayName;

        [Header("파괴 게이지")]

        [Tooltip(
            "이 파츠를 실제로 파괴할 수 있는지 설정합니다. " +
            "Head/팔/다리는 체크, 최종 타격점인 Torso는 해제하는 것을 권장합니다.")]
        [SerializeField]
        private bool canBreak = true;

        [Tooltip(
            "파츠 파괴에 필요한 최대 게이지입니다. " +
            "이 값은 보스 전체 HP와 별개의 값입니다.")]
        [SerializeField, Min(0.01f)]
        private float maxHealth = 30f;

        [Tooltip(
            "현재 파츠 파괴 게이지입니다. " +
            "Can Break가 꺼져 있으면 공격을 받아도 감소하지 않습니다.")]
        [SerializeField]
        private float currentHealth;

        [Tooltip("현재 파츠가 파괴되었는지 표시합니다.")]
        [SerializeField]
        private bool isBroken;

        [Header("전체 보스 HP 전달")]

        [Tooltip(
            "이 파츠를 공격했을 때 Root 실제 HP에 적용할 개별 피해 배율입니다. " +
            "기본 1이면 공격력 그대로 Root HP에 전달됩니다.")]
        [SerializeField, Min(0f)]
        private float rootDamageMultiplier = 1f;

        [Tooltip(
            "이 파츠가 연결될 테스트 RootController입니다. " +
            "비워 두면 부모 계층에서 자동으로 찾습니다.")]
        [SerializeField]
        private BossPartDamageTestRootController rootController;

        [Header("피격 판정")]

        [Tooltip(
            "피격에 사용할 Collider2D 목록입니다. " +
            "비워 두면 같은 GameObject의 Collider2D를 자동 수집합니다.")]
        [SerializeField]
        private Collider2D[] hitColliders;

        [Tooltip(
            "파괴 시 이 파츠의 피해 Collider를 비활성화합니다.")]
        [SerializeField]
        private bool disableCollidersWhenBroken = true;

        [Header("시각 확인")]

        [Tooltip(
            "파츠를 구성하는 SpriteRenderer 목록입니다. " +
            "비워 두면 현재 오브젝트 및 자식에서 자동 수집합니다.")]
        [SerializeField]
        private SpriteRenderer[] spriteRenderers;

        [Tooltip(
            "피격 순간 사용할 Material입니다. " +
            "비워 두면 색상 변화로 피격을 표시합니다.")]
        [SerializeField]
        private Material hitFlashMaterial;

        [Tooltip("Hit Flash 유지 시간입니다.")]
        [SerializeField, Min(0f)]
        private float hitFlashDuration = 0.08f;

        [Tooltip(
            "Hit Flash Material이 없을 때 사용할 피격 색상입니다.")]
        [SerializeField]
        private Color fallbackHitColor =
            new Color(1f, 0.35f, 0.35f, 1f);

        [Tooltip(
            "파츠 파괴 시 SpriteRenderer를 숨깁니다.")]
        [SerializeField]
        private bool hideRenderersWhenBroken = true;

        [Header("디버그")]

        [Tooltip(
            "초기화, 피격, Root 피해 전달, 파괴 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog = true;

        private Material[][] originalSharedMaterials;
        private Color[] originalColors;
        private Coroutine hitFlashCoroutine;

        public BossPartDamageTestType PartType => partType;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsBroken => isBroken;
        public bool CanBreak => canBreak;
        public float RootDamageMultiplier => rootDamageMultiplier;

        private void Awake()
        {
            ResolveReferences();
            CacheOriginalVisualState();
            ResolveRootController();
        }

        private void OnEnable()
        {
            ResetPart();

            ResolveRootController();

            if (rootController != null)
            {
                rootController.RegisterPart(this);
            }
        }

        /// <summary>
        /// 실제 SyringeProjectile 등이 호출하는 피해 진입점입니다.
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

            float appliedDamage =
                Mathf.Max(0f, damage);

            if (appliedDamage <= 0f)
            {
                return;
            }

            ResolveRootController();

            // ─────────────────────────────
            // 1. Root 실제 HP 피해
            // ─────────────────────────────

            bool rootDefeated = false;

            if (rootController != null)
            {
                rootDefeated =
                    rootController.NotifyPartDamaged(
                        this,
                        appliedDamage,
                        rootDamageMultiplier);
            }
            else if (debugLog)
            {
                Debug.LogWarning(
                    $"[BossPartTest] {GetPartLabel()} | " +
                    "RootController를 찾지 못했습니다. " +
                    "BossPartDamageTestRoot 최상위에 RootController가 있는지 확인하세요.",
                    this);
            }

            // 같은 공격으로 Root HP가 0이 됐다면
            // 최종 사망을 우선합니다.
            if (rootDefeated)
            {
                if (debugLog)
                {
                    Debug.Log(
                        $"[BossPartTest] {GetPartLabel()} 공격으로 " +
                        "Root HP가 0이 되었습니다.",
                        this);
                }

                return;
            }

            // ─────────────────────────────
            // 2. 파츠 파괴 게이지
            // ─────────────────────────────

            if (!canBreak)
            {
                if (debugLog)
                {
                    Debug.Log(
                        $"[BossPartTest] {GetPartLabel()} 피격 | " +
                        $"Damage={appliedDamage:0.##}, " +
                        $"Critical={isCritical}, " +
                        "BreakGauge=Disabled | " +
                        "Root HP에만 피해 전달",
                        this);
                }

                PlayHitFlash();
                return;
            }

            float healthBeforeDamage =
                currentHealth;

            currentHealth =
                Mathf.Max(
                    0f,
                    currentHealth - appliedDamage);

            float actualBreakDamage =
                Mathf.Max(
                    0f,
                    healthBeforeDamage - currentHealth);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartTest] {GetPartLabel()} 피격 | " +
                    $"Damage={appliedDamage:0.##}, " +
                    $"BreakDamage={actualBreakDamage:0.##}, " +
                    $"Critical={isCritical}, " +
                    $"BreakHP={currentHealth:0.##}/{maxHealth:0.##}",
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
        /// 파츠 자체는 개별 넉백을 받지 않습니다.
        /// </summary>
        public override void Knockback(
            Vector2 knockback)
        {
            // 루트 보스가 이동을 담당하므로
            // 개별 파츠 Knockback은 적용하지 않습니다.
        }

        /// <summary>
        /// 파츠 상태를 초기화합니다.
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

            for (int i = 0;
                 i < spriteRenderers.Length;
                 i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].enabled = true;
                }
            }

            RestoreOriginalVisualState();

            if (rootController != null)
            {
                rootController.RegisterPart(this);
            }

            if (debugLog)
            {
                string breakState =
                    canBreak
                        ? $"BreakHP={currentHealth:0.##}/{maxHealth:0.##}"
                        : "BreakGauge=Disabled";

                Debug.Log(
                    $"[BossPartTest] {GetPartLabel()} 초기화 | " +
                    breakState,
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
        /// 파츠의 파괴 게이지가 0이 되었을 때 실행합니다.
        /// 일반 Monster 사망 처리는 호출하지 않습니다.
        /// </summary>
        private void BreakPart()
        {
            if (!canBreak)
            {
                return;
            }

            if (isBroken)
            {
                return;
            }

            isBroken = true;
            currentHealth = 0f;

            StopHitFlashAndRestore();

            if (disableCollidersWhenBroken)
            {
                SetDamageEnabled(false);
            }

            if (hideRenderersWhenBroken)
            {
                for (int i = 0;
                     i < spriteRenderers.Length;
                     i++)
                {
                    if (spriteRenderers[i] != null)
                    {
                        spriteRenderers[i].enabled = false;
                    }
                }
            }

            ResolveRootController();

            if (rootController != null)
            {
                rootController.NotifyPartBroken(this);
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartTest] {GetPartLabel()} 파괴 완료 | " +
                    "파츠만 제거하며 보스 전체 사망/보상은 호출하지 않습니다.",
                    this);
            }
        }

        /// <summary>
        /// Root 최종 사망 등의 상황에서
        /// 파츠 Collider를 일괄적으로 켜거나 끌 때 사용합니다.
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
                    hitColliders[i].enabled = enabled;
                }
            }
        }

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