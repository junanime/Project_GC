using UnityEngine;

namespace Vampire
{
    public class RelicShopManager : MonoBehaviour
    {
        [SerializeField] private RelicSlotButton[] relicButtons;

        private void Awake()
        {
            InitButtons();
        }

        private void OnEnable()
        {
            RefreshAll();
        }

        private void InitButtons()
        {
            for (int i = 0; i < relicButtons.Length; i++)
            {
                if (relicButtons[i] != null)
                {
                    relicButtons[i].Init(this);
                }
            }
        }

        public void SelectRelic(RelicSlotButton relicButton)
        {
            if (relicButton == null)
            {
                return;
            }

            string relicId = relicButton.RelicId;

            if (string.IsNullOrWhiteSpace(relicId))
            {
                return;
            }

            bool unlocked = RelicSaveData.IsUnlocked(relicId);

            if (!unlocked)
            {
                bool success = TryBuyRelic(relicButton);

                if (!success)
                {
                    Debug.Log("[RelicShop] 실버가 부족해서 유물을 구매할 수 없습니다.");
                    return;
                }
            }

            // 여기서 한 개만 장착됨.
            // 2번을 장착하면 기존 1번은 자동으로 체크 해제됨.
            RelicSaveData.Equip(relicId);

            RefreshAll();
        }

        private bool TryBuyRelic(RelicSlotButton relicButton)
        {
            int price = relicButton.Price;

            // 네 프로젝트의 SilverWallet 함수명에 맞춰 사용.
            // 기존에 TrySpend가 있다면 아래 그대로 사용하면 됨.
            bool paid = SilverWallet.TrySpend(price);

            if (!paid)
            {
                return false;
            }

            RelicSaveData.Unlock(relicButton.RelicId);

            Debug.Log($"[RelicShop] 유물 구매 완료: {relicButton.RelicId}");

            return true;
        }

        private void RefreshAll()
        {
            for (int i = 0; i < relicButtons.Length; i++)
            {
                if (relicButtons[i] != null)
                {
                    relicButtons[i].Refresh();
                }
            }
        }
    }
}