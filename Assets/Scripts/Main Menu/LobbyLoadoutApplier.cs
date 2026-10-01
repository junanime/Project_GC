using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class LobbyLoadoutApplier : MonoBehaviour
    {
        [SerializeField] private bool debugLog = true;

        private IEnumerator Start()
        {
            // LevelManager, Character.Init, AbilityManager.Init이 먼저 끝나도록 대기
            yield return null;
            yield return null;

            Character player = FindObjectOfType<Character>();
            AbilityManager abilityManager = FindObjectOfType<AbilityManager>();

            if (player == null)
            {
                Debug.LogWarning("[LobbyLoadoutApplier] Player Character를 찾지 못했습니다.");
                yield break;
            }

            StartingNeedleSelection.Apply(abilityManager);
            MerchantItemBlueprint[] items = CrossSceneData.StartingLobbyItems;

            if (items == null || items.Length == 0)
            {
                if (debugLog)
                {
                    Debug.Log("[LobbyLoadoutApplier] 적용할 로비 시작 아이템이 없습니다.");
                }

                yield break;
            }

            for (int i = 0; i < items.Length; i++)
            {
                MerchantItemBlueprint item = items[i];

                if (item == null)
                {
                    continue;
                }

                MerchantItemEffectUtility.ApplyToCharacter(item, player, abilityManager, debugLog);
            }

            CrossSceneData.ClearStartingLobbyItems();
        }
    }
}