using System.Collections;
using UnityEngine;

namespace Vampire
{
    public sealed class AcidToadMonster : MiniBossMonster
    {
        public enum Pattern { Basic, Leap, Jet }
        public Pattern CurrentPattern { get; private set; }
        public bool Busy { get; private set; }
        public bool AutoPatterns = true;
        public static float BodyWidth => (Resources.Load<SnailBossSettings>("SnailBossSettings")?.bodyWidth ?? 4.2f) * .8f;
        FoodAtlasMotion motion;
        Transform mouth;
        float delay, flash;
        int next;
        Coroutine attackRoutine;
        AcidJet jet;
        LineRenderer warning;
        Vector3 bodyOrigin;
        public override void Setup(int index, Vector2 position, MonsterBlueprint blueprint, float hpBuff = 0)
        {
            base.Setup(index,position,blueprint,hpBuff);
            transform.localScale=Vector3.one*(BodyWidth/2.8f);
            if(monsterSpriteAnimator!=null)monsterSpriteAnimator.enabled=false;
            motion=GetComponent<FoodAtlasMotion>()??gameObject.AddComponent<FoodAtlasMotion>();motion.Configure(monsterSpriteRenderer);
            monsterSpriteRenderer.transform.localScale=Vector3.one;
            bodyOrigin=Vector3.zero;monsterSpriteRenderer.transform.localPosition=bodyOrigin;
            monsterSpriteRenderer.sortingOrder=500;
            monsterHitbox.size=new Vector2(2.45f,1.55f);monsterHitbox.offset=new Vector2(0,.8f);
            monsterLegsCollider.radius=.85f;centerTransform.localPosition=new Vector3(0,.85f);
            if(mouth==null){mouth=new GameObject("Acid mouth").transform;mouth.SetParent(transform,false);}
            rb.drag=0;delay=1.5f;next=0;Busy=false;flash=0;
            motion.Play("ToadLocomotion",8,8,1.2f,true);
            var marker=GetComponent<MapMarker>()??gameObject.AddComponent<MapMarker>();
            marker.enabled=true;marker.Configure(MapMarkerKind.Boss,"위산 두꺼비",true);
        }
        protected override void Update()
        {
            if(!alive||IsFieldRuntimeSuspended||MiniStageRuntimeState.IsInsideMiniStage||Time.timeScale<=0)return;
            if(playerCharacter==null)return;
            if(!Busy)
            {
                monsterSpriteRenderer.flipX=playerCharacter.transform.position.x<transform.position.x;
                motion.Play("ToadLocomotion",rb.velocity.sqrMagnitude>.02f?0:8,8,rb.velocity.sqrMagnitude>.02f?.85f:1.2f,true);
                if(AutoPatterns&&(delay-=Time.deltaTime)<=0)UsePattern((Pattern)(next++%3));
            }
            mouth.localPosition=new Vector3(monsterSpriteRenderer.flipX?-.85f:.85f,1.5f,0);
            flash=Mathf.Max(0,flash-Time.deltaTime);
            monsterSpriteRenderer.color=Color.Lerp(Color.white,new Color(1,.6f,.5f),flash/.12f);
        }
        protected override void FixedUpdate()
        {
            if(!alive||IsFieldRuntimeSuspended||MiniStageRuntimeState.IsInsideMiniStage||playerCharacter==null){if(rb!=null)rb.velocity=Vector2.zero;return;}
            Vector2 d=(Vector2)playerCharacter.transform.position-rb.position;
            rb.velocity=!Busy&&d.magnitude>4?d.normalized*.75f*IceMoveMultiplier:Vector2.zero;
            if(entityManager?.Grid!=null)entityManager.Grid.UpdateClient(this);
        }
        public bool UsePattern(Pattern pattern)
        {
            if(!alive||Busy||playerCharacter==null||IsFieldRuntimeSuspended)return false;
            Busy=true;CurrentPattern=pattern;rb.velocity=Vector2.zero;attackRoutine=StartCoroutine(Attack(pattern));return true;
        }
        IEnumerator Wait(float duration)
        {for(float t=0;t<duration;){if(!IsFieldRuntimeSuspended&&!MiniStageRuntimeState.IsInsideMiniStage)t+=Time.deltaTime;yield return null;}}
        IEnumerator Attack(Pattern pattern)
        {
            monsterSpriteRenderer.flipX=playerCharacter.transform.position.x<transform.position.x;
            mouth.localPosition=new Vector3(monsterSpriteRenderer.flipX?-.85f:.85f,1.5f,0);
            if(pattern==Pattern.Leap)yield return Leap();
            else
            {
                Vector2 direction=((Vector2)playerCharacter.transform.position-(Vector2)mouth.position).normalized;
                if(pattern==Pattern.Jet)warning=AcidJetTelegraph.Make(mouth.position,direction,11,1.5f);
                float charge=pattern==Pattern.Jet?1.15f:.6f;
                motion.Play("ToadAttack",0,8,charge);yield return Wait(charge);
                if(warning!=null)Destroy(warning.gameObject);
                if(pattern==Pattern.Basic)
                {
                    motion.Play("ToadAttack",8,8,.5f);
                    var snail=Resources.Load<SnailBossSettings>("SnailBossSettings");float speed=snail!=null?snail.projectileSpeed:3.2f;
                    for(int i=-1;i<=1;i++)AcidGlob.Fire(mouth.position,Quaternion.Euler(0,0,i*14)*direction,speed,8,playerCharacter);
                    yield return Wait(.55f);
                }
                else
                {
                    jet=AcidJet.Create(mouth,direction,11,1.5f,2.1f,8,playerCharacter);
                    for(float t=0;t<2.1f;){if(!IsFieldRuntimeSuspended&&!MiniStageRuntimeState.IsInsideMiniStage){t+=Time.deltaTime;motion.Sample("ToadAttack",8,8,t/2.1f);}yield return null;}
                    if(jet!=null)Destroy(jet.gameObject);
                }
            }
            motion.Play("ToadLocomotion",8,8,1.2f,true);yield return Wait(.55f);Busy=false;delay=2.1f;attackRoutine=null;
        }
        IEnumerator Leap()
        {
            Vector2 from=rb.position;
            Vector2 target=from+Vector2.ClampMagnitude((Vector2)playerCharacter.transform.position-from,7);
            warning=AcidJetTelegraph.Circle(target,1.65f);
            motion.Play("ToadJump",0,3,.9f);yield return Wait(.9f);
            monsterHitbox.enabled=false;monsterLegsCollider.enabled=false;
            for(float t=0;t<.65f;)
            {
                if(!IsFieldRuntimeSuspended&&!MiniStageRuntimeState.IsInsideMiniStage)
                {
                    t+=Time.deltaTime;float f=Mathf.Clamp01(t/.65f);
                    rb.position=Vector2.Lerp(from,target,f);motion.Sample("ToadJump",3,3,f);
                    monsterSpriteRenderer.transform.localPosition=bodyOrigin+Vector3.up*(Mathf.Sin(f*Mathf.PI)*2.7f);
                }
                yield return null;
            }
            rb.position=target;monsterSpriteRenderer.transform.localPosition=bodyOrigin;
            monsterHitbox.enabled=true;monsterLegsCollider.enabled=true;
            if(warning!=null)Destroy(warning.gameObject);
            motion.Play("ToadJump",6,2,.35f);
            AcidLandingRipple.Create(target,1.65f);ToadCameraShake.Play(.25f,.13f);
            if(Vector2.Distance(playerCharacter.transform.position,target)<1.85f)playerCharacter.TakeDamage(16);
            yield return Wait(.4f);
        }
        public override void TakeDamage(float damage,Vector2 knockback=default(Vector2),bool isCritical=false)
        {flash=.12f;base.TakeDamage(damage,knockback*.2f,isCritical);}
        protected override void OnFieldRuntimeSuspended()
        {
            base.OnFieldRuntimeSuspended();Cancel();
        }
        void Cancel()
        {
            if(attackRoutine!=null){StopCoroutine(attackRoutine);attackRoutine=null;}if(jet!=null)Destroy(jet.gameObject);if(warning!=null)Destroy(warning.gameObject);
            Busy=false;delay=1.2f;if(rb!=null)rb.velocity=Vector2.zero;
            if(monsterSpriteRenderer!=null)monsterSpriteRenderer.transform.localPosition=bodyOrigin;
            if(monsterHitbox!=null)monsterHitbox.enabled=alive;
            if(monsterLegsCollider!=null)monsterLegsCollider.enabled=alive;
        }
        public override IEnumerator Killed(bool killedByPlayer=true)
        {
            if(!alive)yield break;
            Cancel();alive=false;
            var marker=GetComponent<MapMarker>();if(marker!=null)marker.enabled=false;
            motion.Sample("ToadJump",6,1,0);yield return Wait(.3f);
            yield return base.Killed(killedByPlayer);
        }
        void OnDisable(){Cancel();}
    }

    public static class AcidJetTelegraph
    {
        public static LineRenderer Make(Vector2 start,Vector2 direction,float length,float width)
        {
            var root=new GameObject("산성액 발사 경고");
            var r=AcidJet.Line(root.transform,new Color(1,.48f,.08f,.4f),width,480);
            r.transform.SetParent(null);Object.Destroy(root);r.positionCount=2;r.SetPosition(0,start);r.SetPosition(1,start+direction*length);return r;
        }
        public static LineRenderer Circle(Vector2 position,float radius)
        {
            var r=Make(position,Vector2.right,0,.09f);r.name="Toad circular landing warning";r.loop=true;r.positionCount=64;
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;r.SetPosition(i,position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius);}
            r.startColor=r.endColor=new Color(1,.32f,.04f,.9f);return r;
        }
    }
    public sealed class AcidLandingRipple:MonoBehaviour
    {
        LineRenderer line;Vector2 origin;float age,radius;
        public static void Create(Vector2 origin,float radius)
        {var line=AcidJetTelegraph.Circle(origin,radius);var ripple=line.gameObject.AddComponent<AcidLandingRipple>();ripple.line=line;ripple.origin=origin;ripple.radius=radius;}
        void Update(){if(MiniStageRuntimeState.IsInsideMiniStage)return;age+=Time.deltaTime;float t=age/.35f;if(t>=1){Destroy(gameObject);return;}for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;line.SetPosition(i,origin+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*(.6f+t*.4f));}line.startColor=line.endColor=new Color(.78f,.9f,.23f,1-t);}
    }
    [DefaultExecutionOrder(10000)]
    public sealed class ToadCameraShake:MonoBehaviour
    {
        float left,total,strength;Vector3 offset,lastApplied;
        public static void Play(float duration,float strength){var c=Camera.main;if(c==null)return;var s=c.GetComponent<ToadCameraShake>()??c.gameObject.AddComponent<ToadCameraShake>();s.left=s.total=duration;s.strength=strength;}
        void LateUpdate(){if((transform.position-lastApplied).sqrMagnitude<.000001f)transform.position-=offset;offset=Vector3.zero;if(Time.timeScale<=0||MiniStageRuntimeState.IsInsideMiniStage)return;if(left<=0)return;left-=Time.deltaTime;offset=(Vector3)(Random.insideUnitCircle*strength*Mathf.Clamp01(left/total));transform.position+=offset;lastApplied=transform.position;}
        void OnDisable(){if((transform.position-lastApplied).sqrMagnitude<.000001f)transform.position-=offset;offset=Vector3.zero;}
    }
}
