using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    [RequireComponent(typeof(Collider2D))]
    public class MerchantNPC : MonoBehaviour
    {
        private Character playerCharacter;
        private bool isShopOpen = false;

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
        }

        void OnCollisionEnter2D(Collision2D col)
        {
            if (!isShopOpen && playerCharacter != null && col.collider.gameObject == playerCharacter.gameObject)
            {
                OpenShopUI();
            }
        }

        private void OpenShopUI()
        {
            isShopOpen = true;
            Debug.Log("수상한 상인과 부딪혔습니다! 상점 UI를 엽니다.");

            Time.timeScale = 0;
            MerchantUIManager.Instance.OpenShop(this);
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