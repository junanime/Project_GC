using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    [DefaultExecutionOrder(520)]
    public sealed class HyukiSnowVisual : MonoBehaviour
    {
        sealed class Mote {public SpriteRenderer sprite;public Vector3 position,velocity;public float life,maxLife;}
        Character owner;CharacterSkillRuntime skill;SpriteRenderer body;
        readonly List<Mote> motes=new List<Mote>();readonly List<SpriteRenderer> storm=new List<SpriteRenderer>();
        Vector3 previous;float emission,phase;
        public bool DashingWake {get;private set;}
        public int WakeCount => motes.FindAll(m=>m.sprite.enabled).Count;
        public int StormCount => storm.FindAll(r=>r.enabled).Count;
        public void Bind(Character c,CharacterSkillRuntime s){owner=c;skill=s;previous=transform.position;body=c.GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();}
        SpriteRenderer Make(string name)
        {
            var r=new GameObject(name).AddComponent<SpriteRenderer>();r.transform.SetParent(transform,false);r.enabled=false;
            if(body!=null){r.sortingLayerID=body.sortingLayerID;r.sortingOrder=body.sortingOrder+2;}return r;
        }
        static float Hash(int i)=>Mathf.Repeat(Mathf.Sin(i*127.1f+311.7f)*43758.5453f,1);
        void Size(SpriteRenderer r,float width)
        {float s=width/r.sprite.bounds.size.x;r.transform.localScale=new Vector3(s/Mathf.Abs(transform.lossyScale.x),s/Mathf.Abs(transform.lossyScale.y),1);}
        void LateUpdate()
        {
            if(owner==null||!owner.IsAlive){Hide();return;}
            var art=skill.Definition.iceComponents;if(art==null||art.Length<4)return;
            float dt=Time.deltaTime;if(dt<=0)return;phase+=dt;
            Vector3 delta=transform.position-previous;previous=transform.position;DashingWake=owner.IsDashing;
            if(delta.sqrMagnitude>.000001f)
            {
                Vector3 direction=delta.normalized;emission+=dt*(DashingWake?85:22);
                int count=Mathf.Min(8,Mathf.FloorToInt(emission));emission-=count;
                for(int i=0;i<count;i++)
                {
                    var m=motes.Find(x=>!x.sprite.enabled);
                    if(m==null&&motes.Count<64){m=new Mote{sprite=Make("Hyuki drifting snow")};motes.Add(m);}
                    if(m==null)break;
                    int id=motes.IndexOf(m);bool shard=DashingWake&&id%3!=0;
                    m.sprite.sprite=art[shard?2:id%4==0?3:1];m.sprite.enabled=true;
                    Vector3 side=new Vector3(-direction.y,direction.x,0);
                    m.position=Vector3.Lerp(previous-delta,previous,(i+.5f)/Mathf.Max(1,count))-direction*.18f+side*((Hash(id+21)-.5f)*.65f)+Vector3.down*.08f;
                    m.sprite.transform.position=m.position;
                    m.velocity=-direction*(DashingWake?1.6f:.45f)+side*((Hash(id+51)-.5f)*.5f);
                    m.maxLife=m.life=DashingWake?.5f:.75f;
                    m.sprite.transform.rotation=Quaternion.Euler(0,0,shard||id%4==0?Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg:Hash(id)*180);
                    Size(m.sprite,shard?.3f:id%4==0?.65f:.13f);
                }
            }
            else emission=0;
            foreach(var m in motes)
            {
                if(!m.sprite.enabled)continue;m.life-=dt;
                if(m.life<=0){m.sprite.enabled=false;continue;}
                m.position+=m.velocity*dt;m.sprite.transform.position=m.position;
                m.sprite.color=new Color(.85f,1,1,(DashingWake?.95f:.55f)*Mathf.Clamp01(m.life/m.maxLife));
            }
            var v=GetComponent<PhoenixSkillVisual>();float strength=v!=null?v.BlizzardStrength:0;
            var camera=Camera.main;
            if(strength>0&&camera!=null)
            {
                while(storm.Count<120)storm.Add(Make("Blizzard snow and ice"));
                float height=camera.orthographicSize*2,width=height*camera.aspect;
                for(int i=0;i<storm.Count;i++)
                {
                    var r=storm[i];r.enabled=true;r.sortingOrder=30001;
                    int kind=i%5==0?1:2;r.sprite=art[kind];
                    float speed=.2f+Hash(i+9)*.5f;
                    float x=Mathf.Repeat(Hash(i+11)+phase*speed,1.2f)-.1f;
                    float y=Mathf.Repeat(Hash(i+37)-phase*speed*1.3f,1.2f)-.1f;
                    r.transform.position=new Vector3(camera.transform.position.x+(x-.5f)*width,camera.transform.position.y+(y-.5f)*height,transform.position.z-1);
                    r.transform.rotation=Quaternion.Euler(0,0,kind==3?-40:Hash(i+87)*360+phase*(Hash(i+3)-.5f)*40);
                    Size(r,(kind==1?.08f:.12f)+Hash(i+67)*.12f);
                    r.color=new Color(.85f,1,1,strength*(.2f+.35f*Hash(i+8)));
                }
            }
            else foreach(var r in storm)r.enabled=false;
        }
        void Hide(){foreach(var m in motes)m.sprite.enabled=false;foreach(var r in storm)r.enabled=false;emission=0;previous=transform.position;}
        void OnDisable(){Hide();}
    }
}
