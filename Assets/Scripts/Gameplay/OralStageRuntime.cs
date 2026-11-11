using UnityEngine;

namespace Vampire
{
    [DisallowMultipleComponent, DefaultExecutionOrder(1100)]
    public sealed class OralStageRuntime : MonoBehaviour
    {
        public static OralStageRuntime Instance {get;private set;}
        public LevelManager Level {get;private set;}
        public FiniteStomachMap Map {get;private set;}
        public bool OnCeiling {get;private set;}
        public bool IsJumping {get;private set;}
        public bool IsSliding {get;private set;}
        public bool IsAirborne => IsJumping || IsSliding || (Level!=null && Level.PlayerCharacter!=null && (Level.PlayerCharacter.GetComponent<OralTasteBounce>()?.Active??false));
        public float SlideProgress => IsSliding ? Mathf.Clamp01(slideElapsed / SlideSeconds) : 0;
        public float SlideVisualHeight {get;private set;}
        public Vector2 SlideLanding {get;private set;}
        // Stable idle-art HUD anchors inherit travel translation, never animated
        // sprite bounds. Convert through the parent so character scale is respected.
        public Vector3 TravelVisualWorldOffset
        {
            get
            {
                var tasteBounce=Level!=null&&Level.PlayerCharacter!=null?Level.PlayerCharacter.GetComponent<OralTasteBounce>():null;
                if(tasteBounce!=null&&tasteBounce.Active)return tasteBounce.VisualWorldOffset;
                var visual=IsSliding?slideVisual:IsJumping?jumpVisual:null;
                if(visual==null)return Vector3.zero;
                var origin=IsSliding?slideVisualOrigin:visualOrigin;
                var delta=visual.transform.localPosition-origin;
                var parent=visual.transform.parent;
                return parent!=null?parent.TransformVector(delta):delta;
            }
        }
        public const float SlideSeconds = 1.12f, SlideGroundSeconds = .65f;
        Vector2 lastSupportedPosition, slideStart, slideLaunch, slidePosition;
        bool hasSupportedPosition;
        float slideElapsed;
        SpriteRenderer slideVisual; Vector3 slideVisualOrigin; Quaternion slideVisualRotation;
        Rigidbody2D slideBody; RigidbodyType2D slideBodyType;
        public bool Ready {get;private set;}
        public bool ExitReady => exit!=null;
        public Vector2 JumpLanding {get;private set;}
        SpriteRenderer jumpVisual,playerVisual;Vector3 visualOrigin;float jumpStarted,jumpDuration;
        OralZone playerZone=OralZone.Tongue;
        OralSwallowExit exit;
        public static OralStageRuntime Install(LevelManager level)
        {
            var runtime=level.GetComponent<OralStageRuntime>()??level.gameObject.AddComponent<OralStageRuntime>();
            Instance=runtime;runtime.Level=level;runtime.Map=FiniteStomachMap.Instance;
            runtime.playerVisual=level.PlayerCharacter.GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            (runtime.GetComponent<OralMapVisual>()??runtime.gameObject.AddComponent<OralMapVisual>()).Configure(runtime);
            (runtime.GetComponent<OralFieldEvents>()??runtime.gameObject.AddComponent<OralFieldEvents>()).Configure(level);
            runtime.lastSupportedPosition=level.PlayerCharacter.Position;
            runtime.hasSupportedPosition=true;runtime.Ready=true;return runtime;
        }
        public static bool IsSlidingActor(Character actor) => Instance!=null && Instance.IsSliding &&
            Instance.Level!=null && actor==Instance.Level.PlayerCharacter;
        public static void CancelPlayerTravel(Character actor)
        {
            if(Instance==null || Instance.Level==null || actor!=Instance.Level.PlayerCharacter)return;
            Instance.EndJump();Instance.EndSlide(false);
        }
        public static bool IsVisualInverted(Character actor)
        {
            var stage=Instance;
            return stage!=null&&stage.OnCeiling&&stage.Level!=null&&actor==stage.Level.PlayerCharacter&&
                stage.playerVisual!=null&&stage.playerVisual.flipY;
        }
        // Authored hand/beak anchors stay in their ordinary local space. Reflect each
        // final world position once; aiming direction and actor rotation stay unchanged.
        public static Vector3 MirrorVisualPoint(Character actor,Vector3 uprightWorldPoint)
        {
            if(IsVisualInverted(actor))uprightWorldPoint.y=2*Instance.playerVisual.transform.position.y-uprightWorldPoint.y;
            return uprightWorldPoint;
        }
        public void SetCeiling(bool value)
        {
            if(IsJumping)EndJump();EndSlide(false);OnCeiling=value;playerZone=OralZone.Tongue;
            if(Level==null||Map==null)return;
            var player=Level.PlayerCharacter;if(player==null)return;
            var p=OralMapGeometry.Clamp(Map,player.transform.position,.5f,OralZone.Tongue);
            lastSupportedPosition=p;hasSupportedPosition=true;
            player.transform.position=p;var rb=player.GetComponent<Rigidbody2D>();if(rb!=null){rb.position=p;rb.velocity=Vector2.zero;}
        }
        public bool BeginJump(Character player,Vector2 direction,out Vector2 landing)
        {
            landing=player.transform.position;
            if(!Ready||OnCeiling||IsAirborne||Level.IsRunFlowPaused||MiniStageRuntimeState.IsInsideMiniStage||player!=Level.PlayerCharacter)return false;
            if(!OralMapGeometry.TryJump(Map,player.transform.position,direction,out landing))return false;
            JumpLanding=landing;IsJumping=true;jumpStarted=Time.time;jumpDuration=.55f;
            var animator=player.GetComponentInChildren<SpriteAnimator>();jumpVisual=animator!=null?animator.GetComponent<SpriteRenderer>():player.GetComponentInChildren<SpriteRenderer>();
            if(jumpVisual!=null)visualOrigin=jumpVisual.transform.localPosition;
            return true;
        }
        public void EndJump()
        {
            if(!IsJumping)return;
            if(jumpVisual!=null)jumpVisual.transform.localPosition=visualOrigin;
            jumpVisual=null;IsJumping=false;
            if(Map!=null)playerZone=OralMapGeometry.ZoneAt(Map,JumpLanding);
            lastSupportedPosition=JumpLanding;hasSupportedPosition=true;
        }
        public Vector2 ConstrainPlayer(Vector2 position,float radius)
        {
            if(IsSliding)return slidePosition;
            if(IsJumping)return position;
            if(OnCeiling)playerZone=OralZone.Tongue;
            if(Ready && !OnCeiling && playerZone==OralZone.Teeth && hasSupportedPosition &&
                Level!=null && Level.PlayerCharacter!=null && Level.PlayerCharacter.IsAlive &&
                !Level.IsRunFlowPaused && !Level.PlayerCharacter.IsPortalTravelling &&
                !MiniStageRuntimeState.IsInsideMiniStage &&
                OralMapGeometry.TrySlideToTongue(Map,lastSupportedPosition,position,out var landing))
            {
                BeginSlide(lastSupportedPosition,landing);
                return slidePosition;
            }
            var supported=OralMapGeometry.Clamp(Map,position,radius,playerZone);
            lastSupportedPosition=supported;hasSupportedPosition=true;
            return supported;
        }
        void BeginSlide(Vector2 from,Vector2 landing)
        {
            var player=Level.PlayerCharacter;
            IsSliding=true;slideElapsed=0;SlideVisualHeight=0;
            slideStart=slidePosition=from;
            SlideLanding=OralMapGeometry.Clamp(Map,landing,.65f,OralZone.Tongue);
            slideLaunch=Vector2.Lerp(from,SlideLanding,.62f);
            slideVisual=player.GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            if(slideVisual==null)slideVisual=player.GetComponentInChildren<SpriteRenderer>();
            if(slideVisual!=null){slideVisualOrigin=slideVisual.transform.localPosition;slideVisualRotation=slideVisual.transform.localRotation;}
            slideBody=player.GetComponent<Rigidbody2D>();
            if(slideBody!=null){slideBodyType=slideBody.bodyType;slideBody.velocity=Vector2.zero;slideBody.angularVelocity=0;slideBody.bodyType=RigidbodyType2D.Kinematic;}
            // Ground locomotion does not fight the glide; the small hop belongs only to its final phase.
            player.StartIdleAnimation();
        }
        void TickSlide()
        {
            var player=Level.PlayerCharacter;
            if(!player.IsAlive || Level.IsLevelEnded || Map==null || !Map.IsOral ||
                MiniStageRuntimeState.IsInsideMiniStage || player.IsPortalTravelling)
            {EndSlide(false);return;}
            if(!Level.IsRunFlowPaused && !player.IsChoosingAugment)slideElapsed+=Time.deltaTime;
            float hop=Mathf.Clamp01((slideElapsed-SlideGroundSeconds)/(SlideSeconds-SlideGroundSeconds));
            if(slideElapsed<SlideGroundSeconds)
            {
                float glide=Mathf.Clamp01(slideElapsed/SlideGroundSeconds);
                slidePosition=Vector2.Lerp(slideStart,slideLaunch,glide*glide);
            }
            else slidePosition=Vector2.Lerp(slideLaunch,SlideLanding,Mathf.SmoothStep(0,1,hop));
            SlideVisualHeight=Mathf.Sin(hop*Mathf.PI)*.9f;
            if(slideBody!=null){slideBody.position=slidePosition;slideBody.velocity=Vector2.zero;}
            player.transform.position=new Vector3(slidePosition.x,slidePosition.y,player.transform.position.z);
            if(slideVisual!=null)
            {
                slideVisual.transform.localPosition=slideVisualOrigin+Vector3.up*SlideVisualHeight;
                float lean=Mathf.Sin(Mathf.Clamp01(slideElapsed/SlideSeconds)*Mathf.PI)*-Mathf.Sign(SlideLanding.x-slideStart.x)*9;
                slideVisual.transform.localRotation=slideVisualRotation*Quaternion.Euler(0,0,lean);
            }
            if(slideElapsed>=SlideSeconds)EndSlide(true);
        }
        void EndSlide(bool land)
        {
            if(!IsSliding)return;
            if(slideVisual!=null){slideVisual.transform.localPosition=slideVisualOrigin;slideVisual.transform.localRotation=slideVisualRotation;}
            if(slideBody!=null){slideBody.bodyType=slideBodyType;slideBody.velocity=Vector2.zero;slideBody.angularVelocity=0;}
            IsSliding=false;SlideVisualHeight=0;slideVisual=null;slideBody=null;
            if(land && Level!=null && Level.PlayerCharacter!=null && Map!=null)
            {
                playerZone=OralZone.Tongue;
                var p=OralMapGeometry.Clamp(Map,SlideLanding,.65f,OralZone.Tongue);
                var player=Level.PlayerCharacter;player.transform.position=new Vector3(p.x,p.y,player.transform.position.z);
                var rb=player.GetComponent<Rigidbody2D>();if(rb!=null){rb.position=p;rb.velocity=Vector2.zero;}
                lastSupportedPosition=p;hasSupportedPosition=true;
            }
        }
        public void TeleportPlayer(Vector2 position,OralZone zone)
        {
            EndJump();EndSlide(false);playerZone=zone;position=OralMapGeometry.Clamp(Map,position,.5f,zone);
            lastSupportedPosition=position;hasSupportedPosition=true;
            var player=Level.PlayerCharacter;player.transform.position=position;
            var rb=player.GetComponent<Rigidbody2D>();if(rb!=null){rb.position=position;rb.velocity=Vector2.zero;}
        }
        void LateUpdate()
        {
            if(!Ready||Level==null||Level.PlayerCharacter==null)return;
            if(exit==null&&Level.CurrentLevelTime>=Level.LevelDuration&&!Level.IsLevelEnded)
                exit=OralSwallowExit.Create(this);
            if(IsSliding)TickSlide();
            if(IsJumping)
            {
                if(jumpVisual!=null){float t=Mathf.Clamp01((Time.time-jumpStarted)/jumpDuration);jumpVisual.transform.localPosition=visualOrigin+Vector3.up*(Mathf.Sin(t*Mathf.PI)*1.15f);}
                if(!Level.PlayerCharacter.IsDashing||!Level.PlayerCharacter.IsAlive)EndJump();
            }
        }
        void OnDisable(){EndJump();EndSlide(false);}
        void OnDestroy(){EndJump();EndSlide(false);if(exit!=null)Destroy(exit.gameObject);if(Instance==this)Instance=null;}
    }

    // A timed exit is optional to use: never require a particular cavity/event roll to finish.
    public sealed class OralSwallowExit : InteractableEventObject
    {
        OralStageRuntime runtime;float born;SpriteRenderer visual;
        public static OralSwallowExit Create(OralStageRuntime owner)
        {
            var go=new GameObject("구강 출구 · 위장으로 이동");
            go.transform.position=OralMapGeometry.Clamp(owner.Map,OralMapGeometry.TongueCenter(owner.Map)+Vector2.down*OralMapGeometry.TongueRadii(owner.Map).y*.72f,1,OralZone.Tongue);
            var col=go.AddComponent<CircleCollider2D>();col.radius=1.8f;col.isTrigger=true;
            var exit=go.AddComponent<OralSwallowExit>();exit.runtime=owner;exit.ConfigureReusableInteraction();exit.born=Time.time;
            exit.visual=go.AddComponent<SpriteRenderer>();exit.visual.sprite=BossTravelArt.Hole;
            if(exit.visual.sprite!=null){float scale=4.2f/exit.visual.sprite.bounds.size.x;exit.visual.transform.localScale=Vector3.one*scale;col.radius=1.8f/scale;}
            GroundVisualSorting.ApplyBackground(exit.visual,-2);
            go.AddComponent<MapMarker>().Configure(MapMarkerKind.Event,"위장으로 이동 · 상호작용");
            return exit;
        }
        protected override bool InteractionAvailable=>runtime!=null&&!runtime.OnCeiling&&!runtime.IsAirborne&&!runtime.Level.IsRunFlowPaused;
        protected override bool ExecuteInteraction(Character player)=>runtime!=null&&StageProgression.Ensure(runtime.Level).BeginOralToStomach();
        protected override void Update(){base.Update();if(visual!=null)visual.color=new Color(1,1,1,Mathf.Lerp(.72f,1,.5f+.5f*Mathf.Sin((Time.time-born)*2)));}
    }
}
