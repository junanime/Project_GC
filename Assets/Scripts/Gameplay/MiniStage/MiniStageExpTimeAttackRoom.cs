namespace Vampire
{
    [System.Obsolete("Use MiniStageCollectionRoom with Experience reward kind.")]
    public class MiniStageExpTimeAttackRoom : MiniStageCollectionRoom
    {
        private void Awake() { rewardKind = CollectionRewardKind.Experience; }
        private void Reset() { rewardKind = CollectionRewardKind.Experience; }
    }
}
