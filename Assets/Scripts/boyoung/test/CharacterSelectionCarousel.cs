using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Vampire
{
    public class CharacterSelectionCarousel : MonoBehaviour
    {
        public enum CharacterDisplayType
        {
            Sprite2D,
            Model3D
        }

        public enum SlotType
        {
            Left,
            Center,
            Right
        }

        [System.Serializable]
        public class CharacterData
        {
            [Header("기본 정보")]
            public string characterName;

            [TextArea(2, 5)]
            public string description;

            [Header("표시 방식")]
            public CharacterDisplayType displayType;

            [Tooltip("Sprite2D일 때 사용")]
            public Sprite characterSprite;

            [Tooltip("Model3D일 때 사용")]
            public GameObject characterModelPrefab;

            [Header("3D Preview 보정")]
            public Vector3 previewPosition = Vector3.zero;

            public Vector3 previewRotation =
                new Vector3(0f, 180f, 0f);

            public Vector3 previewScale =
                Vector3.one;

            [Header("능력치")]
            [Range(0, 5)]
            public int attack;

            [Range(0, 5)]
            public int defense;

            [Range(0, 5)]
            public int speed;
        }

        [Header("캐릭터 3명")]
        [SerializeField]
        private CharacterData[] characters =
            new CharacterData[3];

        [Header("캐릭터 Presenter")]
        [SerializeField]
        private CharacterPreviewPresenter[] presenters =
            new CharacterPreviewPresenter[3];

        [Header("Carousel 위치")]
        [SerializeField]
        private RectTransform leftAnchor;

        [SerializeField]
        private RectTransform centerAnchor;

        [SerializeField]
        private RectTransform rightAnchor;

        [Header("Carousel 크기")]
        [SerializeField]
        private Vector3 sideScale =
            new Vector3(0.65f, 0.65f, 1f);

        [SerializeField]
        private Vector3 centerScale =
            Vector3.one;

        [Header("Carousel 애니메이션")]
        [SerializeField]
        private float moveDuration = 0.4f;

        [SerializeField]
        private float frontArcHeight = 40f;

        [SerializeField]
        private float backArcHeight = 100f;

        [Header("버튼")]
        [SerializeField]
        private Button previousButton;

        [SerializeField]
        private Button nextButton;

        [SerializeField]
        private Button selectButton;

        [Header("정보 UI")]
        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text descriptionText;

        [SerializeField]
        private Slider attackSlider;

        [SerializeField]
        private Slider defenseSlider;

        [SerializeField]
        private Slider speedSlider;

        private const string MainMenuSceneName = "Main Menu";
        private const string GameSceneName = "Level 1";

        private int currentIndex = 0;
        private bool isAnimating = false;

        public static int SelectedCharacterIndex
        {
            get;
            private set;
        }

        private void Start()
        {
            if (characters.Length != 3 ||
                presenters.Length != 3)
            {
                Debug.LogError(
                    "캐릭터와 Presenter는 각각 정확히 3개가 필요합니다."
                );

                return;
            }

            SetupPresenters();

            SetCarouselImmediate();

            UpdateCharacterInfo();
        }

        private void SetupPresenters()
        {
            for (int i = 0; i < 3; i++)
            {
                if (presenters[i] != null)
                {
                    presenters[i].ShowCharacter(
                        characters[i]
                    );
                }
            }
        }

        public void OnNextClicked()
        {
            if (isAnimating)
                return;

            int newIndex =
                (currentIndex + 1) %
                characters.Length;

            StartCoroutine(
                RotateCarousel(newIndex)
            );
        }

        public void OnPreviousClicked()
        {
            SceneManager.LoadScene("Level 1");
        }

        private IEnumerator RotateCarousel(
            int newCurrentIndex)
        {
            isAnimating = true;

            SetButtonsInteractable(false);

            Vector2[] startPositions =
                new Vector2[3];

            Vector2[] targetPositions =
                new Vector2[3];

            Vector3[] startScales =
                new Vector3[3];

            Vector3[] targetScales =
                new Vector3[3];

            SlotType[] oldSlots =
                new SlotType[3];

            SlotType[] newSlots =
                new SlotType[3];

            for (int i = 0; i < 3; i++)
            {
                RectTransform rect =
                    presenters[i].RectTransform;

                startPositions[i] =
                    rect.anchoredPosition;

                startScales[i] =
                    rect.localScale;

                oldSlots[i] =
                    GetSlotForCharacter(
                        i,
                        currentIndex
                    );

                newSlots[i] =
                    GetSlotForCharacter(
                        i,
                        newCurrentIndex
                    );

                targetPositions[i] =
                    GetAnchorPosition(
                        newSlots[i]
                    );

                targetScales[i] =
                    GetSlotScale(
                        newSlots[i]
                    );
            }

            float elapsed = 0f;

            while (elapsed < moveDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t =
                    Mathf.Clamp01(
                        elapsed / moveDuration
                    );

                float smoothT =
                    t * t * (3f - 2f * t);

                for (int i = 0; i < 3; i++)
                {
                    RectTransform rect =
                        presenters[i].RectTransform;

                    Vector2 pos =
                        Vector2.Lerp(
                            startPositions[i],
                            targetPositions[i],
                            smoothT
                        );

                    bool isBackMove =
                        IsBackRotation(
                            oldSlots[i],
                            newSlots[i]
                        );

                    float arc =
                        Mathf.Sin(
                            Mathf.PI * t
                        );

                    if (isBackMove)
                    {
                        pos.y -=
                            backArcHeight * arc;
                    }
                    else
                    {
                        pos.y +=
                            frontArcHeight * arc;
                    }

                    rect.anchoredPosition = pos;

                    rect.localScale =
                        Vector3.Lerp(
                            startScales[i],
                            targetScales[i],
                            smoothT
                        );
                }

                yield return null;
            }

            currentIndex = newCurrentIndex;

            SetCarouselImmediate();

            UpdateCharacterInfo();

            SetSiblingOrder();

            SetButtonsInteractable(true);

            isAnimating = false;
        }

        private bool IsBackRotation(
            SlotType oldSlot,
            SlotType newSlot)
        {
            return
                (oldSlot == SlotType.Left &&
                 newSlot == SlotType.Right)
                ||
                (oldSlot == SlotType.Right &&
                 newSlot == SlotType.Left);
        }

        private void SetCarouselImmediate()
        {
            for (int i = 0; i < 3; i++)
            {
                SlotType slot =
                    GetSlotForCharacter(
                        i,
                        currentIndex
                    );

                RectTransform rect =
                    presenters[i].RectTransform;

                rect.anchoredPosition =
                    GetAnchorPosition(slot);

                rect.localScale =
                    GetSlotScale(slot);
            }

            SetSiblingOrder();
        }

        private SlotType GetSlotForCharacter(
            int characterIndex,
            int selectedIndex)
        {
            if (characterIndex == selectedIndex)
            {
                return SlotType.Center;
            }

            int leftIndex =
                (selectedIndex - 1 +
                 characters.Length) %
                characters.Length;

            if (characterIndex == leftIndex)
            {
                return SlotType.Left;
            }

            return SlotType.Right;
        }

        private Vector2 GetAnchorPosition(
            SlotType slot)
        {
            switch (slot)
            {
                case SlotType.Left:
                    return leftAnchor.anchoredPosition;

                case SlotType.Center:
                    return centerAnchor.anchoredPosition;

                case SlotType.Right:
                    return rightAnchor.anchoredPosition;
            }

            return Vector2.zero;
        }

        private Vector3 GetSlotScale(
            SlotType slot)
        {
            if (slot == SlotType.Center)
            {
                return centerScale;
            }

            return sideScale;
        }

        private void SetSiblingOrder()
        {
            for (int i = 0; i < 3; i++)
            {
                SlotType slot =
                    GetSlotForCharacter(
                        i,
                        currentIndex
                    );

                if (slot != SlotType.Center)
                {
                    presenters[i].transform
                        .SetAsFirstSibling();
                }
            }

            presenters[currentIndex]
                .transform
                .SetAsLastSibling();
        }

        private void UpdateCharacterInfo()
        {
            CharacterData data =
                characters[currentIndex];

            if (nameText != null)
            {
                nameText.text =
                    data.characterName;
            }

            if (descriptionText != null)
            {
                descriptionText.text =
                    data.description;
            }

            if (attackSlider != null)
            {
                attackSlider.value =
                    data.attack;
            }

            if (defenseSlider != null)
            {
                defenseSlider.value =
                    data.defense;
            }

            if (speedSlider != null)
            {
                speedSlider.value =
                    data.speed;
            }
        }

        private void SetButtonsInteractable(
            bool interactable)
        {
            if (previousButton != null)
            {
                previousButton.interactable =
                    interactable;
            }

            if (nextButton != null)
            {
                nextButton.interactable =
                    interactable;
            }

            if (selectButton != null)
            {
                selectButton.interactable =
                    interactable;
            }
        }

        public void OnBackClicked()
        {
            Debug.Log("뒤로가기 버튼 클릭!");
            SceneManager.LoadScene("Main Menu");
        }

        public void OnSelectClicked()
        {
            SelectedCharacterIndex =
                currentIndex;

            Debug.Log(
                "선택된 캐릭터: " +
                characters[currentIndex]
                    .characterName
            );

            SceneManager.LoadScene(GameSceneName);
        }

        public int GetCurrentCharacterIndex()
        {
            return currentIndex;
        }

        public CharacterData GetCurrentCharacter()
        {
            return characters[currentIndex];
        }
    }
}
