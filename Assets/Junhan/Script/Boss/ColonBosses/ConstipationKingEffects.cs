using UnityEngine;

namespace Vampire
{
    public static class ConstipationKingEffects
    {
        public static LineRenderer Line(ConstipationKingRuntime boss, Vector2[] points, float width, Color color, int order = 510)
        {
            var line = FoodBossEffects.Line(boss.Effects, points, width, color, false, order, true);
            ColonAttackWall.Mask(line.gameObject, points[0]); return line;
        }
        public static LineRenderer Warning(ConstipationKingRuntime boss, Vector2[] points, Vector2 origin)
        {
            var line = FoodBossEffects.Line(boss.Effects, points, .075f, ConstipationKingRuntime.WarningColor, false, 480);
            ColonAttackWall.Mask(line.gameObject, origin); return line;
        }
        public static SpriteRenderer Prop(ConstipationKingRuntime boss, string key, Vector2 at, float width, Color fallback)
        {
            var go = new GameObject("King effect · " + key); go.transform.SetParent(boss.Effects, false); go.transform.position = at;
            var art = go.AddComponent<SpriteRenderer>(); art.sortingOrder = 525;
            art.sprite = ConstipationKingVisual.Get(key) ?? ConstipationKingPropArt.Get(key);
            if (art.sprite == null) { art.sprite = BombCircleArt.Sprite; art.color = fallback; }
            go.transform.localScale = Vector3.one * width / Mathf.Max(.01f, art.sprite.bounds.size.x);
            return art;
        }
        public static GameObject Booster(ConstipationKingRuntime boss, Vector2 direction)
        {
            var art = Prop(boss, "Booster0", boss.Visual.Outlet, 4, Color.cyan);
            art.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180);
            art.gameObject.AddComponent<ConstipationKingBooster>().Initialize(boss); return art.gameObject;
        }
        public static GameObject Overflow(ConstipationKingRuntime boss, float radius)
        {
            return ConstipationKingOverflowVisual.Create(boss, radius).gameObject;
        }
    }

    public sealed class ConstipationKingProjectile : MonoBehaviour
    {
        ConstipationKingRuntime owner;
        Vector2 velocity;
        float damage, radius, age;
        bool split;
        public Vector2 Velocity => velocity;
        public static ConstipationKingProjectile Create(ConstipationKingRuntime boss, Vector2 at, Vector2 direction, float speed, float damage, float radius, bool split = false)
        {
            var art = ConstipationKingEffects.Prop(boss, "PoopProjectile", at, radius * 2.15f, new Color(.57f, .29f, .1f));
            var p = art.gameObject.AddComponent<ConstipationKingProjectile>(); p.owner = boss; p.velocity = direction.normalized * speed; p.damage = damage; p.radius = radius; p.split = split;
            OctoberEnemyProjectile.Register(art.gameObject, true); return p;
        }
        void Update()
        {
            if (owner == null || owner.Dead) { Destroy(gameObject); return; }
            if (owner.EncounterPaused || owner.Immobilized) return;
            age += Time.deltaTime; Vector2 from = transform.position, to = from + velocity * Time.deltaTime;
            if (age > 7 || !ColonAttackWall.Clear(from, to, radius)) { Destroy(gameObject); return; }
            transform.position = to;
            if (SnailBossProjectile.SegmentDistance(owner.Player.transform.position, from, to) < radius + .18f && ColonAttackWall.Clear(from, owner.Player.transform.position, .1f))
            { owner.TryHit(damage); Destroy(gameObject); return; }
            if (split && age >= 1.05f)
            {
                for (int i = -2; i <= 2; i++) Create(owner, to, FoodBossRuntime.Rotate(velocity.normalized, i * 20), 6, 14, .23f);
                Destroy(gameObject);
            }
        }
    }

    public sealed class ConstipationKingBooster : MonoBehaviour
    {
        ConstipationKingRuntime owner; SpriteRenderer art; float age;
        public void Initialize(ConstipationKingRuntime boss) { owner = boss; art = GetComponent<SpriteRenderer>(); }
        void Update()
        {
            if (owner == null || owner.Dead) { Destroy(gameObject); return; }
            if (owner.EncounterPaused || owner.Immobilized) return;
            age += Time.deltaTime; var sprite = ConstipationKingVisual.Get("Booster" + ((int)(age * 12) % 4)); if (sprite != null) art.sprite = sprite;
        }
    }

    public sealed class ConstipationKingHookBranch : MonoBehaviour
    {
        ConstipationKingRuntime owner; Vector2 from, to, previous; LineRenderer rope; SpriteRenderer cup; float age; bool hit;
        public static void Create(ConstipationKingRuntime boss, Vector2 from, Vector2 to)
        {
            var root = new GameObject("Plunger · forward 45 degree branch"); root.transform.SetParent(boss.Effects, true);
            var b = root.AddComponent<ConstipationKingHookBranch>(); b.owner = boss; b.from = b.previous = from; b.to = to;
            b.rope = ConstipationKingEffects.Line(boss, new[] { from, from }, .05f, new Color(.6f, .4f, .18f), 521); b.rope.transform.SetParent(root.transform, true);
            b.cup = ConstipationKingEffects.Prop(boss, "Plunger", from, .7f, new Color(.7f, .24f, .15f)); b.cup.transform.SetParent(root.transform, true);
            b.cup.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
        }
        void Update()
        {
            if (owner == null || owner.Dead) { Destroy(gameObject); return; }
            if (owner.EncounterPaused || owner.Immobilized) return;
            age += Time.deltaTime;
            if (age >= 1.6f) { Destroy(gameObject); return; }
            float u = age < .65f ? age / .65f : age < 1 ? 1 : 1 - (age - 1) / .6f;
            var point = Vector2.Lerp(from, to, Mathf.Clamp01(u)); cup.transform.position = point; rope.SetPosition(1, point);
            if (!hit && ColonAttackWall.Clear(from, owner.Player.transform.position) && SnailBossProjectile.SegmentDistance(owner.Player.transform.position, previous, point) < .5f)
            { owner.TryHit(18); hit = true; }
            previous = point;
        }
    }

    public sealed class ConstipationKingPaper : MonoBehaviour
    {
        ConstipationKingRuntime owner; Vector2 from, end, direction; LineRenderer paper, warning; SpriteRenderer roll; float age; bool rewind;
        MaterialPropertyBlock paperProperties;
        public Vector2 Origin => from;
        public Vector2 Tip { get; private set; }
        public static void Create(ConstipationKingRuntime boss, Vector2 from, Vector2 end, bool rewind)
        {
            var go = new GameObject(rewind ? "Three-way paper · straight rewind" : "Paper roll · forward escalator"); go.transform.SetParent(boss.Effects, true);
            var p = go.AddComponent<ConstipationKingPaper>(); p.owner = boss; p.from = p.Tip = from; p.end = end; p.direction = (end - from).normalized; p.rewind = rewind;
            p.paperProperties = new MaterialPropertyBlock();
            p.warning = ConstipationKingEffects.Warning(boss, ColonBossEffects.Lane(from, end, 1), from); p.warning.transform.SetParent(go.transform, true);
            p.paper = ConstipationKingEffects.Line(boss, new[] { from, from }, .82f, new Color(1, .97f, .83f), 518); p.paper.transform.SetParent(go.transform, true);
            if (ConstipationKingPropArt.PaperMaterial != null) p.paper.sharedMaterial = ConstipationKingPropArt.PaperMaterial;
            p.roll = ConstipationKingEffects.Prop(boss, "PaperRoll", from, 1.05f, new Color(1, .96f, .82f)); p.roll.transform.SetParent(go.transform, true);
            p.paper.enabled = p.roll.enabled = false;
        }
        void Update()
        {
            if (owner == null || owner.Dead) { Destroy(gameObject); return; }
            if (owner.EncounterPaused || owner.Immobilized) return;
            age += Time.deltaTime;
            if (age < .85f) return;
            if (warning != null) { Destroy(warning.gameObject); warning = null; }
            paper.enabled = roll.enabled = true;
            float t = age - .85f;
            if (!rewind && t >= 2.1f)
            {
                // Flight and disappearance begin at maximum range, with no late rolling delay.
                Vector2 offset = direction * ((t - 2.1f) * 7) + Vector2.up * ((t - 2.1f) * 4);
                paper.SetPosition(0, from + offset); paper.SetPosition(1, end + offset); roll.transform.position = end + offset;
                float alpha = Mathf.Clamp01(1 - (t - 2.1f) / .35f);
                paper.startColor = paper.endColor = new Color(1, .97f, .83f, alpha); roll.color = new Color(1, 1, 1, alpha);
                if (t >= 2.45f) Destroy(gameObject); return;
            }
            if (rewind && t >= 4.1f) { Destroy(gameObject); return; }
            float progress = t < 2.1f ? t / 2.1f : 1 - (t - 2.1f) / 2;
            Tip = Vector2.Lerp(from, end, Mathf.Clamp01(progress)); roll.transform.position = Tip;
            paper.SetPosition(0, from); paper.SetPosition(1, Tip);
            paper.GetPropertyBlock(paperProperties);
            paperProperties.SetFloat("_Length", Vector2.Distance(from, Tip));
            paperProperties.SetFloat("_FlowDistance", t < 2.1f ? t * 2.5f : 5.25f - (t - 2.1f) * 2);
            paper.SetPropertyBlock(paperProperties);
            // The original source remains fixed throughout the return; never rotate the lane.
            if (ColonAttackWall.Clear(from, owner.Player.transform.position) && SnailBossProjectile.SegmentDistance(owner.Player.transform.position, from, Tip) < .59f)
            {
                owner.PushPlayer(direction * (t < 2.1f ? 2.5f : -2) * Time.deltaTime, from);
                owner.TryHit(8);
            }
        }
    }

    public sealed class ConstipationKingVortex : MonoBehaviour
    {
        ConstipationKingRuntime owner; Vector2 center; float age, duration, radius; LineRenderer[] swirls; LineRenderer warning;
        public Vector2 Center => center;
        public float Radius => radius;
        public float CurrentRadius { get; private set; }
        public float Age => age;
        public static void Create(ConstipationKingRuntime boss, Vector2 center, LineRenderer warning, float duration, float radius = 2.6f)
        {
            var go = new GameObject("Flush · independent vortex"); go.transform.SetParent(boss.Effects, true); go.transform.position = center;
            var v = go.AddComponent<ConstipationKingVortex>(); v.owner = boss; v.center = center; v.duration = duration; v.radius = radius; v.warning = warning;
            warning.transform.SetParent(go.transform, true); v.swirls = new LineRenderer[3];
            for (int i = 0; i < 3; i++) { v.swirls[i] = ConstipationKingEffects.Line(boss, new[] { center, center }, .19f * radius / 2.6f, new Color(.27f, .8f, .95f, .85f), 486); v.swirls[i].transform.SetParent(go.transform, true); }
        }
        void Update()
        {
            if (owner == null || owner.Dead) { Destroy(gameObject); return; }
            if (owner.EncounterPaused || owner.Immobilized) return;
            age += Time.deltaTime; if (age >= duration) { Destroy(gameObject); return; }
            CurrentRadius = radius * Mathf.Sqrt(Mathf.SmoothStep(0, 1, age / 1.0f)) * Mathf.SmoothStep(0, 1, (duration - age) / .8f);
            for (int ring = 0; ring < swirls.Length; ring++)
            {
                var points = new Vector2[41];
                float outer = Mathf.Max(0, CurrentRadius - swirls[ring].startWidth * .5f);
                swirls[ring].enabled = outer > .01f;
                for (int i = 0; i < points.Length; i++) { float u = i / 40f, a = age * 230 + ring * 120 + u * 280; points[i] = center + FoodBossRuntime.Dir(a) * Mathf.Lerp(outer * .08f, outer, u); }
                swirls[ring].positionCount = points.Length; for (int i = 0; i < points.Length; i++) swirls[ring].SetPosition(i, points[i]);
            }
            Vector2 delta = center - (Vector2)owner.Player.transform.position;
            if (delta.magnitude < CurrentRadius && ColonAttackWall.Clear(center, owner.Player.transform.position, .18f))
            {
                owner.PushPlayer(delta.normalized * Time.deltaTime * 2, center);
                if (delta.magnitude < CurrentRadius * (.7f / 2.6f)) owner.TryHit(14);
            }
        }
    }

    public sealed class ConstipationKingMinion : IDamageable
    {
        public float Health { get; private set; }
        public float MaxHealth => tribute ? 95 : 120;
        public bool Targetable => Health > 0 && owner != null && !owner.Dead && !owner.EncounterPaused;
        ConstipationKingRuntime owner; bool tribute; float age, contact, delivery, chewing;
        bool mealStarted;
        public bool IsTribute => tribute;
        public bool IsChewing => mealStarted;
        ConstipationKingCarrierVisual carrier;
        public static ConstipationKingMinion Create(ConstipationKingRuntime boss, Vector2 at, bool tribute, int variant)
        {
            var art = ConstipationKingEffects.Prop(boss, "PoopMinion", at, 1.1f, new Color(.55f, .27f, .1f));
            if (!tribute) { var sprite = Resources.Load<Sprite>("ColonMonsters/N01"); if (sprite != null) { art.sprite = sprite; art.color = Color.white; art.transform.localScale = Vector3.one * 1.1f / sprite.bounds.size.x; } }
            var m = art.gameObject.AddComponent<ConstipationKingMinion>(); m.owner = boss; m.tribute = tribute; m.Health = m.MaxHealth;
            if (tribute) m.carrier = ConstipationKingPropArt.ConfigureCarrier(m.gameObject, variant);
            m.gameObject.layer = LayerMask.NameToLayer("Monster Full");
            var rb = m.gameObject.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic; rb.gravityScale = 0;
            var col = m.gameObject.AddComponent<CircleCollider2D>(); col.isTrigger = true; col.radius = .4f / m.transform.localScale.x;
            return m;
        }
        void Update()
        {
            if (Health <= 0 || owner == null || owner.Dead) { Destroy(gameObject); return; }
            if (owner.EncounterPaused || owner.Immobilized) return;
            if (tribute && (owner.PhaseTwo || owner.Transitioning || owner.CurrentPattern != 7)) { Destroy(gameObject); return; }
            if (mealStarted)
            {
                chewing += Time.deltaTime; owner.Visual.SetPoseProgress(Mathf.Clamp01(chewing / .65f));
                carrier?.Tick(Time.deltaTime, Vector2.zero, true, 1, owner.Visual.Mouth);
                if (chewing >= .65f) { owner.FinishTributeMeal(owner.MaxHealth * .025f); Destroy(gameObject); }
                return;
            }
            age += Time.deltaTime; if (age > 24) { Destroy(gameObject); return; }
            Vector2 from = transform.position, target = tribute ? (Vector2)owner.transform.position : (Vector2)owner.Player.transform.position;
            Vector2 direction = ColonNavigation.Toward(from, target);
            if (GetComponent<NeuralBlockedMonsterStatus>()?.Active == true) return;
            bool presenting = tribute && Vector2.Distance(from, target) < 1.6f && ColonAttackWall.Clear(from, target);
            if (presenting)
            {
                delivery += Time.deltaTime;
                carrier?.Tick(Time.deltaTime, direction, true, Mathf.Clamp01(delivery), owner.Visual.Mouth);
                if (delivery >= 1)
                {
                    mealStarted = owner.BeginTributeMeal();
                    if (!mealStarted) Destroy(gameObject);
                }
                return;
            }
            delivery = 0; carrier?.Tick(Time.deltaTime, direction, false, 0, owner.Visual.Mouth);
            float slow = Mathf.Min(GetComponent<FoodBossCombatStatus>()?.SpeedMultiplier ?? 1, GetComponent<HoneySlowStatus>()?.SpeedMultiplier ?? 1) * (GetComponent<IceChillStatus>()?.SpeedMultiplier ?? 1);
            transform.position = ColonAttackWall.Stop(from, from + direction * (tribute ? 1.35f : 1.2f) * slow * Time.deltaTime, .4f);
            if (Vector2.Distance(transform.position, target) < (tribute ? 1.6f : .65f) && ColonAttackWall.Clear(transform.position, target))
            {
                if (!tribute && Time.time >= contact) { owner.TryHit(10); contact = Time.time + 1; }
            }
        }
        public override void TakeDamage(float damage, Vector2 knockback = default, bool isCritical = false)
        {
            if (owner == null || owner.EncounterPaused || Health <= 0 || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            Health = Mathf.Max(0, Health - damage);
            if (Health <= 0)
            {
                GetComponent<FoodBossCombatStatus>()?.Defeated(); RunScore.Current?.NormalKill();
                owner.Level.EntityManager.SpawnExpGem(FiniteStomachMap.SafeSpawn(transform.position, .18f), GemType.White1, true);
                Destroy(gameObject);
            }
        }
        public override void Knockback(Vector2 delta) { }
    }

    public sealed class ConstipationKingShield : IDamageable
    {
        ConstipationKingRuntime owner; int slot; float age; SpriteRenderer art;
        public static void Create(ConstipationKingRuntime boss, int slot)
        {
            var art = ConstipationKingEffects.Prop(boss, "Crown", boss.HitCenter, 1.15f, new Color(1, .8f, .25f));
            var s = art.gameObject.AddComponent<ConstipationKingShield>(); s.owner = boss; s.slot = slot; s.art = art;
            s.gameObject.layer = LayerMask.NameToLayer("Monster Full");
            var rb = s.gameObject.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic; rb.gravityScale = 0;
            var col = s.gameObject.AddComponent<CircleCollider2D>(); col.isTrigger = true; col.radius = .55f / s.transform.localScale.x;
        }
        void Update()
        {
            if (owner == null || owner.Dead) { Destroy(gameObject); return; }
            if (owner.EncounterPaused || owner.Immobilized) return;
            age += Time.deltaTime; if (age >= 4.5f) { Destroy(gameObject); return; }
            Vector2 desired = owner.HitCenter + FoodBossRuntime.Dir(slot * 120 + age * 45) * 2.1f;
            transform.position = ColonAttackWall.Stop(owner.HitCenter, desired, .55f);
            // The upper half of the orbit passes behind the authored body; lower
            // crowns guard its front. The collider follows the same world orbit.
            if (art != null && owner.Visual != null && owner.Visual.Body != null)
                art.sortingOrder = owner.Visual.Body.sortingOrder + (transform.position.y > owner.HitCenter.y ? -1 : 15);
        }
        public static bool TryReflect(Projectile projectile, Collider2D target)
        {
            var shield = target != null ? target.GetComponent<ConstipationKingShield>() : null;
            if (shield == null || shield.owner == null || !shield.owner.Targetable || !projectile.IsPlayerAttack) return false;
            Vector2 at = projectile.transform.position;
            var reflected = ConstipationKingProjectile.Create(shield.owner, at, shield.owner.AimFrom(at), 6.5f, 10, .18f);
            var original = projectile.GetComponentInChildren<SpriteRenderer>();
            if (original != null && original.sprite != null)
            {
                var art = reflected.GetComponent<SpriteRenderer>(); art.sprite = original.sprite; art.color = original.color;
                art.flipX = original.flipX; art.flipY = original.flipY;
                reflected.transform.localScale = original.transform.lossyScale;
                reflected.transform.rotation = Quaternion.Euler(0, 0, original.transform.eulerAngles.z + Vector2.SignedAngle(projectile.TravelDirection, reflected.Velocity));
            }
            shield.owner.RegisterReflection();
            projectile.RemoveForItem(); return true;
        }
        public override void TakeDamage(float amount, Vector2 knockback = default, bool isCritical = false) { }
        public override void Knockback(Vector2 amount) { }
    }
}
