using UnityEngine;
namespace Vampire
{
    // Scene-owned cosmetic summon. Scaled time preserves pause behavior.
    public sealed class PhoenixSummonVisual : MonoBehaviour
    {
        Character owner;
        SpriteRenderer body;
        PhoenixRadianceLayer bird;
        readonly PhoenixRadianceLayer[] feathers=new PhoenixRadianceLayer[18];
        float age,duration;
        bool golden;
        public const float RequiemDuration=1.1f;
        public static PhoenixSummonVisual Play(Character character,bool gold,float seconds=1.1f)
        {
            if(character==null)return null;
            var v=new GameObject(gold?"Golden phoenix summon":"Requiem phoenix summon").AddComponent<PhoenixSummonVisual>();
            v.owner=character;v.body=SyringeAugmentVfx.FindTarget(character);v.golden=gold;v.duration=Mathf.Max(.2f,seconds);
            v.bird=new PhoenixRadianceLayer(v.transform,gold?"GoldPhoenix":"RequiemPhoenix");
            for(int i=0;i<v.feathers.Length;i++)v.feathers[i]=new PhoenixRadianceLayer(v.transform,"FeatherKit");
            return v;
        }
        void LateUpdate()
        {
            if(owner==null||!owner.gameObject.activeInHierarchy||owner.CurrentHealth<=0){Destroy(gameObject);return;}
            age+=Time.deltaTime;if(age>=duration){Destroy(gameObject);return;}
            float t=age/duration;
            bool reduced=GamePreferences.Current.reducedMotion;
            float phase=reduced?0:age;
            float h=body!=null?Mathf.Max(.3f,body.bounds.size.y):.7f;
            Vector3 center=body!=null?body.bounds.center:owner.transform.position;
            float opacity=Mathf.SmoothStep(0,1,t/.14f)*(1-Mathf.SmoothStep(0,1,(t-.78f)/.22f));
            float width=h*(golden?3.2f:4.8f);
            Vector3 position=center+Vector3.up*h*(golden?1.65f:1.8f);
            if(!reduced)position+=Vector3.up*Mathf.Sin(t*Mathf.PI)*h*.16f;
            float frame=golden?PhoenixRadianceLayer.LoopFrame(phase,14):Mathf.SmoothStep(0,1,t/.75f)*11;
            bird.Draw(position,Vector2.one*width,0,frame,opacity,body,5,phase,.005f);
            float release=Mathf.Clamp01((t-.35f)/.65f);
            for(int i=0;i<feathers.Length;i++)
            {
                float a=i*2.399963f;
                float p=Mathf.Clamp01(release*1.3f-(i%3)*.08f);
                Vector3 end=center+new Vector3(Mathf.Cos(a)*width*.9f,Mathf.Sin(a)*width*.52f,0);
                var at=Vector3.Lerp(position,end,p)+Vector3.up*Mathf.Sin(p*Mathf.PI)*h*.5f;
                float size=h*(.35f+.12f*(i%3));
                feathers[i].Draw(at,Vector2.one*size,-a*Mathf.Rad2Deg+phase*20,golden?8:9,Mathf.Sin(p*Mathf.PI)*opacity*.8f,body,6,phase,.01f);
            }
        }
    }
}
