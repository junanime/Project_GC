using UnityEngine;

namespace Vampire
{
    public class Health : Collectable
    {
        [SerializeField] protected float healAmount = 30;

        internal System.Action PracticeUse;

        protected override void OnCollected()
        {
            if (PracticeUse != null) PracticeUse();
            else playerCharacter.GainPickupHealth(healAmount);
            GameAudioManager.PlaySfx(
    GameAudioManager.GameSfxId.HealPickup
);
            Destroy(gameObject);
        }
    }
}
