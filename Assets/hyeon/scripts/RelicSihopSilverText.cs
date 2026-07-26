using TMPro;
using UnityEngine;

namespace Vampire
{
    public class RelicShopSilverText : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI silverText;

        private void Awake()
        {
            if (silverText == null)
            {
                silverText = GetComponent<TextMeshProUGUI>();
            }
        }

        private void OnEnable()
        {
            SilverWallet.OnChanged -= Refresh;
            SilverWallet.OnChanged += Refresh;

            Refresh(SilverWallet.Silver);
        }

        private void OnDisable()
        {
            SilverWallet.OnChanged -= Refresh;
        }

        private void Refresh(int amount)
        {
            if (silverText != null)
            {
                silverText.text = $"보유 실버: {amount}";
            }
        }
    }
}