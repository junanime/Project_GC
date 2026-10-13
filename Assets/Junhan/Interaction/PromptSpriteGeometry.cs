using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Alpha bounds baked in the Editor; no texture Read/Write or GPU readbacks in a run.
    public sealed class PromptSpriteGeometry : ScriptableObject
    {
        // Names/paths deliberately avoid asset references: loading bounds must not
        // pull every interaction texture (including other stages) into memory.
        [Serializable] public struct Entry { public string key, assetPath, spriteName; public Rect bounds; }
        public Entry[] entries;
        static Dictionary<Sprite, Rect> cache;
        static Dictionary<string, Rect> baked;
        public static string Key(Sprite sprite) => sprite.texture.name + "|" + sprite.name;
        public static Rect Bounds(Sprite sprite)
        {
            if (cache == null)
            {
                cache = new Dictionary<Sprite, Rect>();
                baked = new Dictionary<string, Rect>();
                var data = Resources.Load<PromptSpriteGeometry>("Interaction/PromptGeometry");
                if (data != null && data.entries != null)
                    foreach (var e in data.entries) if (!string.IsNullOrEmpty(e.key)) baked[e.key] = e.bounds;
            }
            if (cache.TryGetValue(sprite, out var rect)) return rect;
            if (!baked.TryGetValue(Key(sprite), out rect)) rect = VisibleBodyGeometry.Bounds(sprite);
            cache[sprite] = rect;
            return rect;
        }
        public static void RegisterScaledCopy(Sprite source, Sprite copy)
        {
            Rect rect = Bounds(source);
            float scale = source.pixelsPerUnit / copy.pixelsPerUnit;
            cache[copy] = new Rect(rect.position * scale, rect.size * scale);
        }
        public static Bounds WorldBounds(SpriteRenderer body)
        {
            Rect rect = Bounds(body.sprite);
            Bounds result = default;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3((i & 1) == 0 ? rect.xMin : rect.xMax, (i & 2) == 0 ? rect.yMin : rect.yMax);
                if (body.flipX) p.x = -p.x;
                if (body.flipY) p.y = -p.y;
                p = body.transform.TransformPoint(p);
                if (i == 0) result = new Bounds(p, Vector3.zero); else result.Encapsulate(p);
            }
            return result;
        }
    }
}
