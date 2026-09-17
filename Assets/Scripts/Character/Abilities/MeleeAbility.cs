using UnityEngine;

namespace Vampire
{
    public abstract class MeleeAbility : Ability
    {
        [Header("Melee Stats")]
        [SerializeField] protected LayerMask targetLayer;
        [SerializeField] protected UpgradeableDamage damage;
        [SerializeField] protected UpgradeableKnockback knockback;
        [SerializeField] protected UpgradeableWeaponCooldown cooldown;
        [SerializeField] protected SpriteRenderer weaponSpriteRenderer;
        protected float timeSinceLastAttack;

        protected override void Use()
        {
            base.Use();
            gameObject.SetActive(true);
            timeSinceLastAttack = cooldown.Value;
        }

        void Update()
        {
            if (AttacksBlocked) return;
            timeSinceLastAttack += Time.deltaTime;

            //  적용: 실제 쿨타임 = 기본 쿨타임 / 공격 속도 배율
            float effectiveCooldown = cooldown.Value / playerCharacter.AttackSpeedMultiplier;

            if (timeSinceLastAttack >= effectiveCooldown)
            {
                timeSinceLastAttack = Mathf.Repeat(timeSinceLastAttack, effectiveCooldown);
                Attack();
            }
        }

        protected abstract void Attack();
    }
}
