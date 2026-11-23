using UnityEngine;
using System;
using System.Collections;

namespace Vampire
{
    public class Chest : MonoBehaviour
    {
        public static event Action<Chest> OnAnyChestOpened;
        public static event Action<Chest> RewardResolved;
        protected ChestBlueprint chestBlueprint; 
        protected EntityManager entityManager;
        protected Character playerCharacter;
        protected ZPositioner zPositioner;
        protected Transform chestItemsParent;
        protected SpriteRenderer spriteRenderer;
        protected bool opened = false;
        public bool GuaranteesOriginal { get; set; }
        GameObject guaranteedDrop;
        Action<Collectable> configureDrop;
        internal bool IsExpectingItem(Action<Collectable> onSpawn) => configureDrop == onSpawn;
        internal void GuaranteeItem(GameObject prefab, Action<Collectable> onSpawn)
        {
            guaranteedDrop = prefab;
            configureDrop = onSpawn;
        }
        public bool CanInteract => !opened && chestBlueprint!=null && GetComponent<Collider2D>().enabled && !AcidLakeEvent.Contains(transform.position);

        public void Init(EntityManager entityManager, Character playerCharacter, Transform chestItemsParent)
        {
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;
            this.chestItemsParent = chestItemsParent;
            (zPositioner = gameObject.AddComponent<ZPositioner>()).Init(playerCharacter.transform);
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void Setup(ChestBlueprint chestBlueprint)
        {
            this.chestBlueprint = chestBlueprint;
            GuaranteesOriginal = false;
            guaranteedDrop = null;
            configureDrop = null;
            transform.localScale = Vector3.one;
            spriteRenderer.sprite = chestBlueprint.Visual(chestBlueprint.closedChest);
            opened = false;
            LootInteraction.Attach(this);
            var marker=GetComponent<MapMarker>();if(marker==null)marker=gameObject.AddComponent<MapMarker>();marker.Configure(MapMarkerKind.Chest,"아이템 상자");
            marker.enabled=true;marker.ResetDiscovery();
            StartCoroutine(Appear());
        }

        private void SpawnLoot(Loot<GameObject> loot, bool openedByPlayer = true)
        {
            GameObject item = Instantiate(loot.item, chestItemsParent);
            item.transform.position = transform.position + Vector3.back * .001f;
            Collectable collectable = item.GetComponent<Collectable>();
            collectable.Init(entityManager, playerCharacter);
            configureDrop?.Invoke(collectable);
            configureDrop = null;
            Coin coin = collectable as Coin;
            if (coin != null)
                coin.Setup(item.transform.position, loot.coinType, true, false, !openedByPlayer);
            else
                collectable.Setup(true, false, !openedByPlayer);
            if (openedByPlayer) collectable.DeliverAfterPop();
        }

        public void OpenChest(bool openedByPlayer = true)
        {
            if (AcidLakeEvent.Contains(transform.position)) return;
            if (!opened)
            {
                opened = true;
                PixelInteractionPrompt.Show(this,false);
                var marker=GetComponent<MapMarker>();if(marker!=null)marker.enabled=false;
                // 상자가 실제로 처음 열리는 순간 1회 재생합니다.
                GameAudioManager.PlaySfx(
                    GameAudioManager.GameSfxId.ChestOpen
                );
                OnAnyChestOpened?.Invoke(this);
                StartCoroutine(Open(openedByPlayer));
            }
        }

        // This is some truly atrocious code: tread with caution.
        private IEnumerator Open(bool openedByPlayer = true)
        {
            // Two chests may overlap the player in one physics step. Queue this
            // reward until the first modal has closed instead of losing a choice.
            while (entityManager.AbilitySelectionDialog.MenuOpen) yield return null;
            spriteRenderer.sprite = chestBlueprint.Visual(chestBlueprint.openingChest);
            bool spawnLoot = chestBlueprint.legendaryAugmentChest
                ? !entityManager.AbilitySelectionDialog.HasAvailableLegendaryAbilities()
                : !chestBlueprint.abilityChest || !entityManager.AbilitySelectionDialog.HasAvailableAbilities();
            if (guaranteedDrop != null)
            {
                spawnLoot = true;
                SpawnLoot(new Loot<GameObject> { item = guaranteedDrop }, openedByPlayer);
                guaranteedDrop = null;
            }
            else if (spawnLoot)
                SpawnLoot(chestBlueprint.lootTable.DropLootObject(), openedByPlayer);
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.sprite = chestBlueprint.Visual(chestBlueprint.openChest);
            if (!spawnLoot)
            {
                while (entityManager.AbilitySelectionDialog.MenuOpen) yield return null;
                bool stillAvailable=chestBlueprint.legendaryAugmentChest
                    ? entityManager.AbilitySelectionDialog.HasAvailableLegendaryAbilities()
                    : entityManager.AbilitySelectionDialog.HasAvailableAbilities();
                if (!stillAvailable) SpawnLoot(chestBlueprint.lootTable.DropLootObject(),openedByPlayer);
                else if (chestBlueprint.legendaryAugmentChest) entityManager.AbilitySelectionDialog.OpenLegendary();
                else if (GuaranteesOriginal) entityManager.AbilitySelectionDialog.OpenOvercharge();
                else entityManager.AbilitySelectionDialog.Open(false);
            }
            // A portal can safely unlock once the chest has granted loot or its choice has closed.
            while (entityManager.AbilitySelectionDialog.MenuOpen) yield return null;
            RewardResolved?.Invoke(this);
            yield return new WaitForSeconds(0.15f);
            float t = 0;
            while (t < 1.0f)
            {
                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, EasingUtils.EaseOutQuart(t));
                t += Time.deltaTime*2;
                yield return null;
            }
            entityManager.DespawnChest(this);
        }

        private IEnumerator Appear()
        {
            GetComponent<Collider2D>().enabled = false;
            float t = 0;
            while (t < 1.0f)
            {
                transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, EasingUtils.EaseInQuart(t));
                t += Time.deltaTime*2;
                yield return null;
            }
            transform.localScale = Vector3.one;
            GetComponent<Collider2D>().enabled = true;
        }

        public void CompleteInitialPlacement()
        {
            if(opened)return;
            StopAllCoroutines();transform.localScale=Vector3.one;
            GetComponent<Collider2D>().enabled=true;
        }

    }
}
