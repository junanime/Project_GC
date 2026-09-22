using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    // Visual form and outgoing dash contact only. HP, hurtbox and Character scale stay unchanged.
    [DefaultExecutionOrder(700)]
    public sealed class AriSkillRuntime : MonoBehaviour
    {
        Character owner; CharacterSkillRuntime skill;
        // Same damage layers as the needle prefab, but dash contact needs no weapon/formation.
        int contactLayers;
        SpriteRenderer body,form;
        float bodyWidth,bodyHeight,animationTime,dashTime,transition;
        bool poweredDash,returning,wasActive,hasForm;
        Vector3 footOffset;
        readonly HashSet<int> hitThisDash=new HashSet<int>();
        public bool Transformed => owner!=null&&owner.IsAlive&&(skill.Active||owner.IsDashing&&poweredDash);
        public bool FormVisible => form!=null&&form.enabled;
        public int DashHitCount => hitThisDash.Count;
        public float BaseBodyDiameter => bodyWidth;
        public float AdultBodyDiameter => bodyWidth*skill.Definition.ariVisualScale;
        public float ContactRadius => .5f*(poweredDash?AdultBodyDiameter*skill.Definition.ariPhoenixRange:BaseBodyDiameter*skill.Definition.ariRollWidth);
        public Vector3 VisualScale => form!=null?form.transform.localScale:Vector3.zero;
        public Sprite CurrentFormSprite => form!=null?form.sprite:null;
        public void Bind(Character character,CharacterSkillRuntime runtime)
        {
            contactLayers=LayerMask.GetMask("Monster Full","Chest");
            owner=character;skill=runtime;body=GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            var reference=owner.Blueprint.idleSpriteSequence;
            var sprite=reference!=null&&reference.Length>0?reference[0]:body?.sprite;
            bodyWidth=sprite!=null?sprite.bounds.size.x*Mathf.Abs(body.transform.lossyScale.x):.5f;
            bodyHeight=sprite!=null?sprite.bounds.size.y*Mathf.Abs(body.transform.lossyScale.y):.5f;
            footOffset=body!=null?body.transform.position-transform.position-Vector3.up*(.27f*Mathf.Abs(body.transform.lossyScale.y)):Vector3.zero;
            form=new GameObject("Ari controlled marble phoenix").AddComponent<SpriteRenderer>();
            form.transform.SetParent(transform,false);form.enabled=false;
        }
        public void BeginTransform(){transition=skill.Definition.ariTransformTime;returning=false;wasActive=true;hasForm=true;}
        public void RestoreForm(){transition=0;returning=false;wasActive=skill.Active;hasForm=skill.Active;poweredDash=false;}
        public void BeginDash()
        {
            hitThisDash.Clear();poweredDash=skill.Active;dashTime=0;
            transition=0;returning=false;hasForm=poweredDash;
            Sweep(transform.position,transform.position);
        }
        public void Sweep(Vector2 from,Vector2 to)
        {
            if(owner==null||!owner.IsAlive||!owner.IsDashing||owner.IsTrapBound||Time.timeScale<=0)return;
            var delta=to-from;var direction=delta.sqrMagnitude>.000001f?delta.normalized:owner.LookDirection.normalized;
            float radius=Mathf.Max(.05f,ContactRadius);
            foreach(var c in Physics2D.OverlapCircleAll(from,radius,contactLayers))Hit(c,direction);
            if(delta.sqrMagnitude>.000001f)
                foreach(var hit in Physics2D.CircleCastAll(from,radius,direction,delta.magnitude,contactLayers))Hit(hit.collider,direction);
        }
        void Hit(Collider2D collider,Vector2 direction)
        {
            if(!SyringeSpecialHitEffectUtility.TryGetValidDamageableTarget(collider,owner,out _,out var target,out int id)
                ||target is Monster suspended&&suspended.IsFieldRuntimeSuspended||Ver4HitEffects.Health(target)<=0||!hitThisDash.Add(id))return;
            var d=skill.Definition;
            Ver4HitEffects.Damage(target,poweredDash?d.ariPhoenixDamage:d.ariRollDamage,Vector2.zero,owner,poweredDash?"나 멋지지":"데굴데굴");
            if(Ver4HitEffects.Health(target)<=0||Ver4HitEffects.IsBoss(target)||target is SniperMonster||target is TrapMonster||target is BloodClotMonster)return;
            var rb=target.GetComponent<Rigidbody2D>();
            if(rb==null||rb.bodyType!=RigidbodyType2D.Dynamic||(rb.constraints&RigidbodyConstraints2D.FreezePosition)!=0)return;
            var push=target.GetComponent<AriDashPush>();if(push==null)push=target.gameObject.AddComponent<AriDashPush>();
            push.Begin(rb,direction,poweredDash?d.ariPhoenixPush:d.ariRollPush);
        }
        void LateUpdate()
        {
            if(body==null||form==null)return;
            if(!owner.IsAlive||owner.IsTrapBound)
            {transition=0;hasForm=false;form.enabled=false;body.forceRenderingOff=false;wasActive=skill.Active;return;}
            var d=skill.Definition;float dt=Time.deltaTime;
            animationTime+=dt;if(owner.IsDashing)dashTime+=dt;
            if(Transformed)hasForm=true;
            if(!Transformed&&hasForm&&!returning)
            {returning=true;transition=d.ariTransformTime;}
            if(skill.Active&&!wasActive){returning=false;transition=0;hasForm=true;}
            wasActive=skill.Active;
            Sprite[] frames=null;int frame=0;
            if(owner.IsDashing&&poweredDash)
            {
                frames=d.ariPhoenixDash;
                frame=Mathf.Min(7,Mathf.FloorToInt(dashTime/Mathf.Max(.01f,owner.DashDuration)*8));
            }
            else if(transition>0)
            {
                frames=d.ariPhoenixTransform;
                int step=Mathf.Min(7,Mathf.FloorToInt((1-transition/d.ariTransformTime)*8));
                frame=returning?7-step:step;transition=Mathf.Max(0,transition-dt);
                if(transition<=0&&returning){returning=false;hasForm=false;}
            }
            else if(skill.Active)
            {frames=owner.Velocity.sqrMagnitude>.0001f?d.ariPhoenixWalk:d.ariPhoenixIdle;frame=(int)(animationTime*(owner.Velocity.sqrMagnitude>.0001f?8:6));}
            bool show=hasForm&&body.enabled&&frames!=null&&frames.Length>0;
            body.forceRenderingOff=show;form.enabled=show;if(!show)return;
            form.sprite=frames[frame%frames.Length];form.flipX=owner.LookDirection.x<0;
            form.sortingLayerID=body.sortingLayerID;form.sortingOrder=body.sortingOrder;form.color=body.color;
            // Every pose is anatomically calibrated at import. No sprite-bounds auto-fit.
            float scale=bodyHeight*d.ariVisualScale;
            form.transform.localScale=new Vector3(scale/Mathf.Abs(transform.lossyScale.x),scale/Mathf.Abs(transform.lossyScale.y),1);
            form.transform.position=transform.position+footOffset;form.transform.rotation=Quaternion.identity;
        }
        void OnDisable(){if(body!=null)body.forceRenderingOff=false;if(form!=null)form.enabled=false;hitThisDash.Clear();poweredDash=hasForm=false;transition=0;}
    }
}
