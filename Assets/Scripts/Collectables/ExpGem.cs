using System.Collections;
using UnityEngine;

namespace Vampire
{
    public enum GemType 
    {
        White1 = 1,
        Blue2 = 2,
        Green10 = 10,
        Red50 = 50
    }

    public class ExpGem : Collectable
    {
        [Header("Gem Dependencies")]
        [SerializeField] protected ExpGemBlueprint gemBlueprint;
        protected GemType gemType;
        protected TrailRenderer trailRenderer;
        protected SpriteRenderer spriteRenderer;
        bool inWhirlpool;
        Color beforeWhirlpool;

        public TrailRenderer TrailRenderer { get => trailRenderer; }

        protected override void Awake()
        {
            base.Awake();
            StageActorRegistry.Register<ExpGem>(this);
            trailRenderer = GetComponentInChildren<TrailRenderer>(true);
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        void OnDestroy() => StageActorRegistry.Remove<ExpGem>(this);

        public void Setup(Vector2 position, GemType gemType = GemType.White1, bool spawnAnimation = true)
        {
            trailRenderer.emitting = false;
            transform.position = position;
            trailRenderer.emitting = true;
            if (inWhirlpool)
            {
                spriteRenderer.color = beforeWhirlpool;
                trailRenderer.Clear();
                inWhirlpool = false;
            }
            this.gemType = gemType;
            (Sprite sprite, Color color) = gemBlueprint.gemSpritesAndColors[gemType];
            spriteRenderer.sprite = sprite;
            trailRenderer.startColor = color;
            trailRenderer.endColor = color;
            base.Setup(spawnAnimation);
        }

        public override void Collect(CollectionMode mode = CollectionMode.FromGround)
        {
            // A magnet or contact trigger may execute before the finale's LateUpdate scan.
            if (beingCollected || (OralSwallowFinale.Instance?.TryClaimGem(this) ?? false)) return;
            base.Collect(mode);
        }

        internal void EnterWhirlpool()
        {
            StopAllCoroutines();
            beingCollected = true;
            col.enabled = false;
            enabled = false;
            zPositioner.enabled = true;
            if (magnetic && entityManager.MagneticCollectables.Contains(this)) entityManager.MagneticCollectables.Remove(this);
            beforeWhirlpool = spriteRenderer.color;
            inWhirlpool = true;
            spriteRenderer.color = new Color(.68f, .77f, .9f, .82f);
        }

        protected override void OnCollected()
        {
            spriteRenderer.enabled = false;

            HungrySpiritPickupSignal.NotifyExpOrCoinPickedUp(
                playerCharacter
            );

            playerCharacter.GainExp(
                (float)gemType
            );

            // 실제 EXP 획득 처리가 완료된 순간
            // EXP 양과 관계없이 동일한 획득 효과음을 1회 재생합니다.
            GameAudioManager.PlaySfx(
                GameAudioManager.GameSfxId.ExpPickup
            );

            entityManager.DespawnGem(this);

            spriteRenderer.enabled = true;
        }
    }
}
