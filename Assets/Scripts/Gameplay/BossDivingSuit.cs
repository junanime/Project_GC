using UnityEngine;
namespace Vampire
{
    [DefaultExecutionOrder(11000)]
    public sealed class BossDivingSuit:MonoBehaviour
    {
        Character player;BloodClotTravel travel;SpriteRenderer art;float height,kick;bool paddle;Material material;Sprite sprite;
        Vector3 assemblyFrom;float assembly=1;
        public static BossDivingSuit Attach(Character p,FoodBossKind kind)
        {
            var go=new GameObject("착용한 보스 부스러기 잠수복");var s=go.AddComponent<BossDivingSuit>();s.player=p;s.travel=BloodClotTravel.Ensure(p);
            s.height=s.travel.ReferenceHeight;s.art=go.AddComponent<SpriteRenderer>();s.art.sprite=BossTravelArt.Suit(kind);
            if(s.art.sprite!=null)
            {
                var source=s.art.sprite;s.sprite=Sprite.Create(source.texture,source.rect,Vector2.one*.5f,source.pixelsPerUnit,0,SpriteMeshType.FullRect);s.art.sprite=s.sprite;
                s.material=new Material(Resources.Load<Shader>("BossTravel/DivingSuit"));var r=source.rect;var t=source.texture;
                s.material.SetVector("_SpriteRect",new Vector4(r.x/t.width,r.y/t.height,r.width/t.width,r.height/t.height));s.art.sharedMaterial=s.material;
            }
            s.art.sortingLayerName="Monster Full";s.art.sortingOrder=32012;return s;
        }
        public void Assemble(Vector3 from,float progress){assemblyFrom=from;assembly=progress;}
        public void Kick(float time,bool atSurface){kick=time;paddle=atSurface;assembly=1;}
        void LateUpdate()
        {
            if(player==null||travel.Visual==null){Destroy(gameObject);return;}
            var visual=travel.Visual;visual.sortingLayerName="Monster Full";visual.sortingOrder=32011;
            if(art.sprite==null)return;
            if(paddle)kick+=Time.deltaTime;
            if(material!=null){material.SetFloat("_Kick",kick);material.SetFloat("_Strength",paddle?1:.6f);}
            // Renderer.bounds can remain stale during a long forceRenderingOff journey.
            var opaque=VisibleBodyGeometry.Bounds(visual.sprite);
            Vector2 center=opaque.center;if(visual.flipX)center.x=-center.x;if(visual.flipY)center.y=-center.y;
            Vector3 destination=visual.transform.TransformPoint(center)+Vector3.down*height*.40f;
            transform.position=Vector3.Lerp(assemblyFrom,destination,assembly);
            float scale=height*.80f/art.sprite.bounds.size.y;
            transform.localScale=Vector3.one*scale*Mathf.Lerp(.65f,1,assembly);
            transform.rotation=Quaternion.Euler(0,0,visual.transform.eulerAngles.z+Mathf.Sin(kick*(paddle?24:11))*(paddle?6:3));
            art.color=new Color(1,1,1,visual.color.a);
        }
        void OnDestroy(){if(material!=null)Destroy(material);if(sprite!=null)Destroy(sprite);}
    }
}
