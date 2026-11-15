using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>One footprint shared by the warning, hot floor and contact test.</summary>
    public static class OralSpicyHazard
    {
        // ColonFieldEvents.DryRadius = 1.4: the dry strip footprint is 2.8 wide.
        // The oral circle's diameter is twice that width (5.6 world units).
        public const float Radius = 2.8f;
        public const float Passage = 1.4f;
        public const float EdgePassage = 1f;
        public const float FadeSeconds = .75f;
        public const float BurnSeconds = 3f;
        public const float BurnDamage = 3f;
        public const int Segments = 96;

        public static bool IsHot(float age) => age >= OralTasteEvents.SpicyWarning &&
            age < OralTasteEvents.SpicyWarning + OralTasteEvents.SpicySeconds;

        public static bool Contains(IReadOnlyList<Vector2> centers, Vector2 position)
        {
            for (int i = 0; i < centers.Count; i++)
                if ((position - centers[i]).sqrMagnitude <= Radius * Radius) return true;
            return false;
        }

        public static List<Vector2> Layout(Vector2 origin, Rect bounds, Func<Vector2, float> clearance, float rotation)
        {
            var result = new List<Vector2>();var candidates = new List<Vector2>();
            float pitch = Radius * .35f;
            int reach = Mathf.CeilToInt(bounds.size.magnitude / pitch) + 1;
            for (int row = -reach; row <= reach; row++)
            for (int column = -reach; column <= reach; column++)
            {
                var offset = new Vector2((column + ((row & 1) == 0 ? 0 : .5f)) * pitch,
                    row * pitch * .8660254f);
                Vector2 point = origin + OralMapGeometry.Rotate(offset, rotation);
                if (!bounds.Contains(point) || clearance(point) < Radius + EdgePassage) continue;
                bool fits = true;
                // Sample the entire rim as well: a curved wall's approximate distance alone
                // must never put a visible part of the circle on gums or a tooth sidewall.
                for (int i = 0; i < Segments && fits; i++)
                {
                    float angle = i * Mathf.PI * 2 / Segments;
                    var rim = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;
                    fits = clearance(rim) >= EdgePassage;
                }
                if (fits) candidates.Add(point);
            }
            // A coarse large-disc grid leaves the shallow southern tongue almost empty.
            // Instead fill the largest remaining gaps from a fine candidate grid. Accepted
            // discs never move or overlap, so the warning footprint stays authoritative.
            if (candidates.Count == 0) return result;
            candidates.Sort((a,b)=>(a-origin).sqrMagnitude.CompareTo((b-origin).sqrMagnitude));
            result.Add(candidates[0]);candidates.RemoveAt(0);
            while (candidates.Count > 0)
            {
                int best=-1;float bestDistance=0;
                for(int i=0;i<candidates.Count;i++)
                {
                    float nearest=float.PositiveInfinity;
                    foreach(var placed in result)nearest=Mathf.Min(nearest,(placed-candidates[i]).sqrMagnitude);
                    if(nearest>bestDistance){best=i;bestDistance=nearest;}
                }
                if(best<0||bestDistance<(Radius*2+Passage)*(Radius*2+Passage))break;
                result.Add(candidates[best]);candidates.RemoveAt(best);
            }
            return result;
        }
    }

    /// <summary>A single burn clock per player; overlapping contacts cannot add ticks.</summary>
    public sealed class OralSpicyBurnClock
    {
        public float Remaining { get; private set; }
        float untilTick;
        public void Expose() => Remaining = OralSpicyHazard.BurnSeconds;
        public bool Tick(float dt)
        {
            if (dt <= 0 || Remaining <= 0) return false;
            float elapsed = Mathf.Min(dt, Remaining);
            Remaining = Mathf.Max(0, Remaining - dt);
            untilTick -= elapsed;
            if (untilTick > 0) return false;
            // Do not catch up with several damage calls after a long frame, or retry a
            // tick blocked by the player's ordinary invincibility / shield rules.
            untilTick = 1f;
            return true;
        }
        public void Reset() { Remaining = 0; untilTick = 0; }
    }
}
