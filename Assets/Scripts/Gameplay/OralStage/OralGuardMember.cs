using UnityEngine;

namespace Vampire
{
    // Neutral trigger, not an enemy: player weapons cannot select or hurt it.
    // Enemy Projectile explicitly recognizes it before checking its player mask.
    public sealed class OralGuardMember : IDamageable
    {
        OralGuardEscort escort;
        CircleCollider2D hitbox;
        int index;
        public OralGuardEscort Escort => escort;
        public bool Vulnerable => escort != null && escort.MemberWalking(index);
        public void Configure(OralGuardEscort owner, int memberIndex, float height)
        {
            escort = owner; index = memberIndex;
            gameObject.layer = 0;
            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            hitbox = gameObject.AddComponent<CircleCollider2D>();
            hitbox.isTrigger = true; hitbox.radius = Mathf.Max(.18f, height * .36f);
            hitbox.offset = Vector2.up * height * .3f;
        }
        public void SetEnabled(bool value) { if (hitbox != null) hitbox.enabled = value; }
        public void ReceiveEnemyProjectile(float damage)
        { if (escort != null) escort.TakeMemberDamage(index, damage); }
        // Generic weapon/AOE damage has no enemy source. Never allow friendly fire.
        public override void TakeDamage(float damage, Vector2 knockback = default, bool isCritical = false) { }
        // Escort route stays on the tongue; hits never shove a member into a chasm.
        public override void Knockback(Vector2 knockback) { }
    }
}
