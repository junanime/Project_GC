using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed partial class FoodBossRuntime:IDamageable
    {
        public FoodBossKind Kind {get;private set;}public Character Player {get;private set;}
        public float Health {get;private set;}public float MaxHealth {get;private set;}
        public bool Dead {get;private set;}public bool PhaseTwo {get;private set;}public bool Transitioning {get;private set;}
        public bool AutoPatterns=true;public bool Busy=>action!=null;public bool Moving {get;private set;}
        public bool Recovering {get;private set;}public int Reversals {get;private set;}
        public FoodBossAttack CurrentAttack {get;private set;}public FoodBossVisual Visual {get;private set;}
        public FoodBossAudio Audio {get;private set;}
        public FoodBossPhaseVisual PhaseVisual {get;private set;}
        public Transform Effects {get;private set;}public Color Warning=>FoodBossArt.Warnings[(int)Kind];
        public static bool Paused=>Time.timeScale<=0||MiniStageRuntimeState.IsInsideMiniStage;
        public const float BasicSpeed=1.8f,BasicGap=.55f;public const int RingCount=12;
        Rigidbody2D body;Collider2D hitbox;Coroutine action;float nextAction=2,age,damageReceived,nextContact,recoveryHealed,nextReverse;
        int sequence,basicSequence;bool recoveryUsed,suspended;Vector2 recoveryDirection;LevelManager level;
        readonly List<FoodBossWeakpoint> weakpoints=new List<FoodBossWeakpoint>();
        public static FoodBossRuntime Spawn(FoodBossKind kind,Character player,Vector2 point,Transform parent=null)
        {
            var go=new GameObject(FoodBossArt.Names[(int)kind]);go.transform.SetParent(parent,true);go.transform.position=FiniteStomachMap.SafeSpawn(point,2.4f);
            go.layer=LayerMask.NameToLayer("Monster Full");
            var b=go.AddComponent<FoodBossRuntime>();b.Initialize(kind,player);return b;
        }
        public void Initialize(FoodBossKind kind,Character player)
        {
            Kind=kind;Player=player;level=FindObjectOfType<LevelManager>();MaxHealth=24000*(kind==FoodBossKind.Denikani?1.1f:kind==FoodBossKind.Maribi?.9f:1);Health=MaxHealth;
            body=GetComponent<Rigidbody2D>();body.gravityScale=0;body.constraints=RigidbodyConstraints2D.FreezeRotation;body.drag=0;
            var col=gameObject.AddComponent<CapsuleCollider2D>();col.direction=CapsuleDirection2D.Horizontal;col.size=new Vector2(FoodBossArt.Widths[(int)kind]*.72f,1.45f);col.offset=Vector2.up*.65f;col.isTrigger=true;hitbox=col;
            StomachSlopeBody.Ensure(gameObject);Effects=new GameObject(name+" patterns").transform;
            Visual=new GameObject("Boss articulated visual").AddComponent<FoodBossVisual>();Visual.transform.SetParent(transform,false);Visual.Initialize(this);
            Audio=gameObject.AddComponent<FoodBossAudio>();Audio.Initialize(this);
            PhaseVisual=gameObject.AddComponent<FoodBossPhaseVisual>();PhaseVisual.Initialize(this);
            gameObject.AddComponent<MapMarker>().Configure(MapMarkerKind.Boss,name,true);
            gameObject.AddComponent<FoodBossHealthHud>().Initialize(this);
        }
        void Update()
        {
            if(Dead||Player==null)return;
            if(MiniStageRuntimeState.IsInsideMiniStage||level!=null&&level.IsLevelEnded)
            {if(!suspended){suspended=true;Cancel();ClearEffects();}body.velocity=Vector2.zero;return;}
            if(suspended){suspended=false;nextAction=1.5f;}
            if(Paused)return;age+=Time.deltaTime;
            if(Busy||Transitioning)return;
            Vector2 delta=(Vector2)Player.transform.position-(Vector2)transform.position;Visual.Face(delta);
            Vector2 beforeMove=transform.position;
            if(delta.magnitude>4.5f)MoveToward(beforeMove+delta.normalized*.7f*Time.deltaTime);
            SetChasing(((Vector2)transform.position-beforeMove).sqrMagnitude>1e-10f);
            nextAction-=Time.deltaTime;
            if(AutoPatterns&&nextAction<=0)
            {
                if(!PhaseTwo&&!recoveryUsed&&Health<=MaxHealth*.72f){recoveryUsed=true;Play(FoodBossAttack.Recovery);return;}
                FoodBossAttack[] order={FoodBossAttack.Basic,FoodBossAttack.Dash,FoodBossAttack.Basic,FoodBossAttack.UniqueOne,FoodBossAttack.Radial,FoodBossAttack.UniqueTwo};
                Play(order[sequence++%order.Length]);
            }
        }
        void SetChasing(bool moving)
        {
            bool starting=moving&&!Moving;Moving=moving;
            // One chance per actual movement start, never once per moving frame.
            if(starting)Audio.TryChaseCry();
        }
        public bool Play(FoodBossAttack attack)
        {
            if(Dead||Busy||Transitioning||Paused||attack==FoodBossAttack.Recovery&&PhaseTwo)return false;
            CurrentAttack=attack;SetChasing(false);Audio.FadeCries();body.velocity=Vector2.zero;action=StartCoroutine(Execute(attack));return true;
        }
        IEnumerator Execute(FoodBossAttack attack)
        {
            // Yield first so even an immediate completion cannot leave a stale Coroutine handle.
            yield return null;
            switch(attack){case FoodBossAttack.Basic:yield return Basic();break;case FoodBossAttack.Radial:yield return Radial();break;case FoodBossAttack.Dash:yield return Dash();break;case FoodBossAttack.UniqueOne:yield return UniqueOne();break;case FoodBossAttack.UniqueTwo:yield return UniqueTwo();break;case FoodBossAttack.Recovery:Audio.Play(FoodBossSound.Recover);yield return Recovery();break;}
            Visual.ResetPose();hitbox.enabled=true;Recovering=false;Moving=false;body.velocity=Vector2.zero;action=null;nextAction=PhaseTwo?1.45f:1.9f;
        }
        public static Vector2 Dir(float a)=>new Vector2(Mathf.Cos(a*Mathf.Deg2Rad),Mathf.Sin(a*Mathf.Deg2Rad));
        public static Vector2 Rotate(Vector2 v,float angle)=>(Vector2)(Quaternion.Euler(0,0,angle)*v);
        public Vector2 Aim=>Player!=null?((Vector2)Player.transform.position-(Vector2)transform.position).normalized:Vector2.down;
        public Vector2 Mouth=>Visual!=null?Visual.ShotOrigin:(Vector2)transform.position+Vector2.up*.65f;
        public Vector2 FireAim=>Player!=null?((Vector2)Player.transform.position-Mouth).normalized:Aim;
        public IEnumerator Wait(float seconds){for(float t=0;t<seconds;){if(!Paused)t+=Time.deltaTime;yield return null;}}
        void MoveToward(Vector2 target){var at=(Vector2)transform.position;float factor=GetComponent<StomachSlopeBody>().MovementMultiplier(target-at);factor*=GetComponent<IceChillStatus>()?.SpeedMultiplier??1;factor*=GetComponent<FoodBossCombatStatus>()?.SpeedMultiplier??1;target=at+(target-at)*factor;target=FiniteStomachMap.ClampDash(at,target,1.25f);body.position=target;transform.position=target;}
        IEnumerator Basic()
        {
            bool signature=(basicSequence++%2)==1;
            Visual.Face(Aim);
            yield return Wait(.45f);
            if(!signature)
            {
                // Familiar aimed bursts alternate with the boss-specific pattern.
                for(int i=0;i<(PhaseTwo?5:3);i++)
                {
                    Visual.Face(Aim);Vector2 dir=FireAim;Visual.Recoil=-.10f;
                    Audio.Play(FoodBossSound.Shot);FoodBossBullet.Fire(this,Mouth,dir,BasicSpeed,8);
                    yield return Wait(.12f);Visual.Recoil=0;yield return Wait(BasicGap-.12f);
                }
                yield break;
            }
            switch(Kind)
            {
                case FoodBossKind.Rolpangi:yield return SpiralBasic();break;
                case FoodBossKind.Denikani:yield return ClawBasic();break;
                case FoodBossKind.Maribi:yield return RhythmBasic();break;
                case FoodBossKind.Tangdengi:yield return SkewerBasic();break;
                case FoodBossKind.Hurumi:yield return WaveBasic();break;
            }
        }
        IEnumerator Radial()
        {
            Vector2 at=(Vector2)transform.position+Vector2.up*.6f;var ring=FoodBossEffects.Ring(this,at,1.5f);yield return Wait(.85f);Destroy(ring.gameObject);
            for(int n=0;n<(PhaseTwo?2:1);n++){Audio.Play(FoodBossSound.Radial);for(int i=0;i<RingCount;i++){var d=Dir(i*360f/RingCount+n*180f/RingCount);FoodBossBullet.Fire(this,at+d*1.1f,d,1.7f,8);}yield return Wait(.7f);}
        }
        public override void TakeDamage(float damage,Vector2 knockback=default,bool isCritical=false)
        {
            if(Dead||Transitioning||Paused||damage<=0||float.IsNaN(damage)||float.IsInfinity(damage)||Health<=0)return;
            if(Recovering)
            {
                if(Kind==FoodBossKind.Denikani){if(SuppressHitFlash)return;Vector2 source=knockback.sqrMagnitude>.01f?-knockback.normalized:Player!=null?((Vector2)Player.transform.position-(Vector2)transform.position).normalized:Vector2.zero;
                    if(Time.time>=nextReverse&&Vector2.Dot(source,recoveryDirection)>.25f){recoveryDirection=-recoveryDirection;Reversals++;nextReverse=Time.time+.65f;Audio.Play(FoodBossSound.Break);FoodBossEffects.Burst(this,transform.position,2);}
                    return;}
                if(Kind==FoodBossKind.Maribi||Kind==FoodBossKind.Tangdengi)return;
            }
            var status=GetComponent<OctoberItemTargetStatus>();if(status!=null&&status.VulnerableUntil>Time.time&&!SuppressHitFlash)damage*=1.08f;
            float old=Health;Health=Mathf.Max(0,Health-damage);damageReceived+=old-Health;
            if(Health<=0){Cancel();Dead=true;hitbox.enabled=false;ClearEffects();GetComponent<FoodBossCombatStatus>()?.Defeated();RunScore.Current?.BossKill();StartCoroutine(Death());}
            else if(!PhaseTwo&&Health<=MaxHealth*.5f){Cancel();ClearEffects();action=StartCoroutine(Transition());}
            else if(Health<old)Audio.TryHurtCry();
        }
        public override void Knockback(Vector2 knockback){}
        public void SetPhaseTwoForTest(){if(PhaseTwo)return;TakeDamage(Mathf.Max(1,Health-MaxHealth*.49f));}
        IEnumerator Transition()
        {
            Transitioning=true;PhaseVisual.Begin();
            float duration=FoodBossPhaseVisual.Duration(Kind);
            for(float t=0;t<duration;){if(!Paused){t+=Time.deltaTime;PhaseVisual.SetProgress(t/duration);}yield return null;}
            PhaseTwo=true;basicSequence=0;Visual.ResetPose();PhaseVisual.End();
            Transitioning=false;action=null;nextAction=.5f;
        }
        void Cancel(){if(action!=null)StopCoroutine(action);action=null;if(Audio!=null)Audio.Stop();if(PhaseVisual!=null)PhaseVisual.End();Recovering=false;Transitioning=false;Moving=false;if(body!=null)body.velocity=Vector2.zero;if(hitbox!=null)hitbox.enabled=true;if(Visual!=null)Visual.ResetPose();if(Player!=null)Player.GetComponent<SnailSlowStatus>()?.Exit(GetInstanceID(),0);}
        void ClearEffects(){weakpoints.Clear();if(Effects!=null)for(int i=Effects.childCount-1;i>=0;i--){var g=Effects.GetChild(i).gameObject;g.SetActive(false);Destroy(g);}}
        IEnumerator Death()
        {
            Audio.Play(FoodBossSound.Death);
            body.velocity=Vector2.zero;body.simulated=false;
            Visual.enabled=false;
            if(level!=null&&!level.IsLevelEnded)yield return BossEscapePresentation.Play(level,Kind,Visual.transform,transform.position);
            if(level!=null&&!level.IsLevelEnded&&!StageProgression.Ensure(level).FinalBossDefeated(transform.position,damageReceived/Mathf.Max(20,age),Kind))level.LevelPassed(null);
            Destroy(gameObject);
        }
        void OnTriggerStay2D(Collider2D other){if(Dead||Paused||Recovering||Transitioning||Visual.Airborne||Time.time<nextContact)return;var p=other.GetComponentInParent<Character>();if(p==null)return;p.TakeDamage(12);nextContact=Time.time+.9f;}
        void OnDisable(){Cancel();ClearEffects();}
        void OnDestroy(){if(Effects!=null)Destroy(Effects.gameObject);}
        void Heal(float fraction){float amount=Mathf.Min(MaxHealth*fraction,MaxHealth*.08f-recoveryHealed);if(amount<=0||Dead||PhaseTwo)return;Health=Mathf.Min(MaxHealth,Health+amount);recoveryHealed+=amount;}
    }
}
