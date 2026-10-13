using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Swept relative motion catches fast needles crossing between rendered frames.
    [DefaultExecutionOrder(500)]
    public sealed class FormationNeedleCollision : MonoBehaviour
    {
        private static readonly List<FormationNeedleCollision> active = new List<FormationNeedleCollision>();
        private static int evaluatedFrame = -1;
        private SyringeProjectile needle;
        private AcupunctureFormationController formation;
        private Character source;
        private LayerMask layer;
        private int burst;
        private float damage;
        private Vector2 previous;
        private bool leased;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() { active.Clear(); evaluatedFrame = -1; }
        public void Initialize(SyringeProjectile projectile, AcupunctureFormationController owner, int id, float amount, Character character, LayerMask mask)
        {
            ResetLease();
            needle = projectile; formation = owner; burst = id; damage = amount; source = character; layer = mask;
            previous = transform.position; leased = true; active.Add(this);
        }
        public void ResetLease() { leased = false; active.Remove(this); }
        private void OnDisable() { ResetLease(); }
        private bool Flying => leased && needle != null && needle.IsFlying && source != null && source.IsAlive;
        public static bool Crossed(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, float radius)
        {
            Vector2 start = a0 - b0, delta = (a1 - a0) - (b1 - b0);
            float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector2.Dot(start, delta) / delta.sqrMagnitude) : 0;
            return (start + delta * t).sqrMagnitude <= radius * radius;
        }
        private void LateUpdate()
        {
            if (Time.timeScale <= 0 || evaluatedFrame == Time.frameCount) return;
            evaluatedFrame = Time.frameCount;
            active.RemoveAll(x => x == null || !x.Flying);
            // Do not mutate the registry during collision resolution: pooling invokes OnDisable.
            var frame = active.ToArray();
            for (int i = 0; i < frame.Length; i++)
            {
                var a = frame[i];
                if (!a.Flying) continue;
                for (int j = i + 1; j < frame.Length; j++)
                {
                    var b = frame[j];
                    if (!b.Flying || a.formation != b.formation || a.burst == b.burst) continue;
                    if (!Crossed(a.previous, a.transform.position, b.previous, b.transform.position, .14f)) continue;
                    Vector2 relative = a.previous - b.previous;
                    Vector2 delta = ((Vector2)a.transform.position - a.previous) - ((Vector2)b.transform.position - b.previous);
                    float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector2.Dot(relative, delta) / delta.sqrMagnitude) : 0;
                    Vector2 position = (Vector2.Lerp(a.previous, a.transform.position, t) + Vector2.Lerp(b.previous, b.transform.position, t)) * .5f;
                    a.leased = b.leased = false;
                    a.needle.RemoveForItem(); b.needle.RemoveForItem();
                    SyringeAugmentVfx.PlayRadius("Explosion", position, 1f);
                    Ver4HitEffects.AreaDamage(position, 1f, Mathf.Max(a.damage, b.damage), 0, a.source, a.layer, "침술진");
                    break;
                }
            }
            foreach (var item in active) if (item != null) item.previous = item.transform.position;
        }
    }
}
