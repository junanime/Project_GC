using UnityEngine;

namespace Vampire
{
    // Shared action state, never fake keyboard/mouse events. One touch on one
    // control cannot release another finger's movement or charge gesture.
    public static class MobileGameplayInput
    {
        public static bool Active { get; internal set; }
        public static bool ChargeHeld { get; internal set; }
        public static Vector2 Aim { get; internal set; }
        private static int interactionFrame = -100;
        private static int arrowFrame = -100;
        private static int arrow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Active = false;
            ClearGestures();
        }

        public static void ClearGestures()
        {
            ChargeHeld = false;
            Aim = Vector2.zero;
            interactionFrame = arrowFrame = -100;
        }

        public static void RequestInteraction()
        {
            if (Active && Time.timeScale > 0) interactionFrame = Time.frameCount;
        }

        public static bool ConsumeInteraction()
        {
            if (!Active || Time.timeScale <= 0 || Time.frameCount - interactionFrame > 1) return false;
            interactionFrame = -100;
            return true;
        }

        // Directions are explicit: 0 up, 1 down, 2 left, 3 right.
        public static void RequestArrow(int direction)
        {
            if (!Active || Time.timeScale <= 0 || direction < 0 || direction > 3) return;
            arrow = direction;
            arrowFrame = Time.frameCount;
        }

        public static bool ConsumeArrow(out int direction)
        {
            direction = arrow;
            if (!Active || Time.timeScale <= 0 || Time.frameCount - arrowFrame > 1) return false;
            arrowFrame = -100;
            return true;
        }
    }
}
