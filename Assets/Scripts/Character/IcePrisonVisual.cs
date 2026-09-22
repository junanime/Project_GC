using UnityEngine;
namespace Vampire
{
    public sealed class IcePrisonVisual : MonoBehaviour
    {
        SpriteRenderer visual,body;NeuralBlockedMonsterStatus status;CharacterSkillDefinition art;
        void Awake()
        {
            status=GetComponent<NeuralBlockedMonsterStatus>();body=GetComponentInChildren<SpriteRenderer>();
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
            visual.transform.localScale=new Vector3(size.x*1.2f/visual.sprite.bounds.size.x/Mathf.Max(.001f,Mathf.Abs(scale.x)),size.y*1.18f/visual.sprite.bounds.size.y/Mathf.Max(.001f,Mathf.Abs(scale.y)),1);
        }
        void OnDisable(){if(visual!=null)visual.enabled=false;}
        public void Rebind(NeuralBlockedMonsterStatus source){status=source;}
    }
}
