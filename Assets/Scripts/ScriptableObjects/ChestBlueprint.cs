using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(fileName = "Chest", menuName = "Blueprints/Chest", order = 1)]
    public class ChestBlueprint : ScriptableObject
    {
        public bool abilityChest = false;
        public bool legendaryAugmentChest = false;
        public Sprite closedChest;
        public Sprite openingChest;
        public Sprite openChest;
        [Min(0)] public float worldSpriteWidth;
        private readonly System.Collections.Generic.Dictionary<Sprite, Sprite> worldSprites = new System.Collections.Generic.Dictionary<Sprite, Sprite>();
        // Reuse UI artwork at a world-space size without scaling the pooled chest's collider.
        public Sprite Visual(Sprite source)
        {
            if (source == null || worldSpriteWidth <= 0) return source;
            if (worldSprites.TryGetValue(source, out var cached) && cached != null) return cached;
            var rect = source.rect;
            var sprite = Sprite.Create(source.texture, rect, source.pivot / rect.size,
                rect.width / worldSpriteWidth, 0, SpriteMeshType.FullRect);
            sprite.name = source.name + " world chest";
            worldSprites[source] = sprite;
            return sprite;
        }
        public LootTable<GameObject> lootTable;
    }
}
