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

        // 결과화면 캐릭터 애니메이션
        [SerializeField] private ResultCharacterAnimator selectedCharacterAnimator;


        [Header("Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button mainMenuButton;


        [Header("Scene")]
        [SerializeField] private int mainMenuSceneIndex = 0;


        private bool initialized;


        // =========================================================
        // Unity
        // =========================================================

        private void Awake()
        {
            InitializeIfNeeded();

            // 게임 시작 시 결과창 숨김
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }


        // =========================================================
        // Initialize
        // =========================================================

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


        // =========================================================
        // Open
        // =========================================================

        public void Open(bool levelPassed)
        {
            InitializeIfNeeded();


            // 게임 정지
            Time.timeScale = 0f;


            // 먼저 패널을 활성화해야
            // ResultCharacterAnimator의 Coroutine이 정상적으로 실행됨
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }


            // 결과 데이터 갱신
            UpdateResultData();
        }


        // =========================================================
        // Result Data
        // =========================================================

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
            // 추후 구현
            // =========================

            if (buildNameText != null)
            {
                buildNameText.text = "-";
            }


            // =========================
            // 가장 피해를 많이 준 증강
            // 추후 구현
            // =========================

            if (topDamageAugmentText != null)
            {
                topDamageAugmentText.text = "-";
            }


            // =========================
            // 선택한 캐릭터
            // =========================

            UpdateSelectedCharacter();
        }


        // =========================================================
        // Selected Character
        // =========================================================

        private void UpdateSelectedCharacter()
        {
            CharacterBlueprint selectedCharacter =
                CrossSceneData.CharacterBlueprint;


            // =====================================================
            // CharacterBlueprint이 없는 경우
            // =====================================================

            if (selectedCharacter == null)
            {
                // 애니메이션 제거
                if (selectedCharacterAnimator != null)
                {
                    selectedCharacterAnimator.Clear();
                }
                else if (selectedCharacterImage != null)
                {
                    selectedCharacterImage.sprite = null;
                    selectedCharacterImage.enabled = false;
                }


                // 이름
                if (selectedCharacterNameText != null)
                {
                    selectedCharacterNameText.text = "-";
                }


                // 설명
                if (selectedCharacterDescriptionText != null)
                {
                    selectedCharacterDescriptionText.text = "-";
                }


                Debug.LogWarning(
                    "[LevelResultPanel] 선택된 CharacterBlueprint이 없습니다."
                );

                return;
            }


            // =====================================================
            // 캐릭터 이미지 + 애니메이션
            // =====================================================

            if (selectedCharacterAnimator != null)
            {
                // ResultCharacterAnimator가
                // 캐릭터에 맞는 애니메이션을 실행
                selectedCharacterAnimator.SetCharacter(
                    selectedCharacter
                );
            }
            else
            {
                // ResultCharacterAnimator가 연결되지 않았다면
                // 기존처럼 정적인 첫 번째 이미지 사용
                SetStaticCharacterImage(selectedCharacter);
            }


            // =====================================================
            // 캐릭터 이름
            // =====================================================

            if (selectedCharacterNameText != null)
            {
                selectedCharacterNameText.text =
                    string.IsNullOrEmpty(selectedCharacter.name)
                        ? "-"
                        : selectedCharacter.name;
            }


            // =====================================================
            // 캐릭터 설명
            // =====================================================

            if (selectedCharacterDescriptionText != null)
            {
                selectedCharacterDescriptionText.text =
                    string.IsNullOrEmpty(selectedCharacter.description)
                        ? "-"
                        : selectedCharacter.description;
            }
        }


        // =========================================================
        // Static Character Image Fallback
        // =========================================================

        private void SetStaticCharacterImage(
            CharacterBlueprint selectedCharacter
        )
        {
            if (selectedCharacterImage == null)
                return;


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


        // =========================================================
        // Time Format
        // =========================================================

        private string FormatTime(float seconds)
        {
            int totalSeconds =
                Mathf.Max(
                    0,
                    Mathf.FloorToInt(seconds)
                );

            int minutes = totalSeconds / 60;
            int secs = totalSeconds % 60;

            return $"{minutes:00}:{secs:00}";
        }


        // =========================================================
        // Close
        // =========================================================

        public void Close()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }


        // =========================================================
        // Retry
        // =========================================================

        public void Restart()
        {
            Time.timeScale = 1f;

            SceneManager.LoadScene(
                SceneManager.GetActiveScene().buildIndex
            );
        }


        // =========================================================
        // Main Menu
        // =========================================================

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;

            SceneManager.LoadScene(mainMenuSceneIndex);
        }
    }
}