using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Baked alpha bounds omit transparent atlas padding without enabling texture Read/Write in builds.
    public sealed class VisibleBodyGeometry : ScriptableObject
    {
        [Serializable] public struct Entry { public Sprite sprite; public Rect bounds; public Vector2[] outline; }
        public Entry[] entries;
        static Dictionary<Sprite, Entry> cache;
        public static Rect Bounds(Sprite sprite)
        {
            if (cache == null)
            {
                cache = new Dictionary<Sprite, Entry>();
                var data = Resources.Load<VisibleBodyGeometry>("VisibleBodyGeometry");
                if (data != null && data.entries != null)
                    foreach (var e in data.entries) if (e.sprite != null) cache[e.sprite] = e;
            }
            if (cache.TryGetValue(sprite, out var entry)) return entry.bounds;
            var b = sprite.bounds;
            return new Rect(b.min, b.size);
        }
        static Vector2 World(SpriteRenderer body,Vector2 p)
        {if(body.flipX)p.x=-p.x;if(body.flipY)p.y=-p.y;return body.transform.TransformPoint(p);}
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        public static Vector2 Surface(SpriteRenderer body, Vector2 incoming, Vector2 direction)
        {
            Bounds(body.sprite); // Ensure baked cache is available.
            var points=cache.TryGetValue(body.sprite,out var entry)&&entry.outline!=null&&entry.outline.Length>=3?entry.outline:body.sprite.vertices;
            if(points.Length<3)return body.bounds.ClosestPoint(incoming);
            Vector2 from=incoming-direction*(body.bounds.size.magnitude+1);
            float best=float.PositiveInfinity,nearest=float.PositiveInfinity;Vector2 hit=incoming,near=incoming;
            for(int i=0;i<points.Length;i++)
            {
                Vector2 a=World(body,points[i]),b=World(body,points[(i+1)%points.Length]),edge=b-a;
                float d=Cross(direction,edge);
                if(Mathf.Abs(d)>.00001f)
                {
                    float t=Cross(a-from,edge)/d,u=Cross(a-from,direction)/d;
                    if(t>=0&&u>=0&&u<=1&&t<best){best=t;hit=from+direction*t;}
                }
                var p=a+edge*Mathf.Clamp01(Vector2.Dot(incoming-a,edge)/Mathf.Max(.00001f,edge.sqrMagnitude));
                float distance=(incoming-p).sqrMagnitude;if(distance<nearest){nearest=distance;near=p;}
            }
            return float.IsPositiveInfinity(best)?near:hit;
        }
        public static Bounds InSpace(SpriteRenderer body, Transform space)
        {
            var r = Bounds(body.sprite);
            Bounds result = default;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3((i & 1) == 0 ? r.xMin : r.xMax, (i & 2) == 0 ? r.yMin : r.yMax);
                if (body.flipX) p.x = -p.x;
                if (body.flipY) p.y = -p.y;
                p = space.InverseTransformPoint(body.transform.TransformPoint(p));
                if (i == 0) result = new Bounds(p, Vector3.zero); else result.Encapsulate(p);
            }
            return result;
        }
        public static void Fit(BoxCollider2D box, params SpriteRenderer[] bodies)
        {
            bool found = false; Bounds bounds = default;
            foreach (var body in bodies)
            {
                if (body == null || !body.enabled || body.sprite == null) continue;
                var b = InSpace(body, box.transform);
                if (!found) { bounds = b; found = true; } else bounds.Encapsulate(b);
            }
            if (!found) return;
            box.offset = bounds.center;
            box.size = new Vector2(Mathf.Max(.01f, bounds.size.x), Mathf.Max(.01f, bounds.size.y));
        }
    }

    // Runs after animation; does not enable colliders, alter feet or resize any art.
    [DefaultExecutionOrder(500)]
    public sealed class MonsterBodyFit : MonoBehaviour
    {
        BoxCollider2D box; SpriteRenderer[] bodies;
        public void Configure(BoxCollider2D target, params SpriteRenderer[] renderers) { box = target; bodies = renderers; Refresh(); }
        public void Refresh() { if (box != null && bodies != null) VisibleBodyGeometry.Fit(box, bodies); }
        void LateUpdate() { Refresh(); }
    }
}
