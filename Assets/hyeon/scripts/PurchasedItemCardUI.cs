using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class PurchasedItemCardUI : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI countText;

        public void Setup(MerchantItemBlueprint item, int count)
        {
            if (item == null)
            {
                return;
            }

            if (itemIcon != null)
            {
                itemIcon.sprite = item.itemIcon;
                itemIcon.enabled = item.itemIcon != null;
                itemIcon.preserveAspect = true;
            }

            if (countText != null)
            {
                countText.text = count > 1 ? $"x{count}" : string.Empty;
            }
        }
    }
}