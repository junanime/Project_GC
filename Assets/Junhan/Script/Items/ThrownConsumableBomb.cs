using System.Linq;
using UnityEngine;

namespace Vampire
{
    public sealed class ThrownConsumableBomb : MonoBehaviour
    {
        public Vector2 Target {get;private set;}
        public const float FlightSeconds=.65f;
        Vector2 from;float age,damage;EntityManager entities;LevelManager level;SpriteRenderer visual;
        bool exploded;static Sprite[] explosionFrames;
        public static ThrownConsumableBomb Launch(Character player,EntityManager entities,Vector2 target,float damage,Sprite sprite)
        {
            var bomb=new GameObject("Thrown consumable bomb").AddComponent<ThrownConsumableBomb>();
            bomb.from=player.CenterTransform.position;bomb.Target=target;bomb.damage=damage;bomb.entities=entities;bomb.level=FindObjectOfType<LevelManager>();
            bomb.visual=bomb.gameObject.AddComponent<SpriteRenderer>();bomb.visual.sprite=sprite;bomb.visual.sortingLayerName="Monster Full";bomb.visual.sortingOrder=250;
            bomb.transform.localScale=Vector3.one*(.45f/Mathf.Max(.01f,sprite!=null?sprite.bounds.size.x:1));bomb.transform.position=bomb.from;return bomb;
        }
        void Update()
        {
            if(level==null||level.IsLevelEnded){Destroy(gameObject);return;}
            if(Time.timeScale<=0||level.IsRunFlowPaused)return;
            if(exploded)
            {
                age+=Time.deltaTime;int frame=Mathf.FloorToInt(age/.06f);
                if(frame>=explosionFrames.Length){Destroy(gameObject);return;}
                visual.sprite=explosionFrames[frame];return;
            }
            age+=Time.deltaTime;float t=Mathf.Clamp01(age/FlightSeconds);
            transform.position=Vector2.Lerp(from,Target,t)+Vector2.up*(Mathf.Sin(t*Mathf.PI)*1.4f);
            transform.rotation=Quaternion.Euler(0,0,-360*t);
            if(t>=1)Explode();
        }
        void Explode()
        {
            foreach(var hit in Physics2D.OverlapCircleAll(Target,BombTargeting.Radius,LayerMask.GetMask("Monster Full")))
                hit.GetComponentInParent<CurrencyJar>()?.TakeDamage(damage);
            foreach(var monster in entities.LivingMonsters.ToArray())
            {
                if(!NeedleTargeting.IsValid(monster,~0))continue;
                if(Vector2.Distance(monster.BodyHitbox.ClosestPoint(Target),Target)>BombTargeting.Radius)continue;
                float before=monster.HP;monster.TakeItemPureDamage(damage);
                AugmentDamageTracker.Instance?.RecordDamage("폭탄",Mathf.Max(0,before-Mathf.Max(0,monster.HP)));
            }
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.BombPickup);
            // Reuse the existing orange explosion sheet, preserving its expansion across equal-sized frames.
            if(explosionFrames==null)explosionFrames=Resources.LoadAll<Sprite>("SyringeVfx/Explosion").OrderBy(s=>s.name,System.StringComparer.Ordinal).ToArray();
            if(explosionFrames.Length==0){Destroy(gameObject);return;}
            exploded=true;age=0;visual.sprite=explosionFrames[0];transform.position=Target;transform.rotation=Quaternion.identity;
            transform.localScale=Vector3.one*(BombTargeting.Radius*2/(explosionFrames[0].rect.width/explosionFrames[0].pixelsPerUnit));
        }
    }
}
