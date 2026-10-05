using UnityEngine;

namespace Vampire
{
    // Retain scene references; Character.Init owns initialization to avoid duplicate bonuses.
    public class EquippedRelicApplier : MonoBehaviour
    {
        [SerializeField] private Character playerCharacter;
        [SerializeField] private RelicBlueprint[] relics;
        private void Start()
        {
            if (playerCharacter == null) playerCharacter = FindObjectOfType<Character>();
            if (playerCharacter != null) RelicRuntime.Ensure(playerCharacter);
        }
    }
}
