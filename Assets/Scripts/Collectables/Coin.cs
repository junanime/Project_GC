using UnityEngine;

namespace Vampire
{
    public enum CoinType 
    {
        Bronze1 = 1,
        Silver2 = 2,
        Gold5 = 5,
        Pouch30 = 30,
        Bag50 = 50
    }

    public class Coin : Collectable
    {
        [Header("Coin Dependencies")]
        [SerializeField] protected CoinBlueprint coinBlueprint;
        protected SpriteRenderer spriteRenderer;
        protected CoinType coinType;
        public CoinType CoinType { get => coinType; }

        protected override void Awake()
        {
            base.Awake();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void Setup(Vector2 position, CoinType coinType = CoinType.Bronze1, bool spawnAnimation = true, bool collectableDuringSpawn = true)
        {
            this.coinType = coinType;
            spriteRenderer.sprite = coinBlueprint.coinSprites[coinType];
            transform.position = position;
            base.Setup(spawnAnimation, collectableDuringSpawn);
        }

        protected override void OnCollected()
        {
            HungrySpiritPickupSignal.NotifyExpOrCoinPickedUp(
                playerCharacter
            );

            switch (coinType)
            {
                case CoinType.Silver2:
                    GameAudioManager.PlaySfx(
                        GameAudioManager.GameSfxId.SilverPickup
                    );
                    break;

                case CoinType.Pouch30:
                case CoinType.Bag50:
                    GameAudioManager.PlaySfx(
                        GameAudioManager.GameSfxId.CoinPouchPickup
                    );
                    break;

                case CoinType.Bronze1:
                case CoinType.Gold5:
                default:
                    GameAudioManager.PlaySfx(
                        GameAudioManager.GameSfxId.GoldPickup
                    );
                    break;
            }

            entityManager.DespawnCoin(this);
        }
    }
}
