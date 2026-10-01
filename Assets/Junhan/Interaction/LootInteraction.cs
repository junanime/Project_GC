using UnityEngine;

namespace Vampire
{
    // Joins the same closest-object E-key focus group as existing field interactions.
    public sealed class LootInteraction : InteractableEventObject
    {
        Chest chest;
        MerchantNPC merchant;
        protected override bool AllowMiniStageInteraction=>chest!=null;
        protected override bool InteractionAvailable=>chest!=null?chest.CanInteract:merchant!=null&&merchant.CanInteract;
        protected override Component PromptOwner=>chest!=null?(Component)chest:merchant!=null?merchant:this;
        protected override bool ExecuteInteraction(Character player)
        {
            if(!InteractionAvailable)return false;
            if(chest!=null){chest.OpenChest();return true;}
            return merchant!=null&&merchant.TryOpenShop();
        }
        public static void Attach(Component owner)
        {
            if(owner.GetComponentInChildren<LootInteraction>(true)!=null)return;
            var area=new GameObject("E interaction range");area.transform.SetParent(owner.transform,false);
            var scale=owner.transform.lossyScale;area.transform.localScale=new Vector3(1/Mathf.Max(.01f,Mathf.Abs(scale.x)),1/Mathf.Max(.01f,Mathf.Abs(scale.y)),1);
            var trigger=area.AddComponent<CircleCollider2D>();trigger.isTrigger=true;trigger.radius=1.3f;
            var interaction=area.AddComponent<LootInteraction>();
            interaction.chest=owner as Chest;interaction.merchant=owner as MerchantNPC;
            interaction.ConfigureReusableInteraction();
        }
    }
}
