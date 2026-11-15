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
            coinText.text = GStarDemo.Enabled ? "체험판 · 무료" : ProgressPrefs.GetInt("Coins").ToString();
        }
    }
}
