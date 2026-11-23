using UnityEngine;
using TMPro;

namespace Vampire
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class CoinDisplay : MonoBehaviour
    {
        private TextMeshProUGUI coinText;

        void Start()
        {
            coinText = GetComponent<TextMeshProUGUI>();
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            if (coinText == null) coinText = GetComponent<TextMeshProUGUI>();
            coinText.text = ProgressPrefs.GetInt("Coins").ToString();
        }
    }
}
