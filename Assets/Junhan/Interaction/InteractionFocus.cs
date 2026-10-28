using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // All E-key interactions share this registry, including field loot and mini-stage portals.
    public static class InteractionFocus
    {
        private sealed class Entry { public Component owner; public Func<bool> available; public Func<Component> visualOwner; }
        private static readonly List<Entry> entries = new List<Entry>();
        private static Component selected;
        private static Character player;
        private static Component touched;
        private static int touchedFrame=-10;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { entries.Clear(); selected = touched = null; player = null; touchedFrame=-10; }
        public static bool SelectTouched(Component owner)
        {
            var entry=entries.Find(e=>e.owner==owner);
            if(Time.timeScale<=0 || entry==null || owner==null || !owner.gameObject.activeInHierarchy || entry.available==null || !entry.available())return false;
            touched=selected=owner;touchedFrame=Time.frameCount;return true;
        }
        public static void Register(Component owner, Func<bool> available, Func<Component> visualOwner=null)
        {
            if (owner == null) return;
            var entry = entries.Find(e => e.owner == owner);
            if (entry == null) { entry = new Entry { owner = owner }; entries.Add(entry); }
            entry.available = available;
            entry.visualOwner=visualOwner;
        }
        public static bool SelectAtScreenPoint(Vector2 point,Camera camera)
        {
            if(Time.timeScale<=0 || camera==null)return false;
            Component target=null;float closest=float.PositiveInfinity;
            foreach(var entry in entries)
            {
                if(entry.owner==null||!entry.owner.gameObject.activeInHierarchy||entry.available==null||!entry.available())continue;
                var visual=entry.visualOwner!=null?entry.visualOwner():entry.owner;
                if(visual==null)continue;
                // Anatomical openings are terrain-shader landmarks, with no SpriteRenderer.
                if(visual is StomachOpening opening)
                {
                    var a=camera.WorldToScreenPoint(opening.Center-(Vector2)opening.Radii);
                    var b=camera.WorldToScreenPoint(opening.Center+(Vector2)opening.Radii);
                    var hit=Rect.MinMaxRect(Mathf.Min(a.x,b.x)-12,Mathf.Min(a.y,b.y)-12,Mathf.Max(a.x,b.x)+12,Mathf.Max(a.y,b.y)+12);
                    float distance=(hit.center-point).sqrMagnitude;
                    if(hit.Contains(point)&&distance<closest){closest=distance;target=entry.owner;}
                }
                foreach(var sprite in visual.GetComponentsInChildren<SpriteRenderer>())
                {
                    string name=sprite.name.ToLowerInvariant();
                    if(!sprite.enabled||sprite.sprite==null||name.Contains("shadow")||name.Contains("prompt")||sprite.GetComponent<SyringeAugmentVfx>()!=null)continue;
                    var bounds=PromptSpriteGeometry.WorldBounds(sprite);
                    var a=camera.WorldToScreenPoint(bounds.min);var b=camera.WorldToScreenPoint(bounds.max);
                    var hit=Rect.MinMaxRect(Mathf.Min(a.x,b.x)-12,Mathf.Min(a.y,b.y)-12,Mathf.Max(a.x,b.x)+12,Mathf.Max(a.y,b.y)+12);
                    float distance=(hit.center-point).sqrMagnitude;
                    if(hit.Contains(point)&&distance<closest){closest=distance;target=entry.owner;}
                }
            }
            return target!=null&&SelectTouched(target);
        }
        public static bool IsFocused(Component owner) => owner != null && Selected == owner;
        public static Component Selected
        {
            get
            {
                if (Time.timeScale <= 0f) return null;
                if (player == null || !player.gameObject.activeInHierarchy) player = UnityEngine.Object.FindObjectOfType<Character>();
                if (player == null || !player.IsAlive) return null;
                if(touched!=null && Time.frameCount-touchedFrame<=1)
                {
                    var target=entries.Find(e=>e.owner==touched);
                    if(target!=null && touched.gameObject.activeInHierarchy && target.available!=null && target.available())return touched;
                }
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
