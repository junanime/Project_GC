using System;
using System.Collections.Generic;

namespace Vampire
{
    /// <summary>
    /// Parent-local progression only. It never enables an ability, changes a
    /// projectile, or rolls a preview. Gameplay effect application stays in the
    /// acquisition handler; save restore replaces counts rather than stacking.
    /// </summary>
    public sealed class OriginalAugmentProgress
    {
        public const int EffectsPerParent = 3;
        public const int MaxEffectSelections = 3;
        public const int MaxParentLevel = EffectsPerParent * MaxEffectSelections;

        [Serializable]
        public sealed class ParentSnapshot
        {
            public string parentId;
            public int[] selections;
        }

        private readonly Dictionary<string, int[]> counts = new Dictionary<string, int[]>(StringComparer.Ordinal);

        // Stable parent names, not serialized enum positions, identify progress.
        public bool RegisterOwnedParent(string parentId)
        {
            if (string.IsNullOrWhiteSpace(parentId)) throw new ArgumentException("Missing parent ID.", nameof(parentId));
            if (counts.ContainsKey(parentId)) return false;
            counts.Add(parentId, new int[EffectsPerParent]);
            return true;
        }

        public bool IsOwned(string parentId) => parentId != null && counts.ContainsKey(parentId);

        public int Count(string parentId, int effectIndex)
        {
            ValidateIndex(effectIndex);
            return parentId != null && counts.TryGetValue(parentId, out var values) ? values[effectIndex] : 0;
        }

        public int Level(string parentId)
        {
            if (parentId == null || !counts.TryGetValue(parentId, out var values)) return 0;
            return values[0] + values[1] + values[2];
        }

        public bool CanSelect(string parentId, int effectIndex)
        {
            ValidateIndex(effectIndex);
            return IsOwned(parentId) && Count(parentId, effectIndex) < MaxEffectSelections;
        }

        public bool TrySelect(string parentId, int effectIndex)
        {
            if (!CanSelect(parentId, effectIndex)) return false;
            counts[parentId][effectIndex]++;
            return true;
        }

        public AugmentUpgradeGrade ResolveNumericGrade(string parentId, AugmentUpgradeGrade rolledGrade)
        {
            if (!IsOwned(parentId)) throw new ArgumentException("Parent is not owned.", nameof(parentId));
            if (!AugmentUpgradeOdds.IsNumeric(rolledGrade)) throw new ArgumentException("Not a numeric grade.", nameof(rolledGrade));
            return Level(parentId) == MaxParentLevel ? AugmentUpgradeGrade.Supreme : rolledGrade;
        }

        public List<ParentSnapshot> Capture()
        {
            var result = new List<ParentSnapshot>();
            var parentIds = new List<string>(counts.Keys);
            parentIds.Sort(StringComparer.Ordinal);
            foreach (var id in parentIds)
                result.Add(new ParentSnapshot { parentId = id, selections = (int[])counts[id].Clone() });
            return result;
        }

        // Validate the whole snapshot first. Invalid/duplicate/unknown parents
        // cannot partially overwrite a live run or accidentally grant ownership.
        public bool TryRestore(IReadOnlyList<ParentSnapshot> snapshots, Func<string, bool> isOwnedParent)
        {
            if (snapshots == null || isOwnedParent == null) return false;
            var restored = new Dictionary<string, int[]>(StringComparer.Ordinal);
            foreach (var item in snapshots)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.parentId) || restored.ContainsKey(item.parentId)
                    || !isOwnedParent(item.parentId) || item.selections == null || item.selections.Length != EffectsPerParent)
                    return false;
                foreach (int count in item.selections)
                    if (count < 0 || count > MaxEffectSelections) return false;
                restored.Add(item.parentId, (int[])item.selections.Clone());
            }
            counts.Clear();
            foreach (var item in restored) counts.Add(item.Key, item.Value);
            return true;
        }

        private static void ValidateIndex(int index)
        {
            if (index < 0 || index >= EffectsPerParent) throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
