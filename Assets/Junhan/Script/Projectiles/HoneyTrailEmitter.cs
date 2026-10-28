using UnityEngine;
namespace Vampire
{
    // Cosmetic only: three one-second opportunities per hit, independent of slow balance.
    public sealed class HoneyTrailEmitter : MonoBehaviour
    {
        SpriteRenderer body;
        Vector3 previous;
        float remaining, untilStamp = 1, distance;
        Vector2 direction = Vector2.right;
        public int Emitted { get; private set; }
        public static void Touch(SpriteRenderer target)
        {
            if(target==null)return;
            var emitter=target.GetComponent<HoneyTrailEmitter>()??target.gameObject.AddComponent<HoneyTrailEmitter>();
            emitter.body=target;
            if(emitter.remaining<=0){emitter.untilStamp=1;emitter.distance=0;emitter.previous=Actor(target).position;}
            emitter.remaining=3;
        }
        void LateUpdate(){Advance(Time.deltaTime);}
        static Transform Actor(SpriteRenderer target){var boss=target.GetComponentInParent<FoodBossRuntime>();return boss!=null?boss.transform:target.GetComponentInParent<Monster>()?.transform??target.transform;}
        public void Advance(float dt)
        {
            if(remaining<=0||body==null)return;
            if(!SyringeAugmentVfx.IsLiving(body)){remaining=0;return;}
            if(MiniStageRuntimeState.IsInsideMiniStage || (body.GetComponentInParent<Monster>() is Monster m && m.IsFieldRuntimeSuspended))return;
            // Use actor movement, excluding animated body lifts, recoil and phase poses.
            var boss=body.GetComponentInParent<FoodBossRuntime>();
            var actor=Actor(body);
            Vector3 current=actor.position;
            Vector2 delta=current-previous;previous=current;
            float size=Mathf.Clamp(Mathf.Min(body.bounds.size.x,body.bounds.size.y)*.38f,.14f,.65f);
            if(delta.sqrMagnitude>25){distance=0;delta=Vector2.zero;} // scene transfers/teleports do not draw trails
            if(delta.sqrMagnitude>.000001f){direction=delta.normalized;distance+=delta.magnitude;}
            float step=Mathf.Min(Mathf.Max(0,dt),remaining);remaining-=step;untilStamp-=step;
            if(untilStamp<=.0001f)
            {
                untilStamp+=1;
                if(distance>=size*.15f && body.enabled && (boss==null||!boss.Visual.Airborne))
                {
                    Vector3 foot=boss!=null?actor.position:new Vector3(body.bounds.center.x,body.bounds.min.y,body.transform.position.z);
                    HoneyTrailStamp.Spawn(foot-(Vector3)direction*size*.3f,direction,size);Emitted++;
                }
                distance=0;
            }
        }
        void OnDisable(){remaining=distance=0;untilStamp=1;body=null;Emitted=0;}
    }

    public sealed class HoneyTrailStamp : MonoBehaviour
    {
        static readonly System.Collections.Generic.List<HoneyTrailStamp> pool=new System.Collections.Generic.List<HoneyTrailStamp>();
        readonly LineRenderer[] strokes=new LineRenderer[5];
        Material material;float age;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPool(){pool.Clear();}
        public static void Spawn(Vector3 position,Vector2 direction,float size)
        {
            pool.RemoveAll(p=>p==null);
            HoneyTrailStamp stamp=null;
            foreach(var p in pool)if(!p.gameObject.activeSelf){stamp=p;break;}
            if(stamp==null)
            {
                if(pool.Count>=128)return;
                stamp=new GameObject("Short honey stain").AddComponent<HoneyTrailStamp>();pool.Add(stamp);
            }
            stamp.transform.position=position;stamp.age=0;stamp.gameObject.SetActive(true);
            stamp.Setup(direction,size);
        }
        void Setup(Vector2 direction,float size)
        {
            if(material==null)material=new Material(Resources.Load<Shader>("AugmentVfx/OpacitySprite"));
            for(int i=0;i<strokes.Length;i++)
            {
                if(strokes[i]==null)
                {
                    var r=new GameObject("Honey stroke "+i).AddComponent<LineRenderer>();r.transform.SetParent(transform,false);
                    r.sharedMaterial=material;r.useWorldSpace=false;r.positionCount=2;r.numCapVertices=6;GroundVisualSorting.Apply(r,i);strokes[i]=r;
                }
                var line=strokes[i];
                Vector3 axis=new Vector3(direction.x,direction.y*.50f)*size;
                bool baseStroke=i==0||i==2;
                Vector3 offset=baseStroke?Vector3.zero:i==4?new Vector3(0,size*.045f):axis*.24f+new Vector3(-axis.y,axis.x)*.05f;
                float length=baseStroke?.24f:i==4?.10f:.055f;
                line.SetPosition(0,offset-axis*length);
                line.SetPosition(1,offset+axis*length);
                line.startWidth=line.endWidth=size*(i==0?.25f:i==1?.21f:i==2?.19f:i==3?.15f:.026f);
                // Both outlines render before both fills, keeping a single irregular puddle edge.
                line.startColor=line.endColor=i<2?new Color(.73f,.42f,.08f,.85f):i==4?new Color(1,.94f,.60f,.9f):new Color(1,.73f,.22f,.85f);
            }
            AugmentVfxTag.Mark(gameObject);
        }
        void Update()
        {
            if(MiniStageRuntimeState.IsInsideMiniStage)return;
            age+=Time.deltaTime;
            if(age>=3){gameObject.SetActive(false);return;}
            float fade=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((age-2)/1));
            foreach(var r in strokes){var c=r.startColor;c.a=(r==strokes[4]?.9f:.85f)*fade;r.startColor=r.endColor=c;}
        }
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
