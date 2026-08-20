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


        // =========================================================
        // 부적 설명
        // =========================================================
        public string Description
        {
            get
            {
                switch (effectType)
                {
                    case RelicEffectType.MaxHealth:
                        return $"최대 체력이 {effectValue:0} 증가합니다.";

                    case RelicEffectType.MoveSpeed:
                        return $"이동 속도가 {effectValue:0.##} 증가합니다.";

                    case RelicEffectType.CritChance:
                        return $"치명타 확률이 {effectValue * 100f:0.#}% 증가합니다.";

                    default:
                        return string.Empty;
                }
            }
        }
    }
}