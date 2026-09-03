using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class LobbyShopPopup : MonoBehaviour
    {
        [SerializeField] private Button shopIconButton;
        [SerializeField] private GameObject shopUIContainer;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (shopIconButton != null)
                shopIconButton.onClick.AddListener(OpenShop);

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseShop);

            if (shopUIContainer != null)
                shopUIContainer.SetActive(false);
        }

        public void OpenShop()
        {
            shopUIContainer.SetActive(true);
        }

        public void CloseShop()
        {
            shopUIContainer.SetActive(false);
        }
    }
}