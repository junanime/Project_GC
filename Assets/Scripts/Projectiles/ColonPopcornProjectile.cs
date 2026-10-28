using System.Linq;
using UnityEngine;
namespace Vampire
{
    // A shared projectile pool may next be used by a mini-stage sniper: restore on every despawn.
    public sealed class ColonPopcornProjectile : MonoBehaviour
    {
        SpriteRenderer body;Sprite savedSprite;Vector3 savedScale;Quaternion savedRotation;bool activeArt;
        CapsuleCollider2D capsule;Vector2 savedColliderSize,savedColliderOffset;
        public static void Apply(Projectile projectile,bool popcorn)
        {
            var art=projectile.GetComponent<ColonPopcornProjectile>();
            if(!popcorn){art?.Restore();return;}
            if(art==null)art=projectile.gameObject.AddComponent<ColonPopcornProjectile>();
            art.Configure();
        }
        void Configure()
        {
            Restore();body=GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r=>r.sprite!=null);
            if(body==null)return;
            var sprite=Resources.LoadAll<Sprite>("ColonMonsters/Extras").FirstOrDefault(s=>s.name=="Popcorn");if(sprite==null)return;
            savedSprite=body.sprite;savedScale=body.transform.localScale;savedRotation=body.transform.localRotation;activeArt=true;
            float parentScale=body.transform.parent!=null?Mathf.Abs(body.transform.parent.lossyScale.y):1;
            body.sprite=sprite;float scale=.20f/(sprite.bounds.size.y*Mathf.Max(.001f,parentScale));body.transform.localScale=new Vector3(scale,scale,1);
            capsule=GetComponent<CapsuleCollider2D>();
            if(capsule!=null)
            {
                savedColliderSize=capsule.size;savedColliderOffset=capsule.offset;
                var bounds=VisibleBodyGeometry.InSpace(body,capsule.transform);
                capsule.size=(Vector2)bounds.size*.8f;capsule.offset=bounds.center;
            }
        }
        void LateUpdate(){if(activeArt&&body!=null)body.transform.Rotate(0,0,240*Time.deltaTime);}
        void Restore(){if(activeArt){if(body!=null){body.sprite=savedSprite;body.transform.localScale=savedScale;body.transform.localRotation=savedRotation;}if(capsule!=null){capsule.size=savedColliderSize;capsule.offset=savedColliderOffset;}}activeArt=false;}
        void OnDisable()=>Restore();
    }
}
