using System;
using System.Collections.Generic;
using UnityEngine;
using P = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;

namespace Vampire
{
    [Flags]
    public enum CombatStatusTag
    {
        None = 0, Slow = 1, Chill = 2, Freeze = 4, Burn = 8, Poison = 16,
        Corrosion = 32, Mark = 64, Infection = 128, DeathMark = 256,
        Seed = 512, Stun = 1024, Vulnerability = 2048
    }

    // Implementations return only live effects. The presence of a pooled component is insufficient.
    public interface ICombatStatus { CombatStatusTag ActiveStatusTags { get; } }

    public static class CombatStatusRules
    {
        static readonly List<MonoBehaviour> providers = new List<MonoBehaviour>(24);

        // Capability metadata, including retired needles and upgrade-only marks.
        // Owning one of these needles does not itself make a target debuffed.
        public static CombatStatusTag NeedleTags(P type)
        {
            switch (type)
            {
                case P.IceNeedle: return CombatStatusTag.Chill | CombatStatusTag.Slow | CombatStatusTag.Freeze;
                case P.FireNeedle: return CombatStatusTag.Burn;
                case P.Poison: return CombatStatusTag.Poison;
                case P.Honey: return CombatStatusTag.Slow;
                case P.CorrosionNeedle: return CombatStatusTag.Corrosion;
                case P.MarkNeedle: case P.BipolarNeedle: return CombatStatusTag.Mark;
                case P.GutBacteriaNeedle: return CombatStatusTag.Infection;
                case P.DigestiveAcidSacNeedle: return CombatStatusTag.DeathMark;
                case P.WoodNeedle: return CombatStatusTag.Seed;
                default: return CombatStatusTag.None;
            }
        }

        public static CombatStatusTag ActiveTags(Component target)
        {
            if (target == null || !target.gameObject.activeInHierarchy || target is Character)
                return CombatStatusTag.None;
            CombatStatusTag result = CombatStatusTag.None;
            // Boss parts may keep their debuffs on a parent. Queries do not allocate per hit.
            for (var current = target.transform; current != null; current = current.parent)
            {
                current.GetComponents(providers);
                foreach (var provider in providers)
                    if (provider != null && provider.isActiveAndEnabled && provider is ICombatStatus status)
                        result |= status.ActiveStatusTags;
            }
            providers.Clear();
            return result;
        }

        public static float DamageMultiplier(Character source, CombatStatusTag tags)
            => tags != CombatStatusTag.None && source != null && source.Skills != null ? 1 + source.Skills.StatusDamageBonus : 1;

        public static float DamageMultiplier(Character source, Component target)
        {
            if (source == null || source.Skills == null || !source.Skills.IsHyuki)
                return 1;
            return DamageMultiplier(source, ActiveTags(target));
        }
    }
}
