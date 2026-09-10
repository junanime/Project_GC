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


            // 먼저 패널을 활성화
            // ResultCharacterAnimator Coroutine 실행을 위해 필요
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
            // 가장 많이 모은 태그의 대표 시너지
            // =========================

            UpdateBuildName();


            // =========================
            // 가장 피해를 많이 준 증강
            // =========================

            UpdateTopDamageAugment();


            // =========================
            // 선택한 캐릭터
            // =========================

            UpdateSelectedCharacter();
        }


        // =========================================================
        // Build Name / Dominant Synergy
        // =========================================================

        private void UpdateBuildName()
        {
            if (buildNameText == null)
                return;


            if (SynergyManager.Instance == null)
            {
                buildNameText.text = "시너지 없음";

                Debug.LogWarning(
                    "[LevelResultPanel] SynergyManager.Instance를 찾을 수 없습니다."
                );

                return;
            }


            string synergyName =
                SynergyManager.Instance.GetDominantSynergyName();


            if (string.IsNullOrEmpty(synergyName))
            {
                buildNameText.text = "시너지 없음";
            }
            else
            {
                buildNameText.text = synergyName;
            }


            Debug.Log(
                $"[LevelResultPanel] 최종 빌드 : {buildNameText.text}"
            );
        }


        // =========================================================
        // Top Damage Augment
        // =========================================================

        private void UpdateTopDamageAugment()
        {
            if (topDamageAugmentText == null)
                return;


            // AugmentDamageTracker가 씬에 없는 경우
            // 증강 피해 기록이 없다고 보고 기본침 표시
            if (AugmentDamageTracker.Instance == null)
            {
                topDamageAugmentText.text = "기본침";

                Debug.LogWarning(
                    "[LevelResultPanel] AugmentDamageTracker.Instance를 찾을 수 없습니다. 기본침으로 표시합니다."
                );

                return;
            }


            // 가장 피해를 많이 준 증강 이름 / 피해량
            string topAugmentName =
                AugmentDamageTracker.Instance.GetTopDamageAugmentName();

            float topDamage =
                AugmentDamageTracker.Instance.GetTopDamageAmount();


            // 피해를 준 증강이 하나도 없는 경우
            // 기본침은 Tracker에서 제외되어 있으므로 결과창에는 기본침을 표시
            if (topDamage <= 0f ||
                string.IsNullOrWhiteSpace(topAugmentName) ||
                topAugmentName == "-")
            {
                topDamageAugmentText.text = "기본침";

                Debug.Log(
                    "[LevelResultPanel] 피해를 준 증강 없음 → 기본침 표시"
                );

                return;
            }


            // 피해를 준 증강이 있으면 최고 피해 증강 표시
            topDamageAugmentText.text = topAugmentName;


            Debug.Log(
                $"[LevelResultPanel] 최고 피해 증강 : " +
                $"{topDamageAugmentText.text} / " +
                $"누적 피해 : {topDamage:0.##}"
            );
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
                selectedCharacterAnimator.SetCharacter(
                    selectedCharacter
                );
            }
            else
            {
                // Animator가 연결되지 않았다면
                // 첫 번째 걷기 Sprite 사용
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