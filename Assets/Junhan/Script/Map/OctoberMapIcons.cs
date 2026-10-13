using UnityEngine;

namespace Vampire
{
    public static class OctoberMapIcons
    {
        public static Sprite For(MapMarker marker)
        {
            var chameleon=marker.GetComponent<AcidToadMonster>();
            if(chameleon!=null)return ChameleonArt.Portrait(chameleon.Kind,0,true);
            if(marker.GetComponent<FinalBossSummonInteractable>()!=null)return OctoberArt.Get("OctoberUI/BossAltarIcon");
            if(marker.MarkerKind==MapMarkerKind.Chest||marker.GetComponent<Chest>()!=null)return OctoberArt.Get("OctoberUI/ItemChestIcon");
            string n=(marker.name+marker.DisplayName).ToLowerInvariant();
            string key=marker.MarkerKind==MapMarkerKind.Boss?"Boss":marker.MarkerKind==MapMarkerKind.Portal?"Portal":marker.MarkerKind==MapMarkerKind.Shop||marker.MarkerKind==MapMarkerKind.NPC?"Merchant":marker.MarkerKind==MapMarkerKind.EliteSpawner||n.Contains("elite")||n.Contains("vending")||n.Contains("자판기")?"Vending":n.Contains("blood")||n.Contains("혈전")?"Portal":null;
            return key!=null?OctoberArt.Get("OctoberUI/MapSymbols",key):RunBookArt.Icon(15);
        }
    }
}
