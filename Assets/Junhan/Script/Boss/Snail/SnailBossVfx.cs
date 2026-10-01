using UnityEngine;
namespace Vampire
{
    public sealed class SnailBossVfx : MonoBehaviour
    {
        Vector2 velocity;float life,remaining;SpriteRenderer sr;bool warning;float width;
        public static void Puff(SnailBossRuntime owner,string key,Vector2 pos,float size,float lifetime)
        {Make(owner,key,pos,size,lifetime,Vector2.zero);}
        static SnailBossVfx Make(SnailBossRuntime owner,string key,Vector2 pos,float size,float lifetime,Vector2 velocity)
        {
            var go=new GameObject("Cake "+key+" VFX");go.transform.SetParent(owner.EffectsRoot);go.transform.position=pos;
            var v=go.AddComponent<SnailBossVfx>();v.sr=SnailBossArt.Make(go.transform,key,size,525);v.width=size;
            v.life=v.remaining=lifetime;v.velocity=velocity;return v;
        }
        public static void Burst(SnailBossRuntime owner,Vector2 pos,string key,int count,float spread)
        {
            for(int i=0;i<count;i++){Vector2 dir=Random.insideUnitCircle;var v=Make(owner,key,pos,Random.Range(.16f,.36f),Random.Range(.3f,.65f),dir*spread*3);v.sr.transform.localRotation=Quaternion.Euler(0,0,Random.Range(0,360));}
        }
        public static void Warning(SnailBossRuntime owner,Vector2 pos,float radius,float duration,bool chocolate)
        {
            var v=Make(owner,chocolate?"ChocolatePool":"CreamPool",pos,radius*2,duration,Vector2.zero);v.warning=true;
            v.sr.sortingLayerName="GroundEffects";
        }
        void Update()
        {
            if(SnailBossRuntime.Paused)return;remaining-=Time.deltaTime;
            transform.position+=(Vector3)velocity*Time.deltaTime;
            float a=Mathf.Clamp01(remaining/life);
            sr.color=warning?new Color(1,.35f,.25f,.25f+.4f*(1-a)):new Color(1,1,1,a);
            if(remaining<=0)Destroy(gameObject);
        }
    }
}
