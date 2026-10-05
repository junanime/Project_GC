using UnityEngine;
namespace Vampire
{
    public sealed class DiscoveryPreviewArt : ScriptableObject
    {
        public Sprite health, potion, magnet, coin, gem, monster, ring;
        public static DiscoveryPreviewArt Load() => Resources.Load<DiscoveryPreviewArt>("DiscoveryPreviewArt");
    }
}
