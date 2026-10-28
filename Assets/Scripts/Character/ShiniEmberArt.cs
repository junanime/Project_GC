using UnityEngine;
namespace Vampire
{
    public sealed class ShiniEmberArt : ScriptableObject
    {
        public Sprite[] cast, flames, digits;
        public Sprite[] castUp, castDown;
        public Vector2[] mouths;
        public Vector2[] mouthsUp, mouthsDown;
        public float castHeight;
        public Sprite ember;
        public float emberHeight;
        public Material flameMaterial;
    }
}
