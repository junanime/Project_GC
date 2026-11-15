using TMPro;
using UnityEngine;

namespace Vampire
{
    public class SilverCoinDisplay : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TextMeshProUGUI silverText;

        [Header("Display Format")]
        [SerializeField] private string format = "실버코인 : {0:N0}";

        [Header("Options")]
        [SerializeField] private bool updateOnEnable = true;
        [SerializeField] private bool useComma = true;

        private void OnEnable()
        {
            SilverWallet.OnChanged += UpdateDisplay;

            if (updateOnEnable)
            {
                UpdateDisplay(SilverWallet.Silver);
            }
        }

        private void OnDisable()
        {
            SilverWallet.OnChanged -= UpdateDisplay;
        }

        public void UpdateDisplay()
        {
            UpdateDisplay(SilverWallet.Silver);
        }

        private void UpdateDisplay(int value)
        {
            if (silverText == null)
            {
                return;
            }

            if (GStarDemo.Enabled) { silverText.text = "체험판 · 무료"; return; }

            if (useComma)
            {
                silverText.text = string.Format(format, value);
            }
            else
            {
                string noCommaFormat = format.Replace(":N0", "");
                silverText.text = string.Format(noCommaFormat, value);
            }
        }
    }
}
