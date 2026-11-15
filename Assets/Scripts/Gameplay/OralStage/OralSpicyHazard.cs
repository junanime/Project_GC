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
        public const int TargetCount = 10;

        public static bool IsHot(float age) => age >= OralTasteEvents.SpicyWarning &&
            age < OralTasteEvents.SpicyWarning + OralTasteEvents.SpicySeconds;

        public static bool Contains(IReadOnlyList<Vector2> centers, Vector2 position)
        {
            for (int i = 0; i < centers.Count; i++)
                if ((position - centers[i]).sqrMagnitude <= Radius * Radius) return true;
            return false;
        }

        // Packed on the actual tongue surface, in world-rect-relative offsets. A center-
        // pinned greedy layout blocks the last two pockets even when ten discs fit.
        // Three arrangements and their mirrors are safe seeds, then continuously jittered.
        // Each was packed with 1.50 between rims and 1.05 at the edge; runtime checks
        // still enforce the shared Passage / EdgePassage against the current map.
        static readonly Vector2[][] PackedLayouts = {
            new[] {
                new Vector2(0.10588628f, 0.08407173f),
                new Vector2(-0.14719348f, 0.01994497f),
                new Vector2(0.00369636f, 0.12149071f),
                new Vector2(0.03878388f, 0.00427514f),
                new Vector2(-0.00192841f, -0.10872014f),
                new Vector2(-0.14948686f, -0.10866522f),
                new Vector2(-0.08090870f, 0.10134905f),
                new Vector2(0.14952609f, -0.02647652f),
                new Vector2(0.08371609f, -0.10867891f),
                new Vector2(-0.06709274f, -0.02543698f)
            },
            new[] {
                new Vector2(-0.14953202f, -0.02759067f),
                new Vector2(-0.04016976f, 0.00248824f),
                new Vector2(-0.00858705f, 0.12187915f),
                new Vector2(-0.10655944f, 0.08352756f),
                new Vector2(0.07621967f, 0.10384837f),
                new Vector2(-0.08312592f, -0.10868539f),
                new Vector2(0.06665099f, -0.02379714f),
                new Vector2(0.13704184f, -0.10043524f),
                new Vector2(0.00251490f, -0.10877126f),
                new Vector2(0.14511912f, 0.02749149f)
            },
            new[] {
                new Vector2(0.06394661f, -0.10871302f),
                new Vector2(0.14953167f, -0.10471735f),
                new Vector2(-0.01576276f, 0.12131422f),
                new Vector2(0.02349560f, 0.00444172f),
                new Vector2(-0.14459384f, -0.10741042f),
                new Vector2(0.07865063f, 0.10261369f),
                new Vector2(-0.06208804f, 0.00463884f),
                new Vector2(0.14622372f, 0.02367491f),
                new Vector2(-0.14696476f, 0.02102936f),
                new Vector2(-0.02171396f, -0.10867433f)
            }
        };

        static bool Fits(Vector2 point, Rect bounds, Func<Vector2, float> clearance, float extraClearance = 0)
        {
            if (!bounds.Contains(point) || clearance(point) < Radius + EdgePassage + extraClearance) return false;
            // Validate the whole visible rim, including the curved northern wall.
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2 / Segments;
                var rim = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;
                if (clearance(rim) < EdgePassage + extraClearance) return false;
            }
            return true;
        }

        static float NextUnit(ref uint state)
        {
            // Local stream: layout retries never consume gameplay's global random state.
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state & 0x00ffffffu) / 16777216f;
        }

        static void VaryPositions(List<Vector2> centers, Rect bounds, Func<Vector2, float> clearance, float variation)
        {
            // Use the full continuous event input, not only its six-layout bucket.
            uint state = BitConverter.ToUInt32(BitConverter.GetBytes(variation), 0) ^ 0x9e3779b9u;
            if (state == 0) state = 0x6d2b79f5u;
            const int sweeps = 21;
            float separation = Radius * 2 + Passage + .02f;
            float minimumSquared = separation * separation;
            for (int sweep = 0; sweep < sweeps; sweep++)
            {
                int first = Mathf.FloorToInt(NextUnit(ref state) * centers.Count);
                float reach = Mathf.Lerp(.55f, .12f, Mathf.Max(0, sweep - 1) / (float)(sweeps - 2));
                for (int offset = 0; offset < centers.Count; offset++)
                {
                    int index = (first + offset) % centers.Count;
                    float angle = NextUnit(ref state) * Mathf.PI * 2;
                    // First pass fits inside every authored seed's extra safety margin,
                    // so even tight outer circles receive their own continuous offset.
                    float distance = sweep == 0 ? .018f + NextUnit(ref state) * .01f : Mathf.Sqrt(NextUnit(ref state)) * reach;
                    var point = centers[index] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    bool separated = true;
                    for (int other = 0; other < centers.Count; other++)
                        if (other != index && (point - centers[other]).sqrMagnitude < minimumSquared)
                        { separated = false; break; }
                    // Every accepted move is safe immediately; there is no temporarily
                    // overlapping relaxation state, retries or unbounded solver loop.
                    if (separated && Fits(point, bounds, clearance, .01f)) centers[index] = point;
                }
            }
        }

        public static List<Vector2> Layout(Vector2 origin, Rect bounds, Func<Vector2, float> clearance, float variation)
        {
            var result = new List<Vector2>(TargetCount);
            int variants = PackedLayouts.Length * 2;
            int first = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(-12f, 12f, variation) * variants), 0, variants - 1);
            float separationSquared = (Radius * 2 + Passage) * (Radius * 2 + Passage);
            for (int attempt = 0; attempt < variants; attempt++)
            {
                int variant = (first + attempt) % variants;
                float mirror = variant >= PackedLayouts.Length ? -1f : 1f;
                result.Clear();
                foreach (var offset in PackedLayouts[variant % PackedLayouts.Length])
                {
                    var point = origin + Vector2.Scale(new Vector2(offset.x * mirror, offset.y), bounds.size);
                    bool fits = Fits(point, bounds, clearance);
                    foreach (var placed in result)
                        if ((point - placed).sqrMagnitude < separationSquared) { fits = false; break; }
                    if (!fits) break;
                    result.Add(point);
                }
                if (result.Count == TargetCount)
                {
                    VaryPositions(result, bounds, clearance, variation);
                    return result;
                }
            }
            // Future map-size/geometry edits must never force ten unsafe footprints.
            // If no packed arrangement fits, retain the conservative sampled fallback.
            result.Clear();
            var candidates = new List<Vector2>();
            float pitch = Radius * .35f;
            int reach = Mathf.CeilToInt(bounds.size.magnitude / pitch) + 1;
            for (int row = -reach; row <= reach; row++)
            for (int column = -reach; column <= reach; column++)
            {
                var offset = new Vector2((column + ((row & 1) == 0 ? 0 : .5f)) * pitch,
                    row * pitch * .8660254f);
                Vector2 point = origin + OralMapGeometry.Rotate(offset, variation);
                if (Fits(point, bounds, clearance)) candidates.Add(point);
            }
            // A coarse large-disc grid leaves the shallow southern tongue almost empty.
            // Instead fill the largest remaining gaps from a fine candidate grid. Accepted
            // discs never move or overlap, so the warning footprint stays authoritative.
            if (candidates.Count == 0) return result;
            candidates.Sort((a,b)=>(a-origin).sqrMagnitude.CompareTo((b-origin).sqrMagnitude));
            result.Add(candidates[0]);candidates.RemoveAt(0);
            while (candidates.Count > 0 && result.Count < TargetCount)
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
