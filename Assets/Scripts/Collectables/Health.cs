using UnityEngine;

namespace Vampire
{
    public class Health : Collectable
    {
        [SerializeField] protected float healAmount = 30;

        protected override void OnCollected()
        {
            playerCharacter.GainHealth(healAmount);
            GameAudioManager.PlaySfx(
    GameAudioManager.GameSfxId.HealPickup
);
            Destroy(gameObject);
        }
    }
}
