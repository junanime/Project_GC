using System.Collections.Generic;
using UnityEngine;
using Parent = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;

namespace Vampire
{
    // Queued by a real card purchase only. An old projectile cannot spend a cue for
    // a rank it did not carry at launch. Restores and free starting originals stay quiet.
    internal sealed class OriginalImpactFeedback
    {
        struct Entry
        {
            public Parent parent;
            public int option;
            public int rank;
        }

        readonly List<Entry> pending = new List<Entry>();
        int lastCueFrame = -1;

        internal bool Pending => pending.Count > 0;
        internal void Arm(Parent parent, int option, int rank)
        {
            for (int i = 0; i < pending.Count; i++)
                if (pending[i].parent == parent && pending[i].option == option)
                {
                    pending[i] = new Entry { parent = parent, option = option, rank = rank };
                    return;
                }
            pending.Add(new Entry { parent = parent, option = option, rank = rank });
        }

        internal bool TryConsume(Ver4CombatSnapshot snapshot, Parent proc, int option)
        {
            if (pending.Count == 0 || lastCueFrame == Time.frameCount) return false;
            for (int i = 0; i < pending.Count; i++)
            {
                var entry = pending[i];
                if (entry.parent != proc || (option >= 0 && option != entry.option)) continue;
                if (snapshot.Count(entry.parent, entry.option) < entry.rank) continue;
                pending.RemoveAt(i);
                lastCueFrame = Time.frameCount;
                return true;
            }
            return false;
        }

        internal bool OnImpact(Ver4CombatSnapshot snapshot, Vector3 point, Component target, Parent proc, int option)
        {
            if (!TryConsume(snapshot, proc, option)) return false;
            var spark = SyringeAugmentVfx.Play("CursorControlHit", point, SyringeAugmentVfx.FindTarget(target));
            if (spark != null)
            {
                spark.SetWorldSize(Vector2.one * .56f, Vector2.one);
                spark.SetStrength(.78f);
            }
            return true;
        }
    }
}
