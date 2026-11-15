using UnityEngine;

namespace Vampire
{
    /// <summary>Cached yaw frames of the fully retracted melon turtle, from one unmodified sheet.</summary>
    public static class ColonTurtleSpinFrames
    {
        public const int Count = 8;
        public const float ReferenceWidth = 170f;
        public const float ReferenceHeight = 133f;
        const float Padding = 2f;
        const float PixelsPerUnit = 125f;
        // Opaque bounds in source pixels, measured from the PNG's top-left.
        static readonly Rect[] OpaqueBounds = {
            new Rect(62, 93, 320, 272),
            new Rect(506, 94, 319, 272),
            new Rect(949, 93, 318, 273),
            new Rect(1395, 94, 315, 273),
            new Rect(62, 537, 320, 273),
            new Rect(507, 537, 318, 273),
            new Rect(949, 537, 320, 273),
            new Rect(1392, 536, 321, 274)
        };
        static Sprite[] frames;
        static bool attemptedLoad;

        static int Wrap(int index) => (index % Count + Count) % Count;

        public static Vector2 PixelSize(int index) => OpaqueBounds[Wrap(index)].size;

        public static Sprite Get(int index)
        {
            EnsureLoaded();
            return frames == null ? null : frames[Wrap(index)];
        }

        static void EnsureLoaded()
        {
            if (attemptedLoad) return;
            attemptedLoad = true;
            var texture = Resources.Load<Texture2D>("ColonBosses/turtle/Spin");
            if (texture == null)
            {
                Debug.LogError("Missing melon turtle spin sheet: ColonBosses/turtle/Spin");
                return;
            }
            frames = new Sprite[Count];
            for (int i = 0; i < Count; i++)
            {
                Rect opaque = OpaqueBounds[i];
                var rect = new Rect(opaque.x - Padding, texture.height - opaque.yMax - Padding,
                    opaque.width + Padding * 2, opaque.height + Padding * 2);
                // The opaque bottom stays exactly on the ground, independent of frame height.
                frames[i] = Sprite.Create(texture, rect, new Vector2(.5f, Padding / rect.height),
                    PixelsPerUnit, 0, SpriteMeshType.FullRect);
                frames[i].name = "Spin_" + i.ToString("D3");
            }
        }
    }
}
