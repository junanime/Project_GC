using UnityEngine;
namespace Vampire
{
    // Reuse the monster's current sprite so silhouettes, flipping and animation match automatically.
    [DefaultExecutionOrder(950)]
    public sealed class IceChillVisual : MonoBehaviour
    {
        SpriteRenderer body,overlay;readonly SpriteRenderer[] snow=new SpriteRenderer[9];
        IceChillStatus status;CharacterSkillDefinition art;Material material;float phase;
        public bool Visible => overlay!=null&&overlay.enabled;
        public Sprite DisplayedSprite => overlay!=null?overlay.sprite:null;
        void Awake()
        {
            status=GetComponent<IceChillStatus>();body=SyringeAugmentVfx.FindTarget(this);
            art=Resources.Load<CharacterSkillDefinition>("HyukiSkills");
            material=new Material(Shader.Find("Vampire/IceChillTint"));
            overlay=Make("Ice slow silhouette");overlay.sharedMaterial=material;
            for(int i=0;i<snow.Length;i++)snow[i]=Make("Ice slow falling snow");
        }
        SpriteRenderer Make(string name){var r=new GameObject(name).AddComponent<SpriteRenderer>();r.transform.SetParent(transform,false);r.enabled=false;return r;}
        void LateUpdate()
        {
            bool show=status!=null&&status.Stacks>0&&body!=null&&body.enabled&&SyringeAugmentVfx.IsLiving(this);
            overlay.enabled=show;foreach(var r in snow)r.enabled=show;
            if(!show)return;
            if(!(GetComponent<Monster>() is Monster m&&m.IsFieldRuntimeSuspended))phase+=Time.deltaTime;
            overlay.sprite=body.sprite;overlay.flipX=body.flipX;overlay.flipY=body.flipY;
            overlay.transform.SetPositionAndRotation(body.transform.position,body.transform.rotation);
            var scale=body.transform.lossyScale;var parent=transform.lossyScale;
            overlay.transform.localScale=new Vector3(scale.x/parent.x,scale.y/parent.y,1);
            overlay.color=new Color(.58f,.86f,1,.23f+.025f*status.Stacks);
            overlay.sortingLayerID=body.sortingLayerID;overlay.sortingOrder=body.sortingOrder+1;
            Bounds b=body.bounds;
            for(int i=0;i<snow.Length;i++)
            {
                var r=snow[i];if(art==null||art.iceComponents==null||art.iceComponents.Length<2){r.enabled=false;continue;}
                r.sprite=art.iceComponents[1];float age=Mathf.Repeat(phase*(.45f+.07f*(i%3))+i*.173f,1);
                float x=Mathf.Sin(i*9.31f)*.6f+Mathf.Sin(phase+i)*.05f;
                r.transform.position=b.center+new Vector3(x*b.size.x,(.9f-age*1.5f)*b.size.y,0);
                float width=Mathf.Max(.025f,b.size.x*(i<2?.24f:.06f));
                r.transform.localScale=new Vector3(width/r.sprite.bounds.size.x/Mathf.Abs(parent.x),width/r.sprite.bounds.size.x/Mathf.Abs(parent.y),1);
                r.color=new Color(.8f,.95f,1,Mathf.Sin(age*Mathf.PI)*.7f);r.sortingLayerID=body.sortingLayerID;r.sortingOrder=body.sortingOrder+2;
            }
        }
        void OnDisable(){if(overlay!=null)overlay.enabled=false;foreach(var r in snow)if(r!=null)r.enabled=false;}
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
