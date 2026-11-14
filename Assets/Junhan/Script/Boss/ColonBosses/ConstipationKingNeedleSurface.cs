using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Vampire
{
    // The approved sprites keep their full rectangular frame and remain unreadable.
    // A separately baked alpha mask locates skin without rewriting their source art.
    public static class ConstipationKingNeedleSurface
    {
        sealed class Mask { public int width, height; public byte[] bits; }
        static Dictionary<string, Mask> masks;

#if UNITY_EDITOR
        public static void ClearCache() { masks = null; }
#endif

        static void Load()
        {
            if (masks != null) return;
            masks = new Dictionary<string, Mask>();
            var data = Resources.Load<TextAsset>("ConstipationKing/NeedleSurfaces");
            if (data == null) return;
            using (var reader = new BinaryReader(new MemoryStream(data.bytes)))
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadString();
                    var mask = new Mask { width = reader.ReadInt32(), height = reader.ReadInt32() };
                    mask.bits = reader.ReadBytes(reader.ReadInt32());
                    masks.Add(name, mask);
                }
            }
        }

        static bool Opaque(Mask mask, int x, int y)
        {
            if (x < 0 || y < 0 || x >= mask.width || y >= mask.height) return false;
            int bit = y * mask.width + x;
            return (bit >> 3) < mask.bits.Length && (mask.bits[bit >> 3] & (1 << (bit & 7))) != 0;
        }

        public static bool HasMask(Sprite sprite)
        {
            Load();
            return sprite != null && masks.ContainsKey(sprite.texture.name);
        }

        public static bool IsOpaque(SpriteRenderer body, Vector2 world)
        {
            Load();
            if (body == null || body.sprite == null || !masks.TryGetValue(body.sprite.texture.name, out var mask)) return false;
            var sprite = body.sprite;
            Vector2 local = body.transform.InverseTransformPoint(world);
            if (body.flipX) local.x = -local.x;
            if (body.flipY) local.y = -local.y;
            Vector2 pixel = local * sprite.pixelsPerUnit + sprite.pivot;
            int x = Mathf.FloorToInt(pixel.x), y = Mathf.FloorToInt(pixel.y);
            return x >= 0 && y >= 0 && x < sprite.rect.width && y < sprite.rect.height
                && Opaque(mask, x + (int)sprite.rect.x, y + (int)sprite.rect.y);
        }

        public static Vector2 Surface(SpriteRenderer body, Vector2 incoming, Vector2 direction)
        {
            if (body == null || body.sprite == null) return incoming;
            Load();
            var sprite = body.sprite;
            if (!masks.TryGetValue(sprite.texture.name, out var mask))
                return VisibleBodyGeometry.Surface(body, incoming, direction);

            Rect rect = sprite.rect;
            bool VisiblePixel(int x, int y) => x >= 0 && y >= 0 && x < rect.width && y < rect.height
                && Opaque(mask, x + (int)rect.x, y + (int)rect.y);
            Vector2 local = body.transform.InverseTransformPoint(incoming);
            Vector2 axis = body.transform.InverseTransformVector(direction.normalized);
            if (body.flipX) { local.x = -local.x; axis.x = -axis.x; }
            if (body.flipY) { local.y = -local.y; axis.y = -axis.y; }
            local = local * sprite.pixelsPerUnit + sprite.pivot;
            axis.Normalize();
            float span = rect.size.magnitude;
            Vector2 begin = local - axis * span, nearest = local;
            float best = float.PositiveInfinity;
            for (float distance = 0; axis.sqrMagnitude > .0001f && distance < span * 2; distance += .5f)
            {
                Vector2 point = begin + axis * distance;
                int x = Mathf.FloorToInt(point.x), y = Mathf.FloorToInt(point.y);
                if (!VisiblePixel(x, y)) continue;
                // Pixel centres remain opaque after a flipped/rotated world transform.
                nearest = new Vector2(x + .5f, y + .5f); best = 0; break;
            }
            // A padded collision circle can meet a gap beside the staff or throne.
            // Resolve that uncommon miss against real pixels, not the sprite rectangle.
            if (float.IsPositiveInfinity(best))
                for (int y = 0; y < (int)rect.height; y += 2)
                    for (int x = 0; x < (int)rect.width; x += 2)
                    {
                        if (!VisiblePixel(x, y)) continue;
                        var point = new Vector2(x + .5f, y + .5f);
                        float distance = (point - local).sqrMagnitude;
                        if (distance < best) { best = distance; nearest = point; }
                    }
            if (float.IsPositiveInfinity(best)) return incoming;
            Vector2 result = (nearest - sprite.pivot) / sprite.pixelsPerUnit;
            if (body.flipX) result.x = -result.x;
            if (body.flipY) result.y = -result.y;
            return body.transform.TransformPoint(result);
        }
    }
}
