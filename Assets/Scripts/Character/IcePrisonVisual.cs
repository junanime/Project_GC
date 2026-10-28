using UnityEngine;
namespace Vampire
{
    public sealed class IcePrisonVisual : MonoBehaviour
    {
        SpriteRenderer visual,body,overlay;NeuralBlockedMonsterStatus status;CharacterSkillDefinition art;Material tint;
        void Awake()
        {
            status=GetComponent<NeuralBlockedMonsterStatus>();body=SyringeAugmentVfx.FindTarget(this);
            art=Resources.Load<CharacterSkillDefinition>("HyukiSkills");
            visual=new GameObject("Translucent ice prison").AddComponent<SpriteRenderer>();visual.transform.SetParent(transform,false);
            overlay=new GameObject("Frozen body silhouette").AddComponent<SpriteRenderer>();overlay.transform.SetParent(body!=null?body.transform:transform,false);
            tint=new Material(Resources.Load<Shader>("AugmentVfx/PoisonTint"));overlay.sharedMaterial=tint;overlay.enabled=false;
            AugmentVfxTag.Mark(overlay.gameObject);AugmentVfxTag.Mark(visual.gameObject);
        }
        void LateUpdate()
        {
            if(visual==null)return;
            visual.enabled=status!=null&&status.IceFrozen&&body!=null&&body.enabled&&art!=null&&art.icePrison!=null&&art.icePrison.Length>0;
            overlay.enabled=visual.enabled;
            if(!visual.enabled)return;
            overlay.sprite=body.sprite;overlay.flipX=body.flipX;overlay.flipY=body.flipY;
            overlay.color=new Color(.58f,.86f,1,.32f*body.color.a);overlay.sortingLayerID=body.sortingLayerID;overlay.sortingOrder=body.sortingOrder+1;
            StatusTintCycle.Submit(body,overlay,StatusTintKind.Ice);
            visual.sprite=art.icePrison[(int)(Time.time*8)%art.icePrison.Length];
            visual.color=new Color(1,1,1,.43f);visual.sortingLayerID=body.sortingLayerID;visual.sortingOrder=body.sortingOrder+1;
            visual.transform.position=body.bounds.center;
            Vector3 size=body.bounds.size,scale=transform.lossyScale;
            float fit=Mathf.Max(size.x*1.2f/visual.sprite.bounds.size.x,size.y*1.18f/visual.sprite.bounds.size.y);
            visual.transform.localScale=new Vector3(fit/Mathf.Max(.001f,Mathf.Abs(scale.x)),fit/Mathf.Max(.001f,Mathf.Abs(scale.y)),1);
        }
        void OnDisable(){if(visual!=null)visual.enabled=false;if(overlay!=null)overlay.enabled=false;}
        void OnDestroy(){if(tint!=null)Destroy(tint);if(overlay!=null)Destroy(overlay.gameObject);if(visual!=null)Destroy(visual.gameObject);}
        public void Rebind(NeuralBlockedMonsterStatus source){status=source;}
    }
}
