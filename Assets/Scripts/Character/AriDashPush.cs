using UnityEngine;
namespace Vampire
{
    // Distance-limited knockback independent of enemy drag. Pooled enemies clear pending motion.
    [DefaultExecutionOrder(1000)]
    public sealed class AriDashPush : MonoBehaviour
    {
        Rigidbody2D body;Vector2 direction;float remaining,speed;
        readonly RaycastHit2D[] hits=new RaycastHit2D[64];
        public float RemainingDistance=>remaining;
        public void Begin(Rigidbody2D rb,Vector2 heading,float distance)
        {body=rb;direction=heading.normalized;remaining=Mathf.Max(0,distance);speed=remaining/.12f;enabled=true;}
        void FixedUpdate()
        {
            if(body==null||remaining<=0||GetComponent<Monster>() is Monster m&&(m.HP<=0||m.IsFieldRuntimeSuspended))
            {if(body!=null)body.velocity=Vector2.zero;enabled=false;return;}
            float step=Mathf.Min(remaining,speed*Time.fixedDeltaTime);
            var filter=new ContactFilter2D();filter.NoFilter();filter.useTriggers=false;
            int count=body.Cast(direction,filter,hits,step);
            for(int i=0;i<count;i++)
            {
                var c=hits[i].collider;
                if(c==null||c.attachedRigidbody==body||c.GetComponentInParent<Character>()!=null)continue;
                if(c.GetComponentInParent<BloodClotObstacle>()==null&&c.GetComponentInParent<IDamageable>()!=null)continue;
                step=Mathf.Min(step,Mathf.Max(0,hits[i].distance-.02f));
            }
            body.velocity=Vector2.zero;body.MovePosition(body.position+direction*step);
            remaining=Mathf.Max(0,remaining-step);if(step<=.0001f)remaining=0;
        }
        void OnDisable(){remaining=0;}
    }
}
