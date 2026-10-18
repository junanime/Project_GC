using System.Collections;
using UnityEngine;

namespace Vampire
{
    public abstract class Collectable : MonoBehaviour
    {
        [Header("Type")]
        [field: SerializeField] public CollectableType CollectableType;
        [Header("Spawn Animation")]
        [SerializeField] protected float saSpeed = 1;
        [SerializeField] protected float saHeight = 1;
        [SerializeField] protected float saOffsetMax = 0.2f;
        [Header("Collect Animation")]
        [SerializeField] protected float juiciness = 2;
        [SerializeField] protected float lerpTime = 1;
        [Header("Attributes")]
        [SerializeField] protected bool magnetic = false;
        protected EntityManager entityManager;
        protected Character playerCharacter;
        protected ZPositioner zPositioner;
        protected Collider2D col;
        protected bool beingCollected = false;
        private bool waitingForSpawn;
        private bool requiresPickupRange;
        public bool IsBeingCollected => beingCollected;
        public uint SpawnGeneration { get; private set; }

        protected virtual void Awake()
        {
            col = GetComponent<Collider2D>();
            zPositioner = gameObject.AddComponent<ZPositioner>();
        }

        public virtual void Init(EntityManager entityManager, Character playerCharacter)
        {
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;
            zPositioner.Init(playerCharacter.transform);
        }

        public virtual void Setup(bool spawnAnimation = true, bool collectableDuringSpawn = true, bool requirePickupRange = false)
        {
            unchecked { SpawnGeneration++; }
            waitingForSpawn = spawnAnimation && !collectableDuringSpawn;
            requiresPickupRange = requirePickupRange;
            col.enabled = !spawnAnimation || collectableDuringSpawn;
            beingCollected = false;
            enabled = true; //  [추가] 오브젝트 재사용을 위해 고유 Update 루프를 다시 켭니다.

            if (magnetic)
                entityManager.MagneticCollectables.Add(this);
            if (spawnAnimation)
                StartCoroutine(SpawnAnimation());
            gameObject.SetActive(true);
            TutorialGuide.QueueItem(this);

            //  [추가] 만약 보석이 스폰되는 시점에 이미 MRI 자석을 보유 중이라면 즉시 흡수!
            if (playerCharacter != null && playerCharacter.AutoCollectItems)
            {
                Collect();
            }
        }

        public virtual void Collect(CollectionMode collectionMode = CollectionMode.FromGround)
        {
            if (AcidLakeEvent.Contains(transform.position)) return;
            // Don't collect again if already being collected
            if (beingCollected || waitingForSpawn)
                return;

            // Chest rewards stay in the field until the player's pickup area reaches them,
            // including when an automatic collection relic is active.
            if (requiresPickupRange && (!col.enabled || !playerCharacter.CollectableCollider.enabled ||
                col.Distance(playerCharacter.CollectableCollider).distance > 0f))
                return;

            // Don't collect if storable in inventory and inventory is full
            bool storeInInventory = CollectableType.inventoryStackSize > 0;
            bool hasInventorySlot = entityManager.Inventory.TryGetInventorySlot(this, out InventorySlot inventorySlot);
            if (storeInInventory && hasInventorySlot && inventorySlot.IsFull())
                return;
            
            // Flag that this is being collected so that it does not accidentally get collected a second time
            beingCollected = true;
            col.enabled = false;
            if (magnetic)
                entityManager.MagneticCollectables.Remove(this);

            // Both consumables and immediate rewards are absorbed by the character.
            if (storeInInventory && hasInventorySlot)
                StartCoroutine(StoreAfterPickup(inventorySlot, collectionMode));
            else
                StartCoroutine(FlyToPlayer(collectionMode));
        }

        public void Use()
        {
            OnCollected();
        }

        public void DissolveInAcidLake()
        {
            if (!gameObject.activeInHierarchy) return;
            StopAllCoroutines();
            if (beingCollected && entityManager.Inventory.TryGetInventorySlot(this, out var slot)) slot.CancelItemBeingCollected(this);
            if (entityManager.MagneticCollectables.Contains(this)) entityManager.MagneticCollectables.Remove(this);
            if (this is ExpGem gem) entityManager.DespawnGem(gem);
            // Chest coins are instantiated rather than obtained from CoinPool.
            else if (this is Coin coin && GetComponentInParent<CoinPool>() != null) entityManager.DespawnCoin(coin, false);
            else Destroy(gameObject);
        }

        protected abstract void OnCollected();

        protected virtual IEnumerator FlyToPlayer(CollectionMode collectionMode = CollectionMode.FromGround)
        {
            yield return StartCoroutine(MoveToPlayer(collectionMode));
            OnCollected();
        }

        private IEnumerator MoveToPlayer(CollectionMode collectionMode)
        {
            if (collectionMode == CollectionMode.FromChest)
            {
                yield return StartCoroutine(ChestAnimation());
                zPositioner.enabled = true;
            }

            float distance = Vector2.Distance(transform.position, playerCharacter.CenterTransform.position);
            if (distance == 0.0f) distance = Mathf.Epsilon;
            float c = juiciness / distance;
            float timeScale = 1.0f/(lerpTime*Mathf.Sqrt(distance));
            float t = -Time.deltaTime*timeScale;
            Vector3 pickupPos = transform.position;
            while (t < 1)
            {
                t += Time.deltaTime*timeScale;
                float lerpT = EasingUtils.EaseInBack(t, c);
                if (lerpT >= 1) break;
                transform.position = Vector3.LerpUnclamped(pickupPos, playerCharacter.CenterTransform.position, lerpT);
                yield return null;
            }
            transform.position = playerCharacter.CenterTransform.position;
            yield return null;
        }

        private IEnumerator StoreAfterPickup(InventorySlot inventorySlot, CollectionMode collectionMode)
        {
            // Reserve capacity during pickup so overlapping drops cannot exceed the limit.
            inventorySlot.AddItemBeingCollected(this);
            yield return StartCoroutine(MoveToPlayer(collectionMode));
            gameObject.SetActive(false);
            inventorySlot.FinalizeAddItemBeingCollected(this);
        }

        protected virtual IEnumerator SpawnAnimation()
        {
            zPositioner.enabled = false;
            float t = 0;
            float horizontalSpeed = Random.Range(-saOffsetMax, saOffsetMax);
            Vector3 spawnPosition = transform.position;
            while (!beingCollected && t < 1)
            {
                transform.position = spawnPosition + Vector3.up*EasingUtils.Bounce(t, saHeight) + Vector3.right*horizontalSpeed*t;
                t += Time.deltaTime*saSpeed;
                yield return null;
            }
            if (!beingCollected)
                transform.position = spawnPosition + Vector3.right * horizontalSpeed;
            waitingForSpawn = false;
            col.enabled = !beingCollected;
            zPositioner.enabled = true;
        }

        protected virtual IEnumerator ChestAnimation()
        {
            float t = 0;
            float horizontalSpeed = Random.Range(-saOffsetMax, saOffsetMax);
            Vector3 spawnPosition = transform.position;
            while (t < 0.2f)
            {
                zPositioner.enabled = false;
                transform.position = spawnPosition + Vector3.up*EasingUtils.Bounce(t, saHeight) + Vector3.right*horizontalSpeed*t;
                t += Time.deltaTime*saSpeed;
                yield return null;
            }
        }

        void OnTriggerStay2D(Collider2D collider)
        {
            if (collider == playerCharacter.CollectableCollider)
            {
                Collect();
            }
        }

        protected virtual void Update()
        {
            // 플레이어가 MRI 자석을 보유하고 있고, 아직 수집이 시작되지 않은 보석이라면
            if (playerCharacter != null && playerCharacter.AutoCollectItems && !beingCollected)
            {
                Collect();
                if (beingCollected) enabled = false;
            }
        }

        public enum CollectionMode
        {
            FromGround,
            FromChest
        }
    }
}
