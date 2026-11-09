using UnityEngine;

namespace Vampire
{
    /// <summary>Breakable field debris, not a scoring monster.</summary>
    public sealed class ColonResidue : IDamageable
    {
        ColonFieldEvents owner;
        SpriteRenderer visual;
        Collider2D hitbox;
        float age, health = 1, speed, spin;
        bool broken;
        Vector3 originalScale;
        public bool Broken => broken;
        public static ColonResidue Create(ColonFieldEvents events, Vector2 at, int index)
        {
            var go = new GameObject("굴러오는 장내 찌꺼기"); go.transform.SetParent(events.transform, false); go.transform.position = at;
            var r = go.AddComponent<ColonResidue>(); r.owner = events;
            r.visual = ColonEventVisual.Sprite(go.transform, "찌꺼기 덩어리", ColonSpecialEffects.Art("beetle", 11), at, .54f + (index % 3) * .08f);
            r.originalScale = r.visual.transform.localScale; r.visual.transform.localPosition = Vector3.up * .25f;
            r.speed = 2.2f + (index % 3) * .20f; r.spin = index % 2 == 0 ? -135 : 125;
            go.AddComponent<ZPositioner>(); var rb = go.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic; rb.gravityScale = 0;
            var hit = new GameObject("찌꺼기 공격 판정"); hit.transform.SetParent(go.transform, false); hit.layer = LayerMask.NameToLayer("Monster Full");
            var box = hit.AddComponent<CircleCollider2D>(); box.isTrigger = true; box.radius = .32f; box.offset = Vector2.up * .22f; r.hitbox = box;
            return r;
        }
        void Update()
        {
            if (owner == null || owner.Paused) return;
            if (broken)
            { age += Time.deltaTime; visual.transform.localScale = originalScale * (1 + age * .9f); visual.color = new Color(1, 1, 1, Mathf.Clamp01(1 - age / .25f)); if (age >= .25f) gameObject.SetActive(false); return; }
            age += Time.deltaTime;
            var before = (Vector2)transform.position; Vector2 dir = owner.Flow.Direction(before);
            Vector2 next = ColonAttackWall.Stop(before, before + dir * speed * Time.deltaTime, .35f);
            transform.position = new Vector3(next.x, next.y, transform.position.z); GetComponent<Rigidbody2D>().position = next;
            visual.transform.localRotation = Quaternion.Euler(0, 0, age * spin);
            // Spin around the visible lump's centre, even when source art uses a foot pivot.
            visual.transform.localPosition = Vector3.up * (.32f + Mathf.Abs(Mathf.Sin(age * 7)) * .035f) -
                visual.transform.localRotation * Vector3.Scale(visual.sprite.bounds.center, originalScale);
            if (Vector2.Distance(next, owner.Player.Position) < .45f) owner.HitByResidue(next);
            if (age > 9 || dir.sqrMagnitude < .1f) { hitbox.enabled = false; gameObject.SetActive(false); }
        }
        public override void Knockback(Vector2 amount) { }
        public override void TakeDamage(float damage, Vector2 knockback = default, bool isCritical = false)
        {
            if (broken || owner == null || !owner.Running || owner.Current != ColonEventKind.Residue || damage <= 0) return;
            health -= damage; if (health > 0) return;
            broken = true; age = 0; hitbox.enabled = false; owner.Broken++;
            ColonSpecialEffects.Puff(transform.position, new Color(.72f, .46f, .27f), .35f);
        }
    }
}
