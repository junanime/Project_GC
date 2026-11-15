using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>Persistent optional props coexist with the disruptive oral event timeline.</summary>
    [DefaultExecutionOrder(12300)]
    public sealed class OralAmbientContent : MonoBehaviour
    {
        public static OralAmbientContent Instance {get;private set;}
        public LevelManager Level {get;private set;}
        public float PlayerMoveMultiplier => pushing ? .7f : 1f;
        public bool RockActive => rock!=null;
        public bool RockFalling {get;private set;}
        public Vector2 RockPosition {get;private set;}
        public Vector2 RockVelocity {get;private set;}
        public float RockRadius {get;private set;}
        public int RockHits {get;private set;}
        public bool GuardActive => guard!=null;
        public Vector2 GuardPosition => guard!=null?guard.Position:Vector2.zero;
        public Vector2 GuardGoal => guard!=null?guard.Route.Landing:Vector2.zero;
        public OralGuardEscort Guard => guard;
        public int GuardFailed {get;private set;}
        bool guardResultRecorded;
        public int GuardCompleted {get;private set;}
        public Chest GuardReward {get;private set;}
        public const float RockLifetime=55f,GuardLifetime=55f;
        FiniteStomachMap map;Character player;SpriteRenderer rock,rockShadow;
        OralGuardEscort guard;
        readonly List<Monster> scratch=new List<Monster>();
        readonly Dictionary<Monster,float> rockCooldown=new Dictionary<Monster,float>();
        static readonly Dictionary<string,Sprite> art=new Dictionary<string,Sprite>();
        bool pushing,rockDashHit;float rockAge,fallAge,rollAngle,rockScale,clock,bounceCooldown,rockHopRemaining;
        float ashiHeight=.6f;
        public void Configure(LevelManager level)
        {
            if(Instance!=null&&Instance!=this)Instance.ClearAll();Instance=this;Level=level;player=level.PlayerCharacter;map=FiniteStomachMap.Instance;
            var renderer=player.GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            if(renderer!=null)ashiHeight=Mathf.Clamp(PromptSpriteGeometry.WorldBounds(renderer).size.y,.3f,2);
        }
        public static Sprite EventSprite(string id)
        {
            if(art.TryGetValue(id,out var result)&&result!=null)return result;
            result=Resources.Load<Sprite>("OralMap/Events/"+id);
            if(result==null){var texture=Resources.Load<Texture2D>("OralMap/Events/"+id);if(texture!=null)result=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f,100);}
            art[id]=result;return result;
        }
        SpriteRenderer Shadow(string name){var r=OralEventArt.Sprite(transform,name,OralEventArt.Disc,7);GroundVisualSorting.Apply(r,7);r.color=new Color(.18f,.035f,.07f,.27f);return r;}
        public bool BeginBoulder()
        {
            if(rock!=null||player==null||map==null)return false;
            rock=OralEventArt.Sprite(transform,"치석 굴림돌",EventSprite("Rock"),25);if(rock.sprite==null){Destroy(rock.gameObject);rock=null;return false;}
            float height=Mathf.Clamp(ashiHeight*1.8f,.7f,2.5f);rockScale=height/rock.sprite.bounds.size.y;rock.transform.localScale=Vector3.one*rockScale;RockRadius=height*.42f;
            RockPosition=OralMapGeometry.Clamp(map,player.Position+new Vector2(1.4f,0),RockRadius+.3f,OralZone.Tongue);RockVelocity=Vector2.zero;rockAge=fallAge=rollAngle=rockHopRemaining=0;RockFalling=false;RockHits=0;rockCooldown.Clear();
            rockShadow=Shadow("굴림돌 지면 그림자");rockShadow.transform.localScale=new Vector3(height*.85f,height*.24f,1);PoseRock();return true;
        }
        public bool BeginGuard()
        {
            if(guard!=null||player==null||map==null)return false;
            var go=new GameObject("민트 구강수호대 · 어금니 호위");go.transform.SetParent(transform,false);
            var escort=go.AddComponent<OralGuardEscort>();
            var frames=OralGuardArt.Frames;
            if(!escort.Configure(Level,map,player.Position,ashiHeight,frames!=null&&frames.Length>0?frames[0]:null))
            {go.SetActive(false);Destroy(go);return false;}
            guard=escort;guardResultRecorded=false;GuardReward=null;return true;
        }
        void Update()
        {
            if(Level==null||player==null||map==null)return;
            bool hidden=MiniStageRuntimeState.IsInsideMiniStage||StageEntryLoading.IsLoading||player.IsPortalTravelling||(Level.GetComponent<StageProgression>()?.Travelling??false)||(OralFieldEvents.Instance?.OnCeiling??false);
            foreach(var r in GetComponentsInChildren<SpriteRenderer>())r.forceRenderingOff=hidden;
            bool paused=hidden||OralEventArt.Paused(Level);
            if(guard!=null)guard.SetSuspended(paused,hidden);
            if(paused){SetPush(false);return;}
            float dt=Time.deltaTime;clock+=dt;bounceCooldown=Mathf.Max(0,bounceCooldown-dt);bool push=false;
            if(rock!=null)
            {
                // A stalled frame must not teleport a rock through a ledge or miss a dash contact.
                float remaining=Mathf.Min(dt,.25f);
                while(remaining>0&&rock!=null){float step=Mathf.Min(.05f,remaining);push=TickRock(step)||push;remaining-=step;}
            }
            SetPush(push);if(guard!=null)TickGuard(dt);
        }
        void SetPush(bool value){if(pushing==value)return;pushing=value;if(player!=null)player.UpdateMoveSpeed();}
        bool TickRock(float dt)
        {
            rockAge+=dt;rockHopRemaining=Mathf.Max(0,rockHopRemaining-dt);
            if(RockFalling){fallAge+=dt;PoseRock();if(fallAge>=.9f)RemoveRock();return false;}
            if(rockAge>RockLifetime){RemoveRock();return false;}
            Vector2 previous=RockPosition,offset=RockPosition-player.Position,input=player.IsDashing&&player.Velocity.sqrMagnitude>.01f?player.Velocity.normalized:player.EffectiveMoveDirection.normalized;
            bool touching=offset.magnitude<RockRadius+.42f&&Vector2.Dot(input,offset.normalized)>.4f&&!player.IsOralSliding&&!(OralStageRuntime.Instance?.IsAirborne??false)&&OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Tongue;
            bool push=touching&&!player.IsDashing&&RockVelocity.sqrMagnitude<.1f;
            if(!player.IsDashing)rockDashHit=false;
            if(touching&&player.IsDashing&&!rockDashHit){rockDashHit=true;RockVelocity=(player.Velocity.sqrMagnitude>.1f?player.Velocity.normalized:input)*9f;GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.MonsterHit);}
            if(push)
            {
                float speed=Mathf.Max(.1f,Vector2.Dot(player.Velocity,input));RockPosition+=input*speed*dt;
                var rb=player.GetComponent<Rigidbody2D>();if(rb!=null&&Vector2.Distance(player.Position,RockPosition)<RockRadius+.16f)rb.position=RockPosition-offset.normalized*(RockRadius+.16f);
            }
            else if(RockVelocity.sqrMagnitude>.001f){RockPosition+=RockVelocity*dt;RockVelocity=Vector2.MoveTowards(RockVelocity,Vector2.zero,6f*dt);}
            if(OralTasteEvents.Instance!=null&&bounceCooldown<=0&&OralTasteEvents.Instance.TryBounceBoulder(RockPosition,out var bounce)){RockVelocity=bounce*7f;bounceCooldown=.9f;rockHopRemaining=.6f;}
            float distance=Vector2.Distance(previous,RockPosition);rollAngle-=Mathf.Sign(RockPosition.x-previous.x)*distance/Mathf.Max(.1f,RockRadius)*Mathf.Rad2Deg;
            if(RockPosition.y<OralMapGeometry.SouthFloor(map)+RockRadius*.4f){RockPosition=new Vector2(RockPosition.x,OralMapGeometry.SouthFloor(map)+RockRadius*.4f);RockVelocity=new Vector2(RockVelocity.x,0);}
            if(OralMapGeometry.TongueDistance(map,RockPosition,out _) < -RockRadius*.2f){RockFalling=true;fallAge=0;RockVelocity=Vector2.zero;PoseRock();return false;}
            if(distance>.002f)
            {
                Snapshot();foreach(var m in scratch)
                {
                    if(!Eligible(m)||m.IsFieldRuntimeSuspended||m is BloodClotMonster||m is TrapMonster||OralMapGeometry.ZoneAt(map,m.Position)!=OralZone.Tongue)continue;
                    if(rockCooldown.TryGetValue(m,out float next)&&clock<next)continue;
                    Vector2 path=RockPosition-previous;float u=Mathf.Clamp01(Vector2.Dot(m.Position-previous,path)/Mathf.Max(.0001f,path.sqrMagnitude));if(Vector2.Distance(m.Position,previous+path*u)>RockRadius+Mathf.Min(.6f,m.Size.magnitude*.5f))continue;
                    rockCooldown[m]=clock+.65f;RockHits++;m.TakeDamage(Mathf.Max(12f,m.Blueprint!=null?m.Blueprint.hp*.40f:12f),path.normalized*1.3f);
                }
            }
            PoseRock();return push;
        }
        void PoseRock()
        {
            if(rock==null)return;float fall=Mathf.Clamp01(fallAge/.9f),hop=RockFalling?0:Mathf.Sin(rockHopRemaining/.6f*Mathf.PI)*ashiHeight*1.5f;rock.transform.position=RockPosition+Vector2.up*(RockRadius*.65f+hop-fall*fall*ashiHeight*2.3f);rock.transform.rotation=Quaternion.Euler(0,0,rollAngle+fall*140);rock.transform.localScale=Vector3.one*rockScale*(1-fall*.7f);rock.color=new Color(1,1,1,1-fall);
            float lifeFade=1-Mathf.Clamp01((rockAge-(RockLifetime-3))/3);rock.color=new Color(1,1,1,(1-fall)*lifeFade);
            rockShadow.transform.position=RockPosition;rockShadow.color=new Color(.18f,.035f,.07f,.27f*(1-fall)*lifeFade);
        }
        void TickGuard(float dt)
        {
            guard.Tick(dt);
            if(!guardResultRecorded&&(guard.Complete||guard.Failed))
            {
                guardResultRecorded=true;
                if(guard.Complete)
                {
                    GuardCompleted++;
                    var blueprint=RemakeBalance.Current?.levelUpChest;
                    if(blueprint!=null&&Level.EntityManager!=null)
                    {GuardReward=Level.EntityManager.SpawnChest(blueprint,guard.Route.Landing);if(GuardReward!=null)GuardReward.CompleteInitialPlacement();}
                    GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.ChestOpen);
                }
                else GuardFailed++;
            }
            if(guard.ReadyToRemove)RemoveGuard();
        }
        void Snapshot(){scratch.Clear();if(Level.EntityManager?.LivingMonsters!=null)foreach(var m in Level.EntityManager.LivingMonsters)scratch.Add(m);}
        static bool Eligible(Monster m)=>m!=null&&m.gameObject.activeInHierarchy&&m.HP>0&&!m.IsMiniStageOwned;
        void RemoveRock(){if(rock!=null){rock.gameObject.SetActive(false);Destroy(rock.gameObject);}if(rockShadow!=null)Destroy(rockShadow.gameObject);rock=rockShadow=null;RockVelocity=Vector2.zero;RockFalling=false;rockCooldown.Clear();SetPush(false);}
        void RemoveGuard(){if(guard!=null){guard.gameObject.SetActive(false);Destroy(guard.gameObject);}guard=null;}
        public void ClearAll(){RemoveRock();RemoveGuard();SetPush(false);}
        public void CancelAll(){ClearAll();}
        void OnDisable(){ClearAll();}
        void OnDestroy(){ClearAll();if(Instance==this)Instance=null;}
    }
}
