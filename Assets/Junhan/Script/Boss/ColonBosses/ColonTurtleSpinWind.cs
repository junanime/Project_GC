using UnityEngine;

namespace Vampire
{
    // One unwrapped angular clock drives the shell and the faster surrounding air.
    // No duplicate body, flip animation, hit volume, or independently drifting particle timer.
    [DefaultExecutionOrder(100)]
    public sealed class ColonTurtleSpinWind : MonoBehaviour
    {
        public const float Windup=.22f,Travel=1.5f,Settle=.34f;
        public const float Revolutions=6,WindRatio=1.65f;
        public const float Duration=Windup+Travel+Settle;
        public const float TopSpeed=Revolutions*360/(Travel+(Windup+Settle)*.5f);
        public float BodyAngle { get; private set; }
        public float WindAngle => BodyAngle*WindRatio;
        public float Strength { get; private set; }
        public Vector2 WorldCenter { get; private set; }
        ColonBossRuntime owner;
        LineRenderer[] trails;
        Material material;
        readonly Vector3[][] points={new Vector3[41],new Vector3[41]};
        static readonly float[] Offsets={0,143,267},Sweeps={112,76,43};
        static readonly float[] Radii={.60f,.65f,.57f},Widths={.17f,.075f,.045f};

        public static float Angle(float seconds)
        {
            float t=Mathf.Clamp(seconds,0,Duration);
            if(t<Windup){float u=t/Windup;return TopSpeed*Windup*(u*u*u-.5f*u*u*u*u);}
            float distance=Windup*.5f;
            if(t<=Windup+Travel)return TopSpeed*(distance+t-Windup);
            float q=(t-Windup-Travel)/Settle;
            return TopSpeed*(distance+Travel+Settle*(q-q*q*q+.5f*q*q*q*q));
        }
        public static ColonTurtleSpinWind Create(ColonBossRuntime boss)
        {
            var go=new GameObject("Melon shell · same-direction spinning wind");
            go.transform.SetParent(boss.Effects,false);
            var wind=go.AddComponent<ColonTurtleSpinWind>();wind.owner=boss;
            var shader=Resources.Load<Shader>("FoodBosses/SoftFlow")??Shader.Find("Sprites/Default");
            wind.material=new Material(shader);wind.material.SetFloat("_Mode",2);
            wind.trails=new LineRenderer[6];
            for(int i=0;i<wind.trails.Length;i++)
            {
                int ribbon=i/2;
                var r=FoodBossEffects.Line(go.transform,new Vector2[0],Widths[ribbon],Color.white,false,i%2==0?508:516);
                r.name="Wind "+ribbon+(i%2==0?" · behind shell":" · in front of shell");
                r.sharedMaterial=wind.material;r.numCapVertices=3;r.numCornerVertices=3;
                r.widthCurve=new AnimationCurve(new Keyframe(0,.05f),new Keyframe(.55f,1),new Keyframe(1,.02f));
                r.enabled=false;wind.trails[i]=r;
            }
            return wind;
        }
        public void Set(float bodyAngle,float strength)
        {
            BodyAngle=bodyAngle;Strength=Mathf.Clamp01(strength);Draw();
        }
        void LateUpdate()
        {
            if(owner==null||owner.Dead){gameObject.SetActive(false);Destroy(gameObject);return;}
            if(owner.EncounterPaused||owner.Immobilized)return;
            Draw();
        }
        void Draw()
        {
            if(owner==null||trails==null)return;
            Vector2 center=WorldCenter=owner.Visual.GroundSpinCenter;
            float radius=owner.Definition.Size;
            for(int ribbon=0;ribbon<3;ribbon++)
            {
                int back=0,front=0;
                // Unequal arcs make the rotational direction readable instead of a flashing ring.
                for(int j=0;j<=40;j++)
                {
                    float a=(WindAngle+Offsets[ribbon]-Sweeps[ribbon]*(1-j/40f))*Mathf.Deg2Rad;
                    float y=Mathf.Sin(a);
                    var p=(Vector3)(center+new Vector2(Mathf.Cos(a)*radius*Radii[ribbon],y*radius*Radii[ribbon]*ColonBossVisual.GroundPlaneY));
                    if(y>=0)points[0][back++]=p;else points[1][front++]=p;
                }
                for(int side=0;side<2;side++)
                {
                    var r=trails[ribbon*2+side];int count=side==0?back:front;
                    r.enabled=Strength>.005f&&count>=2&&owner.Visual.Visible;
                    if(!r.enabled)continue;
                    r.positionCount=count;for(int j=0;j<count;j++)r.SetPosition(j,points[side][j]);
                    var c=Color.Lerp(new Color(.73f,.91f,.57f),new Color(1,.98f,.78f),ribbon*.36f);
                    c.a=Strength*(ribbon==0?.82f:.54f);r.startColor=r.endColor=c;
                    r.widthMultiplier=Widths[ribbon]*Mathf.Lerp(.45f,1,Strength);
                    ColonAttackWall.Mask(r.gameObject,owner.transform.position);
                }
            }
        }
        void OnDisable(){if(trails!=null)foreach(var r in trails)if(r!=null)r.enabled=false;}
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
