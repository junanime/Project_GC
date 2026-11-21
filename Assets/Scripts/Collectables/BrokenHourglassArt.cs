using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Only the hourglass animates. All drops and HUD slots share four small sprites.
    internal sealed class BrokenHourglassArt : MonoBehaviour
    {
        static Sprite[] frames;
        Image icon;
        SpriteRenderer world;
        int frame = -1;

        public static void Attach(GameObject owner, Image hud = null)
        {
            var art = owner.GetComponent<BrokenHourglassArt>();
            if (art == null) art = owner.AddComponent<BrokenHourglassArt>();
            art.icon = hud;
            art.world = hud == null ? owner.GetComponentInChildren<SpriteRenderer>() : null;
            if (frames == null)
            {
                frames = new Sprite[4];
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = Resources.Load<Sprite>("GStarDemo/broken-hourglass-" + i);
            }
            art.Paint(0);
        }

        void Update() => Paint((int)(Time.time * 8f) % 4);
        void Paint(int next)
        {
            if (frames == null || next == frame) return;
            frame = next;
            if (icon != null) icon.sprite = frames[frame];
            else if (world != null) world.sprite = frames[frame];
        }
    }
}
