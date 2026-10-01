using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;

namespace Vampire
{
    public class CharacterCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image characterImage;
        [SerializeField] private RectTransform characterImageRect;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private TextMeshProUGUI mvspdText;
        [SerializeField] private TextMeshProUGUI luckText;
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private LocalizedString buyLocalization, selectLocalization;
        [SerializeField] private Image buttonImage;
        [SerializeField] private Color selectColor, buyColor;
        [SerializeField] private RectTransform startingAbilitiesParent;
        [SerializeField] private GameObject startingAbilityContainerPrefab;
        [SerializeField] private Vector2 startingAbilitiesRectSize = new Vector2(365, 85);
        private CharacterSelector characterSelector;
        private CharacterBlueprint characterBlueprint;
        private SilverCoinDisplay silverCoinDisplay;
        private StartingAbilityContainer[] startingAbilityContainers;
        private bool initialized;

        private void OnEnable()
        {
            buyLocalization.StringChanged += OnLocalizationChanged;
            selectLocalization.StringChanged += OnLocalizationChanged;
        }

        private void OnDisable()
        {
            buyLocalization.StringChanged -= OnLocalizationChanged;
            selectLocalization.StringChanged -= OnLocalizationChanged;
        }

        private void OnLocalizationChanged(string value)
        {
            UpdateButtonText();
        }

        public void Init(CharacterSelector characterSelector, CharacterBlueprint characterBlueprint, SilverCoinDisplay silverCoinDisplay)
        {
            this.characterSelector = characterSelector;
            this.characterBlueprint = characterBlueprint;
            this.silverCoinDisplay = silverCoinDisplay;

            characterImage.sprite = ApothecaryUI.CharacterProfile(characterBlueprint);
            nameText.text = characterBlueprint.name.ToString();
            hpText.text = characterBlueprint.hp.ToString();
            armorText.text = characterBlueprint.armor.ToString();
            mvspdText.text = Mathf.RoundToInt(characterBlueprint.movespeed / 1.15f * 100f).ToString() + "%";
            luckText.text = characterBlueprint.luck.ToString();

            UpdateButtonText();
            buttonImage.color = IsOwned() ? selectColor : buyColor;

            startingAbilityContainers = new StartingAbilityContainer[characterBlueprint.startingAbilities.Length];

            for (int i = 0; i < characterBlueprint.startingAbilities.Length; i++)
            {
                startingAbilityContainers[i] = Instantiate(startingAbilityContainerPrefab, startingAbilitiesParent).GetComponent<StartingAbilityContainer>();
                startingAbilityContainers[i].AbilityImage.sprite = characterBlueprint.startingAbilities[i].GetComponent<Ability>().Image;
            }

            initialized = true;
        }
        private bool IsOwned()
        {
            return LobbyUnlockSave.IsUnlocked("Character", characterBlueprint.name, characterBlueprint.owned);
        }

        private string GetCharacterUnlockId()
        {
            return characterBlueprint.name;
        }

        public void UpdateLayout()
        {
            // Character image layout
            float yHeight = Mathf.Abs(characterImageRect.sizeDelta.y);
            float xWidth = characterImage.sprite.rect.width / characterImage.sprite.rect.height * yHeight;
            if (xWidth > Mathf.Abs(characterImageRect.sizeDelta.x))
            {
                xWidth = Mathf.Abs(characterImageRect.sizeDelta.x);
                yHeight = characterBlueprint.walkSpriteSequence[0].textureRect.height / (float) characterBlueprint.walkSpriteSequence[0].textureRect.width * xWidth;
            }
            ((RectTransform)characterImage.transform).sizeDelta = new Vector2(xWidth, yHeight);
            
            // Character abilities layout
            float maxImageWidth = startingAbilitiesRectSize.x / startingAbilityContainers.Length;
            for (int i = 0; i < startingAbilityContainers.Length; i++)
            {
                StartingAbilityContainer startingAbilityContainer = startingAbilityContainers[i];
                float imageHeight = startingAbilitiesRectSize.y;
                float imageWidth = startingAbilityContainer.AbilityImage.sprite.textureRect.width / (float) startingAbilityContainer.AbilityImage.sprite.textureRect.height * imageHeight;
                if (imageWidth > maxImageWidth)
                {
                    imageWidth = maxImageWidth;
                    imageHeight = startingAbilityContainer.AbilityImage.sprite.textureRect.height / (float) startingAbilityContainer.AbilityImage.sprite.textureRect.width * imageWidth;
                }
                startingAbilityContainer.ImageRect.sizeDelta = new Vector2(imageWidth, imageHeight);
            }
        }

        public void Selected()
        {
            if (!IsOwned())
            {
                if (SilverWallet.TrySpend(characterBlueprint.cost))
                {
                    LobbyUnlockSave.Unlock("Character", GetCharacterUnlockId());

                    characterBlueprint.owned = true;

                    UpdateButtonText();
                    buttonImage.color = selectColor;

                    if (silverCoinDisplay != null)
                    {
                        silverCoinDisplay.UpdateDisplay();
                    }

                    Debug.Log($"[Lobby] Character Unlocked: {characterBlueprint.name}");
                }
                else
                {
                    Debug.Log($"[Lobby] Not enough silver. Need {characterBlueprint.cost}, Current {SilverWallet.Silver}");
                }

                return;
            }

            characterSelector.StartGame(characterBlueprint);
        }
        private void UpdateButtonText()
        {
            if (!initialized)
            {
                return;
            }

            if (IsOwned())
            {
                buttonText.text = selectLocalization.GetLocalizedString();
            }
            else
            {
                buttonText.text = $"{buyLocalization.GetLocalizedString()} ({characterBlueprint.cost} 실버)";
            }
        }
    }
}
