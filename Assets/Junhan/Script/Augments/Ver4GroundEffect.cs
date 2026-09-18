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
        private Material material;
        public static void Create(Vector2 position,float duration,bool healing,float damage,Character source,LayerMask layer)
        {
            var obj=new GameObject(healing ? "목침 회복 장판" : "빙결침 냉기 장판"); obj.transform.position=position;
            var effect=obj.AddComponent<Ver4GroundEffect>(); effect.end=Time.time+duration;
            effect.nextTick=Time.time+.25f; effect.healing=healing; effect.damage=damage; effect.source=source; effect.layer=layer;
            effect.syringe=SyringeAbilityResolver.FindOwnedOrFirst(FindObjectOfType<AbilityManager>());
            var line=obj.AddComponent<LineRenderer>(); line.useWorldSpace=false; line.loop=true; line.positionCount=24;
            line.startWidth=line.endWidth=.07f;
            effect.material=new Material(Shader.Find("Sprites/Default")); line.sharedMaterial=effect.material;
            line.startColor=line.endColor=healing ? new Color(.4f,1,.4f,.7f) : new Color(.4f,.8f,1,.7f);
            for(int i=0;i<24;i++) line.SetPosition(i,new Vector3(Mathf.Cos(i*Mathf.PI/12),Mathf.Sin(i*Mathf.PI/12),0));
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
        private void OnDestroy() { if(material!=null) Destroy(material); }
    }
}
