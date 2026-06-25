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
            CrossSceneData.StartingLobbyItems = LobbyLoadoutData.ConsumeSelectedCarryItems();
            CrossSceneData.CharacterBlueprint = characterBlueprint;
            CrossSceneData.StartingLobbyItems = LobbyLoadoutData.ConsumeSelectedCarryItems();

            SceneManager.LoadScene(1);
        }
    }
}