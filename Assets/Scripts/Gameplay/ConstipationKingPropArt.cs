using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>Runtime equivalents of V3's code-drawn blob, cup, paper roll and overflow.</summary>
    public static class ConstipationKingPropArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Material floorMaterial;
        static Material paperMaterial;
        public static Material PaperMaterial
        {
            get
            {
                if (paperMaterial != null) return paperMaterial;
                var shader = Resources.Load<Shader>("ConstipationKing/PaperStrip");
                if (shader != null) paperMaterial = new Material(shader);
                return paperMaterial;
            }
        }
        public static Material FloorMaterial
        {
            get
            {
                if (floorMaterial != null) return floorMaterial;
                var shader = Resources.Load<Shader>("ConstipationKing/FloorSprite");
                if (shader != null) floorMaterial = new Material(shader);
                return floorMaterial;
            }
        }
        public static Sprite Get(string key)
        {
            if (cache.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            if (key == "PoopMinion") return Resources.Load<Sprite>("ColonMonsters/N01");
            Painter p;
            switch (key)
            {
                case "PoopProjectile": p = Blob(); break;
                case "Plunger": p = Plunger(); break;
                case "PaperRoll": p = PaperRoll(); break;
                case "OverflowPool": p = Overflow(); break;
                default: return null;
            }
            var texture = new Texture2D(p.Width, p.Height, TextureFormat.RGBA32, false)
            { name = "V3 procedural " + key, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(p.Pixels); texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, p.Width, p.Height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = key; cache[key] = sprite; return sprite;
        }

        public static ConstipationKingCarrierVisual ConfigureCarrier(GameObject root, int variant)
        {
            var visual = root.AddComponent<ConstipationKingCarrierVisual>(); visual.Initialize(variant); return visual;
        }

        static Color C(float r, float g, float b, float a = 1) => new Color(r / 255, g / 255, b / 255, a);
        static Painter Blob()
        {
            var p = new Painter(160, 160);
            var edge = new Vector2[48];
            for (int k = 0; k < edge.Length; k++)
            {
                float a = k * Mathf.PI * 2 / edge.Length, r = 66 * (1 + .055f * Mathf.Sin(a * 5) + .025f * Mathf.Cos(a * 7));
                edge[k] = new Vector2(80 + Mathf.Cos(a) * r, 80 + Mathf.Sin(a) * r);
            }
            p.Polygon(edge, C(166, 91, 43)); p.Path(edge, C(77, 41, 29), 5, true);
            p.Ellipse(80, 65, 49, 36, C(132, 67, 32));
            p.Ellipse(66, 97, 42, 25, C(201, 128, 64));
            p.Path(new[] { new Vector2(48, 99), new Vector2(69, 111), new Vector2(86, 108) }, C(240, 182, 95), 7);
            p.Ellipse(102, 46, 8, 4, C(104, 51, 28));
            return p;
        }
        static Painter Plunger()
        {
            var p = new Painter(256, 192);
            p.Line(new Vector2(20, 96), new Vector2(144, 96), C(90, 45, 34), 26);
            p.Line(new Vector2(21, 100), new Vector2(146, 100), C(205, 153, 94), 15);
            p.Line(new Vector2(29, 104), new Vector2(133, 104), C(238, 191, 124), 4);
            var cup = new[] { new Vector2(137, 56), new Vector2(169, 53), new Vector2(196, 40), new Vector2(211, 18),
                new Vector2(234, 18), new Vector2(234, 174), new Vector2(211, 174), new Vector2(196, 151), new Vector2(169, 139), new Vector2(137, 136) };
            p.Polygon(cup, C(182, 74, 60)); p.Path(cup, C(81, 43, 37), 7, true);
            p.Line(new Vector2(222, 31), new Vector2(222, 160), C(242, 145, 107), 9);
            p.Line(new Vector2(148, 125), new Vector2(189, 138), C(231, 118, 88), 7);
            p.Line(new Vector2(146, 66), new Vector2(193, 54), C(142, 47, 40), 8);
            return p;
        }
        static Painter PaperRoll()
        {
            var p = new Painter(256, 256);
            p.Ellipse(133, 59, 82, 31, C(75, 52, 69, .17f));
            p.Polygon(new[] { new Vector2(52, 69), new Vector2(52, 191), new Vector2(204, 191), new Vector2(204, 69) }, C(247, 236, 213));
            p.Line(new Vector2(53, 71), new Vector2(53, 190), C(142, 116, 91), 4);
            p.Line(new Vector2(202, 71), new Vector2(202, 190), C(142, 116, 91), 4);
            p.Line(new Vector2(190, 74), new Vector2(190, 190), C(216, 200, 173), 16);
            p.Line(new Vector2(93, 78), new Vector2(93, 190), C(255, 253, 244), 12);
            for (int side = 0; side < 2; side++)
            {
                float y = side == 0 ? 69 : 191;
                p.Ellipse(128, y, 76, 29, side == 0 ? C(229, 211, 181) : C(255, 249, 234));
                p.Ring(128, y, 76, 29, C(146, 121, 94), 3.5f);
                for (int i = 0; i < 4; i++) p.Ring(128, y, 65 - i * 11, 25 - i * 4.2f, C(201, 183, 150), 1.8f);
                p.Ellipse(128, y, 22, 10, C(108, 75, 50));
                p.Ring(128, y, 25, 12, C(184, 149, 104), 3.5f);
                p.Ellipse(125, y + 2, 14, 5, C(78, 51, 37));
            }
            return p;
        }
        static Painter Overflow()
        {
            var p = new Painter(384, 384);
            var edge = new Vector2[128];
            for (int i = 0; i < edge.Length; i++)
            {
                float a = i * Mathf.PI * 2 / edge.Length, r = 179 * (1 + .015f * Mathf.Sin(a * 9) + .011f * Mathf.Sin(a * 17));
                edge[i] = new Vector2(192 + Mathf.Cos(a) * r, 192 + Mathf.Sin(a) * r);
            }
            p.Polygon(edge, C(164, 93, 48)); p.Path(edge, C(111, 61, 37), 3, true);
            for (int j = 0; j < 5; j++) p.Ring(192, 192, 30 + j * 31, 30 + j * 31, C(212, 155, 92, .4f), 3);
            for (int k = 0; k < 32; k++)
            {
                float a = k * 2.39996f, radius = Mathf.Sqrt((k + .6f) / 32) * 169;
                float x = 192 + Mathf.Cos(a) * radius, y = 192 + Mathf.Sin(a) * radius;
                p.Ellipse(x, y, 3.7f, 2.4f, C(229, 180, 118, .75f));
                if (k % 4 == 0) p.Ring(x + 8, y + 3, 4, 3, C(109, 59, 41, .65f), 1.5f);
            }
            return p;
        }

        sealed class Painter
        {
            public readonly int Width, Height;
            public readonly Color[] Pixels;
            public Painter(int width, int height) { Width = width; Height = height; Pixels = new Color[width * height]; }
            void Put(int x, int y, Color color, float coverage = 1)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) return;
                float alpha = Mathf.Clamp01(color.a * coverage); if (alpha <= 0) return;
                int i = y * Width + x; Color old = Pixels[i]; float a = alpha + old.a * (1 - alpha);
                Pixels[i] = new Color((color.r * alpha + old.r * old.a * (1 - alpha)) / a,
                    (color.g * alpha + old.g * old.a * (1 - alpha)) / a, (color.b * alpha + old.b * old.a * (1 - alpha)) / a, a);
            }
            public void Ellipse(float cx, float cy, float rx, float ry, Color color)
            {
                for (int y = Mathf.FloorToInt(cy - ry - 1); y <= cy + ry + 1; y++)
                for (int x = Mathf.FloorToInt(cx - rx - 1); x <= cx + rx + 1; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry));
                    Put(x, y, color, Mathf.Clamp01((1 - d) * Mathf.Min(rx, ry) + .5f));
                }
            }
            public void Ring(float cx, float cy, float rx, float ry, Color color, float width)
            {
                var points = new Vector2[97];
                for (int i = 0; i < points.Length; i++) { float a = i * Mathf.PI * 2 / 96; points[i] = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry); }
                Path(points, color, width);
            }
            public void Line(Vector2 a, Vector2 b, Color color, float width)
            {
                float radius = width * .5f; Vector2 delta = b - a; float length = delta.sqrMagnitude;
                int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 1), maxX = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 1);
                int minY = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 1), maxY = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 1);
                for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
                {
                    Vector2 point = new Vector2(x, y), nearest = a + delta * (length > 0 ? Mathf.Clamp01(Vector2.Dot(point - a, delta) / length) : 0);
                    Put(x, y, color, Mathf.Clamp01(radius + .5f - Vector2.Distance(point, nearest)));
                }
            }
            public void Path(Vector2[] points, Color color, float width, bool closed = false)
            {
                for (int i = 1; i < points.Length; i++) Line(points[i - 1], points[i], color, width);
                if (closed && points.Length > 1) Line(points[points.Length - 1], points[0], color, width);
            }
            public void Polygon(Vector2[] points, Color color)
            {
                for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
                {
                    bool inside = false;
                    for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                    {
                        var a = points[i]; var b = points[j];
                        if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                    }
                    if (inside) Put(x, y, color);
                }
            }
        }
    }

    // Driven by the minion, so presentation and combat stop on the same pause frame.
    public sealed class ConstipationKingCarrierVisual : MonoBehaviour
    {
        SpriteRenderer body, food;
        Vector3 originalScale;
        float bodyHeight, clock;
        public void Initialize(int variant)
        {
            body = GetComponent<SpriteRenderer>();
            var sprite = Resources.Load<Sprite>("ColonMonsters/" + new[] { "N01", "N07", "N05" }[Mathf.Abs(variant) % 3]);
            if (sprite != null) { body.sprite = sprite; body.color = Color.white; transform.localScale = Vector3.one * 1.1f / sprite.bounds.size.x; }
            originalScale = transform.localScale; bodyHeight = body.sprite != null ? body.sprite.bounds.size.y * originalScale.y : 1;
            food = new GameObject("Carried royal tribute · visible above courier").AddComponent<SpriteRenderer>();
            food.transform.SetParent(transform, false); food.sprite = ConstipationKingVisual.Get("Tribute" + Mathf.Abs(variant) % 3); food.sortingOrder = body.sortingOrder + 1;
            if (food.sprite != null) food.transform.localScale = Vector3.one * .58f / (food.sprite.bounds.size.x * originalScale.x);
            Tick(0, Vector2.right, false, 0, transform.position);
        }
        public void Tick(float dt, Vector2 direction, bool presenting, float delivery, Vector2 mouth)
        {
            if (body == null || food == null) return;
            clock += Mathf.Max(0, dt);
            if (Mathf.Abs(direction.x) > .01f) body.flipX = direction.x > 0;
            float bow = presenting ? Mathf.Sin(Mathf.Clamp01(delivery * 2) * Mathf.PI * .5f) : 0;
            transform.localScale = new Vector3(originalScale.x * (1 + .025f * bow), originalScale.y * (1 - .08f * bow), originalScale.z);
            var carry = (Vector2)transform.position + Vector2.up * (bodyHeight + .12f + Mathf.Sin(clock * 9) * .025f * (1 - bow));
            float flight = Mathf.SmoothStep(0, 1, Mathf.Clamp01((delivery - .5f) * 2));
            food.transform.position = Vector2.Lerp(carry, mouth, flight) + Vector2.up * Mathf.Sin(flight * Mathf.PI) * .35f;
            food.color = new Color(1, 1, 1, 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((delivery - .85f) / .15f)));
            if (food.sprite != null) food.transform.localScale = Vector3.one * (.58f * (1 - .65f * flight)) / (food.sprite.bounds.size.x * transform.localScale.x);
        }
    }
}
