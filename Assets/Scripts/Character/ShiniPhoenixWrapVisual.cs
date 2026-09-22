using UnityEngine;
namespace Vampire
{
    [DefaultExecutionOrder(520)]
    public sealed class ShiniPhoenixWrapVisual : MonoBehaviour
    {
        sealed class Wing
        {
            const int Columns=36, Rows=14;
            public readonly MeshRenderer renderer;
            readonly Mesh mesh; readonly Vector3[] vertices=new Vector3[(Columns+1)*(Rows+1)];
            readonly Color[] colors=new Color[(Columns+1)*(Rows+1)]; readonly int side;
            public Wing(Transform parent,Material material,Sprite art,int side)
            {
                this.side=side;var obj=new GameObject(side<0?"Phoenix left folded wing":"Phoenix right folded wing");obj.transform.SetParent(parent,false);
                renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.enabled=false;
                mesh=new Mesh {name="Curved phoenix feather surface"};mesh.MarkDynamic();obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                var uv=new Vector2[vertices.Length];var triangles=new int[Columns*Rows*6];int ti=0;
                for(int y=0;y<=Rows;y++)for(int x=0;x<=Columns;x++)
                {
                    float u=(float)x/Columns,v=(float)y/Rows,sx=side<0?.9f-u*.9f:.1f+u*.9f;
                    uv[y*(Columns+1)+x]=new Vector2((art.rect.x+sx*art.rect.width)/art.texture.width,(art.rect.y+v*art.rect.height)/art.texture.height);
                    if(x<Columns&&y<Rows){int a=y*(Columns+1)+x,b=a+Columns+1;triangles[ti++]=a;triangles[ti++]=b;triangles[ti++]=a+1;triangles[ti++]=a+1;triangles[ti++]=b;triangles[ti++]=b+1;}
                }
                mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            }
            public void Pose(Vector3 shoulder,float fold,float span,float phase,float alpha,SpriteRenderer body,int order)
            {
                float px=0,pz=0;
                for(int x=0;x<=Columns;x++)
                {
                    float u=(float)x/Columns;
                    float yaw=fold*(65+105*Mathf.SmoothStep(0,1,(u-.26f)/.52f))*Mathf.Deg2Rad;
                    if(x>0){px+=Mathf.Cos(yaw)*span/Columns;pz-=Mathf.Sin(yaw)*span/Columns;}
                    for(int y=0;y<=Rows;y++)
                    {
                        float v=(float)y/Rows;
                        float flutter=(Mathf.Sin(phase*17+u*13+v*7)+.4f*Mathf.Sin(phase*29-u*21))*.012f*u;
                        float featherY=(v-(side<0?.34f:.30f))*span*.68f;
                        float perspective=1-pz*.14f;
                        int i=y*(Columns+1)+x;
                        vertices[i]=shoulder+new Vector3(side*px*perspective,(featherY-u*.09f*fold+flutter)*perspective,pz);
                        float shade=.84f+.16f*Mathf.Abs(Mathf.Cos(yaw));colors[i]=new Color(1,shade,shade,alpha);
                    }
                }
                mesh.vertices=vertices;mesh.colors=colors;mesh.RecalculateBounds();
                renderer.sortingLayerID=body.sortingLayerID;renderer.sortingOrder=body.sortingOrder+order;renderer.enabled=alpha>0;
            }
            public void Dispose(){Object.Destroy(mesh);}
        }
        Character owner;CharacterSkillRuntime skill;SpriteRenderer body,rear,burst;
        Wing left,right;readonly SpriteRenderer[] sparks=new SpriteRenderer[22];
        Material material;float burstRemaining,phase;bool wasSummoning;
        public bool WingsVisible => left!=null&&left.renderer.enabled;
        public bool CorrectLayerOrder => rear.sortingOrder<body.sortingOrder&&left.renderer.sortingOrder>body.sortingOrder&&right.renderer.sortingOrder>left.renderer.sortingOrder;
        public float WrapProgress {get;private set;}
        public void Bind(Character c,CharacterSkillRuntime s)
        {
            owner=c;skill=s;body=c.GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            material=new Material(Shader.Find("Vampire/PhoenixWrapFire"));
            rear=Make("Phoenix body behind Shini",-2);burst=Make("Phoenix ignition burst",6);
            var art=s.Definition.fireWrapParts;if(art==null||art.Length<4)return;
            material.mainTexture=art[0].texture;
            left=new Wing(transform,material,art[1],-1);right=new Wing(transform,material,art[2],1);
            for(int i=0;i<sparks.Length;i++)sparks[i]=Make("Phoenix rising ember",5);
        }
        SpriteRenderer Make(string name,int order)
        {var r=new GameObject(name).AddComponent<SpriteRenderer>();r.transform.SetParent(transform,false);r.sharedMaterial=material;r.enabled=false;if(body!=null){r.sortingLayerID=body.sortingLayerID;r.sortingOrder=body.sortingOrder+order;}return r;}
        void Set(SpriteRenderer r,Sprite sprite,Vector3 pos,float width,float angle,float alpha)
        {
            r.sprite=sprite;r.transform.position=pos;r.transform.rotation=Quaternion.Euler(0,0,angle);
            float scale=width/sprite.bounds.size.x;r.transform.localScale=new Vector3(scale/Mathf.Abs(transform.lossyScale.x),scale/Mathf.Abs(transform.lossyScale.y),1);
            r.color=new Color(1,1,1,alpha);r.enabled=alpha>0;
        }
        void LateUpdate()
        {
            if(left==null||body==null)return;
            var art=skill.Definition.fireWrapParts;
            if(!owner.IsAlive){Hide();return;}
            phase+=Time.deltaTime;material.SetFloat("_FlameTime",phase);
            rear.sortingOrder=body.sortingOrder-2;burst.sortingOrder=body.sortingOrder+6;
            bool summon=skill.IsSummoning;
            if(wasSummoning&&!summon&&skill.Active)burstRemaining=.45f;
            wasSummoning=summon;
            float t=1-skill.SummonRemaining/PhoenixSkillVisual.Duration;
            WrapProgress=Mathf.SmoothStep(0,1,(t-.42f)/.43f);
            rear.enabled=left.renderer.enabled=right.renderer.enabled=summon;
            foreach(var spark in sparks)spark.enabled=summon;
            if(summon)
            {
                float fade=Mathf.SmoothStep(0,1,t/.12f),descend=Mathf.SmoothStep(0,1,(t-.3f)/.25f);
                Vector3 center=transform.position+Vector3.up*(Mathf.Lerp(1,.35f,descend)-.55f*WrapProgress);
                float spread=Mathf.SmoothStep(0,1,t/.28f);
                float fold=Mathf.Lerp(.45f,0,spread)+WrapProgress;
                float span=Mathf.Lerp(1.6f,1.4f,WrapProgress);
                Set(rear,art[0],center+Vector3.up*.12f,1.45f*(1+.018f*Mathf.Sin(phase*13)),0,fade);
                left.Pose(transform.InverseTransformPoint(center+Vector3.left*.25f),fold,span,phase,fade,body,3);
                right.Pose(transform.InverseTransformPoint(center+Vector3.right*.25f),fold*1.025f,span,phase+.25f,fade,body,4);
                for(int i=0;i<sparks.Length;i++)
                {
                    float age=Mathf.Repeat(phase*(.8f+.15f*(i%4))+i*.173f,1);
                    Vector3 pos=center+new Vector3(Mathf.Sin(i*13.7f)*Mathf.Lerp(1.6f,.65f,WrapProgress),age*1.1f-.25f,0);
                    sparks[i].sortingOrder=body.sortingOrder+5;
                    Set(sparks[i],art[3],pos,.035f+.025f*(i%3),i*31,fade*(1-age));
                }
                float ignition=Mathf.SmoothStep(0,1,(t-.88f)/.12f);
                Set(burst,art[3],transform.position,Mathf.Lerp(.25f,1.25f,ignition),t*70,ignition*.8f);
            }
            else if(burstRemaining>0)
            {
                burstRemaining=Mathf.Max(0,burstRemaining-Time.deltaTime);float p=1-burstRemaining/.45f;
                Set(burst,art[3],transform.position,Mathf.Lerp(1.3f,2.3f,p),p*45,1-p);
            }
            else burst.enabled=false;
        }
        void Hide(){rear.enabled=burst.enabled=false;if(left!=null){left.renderer.enabled=right.renderer.enabled=false;foreach(var spark in sparks)spark.enabled=false;}burstRemaining=0;wasSummoning=false;}
        void OnDisable(){if(rear!=null)Hide();}
        void OnDestroy(){left?.Dispose();right?.Dispose();if(material!=null)Destroy(material);}
    }
}
