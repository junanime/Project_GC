using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    // The float tube is the contact surface on land and the buoyancy surface in acid.
    public sealed class AmphibiousCapsule:InteractableEventObject
    {
        public const float Width=3.2f;
        public bool Landed {get;private set;}
        public bool Ready {get;set;}
        public float Immersion {get;private set;}
        public float HatchOpen {get;private set;}
        public float RotorAngle=>rotor!=null?rotor.localEulerAngles.z:0;
        public Vector3 BoardingPoint=>visual.TransformPoint(new Vector3(.28f,.99f,0));
        public float Waterline=>transform.position.y+.29f;
        StageProgression owner;Transform visual,rotor,leftDoor,rightDoor;SpriteRenderer shell,shadow;
        Material immersion,plain;float height,age,fallHeight,speed,fanSpeed;Vector3 previous;
        readonly List<SpriteRenderer> chutes=new List<SpriteRenderer>();readonly List<Mesh> meshes=new List<Mesh>();
        LineRenderer wake;Material wakeMaterial;
        protected override bool KeepVisibleAfterInteraction=>true;
        protected override bool AllowSubmergedInteraction=>true;
        protected override bool InteractionAvailable=>Ready&&owner!=null&&owner.CanEnter(CurrentPlayer);
        protected override Component PromptOwner=>shell!=null?(Component)shell:this;
        protected override bool ExecuteInteraction(Character p)=>owner!=null&&owner.TryBoard(p,this);
        public static AmphibiousCapsule Create(StageProgression owner,Vector2 at)
        {
            var go=new GameObject("대장행 수륙양용 캡슐");go.transform.position=at;
            var hit=go.AddComponent<BoxCollider2D>();hit.isTrigger=true;hit.size=new Vector2(Width+1.2f,2.6f);hit.enabled=false;
            var c=go.AddComponent<AmphibiousCapsule>();c.owner=owner;c.Build();return c;
        }
        void Build()
        {
            visual=new GameObject("Floating capsule body").transform;visual.SetParent(transform,false);
            shell=new GameObject("Cream mint capsule and float tube").AddComponent<SpriteRenderer>();shell.transform.SetParent(visual,false);
            shell.sprite=Resources.Load<Sprite>("AmphibiousCapsule/Body");float scale=Width/shell.sprite.bounds.size.x;
            shell.transform.localScale=Vector3.one*scale;height=shell.sprite.bounds.size.y*scale;
            shell.transform.localPosition=Vector3.up*height*.5f;shell.sortingLayerName="Monster Full";shell.sortingOrder=32010;
            immersion=new Material(Resources.Load<Shader>("AmphibiousCapsule/Immersion"));shell.sharedMaterial=immersion;
            plain=new Material(Shader.Find("Sprites/Default"));
            // Coordinates measured against the tightly imported body sprite, in world units.
            Vector2 hatch=new Vector2(.28f,.99f);leftDoor=Panel("Hatch left door",hatch+Vector2.left*.143f,new Vector2(.286f,.73f),new Color(.67f,.89f,.78f));
            rightDoor=Panel("Hatch right door",hatch+Vector2.right*.143f,new Vector2(.286f,.73f),new Color(.40f,.75f,.65f));
            rotor=new GameObject("Three rotating propeller blades").transform;rotor.SetParent(visual,false);rotor.localPosition=new Vector3(1.07f,1.54f);rotor.localScale=new Vector3(.82f,1,1);
            for(int i=0;i<3;i++)
            {
                var blade=new List<Vector2>();for(int j=0;j<24;j++){float a=j*Mathf.PI*2/24;blade.Add(new Vector2(Mathf.Cos(a)*.10f,.23f+Mathf.Sin(a)*.19f));}
                var part=Polygon("Propeller blade",rotor,blade,new Color(1,.90f,.65f),32013);part.localRotation=Quaternion.Euler(0,0,i*120);
            }
            var hub=new List<Vector2>();for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24;hub.Add(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.085f);}Polygon("Coral rotor hub",rotor,hub,new Color(1,.35f,.38f),32014);
            shadow=new GameObject("Ground-only capsule shadow").AddComponent<SpriteRenderer>();shadow.transform.SetParent(transform,false);shadow.sprite=SupplyCapsuleArt.Droplet;
            shadow.transform.localScale=new Vector3(Width*.9f,.45f,1);shadow.color=new Color(.25f,.06f,.14f,.18f);GroundVisualSorting.ApplyBackground(shadow,-5);
            for(int i=0;i<2;i++){var s=new GameObject("Descent parachute").AddComponent<SpriteRenderer>();s.transform.SetParent(visual,false);s.sprite=SupplyCapsuleArt.Parachute;s.sortingLayerName="Monster Full";s.sortingOrder=32015;s.transform.localPosition=new Vector3(i==0?-1f:.8f,1.5f);s.transform.localScale=Vector3.one*(1.3f/s.sprite.bounds.size.x);chutes.Add(s);}
            wake=new GameObject("Tube water contact ripple").AddComponent<LineRenderer>();wake.transform.SetParent(transform,false);wake.useWorldSpace=true;wake.loop=false;wake.positionCount=33;wake.widthMultiplier=.035f;wake.sortingLayerName="Monster Full";wake.sortingOrder=32015;
            wakeMaterial=new Material(Shader.Find("Sprites/Default"));wake.sharedMaterial=wakeMaterial;wake.enabled=false;previous=transform.position;SetHatch(0);
            gameObject.AddComponent<MapMarker>().Configure(MapMarkerKind.Event,"수륙양용 캡슐 · 대장으로 이동");
        }
        Transform Panel(string name,Vector2 at,Vector2 size,Color color)
        {
            var points=new List<Vector2>();for(int corner=0;corner<4;corner++)for(int j=0;j<7;j++){float a=(corner*90+j*90f/6)*Mathf.Deg2Rad;float r=.055f;float cx=corner==0||corner==3?1:-1,cy=corner<2?1:-1;points.Add(new Vector2(cx*(size.x*.5f-r)+Mathf.Cos(a)*r,cy*(size.y*.5f-r)+Mathf.Sin(a)*r));}
            var p=Polygon(name,visual,points,color,32012);p.localPosition=at;
            var line=p.gameObject.AddComponent<LineRenderer>();line.sharedMaterial=plain;line.useWorldSpace=false;line.loop=true;line.positionCount=points.Count;line.widthMultiplier=.012f;line.startColor=line.endColor=new Color(.12f,.32f,.31f);line.sortingLayerName="Monster Full";line.sortingOrder=32013;
            for(int i=0;i<points.Count;i++)line.SetPosition(i,points[i]);return p;
        }
        Transform Polygon(string name,Transform parent,List<Vector2> points,Color color,int order)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);var vertices=new Vector3[points.Count+1];var colors=new Color[vertices.Length];var tri=new int[points.Count*3];
            for(int i=0;i<points.Count;i++){vertices[i+1]=points[i];tri[i*3]=0;tri[i*3+1]=i+1;tri[i*3+2]=(i+1)%points.Count+1;}for(int i=0;i<colors.Length;i++)colors[i]=color;
            var mesh=new Mesh();mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=tri;mesh.RecalculateBounds();meshes.Add(mesh);go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=plain;r.sortingLayerName="Monster Full";r.sortingOrder=order;return go.transform;
        }
        public void SetHatch(float amount){HatchOpen=Mathf.Clamp01(amount);if(leftDoor==null)return;leftDoor.localPosition=new Vector3(.28f-.143f-.31f*HatchOpen,.99f);rightDoor.localPosition=new Vector3(.28f+.143f+.31f*HatchOpen,.99f);leftDoor.localScale=rightDoor.localScale=new Vector3(1-.9f*HatchOpen,1,1);}
        public IEnumerator Descend()
        {
            for(float t=0;t<2.6f;t+=Time.deltaTime){fallHeight=7*(1-Mathf.SmoothStep(0,1,t/2.6f));yield return null;}
            fallHeight=0;Landed=true;GetComponent<Collider2D>().enabled=true;StartCoroutine(Splash());
            for(float t=0;t<.65f;t+=Time.deltaTime){foreach(var s in chutes){s.color=new Color(1,1,1,1-t/.65f);s.transform.localScale=new Vector3(s.transform.localScale.x,Mathf.Lerp(1,.1f,t/.65f),1);}yield return null;}
            foreach(var s in chutes)s.enabled=false;
        }
        IEnumerator Splash()
        {
            var drops=new List<SpriteRenderer>();bool wet=AcidLakeEvent.Contains(transform.position);
            for(int i=0;i<12;i++){var s=new GameObject("Landing droplet").AddComponent<SpriteRenderer>();s.sprite=SupplyCapsuleArt.Droplet;s.sortingLayerName="Monster Full";s.sortingOrder=32016;s.transform.localScale=Vector3.one*.07f;drops.Add(s);}
            for(float t=0;t<.6f;t+=Time.deltaTime){for(int i=0;i<drops.Count;i++){float side=i%2==0?-1:1;drops[i].transform.position=transform.position+new Vector3(side*(1.15f+t*(.4f+i*.08f)),Mathf.Sin(t/.6f*Mathf.PI)*(.18f+i*.015f),0);var c=wet?new Color(.79f,.94f,.19f):new Color(1,.52f,.65f);c.a=1-t/.6f;drops[i].color=c;}yield return null;}foreach(var s in drops)Destroy(s.gameObject);
        }
        protected override void Update(){base.Update();}
        void LateUpdate()
        {
            if(shell==null)return;float dt=Time.deltaTime;age+=dt;
            float target=Landed&&AcidLakeEvent.Contains(transform.position)?1:0;Immersion=Mathf.MoveTowards(Immersion,target,dt*2.4f);
            speed=Mathf.Lerp(speed,dt>0?Vector2.Distance(previous,transform.position)/dt:0,dt*8);previous=transform.position;
            fanSpeed=Mathf.MoveTowards(fanSpeed,owner!=null&&owner.CapsuleBoarded?780:0,dt*900);rotor.Rotate(0,0,-fanSpeed*dt);
            visual.localPosition=new Vector3(0,fallHeight+Immersion*Mathf.Sin(age*3.2f)*.025f,0);
            float cut=Mathf.Lerp(transform.position.y-.15f,Waterline,Immersion);immersion.SetFloat("_CutY",cut);immersion.SetFloat("_Wet",Immersion);immersion.SetFloat("_Clock",age);
            shadow.enabled=Immersion<.05f;shadow.color=new Color(.25f,.06f,.14f,Mathf.Lerp(.05f,.18f,1-fallHeight/7));
            wake.enabled=Immersion>.01f;wake.startColor=wake.endColor=new Color(.90f,1,.56f,Immersion*.7f);
            for(int i=0;i<33;i++){float a=Mathf.PI+i*Mathf.PI/32;wake.SetPosition(i,transform.position+new Vector3(Mathf.Cos(a)*(1.52f+.04f*Mathf.Sin(age*3)),.30f+Mathf.Sin(a)*.13f+Mathf.Sin(i*.9f+age*4)*.014f,0));}
        }
        void OnDestroy(){if(immersion!=null)Destroy(immersion);if(plain!=null)Destroy(plain);if(wakeMaterial!=null)Destroy(wakeMaterial);foreach(var m in meshes)if(m!=null)Destroy(m);}
    }
}
