namespace Vampire
{
    public class Magnet : Collectable
    {   
        protected override void OnCollected()
        {
            entityManager.CollectAllCoinsAndGems();
            GameAudioManager.PlaySfx(
    GameAudioManager.GameSfxId.MagnetPickup
);
            Destroy(gameObject);
        }
    }
}
