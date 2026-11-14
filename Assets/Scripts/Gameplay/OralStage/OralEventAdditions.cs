using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    [DefaultExecutionOrder(12000)]
    public sealed class OralEventAdditions : MonoBehaviour
    {
        public const float HiccupWarning=1.2f, HiccupRise=.45f, HiccupHold=3f, HiccupFall=.45f;
        public LevelManager Level {get;private set;}
        public float LiftHeight {get;private set;}
        public float BrushWidth {get;private set;}
        public float BrushTravelSeconds {get;private set;}
        public int BrushPass {get;private set;}=-1;
        public bool BrushVertical=>BrushPass==1;
        public Vector2 BrushPosition {get;private set;}
        public int BrushPlayerHits {get;private set;}
        public int BrushMonsterHits {get;private set;}
        public OralBalloonDelivery Delivery {get;private set;}
        public IReadOnlyList<OralHiccupLift> Lifts=>lifts;
        public bool HiccupHolding {get;private set;}
        public bool BrushWarningVisible=>lane!=null&&lane.enabled;
        readonly List<OralHiccupLift> lifts=new List<OralHiccupLift>();
        readonly List<Monster> scratch=new List<Monster>();
        readonly HashSet<Monster> brushHit=new HashSet<Monster>();
        OralFieldEvents owner;FiniteStomachMap map;SpriteRenderer brush;LineRenderer lane;
        readonly List<LineRenderer> hatches=new List<LineRenderer>();
        readonly List<SpriteRenderer> foam=new List<SpriteRenderer>();
        Vector2 from,to,direction,previous;float phaseAge;bool lifted,playerHit,passSound;
        const float BrushWarning=1.2f,BrushGap=.4f;
        public void Configure(OralFieldEvents events,LevelManager level)
        {
            owner=events;Level=level;map=FiniteStomachMap.Instance;
            var f=FindObjectOfType<StageEventDirector>(true)?.ReferenceFieldEvents;BrushWidth=(f!=null?f.ReferenceFantaWidth:2f)*1.2f;BrushTravelSeconds=f!=null?f.ReferenceFantaTravelSeconds:1.4f;
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");var body=level.PlayerCharacter.GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            var reference=config!=null&&config.characters.Length>0?config.characters[0]:level.PlayerCharacter.Blueprint;
            float h=reference.idleSpriteSequence.Length>0&&reference.idleSpriteSequence[0]!=null?reference.idleSpriteSequence[0].bounds.size.y:1;
            LiftHeight=Mathf.Max(.3f,h*(body!=null?Mathf.Abs(body.transform.lossyScale.y):1))*3;
            brush=OralEventArt.Sprite(transform,"기본 칫솔 · 쓸어내기",OralEventArt.Brush,25);brush.enabled=false;
            if(brush.sprite!=null)brush.transform.localScale=Vector3.one*(BrushWidth/brush.sprite.bounds.size.y);
            lane=OralEventArt.Line(transform,"칫솔 경로 경고",new Color(1,.72f,.25f,.14f),BrushWidth,13,true);lane.positionCount=2;lane.enabled=false;
            for(int i=0;i<26;i++){var l=OralEventArt.Line(transform,"칫솔 경로 빗금",new Color(1,.8f,.4f,.5f),.045f,14,true);l.positionCount=2;l.enabled=false;hatches.Add(l);}
            for(int i=0;i<14;i++){var r=OralEventArt.Sprite(transform,"흰 칫솔 거품",OralEventArt.Disc,28);r.color=new Color(.9f,1,.98f,.65f);r.enabled=false;foam.Add(r);}
        }
        public float Begin(OralEventKind kind)
        {
            EndEvent();
            if(kind==OralEventKind.Hiccup){lifted=false;return HiccupWarning+HiccupRise+HiccupHold+HiccupFall;}
            if(kind==OralEventKind.Brush){BrushPass=-1;BrushPlayerHits=BrushMonsterHits=0;return 3*(BrushWarning+BrushTravelSeconds+BrushGap);}
            if(kind==OralEventKind.BalloonDelivery)
            {
                // Temporary fixed-cycle appearance; independent random drop tables are intentionally deferred.
                if(Delivery==null)Delivery=OralBalloonDelivery.Create(this,Level,map);
                return 1.5f;
            }
            return 0;
        }
        public void Tick(OralEventKind kind,float age)
        {
            if(kind==OralEventKind.Hiccup)TickHiccup(age);
            else if(kind==OralEventKind.Brush)TickBrush(age);
        }
        void TickHiccup(float age)
        {
            if(age<HiccupWarning)return;
            if(!lifted)
            {
                lifted=true;
                Snapshot();foreach(var m in scratch)
                {
                    if(!Eligible(m)||m.IsFieldRuntimeSuspended||m is BloodClotMonster||m is BossMonster||m is MiniBossMonster||m is TrapMonster)continue;
                    var lift=m.GetComponent<OralHiccupLift>()??m.gameObject.AddComponent<OralHiccupLift>();lift.Begin(this,m);lifts.Add(lift);
                }
                GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.PeristalsisTilt);
            }
            float t=age-HiccupWarning;
            HiccupHolding=t>=HiccupRise&&t<HiccupRise+HiccupHold;
            float factor=t<HiccupRise?Mathf.Sin(Mathf.Clamp01(t/HiccupRise)*Mathf.PI*.5f):t<HiccupRise+HiccupHold?1:1-Mathf.SmoothStep(0,1,(t-HiccupRise-HiccupHold)/HiccupFall);
            foreach(var lift in lifts)if(lift!=null&&lift.Active)lift.Sample(LiftHeight*factor);
        }
        void StartPass(int pass)
        {
            BrushPass=pass;playerHit=passSound=false;brushHit.Clear();
            direction=pass==1?Vector2.down:pass==2?Vector2.left:Vector2.right;
            Vector2 center=OralMapGeometry.Clamp(map,Level.PlayerCharacter.Position,.8f,OralZone.Tongue);
            var camera=Camera.main;float halfY=camera!=null?camera.orthographicSize:6,halfX=halfY*(camera!=null?camera.aspect:1.7778f);
            float half=pass==1?halfY:halfX;
            from=center-direction*(half+1);to=center+direction*(half+1);previous=from;BrushPosition=from;
            lane.SetPosition(0,from);lane.SetPosition(1,to);
            var side=new Vector2(-direction.y,direction.x);
            for(int i=0;i<hatches.Count;i++){Vector2 p=Vector2.Lerp(from,to,(i+.5f)/hatches.Count);hatches[i].SetPosition(0,p-side*BrushWidth*.48f-direction*.18f);hatches[i].SetPosition(1,p+side*BrushWidth*.48f+direction*.18f);}
            brush.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.DangerWave);
        }
        void TickBrush(float age)
        {
            float span=BrushWarning+BrushTravelSeconds+BrushGap;int pass=Mathf.Min(2,Mathf.FloorToInt(age/span));
            if(pass!=BrushPass)StartPass(pass);phaseAge=age-pass*span;
            bool warn=phaseAge<BrushWarning,travel=phaseAge>=BrushWarning&&phaseAge<BrushWarning+BrushTravelSeconds;
            lane.enabled=warn;foreach(var hatch in hatches)hatch.enabled=warn;brush.enabled=travel;
            foreach(var r in foam)r.enabled=travel;
            if(!travel)return;
            if(!passSound){passSound=true;GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.AcidRefluxWavePass);}
            float p=Mathf.Clamp01((phaseAge-BrushWarning)/BrushTravelSeconds);BrushPosition=Vector2.Lerp(from,to,p);
            brush.transform.position=BrushPosition;
            var side=new Vector2(-direction.y,direction.x);
            for(int i=0;i<foam.Count;i++){float u=Mathf.Repeat(phaseAge*2+i/(float)foam.Count,1);var r=foam[i];r.transform.position=BrushPosition+side*((i/(float)(foam.Count-1)-.5f)*BrushWidth*.8f)-direction*u*.55f;r.transform.localScale=Vector3.one*(.08f+u*.15f);r.color=new Color(.92f,1,.98f,(1-u)*.72f);}
            var player=Level.PlayerCharacter;
            if(!playerHit&&!OralStageRuntime.Instance.IsAirborne&&!player.IsDashing&&OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Tongue&&Hits(player.Position))
            {playerHit=true;BrushPlayerHits++;player.TakeDamage(8,direction*1.5f);}
            Snapshot();foreach(var m in scratch)
            {
                if(!Eligible(m)||m.IsFieldRuntimeSuspended||m is BloodClotMonster||m is TrapMonster||OralMapGeometry.ZoneAt(map,m.Position)!=OralZone.Tongue||!Hits(m.Position))continue;
                Vector2 pushed=OralMapGeometry.Clamp(map,BrushPosition+direction*.35f+side*Vector2.Dot(m.Position-BrushPosition,side),.55f,OralZone.Tongue);
                var rb=m.GetComponent<Rigidbody2D>();if(rb!=null){rb.position=pushed;rb.velocity=Vector2.zero;}m.transform.position=new Vector3(pushed.x,pushed.y,m.transform.position.z);
                if(brushHit.Add(m)){BrushMonsterHits++;m.TakeDamage(8);}
            }
            previous=BrushPosition;
        }
        bool Hits(Vector2 p)
        {
            var side=new Vector2(-direction.y,direction.x);float along=Vector2.Dot(p-previous,direction),length=Vector2.Distance(previous,BrushPosition);
            return Mathf.Abs(Vector2.Dot(p-previous,side))<=BrushWidth*.5f&&along>=-.2f&&along<=length+.3f;
        }
        void Snapshot(){scratch.Clear();if(Level?.EntityManager?.LivingMonsters!=null)foreach(var m in Level.EntityManager.LivingMonsters)scratch.Add(m);}
        static bool Eligible(Monster m)=>m!=null&&m.gameObject.activeInHierarchy&&m.HP>0&&!m.IsMiniStageOwned;
        public void EndEvent()
        {
            foreach(var lift in lifts)if(lift!=null)lift.Restore();lifts.Clear();HiccupHolding=false;
            if(brush!=null)brush.enabled=false;if(lane!=null)lane.enabled=false;foreach(var l in hatches)if(l!=null)l.enabled=false;foreach(var r in foam)if(r!=null)r.enabled=false;
        }
        void LateUpdate()
        {
            if(MiniStageRuntimeState.IsInsideMiniStage||StageEntryLoading.IsLoading)
            {if(brush!=null)brush.enabled=false;if(lane!=null)lane.enabled=false;foreach(var l in hatches)l.enabled=false;foreach(var r in foam)r.enabled=false;}
        }
        void OnDisable(){EndEvent();}
        void OnDestroy(){EndEvent();}
    }
}
