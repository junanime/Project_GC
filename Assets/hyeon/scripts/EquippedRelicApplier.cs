using UnityEngine;

namespace Vampire
{
    public class EquippedRelicApplier : MonoBehaviour
    {
        [SerializeField] private Character playerCharacter;
        [SerializeField] private RelicBlueprint[] relics;

        private void Start()
        {
            if (playerCharacter == null)
            {
                playerCharacter = FindObjectOfType<Character>();
            }

            if (playerCharacter == null)
            {
                Debug.LogWarning("[EquippedRelicApplier] Character를 찾지 못했습니다.");
                return;
            }

            ApplyEquippedRelic();
        }

        private void ApplyEquippedRelic()
        {
            string equippedRelicId = RelicSaveData.GetEquippedRelicId();

            if (string.IsNullOrWhiteSpace(equippedRelicId))
            {
                return;
            }

            RelicBlueprint relic = FindRelic(equippedRelicId);

            if (relic == null)
            {
                Debug.LogWarning($"[EquippedRelicApplier] 장착된 유물을 찾지 못했습니다: {equippedRelicId}");
                return;
            }

            ApplyRelicEffect(relic);
        }

        private RelicBlueprint FindRelic(string relicId)
        {
            for (int i = 0; i < relics.Length; i++)
            {
                if (relics[i] != null && relics[i].relicId == relicId)
                {
                    return relics[i];
                }
            }

            return null;
        }

        private void ApplyRelicEffect(RelicBlueprint relic)
        {
            switch (relic.effectType)
            {
                case RelicBlueprint.RelicEffectType.MaxHealth:
                    playerCharacter.AddMaxHealthBonus(relic.effectValue);
                    break;

                case RelicBlueprint.RelicEffectType.MoveSpeed:
                    playerCharacter.AddMoveSpeedBoost(relic.effectValue);
                    break;

                case RelicBlueprint.RelicEffectType.CritChance:
                    playerCharacter.AddCritChance(relic.effectValue);
                    break;
            }

            Debug.Log($"[Relic] {relic.relicName} 적용 완료 / 효과값: {relic.effectValue}");
        }
    }
}