using System.Collections.Generic;
using System.Linq;
namespace Vampire
{
    // Retire offers only. IDs and runtime effects remain valid for existing runs.
    public static class ItemOfferPolicy
    {
        public enum Role { All, Attack, Survival, Status, Utility }
        public static string RoleName(Role role)
        { switch(role){case Role.Attack:return "공격";case Role.Survival:return "생존";case Role.Status:return "상태";case Role.Utility:return "유틸";default:return "전체";} }
        public static Role RoleOf(int id)
        {
            switch(id)
            {
                case 2:case 8:case 15:case 18:case 26:case 35:case 36:case 38:case 41:case 43:case 51:case 52:case 57:case 63:return Role.Survival;
                case 11:case 12:case 42:case 44:case 46:case 47:case 48:case 50:case 53:case 65:return Role.Status;
                case 3:case 4:case 17:case 20:case 39:case 45:case 49:case 59:case 60:case 61:case 64:return Role.Utility;
                default:return Role.Attack;
            }
        }
        public static MerchantItemBlueprint[] Filter(IEnumerable<MerchantItemBlueprint> items,Role role) =>
            items.Where(i=>i!=null&&(role==Role.All||RoleOf(i.octoberId)==role)).ToArray();
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
