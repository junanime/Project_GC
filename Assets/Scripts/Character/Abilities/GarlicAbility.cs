using UnityEngine;

namespace Vampire
{
    public class GarlicAbility : Ability
    {
        [Header("Garlic Stats")]
        [SerializeField] protected LayerMask monsterLayer;
        [SerializeField] protected UpgradeableDamage damage;
        [SerializeField] protected UpgradeableAOE radius;
        [SerializeField] protected UpgradeableDamageRate damageRate;
        [SerializeField] protected UpgradeableKnockback knockback;

        private float timeSinceLastAttack;
        private FastList<GameObject> hitMonsters;
        private CircleCollider2D damageCollider;
        private SpriteRenderer spriteRenderer;


        // =========================================================
        // Awake
        // =========================================================

        private void Awake()
        {
            damageCollider =
                GetComponent<CircleCollider2D>();

            spriteRenderer =
                GetComponentInChildren<SpriteRenderer>();
        }


        // =========================================================
        // Init
        // =========================================================

        public override void Init(
            AbilityManager abilityManager,
            EntityManager entityManager,
            Character playerCharacter
        )
        {
            base.Init(
                abilityManager,
                entityManager,
                playerCharacter
            );

            transform.SetParent(
                playerCharacter.transform
            );

            transform.localPosition =
                Vector3.zero;
        }


        // =========================================================
        // Use
        // =========================================================

        protected override void Use()
        {
            base.Use();

            gameObject.SetActive(true);

            hitMonsters =
                new FastList<GameObject>();


            if (damageCollider != null)
            {
                damageCollider.radius =
                    radius.Value;
            }


            if (spriteRenderer != null)
            {
                spriteRenderer.transform.localScale =
                    Vector3.one *
                    radius.Value *
                    2f;
            }
        }


        // =========================================================
        // Upgrade
        // =========================================================

        protected override void Upgrade()
        {
            base.Upgrade();


            if (damageCollider != null)
            {
                damageCollider.radius =
                    radius.Value;
            }


            if (spriteRenderer != null)
            {
                spriteRenderer.transform.localScale =
                    Vector3.one *
                    radius.Value *
                    2f;
            }
        }


        // =========================================================
        // Update
        // =========================================================

        private void Update()
        {
            float safeDamageRate =
                Mathf.Max(
                    0.01f,
                    damageRate.Value
                );


            float attackInterval =
                1f / safeDamageRate;


            timeSinceLastAttack +=
                Time.deltaTime;


            if (timeSinceLastAttack >= attackInterval)
            {
                Collider2D[] hitColliders =
                    Physics2D.OverlapCircleAll(
                        transform.position,
                        radius.Value,
                        monsterLayer
                    );


                foreach (
                    Collider2D collider in hitColliders
                )
                {
                    IDamageable damageable =
                        collider.GetComponentInParent<IDamageable>();


                    if (damageable == null)
                    {
                        continue;
                    }


                    Damage(
                        damageable
                    );
                }


                timeSinceLastAttack =
                    Mathf.Repeat(
                        timeSinceLastAttack,
                        attackInterval
                    );
            }
        }


        // =========================================================
        // Damage
        // =========================================================

        private void Damage(
            IDamageable damageable
        )
        {
            if (damageable == null)
            {
                return;
            }


            Vector2 knockbackDirection =
                (
                    damageable.transform.position -
                    transform.position
                ).normalized;


            float dealtDamage =
                damage.Value;


            // 실제 피해
            damageable.TakeDamage(
                dealtDamage,
                knockback.Value *
                knockbackDirection
            );


            // =====================================================
            // 피해량 기록
            // =====================================================
            //
            // 기존:
            //
            // playerCharacter.OnDealDamage.Invoke(
            //     damage.Value
            // );
            //
            // 변경:
            //
            // ReportDamage()
            //
            // 1. StatsManager 총 피해량
            // 2. AugmentDamageTracker 증강별 피해량
            //
            // 을 동시에 기록
            // =====================================================

            ReportDamage(
                dealtDamage
            );
        }


        // =========================================================
        // Deregister Monster
        // =========================================================

        private void DeregisterMonster(
            Monster monster
        )
        {
            if (
                monster == null ||
                hitMonsters == null
            )
            {
                return;
            }


            hitMonsters.Remove(
                monster.gameObject
            );
        }


        // =========================================================
        // Trigger Enter
        // =========================================================

        private void OnTriggerEnter2D(
            Collider2D collider
        )
        {
            if (hitMonsters == null)
            {
                return;
            }


            bool isMonsterLayer =
                (monsterLayer &
                 (1 << collider.gameObject.layer)) != 0;


            if (!isMonsterLayer)
            {
                return;
            }


            if (
                hitMonsters.Contains(
                    collider.gameObject
                )
            )
            {
                return;
            }


            Monster monster =
                collider.gameObject
                    .GetComponentInParent<Monster>();


            if (monster == null)
            {
                return;
            }


            hitMonsters.Add(
                collider.gameObject
            );


            monster.OnKilled.AddListener(
                DeregisterMonster
            );


            // 범위에 처음 들어온 순간 1회 피해
            Damage(
                monster
            );
        }
    }
}