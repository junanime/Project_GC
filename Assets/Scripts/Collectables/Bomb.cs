using UnityEngine;

namespace Vampire
{
    public class Bomb : Collectable
    {
        [SerializeField] protected float bombDamage;

        protected override void OnCollected()
        {
            entityManager.DamageAllVisibileEnemies(bombDamage);
            GameAudioManager.PlaySfx(
    GameAudioManager.GameSfxId.BombPickup
);
            Destroy(gameObject);
        }
    }
}
