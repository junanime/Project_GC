using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Shared artwork for acquisition cards, catalog and HUD. IDs remain save-compatible.
    public static class PhoenixNobleArt
    {
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static Sprite Get(string key)
        {
            if (!sprites.TryGetValue(key, out var sprite) || sprite == null)
                sprites[key] = sprite = Resources.Load<Sprite>("PhoenixNoble/" + key);
            return sprite;
        }
    }
}
