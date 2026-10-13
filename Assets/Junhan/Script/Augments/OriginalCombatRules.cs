using UnityEngine;

namespace Vampire
{
    // Stage values are totals, never compounded with the previous selection.
    public static class OriginalCombatRules
    {
        public static float FormationDamage(int stage) => 1f;
        public static int FormationEndpointCount(int stage) => stage <= 0 ? 0 : 1 + 2 * Mathf.Clamp(stage, 1, 3);
        public static float FormationRepeatChance(int stage) => stage <= 0 ? 0f : stage == 1 ? .20f : stage == 2 ? .31f : .40f;
        public static float FormationCollisionDamage(int stage) => stage <= 0 ? 0f : 1f + .25f * (Mathf.Clamp(stage, 1, 3) - 1);
        public static float AlternatingBonus(int stage) => stage <= 0 ? 0f : stage == 1 ? .20f : stage == 2 ? .35f : .50f;
        public static float BipolarDamage(int crossStage, int alternateStage, bool lateral, bool empowered)
            => 1f + (crossStage >= (lateral ? 3 : 2) ? .25f : 0f) + (empowered ? AlternatingBonus(alternateStage) : 0f);
        public static float PierceSpeed(int stage, int targets)
            => 1f + (stage <= 0 ? 0f : .05f + .05f * Mathf.Clamp(stage, 1, 3)) * Mathf.Clamp(targets, 0, 2 + Mathf.Clamp(stage, 1, 3));
        public static float PierceDamage(int stage, int previousTargets)
            => 1f + .06f * Mathf.Clamp(stage, 0, 3) * Mathf.Clamp(previousTargets, 0, 5);
        public static float PressureBonus(int stage, float distance, float perDistance, float maximum)
            => Mathf.Clamp(Mathf.Max(0, distance) * Mathf.Max(0, perDistance) + Mathf.Max(0, maximum) * .10f * Mathf.Clamp(stage, 0, 3), 0, Mathf.Max(0, maximum));
        public static float BacteriaHarvestChance(int stage, int stacks)
            => stage <= 0 ? 0 : Mathf.Clamp01(Mathf.Max(0, stacks - 3) * (.05f + .05f * Mathf.Clamp(stage, 1, 3)));
    }
}
