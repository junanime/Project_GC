using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>Independent UFO core pools; the existing boss owns timing and execution.</summary>
    public sealed class BossFiveCoreSkillController : MonoBehaviour
    {
        [Serializable]
        public sealed class CombinationSlot
        {
            public string label;
            public BossPartDamageTestPart primary;
            public BossPartDamageTestPart secondary;
            [Tooltip("Leave empty until the combination Pattern script is implemented.")]
            public BossPatternBase pattern;
        }

        [SerializeField] private CombinationSlot[] combinationSlots = new CombinationSlot[0];
        public IReadOnlyList<CombinationSlot> CombinationSlots => combinationSlots;

        private BossPartDamageTestPart RequiredSecondary(BossPatternBase pattern)
        {
            foreach (var slot in combinationSlots)
                if (slot != null && slot.pattern == pattern && slot.primary == pattern.OwnerPart)
                    return slot.secondary;
            return pattern.CombinationCore;
        }
        [Tooltip("The UFO health root with five distinct registered cores.")]
        [SerializeField] private BossPartDamageTestRootController healthRoot;
        [Tooltip("Owner of the UFO basic attack. Its destruction stops further basic shots.")]
        [SerializeField] private BossPartDamageTestPart basicAttackOwner;
        [Tooltip("Currently attacking primary core. Runtime only; does not control damage eligibility.")]
        [SerializeField] private BossPartDamageTestPart activePrimary;
        [Tooltip("Second active core in phase 2 and later. Runtime only.")]
        [SerializeField] private BossPartDamageTestPart activeSecondary;

        public bool IsConfigured => isActiveAndEnabled && healthRoot != null && healthRoot.UsesFiveCoreHealth;
        public BossPartDamageTestPart ActivePrimary => activePrimary;
        public BossPartDamageTestPart ActiveSecondary => activeSecondary;
        public bool CanBasicAttack => IsAlive(basicAttackOwner);
        public bool IsAlive(BossPartDamageTestPart core) => IsConfigured &&
            healthRoot.CanReceivePartDamage(core) && core != null && !core.IsBroken && core.isActiveAndEnabled;

        public bool CanSelect(BossPatternBase pattern, int phase)
        {
            if (pattern == null || !pattern.isActiveAndEnabled || !IsAlive(pattern.OwnerPart)) return false;
            foreach (var slot in combinationSlots)
                if (slot != null && slot.pattern == pattern && (slot.primary != pattern.OwnerPart ||
                    slot.secondary == null || slot.secondary == slot.primary ||
                    (pattern.CombinationCore != null && pattern.CombinationCore != slot.secondary))) return false;
            var secondary = RequiredSecondary(pattern);
            if (secondary != null && (phase < 2 ||
                secondary == pattern.OwnerPart || !IsAlive(secondary))) return false;
            return pattern.CanUse();
        }

        public BossPatternBase SelectPattern(int phase)
        {
            var available = new List<List<BossPatternBase>>();
            var seen = new HashSet<BossPartDamageTestPart>();
            if (IsConfigured)
                foreach (var core in healthRoot.FiveCores)
                {
                    if (!IsAlive(core) || !seen.Add(core)) continue;
                    var skills = new List<BossPatternBase>();
                    foreach (var skill in core.SkillPool ?? Array.Empty<BossPatternBase>())
                        if (skill != null && skill.OwnerPart == core && CanSelect(skill, phase) && !skills.Contains(skill))
                            skills.Add(skill);
                    foreach (var slot in combinationSlots)
                        if (slot != null && slot.primary == core && slot.secondary != null &&
                            slot.pattern != null && slot.pattern.OwnerPart == core &&
                            CanSelect(slot.pattern, phase) && !skills.Contains(slot.pattern)) skills.Add(slot.pattern);
                    if (skills.Count > 0) available.Add(skills);
                }
            if (available.Count == 0) return null;
            var selectedPool = available[UnityEngine.Random.Range(0, available.Count)];
            return selectedPool[UnityEngine.Random.Range(0, selectedPool.Count)];
        }

        public bool BeginPattern(BossPatternBase pattern, int phase)
        {
            EndPattern();
            if (!CanSelect(pattern, phase)) return false;
            activePrimary = pattern.OwnerPart;
            if (phase >= 2)
            {
                activeSecondary = RequiredSecondary(pattern);
                if (activeSecondary == null)
                {
                    var others = new List<BossPartDamageTestPart>();
                    foreach (var core in healthRoot.FiveCores)
                        if (core != activePrimary && IsAlive(core)) others.Add(core);
                    if (others.Count > 0) activeSecondary = others[UnityEngine.Random.Range(0, others.Count)];
                }
            }
            return true;
        }

        public bool CanContinue(BossPatternBase pattern) => pattern != null && pattern.isActiveAndEnabled &&
            IsAlive(activePrimary) && activePrimary == pattern.OwnerPart &&
            (activeSecondary == null || IsAlive(activeSecondary)) &&
            (RequiredSecondary(pattern) == null || activeSecondary == RequiredSecondary(pattern));

        public void EndPattern() { activePrimary = null; activeSecondary = null; }
        private void OnDisable() { EndPattern(); }
    }
}
