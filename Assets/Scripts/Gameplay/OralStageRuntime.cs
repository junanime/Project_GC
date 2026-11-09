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
            runtime.Ready=true;return runtime;
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
            if(IsJumping)EndJump();OnCeiling=value;playerZone=OralZone.Tongue;
            if(Level==null||Map==null)return;
            var player=Level.PlayerCharacter;if(player==null)return;
            var p=OralMapGeometry.Clamp(Map,player.transform.position,.5f,OralZone.Tongue);
            player.transform.position=p;var rb=player.GetComponent<Rigidbody2D>();if(rb!=null){rb.position=p;rb.velocity=Vector2.zero;}
        }
        public bool BeginJump(Character player,Vector2 direction,out Vector2 landing)
        {
            landing=player.transform.position;
            if(!Ready||OnCeiling||IsJumping||Level.IsRunFlowPaused||MiniStageRuntimeState.IsInsideMiniStage||player!=Level.PlayerCharacter)return false;
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
        }
        public Vector2 ConstrainPlayer(Vector2 position,float radius)
        {
            if(IsJumping)return position;
            if(OnCeiling)playerZone=OralZone.Tongue;
            return OralMapGeometry.Clamp(Map,position,radius,playerZone);
        }
        public void TeleportPlayer(Vector2 position,OralZone zone)
        {
            EndJump();playerZone=zone;position=OralMapGeometry.Clamp(Map,position,.5f,zone);
            var player=Level.PlayerCharacter;player.transform.position=position;
            var rb=player.GetComponent<Rigidbody2D>();if(rb!=null){rb.position=position;rb.velocity=Vector2.zero;}
        }
        void LateUpdate()
        {
            if(!Ready||Level==null||Level.PlayerCharacter==null)return;
            if(exit==null&&Level.CurrentLevelTime>=Level.LevelDuration&&!Level.IsLevelEnded)
                exit=OralSwallowExit.Create(this);
            if(IsJumping)
            {
                if(jumpVisual!=null){float t=Mathf.Clamp01((Time.time-jumpStarted)/jumpDuration);jumpVisual.transform.localPosition=visualOrigin+Vector3.up*(Mathf.Sin(t*Mathf.PI)*1.15f);}
                if(!Level.PlayerCharacter.IsDashing||!Level.PlayerCharacter.IsAlive)EndJump();
            }
        }
        void OnDisable(){EndJump();}
        void OnDestroy(){EndJump();if(exit!=null)Destroy(exit.gameObject);if(Instance==this)Instance=null;}
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
        protected override bool InteractionAvailable=>runtime!=null&&!runtime.OnCeiling&&!runtime.IsJumping&&!runtime.Level.IsRunFlowPaused;
        protected override bool ExecuteInteraction(Character player)=>runtime!=null&&StageProgression.Ensure(runtime.Level).BeginOralToStomach();
        protected override void Update(){base.Update();if(visual!=null)visual.color=new Color(1,1,1,Mathf.Lerp(.72f,1,.5f+.5f*Mathf.Sin((Time.time-born)*2)));}
    }
}
