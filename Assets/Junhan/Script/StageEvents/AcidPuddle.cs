using UnityEngine;

namespace Vampire
{
    public class AcidPuddle : MonoBehaviour
    {
        [Header("Runtime Settings")]
        [Tooltip("위산 장판이 유지되는 시간입니다.")]
        [SerializeField] private float lifeTime = 6f;

        [Tooltip("위산 장판이 한 번 틱 데미지를 줄 때의 피해량입니다.")]
        [SerializeField] private float damagePerTick = 3f;

        [Tooltip("위산 장판 데미지가 반복되는 간격입니다.")]
        [SerializeField] private float tickInterval = 0.5f;

        [Tooltip("체크하면 플레이어가 위산 장판에 들어간 즉시 데미지를 받습니다.")]
        [SerializeField] private bool damageImmediatelyOnEnter = false;

        [Header("Visual")]
        [Tooltip("위산 장판의 대표 SpriteRenderer입니다. 비워두면 자식 오브젝트에서 자동으로 찾습니다.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("위산 장판에 포함된 모든 SpriteRenderer입니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] private SpriteRenderer[] spriteRenderers;

        [Tooltip("데미지가 활성화된 뒤 위산 장판 색상입니다.")]
        [SerializeField] private Color activeColor = new Color(0.4f, 1f, 0.1f, 0.55f);

        [Tooltip("데미지가 활성화되기 전 경고 상태의 위산 장판 색상입니다.")]
        [SerializeField] private Color warningColor = new Color(0.4f, 1f, 0.1f, 0.25f);

        [Tooltip("위산 장판 생성 후 데미지가 활성화되기 전까지의 경고 시간입니다.")]
        [SerializeField] private float warningDuration = 0.4f;

        [Header("Visual Sorting")]
        [Tooltip("체크하면 위산 장판의 Sorting Layer와 Order in Layer를 코드에서 강제로 적용합니다.")]
        [SerializeField] private bool forceVisualSorting = true;

        [Tooltip("위산 장판에 적용할 Sorting Layer 이름입니다. 프로젝트에 없는 이름이면 Default로 유지됩니다.")]
        [SerializeField] private string puddleSortingLayerName = "Default";

        [Tooltip("위산 장판의 Order in Layer입니다. 몬스터/플레이어보다 낮고, 배경보다 높게 맞추세요.")]
        [SerializeField] private int puddleOrderInLayer = -5;

        private Character targetCharacter;
        private float tickTimer;
        private float lifeTimer;
        private float warningTimer;
        private bool activeDamage = false;

        private void Awake()
        {
            CacheSpriteRenderersIfNeeded();
            ApplyVisualSorting();
        }

        private void OnValidate()
        {
            CacheSpriteRenderersIfNeeded();
            ApplyVisualSorting();
        }

        public void Init(
            float lifeTime,
            float damagePerTick,
            float tickInterval,
            float puddleScale,
            bool damageImmediatelyOnEnter,
            float warningDuration)
        {
            this.lifeTime = Mathf.Max(0.1f, lifeTime);
            this.damagePerTick = Mathf.Max(0f, damagePerTick);
            this.tickInterval = Mathf.Max(0.05f, tickInterval);
            this.damageImmediatelyOnEnter = damageImmediatelyOnEnter;
            this.warningDuration = Mathf.Max(0f, warningDuration);

            lifeTimer = this.lifeTime;
            warningTimer = this.warningDuration;
            tickTimer = this.tickInterval;

            transform.localScale = new Vector3(puddleScale, puddleScale, 1f);

            CacheSpriteRenderersIfNeeded();
            ApplyVisualSorting();

            activeDamage = this.warningDuration <= 0f;

            if (activeDamage)
            {
                SetVisualColor(activeColor);
            }
            else
            {
                SetVisualColor(warningColor);
            }
        }

        private void Update()
        {
            UpdateLifetime();
            UpdateWarningState();
            UpdateDamageTick();
        }

        private void UpdateLifetime()
        {
            lifeTimer -= Time.deltaTime;

            if (lifeTimer <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateWarningState()
        {
            if (activeDamage)
            {
                return;
            }

            warningTimer -= Time.deltaTime;

            if (warningTimer <= 0f)
            {
                activeDamage = true;
                SetVisualColor(activeColor);
            }
        }

        private void UpdateDamageTick()
        {
            if (!activeDamage)
            {
                return;
            }

            if (targetCharacter == null)
            {
                return;
            }

            tickTimer -= Time.deltaTime;

            if (tickTimer <= 0f)
            {
                targetCharacter.TakeDamage(damagePerTick);
                tickTimer = tickInterval;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Character character = other.GetComponentInParent<Character>();

            if (character == null)
            {
                return;
            }

            targetCharacter = character;
            tickTimer = damageImmediatelyOnEnter ? 0f : tickInterval;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Character character = other.GetComponentInParent<Character>();

            if (character != null && character == targetCharacter)
            {
                targetCharacter = null;
            }
        }

        private void CacheSpriteRenderersIfNeeded()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }

            if (spriteRenderers == null || spriteRenderers.Length == 0)
            {
                spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }
        }

        private void SetVisualColor(Color color)
        {
            CacheSpriteRenderersIfNeeded();

            if (spriteRenderers == null || spriteRenderers.Length == 0)
            {
                return;
            }

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == null)
                {
                    continue;
                }

                spriteRenderers[i].color = color;
            }
        }

        private void ApplyVisualSorting()
        {
            if (!forceVisualSorting)
            {
                return;
            }

            CacheSpriteRenderersIfNeeded();

            if (spriteRenderers == null || spriteRenderers.Length == 0)
            {
                return;
            }

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = spriteRenderers[i];

                if (renderer == null)
                {
                    continue;
                }

                renderer.sortingLayerName = puddleSortingLayerName;
                renderer.sortingOrder = puddleOrderInLayer;
            }
        }
    }
}