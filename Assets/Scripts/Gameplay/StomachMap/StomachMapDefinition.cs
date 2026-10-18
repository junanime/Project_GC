using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(menuName = "Vampire/Stomach Map")]
    public sealed class StomachMapDefinition : ScriptableObject
    {
        public Material terrainMaterial;
        // Reference: 1164px-wide map, 7px yellow marker, Ashi visible width = 3.5px.
        // Ashi's opaque idle width = 330 source pixels / 675 PPU * 1.25 = .611111 units.
        [Min(10)] public float worldWidth = 203.2381f;
        [Min(.1f)] public float aspect = 1.5f;
        [Min(4)] public float chunkSize = 18f;
        // Latest reference: 86px-tall yellow box represents Ashi's 276px opaque idle height.
        // The 894px-tall reference contains one full 104.5-cell material tile.
        public const float ReferencePixelSize = (276f / 675f * 1.25f) * (894f / 86f) / 104.5f;
        [Min(.001f)] public float pixelWorldSize = ReferencePixelSize;
        public Vector2 startUV = new Vector2(.543f, .455f);
        [Min(1)] public float wallWidth = 8f;
        [Min(.5f)] public float climbableWidth = 6f;
        public Vector2 Size => new Vector2(worldWidth, worldWidth / aspect);
    }
}
