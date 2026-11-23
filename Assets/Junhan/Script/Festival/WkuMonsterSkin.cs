using UnityEngine;
namespace Vampire
{
    // Rendering only: the original body still drives colliders, movement and shadows.
    [DefaultExecutionOrder(20000)]
    public sealed class WkuMonsterSkin:MonoBehaviour
    {
        SpriteRenderer body, visual;BoxCollider2D originalHitbox;WkuMonsterLook look;bool previousHidden;
        public string LookId=>look?.id;public SpriteRenderer Visual=>visual;
        public static void Apply(Monster monster,SpriteRenderer renderer)
        {
            if(!FestivalDemo.Enabled||!FestivalDemo.Rules.StartsWith("wku_")||renderer==null)return;
            var skin=monster.GetComponent<WkuMonsterSkin>()??monster.gameObject.AddComponent<WkuMonsterSkin>();
            skin.Release();skin.body=renderer;skin.originalHitbox=renderer.GetComponent<BoxCollider2D>();skin.look=WkuMonsterLookCatalog.Next();
            if(skin.look==null)throw new System.InvalidOperationException("WKU ordinary monster art catalog is missing.");
            if(skin.visual==null){var holder=new GameObject("WKU ordinary appearance");holder.transform.SetParent(monster.transform,false);skin.visual=holder.AddComponent<SpriteRenderer>();}
            skin.previousHidden=renderer.forceRenderingOff;renderer.forceRenderingOff=true;skin.visual.gameObject.SetActive(true);
        }
        void LateUpdate()
        {
            if(body==null||visual==null||look==null)return;
            var frame=look.frames[(int)(Time.time/Mathf.Max(.02f,look.frameSeconds))%look.frames.Length];
            visual.sprite=frame;visual.enabled=body.enabled;visual.sharedMaterial=body.sharedMaterial;visual.color=body.color;visual.flipX=body.flipX;
            visual.sortingLayerID=body.sortingLayerID;visual.sortingOrder=body.sortingOrder;
            var original=originalHitbox!=null&&originalHitbox.enabled?originalHitbox.bounds:body.bounds;float scale=original.size.y/Mathf.Max(.01f,frame.bounds.size.y);
            visual.transform.localScale=new Vector3(scale/Mathf.Max(.01f,Mathf.Abs(transform.lossyScale.x)),scale/Mathf.Max(.01f,Mathf.Abs(transform.lossyScale.y)),1);
            visual.transform.position=new Vector3(original.center.x,original.min.y,body.transform.position.z);
            var bounds=visual.bounds;visual.transform.position+=new Vector3(original.center.x-bounds.center.x,original.min.y-bounds.min.y,0);
        }
        void Release(){if(body!=null)body.forceRenderingOff=previousHidden;if(visual!=null)visual.gameObject.SetActive(false);}
        void OnDisable(){Release();}
    }
}
