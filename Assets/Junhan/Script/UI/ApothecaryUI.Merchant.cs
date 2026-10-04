using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        List<MerchantItemBlueprint> merchantItems;
        MerchantUIManager merchant;
        int merchantRerollCost;
        public void ShowOctoberShop(List<MerchantItemBlueprint> items,int cost,MerchantUIManager manager)
        {merchantItems=items;merchantRerollCost=cost;merchant=manager;Show("merchant");}
        void OctoberMerchant()
        {
            var dim=ImageAt(content,null,0,0,1,1,false);dim.color=new Color(0,0,0,.74f);
            ImageAt(content,OctoberArt.Get("OctoberUI/Banner"),.32f,.78f,.68f,1);
            var title=Label(content,"수상한 아저씨",.34f,.86f,.66f,.967f,32);title.color=new Color(1,.94f,.68f);title.outlineColor=OctoberArt.Ink;title.outlineWidth=.15f;
            Label(content,$"보유 골드  {(stats!=null?stats.CoinsGained:0):N0}",.75f,.865f,.94f,.92f,20).color=Color.white;
            for(int i=0;i<(merchantItems?.Count??0);i++)
            {
                var item=merchantItems[i];float x=.155f+i*.235f;
                var card=Rect("Merchant "+item.itemName,content,x,.20f,x+.22f,.83f);
                var grade=item.itemRarity==MerchantItemBlueprint.Rarity.Uncommon?AugmentUpgradeGrade.Rare:item.itemRarity==MerchantItemBlueprint.Rarity.Rare?AugmentUpgradeGrade.Epic:item.itemRarity==MerchantItemBlueprint.Rarity.Legendary?AugmentUpgradeGrade.Legendary:AugmentUpgradeGrade.Common;
                ImageAt(card,AugmentPanelTheme.Frame(grade),0,0,1,1,false);
                ImageAt(card,item.itemIcon,.16f,.58f,.84f,.91f);
                Label(card,AugmentUpgradeOdds.DisplayName(grade),.25f,.50f,.75f,.565f,16).color=AugmentPanelTheme.Accent(grade)*.7f;
                Label(card,item.itemName,.08f,.405f,.92f,.50f,23).fontStyle=TMPro.FontStyles.Bold;
                Label(card,item.description,.11f,.155f,.89f,.401f,18);
                var runtime=level?.PlayerCharacter?.GetComponent<OctoberItemRuntime>();
                int price=RelicRuntime.Price(level?.PlayerCharacter,item.cost);
                bool owned=runtime!=null&&runtime.Has(item.octoberId);bool afford=stats!=null&&stats.CoinsGained>=price;
                ActionButton(card,owned?"보유 중":$"구매 · {price} G",.13f,.04f,.87f,.138f,()=>merchant.OnClickPurchaseItem(item,null),true,!owned&&afford);
            }
            ActionButton(content,"새로고침 · "+merchantRerollCost+" G",.37f,.075f,.63f,.16f,()=>merchant.OnClickRerollItems(),false,stats!=null&&stats.CoinsGained>=merchantRerollCost);
            ActionButton(content,"닫기",.77f,.075f,.92f,.16f,()=>merchant.CloseShop());
        }
    }
}
