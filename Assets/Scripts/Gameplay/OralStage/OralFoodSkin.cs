using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // All variants share the exact source alpha, pixel grid, bounds and foot pivot.
    public sealed class OralFoodSkin
    {
        public Sprite Body { get; private set; }
        public Sprite Left { get; private set; }
        public Sprite Right { get; private set; }

        public static OralFoodSkin[] Build(Sprite original, Texture2D flavors, int kind, List<Object> owned)
        {
            var variants = new OralFoodSkin[flavors == null ? 1 : 3];
            for (int variant = 0; variant < variants.Length; variant++)
            {
                var sprite = original;
                if (variant > 0)
                {
                    // Two-pixel gutters around every frame prevent a neighbouring wrapper
                    // from appearing at the edge. The palettes are baked at import size.
                    int cell = (variant - 1) * 6 + kind;
                    float x = flavors.width / 1410f, y = flavors.height / 1536f;
                    var rect = new Rect((cell%3 * 470 + 2)*x, (cell/3 * 384 + 2)*y,
                        original.rect.width * 1536f / original.texture.width * x,
                        original.rect.height * 1024f / original.texture.height * y);
                    sprite = Sprite.Create(flavors,rect,new Vector2(.5f,0),rect.height / original.bounds.size.y,0,SpriteMeshType.FullRect);
                    sprite.name = original.name + " palette " + variant;
                    owned.Add(sprite);
                }
                var skin = variants[variant] = new OralFoodSkin { Body = sprite };
                if (kind != 0) continue;
                Rect r = sprite.rect;
                int cut = Mathf.FloorToInt(r.width * .5f);
                skin.Left = Half(sprite, new Rect(r.x,r.y,cut,r.height), owned);
                skin.Right = Half(sprite, new Rect(r.x+cut,r.y,r.width-cut,r.height), owned);
            }
            return variants;
        }

        static Sprite Half(Sprite body, Rect rect, List<Object> owned)
        {
            var half = Sprite.Create(body.texture, rect, new Vector2(.5f, .5f),
                body.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            owned.Add(half);
            return half;
        }

    }
}
