using UnityEngine;

namespace Vampire
{
    public partial class AbilitySelectionDialog
    {
        AugmentSelectionMotion selectionMotion;
        public bool SelectionAnimating { get; private set; }

        public void CancelForStageTransition()
        {
            pendingLevelRewards.Clear();
            if(!menuOpen&&!SelectionAnimating)return;
            float previousTimeScale=Time.timeScale;
            OnDisable();
            menuOpen=false; // A departing field must not chain another choice.
            Close();
            Time.timeScale=previousTimeScale;
        }

        public void SelectWithMotion(AbilityCard card, Ability selectedAbility)
        {
            if (!menuOpen || SelectionAnimating || selectedAbility == null || (practiceOwner != null && !rerollUsed)) return;
            SelectionAnimating = true; // Lock before Select: double clicks/touches cannot grant twice.
            string analyticsId=AnalyticsAugmentId(selectedAbility), analyticsName=selectedAbility.Name;
            bool validChoice=selectedAbility.RequirementsMet();
            selectedAbility.Select();
            if(validChoice)GStarAnalytics.Record("augment_choice",analyticsId,analyticsName,analyticsAugmentOffers);
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
