using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class AbilitySelectionDialog : DialogBox
    {
        [Header("Ability Card References")]
        [Tooltip("증강 카드들이 생성될 부모 Transform입니다.")]
        [SerializeField] private Transform abilityCardsParent;

        [Tooltip("증강 카드 프리팹입니다.")]
        [SerializeField] private GameObject abilityCardPrefab;

        [Header("System References")]
        [Tooltip("게임 일시정지 상태를 관리하는 PauseMenu입니다.")]
        [SerializeField] private PauseMenu pauseMenu;

        [Tooltip("기존 증강 선택창에서 뒤에 흩날리던 파티클 오브젝트입니다. 다이아몬드 비를 없애려면 이 값을 비워두거나 비활성화하면 됩니다.")]
        [SerializeField] private GameObject particles;

        [Tooltip("선택 가능한 증강이 없을 때 대체로 생성할 상자 블루프린트입니다.")]
        [SerializeField] private ChestBlueprint failsafeChestBlueprint;

        [Header("Mini Map Layer Fix")]
        [Tooltip("증강 선택창이 열렸을 때 패널 뒤로 보낼 미니맵 루트 오브젝트입니다. 미니맵 배경, 마커, 테두리를 포함한 최상위 오브젝트를 넣는 것을 추천합니다.")]
        [SerializeField] private GameObject minimapObject;

        [Tooltip("Minimap Object가 비어 있을 때 이름에 Minimap, MiniMap, Mini Map, Mini_Map이 들어간 UI 오브젝트를 자동으로 찾아봅니다.")]
        [SerializeField] private bool autoFindMinimapIfEmpty = true;

        [Tooltip("미니맵 오브젝트에 Canvas가 없으면 런타임에 Canvas를 추가해서 레이어 순서를 강제로 제어합니다.")]
        [SerializeField] private bool addCanvasToMinimapIfMissing = true;

        [Tooltip("증강 선택창이 열렸을 때 미니맵에 적용할 Sorting Order입니다. 낮을수록 뒤로 갑니다.")]
        [SerializeField] private int minimapModalSortingOrder = -100;

        [Header("Card Animation")]
        [Tooltip("카드가 하나씩 등장할 때의 지연 시간입니다.")]
        [SerializeField] private float cardPopupDelay = 0.1f;

        [Header("Reroll Settings")]
        [Tooltip("체크하면 증강 선택창에서 1회 새로고침 버튼을 사용할 수 있습니다.")]
        [SerializeField] private bool enableReroll = true;

        [Tooltip("새로고침 버튼입니다. UI에 만든 Button 오브젝트를 연결하세요.")]
        [SerializeField] private Button rerollButton;

        [Tooltip("새로고침 버튼의 Image 컴포넌트입니다. 버튼 스프라이트 색상/알파를 바꾸는 데 사용합니다.")]
        [SerializeField] private Image rerollButtonImage;

        [Tooltip("새로고침 버튼 안의 TMP 텍스트입니다.")]
        [SerializeField] private TextMeshProUGUI rerollButtonText;

        [Tooltip("새로고침 버튼에 표시할 문구입니다. 사용 후에도 이 문구를 유지합니다.")]
        [SerializeField] private string rerollButtonLabel = "새로고침";

        [Tooltip("새로고침 시 이전 증강과 완전히 다른 조합이 나오도록 몇 번까지 다시 뽑아볼지 정합니다. 후보가 적으면 같은 증강이 다시 나올 수 있습니다.")]
        [SerializeField] private int rerollRetryCount = 10;

        [Header("Reroll Button Visual")]
        [Tooltip("새로고침 버튼이 사용 가능할 때의 이미지 색상입니다. 보통 흰색으로 두면 원본 스프라이트 색상이 그대로 보입니다.")]
        [SerializeField] private Color rerollAvailableImageColor = Color.white;

        [Tooltip("새로고침 버튼을 이미 사용했을 때의 이미지 색상입니다. 검정 반투명으로 두면 비활성화 느낌이 납니다.")]
        [SerializeField] private Color rerollUsedImageColor = new Color(0f, 0f, 0f, 0.55f);

        [Tooltip("새로고침 버튼이 사용 가능할 때의 텍스트 색상입니다.")]
        [SerializeField] private Color rerollAvailableTextColor = Color.white;

        [Tooltip("새로고침 버튼을 이미 사용했을 때의 텍스트 색상입니다.")]
        [SerializeField] private Color rerollUsedTextColor = new Color(1f, 1f, 1f, 0.45f);

        [Tooltip("체크하면 Button 컴포넌트의 Disabled Color도 검정 반투명 색상으로 자동 설정합니다.")]
        [SerializeField] private bool applyDisabledColorToButtonTransition = true;

        [Header("Debug")]
        [Tooltip("체크하면 증강 새로고침 관련 로그를 Console에 출력합니다.")]
        [SerializeField] private bool debugRerollLog = false;

        private AbilityManager abilityManager;
        private EntityManager entityManager;
        private Character playerCharacter;
        private List<AbilityCard> abilityCards;
        private List<Ability> displayedAbilities;

        private bool menuOpen = false;
        private bool rerollUsed = false;

        private Canvas minimapCanvas;
        private CanvasGroup minimapCanvasGroup;
        private Transform minimapOriginalParent;
        private int minimapOriginalSiblingIndex;
        private bool minimapOriginalOverrideSorting;
        private int minimapOriginalSortingOrder;
        private bool minimapOriginalCanvasCached = false;
        private bool minimapLayerMovedForModal = false;

        public bool MenuOpen
        {
            get => menuOpen;
        }

        public void Init(AbilityManager abilityManager, EntityManager entityManager, Character playerCharacter)
        {
            this.abilityManager = abilityManager;
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;
        }

        private void Awake()
        {
            SetupRerollButton();
        }

        private void OnEnable()
        {
            SetupRerollButton();
            UpdateRerollButtonState();
        }

        private void SetupRerollButton()
        {
            if (rerollButton == null)
            {
                return;
            }

            if (rerollButtonImage == null)
            {
                rerollButtonImage = rerollButton.GetComponent<Image>();
            }

            if (rerollButtonText == null)
            {
                rerollButtonText = rerollButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            rerollButton.onClick.RemoveListener(RerollAbilities);
            rerollButton.onClick.AddListener(RerollAbilities);

            ApplyRerollButtonColorBlock();
        }

        private void ApplyRerollButtonColorBlock()
        {
            if (rerollButton == null || !applyDisabledColorToButtonTransition)
            {
                return;
            }

            ColorBlock colors = rerollButton.colors;
            colors.normalColor = rerollAvailableImageColor;
            colors.highlightedColor = rerollAvailableImageColor;
            colors.selectedColor = rerollAvailableImageColor;
            colors.pressedColor = new Color(
                rerollAvailableImageColor.r * 0.85f,
                rerollAvailableImageColor.g * 0.85f,
                rerollAvailableImageColor.b * 0.85f,
                rerollAvailableImageColor.a
            );
            colors.disabledColor = rerollUsedImageColor;
            colors.colorMultiplier = 1f;

            rerollButton.colors = colors;
        }

        public void Open(bool failsafe = true)
        {
            base.Open();

            transform.SetAsLastSibling();
            ApplyMinimapBehindModal();

            menuOpen = true;
            rerollUsed = false;
            Time.timeScale = 0;

            if (pauseMenu != null)
            {
                pauseMenu.TimeIsFrozen = true;
            }

            if (particles != null)
            {
                particles.SetActive(true);
            }

            UpdateRerollButtonState();

            displayedAbilities = abilityManager.SelectAbilities();

            if (displayedAbilities.Count > 0)
            {
                // 실제 선택 가능한 증강 카드가 존재하는 경우에만
                // 현재 BGM을 Pause하고 증강 선택 전용 BGM을 시작합니다.
                GameAudioManager.EnterAugmentSelectionAudio();
                Populate(displayedAbilities);
            }
            else
            {
                if (failsafe && entityManager != null && playerCharacter != null)
                {
                    entityManager.SpawnChest(
                        failsafeChestBlueprint,
                        (Vector2)playerCharacter.transform.position + Vector2.up
                    );
                }

                Close();
            }
        }

        private void Populate(List<Ability> abilities)
        {
            if (abilityCards == null)
            {
                abilityCards = new List<AbilityCard>();
            }

            int i = 0;

            for (; i < abilities.Count; i++)
            {
                if (i >= abilityCards.Count)
                {
                    AbilityCard newCard = Instantiate(abilityCardPrefab, abilityCardsParent).GetComponent<AbilityCard>();
                    abilityCards.Add(newCard);
                }

                abilityCards[i].Init(this, abilities[i], cardPopupDelay * i);
                abilityCards[i].gameObject.SetActive(true);
            }

            for (; i < abilityCards.Count; i++)
            {
                abilityCards[i].gameObject.SetActive(false);
            }
        }

        public void RerollAbilities()
        {
            if (!enableReroll)
            {
                return;
            }

            if (!menuOpen)
            {
                return;
            }

            if (rerollUsed)
            {
                return;
            }

            if (abilityManager == null)
            {
                return;
            }

            if (displayedAbilities == null || displayedAbilities.Count <= 0)
            {
                return;
            }

            List<Ability> previousAbilities = new List<Ability>(displayedAbilities);

            abilityManager.ReturnAbilities(displayedAbilities);
            displayedAbilities = null;

            List<Ability> newAbilities = SelectRerolledAbilities(previousAbilities);

            if (newAbilities == null || newAbilities.Count <= 0)
            {
                displayedAbilities = abilityManager.SelectAbilities();

                if (displayedAbilities == null || displayedAbilities.Count <= 0)
                {
                    if (debugRerollLog)
                    {
                        Debug.LogWarning("[AbilitySelectionDialog] 새로고침할 수 있는 증강이 없습니다.");
                    }

                    displayedAbilities = previousAbilities;
                    Populate(displayedAbilities);

                    rerollUsed = true;
                    UpdateRerollButtonState();

                    return;
                }
            }
            else
            {
                displayedAbilities = newAbilities;
            }

            rerollUsed = true;
            UpdateRerollButtonState();
            Populate(displayedAbilities);

            if (debugRerollLog)
            {
                Debug.Log("[AbilitySelectionDialog] 증강 새로고침 완료");
            }
        }

        private List<Ability> SelectRerolledAbilities(List<Ability> previousAbilities)
        {
            int retryCount = Mathf.Max(1, rerollRetryCount);

            List<Ability> bestSelection = null;
            int bestDifferentCount = -1;

            for (int i = 0; i < retryCount; i++)
            {
                List<Ability> candidate = abilityManager.SelectAbilities();

                if (candidate == null || candidate.Count <= 0)
                {
                    continue;
                }

                int differentCount = CountDifferentAbilities(candidate, previousAbilities);

                if (differentCount > bestDifferentCount)
                {
                    if (bestSelection != null)
                    {
                        abilityManager.ReturnAbilities(bestSelection);
                    }

                    bestSelection = candidate;
                    bestDifferentCount = differentCount;
                }
                else
                {
                    abilityManager.ReturnAbilities(candidate);
                }

                if (differentCount >= candidate.Count)
                {
                    break;
                }
            }

            return bestSelection;
        }

        private int CountDifferentAbilities(List<Ability> newAbilities, List<Ability> previousAbilities)
        {
            if (newAbilities == null)
            {
                return 0;
            }

            if (previousAbilities == null || previousAbilities.Count <= 0)
            {
                return newAbilities.Count;
            }

            int differentCount = 0;

            for (int i = 0; i < newAbilities.Count; i++)
            {
                if (!previousAbilities.Contains(newAbilities[i]))
                {
                    differentCount++;
                }
            }

            return differentCount;
        }

        private void UpdateRerollButtonState()
        {
            if (rerollButton == null)
            {
                return;
            }

            bool shouldShow = enableReroll && menuOpen;
            rerollButton.gameObject.SetActive(shouldShow);

            if (!shouldShow)
            {
                return;
            }

            bool canUseReroll = !rerollUsed;
            rerollButton.interactable = canUseReroll;

            if (rerollButtonImage != null)
            {
                rerollButtonImage.color = canUseReroll
                    ? rerollAvailableImageColor
                    : rerollUsedImageColor;
            }

            if (rerollButtonText != null)
            {
                rerollButtonText.text = rerollButtonLabel;
                rerollButtonText.color = canUseReroll
                    ? rerollAvailableTextColor
                    : rerollUsedTextColor;
            }

            ApplyRerollButtonColorBlock();
        }

        public override void Close()
        {
            // 증강 선택 BGM이 재생 중이었다면 종료하고,
            // 증강창이 열리기 직전에 재생 중이던 BGM을 이어서 재생합니다.
            GameAudioManager.ExitAugmentSelectionAudio();

            if (displayedAbilities != null)
            {
                abilityManager.ReturnAbilities(displayedAbilities);
                displayedAbilities = null;
            }

            RestoreMinimapLayer();

            menuOpen = false;
            Time.timeScale = 1;

            if (pauseMenu != null)
            {
                pauseMenu.TimeIsFrozen = false;
            }

            if (particles != null)
            {
                particles.SetActive(false);
            }

            UpdateRerollButtonState();

            base.Close();
        }

        public bool HasAvailableAbilities()
        {
            return abilityManager.HasAvailableAbilities();
        }

        private void ApplyMinimapBehindModal()
        {
            ResolveMinimapObject();

            if (minimapObject == null)
            {
                return;
            }

            minimapObject.SetActive(true);

            if (!minimapLayerMovedForModal)
            {
                minimapOriginalParent = minimapObject.transform.parent;
                minimapOriginalSiblingIndex = minimapObject.transform.GetSiblingIndex();
                minimapLayerMovedForModal = true;
            }

            if (minimapOriginalParent != null)
            {
                minimapObject.transform.SetAsFirstSibling();
            }

            PrepareMinimapCanvas();

            if (minimapCanvas != null)
            {
                minimapCanvas.overrideSorting = true;
                minimapCanvas.sortingOrder = minimapModalSortingOrder;
            }

            if (minimapCanvasGroup != null)
            {
                minimapCanvasGroup.interactable = false;
                minimapCanvasGroup.blocksRaycasts = false;
            }
        }

        private void RestoreMinimapLayer()
        {
            if (minimapObject == null)
            {
                return;
            }

            if (minimapLayerMovedForModal && minimapOriginalParent != null && minimapObject.transform.parent == minimapOriginalParent)
            {
                int safeIndex = Mathf.Clamp(minimapOriginalSiblingIndex, 0, minimapOriginalParent.childCount - 1);
                minimapObject.transform.SetSiblingIndex(safeIndex);
            }

            if (minimapCanvas != null && minimapOriginalCanvasCached)
            {
                minimapCanvas.overrideSorting = minimapOriginalOverrideSorting;
                minimapCanvas.sortingOrder = minimapOriginalSortingOrder;
            }

            if (minimapCanvasGroup != null)
            {
                minimapCanvasGroup.interactable = true;
                minimapCanvasGroup.blocksRaycasts = false;
            }

            minimapLayerMovedForModal = false;
        }

        private void ResolveMinimapObject()
        {
            if (minimapObject != null || !autoFindMinimapIfEmpty)
            {
                return;
            }

            RectTransform[] rectTransforms = FindObjectsOfType<RectTransform>(true);

            for (int i = 0; i < rectTransforms.Length; i++)
            {
                string lowerName = rectTransforms[i].gameObject.name.ToLower();

                if (lowerName.Contains("minimap") ||
                    lowerName.Contains("mini_map") ||
                    lowerName.Contains("mini map"))
                {
                    minimapObject = rectTransforms[i].gameObject;
                    return;
                }
            }
        }

        private void PrepareMinimapCanvas()
        {
            if (minimapObject == null)
            {
                return;
            }

            if (minimapCanvas == null)
            {
                minimapCanvas = minimapObject.GetComponent<Canvas>();

                if (minimapCanvas == null && addCanvasToMinimapIfMissing)
                {
                    minimapCanvas = minimapObject.AddComponent<Canvas>();
                }
            }

            if (minimapCanvas != null && !minimapOriginalCanvasCached)
            {
                minimapOriginalOverrideSorting = minimapCanvas.overrideSorting;
                minimapOriginalSortingOrder = minimapCanvas.sortingOrder;
                minimapOriginalCanvasCached = true;
            }

            if (minimapCanvasGroup == null)
            {
                minimapCanvasGroup = minimapObject.GetComponent<CanvasGroup>();

                if (minimapCanvasGroup == null)
                {
                    minimapCanvasGroup = minimapObject.AddComponent<CanvasGroup>();
                }
            }
        }
    }
}