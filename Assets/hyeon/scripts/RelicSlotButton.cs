using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class RelicSlotButton : MonoBehaviour
    {
        [Header("Relic Data")]
        [SerializeField] private RelicBlueprint relic;

        [Header("UI")]
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject lockObject;
        [SerializeField] private GameObject checkObject;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI statusText;

        private RelicShopManager manager;

        public RelicBlueprint Relic => relic;
        public string RelicId => relic != null ? relic.relicId : string.Empty;
        public int Price => relic != null ? relic.price : 0;

        public void Init(RelicShopManager owner)
        {
            manager = owner;

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
                button.onClick.AddListener(OnClick);
            }

            Refresh();
        }

        private void OnClick()
        {
            if (manager != null)
            {
                manager.SelectRelic(this);
            }
        }

        public void Refresh()
        {
            if (relic == null)
            {
                Debug.LogWarning($"[RelicSlotButton] {gameObject.name}에 RelicBlueprint가 연결되지 않았습니다.");
                return;
            }

            bool unlocked = RelicSaveData.IsUnlocked(relic.relicId);
            bool equipped = RelicSaveData.IsEquipped(relic.relicId);

            if (iconImage != null)
            {
                iconImage.sprite = relic.icon;
                iconImage.enabled = relic.icon != null;
                iconImage.preserveAspect = true;

                Color iconColor = iconImage.color;
                iconColor.a = unlocked ? 1f : 0.45f;
                iconImage.color = iconColor;
            }

            if (lockObject != null)
            {
                lockObject.SetActive(!unlocked);
            }

            if (checkObject != null)
            {
                checkObject.SetActive(equipped);
            }

            if (nameText != null)
            {
                nameText.text = relic.relicName;
            }

            if (priceText != null)
            {
                priceText.gameObject.SetActive(!unlocked);
                priceText.text = $"{relic.price} Silver";
            }

            if (statusText != null)
            {
                if (!unlocked)
                {
                    statusText.text = "잠김";
                }
                else if (equipped)
                {
                    statusText.text = "장착중";
                }
                else
                {
                    statusText.text = "보유중";
                }
            }
        }
    }
}