using System.Collections.Generic;
using System.Linq;
namespace Vampire
{
    // Retire offers only. IDs and runtime effects remain valid for existing runs.
    public static class ItemOfferPolicy
    {
        public static bool Retired(int id) => id==9||id==10||id==19||id==23||id==35||id==40||id==45||id==53;
        public static bool CanOffer(MerchantItemBlueprint item) => item!=null&&!Retired(item.octoberId);
        public static bool CanBuyInLobby(MerchantItemBlueprint item) => CanOffer(item)&&item.canBuyInLobby;
        public static MerchantItemBlueprint[] ForLobby(IEnumerable<MerchantItemBlueprint> items) =>
            items.Where(i=>i!=null&&(CanBuyInLobby(i)||LobbyLoadoutData.IsEquipped(i))).ToArray();
        public static int Gold(MerchantItemBlueprint.Rarity rarity)
        { switch(rarity){case MerchantItemBlueprint.Rarity.Uncommon:return 50;case MerchantItemBlueprint.Rarity.Rare:return 100;case MerchantItemBlueprint.Rarity.Legendary:return 180;default:return 25;} }
        public static int Silver(MerchantItemBlueprint.Rarity rarity)
        { switch(rarity){case MerchantItemBlueprint.Rarity.Uncommon:return 180;case MerchantItemBlueprint.Rarity.Rare:return 400;case MerchantItemBlueprint.Rarity.Legendary:return 900;default:return 80;} }
    }
}
