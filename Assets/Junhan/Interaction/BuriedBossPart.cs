using UnityEngine;

namespace Vampire
{
    public sealed class BuriedBossPart : InteractableEventObject
    {
        public FoodBossKind Kind { get; private set; }
        public BuriedBossEmergence Presentation { get; private set; }
        BuriedBossDirector director;
        protected override bool KeepVisibleAfterInteraction => true;
        protected override bool InteractionAvailable => director != null && director.CanSummon;

        public static BuriedBossPart Create(BuriedBossDirector owner, FoodBossKind kind, Vector2 position)
        {
            var go = new GameObject(FoodBossArt.Names[(int)kind] + " · 땅속 보스 파츠");
            go.transform.position = position;
            var trigger = go.AddComponent<CircleCollider2D>(); trigger.radius = 1.65f; trigger.isTrigger = true;
            var part = go.AddComponent<BuriedBossPart>();
            part.director = owner; part.Kind = kind;
            part.Presentation = go.AddComponent<BuriedBossEmergence>();
            part.Presentation.Initialize(kind);
            go.AddComponent<MapMarker>().Configure(MapMarkerKind.Event, FoodBossArt.Names[(int)kind] + "의 흔적");
            return part;
        }
        protected override bool ExecuteInteraction(Character player) => director.TrySummon(this, player);
        public bool Activate(Character player)
        {
            if (HasInteracted || !ExecuteInteraction(player)) return false;
            CompleteInteraction(); return true;
        }
        public void Restore() { ResetInteractionAvailability(); Presentation.ResetStandby(); }
    }
}
