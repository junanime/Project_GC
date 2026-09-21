using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vampire
{
    public class CharacterSelector : MonoBehaviour
    {
        [SerializeField] protected CharacterBlueprint[] characterBlueprints;
        [SerializeField] protected GameObject characterCardPrefab;
        [SerializeField] protected SilverCoinDisplay silverCoinDisplay;

        private CharacterCard[] characterCards;
        private bool startingGame;

        public void Init()
        {
            characterCards = new CharacterCard[characterBlueprints.Length];

            for (int i = 0; i < characterBlueprints.Length; i++)
            {
                characterCards[i] = Instantiate(characterCardPrefab, transform).GetComponent<CharacterCard>();
                characterCards[i].Init(this, characterBlueprints[i], silverCoinDisplay);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());

            for (int i = 0; i < characterBlueprints.Length; i++)
            {
                characterCards[i].UpdateLayout();
            }
        }

        public void StartGame(CharacterBlueprint characterBlueprint)
        {
            if (startingGame || characterBlueprint == null ||
                !LobbyUnlockSave.IsUnlocked("Character", characterBlueprint.name, characterBlueprint.owned)) return;
            startingGame = true;
            CrossSceneData.CharacterBlueprint = characterBlueprint;
            CrossSceneData.StartingLobbyItems = LobbyLoadoutData.ConsumeSelectedCarryItems();

            SceneManager.LoadScene(1);
        }
    }
}
