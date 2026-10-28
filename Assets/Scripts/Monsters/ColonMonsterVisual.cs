using UnityEngine;
namespace Vampire
{
    [DefaultExecutionOrder(600)]
    public sealed class ColonMonsterVisual : MonoBehaviour
    {
        public ColonMonsterRoster.Entry Entry { get; private set; }
        Monster monster; SpriteRenderer body; Rigidbody2D physicsBody;MonsterBodyFit fit;SniperRecoilVisual recoil;
        Vector3 savedScale,savedPosition,baseScale,basePosition; Quaternion savedRotation;
        float phase; bool captured;
        public void ResetForPool()
        {
            if(captured&&body!=null){body.transform.localScale=savedScale;body.transform.localPosition=savedPosition;body.transform.localRotation=savedRotation;}
            Entry=null;captured=false;enabled=false;
        }
        public void Configure(Monster target,ColonMonsterRoster.Entry entry)
        {
            monster=target;body=target.BodyRenderer;physicsBody=target.GetComponent<Rigidbody2D>();Entry=entry;
            if(body==null)return;
            savedScale=body.transform.localScale;savedPosition=body.transform.localPosition;savedRotation=body.transform.localRotation;captured=true;
            body.sprite=entry.sprite;
            float parentY=body.transform.parent!=null?Mathf.Abs(body.transform.parent.lossyScale.y):1;
            float scale=entry.height/Mathf.Max(.001f,entry.sprite.bounds.size.y*parentY);
            baseScale=Vector3.one*scale;baseScale.z=1;body.transform.localScale=baseScale;body.transform.localRotation=Quaternion.identity;
            var ground=monster.transform.position;ground.z=body.transform.position.z;
            body.transform.position=ground;basePosition=body.transform.localPosition;
            phase=Random.value*6.28f;enabled=true;
            target.ConfigureGroundShadow(target.Blueprint);target.FitVisibleBody();
            fit=target.GetComponent<MonsterBodyFit>();recoil=target.GetComponent<SniperRecoilVisual>();RefreshAimCenter();
            var feet=target.GetComponent<CircleCollider2D>();
            if(feet!=null)feet.radius=Mathf.Max(.05f,body.bounds.size.x/(2.5f*Mathf.Max(.001f,Mathf.Abs(target.transform.lossyScale.x))));
        }
        void LateUpdate()
        {
            if(Entry==null||body==null||monster==null)return;
            body.sprite=Entry.sprite;
            if(monster.HP<=0||monster.IsFieldRuntimeSuspended||Time.timeScale<=0)return;
            float movement=physicsBody!=null?Mathf.Clamp01(physicsBody.velocity.magnitude/.5f):0;
            phase+=Time.deltaTime*(movement>0.1f?9:2.5f);
            float beat=Mathf.Sin(Mathf.Floor(phase*4/6.283185f)*6.283185f/4);
            float squish=beat*.025f*movement;
            if(Entry.id=="S01"&&recoil!=null&&recoil.IsPlaying)squish=-.075f;
            body.transform.localScale=Vector3.Scale(baseScale,new Vector3(1-squish,1+squish,1));
            body.transform.localPosition=basePosition+Vector3.up*(Mathf.Max(0,beat)*Entry.height*.025f*movement);
            float sway=(Entry.id=="S04"?3:Entry.id=="N07"?4:1.5f)*beat*movement;
            body.transform.localRotation=Quaternion.Euler(0,0,sway);
            fit?.Refresh();RefreshAimCenter();
        }
        void RefreshAimCenter(){if(monster.CenterTransform!=null&&monster.BodyHitbox!=null)monster.CenterTransform.position=monster.BodyHitbox.bounds.center;}
        public Vector2 Muzzle
        {
            get
            {
                var b=body.sprite.bounds;var local=new Vector3(Mathf.Lerp(b.min.x,b.max.x,.71f),Mathf.Lerp(b.min.y,b.max.y,.78f));
                if(body.flipX)local.x=-local.x;return body.transform.TransformPoint(local);
            }
        }
        void OnDisable()=>ResetForPool();
    }
}
