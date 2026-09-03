using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class AugmentCardUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;

        public void Setup(AugmentHistoryManager.AugmentEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (icon != null)
            {
                icon.sprite = entry.icon;
                icon.enabled = entry.icon != null;
                icon.preserveAspect = true;
            }

            if (nameText != null)
            {
                nameText.text = entry.displayName;
            }

            if (levelText != null)
            {
                levelText.text = $"Lv.{entry.level}";
            }
        }
    }
}