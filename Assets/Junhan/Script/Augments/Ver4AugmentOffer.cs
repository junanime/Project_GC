using UnityEngine;

namespace Vampire
{
    public enum Ver4RewardKind { NewSpecial, Numeric, Original, LegendaryAbility }

    // A preview is not an owned ability. It is discarded on close/reroll and
    // cannot enter the persistent ability pool or apply itself more than once.
    public sealed class Ver4AugmentOffer : Ability
    {
        public Ver4RewardKind Kind { get; private set; }
        public AugmentUpgradeGrade Grade { get; private set; }
        public SyringeSpecialAugmentAbility Parent { get; private set; }
        public Ability Source { get; private set; }
        public int Option { get; private set; }
        public float Amount { get; private set; }
        private Ver4AugmentRuntime owner;
        private string title, description;
        private bool consumed;
        public override string Name => title;
        public override string Description => description;

        public void Configure(Ver4AugmentRuntime runtime, Ver4RewardKind kind, AugmentUpgradeGrade grade,
            Ability source, int option, float amount, string name, string text)
        {
            owner=runtime; Kind=kind; Grade=grade; Source=source; Parent=source as SyringeSpecialAugmentAbility;
            Option=option; Amount=amount; title=name; description=text; image=source.Image;
            augmentTier=kind==Ver4RewardKind.LegendaryAbility ? AugmentTier.Legendary : AugmentTier.General;
            canAppearAsOwnedUpgrade=false;
        }

        public override bool RequirementsMet() => !consumed && owner != null && owner.IsValid(this);
        public override void Select()
        {
            if (!RequirementsMet()) return;
            consumed=owner.Apply(this);
        }
    }
}
