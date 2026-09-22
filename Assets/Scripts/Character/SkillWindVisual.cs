using UnityEngine;
namespace Vampire
{
    public sealed class SkillWindVisual : MonoBehaviour
    {
        CharacterSkillRuntime skill;
        SpriteRenderer visual;
        public void Bind(CharacterSkillRuntime source)
        {
            skill=source;
            visual=new GameObject("Skill wind",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            visual.transform.SetParent(transform,false);
        }
        void LateUpdate()
        {
            if(visual==null)return;
            bool show=skill!=null && skill.Definition!=null && (skill.AshiActive||skill.AshiPassive);
            visual.enabled=show;
            if(!show)return;
            var frames=skill.AshiActive?skill.Definition.activeWind:skill.Definition.passiveWind;
            if(frames==null||frames.Length==0){visual.enabled=false;return;}
            visual.sprite=frames[(int)(Time.time*(skill.AshiActive?18:10))%frames.Length];
            var body=GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
            visual.sortingLayerID=body.sortingLayerID;visual.sortingOrder=body.sortingOrder+1;
            visual.transform.localPosition=new Vector3(0,-.06f,-.02f);
            float width=skill.AshiActive?1.65f:1.05f;
            visual.transform.localScale=Vector3.one*width/visual.sprite.bounds.size.x;
            visual.color=new Color(1,1,1,skill.AshiActive?.85f:.65f);
        }
    }
    public sealed class SkillProjectileWind : MonoBehaviour
    {
        Sprite[] frames;
        SpriteRenderer visual;
        Vector3 previous;
        float angle;
        public static void Attach(Projectile projectile,CharacterSkillRuntime skill)
        {
            if(projectile==null)return;
            var effect=projectile.GetComponent<SkillProjectileWind>();
            if(effect==null && skill!=null && skill.AshiActive)effect=projectile.gameObject.AddComponent<SkillProjectileWind>();
            if(effect==null)return;
            effect.frames=skill!=null&&skill.AshiActive?skill.Definition.projectileWind:null;
            effect.previous=projectile.transform.position;
            if(effect.visual==null)
            {
                effect.visual=new GameObject("Sprint projectile wake",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                effect.visual.transform.SetParent(projectile.transform,false);
            }
            effect.visual.enabled=false;
        }
        void LateUpdate()
        {
            if(visual==null||frames==null||frames.Length==0)return;
            Vector3 delta=transform.position-previous;previous=transform.position;
            if(delta.sqrMagnitude>.000001f)angle=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
            visual.sprite=frames[(int)(Time.time*20)%frames.Length];visual.enabled=true;
            var direction=Quaternion.Euler(0,0,angle)*Vector3.right;
            visual.transform.position=transform.position-direction*.28f;
            visual.transform.rotation=Quaternion.Euler(0,0,angle);
            float scale=.85f/visual.sprite.bounds.size.x;
            visual.transform.localScale=new Vector3(scale/transform.lossyScale.x,scale/transform.lossyScale.y,1);
            var body=GetComponentInChildren<SpriteRenderer>();
            visual.sortingLayerID=body.sortingLayerID;visual.sortingOrder=body.sortingOrder-1;
            visual.color=new Color(1,1,1,.8f);
        }
        void OnDisable(){frames=null;if(visual!=null)visual.enabled=false;}
    }
}
