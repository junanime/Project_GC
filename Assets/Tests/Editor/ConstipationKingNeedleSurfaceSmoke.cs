#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static class ConstipationKingNeedleSurfaceSmoke
    {
        // The oracle reads original PNG alpha, independently of the runtime mask.
        public static void Validate(Action<bool, string> check)
        {
            var host = new GameObject("Isolated king needle alpha verification");
            var body = host.AddComponent<SpriteRenderer>();
            try
            {
                for (int frame = -1; frame <= 10; frame++)
                {
                    string name = frame < 0 ? "Body" : "Pose" + frame;
                    body.sprite = ConstipationKingVisual.Get(name);
                    check(body.sprite != null && ConstipationKingNeedleSurface.HasMask(body.sprite), "needle mask covers approved sprite: " + name);
                    check(!body.sprite.texture.isReadable, "needle alpha does not require readable runtime texture: " + name);
                    var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        source.LoadImage(File.ReadAllBytes("Assets/Resources/ConstipationKing/" + name + ".png"));
                        var alpha = source.GetPixels32();
                        var sprite = body.sprite; var rect = sprite.rect;
                        bool OriginalPixel(int x, int y) => x >= 0 && y >= 0 && x < rect.width && y < rect.height
                            && alpha[(y + (int)rect.y) * source.width + x + (int)rect.x].a >= 128;
                        int row = (int)(rect.height * .55f), first = -1, last = -1;
                        for (int x = 0; x < rect.width; x++) if (OriginalPixel(x, row)) { if (first < 0) first = x; last = x; }
                        check(first >= 0, "independent source row crosses real body: " + name);
                        for (int facing = 0; facing < 4; facing++)
                        {
                            body.flipX = (facing & 1) != 0; body.flipY = (facing & 2) != 0;
                            host.transform.position = new Vector3(23, -17, 0);
                            host.transform.localScale = new Vector3(1.7f, .83f, 1);
                            host.transform.rotation = Quaternion.Euler(0, 0, 19);
                            Vector2 World(Vector2 pixel)
                            {
                                Vector2 local = (pixel - sprite.pivot) / sprite.pixelsPerUnit;
                                if (body.flipX) local.x = -local.x;
                                if (body.flipY) local.y = -local.y;
                                return host.transform.TransformPoint(local);
                            }
                            Vector2 Pixel(Vector2 world)
                            {
                                Vector2 local = host.transform.InverseTransformPoint(world);
                                if (body.flipX) local.x = -local.x;
                                if (body.flipY) local.y = -local.y;
                                return local * sprite.pixelsPerUnit + sprite.pivot;
                            }
                            for (int side = 0; side < 2; side++)
                            {
                                var start = new Vector2(side == 0 ? -20 : rect.width + 20, row + .5f);
                                var incoming = World(start);
                                Vector2 travel = World(start + Vector2.right * (side == 0 ? 1 : -1)) - incoming;
                                var hit = ConstipationKingNeedleSurface.Surface(body, incoming, travel);
                                var pixel = Pixel(hit);
                                check(OriginalPixel(Mathf.FloorToInt(pixel.x), Mathf.FloorToInt(pixel.y)), "needle lands on original alpha: " + name + " flip=" + facing + " side=" + side);
                                check(Mathf.Abs(pixel.x - ((side == 0 ? first : last) + .5f)) < .1f,
                                    "needle uses first incoming silhouette, not far side: " + name + " flip=" + facing + " side=" + side);
                            }
                            // The entire incoming ray is below this cropped sprite. Its
                            // fallback must still choose a pixel inside the actual art.
                            var gap = new Vector2(rect.width * .5f, -12);
                            var gapWorld = World(gap);
                            var fallback = ConstipationKingNeedleSurface.Surface(body, gapWorld, World(gap + Vector2.right) - gapWorld);
                            var fallbackPixel = Pixel(fallback);
                            check(OriginalPixel(Mathf.FloorToInt(fallbackPixel.x), Mathf.FloorToInt(fallbackPixel.y)),
                                "transparent gap falls back to original skin: " + name + " flip=" + facing);
                            check(ConstipationKingNeedleSurface.IsOpaque(body, fallback), "runtime alpha query respects crop and transform: " + name + " flip=" + facing);
                        }
                    }
                    finally { Object.DestroyImmediate(source); }
                }
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
#endif
