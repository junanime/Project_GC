using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(fileName = "NewRelic", menuName = "Vampire/Relic Blueprint")]
    public class RelicBlueprint : ScriptableObject
    {
        public enum RelicEffectType
        {
            MaxHealth,
            MoveSpeed,
            CritChance
        }

        [Header("Basic Info")]
        public string relicId;
        public string relicName;
        public int price;
        public Sprite icon;

        [Header("Effect")]
        public RelicEffectType effectType;
        public float effectValue;
    }
}