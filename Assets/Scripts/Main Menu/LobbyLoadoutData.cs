using System;
using System.Collections.Generic;

namespace Vampire
{
    public static class LobbyLoadoutData
    {
        public const int MaxCarryItems = 2;

        private static readonly List<MerchantItemBlueprint> selectedCarryItems = new List<MerchantItemBlueprint>();

        public static event Action OnChanged;

        public static IReadOnlyList<MerchantItemBlueprint> SelectedCarryItems => selectedCarryItems;

        public static bool IsEquipped(MerchantItemBlueprint item)
        {
            if (item == null)
            {
                return false;
            }

            return selectedCarryItems.Contains(item);
        }

        public static bool TryBuyAndEquip(MerchantItemBlueprint item, int silverCost, out string message)
        {
            message = string.Empty;

            if (item == null)
            {
                message = "아이템 정보가 비어 있습니다.";
                return false;
            }

            if (!ItemOfferPolicy.CanBuyInLobby(item))
            {
                message = "현재 판매하지 않는 아이템입니다.";
                return false;
            }

            if (selectedCarryItems.Contains(item))
            {
                message = "이미 장착한 시작 아이템입니다.";
                return false;
            }

            if (selectedCarryItems.Count >= MaxCarryItems)
            {
                message = "시작 아이템은 최대 2개까지 장착할 수 있습니다.";
                return false;
            }

            if (!SilverWallet.TrySpend(silverCost))
            {
                message = "실버 코인이 부족합니다.";
                return false;
            }

            selectedCarryItems.Add(item);
            message = $"{item.itemName} 장착 완료";

            OnChanged?.Invoke();

            return true;
        }

        public static void Unequip(MerchantItemBlueprint item)
        {
            if (item == null)
            {
                return;
            }

            if (selectedCarryItems.Remove(item))
            {
                OnChanged?.Invoke();
            }
        }

        public static void Clear()
        {
            selectedCarryItems.Clear();
            OnChanged?.Invoke();
        }

        public static MerchantItemBlueprint[] ConsumeSelectedCarryItems()
        {
            MerchantItemBlueprint[] result = selectedCarryItems.ToArray();
            selectedCarryItems.Clear();
            OnChanged?.Invoke();
            return result;
        }
    }
}