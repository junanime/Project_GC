using UnityEngine;
namespace Vampire
{
    public sealed class IcePrisonVisual : MonoBehaviour
    {
        SpriteRenderer visual,body;NeuralBlockedMonsterStatus status;CharacterSkillDefinition art;
        void Awake()
        {
            status=GetComponent<NeuralBlockedMonsterStatus>();body=SyringeAugmentVfx.FindTarget(this);
            art=Resources.Load<CharacterSkillDefinition>("HyukiSkills");
            visual=new GameObject("Translucent ice prison").AddComponent<SpriteRenderer>();visual.transform.SetParent(transform,false);
        }
        void LateUpdate()
        {
            if(visual==null)return;
            visual.enabled=status!=null&&status.IceFrozen&&body!=null&&body.enabled&&art!=null&&art.icePrison!=null&&art.icePrison.Length>0;
            if(!visual.enabled)return;
            visual.sprite=art.icePrison[(int)(Time.time*8)%art.icePrison.Length];
            visual.color=new Color(1,1,1,.43f);visual.sortingLayerID=body.sortingLayerID;visual.sortingOrder=body.sortingOrder+1;
            visual.transform.position=body.bounds.center;
            Vector3 size=body.bounds.size,scale=transform.lossyScale;
            float fit=Mathf.Max(size.x*1.2f/visual.sprite.bounds.size.x,size.y*1.18f/visual.sprite.bounds.size.y);
            visual.transform.localScale=new Vector3(fit/Mathf.Max(.001f,Mathf.Abs(scale.x)),fit/Mathf.Max(.001f,Mathf.Abs(scale.y)),1);
        }
        void OnDisable(){if(visual!=null)visual.enabled=false;}
        public void Rebind(NeuralBlockedMonsterStatus source){status=source;}
    }
}
