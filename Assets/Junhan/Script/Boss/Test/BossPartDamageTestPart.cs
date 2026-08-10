using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 테스트 단계에서만 사용하는 보스 파츠 구분값입니다.
    /// 정식 파츠 시스템의 최종 enum으로 사용하지 않습니다.
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
    /// 각 자식 파츠가 독립적으로 피해를 받는지 확인하기 위한 최소 테스트 컴포넌트입니다.
    /// Monster를 상속하지 않으므로 EntityManager 등록, 경험치, 골드,
    /// 보상, 처치 트리거가 발생하지 않습니다.
    /// </summary>
    public sealed class BossPartDamageTestPart : IDamageable
    {
        [Header("파츠 정보")]
        [Tooltip("Console 로그와 Inspector에서 구분할 테스트 파츠 종류입니다.")]
        [SerializeField]
        private BossPartDamageTestType partType =
            BossPartDamageTestType.Custom;

        [Tooltip("파츠를 구분하기 위한 표시 이름입니다. 비워 두면 GameObject 이름을 사용합니다.")]
        [SerializeField] private string displayName;

        [Header("체력")]
        [Tooltip("이 테스트 파츠의 최대 체력입니다.")]
        [SerializeField, Min(0.01f)] private float maxHealth = 30f;

        [Tooltip("현재 체력 확인용 값입니다. 플레이 중 Inspector에서 감소 여부를 확인할 수 있습니다.")]
        [SerializeField] private float currentHealth;

        [Tooltip("현재 파츠가 파괴되었는지 확인하는 테스트 상태입니다.")]
        [SerializeField] private bool isBroken;

        [Header("피격 판정")]
        [Tooltip("이 파츠가 피해 판정에 사용할 Collider2D 목록입니다. 비워 두면 같은 GameObject의 Collider2D를 자동 수집합니다.")]
        [SerializeField] private Collider2D[] hitColliders;

        [Tooltip("파츠 체력이 0이 되면 피해 판정 Collider를 끕니다.")]
        [SerializeField] private bool disableCollidersWhenBroken = true;

        [Header("시각 확인")]
        [Tooltip("이 파츠를 구성하는 SpriteRenderer 목록입니다. 비워 두면 현재 오브젝트와 자식에서 자동 수집합니다.")]
        [SerializeField] private SpriteRenderer[] spriteRenderers;

        [Tooltip("피격 순간 사용할 선택적 Material입니다. 비워 두면 색상 변화로 피격을 표시합니다.")]
        [SerializeField] private Material hitFlashMaterial;

        [Tooltip("Hit Flash가 유지되는 시간입니다.")]
        [SerializeField, Min(0f)] private float hitFlashDuration = 0.08f;

        [Tooltip("Hit Flash Material을 사용하지 않을 때 적용할 피격 색상입니다.")]
        [SerializeField]
        private Color fallbackHitColor =
            new Color(1f, 0.35f, 0.35f, 1f);

        [Tooltip("파츠 체력이 0이 되면 SpriteRenderer를 숨깁니다.")]
        [SerializeField] private bool hideRenderersWhenBroken = true;

        [Header("디버그")]
        [Tooltip("파츠 초기화, 피해, 파괴 내용을 Console에 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private Material[][] originalSharedMaterials;
        private Color[] originalColors;
        private Coroutine hitFlashCoroutine;

        public BossPartDamageTestType PartType => partType;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsBroken => isBroken;

        private void Awake()
        {
            ResolveReferences();
            CacheOriginalVisualState();
        }

        private void OnEnable()
        {
            ResetPart();
        }

        public override void TakeDamage(
            float damage,
            Vector2 knockback = default(Vector2),
            bool isCritical = false)
        {
            if (isBroken)
            {
                return;
            }

            float appliedDamage = Mathf.Max(0f, damage);

            if (appliedDamage <= 0f)
            {
                return;
            }

            currentHealth =
                Mathf.Max(0f, currentHealth - appliedDamage);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartTest] {GetPartLabel()} 피격 | " +
                    $"Damage={appliedDamage:0.##}, " +
                    $"Critical={isCritical}, " +
                    $"HP={currentHealth:0.##}/{maxHealth:0.##}",
                    this);
            }

            if (currentHealth <= 0f)
            {
                BreakPart();
                return;
            }

            PlayHitFlash();
        }

        public override void Knockback(Vector2 knockback)
        {
            // 테스트 파츠는 루트 보스와 함께 움직여야 하므로
            // 파츠 단위 넉백은 의도적으로 적용하지 않습니다.
        }

        [ContextMenu("Reset Test Part")]
        public void ResetPart()
        {
            ResolveReferences();

            if (originalSharedMaterials == null ||
                originalColors == null ||
                originalColors.Length != spriteRenderers.Length)
            {
                CacheOriginalVisualState();
            }

            StopHitFlashAndRestore();

            currentHealth = Mathf.Max(0.01f, maxHealth);
            isBroken = false;

            for (int i = 0; i < hitColliders.Length; i++)
            {
                if (hitColliders[i] != null)
                {
                    hitColliders[i].enabled = true;
                }
            }

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].enabled = true;
                }
            }

            RestoreOriginalVisualState();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartTest] {GetPartLabel()} 초기화 | " +
                    $"HP={currentHealth:0.##}/{maxHealth:0.##}",
                    this);
            }
        }

        [ContextMenu("Apply 10 Test Damage")]
        private void ApplyTenTestDamage()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[BossPartTest] Test Damage는 Play Mode에서 실행하세요.",
                    this);

                return;
            }

            TakeDamage(10f);
        }

        private void BreakPart()
        {
            if (isBroken)
            {
                return;
            }

            isBroken = true;
            currentHealth = 0f;

            StopHitFlashAndRestore();

            if (disableCollidersWhenBroken)
            {
                for (int i = 0; i < hitColliders.Length; i++)
                {
                    if (hitColliders[i] != null)
                    {
                        hitColliders[i].enabled = false;
                    }
                }
            }

            if (hideRenderersWhenBroken)
            {
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    if (spriteRenderers[i] != null)
                    {
                        spriteRenderers[i].enabled = false;
                    }
                }
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartTest] {GetPartLabel()} 파괴 완료. " +
                    "보상, 경험치, BossMonster 사망 처리는 호출하지 않습니다.",
                    this);
            }
        }

        private void ResolveReferences()
        {
            if (hitColliders == null ||
                hitColliders.Length == 0)
            {
                hitColliders = GetComponents<Collider2D>();
            }

            if (spriteRenderers == null ||
                spriteRenderers.Length == 0)
            {
                spriteRenderers =
                    GetComponentsInChildren<SpriteRenderer>(true);
            }
        }

        private void CacheOriginalVisualState()
        {
            originalSharedMaterials =
                new Material[spriteRenderers.Length][];

            originalColors =
                new Color[spriteRenderers.Length];

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = spriteRenderers[i];

                if (renderer == null)
                {
                    originalSharedMaterials[i] = null;
                    originalColors[i] = Color.white;
                    continue;
                }

                originalSharedMaterials[i] =
                    renderer.sharedMaterials;

                originalColors[i] = renderer.color;
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
                StopCoroutine(hitFlashCoroutine);
            }

            hitFlashCoroutine =
                StartCoroutine(HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = spriteRenderers[i];

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

                    renderer.sharedMaterials = materials;
                }
                else
                {
                    renderer.color = fallbackHitColor;
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
                StopCoroutine(hitFlashCoroutine);
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

            int count = Mathf.Min(
                spriteRenderers.Length,
                originalColors.Length);

            for (int i = 0; i < count; i++)
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

                renderer.color = originalColors[i];
            }
        }

        private string GetPartLabel()
        {
            string resolvedName =
                string.IsNullOrWhiteSpace(displayName)
                    ? gameObject.name
                    : displayName;

            return $"{resolvedName} ({partType})";
        }
    }
}