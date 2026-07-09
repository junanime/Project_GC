using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class TabCharacterInfoUI : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private Character playerCharacter;

        [Header("UI References")]
        [SerializeField] private Image characterIcon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;

        [SerializeField] private Image hpFill;
        [SerializeField] private TextMeshProUGUI hpText;

        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private TextMeshProUGUI speedText;
        [SerializeField] private TextMeshProUGUI critText;

        private SpriteRenderer playerSpriteRenderer;

        private void OnEnable()
        {
            ResolvePlayer();
            Refresh();
        }

        private void Update()
        {
            if (playerCharacter == null)
            {
                ResolvePlayer();
            }

            Refresh();
        }

        private void ResolvePlayer()
        {
            playerCharacter = FindObjectOfType<Character>();

            if (playerCharacter != null)
            {
                playerSpriteRenderer =
                    playerCharacter.GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void Refresh()
        {
            if (playerCharacter == null)
            {
                return;
            }

            float maxHp = Mathf.Max(1f, playerCharacter.MaxHealth);
            float currentHp = Mathf.Clamp(playerCharacter.CurrentHealth, 0f, maxHp);

            if (nameText != null)
            {
                nameText.text = playerCharacter.DisplayName;
            }

            if (levelText != null)
            {
                levelText.text = $"Lv.{playerCharacter.CurrentLevel}";
            }

            if (hpText != null)
            {
                hpText.text = $"HP {currentHp:0} / {maxHp:0}";
            }

            if (hpFill != null)
            {
                hpFill.fillAmount = currentHp / maxHp;
            }

            if (attackText != null)
            {
                attackText.text =
                    $"공격력  x{playerCharacter.DamageMultiplier:0.00}";
            }

            if (armorText != null)
            {
                armorText.text =
                    $"방어력  {playerCharacter.CurrentArmor:0}";
            }

            if (speedText != null)
            {
                speedText.text =
                    $"속도  {playerCharacter.CurrentMoveSpeed:0.0}";
            }

            if (critText != null)
            {
                critText.text =
                    $"치명타  {playerCharacter.CritChance * 100f:0}%";
            }

            if (characterIcon != null &&
                playerSpriteRenderer != null &&
                playerSpriteRenderer.sprite != null)
            {
                characterIcon.sprite = playerSpriteRenderer.sprite;
                characterIcon.preserveAspect = true;
                characterIcon.color = Color.white;
            }
        }
    }
}