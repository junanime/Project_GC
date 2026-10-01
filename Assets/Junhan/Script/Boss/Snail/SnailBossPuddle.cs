using UnityEngine;
namespace Vampire
{
    public sealed class SnailBossPuddle : MonoBehaviour
    {
        SnailBossRuntime owner;Vector2 from,to;float radius,remaining,slow,tail;SnailSlowStatus status;bool inside;
        public static SnailBossPuddle Circle(SnailBossRuntime owner,Vector2 position,float radius,float duration,float slow,float tail)
            =>Create(owner,position,position,radius,duration,slow,tail);
        public static SnailBossPuddle Path(SnailBossRuntime owner,Vector2 start,Vector2 end)
            =>Create(owner,start,end,.6f,owner.settings.dashPuddleDuration,owner.Chocolate?owner.settings.chocolateSlow:owner.settings.vanillaSlow,0);
        static SnailBossPuddle Create(SnailBossRuntime owner,Vector2 a,Vector2 b,float radius,float duration,float slow,float tail)
        {
            var go=new GameObject("Wet cake terrain");go.transform.SetParent(owner.EffectsRoot);
            var p=go.AddComponent<SnailBossPuddle>();p.owner=owner;p.from=a;p.to=b;p.radius=radius;p.remaining=duration;p.slow=slow;p.tail=tail;
            int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.6f));
            for(int i=0;i<=count;i++)
            {
                var sr=SnailBossArt.Make(go.transform,owner.Chocolate?"ChocolatePool":"CreamPool",radius*2.2f,0);
                sr.sortingLayerName="GroundEffects";sr.transform.position=Vector2.Lerp(a,b,i/(float)count);
                sr.transform.localScale=Vector3.Scale(sr.transform.localScale,new Vector3(1,.55f,1));
                sr.color=new Color(1,1,1,.85f);
            }
            p.UpdateOccupancy();return p;
        }
        void Update()
        {
            if(owner==null||owner.Dead){Destroy(gameObject);return;}
            if(SnailBossRuntime.Paused){Leave(0);return;}
            remaining-=Time.deltaTime;UpdateOccupancy();
            if(remaining<=0)Destroy(gameObject);
        }
        void UpdateOccupancy()
        {
            if(owner.Player==null)return;
            bool now=SnailBossProjectile.SegmentDistance(owner.Player.transform.position,from,to)<=radius;
            if(now&&!inside)
            {
                status=owner.Player.GetComponent<SnailSlowStatus>()??owner.Player.gameObject.AddComponent<SnailSlowStatus>();
                status.Enter(GetInstanceID(),slow);inside=true;
            }
            else if(!now&&inside)Leave(tail);
        }
        void Leave(float seconds){if(inside&&status!=null)status.Exit(GetInstanceID(),seconds);inside=false;}
        void OnDestroy(){Leave(SnailBossRuntime.Paused?0:tail);}
    }
}
