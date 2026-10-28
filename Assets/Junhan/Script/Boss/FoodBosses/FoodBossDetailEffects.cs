using UnityEngine;
namespace Vampire
{
    // Shared, world-space hazard fill. Mesh and outline use the same boundary.
    public sealed class FoodBossHazardFill:MonoBehaviour
    {
        Mesh mesh;Material material;
        public static FoodBossHazardFill Add(LineRenderer line,Vector2[] boundary,Color color)
        {
            var go=new GameObject("Translucent hatched danger area");go.transform.SetParent(line.transform,false);
            var f=go.AddComponent<FoodBossHazardFill>();f.mesh=new Mesh();
            var vertices=new Vector3[boundary.Length];var colors=new Color[vertices.Length];
            color.a=.32f;
            for(int i=0;i<vertices.Length;i++){vertices[i]=go.transform.InverseTransformPoint(boundary[i]);colors[i]=color;}
            var triangles=new int[(vertices.Length-2)*3];
            for(int i=0;i<vertices.Length-2;i++){triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i+2;}
            f.mesh.vertices=vertices;f.mesh.colors=colors;f.mesh.triangles=triangles;f.mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh=f.mesh;
            var r=go.AddComponent<MeshRenderer>();f.material=new Material(Resources.Load<Shader>("FoodBosses/Hazard"));r.sharedMaterial=f.material;r.sortingOrder=line.sortingOrder-1;
            return f;
        }
        public void Fade(float alpha){if(material!=null)material.SetFloat("_Opacity",alpha);}
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }

    // Preserve the approved original three-layer noodle material and articulated outline.
    public sealed class FoodBossNoodle:MonoBehaviour
    {
        LineRenderer[] layers;public Vector2[] Points {get;private set;}
        public static FoodBossNoodle Create(Transform parent,float width,int order=514)
        {
            var n=new GameObject("Articulated udon noodle").AddComponent<FoodBossNoodle>();n.transform.SetParent(parent,false);
            n.layers=new[]{
                FoodBossEffects.Line(n.transform,new[]{Vector2.zero,Vector2.zero},width,new Color(.25f,.13f,.065f),false,order),
                FoodBossEffects.Line(n.transform,new[]{Vector2.zero,Vector2.zero},width-.075f,new Color(1,.85f,.59f),false,order+1),
                FoodBossEffects.Line(n.transform,new[]{Vector2.zero,Vector2.zero},Mathf.Max(.08f,width-.22f),new Color(1,.96f,.81f),false,order+2)};
            return n;
        }
        public void Set(Vector2[] points,float visibility=1)
        {
            Points=points;for(int k=0;k<3;k++){var r=layers[k];r.enabled=visibility>.015f;r.positionCount=points.Length;
                var c=r.startColor;c.a=visibility;r.startColor=r.endColor=c;
                for(int j=0;j<points.Length;j++)r.SetPosition(j,points[j]+(k==2?Vector2.up*.045f:Vector2.zero));}
        }
    }

    public sealed class FoodBossDust:MonoBehaviour
    {
        SpriteRenderer art;Vector2 velocity;float age,life,size;
        public static void Emit(FoodBossRuntime b,Vector2 at,Vector2 direction,int count=4)
        {
            for(int i=0;i<count;i++){
                var p=new GameObject("Sugar skid dust").AddComponent<FoodBossDust>();p.transform.SetParent(b.Effects,true);p.transform.position=at;
                p.art=p.gameObject.AddComponent<SpriteRenderer>();p.art.sprite=BombCircleArt.Sprite;p.art.sortingOrder=508;
                p.art.color=new Color(.96f,.86f,.65f,.7f);p.velocity=FoodBossRuntime.Rotate(direction,Random.Range(-65,65))*Random.Range(.7f,2.3f)+Vector2.up*.35f;
                p.life=Random.Range(.35f,.65f);p.size=Random.Range(.1f,.23f);p.transform.localScale=Vector3.one*(p.size/p.art.sprite.bounds.size.x);
            }
        }
        void Update(){if(FoodBossRuntime.Paused)return;age+=Time.deltaTime;if(age>=life){Destroy(gameObject);return;}transform.position+=(Vector3)velocity*Time.deltaTime;velocity*=Mathf.Exp(-3*Time.deltaTime);var c=art.color;c.a=.7f*(1-age/life);art.color=c;transform.localScale=Vector3.one*(size*(1+age/life)/art.sprite.bounds.size.x);}
    }

    public sealed class FoodBossEnergyBeam:MonoBehaviour
    {
        FoodBossRuntime boss;Vector2 origin,direction;float age,nextHit,duration,length;FoodBossFlowRibbon core;LineRenderer halo;LineRenderer[] pulses;
        public static FoodBossEnergyBeam Create(FoodBossRuntime b,Vector2 origin,Vector2 direction,float length=9,float duration=1.1f)
        {
            var f=new GameObject("Horn energy wave").AddComponent<FoodBossEnergyBeam>();f.transform.SetParent(b.Effects,false);f.boss=b;f.origin=origin;f.direction=direction;f.length=length;f.duration=duration;
            f.halo=FoodBossEffects.Line(f.transform,new[]{origin,origin},1.05f,new Color(.56f,1,.24f,.22f),false,520);
            f.core=FoodBossFlowRibbon.Create(f.transform,new Color(.64f,1,.28f),521,2);
            f.pulses=new LineRenderer[5];for(int i=0;i<5;i++)f.pulses[i]=FoodBossEffects.Line(f.transform,new[]{origin,origin},.045f,new Color(.94f,1,.66f,.85f),false,523);
            return f;
        }
        void Update()
        {
            if(boss==null||boss.Dead){Destroy(gameObject);return;}if(FoodBossRuntime.Paused)return;age+=Time.deltaTime;if(age>=duration){Destroy(gameObject);return;}
            float reach=length*Mathf.SmoothStep(0,1,age/.30f),fade=Mathf.SmoothStep(0,1,(duration-age)/.24f),pulse=1+.035f*Mathf.Sin(age*25);Vector2 end=origin+direction*reach,side=FoodBossRuntime.Rotate(direction,90);
            var points=new Vector2[45];for(int j=0;j<points.Length;j++){float u=j/(float)(points.Length-1);points[j]=Vector2.Lerp(origin,end,u)+side*(.025f*Mathf.Sin(u*18-age*20)*Mathf.Sin(u*Mathf.PI));}
            core.Set(points,.72f*pulse*fade,age,fade);
            halo.SetPosition(0,origin);halo.SetPosition(1,end);halo.startWidth=halo.endWidth=1.05f*fade;
            var hc=halo.startColor;hc.a=.22f*fade;halo.startColor=halo.endColor=hc;
            for(int k=0;k<pulses.Length;k++){float d=Mathf.Repeat(age*11+k*1.8f,length);var r=pulses[k];r.enabled=d+.22f<reach;r.positionCount=17;var c=r.startColor;c.a=.80f*fade;r.startColor=r.endColor=c;for(int j=0;j<17;j++){float a=(j/16f-.5f)*Mathf.PI;r.SetPosition(j,origin+direction*(d+Mathf.Cos(a)*.18f)+side*(Mathf.Sin(a)*.31f*fade));}}
            if(age<duration-.12f&&boss.Player!=null&&boss.Player.IsAlive&&Time.time>=nextHit&&SnailBossProjectile.SegmentDistance(boss.Player.transform.position,origin,end)<.36f*fade+.14f){boss.Player.TakeDamage(14);nextHit=Time.time+.7f;}
        }
    }
}
