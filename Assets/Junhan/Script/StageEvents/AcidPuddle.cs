using UnityEngine;

namespace Vampire
{
    public class AcidPuddle : MonoBehaviour
    {
        [Header("Runtime Settings")]
        [SerializeField] private float lifeTime = 6f;
        [SerializeField] private float damagePerTick = 3f;
        [SerializeField] private float tickInterval = 0.5f;
        [SerializeField] private bool damageImmediatelyOnEnter = false;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color activeColor = new Color(0.4f, 1f, 0.1f, 0.55f);
        [SerializeField] private Color warningColor = new Color(0.4f, 1f, 0.1f, 0.25f);
        [SerializeField] private float warningDuration = 0.4f;

        private Character targetCharacter;
        private float tickTimer;
        private float lifeTimer;
        private float warningTimer;
        private bool activeDamage = false;

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

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = warningColor;
            }

            activeDamage = this.warningDuration <= 0f;

            if (activeDamage && spriteRenderer != null)
            {
                spriteRenderer.color = activeColor;
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

                if (spriteRenderer != null)
                {
                    spriteRenderer.color = activeColor;
                }
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
    }
}