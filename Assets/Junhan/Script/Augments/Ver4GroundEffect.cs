using UnityEngine;
namespace Vampire
{
    public sealed class Ver4GroundEffect : MonoBehaviour
    {
        private float end,nextTick,damage;
        private bool healing;
        private Character source;
        private SyringeDartAbility syringe;
        private LayerMask layer;
        public static void Create(Vector2 position,float duration,bool healing,float damage,Character source,LayerMask layer)
        {
            var obj=new GameObject(healing ? "목침 회복 장판" : "빙결침 냉기 장판"); obj.transform.position=position;
            var effect=obj.AddComponent<Ver4GroundEffect>(); effect.end=Time.time+duration;
            effect.nextTick=Time.time+.25f; effect.healing=healing; effect.damage=damage; effect.source=source; effect.layer=layer;
            effect.syringe=SyringeAbilityResolver.FindOwnedOrFirst(FindObjectOfType<AbilityManager>());
            if(!healing)
            {
                obj.AddComponent<ColdGroundVisual>().Initialize(duration);
                return;
            }
            HerbalGroundVisual.Create(obj.transform, position, 1, duration, 1);
        }
        private void Update()
        {
            if(source==null || Time.time>end) { Destroy(gameObject); return; }
            if(Time.time<nextTick) return; nextTick+=.25f;
            if(healing)
            {
                if(Vector2.Distance(transform.position,source.transform.position)<=1)
                {
                    if(syringe==null || !syringe.HasLifeBurnLegendary()) source.GainHealth(source.MaxHealth*.01f*.25f);
                }
            }
            else foreach(var target in Ver4HitEffects.Nearby(transform.position,1,layer,source))
            {
                if(target is Monster)
                {
                    var slow=target.GetComponent<HoneySlowStatus>()??target.gameObject.AddComponent<HoneySlowStatus>(); slow.Apply(.3f,.8f);
                }
                Ver4HitEffects.Damage(target,damage*.25f,Vector2.zero,source,"빙결침 냉기",true);
            }
        }
    }
}
