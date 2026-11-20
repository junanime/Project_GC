using UnityEngine;

namespace Vampire
{
    public class Health : Collectable
    {
        [SerializeField] protected float healAmount = 30;

        internal System.Action PracticePickup;
        protected override bool StoresInInventory => PracticePickup == null && base.StoresInInventory;

        protected override void OnCollected()
        {
            if (PracticePickup != null) PracticePickup();
            else playerCharacter.GainPickupHealth(healAmount);
            GameAudioManager.PlaySfx(
    GameAudioManager.GameSfxId.HealPickup
);
            Destroy(gameObject);
        }
    }
}
