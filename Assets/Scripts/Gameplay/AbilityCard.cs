using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Vampire
{
    public class AbilityCard : MonoBehaviour
    {
        [Header("Existing References")]
        [Tooltip("증강 아이콘 Image입니다. 기존 Ability Image 오브젝트를 연결하세요.")]
        [SerializeField] private Image abilityImage;

        [Tooltip("증강 아이콘 크기 계산에 사용하는 RectTransform입니다. 기존 Ability Image Rect를 연결하세요.")]
        [SerializeField] private RectTransform abilityImageRect;

        [Tooltip("증강 이름 텍스트입니다.")]
        [SerializeField] private TextMeshProUGUI nameText;

        [Tooltip("증강 설명 텍스트입니다.")]
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Tooltip("선택 / 강화 버튼 텍스트입니다.")]
        [SerializeField] private TextMeshProUGUI buttonText;

        [Header("Tier Visual Image References")]
        [Tooltip("카드 전체 배경 Image입니다. Ability Card 루트 Image가 아니라 Background Button의 Image를 연결하는 것을 추천합니다.")]
        [SerializeField] private Image cardBackgroundImage;

        [Tooltip("아이콘 액자 Image입니다. Ability Image Background 오브젝트의 Image를 연결하세요.")]
        [SerializeField] private Image iconFrameImage;

        [Tooltip("설명 패널 배경 Image입니다. 없으면 비워둬도 됩니다.")]
        [SerializeField] private Image descriptionBackgroundImage;

        [Tooltip("선택 버튼 배경 Image입니다. Selection Button 오브젝트의 Image를 연결하세요.")]
        [SerializeField] private Image selectionButtonImage;

        [Tooltip("하단 장식 엠블럼 Image입니다. 없으면 비워둬도 됩니다.")]
        [SerializeField] private Image bottomEmblemImage;

        [Header("General Tier Sprites")]
        [Tooltip("일반 증강 카드 배경 스프라이트입니다.")]
        [SerializeField] private Sprite generalCardBackgroundSprite;

        [Tooltip("일반 증강 아이콘 액자 스프라이트입니다.")]
        [SerializeField] private Sprite generalIconFrameSprite;

        [Tooltip("일반 증강 하단 엠블럼 스프라이트입니다. 없으면 비워둬도 됩니다.")]
        [SerializeField] private Sprite generalBottomEmblemSprite;

        [Header("Special Tier Sprites")]
        [Tooltip("특수 증강 카드 배경 스프라이트입니다.")]
        [SerializeField] private Sprite specialCardBackgroundSprite;

        [Tooltip("특수 증강 아이콘 액자 스프라이트입니다.")]
        [SerializeField] private Sprite specialIconFrameSprite;

        [Tooltip("특수 증강 하단 엠블럼 스프라이트입니다. 없으면 비워둬도 됩니다.")]
        [SerializeField] private Sprite specialBottomEmblemSprite;

        [Header("Legendary Tier Sprites")]
        [Tooltip("전설 증강 카드 배경 스프라이트입니다.")]
        [SerializeField] private Sprite legendaryCardBackgroundSprite;

        [Tooltip("전설 증강 아이콘 액자 스프라이트입니다.")]
        [SerializeField] private Sprite legendaryIconFrameSprite;

        [Tooltip("전설 증강 하단 엠블럼 스프라이트입니다. 없으면 비워둬도 됩니다.")]
        [SerializeField] private Sprite legendaryBottomEmblemSprite;

        [Header("Tier Colors")]
        [Tooltip("일반 증강 이름 색상입니다.")]
        [SerializeField] private Color generalNameColor = new Color(0.75f, 0.9f, 1f, 1f);

        [Tooltip("특수 증강 이름 색상입니다.")]
        [SerializeField] private Color specialNameColor = new Color(0.9f, 0.65f, 1f, 1f);

        [Tooltip("전설 증강 이름 색상입니다.")]
        [SerializeField] private Color legendaryNameColor = new Color(1f, 0.82f, 0.35f, 1f);

        [Tooltip("일반 증강 설명 글자 색상입니다.")]
        [SerializeField] private Color generalDescriptionColor = Color.white;

        [Tooltip("특수 증강 설명 글자 색상입니다.")]
        [SerializeField] private Color specialDescriptionColor = Color.white;

        [Tooltip("전설 증강 설명 글자 색상입니다.")]
        [SerializeField] private Color legendaryDescriptionColor = Color.white;

        [Tooltip("일반 증강 선택 버튼 색상입니다.")]
        [SerializeField] private Color generalButtonColor = new Color(0.25f, 0.55f, 0.9f, 1f);

        [Tooltip("특수 증강 선택 버튼 색상입니다.")]
        [SerializeField] private Color specialButtonColor = new Color(0.55f, 0.25f, 0.85f, 1f);

        [Tooltip("전설 증강 선택 버튼 색상입니다.")]
        [SerializeField] private Color legendaryButtonColor = new Color(0.95f, 0.45f, 0.1f, 1f);

        [Header("Animation")]
        [Tooltip("카드가 나타나는 속도입니다.")]
        [SerializeField] private float appearSpeed = 3f;

        [Header("Localization")]
        [SerializeField] private LocalizedString selectLocalization;
        [SerializeField] private LocalizedString upgradeLocalization;

        private AbilitySelectionDialog levelUpMenu;
        private Ability ability;
        private bool initialized;

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        private void HandleLocaleChanged(Locale _)
        {
            SetText();
        }

        private void SetText()
        {
            if (!initialized || ability == null)
            {
                return;
            }

            if (nameText != null)
            {
                nameText.text = ability.Name;
            }

            if (descriptionText != null)
            {
                descriptionText.text = ability.Description;
            }

            if (buttonText != null)
            {
                buttonText.text = !ability.Owned
                    ? selectLocalization.GetLocalizedString()
                    : upgradeLocalization.GetLocalizedString() + " (" + ability.Level + " -> " + (ability.Level + 1) + ")";
            }
        }

        public void Init(AbilitySelectionDialog levelUpMenu, Ability ability, float waitToAppear)
        {
            this.levelUpMenu = levelUpMenu;
            this.ability = ability;

            CacheMissingReferences();
            ApplyTierVisuals(ability.Tier);
            ApplyAbilityIcon();

            StartCoroutine(Appear(waitToAppear));

            initialized = true;
            SetText();
        }

        private void CacheMissingReferences()
        {
            if (cardBackgroundImage == null)
            {
                Transform backgroundButton = transform.Find("Background Button");

                if (backgroundButton != null)
                {
                    cardBackgroundImage = backgroundButton.GetComponent<Image>();
                }
            }

            if (iconFrameImage == null)
            {
                Transform iconFrame = transform.Find("Ability Image Background");

                if (iconFrame != null)
                {
                    iconFrameImage = iconFrame.GetComponent<Image>();
                }
            }

            if (selectionButtonImage == null)
            {
                Transform selectionButton = transform.Find("Selection Button");

                if (selectionButton != null)
                {
                    selectionButtonImage = selectionButton.GetComponent<Image>();
                }
            }

            if (bottomEmblemImage == null)
            {
                Transform bottomEmblem = transform.Find("Bottom Emblem");

                if (bottomEmblem != null)
                {
                    bottomEmblemImage = bottomEmblem.GetComponent<Image>();
                }
            }
        }

        private void ApplyTierVisuals(Ability.AugmentTier tier)
        {
            Sprite cardSprite = null;
            Sprite iconFrameSprite = null;
            Sprite bottomEmblemSprite = null;
            Color nameColor = Color.white;
            Color descriptionColor = Color.white;
            Color buttonColor = Color.white;

            switch (tier)
            {
                case Ability.AugmentTier.Special:
                    cardSprite = specialCardBackgroundSprite;
                    iconFrameSprite = specialIconFrameSprite;
                    bottomEmblemSprite = specialBottomEmblemSprite;
                    nameColor = specialNameColor;
                    descriptionColor = specialDescriptionColor;
                    buttonColor = specialButtonColor;
                    break;

                case Ability.AugmentTier.Legendary:
                    cardSprite = legendaryCardBackgroundSprite;
                    iconFrameSprite = legendaryIconFrameSprite;
                    bottomEmblemSprite = legendaryBottomEmblemSprite;
                    nameColor = legendaryNameColor;
                    descriptionColor = legendaryDescriptionColor;
                    buttonColor = legendaryButtonColor;
                    break;

                default:
                    cardSprite = generalCardBackgroundSprite;
                    iconFrameSprite = generalIconFrameSprite;
                    bottomEmblemSprite = generalBottomEmblemSprite;
                    nameColor = generalNameColor;
                    descriptionColor = generalDescriptionColor;
                    buttonColor = generalButtonColor;
                    break;
            }

            if (cardBackgroundImage != null)
            {
                if (cardSprite != null)
                {
                    cardBackgroundImage.sprite = cardSprite;
                }

                cardBackgroundImage.color = Color.white;
                cardBackgroundImage.enabled = true;
            }

            if (iconFrameImage != null)
            {
                if (iconFrameSprite != null)
                {
                    iconFrameImage.sprite = iconFrameSprite;
                }

                iconFrameImage.color = Color.white;
                iconFrameImage.enabled = true;
            }

            if (bottomEmblemImage != null)
            {
                if (bottomEmblemSprite != null)
                {
                    bottomEmblemImage.sprite = bottomEmblemSprite;
                    bottomEmblemImage.color = Color.white;
                    bottomEmblemImage.enabled = true;
                }
                else
                {
                    bottomEmblemImage.enabled = false;
                }
            }

            if (selectionButtonImage != null)
            {
                selectionButtonImage.color = buttonColor;
            }

            if (nameText != null)
            {
                nameText.color = nameColor;
            }

            if (descriptionText != null)
            {
                descriptionText.color = descriptionColor;
            }

            if (descriptionBackgroundImage != null)
            {
                Color descriptionBackgroundColor = buttonColor;
                descriptionBackgroundColor.a = 0.22f;
                descriptionBackgroundImage.color = descriptionBackgroundColor;
            }
        }

        private void ApplyAbilityIcon()
        {
            if (abilityImage == null || ability == null || ability.Image == null)
            {
                return;
            }

            abilityImage.sprite = ability.Image;
            abilityImage.color = Color.white;
            abilityImage.enabled = true;

            if (abilityImageRect == null)
            {
                return;
            }

            float yHeight = abilityImageRect.rect.height;
            float xWidth = ability.Image.textureRect.width / (float)ability.Image.textureRect.height * yHeight;

            if (xWidth > abilityImageRect.rect.width)
            {
                xWidth = abilityImageRect.rect.width;
                yHeight = ability.Image.textureRect.height / (float)ability.Image.textureRect.width * xWidth;
            }

            ((RectTransform)abilityImage.transform).sizeDelta = new Vector2(xWidth, yHeight);
        }

        public IEnumerator Appear(float waitToAppear)
        {
            Vector3 initialScale = transform.localScale;
            transform.localScale = Vector3.zero;

            yield return new WaitForSecondsRealtime(waitToAppear);

            float t = 0f;

            while (t < 1f)
            {
                transform.localScale = Vector3.LerpUnclamped(Vector3.zero, initialScale, EasingUtils.EaseOutBack(t));
                t += Time.unscaledDeltaTime * appearSpeed;
                yield return null;
            }

            transform.localScale = initialScale;
        }

        public void Selected()
        {
            if (ability == null || levelUpMenu == null)
            {
                return;
            }

            ability.Select();

            AugmentHistoryManager.Instance?.RecordAbility(ability);

            levelUpMenu.Close();
        }
    }
}