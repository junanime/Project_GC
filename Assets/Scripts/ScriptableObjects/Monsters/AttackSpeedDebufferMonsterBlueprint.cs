using UnityEngine;
namespace Vampire
{
    // Preserve the asset identity and spawn index while changing to melee behavior.
    [CreateAssetMenu(fileName = "Rolling Candy Debuffer", menuName = "Blueprints/Monsters/Rolling Candy Debuffer", order = 12)]
    public class AttackSpeedDebufferMonsterBlueprint : MeleeMonsterBlueprint
    {
        [Min(0.1f)] public float debuffDuration = 10f;
        [Min(0.1f)] public float contactDebuffCooldown = 2.2f;
    }
}
