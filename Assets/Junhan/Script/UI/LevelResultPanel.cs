using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Vampire
{
    public class LevelResultPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Game References")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private StatsManager statsManager;

        [Header("Result Texts")]
        [SerializeField] private TextMeshProUGUI survivalTimeText;
        [SerializeField] private TextMeshProUGUI killCountText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI buildNameText;
        [SerializeField] private TextMeshProUGUI topDamageAugmentText;

        [Header("Character Result")]
        [SerializeField] private Image selectedCharacterImage;
        [SerializeField] private TextMeshProUGUI selectedCharacterNameText;
        [SerializeField] private TextMeshProUGUI selectedCharacterDescriptionText;

        [Header("Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Scene")]
        [SerializeField] private int mainMenuSceneIndex = 0;

        private bool initialized;


        private void Awake()
        {
            InitializeIfNeeded();

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }


        private void InitializeIfNeeded()
        {
            if (initialized)
                return;

            initialized = true;

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(Restart);
                retryButton.onClick.AddListener(Restart);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
                mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            }
        }


        public void Open(bool levelPassed)
        {
            InitializeIfNeeded();

            // 결과 데이터 갱신
            UpdateResultData();

            // 게임 정지
            Time.timeScale = 0f;

            // 결과 패널 표시
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }
        }


        private void UpdateResultData()
        {
            // =========================
            // 생존 시간
            // =========================

            if (survivalTimeText != null)
            {
                float time = levelManager != null
                    ? levelManager.CurrentLevelTime
                    : 0f;

                survivalTimeText.text = FormatTime(time);
            }


            // =========================
            // 처치 수
            // =========================

            if (killCountText != null)
            {
                killCountText.text = statsManager != null
                    ? statsManager.MonstersKilled.ToString()
                    : "-";
            }


            // =========================
            // 획득 골드
            // =========================

            if (goldText != null)
            {
                goldText.text = statsManager != null
                    ? statsManager.CoinsGained.ToString()
                    : "-";
            }


            // =========================
            // 플레이어 레벨
            // =========================

            if (levelText != null)
            {
                Character player =
                    levelManager != null
                    ? levelManager.PlayerCharacter
                    : null;

                levelText.text = player != null
                    ? player.CurrentLevel.ToString()
                    : "-";
            }


            // =========================
            // 최종 빌드 이름
            // =========================

            if (buildNameText != null)
            {
                buildNameText.text = "-";
            }


            // =========================
            // 가장 피해를 많이 준 증강
            // =========================

            if (topDamageAugmentText != null)
            {
                topDamageAugmentText.text = "-";
            }


            // =========================
            // 선택한 캐릭터 정보
            // =========================

            UpdateSelectedCharacter();
        }


        private void UpdateSelectedCharacter()
        {
            CharacterBlueprint selectedCharacter =
                CrossSceneData.CharacterBlueprint;


            // =========================
            // Blueprint가 없는 경우
            // =========================

            if (selectedCharacter == null)
            {
                if (selectedCharacterImage != null)
                {
                    selectedCharacterImage.sprite = null;
                    selectedCharacterImage.enabled = false;
                }

                if (selectedCharacterNameText != null)
                {
                    selectedCharacterNameText.text = "-";
                }

                if (selectedCharacterDescriptionText != null)
                {
                    selectedCharacterDescriptionText.text = "-";
                }

                Debug.LogWarning(
                    "[LevelResultPanel] 선택된 CharacterBlueprint이 없습니다."
                );

                return;
            }


            // =========================
            // 캐릭터 이미지
            // =========================

            if (selectedCharacterImage != null)
            {
                if (selectedCharacter.walkSpriteSequence != null &&
                    selectedCharacter.walkSpriteSequence.Length > 0)
                {
                    selectedCharacterImage.sprite =
                        selectedCharacter.walkSpriteSequence[0];

                    selectedCharacterImage.enabled = true;
                    selectedCharacterImage.preserveAspect = true;
                }
                else
                {
                    selectedCharacterImage.sprite = null;
                    selectedCharacterImage.enabled = false;

                    Debug.LogWarning(
                        $"[LevelResultPanel] {selectedCharacter.name}의 " +
                        "walkSpriteSequence가 비어 있습니다."
                    );
                }
            }


            // =========================
            // 캐릭터 이름
            // =========================

            if (selectedCharacterNameText != null)
            {
                selectedCharacterNameText.text =
                    string.IsNullOrEmpty(selectedCharacter.name)
                        ? "-"
                        : selectedCharacter.name;
            }


            // =========================
            // 캐릭터 설명
            // =========================

            if (selectedCharacterDescriptionText != null)
            {
                selectedCharacterDescriptionText.text =
                    string.IsNullOrEmpty(selectedCharacter.description)
                        ? "-"
                        : selectedCharacter.description;
            }
        }


        private string FormatTime(float seconds)
        {
            int totalSeconds =
                Mathf.Max(0, Mathf.FloorToInt(seconds));

            int minutes = totalSeconds / 60;
            int secs = totalSeconds % 60;

            return $"{minutes:00}:{secs:00}";
        }


        public void Close()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }


        public void Restart()
        {
            Time.timeScale = 1f;

            SceneManager.LoadScene(
                SceneManager.GetActiveScene().buildIndex
            );
        }


        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;

            SceneManager.LoadScene(mainMenuSceneIndex);
        }
    }
}