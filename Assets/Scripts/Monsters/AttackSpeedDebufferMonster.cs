using UnityEngine;
namespace Vampire
{
    // Keep this script's GUID so existing pools and scene references remain valid.
    public class AttackSpeedDebufferMonster : MeleeMonster
    {
        private float nextDebuffTime;

        public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint blueprint, float hpBuff = 0)
        {
            base.Setup(monsterIndex, position, blueprint, hpBuff);
            nextDebuffTime = 0f;
            if (rb != null) rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        protected override void Update()
        {
            if (IsFieldRuntimeSuspended) return;
            base.Update();
        }

        protected override void FixedUpdate()
        {
            if (IsFieldRuntimeSuspended) return;
            base.FixedUpdate();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryApplyContactDebuff(collision != null ? collision.collider : null);
        }

        protected override void OnCollisionStay2D(Collision2D collision)
        {
            if (IsFieldRuntimeSuspended) return;
            TryApplyContactDebuff(collision != null ? collision.collider : null);
            base.OnCollisionStay2D(collision);
        }

        private void TryApplyContactDebuff(Collider2D other)
        {
            if (!alive || IsFieldRuntimeSuspended || other == null || playerCharacter == null ||
                Time.time < nextDebuffTime ||
                meleeMonsterBlueprint is not AttackSpeedDebufferMonsterBlueprint blueprint)
                return;

            if ((blueprint.meleeLayer & (1 << other.gameObject.layer)) == 0 ||
                other.GetComponentInParent<Character>() != playerCharacter)
                return;

            var runtime = playerCharacter.GetComponent<PlayerAttackSpeedDebuffRuntime>();
            if (runtime == null)
                runtime = playerCharacter.gameObject.AddComponent<PlayerAttackSpeedDebuffRuntime>();
            runtime.Apply(blueprint.debuffDuration, new Color(1f, 0.92156863f, 0.015686275f),
                0.3f, 0.8f, 0.6f, 1.5f, 5, 2.2f, 150, false);
            nextDebuffTime = Time.time + Mathf.Max(0.1f, blueprint.contactDebuffCooldown);
        }

        protected override void OnFieldRuntimeSuspended()
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (monsterSpriteAnimator != null) monsterSpriteAnimator.StopAnimating();
        }

        protected override void OnFieldRuntimeResumed()
        {
            if (alive && monsterSpriteAnimator != null) monsterSpriteAnimator.StartAnimating();
        }
    }
}
