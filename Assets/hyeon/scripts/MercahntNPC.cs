using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    [RequireComponent(typeof(Collider2D))]
    public class MerchantNPC : MonoBehaviour
    {
        private Character playerCharacter;
        private bool isShopOpen = false;
        public bool CanInteract => !isShopOpen && !MiniStageRuntimeState.IsInsideMiniStage && MerchantUIManager.Instance!=null;

        private List<MerchantItemBlueprint> shopItems = new List<MerchantItemBlueprint>();
        private bool hasGeneratedShopItems = false;

        void Start()
        {
            playerCharacter = FindObjectOfType<Character>();

            if (playerCharacter != null)
            {
                ZPositioner zPositioner = gameObject.AddComponent<ZPositioner>();
                zPositioner.Init(playerCharacter.transform);
            }

            GetComponent<Collider2D>().isTrigger = false;
            LootInteraction.Attach(this);
        }

        public bool TryOpenShop()
        {
            if(!CanInteract||Time.timeScale<=0)return false;
            isShopOpen=true;
            PixelInteractionPrompt.Show(this,false);
            Time.timeScale=0;
            MerchantUIManager.Instance.OpenShop(this);
            return true;
        }

        public void CloseShopUI()
        {
            isShopOpen = false;
            Time.timeScale = 1;
        }

        public bool HasGeneratedShopItems()
        {
            return hasGeneratedShopItems;
        }

        public List<MerchantItemBlueprint> GetShopItems()
        {
            return shopItems;
        }

        public void SetShopItems(List<MerchantItemBlueprint> items)
        {
            shopItems = new List<MerchantItemBlueprint>(items);
            hasGeneratedShopItems = true;
        }

        public void ClearShopItems()
        {
            shopItems.Clear();
            hasGeneratedShopItems = false;
        }
    }
}
