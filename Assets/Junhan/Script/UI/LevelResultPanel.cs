using System.Collections.Generic;
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


        [Header("Killed By Monster")]
        [SerializeField] private GameObject killedByMonsterRoot;
        [SerializeField] private Image killedByMonsterImage;
        [SerializeField] private TextMeshProUGUI killedByMonsterNameText;
        [SerializeField] private TextMeshProUGUI killedByMonsterDescriptionText;


        [Header("Acquired Augments")]
        [Tooltip("1~4번째 증강 이미지가 생성될 왼쪽 Grid입니다.")]
        [SerializeField] private Transform acquiredAugmentGridLeft;

        [Tooltip("5~8번째 증강 이미지가 생성될 오른쪽 Grid입니다.")]
        [SerializeField] private Transform acquiredAugmentGridRight;

        [Tooltip("이전 증강 페이지 버튼(<)")]
        [SerializeField] private Button previousAugmentPageButton;

        [Tooltip("다음 증강 페이지 버튼(>)")]
        [SerializeField] private Button nextAugmentPageButton;

        [Tooltip("결과 화면에서 사용할 증강 최대 페이지 수입니다. 페이지당 8개가 표시됩니다.")]
        [Min(1)]
        [SerializeField] private int maxAugmentPages = 2;


        [Header("Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button mainMenuButton;


        [Header("Scene")]
        [SerializeField] private int mainMenuSceneIndex = 0;


        private bool initialized;
        private bool currentLevelPassed;

        private const int AugmentsPerSide = 4;
        private const int AugmentsPerPage = 8;

        private int currentAugmentPage = 0;

        private IReadOnlyList<AugmentHistoryManager.AugmentEntry> currentAugmentEntries;

        private readonly List<Image> spawnedAugmentImages =
            new List<Image>();


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

            // 나를 죽인 몬스터 UI도 게임 시작 시 확실하게 숨김
            if (killedByMonsterRoot != null)
            {
                killedByMonsterRoot.SetActive(false);
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


            if (previousAugmentPageButton != null)
            {
                previousAugmentPageButton.onClick.RemoveListener(PreviousAugmentPage);
                previousAugmentPageButton.onClick.AddListener(PreviousAugmentPage);
            }


            if (nextAugmentPageButton != null)
            {
                nextAugmentPageButton.onClick.RemoveListener(NextAugmentPage);
                nextAugmentPageButton.onClick.AddListener(NextAugmentPage);
            }
        }


        // =========================================================
        // Open
        // =========================================================

        public void Open(bool levelPassed)
        {
            InitializeIfNeeded();

            currentLevelPassed = levelPassed;


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


            // =========================
            // 나를 죽인 몬스터
            // =========================

            UpdateKilledByMonster();


            // =========================
            // 이번 판에 획득한 증강
            // =========================

            UpdateAcquiredAugments();
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
        // Killed By Monster
        // =========================================================

        private void UpdateKilledByMonster()
        {
            // 스테이지 클리어 등 몬스터에게 죽은 상황이 아니면 X 표시
            if (currentLevelPassed)
            {
                ShowNoKillerMonsterUI();

                Debug.Log(
                    "[LevelResultPanel] 스테이지 클리어 상태 → 나를 죽인 몬스터 X 표시"
                );

                return;
            }


            Character player =
                levelManager != null
                    ? levelManager.PlayerCharacter
                    : null;


            if (player == null)
            {
                ShowNoKillerMonsterUI();

                Debug.LogWarning(
                    "[LevelResultPanel] PlayerCharacter를 찾을 수 없습니다. 나를 죽인 몬스터를 X로 표시합니다."
                );

                return;
            }


            MonsterBlueprint killer =
                player.LastDamageMonsterBlueprint;


            // 환경 피해, 이벤트 피해 등 몬스터가 아닌 원인으로 죽은 경우
            if (killer == null)
            {
                ShowNoKillerMonsterUI();

                Debug.Log(
                    "[LevelResultPanel] 마지막 피해 원인이 몬스터가 아닙니다. X 표시"
                );

                return;
            }


            // 실제 몬스터에게 죽었을 때
            if (killedByMonsterRoot != null)
            {
                killedByMonsterRoot.SetActive(true);
            }


            // =========================
            // 몬스터 이미지
            // =========================

            Sprite monsterSprite =
                killer.resultSprite;


            // 결과용 전용 Sprite가 없으면 일반 걷기 Sprite 첫 프레임 사용
            if (monsterSprite == null &&
                killer.walkSpriteSequence != null &&
                killer.walkSpriteSequence.Length > 0)
            {
                monsterSprite =
                    killer.walkSpriteSequence[0];
            }


            // 엘리트 몬스터 별도 애니메이션 fallback
            if (monsterSprite == null &&
                killer is EliteMonsterBlueprint eliteBlueprint)
            {
                Sprite[] eliteSprites =
                    eliteBlueprint.GetEffectiveWalkSpriteSequence();

                if (eliteSprites != null &&
                    eliteSprites.Length > 0)
                {
                    monsterSprite =
                        eliteSprites[0];
                }
            }


            if (killedByMonsterImage != null)
            {
                killedByMonsterImage.sprite =
                    monsterSprite;

                killedByMonsterImage.enabled =
                    monsterSprite != null;

                killedByMonsterImage.preserveAspect =
                    true;
            }


            // =========================
            // 몬스터 이름
            // =========================

            if (killedByMonsterNameText != null)
            {
                killedByMonsterNameText.text =
                    string.IsNullOrWhiteSpace(killer.name)
                        ? "-"
                        : killer.name;
            }


            // =========================
            // 몬스터 특징 / 설명
            // =========================

            if (killedByMonsterDescriptionText != null)
            {
                killedByMonsterDescriptionText.text =
                    string.IsNullOrWhiteSpace(killer.description)
                        ? "-"
                        : killer.description;
            }


            Debug.Log(
                $"[LevelResultPanel] 나를 죽인 몬스터 : " +
                $"{(string.IsNullOrWhiteSpace(killer.name) ? "-" : killer.name)}"
            );
        }


        private void ShowNoKillerMonsterUI()
        {
            // 몬스터가 없어도 영역 자체는 보여주고 X를 표시합니다.
            if (killedByMonsterRoot != null)
            {
                killedByMonsterRoot.SetActive(true);
            }


            if (killedByMonsterImage != null)
            {
                killedByMonsterImage.sprite = null;
                killedByMonsterImage.enabled = false;
            }


            if (killedByMonsterNameText != null)
            {
                killedByMonsterNameText.text = "X";
            }


            if (killedByMonsterDescriptionText != null)
            {
                killedByMonsterDescriptionText.text = "";
            }
        }


        private void ClearKilledByMonsterUI()
        {
            if (killedByMonsterRoot != null)
            {
                killedByMonsterRoot.SetActive(false);
            }


            if (killedByMonsterImage != null)
            {
                killedByMonsterImage.sprite = null;
                killedByMonsterImage.enabled = false;
            }


            if (killedByMonsterNameText != null)
            {
                killedByMonsterNameText.text = "";
            }


            if (killedByMonsterDescriptionText != null)
            {
                killedByMonsterDescriptionText.text = "";
            }
        }


        // =========================================================
        // Acquired Augments
        // =========================================================

        private void UpdateAcquiredAugments()
        {
            ClearAcquiredAugments();

            currentAugmentPage = 0;
            currentAugmentEntries = null;


            if (acquiredAugmentGridLeft == null)
            {
                Debug.LogWarning(
                    "[LevelResultPanel] Acquired Augment Grid Left가 연결되어 있지 않습니다."
                );

                UpdateAugmentPageButtons();
                return;
            }


            if (acquiredAugmentGridRight == null)
            {
                Debug.LogWarning(
                    "[LevelResultPanel] Acquired Augment Grid Right가 연결되어 있지 않습니다."
                );

                UpdateAugmentPageButtons();
                return;
            }


            if (AugmentHistoryManager.Instance == null)
            {
                Debug.LogWarning(
                    "[LevelResultPanel] AugmentHistoryManager.Instance를 찾을 수 없습니다."
                );

                UpdateAugmentPageButtons();
                return;
            }


            currentAugmentEntries =
                AugmentHistoryManager.Instance.Entries;


            if (currentAugmentEntries == null ||
                currentAugmentEntries.Count == 0)
            {
                Debug.Log(
                    "[LevelResultPanel] 이번 판에 획득한 증강이 없습니다."
                );

                UpdateAugmentPageButtons();
                return;
            }


            RefreshAugmentPage();
        }


        private void RefreshAugmentPage()
        {
            ClearAcquiredAugments();


            int totalPages =
                GetTotalAugmentPages();


            currentAugmentPage =
                Mathf.Clamp(
                    currentAugmentPage,
                    0,
                    Mathf.Max(0, totalPages - 1)
                );


            // 획득한 증강이 없어도 Inspector에서 설정한 페이지 이동은 유지합니다.
            if (currentAugmentEntries == null ||
                currentAugmentEntries.Count == 0)
            {
                UpdateAugmentPageButtons();

                Debug.Log(
                    $"[LevelResultPanel] 증강 페이지 표시 완료 : " +
                    $"{currentAugmentPage + 1} / {totalPages} " +
                    "(획득한 증강 없음)"
                );

                return;
            }


            currentAugmentPage =
                Mathf.Clamp(
                    currentAugmentPage,
                    0,
                    Mathf.Max(0, totalPages - 1)
                );


            int startIndex =
                currentAugmentPage * AugmentsPerPage;

            int endIndex =
                Mathf.Min(
                    startIndex + AugmentsPerPage,
                    currentAugmentEntries.Count
                );


            for (int entryIndex = startIndex;
                 entryIndex < endIndex;
                 entryIndex++)
            {
                AugmentHistoryManager.AugmentEntry entry =
                    currentAugmentEntries[entryIndex];

                if (entry == null || entry.icon == null)
                {
                    continue;
                }


                int localIndex =
                    entryIndex - startIndex;


                Transform targetGrid =
                    localIndex < AugmentsPerSide
                        ? acquiredAugmentGridLeft
                        : acquiredAugmentGridRight;


                GameObject imageObject =
                    new GameObject(
                        $"ResultAugmentImage_{entryIndex + 1:00}",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image)
                    );


                imageObject.transform.SetParent(
                    targetGrid,
                    false
                );


                Image spawnedImage =
                    imageObject.GetComponent<Image>();


                spawnedImage.sprite =
                    entry.icon;

                spawnedImage.preserveAspect =
                    true;

                spawnedImage.raycastTarget =
                    false;

                spawnedImage.enabled =
                    true;


                spawnedAugmentImages.Add(
                    spawnedImage
                );
            }


            UpdateAugmentPageButtons();


            Debug.Log(
                $"[LevelResultPanel] 증강 페이지 표시 완료 : " +
                $"{currentAugmentPage + 1} / {totalPages} " +
                $"(표시 {startIndex + 1} ~ {endIndex})"
            );
        }


        private void PreviousAugmentPage()
        {
            if (currentAugmentPage <= 0)
                return;


            currentAugmentPage--;

            RefreshAugmentPage();
        }


        private void NextAugmentPage()
        {
            int totalPages =
                GetTotalAugmentPages();


            if (currentAugmentPage >= totalPages - 1)
                return;


            currentAugmentPage++;

            RefreshAugmentPage();
        }


        private void UpdateAugmentPageButtons()
        {
            if (previousAugmentPageButton != null)
            {
                previousAugmentPageButton.interactable = true;
            }

            if (nextAugmentPageButton != null)
            {
                nextAugmentPageButton.interactable = true;
            }
        }


        private int GetTotalAugmentPages()
        {
            // Inspector의 Max Augment Pages 값을 실제 페이지 수로 사용합니다.
            // 획득한 증강 개수와 관계없이 이 페이지 수까지 이동할 수 있습니다.
            return Mathf.Max(
                1,
                maxAugmentPages
            );
        }


        private void ClearAcquiredAugments()
        {
            for (int i = spawnedAugmentImages.Count - 1;
                 i >= 0;
                 i--)
            {
                Image image =
                    spawnedAugmentImages[i];

                if (image != null)
                {
                    image.gameObject.SetActive(false);

                    Destroy(
                        image.gameObject
                    );
                }
            }


            spawnedAugmentImages.Clear();
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
            ClearAcquiredAugments();

            if (killedByMonsterRoot != null)
            {
                killedByMonsterRoot.SetActive(false);
            }

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