using UnityEngine;

namespace Vampire
{
    public enum CoinType 
    {
        Bronze1 = 1,
        Silver2 = 2,
        Gold5 = 5,
        Pouch30 = 30,
        Bag50 = 50,
        Bag80 = 80
    }

    public class Coin : Collectable
    {
        [Header("Coin Dependencies")]
        [SerializeField] protected CoinBlueprint coinBlueprint;
        protected SpriteRenderer spriteRenderer;
        protected CoinType coinType;
        bool scoreToken;
        public bool IsScoreToken => scoreToken;
        public CoinType CoinType { get => coinType; }

        protected override void Awake()
        {
            base.Awake();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void Setup(Vector2 position, CoinType coinType = CoinType.Bronze1, bool spawnAnimation = true, bool collectableDuringSpawn = true, bool requirePickupRange = false, bool asScoreToken = false)
        {
            this.coinType = coinType;
            scoreToken = asScoreToken;
            spriteRenderer.sprite = asScoreToken ? GStarScoreTokenArt.Sprite : coinBlueprint.coinSprites[coinType];
            gameObject.name = asScoreToken ? "점수 토큰" : "Coin";
            transform.position = position;
            base.Setup(spawnAnimation, collectableDuringSpawn, requirePickupRange);
        }

        protected override void OnCollected()
        {
            HungrySpiritPickupSignal.NotifyExpOrCoinPickedUp(
                playerCharacter
            );

            if (scoreToken)
            {
                GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.ExpPickup);
                entityManager.DespawnCoin(this);
                return;
            }

            switch (coinType)
            {
                case CoinType.Silver2:
                    GameAudioManager.PlaySfx(
                        GameAudioManager.GameSfxId.SilverPickup
                    );
                    break;

                case CoinType.Pouch30:
                case CoinType.Bag50:
                case CoinType.Bag80:
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

    // A compact mint four-point token, intentionally distinct from gold/silver currency.
    public static class GStarScoreTokenArt
    {
        static Sprite sprite;
        public static Sprite Sprite
        {
            get
            {
                if (sprite != null) return sprite;
                const int size = 24;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "GStar mint score token" };
                var colors = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - 11.5f), dy = Mathf.Abs(y - 11.5f);
                    float edge = Mathf.Min(dx / 3f + dy / 11f, dx / 11f + dy / 3f);
                    if (edge > 1) continue;
                    colors[y * size + x] = edge > .77f ? new Color(.12f,.26f,.25f) :
                        edge > .59f ? new Color(.20f,.72f,.55f) : y > x ? new Color(.86f,1f,.81f) : new Color(.43f,.92f,.72f);
                }
                texture.SetPixels(colors); texture.Apply();
                sprite = UnityEngine.Sprite.Create(texture, new Rect(0,0,size,size), Vector2.one * .5f, 48);
                sprite.name = "점수 토큰";
                return sprite;
            }
        }
    }
}
