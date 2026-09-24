using UnityEngine;
namespace Vampire
{
    public static class IceSkillRules
    {
        public const float FreezeDuration = 5f;
        public const float ActiveRadius = 4f;
        public const float ActiveCooldown = 60f;
        public const float InstantFreezeChance = .10f;
        // Gameplay release and the visual fade share a single timing source.
        public const float BlizzardFreezeDelay = 1.65f;
        public static void Freeze(Component target)
        {
            if (target == null || Ver4HitEffects.Health(target) <= 0) return;
            target.GetComponent<IceChillStatus>()?.Clear();
            // Preserve the existing boss resistance: chill instead of a complete stun.
            if (Ver4HitEffects.IsBoss(target))
            {
                if (target is Monster)
                    (target.GetComponent<HoneySlowStatus>() ?? target.gameObject.AddComponent<HoneySlowStatus>()).Apply(FreezeDuration, .8f);
                else
                {
                    var boss = target.GetComponentInParent<BossController>();
                    if (boss == null)
                    {
                        var root = target.GetComponentInParent<BossPartDamageTestRootController>();
                        if (root != null) boss = root.GetComponentInChildren<BossController>();
                    }
                    if (boss != null) boss.ApplyVer4IceChill(FreezeDuration);
                }
                return;
            }
            if(target.GetComponent<Ver4NeedleStatus>()==null)target.gameObject.AddComponent<Ver4NeedleStatus>();
            (target.GetComponent<NeuralBlockedMonsterStatus>() ?? target.gameObject.AddComponent<NeuralBlockedMonsterStatus>()).ApplyIce(FreezeDuration);
        }
    }
}
