using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    // The player, support casters and fleeing enemies can have different destinations.
    // Keep a small LRU of static-terrain flood fields instead of rebuilding on each switch.
    public sealed class ColonNavigation : MonoBehaviour
    {
        const float Cell = 1.5f;
        const int MaxFields = 16;
        sealed class FlowField { public int[] distance; public long used; }
        FiniteStomachMap map;
        StomachMapDefinition definition;
        int w, h;
        long useSequence;
        bool[] walk;
        readonly Dictionary<int, FlowField> fields = new Dictionary<int, FlowField>();
        readonly Queue<int> queue = new Queue<int>();

        public static Vector2 Toward(Vector2 from, Vector2 target)
        {
            var m = FiniteStomachMap.Instance;
            if (m == null || !m.IsColon || !m.AppliesAt(from)) return (target - from).normalized;
            var nav = m.GetComponent<ColonNavigation>() ?? m.gameObject.AddComponent<ColonNavigation>();
            return nav.Direction(m, from, target);
        }
        Vector2 Point(int i) => map.WorldRect.min + new Vector2((i % w + .5f) * Cell, (i / w + .5f) * Cell);
        int Index(Vector2 p)
        {
            var q = (p - map.WorldRect.min) / Cell;
            return Mathf.Clamp((int)q.y, 0, h - 1) * w + Mathf.Clamp((int)q.x, 0, w - 1);
        }
        int[] Distances(int goal)
        {
            if (fields.TryGetValue(goal, out var field))
            {
                field.used = ++useSequence;
                return field.distance;
            }
            if (fields.Count >= MaxFields)
            {
                int oldest = -1; long age = long.MaxValue;
                foreach (var pair in fields)
                    if (pair.Value.used < age) { oldest = pair.Key; age = pair.Value.used; }
                // Reuse the evicted buffer to avoid repeated large managed allocations.
                field = fields[oldest]; fields.Remove(oldest);
            }
            else field = new FlowField { distance = new int[walk.Length] };
            var distance = field.distance;
            for (int i = 0; i < distance.Length; i++) distance[i] = int.MaxValue;
            queue.Clear(); distance[goal] = 0; queue.Enqueue(goal);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue(), x = at % w, y = at / w;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h || (dx == 0 && dy == 0)) continue;
                    int n = ny * w + nx;
                    if (!walk[n] || distance[n] != int.MaxValue) continue;
                    if (dx != 0 && dy != 0 && (!walk[y * w + nx] || !walk[ny * w + x])) continue;
                    distance[n] = distance[at] + 1; queue.Enqueue(n);
                }
            }
            field.used = ++useSequence; fields.Add(goal, field);
            return distance;
        }
        Vector2 Direction(FiniteStomachMap m, Vector2 from, Vector2 target)
        {
            if (map != m || definition != m.Definition || walk == null)
            {
                map = m; definition = m.Definition;
                w = Mathf.CeilToInt(m.WorldRect.width / Cell); h = Mathf.CeilToInt(m.WorldRect.height / Cell);
                walk = new bool[w * h]; fields.Clear(); queue.Clear(); useSequence = 0;
                for (int i = 0; i < walk.Length; i++) walk[i] = m.NavigationDistance(Point(i), out _) >= m.ClimbLimit + .6f;
            }
            bool clear = true; int steps = Mathf.CeilToInt(Vector2.Distance(from, target) / 1.5f);
            for (int k = 1; k < steps; k++)
                if (m.NavigationDistance(Vector2.Lerp(from, target, k / (float)steps), out _) < m.ClimbLimit + .6f) { clear = false; break; }
            if (clear) return (target - from).normalized;
            int goal = Index(m.ClampToField(target, m.ClimbLimit + 1));
            var distance = Distances(goal);
            int here = Index(from), best = here;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                int x = here % w + dx, y = here / w + dy;
                if (x < 0 || x >= w || y < 0 || y >= h) continue;
                int n = y * w + x;
                if (distance[n] < distance[best] && (dx == 0 || dy == 0 || walk[(here / w) * w + x] && walk[y * w + here % w])) best = n;
            }
            return best == here ? (target - from).normalized : (Point(best) - from).normalized;
        }
    }
}
