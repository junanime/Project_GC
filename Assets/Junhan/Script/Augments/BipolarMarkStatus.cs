using UnityEngine;

namespace Vampire
{
    // One pooled enemy owns one mark. Consuming a mark never dispatches another needle hit.
    public sealed class BipolarMarkStatus : MonoBehaviour, ICombatStatus
    {
        public CombatStatusTag ActiveStatusTags => Polarity != 0 && Time.time < expires ? CombatStatusTag.Mark : CombatStatusTag.None;
        public const float Duration = 4f;
        public int Polarity { get; private set; }
        private float expires;
        private Component target;
        private Character source;
        private SyringeAugmentVfx effect;

        public static float Bonus(int stage) => stage <= 0 ? 0f : .2f + .2f * Mathf.Clamp(stage, 1, 3);

        public void Hit(Component victim, int incoming, int stage, float hitDamage, Character owner)
        {
            if (incoming == 0 || stage <= 0 || Ver4HitEffects.Health(victim) <= 0) return;
            if (Time.time >= expires || source != owner) Clear();
            target = victim;
            source = owner;
            incoming = incoming > 0 ? 1 : -1;
            if (Polarity != 0 && Polarity != incoming)
            {
                var tags=CombatStatusRules.ActiveTags(victim);
                Clear();
                Ver4HitEffects.Damage(victim, hitDamage * Bonus(stage), Vector2.zero, owner, "양극침 — 반극 낙인",statusBeforeHit:tags);
                return;
            }
            Polarity = incoming;
            expires = Time.time + Duration;
            if (effect == null)
                effect = SyringeAugmentVfx.Play(incoming > 0 ? "YangMark" : "YinMark", victim.transform.position, SyringeAugmentVfx.FindTarget(victim));
        }

        private void Update()
        {
            if (target is Monster monster && monster.IsFieldRuntimeSuspended) { expires += Time.deltaTime; return; }
            if (Polarity != 0 && (Time.time >= expires || target == null || Ver4HitEffects.Health(target) <= 0)) Clear();
        }

        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();
        private void Clear()
        {
            SyringeAugmentVfx.ReleaseOwned(ref effect);
            Polarity = 0;
            expires = 0;
            target = null;
            source = null;
        }
    }
}
