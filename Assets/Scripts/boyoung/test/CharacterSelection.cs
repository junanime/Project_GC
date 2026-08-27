using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Vampire
{
    public class CharacterSelection : MonoBehaviour
    {
        public enum CharacterDisplayType
        {
            Sprite2D,
            Model3D
        }

        [System.Serializable]
        public struct CharacterData
        {
            public CharacterBlueprint characterBlueprint;
            public string charName;
            public CharacterDisplayType displayType;

            [Tooltip("Sprite2D 표시에 사용할 이미지")]
            public Sprite charSprite;

            [Tooltip("Model3D 표시에 사용할 프리팹")]
            public GameObject characterPrefab;

            [Header("3D Preview Transform")]
            public Vector3 previewPosition;
            public Vector3 previewRotation;
            public Vector3 previewScale;

            [TextArea]
            public string description;

            [Range(0, 5)] public int attackStat;
            [Range(0, 5)] public int defenseStat;
            [Range(0, 5)] public int speedStat;
        }

        [Header("Character Data")]
        public CharacterData[] characters;

        [Header("Controller")]
        [Tooltip("중복 CharacterSelection 중 실제로 사용할 컴포넌트에서만 활성화합니다.")]
        [SerializeField] private bool useAsPrimaryController = true;

        [Header("2D Preview Images")]
        [SerializeField] private Image leftCharImage;
        public Image centerCharImage;
        [SerializeField] private Image rightCharImage;

        [Header("3D Preview RawImages")]
        [SerializeField] private RawImage leftCharRawImage;
        [SerializeField] private RawImage centerCharRawImage;
        [SerializeField] private RawImage rightCharRawImage;

        [Header("3D Model Roots (Outside Overlay Canvas)")]
        [SerializeField] private Transform leftModelRoot;
        [SerializeField] private Transform centerModelRoot;
        [SerializeField] private Transform rightModelRoot;

        [Header("UI Text")]
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descText;

        [Header("Stats")]
        public Slider attackSlider;
        public Slider defenseSlider;
        public Slider speedSlider;

        [Header("Left / Right Cards")]
        public GameObject leftCard;
        public GameObject rightCard;
        public TextMeshProUGUI leftNameText;
        public TextMeshProUGUI rightNameText;

        [Header("Current Selection")]
        [SerializeField] private int currentIndex = 0;

        [Header("Animation")]
        [SerializeField] private RectTransform leftCardRect;
        [SerializeField] private RectTransform centerCardRect;
        [SerializeField] private RectTransform rightCardRect;
        [SerializeField] private float moveTime = 0.3f;

        private Vector2 leftPos;
        private Vector2 centerPos;
        private Vector2 rightPos;

        private GameObject leftModel;
        private GameObject centerModel;
        private GameObject rightModel;

        private bool isMoving = false;
        private static CharacterSelection primaryController;

        private bool IsPrimaryController => primaryController == this;

        public CharacterBlueprint CurrentCharacterBlueprint
        {
            get
            {
                if (!IsPrimaryController && primaryController != null)
                    return primaryController.CurrentCharacterBlueprint;

                if (characters == null || characters.Length == 0)
                    return null;

                ClampCurrentIndex();
                return characters[currentIndex].characterBlueprint;
            }
        }

        private void LegacyDuplicateWarning()
        {
            CharacterSelection[] selections = FindObjectsOfType<CharacterSelection>(true);
            if (selections.Length > 1)
            {
                Debug.LogWarning(
                    $"[CharacterSelection] 씬에 CharacterSelection이 {selections.Length}개 있습니다. " +
                    "실제로 사용하는 컴포넌트 하나만 남기고 버튼과 MenuManager 참조를 그 컴포넌트에 연결하세요.",
                    this);
            }
        }

        private void Awake()
        {
            if (!useAsPrimaryController)
            {
                enabled = false;
                return;
            }

            if (primaryController != null && primaryController != this)
            {
                Debug.LogWarning(
                    $"[CharacterSelection] '{name}'은(는) 중복 컨트롤러이므로 실행을 중지합니다. " +
                    $"현재 주 컨트롤러는 '{primaryController.name}'입니다. " +
                    "Inspector에서 실제 사용할 하나만 Use As Primary Controller를 켜세요.",
                    this);
                enabled = false;
                return;
            }

            primaryController = this;
        }

        private void OnDestroy()
        {
            if (IsPrimaryController)
                primaryController = null;
        }

        private void Start()
        {
            leftPos = leftCardRect.anchoredPosition;
            centerPos = centerCardRect.anchoredPosition;
            rightPos = rightCardRect.anchoredPosition;

            ClampCurrentIndex();
            UpdateCharacterUI();
        }

        public void OnClickNext()
        {
            if (!IsPrimaryController)
            {
                if (primaryController != null)
                    primaryController.OnClickNext();
                return;
            }

            if (isMoving) return;
            if (characters == null || characters.Length == 0) return;

            PlayNextAnimation();
        }

        public void OnClickPrev()
        {
            if (!IsPrimaryController)
            {
                if (primaryController != null)
                    primaryController.OnClickPrev();
                return;
            }

            if (isMoving) return;
            if (characters == null || characters.Length == 0) return;

            PlayPrevAnimation();
        }

        private void ClampCurrentIndex()
        {
            if (characters == null || characters.Length == 0)
            {
                currentIndex = 0;
                return;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, characters.Length - 1);
        }

        private void UpdateCharacterUI()
        {
            if (characters == null || characters.Length == 0)
                return;

            ClampCurrentIndex();

            CharacterData current = characters[currentIndex];

            if (nameText != null)
                nameText.text = current.charName;

            if (descText != null)
                descText.text = current.description;

            if (attackSlider != null)
                attackSlider.value = current.attackStat;

            if (defenseSlider != null)
                defenseSlider.value = current.defenseStat;

            if (speedSlider != null)
                speedSlider.value = current.speedStat;

            UpdateCharacterPreviews();
        }

        private void UpdateCharacterPreviews()
        {
            if (characters == null || characters.Length == 0)
                return;

            if (characters.Length <= 1)
            {
                if (leftCard != null) leftCard.SetActive(false);
                if (rightCard != null) rightCard.SetActive(false);

                leftModel = UpdatePreviewSlot(
                    default, leftCharImage, leftCharRawImage,
                    leftModelRoot, leftModel, false);
                centerModel = UpdatePreviewSlot(
                    characters[currentIndex], centerCharImage, centerCharRawImage,
                    centerModelRoot, centerModel);
                rightModel = UpdatePreviewSlot(
                    default, rightCharImage, rightCharRawImage,
                    rightModelRoot, rightModel, false);
                return;
            }

            int leftIndex = GetLoopIndex(currentIndex - 1);
            int rightIndex = GetLoopIndex(currentIndex + 1);

            if (leftCard != null)
                leftCard.SetActive(true);

            if (rightCard != null)
                rightCard.SetActive(true);

            if (leftNameText != null)
                leftNameText.text = characters[leftIndex].charName;

            if (rightNameText != null)
                rightNameText.text = characters[rightIndex].charName;

            leftModel = UpdatePreviewSlot(
                characters[leftIndex], leftCharImage, leftCharRawImage,
                leftModelRoot, leftModel);
            centerModel = UpdatePreviewSlot(
                characters[currentIndex], centerCharImage, centerCharRawImage,
                centerModelRoot, centerModel);
            rightModel = UpdatePreviewSlot(
                characters[rightIndex], rightCharImage, rightCharRawImage,
                rightModelRoot, rightModel);
        }

        private int GetLoopIndex(int index)
        {
            if (characters == null || characters.Length == 0)
                return 0;

            if (index < 0)
                return characters.Length - 1;

            if (index >= characters.Length)
                return 0;

            return index;
        }

        private GameObject UpdatePreviewSlot(
            CharacterData character,
            Image spriteImage,
            RawImage modelImage,
            Transform modelRoot,
            GameObject existingModel,
            bool showPreview = true)
        {
            if (existingModel != null)
                Destroy(existingModel);

            bool showSprite = showPreview && character.displayType == CharacterDisplayType.Sprite2D;
            bool showModel = showPreview && character.displayType == CharacterDisplayType.Model3D;

            if (spriteImage != null)
            {
                spriteImage.sprite = showSprite ? character.charSprite : null;
                spriteImage.enabled = showSprite;
                spriteImage.gameObject.SetActive(showSprite);
            }

            if (modelImage != null)
            {
                modelImage.enabled = showModel;
                modelImage.gameObject.SetActive(showModel);
            }

            if (!showModel || character.characterPrefab == null || modelRoot == null)
                return null;

            GameObject model = Instantiate(character.characterPrefab, modelRoot);
            SetLayerRecursively(model, modelRoot.gameObject.layer);

            model.transform.localPosition = character.previewPosition;
            model.transform.localRotation = Quaternion.Euler(character.previewRotation);
            model.transform.localScale = character.previewScale == Vector3.zero
                ? Vector3.one
                : character.previewScale;

            return model;
        }

        private void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;

            foreach (Transform child in target.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private void PlayNextAnimation()
        {
            isMoving = true;

            Sequence seq = DOTween.Sequence();

            seq.Join(leftCardRect.DOAnchorPos(centerPos, moveTime));
            seq.Join(centerCardRect.DOAnchorPos(rightPos, moveTime));
            seq.Join(rightCardRect.DOAnchorPos(leftPos, moveTime));

            seq.OnComplete(() =>
            {
                currentIndex++;

                ResetCardPositions();
                UpdateCharacterUI();

                isMoving = false;
            });
        }

        private void PlayPrevAnimation()
        {
            isMoving = true;

            Sequence seq = DOTween.Sequence();

            seq.Join(leftCardRect.DOAnchorPos(rightPos, moveTime));
            seq.Join(centerCardRect.DOAnchorPos(leftPos, moveTime));
            seq.Join(rightCardRect.DOAnchorPos(centerPos, moveTime));

            seq.OnComplete(() =>
            {
                currentIndex--;

                ResetCardPositions();
                UpdateCharacterUI();

                isMoving = false;
            });
        }

        private void ResetCardPositions()
        {
            leftCardRect.anchoredPosition = leftPos;
            centerCardRect.anchoredPosition = centerPos;
            rightCardRect.anchoredPosition = rightPos;
        }
    }
}
