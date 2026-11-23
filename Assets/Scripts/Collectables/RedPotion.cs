using UnityEngine;

namespace Vampire
{
    // Retains the original prefab component identity so existing loadouts still resolve.
    public class RedPotion : Collectable
    {
        protected override void Awake()
        {
            base.Awake();
            BrokenHourglassArt.Attach(gameObject);
        }

        protected override void OnCollected() => TryActivate();

        internal bool TryActivate()
        {
            var level = FindObjectOfType<LevelManager>();
            if (level == null || !level.ActivateHourglass()) return false;
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.RedPotionPickup);
            Destroy(gameObject);
            return true;
        }
    }
}
