using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // All E-key interactions share this registry, including field loot and mini-stage portals.
    public static class InteractionFocus
    {
        private sealed class Entry { public Component owner; public Func<bool> available; }
        private static readonly List<Entry> entries = new List<Entry>();
        private static Component selected;
        private static Character player;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { entries.Clear(); selected = null; player = null; }
        public static void Register(Component owner, Func<bool> available)
        {
            if (owner == null) return;
            var entry = entries.Find(e => e.owner == owner);
            if (entry == null) { entry = new Entry { owner = owner }; entries.Add(entry); }
            entry.available = available;
        }
        public static bool IsFocused(Component owner) => owner != null && Selected == owner;
        public static Component Selected
        {
            get
            {
                if (Time.timeScale <= 0f) return null;
                if (player == null || !player.gameObject.activeInHierarchy) player = UnityEngine.Object.FindObjectOfType<Character>();
                if (player == null || !player.IsAlive) return null;
                Component nearest = null;
                float nearestDistance = float.MaxValue, selectedDistance = float.MaxValue;
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    var entry = entries[i];
                    if (entry.owner == null) { entries.RemoveAt(i); continue; }
                    if (!entry.owner.gameObject.activeInHierarchy || entry.available == null || !entry.available()) continue;
                    float distance = Vector2.Distance(player.transform.position, entry.owner.transform.position);
                    if (entry.owner == selected) selectedDistance = distance;
                    if (distance < nearestDistance - .001f || Mathf.Abs(distance - nearestDistance) <= .001f &&
                        (nearest == null || entry.owner.GetInstanceID() < nearest.GetInstanceID()))
                    { nearest = entry.owner; nearestDistance = distance; }
                }
                // Small hysteresis prevents flicker on the boundary of overlapping ranges.
                if (selectedDistance <= nearestDistance + .08f && selectedDistance < float.MaxValue) return selected;
                selected = nearest;
                return selected;
            }
        }
        public static void ReduceTrigger(Collider2D collider)
        {
            if (collider == null || !collider.isTrigger) return;
            if (collider is CircleCollider2D circle) circle.radius *= .85f;
            else if (collider is BoxCollider2D box) box.size *= .85f;
            else if (collider is CapsuleCollider2D capsule) capsule.size *= .85f;
        }
    }
}
