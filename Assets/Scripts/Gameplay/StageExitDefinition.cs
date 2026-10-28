using UnityEngine;
namespace Vampire
{
    // Shared boss-hole fallback and chest rewards. Colon travel has its own small resource definition.
    public sealed class StageExitDefinition:ScriptableObject
    {
        public Sprite crater;
        public ChestBlueprint rewardChest;
    }
}
