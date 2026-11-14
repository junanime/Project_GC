using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Vampire
{
    // Full-rect animation cells retain a stable pivot, so their transparent padding
    // cannot be used as a hit silhouette. These masks are baked before texture import.
    public static class ColonBossNeedleSurface
    {
        sealed class Mask { public int width, height; public byte[] bits; }
        static Dictionary<string, Mask> masks;

        static void Load()
        {
            if (masks != null) return;
            masks = new Dictionary<string, Mask>();
            var data = Resources.Load<TextAsset>("ColonBosses/NeedleSurfaces");
            if (data == null) return;
            using (var reader = new BinaryReader(new MemoryStream(data.bytes)))
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    var mask = new Mask { width = reader.ReadInt32(), height = reader.ReadInt32() };
                    mask.bits = reader.ReadBytes(reader.ReadInt32());
                    masks[key] = mask;
                }
            }
        }

        static bool Opaque(Mask mask, int x, int y)
        {
            if (x < 0 || y < 0 || x >= mask.width || y >= mask.height) return false;
            int bit = y * mask.width + x;
            return (bit >> 3) < mask.bits.Length && (mask.bits[bit >> 3] & (1 << (bit & 7))) != 0;
        }

        public static Vector2 Surface(ColonBossRuntime boss, SpriteRenderer body, Vector2 incoming, Vector2 direction)
        {
            if (body == null || body.sprite == null) return incoming;
            Load();
            var sprite = body.sprite;
            string key = boss.Definition.Id + "/" + sprite.texture.name;
            if (!masks.TryGetValue(key, out var mask))
                return VisibleBodyGeometry.Surface(body, incoming, direction);

            Rect rect = sprite.rect;
            var collider = boss.GetComponent<Collider2D>();
            bool clipped = body.maskInteraction == SpriteMaskInteraction.VisibleInsideMask && collider != null;
            Bounds window = clipped ? collider.bounds : default;
            bool VisiblePixel(int x,int y)
            {
                if(!Opaque(mask,x+(int)rect.x,y+(int)rect.y))return false;
                if(!clipped)return true;
                Vector2 point=(new Vector2(x+.5f,y+.5f)-sprite.pivot)/sprite.pixelsPerUnit;
                if(body.flipX)point.x=-point.x;if(body.flipY)point.y=-point.y;
                Vector2 world=body.transform.TransformPoint(point);
                return world.x>=window.min.x&&world.x<=window.max.x&&world.y>=window.min.y&&world.y<=window.max.y;
            }
            Vector2 local = body.transform.InverseTransformPoint(incoming);
            Vector2 axis = body.transform.InverseTransformVector(direction.normalized);
            if (body.flipX) { local.x = -local.x; axis.x = -axis.x; }
            if (body.flipY) { local.y = -local.y; axis.y = -axis.y; }
            local = local * sprite.pixelsPerUnit + sprite.pivot;
            axis.Normalize();
            float span = rect.size.magnitude;
            Vector2 begin = local - axis * span, nearest = local;
            float best = float.PositiveInfinity;
            for (float distance = 0; distance < span * 2 && axis.sqrMagnitude > .0001f; distance += .5f)
            {
                Vector2 point = begin + axis * distance;
                int x = Mathf.FloorToInt(point.x), y = Mathf.FloorToInt(point.y);
                if (x < 0 || y < 0 || x >= rect.width || y >= rect.height || !VisiblePixel(x,y)) continue;
                nearest = point; best = 0; break;
            }
            // Collider padding can meet a gap between limbs. Find nearby visible skin
            // instead of embedding the needle in the empty atlas cell.
            if (float.IsPositiveInfinity(best))
                for (int y = 0; y < (int)rect.height; y += 2)
                    for (int x = 0; x < (int)rect.width; x += 2)
                    {
                        if (!VisiblePixel(x,y)) continue;
                        var point = new Vector2(x + .5f, y + .5f);
                        float distance = (point - local).sqrMagnitude;
                        if (distance < best) { best = distance; nearest = point; }
                    }
            Vector2 result = (nearest - sprite.pivot) / sprite.pixelsPerUnit;
            if (body.flipX) result.x = -result.x;
            if (body.flipY) result.y = -result.y;
            return body.transform.TransformPoint(result);
        }
    }
}
