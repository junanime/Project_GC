using System.Collections.Generic;
using System.Linq;

namespace Vampire
{
    // Keep saved IDs and owned legacy relics; retire redundant new offers only.
    public static class RelicSelectionPolicy
    {
        public static string Replacement(string id)
        {
            switch(id)
            {
                case "relic_01":return "ginseng_jelly";
                case "citrus":return "relic_02";
                case "lotus_chips":return "beads";
                case "piercing_arrow":return "needle_case";
                case "walnut":return "firm_shield";
                case "honey_rice":return "bandage";
                case "omija":return "relic_03";
                case "ginger_tea":return "clear_dew";
                default:return null;
            }
        }
        public static bool IsNewOffer(RelicBlueprint relic) => relic != null && Replacement(relic.relicId)==null;
        public static RelicBlueprint[] ForLobby(IEnumerable<RelicBlueprint> catalog) => catalog
            .Where(r => r != null && (IsNewOffer(r) || RelicSaveData.IsUnlocked(r.relicId) || RelicSaveData.IsEquipped(r.relicId))).ToArray();
        public static bool SameChoice(string a, string b) => Canonical(a) == Canonical(b);
        static string Canonical(string id)
        {
            switch(id)
            {
                case "lotus_chips":return "beads";
                case "piercing_arrow":return "needle_case";
                case "honey_rice":return "bandage";
                default:return id;
            }
        }
    }
}
