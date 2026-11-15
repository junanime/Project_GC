using UnityEngine;

namespace Vampire
{
    public static class OralGuardArt
    {
        static Sprite[] frames;
        static Material material;
        public static Material Material
        {
            get
            {
                if (material != null) return material;
                var shader = Shader.Find("Vampire/Oral Guard Alpha Clip");
                if (shader == null) return null;
                material = new Material(shader) { name = "Mint guard crisp alpha", hideFlags = HideFlags.DontSave };
                material.SetFloat("_Cutoff", .95f); return material;
            }
        }
        public static Sprite[] Frames
        {
            get
            {
                if (frames != null && frames.Length > 0 && frames[0] != null) return frames;
                var texture = Resources.Load<Texture2D>("OralMap/Events/GuardMint");
                if (texture == null) return null;
                texture.filterMode = FilterMode.Point;
                frames = new Sprite[6];
                // The second row's feet sit 12 pixels higher. Anchor every frame
                // at the same physical feet, instead of letting its canvas bob.
                for (int i = 0; i < frames.Length; i++)
                {
                    int row = i / 3, foot = row == 0 ? 414 : 402;
                    var rect = new Rect((i % 3) * 512 + 80, texture.height - row * 512 - foot, 368, 320);
                    frames[i] = Sprite.Create(texture, rect, new Vector2(.5f, 0), 100);
                    frames[i].name = "Mint guard walk " + i;
                }
                return frames;
            }
        }
    }
}
