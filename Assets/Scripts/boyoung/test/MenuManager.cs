using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Screen Objects")]
    public GameObject startScreen;
    public GameObject characterSelectScreen;

    [Header("Game Start")]
    [SerializeField] private Vampire.CharacterSelection characterSelection;
    [SerializeField] private int gameSceneBuildIndex = 1;
    [SerializeField] private Vampire.CharacterBlueprint fallbackCharacterBlueprint;

    public void ClickGameStart()
    {
        if (startScreen != null)
            startScreen.SetActive(false);

        if (characterSelectScreen != null)
            characterSelectScreen.SetActive(true);
    }

    public void ClickBack()
    {
        if (characterSelectScreen != null)
            characterSelectScreen.SetActive(false);

        if (startScreen != null)
            startScreen.SetActive(true);
    }

    public void ClickSelectAndPlay()
    {
        Vampire.CharacterBlueprint selectedCharacter = characterSelection != null
            ? characterSelection.CurrentCharacterBlueprint
            : null;

        if (selectedCharacter == null)
            selectedCharacter = fallbackCharacterBlueprint;

        if (selectedCharacter == null)
        {
            Debug.LogWarning("[MenuManager] CharacterBlueprint is not assigned. Cannot start the game.");
            return;
        }

        Vampire.CrossSceneData.CharacterBlueprint = selectedCharacter;

        // 로비에서 구매한 아이템을 인게임으로 전달
        Vampire.CrossSceneData.StartingLobbyItems =
            Vampire.LobbyLoadoutData.ConsumeSelectedCarryItems();

        Debug.Log($"Character selected: {selectedCharacter.name}. Starting game.");
        SceneManager.LoadScene(gameSceneBuildIndex);
    }
}
