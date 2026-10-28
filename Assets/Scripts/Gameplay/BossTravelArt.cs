using UnityEngine;
namespace Vampire
{
    public static class BossTravelArt
    {
        static BossTravelDefinition Definition=>Resources.Load<BossTravelDefinition>("BossTravel/Definition");
        public static Sprite Hole=>Definition!=null?Definition.hole:null;
        public static float AshiHeight=>Definition!=null?Definition.ashiHeight:.5111111f;
        public static Sprite Suit(FoodBossKind k)=>Definition!=null&&Definition.suits.Length>(int)k?Definition.suits[(int)k]:null;
    }
}
