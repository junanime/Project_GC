using System.Collections.Generic;
using System.Linq;

namespace Vampire
{
    // Keep saved IDs and equipped legacy relics. Only new offers retire the exact duplicate.
    public static class RelicSelectionPolicy
    {
        public static bool IsNewOffer(RelicBlueprint relic) => relic != null && relic.relicId != "lotus_chips";
        public static RelicBlueprint[] ForLobby(IEnumerable<RelicBlueprint> catalog) => catalog
            .Where(r => r != null && (IsNewOffer(r) || RelicSaveData.IsUnlocked(r.relicId) || RelicSaveData.IsEquipped(r.relicId))).ToArray();
        public static bool SameChoice(string a, string b) => Canonical(a) == Canonical(b);
        static string Canonical(string id) => id == "lotus_chips" ? "beads" : id;
    }
}
