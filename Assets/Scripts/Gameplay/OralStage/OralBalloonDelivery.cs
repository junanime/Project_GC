using UnityEngine;

namespace Vampire
{
    [DefaultExecutionOrder(12050)]
    public sealed class OralBalloonDelivery : IDamageable
    {
        public bool Popped { get; private set; }
        public Chest LandedChest { get; private set; }
        public Vector2 BalloonPosition => balloon.transform.position + Vector3.up * .65f;
        public Sprite ChestSprite => chest.sprite;
        LevelManager level; FiniteStomachMap map; ChestBlueprint blueprint;
        SpriteRenderer balloon, chest, shadow, burst; LineRenderer left, right;
        CircleCollider2D hitbox; Vector2 ground; float age, fallAge, dropHeight;
        Renderer[] renderers;
        public static OralBalloonDelivery Create(OralEventAdditions owner, LevelManager level, FiniteStomachMap map)
        {
            var blueprint=RemakeBalance.Current?.levelUpChest;
            if(blueprint==null)return null;
            var go=new GameObject("풍선껌 보물배송"); go.transform.SetParent(owner.transform,false);
            var d=go.AddComponent<OralBalloonDelivery>();d.level=level;d.map=map;d.blueprint=blueprint;
            d.ground=OralMapGeometry.Clamp(map,level.PlayerCharacter.Position+new Vector2(2,1),.8f,OralZone.Tongue);
            d.balloon=OralEventArt.Sprite(go.transform,"공격 가능한 풍선껌",OralEventArt.Balloon,32);
            d.balloon.transform.localScale=Vector3.one*(1.3f/d.balloon.sprite.bounds.size.y);
            d.chest=OralEventArt.Sprite(go.transform,"미믹 방과 동일한 레벨업 상자",blueprint.Visual(blueprint.closedChest),31);
            d.chest.transform.localScale=level.EntityManager.ChestVisualScale;
            d.shadow=OralEventArt.Sprite(go.transform,"배송 그림자",OralEventArt.Disc,8);GroundVisualSorting.Apply(d.shadow,8);
            d.shadow.color=new Color(.15f,.03f,.07f,.28f);d.shadow.transform.localScale=new Vector3(1,.32f,1);
            d.left=OralEventArt.Line(go.transform,"왼쪽 매듭",new Color(1,.9f,.7f),.025f,30);d.left.positionCount=2;
            d.right=OralEventArt.Line(go.transform,"오른쪽 매듭",new Color(1,.9f,.7f),.025f,30);d.right.positionCount=2;
            d.burst=OralEventArt.Sprite(go.transform,"풍선껌 파열 조각",OralEventArt.Burst,33);d.burst.enabled=false;
            // Keep the damageable and collider on the same object for all projectile types.
            go.layer=LayerMask.NameToLayer("Monster Full");d.hitbox=go.AddComponent<CircleCollider2D>();d.hitbox.isTrigger=true;d.hitbox.radius=.55f;
            var rb=go.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.gravityScale=0;
            d.renderers=go.GetComponentsInChildren<Renderer>();d.Pose();return d;
        }
        void Update()
        {
            bool hidden=MiniStageRuntimeState.IsInsideMiniStage||StageEntryLoading.IsLoading||
                (OralFieldEvents.Instance?.OnCeiling??false)||level.PlayerCharacter.IsPortalTravelling||(level.GetComponent<StageProgression>()?.Travelling??false);
            foreach(var r in renderers)r.forceRenderingOff=hidden;
            hitbox.enabled=!hidden&&!Popped&&!OralEventArt.Paused(level);
            if(hidden||OralEventArt.Paused(level))return;
            float dt=Time.deltaTime;age+=dt;
            if(!Popped)ground=OralMapGeometry.Clamp(map,ground+new Vector2(Mathf.Sin(age*.7f),Mathf.Cos(age*.6f))*(dt*.15f),.8f,OralZone.Tongue);
            else
            {
                fallAge+=dt;
                if(fallAge>=.7f)
                {
                    LandedChest=level.EntityManager.SpawnChest(blueprint,ground);
                    LandedChest.CompleteInitialPlacement();
                    gameObject.SetActive(false);Destroy(gameObject);return;
                }
            }
            Pose();
        }
        void Pose()
        {
            float height=Popped?Mathf.Lerp(dropHeight,0,Mathf.Pow(Mathf.Clamp01(fallAge/.7f),2)):.8f+Mathf.Sin(age*2)*.13f;
            chest.transform.position=ground+Vector2.up*height;
            balloon.transform.position=ground+Vector2.up*(height+.7f);
            shadow.transform.position=ground;
            hitbox.offset=BalloonPosition-(Vector2)transform.position;
            var tie=balloon.transform.position;
            left.SetPosition(0,tie);left.SetPosition(1,chest.transform.position+new Vector3(-.22f,.15f));
            right.SetPosition(0,tie);right.SetPosition(1,chest.transform.position+new Vector3(.22f,.15f));
            if(Popped){float p=Mathf.Clamp01(fallAge/.18f);balloon.transform.localScale=Vector3.one*(1.3f/balloon.sprite.bounds.size.y)*(1+p*.5f);balloon.color=new Color(1,1,1,1-p);left.enabled=right.enabled=false;
                float b=Mathf.Clamp01(fallAge/.4f);burst.enabled=b<1;burst.transform.localScale=Vector3.one*((1.1f+b*.9f)/burst.sprite.bounds.size.x);burst.color=new Color(1,1,1,1-b);}
        }
        public override void TakeDamage(float damage,Vector2 knockback=default,bool isCritical=false)
        {
            if(damage<=0||Popped||!hitbox.enabled||OralEventArt.Paused(level))return;
            Popped=true;dropHeight=chest.transform.position.y-ground.y;hitbox.enabled=false;
            burst.transform.position=BalloonPosition;
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.ChestOpen);
        }
        public override void Knockback(Vector2 knockback){}
    }
}
