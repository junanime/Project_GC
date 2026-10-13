using UnityEngine;
namespace Vampire
{
    public sealed class ShiniEmberArt : ScriptableObject
    {
        public Sprite[] cast, flames, digits;
        public Vector2[] mouths;
        public float castHeight;
        public Sprite ember;
        public float emberHeight;
        public Material flameMaterial;
    }
}
