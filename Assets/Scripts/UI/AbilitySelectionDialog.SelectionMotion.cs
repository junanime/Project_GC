using UnityEngine;

namespace Vampire
{
    public partial class AbilitySelectionDialog
    {
        AugmentSelectionMotion selectionMotion;
        public bool SelectionAnimating { get; private set; }

        public void SelectWithMotion(AbilityCard card, Ability selectedAbility)
        {
            if (!menuOpen || SelectionAnimating || selectedAbility == null) return;
            SelectionAnimating = true; // Lock before Select: double clicks/touches cannot grant twice.
            selectedAbility.Select();
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.AugmentSelect);
            if (rerollButton != null) rerollButton.interactable = false;
            if (skipButton != null) skipButton.interactable = false;
            selectionMotion = AugmentSelectionMotion.Create(panelContent, card, abilityCards, CompleteSelectionMotion);
        }

        void CompleteSelectionMotion()
        {
            selectionMotion = null;
            SelectionAnimating = false;
            if (skipButton != null) skipButton.interactable = true;
            Close(); // The level-up queue resumes only after the card has disappeared.
        }

        void OnDisable()
        {
            if (selectionMotion != null) selectionMotion.Cancel();
            selectionMotion = null;
            SelectionAnimating = false;
            if (skipButton != null) skipButton.interactable = true;
        }
    }
}
