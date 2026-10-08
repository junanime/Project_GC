namespace Vampire
{
    public sealed class StageExitPortal : InteractableEventObject
    {
        StageProgression progression;
        protected override bool KeepVisibleAfterInteraction => true;
        protected override bool InteractionAvailable => progression != null && progression.CanEnter(CurrentPlayer);
        public void Configure(StageProgression owner) { progression = owner; }
        public void AllowRetry() { ResetInteractionAvailability(); }
        protected override bool ExecuteInteraction(Character player) => progression != null && progression.TryEnter(player, this);
    }
}
