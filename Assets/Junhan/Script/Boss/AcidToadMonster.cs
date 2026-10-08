using System.Collections;
using UnityEngine;

namespace Vampire
{
    // Keep the historical component GUID so every saved level and pool still references this actor.
    public sealed class AcidToadMonster : MiniBossMonster
    {
        public enum Pattern { Basic, Leap, Jet }
        static readonly Pattern[] AttackCycle = { Pattern.Basic, Pattern.Leap, Pattern.Basic, Pattern.Jet };
        public Pattern CurrentPattern { get; private set; }
        public ChameleonKind Kind { get; private set; }
        public bool Busy { get; private set; }
        public bool Invisible { get; private set; }
        public int DriftPulses { get; private set; }
        public bool AutoPatterns=true;
        public static float BodyWidth => ChameleonSettings.Current != null ? ChameleonSettings.Current.bodyWidth : 3.36f;
        public ChameleonMotion Motion => motion;
        ChameleonMotion motion;Transform mouth,effects;float delay,flash;int next;
        Coroutine attackRoutine;SpriteRenderer warning;Material outlineMaterial;Vector3 bodyOrigin;
        float bodyAlpha=1;bool dying;
        public override void Setup(int index,Vector2 position,MonsterBlueprint blueprint,float hpBuff=0)
        {
            base.Setup(index,position,blueprint,hpBuff);Cancel();Kind=ChameleonArt.Kind(blueprint);dying=false;
            transform.localScale=Vector3.one*(BodyWidth/ChameleonArt.Width);
            if(monsterSpriteAnimator!=null)monsterSpriteAnimator.enabled=false;
            var legacy=GetComponent<FoodAtlasMotion>();if(legacy!=null)legacy.enabled=false;
            motion=GetComponent<ChameleonMotion>()??gameObject.AddComponent<ChameleonMotion>();motion.Configure(monsterSpriteRenderer,Kind);
            monsterSpriteRenderer.transform.localScale=Vector3.one;bodyOrigin=Vector3.zero;monsterSpriteRenderer.transform.localPosition=bodyOrigin;monsterSpriteRenderer.sortingOrder=500;
            monsterHitbox.size=new Vector2(2.45f,1.55f);monsterHitbox.offset=new Vector2(0,.8f);monsterLegsCollider.radius=.85f;centerTransform.localPosition=new Vector3(0,.85f);
            if(mouth==null){mouth=new GameObject("Chameleon mouth").transform;mouth.SetParent(transform,false);}
            rb.drag=0;delay=1.5f;next=0;Busy=false;Invisible=false;flash=0;bodyAlpha=1;DriftPulses=0;
            motion.Play(0,2,1.2f,true);
            var marker=GetComponent<MapMarker>()??gameObject.AddComponent<MapMarker>();marker.enabled=true;marker.Configure(MapMarkerKind.Boss,ChameleonArt.Names[(int)Kind],true);
        }
        protected override void Update()
        {
            if(!alive||IsFieldRuntimeSuspended||ChameleonTime.Paused||playerCharacter==null)return;
            if(!Busy)
            {
                monsterSpriteRenderer.flipX=playerCharacter.transform.position.x<transform.position.x;
                motion.Play(rb.velocity.sqrMagnitude>.02f?2:0,rb.velocity.sqrMagnitude>.02f?4:2,rb.velocity.sqrMagnitude>.02f?.7f:1.2f,true);
                if(AutoPatterns&&(delay-=Time.deltaTime)<=0)
                {
                    if(UsePattern(AttackCycle[next]))next=(next+1)%AttackCycle.Length;
                }
            }
            UpdateMouth();
            flash=Mathf.Max(0,flash-Time.deltaTime);var color=Color.Lerp(Color.white,new Color(1,.65f,.6f),flash/.12f);color.a=bodyAlpha;monsterSpriteRenderer.color=color;
            if(warning!=null)warning.flipX=monsterSpriteRenderer.flipX;
        }
        protected override void FixedUpdate()
        {
            if(!alive||IsFieldRuntimeSuspended||ChameleonTime.Paused||playerCharacter==null){if(rb!=null)rb.velocity=Vector2.zero;return;}
            Vector2 d=(Vector2)playerCharacter.transform.position-rb.position;rb.velocity=!Busy&&d.magnitude>4?d.normalized*Blueprint.movespeed*IceMoveMultiplier:Vector2.zero;
            if(entityManager?.Grid!=null)entityManager.Grid.UpdateClient(this);
        }
        public bool UsePattern(Pattern pattern)
        {
            if(!alive||dying||Busy||playerCharacter==null||IsFieldRuntimeSuspended||ChameleonTime.Paused)return false;
            Busy=true;CurrentPattern=pattern;rb.velocity=Vector2.zero;attackRoutine=StartCoroutine(Attack(pattern));return true;
        }
        void UpdateMouth()
        {
            if(mouth==null||motion?.Art.sprite==null)return;
            bool upward=Kind==ChameleonKind.Latte&&motion.FrameIndex==11;
            Vector2 size=motion.Art.sprite.bounds.size;
            mouth.localPosition=new Vector3((monsterSpriteRenderer.flipX?-1:1)*size.x*(upward?.17f:.32f),size.y*(upward?.84f:.46f),0);
        }
        IEnumerator Attack(Pattern pattern)
        {
            monsterSpriteRenderer.flipX=playerCharacter.transform.position.x<transform.position.x;
            UpdateMouth();
            if(pattern==Pattern.Leap)yield return Teleport();
            else if(pattern==Pattern.Basic)
            {
                motion.Sample(6);yield return ChameleonTime.Wait(.6f);
                motion.Sample(7);UpdateMouth();Vector2 direction=((Vector2)playerCharacter.transform.position-(Vector2)mouth.position).normalized;
                var settings=ChameleonSettings.Current;
                for(int i=-1;i<=1;i++)ChameleonProjectile.Fire(Kind,mouth.position,Quaternion.Euler(0,0,i*14)*direction,settings.projectileSpeed,settings.projectileDamage,playerCharacter);
                yield return ChameleonTime.Wait(.45f);
            }
            else yield return Special();
            motion.Sample(12);yield return ChameleonTime.Wait(.5f);motion.Play(0,2,1.2f,true);Busy=false;delay=2.1f;attackRoutine=null;
        }
        Transform Effects
        {
            get{if(effects==null){effects=new GameObject("Owned chameleon skills").transform;effects.SetParent(transform,false);}return effects;}
        }
        IEnumerator Special()
        {
            if(Kind==ChameleonKind.Drift)
            {
                DriftPulses=0;motion.Sample(8);yield return ChameleonTime.Wait(.45f);ToadCameraShake.Play(.15f,.06f);
                motion.Sample(9);yield return ChameleonTime.Wait(.45f);ToadCameraShake.Play(.15f,.06f);
                for(int i=0;i<2;i++)
                {
                    motion.Sample(10);yield return ChameleonTime.Wait(.5f);motion.Sample(11);
                    GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.PeristalsisTilt);
                    ChameleonDriftPulse.Create(Effects,playerCharacter,i==0?Vector2.left:Vector2.right,2.2f);DriftPulses++;
                    yield return ChameleonTime.Wait(2.2f);
                }
            }
            else if(Kind==ChameleonKind.Foam)
            {
                motion.Sample(8);yield return ChameleonTime.Wait(.3f);motion.Sample(9);yield return ChameleonTime.Wait(.45f);motion.Sample(10);yield return ChameleonTime.Wait(.45f);
                motion.Sample(11);ChameleonFoamSkill.Create(Effects,playerCharacter);yield return ChameleonTime.Wait(.5f);
            }
            else if(Kind==ChameleonKind.Latte)
            {
                motion.Sample(8);yield return ChameleonTime.Wait(.3f);motion.Sample(9);yield return ChameleonTime.Wait(.3f);motion.Sample(10);yield return ChameleonTime.Wait(.35f);
                motion.Sample(11);UpdateMouth();ChameleonCoffeeFountain.Create(this,mouth.position,playerCharacter).transform.SetParent(Effects,true);yield return ChameleonTime.Wait(1.9f);
            }
            else
            {
                Vector2 direction=((Vector2)playerCharacter.transform.position-(Vector2)mouth.position).normalized;
                var lane=AcidJetTelegraph.Make(mouth.position,direction,11,1.5f);lane.transform.SetParent(Effects,true);
                motion.Sample(9);yield return ChameleonTime.Wait(.5f);motion.Sample(10);yield return ChameleonTime.Wait(.65f);
                Destroy(lane.gameObject);motion.Sample(11);
                ChameleonSodaWave.Fire(mouth.position,(Vector2)mouth.position+direction*11,2.1f,1.5f,8,playerCharacter).transform.SetParent(Effects,true);
                yield return ChameleonTime.Wait(.6f);
            }
        }
        IEnumerator Teleport()
        {
            motion.Sample(0);monsterHitbox.enabled=false;monsterLegsCollider.enabled=false;rb.simulated=false;Invisible=true;
            for(float t=0;t<.35f;){if(!ChameleonTime.Paused){t+=Time.deltaTime;SetAlpha(1-t/.35f);}yield return null;}
            SetAlpha(0);if(shadow!=null)shadow.SetActive(false);
            yield return ChameleonTime.Wait(ChameleonSettings.Current.invisibleSeconds);
            // Snapshot the player's location once. Never chase them during the one-second tell.
            Vector2 at=playerCharacter.CenterTransform!=null?playerCharacter.CenterTransform.position:playerCharacter.transform.position;
            Vector2 offset=monsterSpriteRenderer.transform.TransformVector(monsterSpriteRenderer.sprite.bounds.center);
            rb.position=at-offset;transform.position=rb.position;
            var go=new GameObject("Chameleon silhouette warning");go.transform.SetParent(monsterSpriteRenderer.transform,false);
            warning=go.AddComponent<SpriteRenderer>();warning.sprite=monsterSpriteRenderer.sprite;warning.flipX=monsterSpriteRenderer.flipX;warning.sortingOrder=540;
            if(outlineMaterial==null)outlineMaterial=new Material(Resources.Load<Shader>("Chameleons/Outline"));
            warning.sharedMaterial=outlineMaterial;warning.color=ChameleonArt.Accent(Kind);
            yield return ChameleonTime.Wait(ChameleonSettings.Current.outlineSeconds);
            Destroy(go);warning=null;Invisible=false;SetAlpha(1);if(shadow!=null)shadow.SetActive(true);
            rb.simulated=true;monsterHitbox.enabled=true;monsterLegsCollider.enabled=true;Physics2D.SyncTransforms();
            if(playerCharacter.IsAlive&&OccupiesSilhouette(playerCharacter.CenterTransform!=null?playerCharacter.CenterTransform.position:playerCharacter.transform.position))playerCharacter.TakeDamage(ChameleonSettings.Current.teleportDamage);
            ToadCameraShake.Play(.15f,.08f);yield return ChameleonTime.Wait(.3f);
        }
        public bool OccupiesSilhouette(Vector2 point)
        {
            var s=monsterSpriteRenderer.sprite;if(s==null)return false;Vector3 local=monsterSpriteRenderer.transform.InverseTransformPoint(point);if(monsterSpriteRenderer.flipX)local.x=-local.x;
            Vector2 pixel=(Vector2)local*s.pixelsPerUnit+s.pivot;
            if(pixel.x<0||pixel.y<0||pixel.x>=s.rect.width||pixel.y>=s.rect.height)return false;
            return ChameleonArt.OccupiesSilhouette(s,pixel);
        }
        void SetAlpha(float alpha){bodyAlpha=Mathf.Clamp01(alpha);if(monsterSpriteRenderer!=null){var c=monsterSpriteRenderer.color;c.a=bodyAlpha;monsterSpriteRenderer.color=c;}}
        public override void TakeDamage(float damage,Vector2 knockback=default(Vector2),bool isCritical=false)
        {if(Invisible||dying)return;flash=.12f;base.TakeDamage(damage,knockback*.2f,isCritical);}
        protected override void OnFieldRuntimeSuspended(){base.OnFieldRuntimeSuspended();Cancel();}
        protected override void OnFieldRuntimeResumed(){base.OnFieldRuntimeResumed();if(rb!=null)rb.simulated=true;}
        void Cancel()
        {
            if(attackRoutine!=null){StopCoroutine(attackRoutine);attackRoutine=null;}
            if(warning!=null)Destroy(warning.gameObject);if(effects!=null){Destroy(effects.gameObject);effects=null;}
            Busy=false;Invisible=false;delay=1.2f;if(rb!=null){rb.velocity=Vector2.zero;if(!IsFieldRuntimeSuspended)rb.simulated=true;}SetAlpha(1);
            if(monsterSpriteRenderer!=null)monsterSpriteRenderer.transform.localPosition=bodyOrigin;
            if(monsterHitbox!=null)monsterHitbox.enabled=alive;if(monsterLegsCollider!=null)monsterLegsCollider.enabled=alive;if(shadow!=null)shadow.SetActive(alive);
        }
        public override IEnumerator Killed(bool killedByPlayer=true)
        {
            if(!alive||dying)yield break;Cancel();dying=true;alive=false;
            monsterHitbox.enabled=false;monsterLegsCollider.enabled=false;
            var marker=GetComponent<MapMarker>();if(marker!=null)marker.enabled=false;
            motion.Sample(13);yield return ChameleonTime.Wait(.12f);motion.Sample(14);yield return ChameleonTime.Wait(.3f);motion.Sample(15);yield return ChameleonTime.Wait(.5f);
            yield return base.Killed(killedByPlayer);
        }
        void OnDisable(){Cancel();}
        void OnDestroy(){if(outlineMaterial!=null)Destroy(outlineMaterial);}
    }

    public static class AcidJetTelegraph
    {
        public static LineRenderer Make(Vector2 start,Vector2 direction,float length,float width)
        {
            var root=new GameObject("탄산액 발사 경고");
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
        void Update(){if(MiniStageRuntimeState.IsInsideMiniStage)return;age+=Time.deltaTime;float t=age/.35f;if(t>=1){Destroy(gameObject);return;}for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;line.SetPosition(i,origin+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*(.6f+t*.4f));}line.startColor=line.endColor=new Color(1,.58f,.12f,1-t);}
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
