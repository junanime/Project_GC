using UnityEngine;
namespace Vampire
{
    public sealed class BossRemnantPickup:InteractableEventObject
    {
        StageProgression owner;
        public static BossRemnantPickup Create(StageProgression progression,FoodBossKind kind,Vector2 position)
        {
            var go=new GameObject(FoodBossArt.Names[(int)kind]+" 부스러기 잠수복");go.transform.position=position;
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=BossTravelArt.Suit(kind);sr.sortingLayerName="Monster Full";sr.sortingOrder=520;
            if(sr.sprite!=null)go.transform.localScale=Vector3.one*(BossTravelArt.AshiHeight/sr.sprite.bounds.size.y);
            go.AddComponent<CircleCollider2D>().radius=1.1f/Mathf.Max(.01f,go.transform.localScale.x);
            var p=go.AddComponent<BossRemnantPickup>();p.owner=progression;
            go.AddComponent<MapMarker>().Configure(MapMarkerKind.Event,"부스러기 잠수복 · 다음 스테이지");return p;
        }
        protected override bool KeepVisibleAfterInteraction=>true;
        protected override bool InteractionAvailable=>owner!=null&&!owner.SuitEquipped&&owner.CanEnter(CurrentPlayer);
        protected override bool ExecuteInteraction(Character p)=>owner!=null&&owner.TryEquip(p,this);
    }
}
