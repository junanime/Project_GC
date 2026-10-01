using UnityEngine;
namespace Vampire
{
    // One immutable shell per phase. Only flesh deforms; toppings have independent transforms.
    public sealed class SnailBossVisual : MonoBehaviour
    {
        public SpriteRenderer Shell {get;private set;}
        public SpriteRenderer Body {get;private set;}
        public SpriteRenderer Toppings {get;private set;}
        public SnailAction Action {get;private set;}
        public bool Chocolate {get;private set;}
        public float Width {get;private set;}
        Transform facingRoot; float actionTime,clock,spin,floatHeight; bool faceRight;
        SpriteRenderer ghostA,ghostB;Material missingMaterial;float stumble;int missing;bool dashExpression;
        public Vector3 Mouth=>facingRoot.TransformPoint(new Vector3(-Width*.30f,Width*.135f,0));
        public void Configure(float width,bool chocolate=false)
        {
            Width=width;
            if(facingRoot==null)
            {
                facingRoot=new GameObject("Locked scale visual rig").transform; facingRoot.SetParent(transform,false);
                Body=SnailBossArt.Make(facingRoot,"Body1",width,502);
                Shell=SnailBossArt.Make(facingRoot,"Shell1",width*.77f,501);
                Toppings=SnailBossArt.Make(facingRoot,"Toppings1",width*.53f,503);
                ghostA=SnailBossArt.Make(facingRoot,"Shell1",width*.77f,502);
                ghostB=SnailBossArt.Make(facingRoot,"Shell1",width*.77f,502);
            }
            SetPhase(chocolate); Pose(SnailAction.Idle);
        }
        public void SetPhase(bool chocolate)
        {
            Chocolate=chocolate; string p=chocolate?"2":"1";
            SnailBossArt.Set(Shell,"Shell"+p,Width*.77f);
            SnailBossArt.Set(Toppings,"Toppings"+p,Width*.53f);
            SnailBossArt.Set(ghostA,"Shell"+p,Width*.77f);SnailBossArt.Set(ghostB,"Shell"+p,Width*.77f);
            SetMissing(missing);
            RefreshBody(); ApplyPose(0);
        }
        public void Face(float deltaX) { if(Mathf.Abs(deltaX)>.1f) faceRight=deltaX>0; }
        public void Stumble(){stumble=.65f;}
        public void SetMissing(int mask)
        {
            missing=mask;
            if(missingMaterial==null)
            {
                var shader=Resources.Load<Shader>("SnailBoss/MissingFruit");
                if(shader==null)return;missingMaterial=new Material(shader);
                var empty=SnailBossArt.Get("EmptyShell1");if(empty!=null)missingMaterial.SetTexture("_EmptyTex",empty.texture);
            }
            missingMaterial.SetVector("_Missing",Chocolate?Vector4.zero:new Vector4((mask&1)!=0?1:0,(mask&2)!=0?1:0,(mask&4)!=0?1:0,(mask&8)!=0?1:0));
            Shell.sharedMaterial=missingMaterial;
        }
        public void Pose(SnailAction action)
        {
            if(action==SnailAction.DashPuff)dashExpression=true;
            if(action==SnailAction.Puff||action==SnailAction.Idle)dashExpression=false;
            if(Action!=action) {Action=action;actionTime=0;RefreshBody();}
        }
        void RefreshBody()
        {
            string key=Action==SnailAction.Groggy||Action==SnailAction.Dead?"Groggy":
                Action==SnailAction.Puff?"Puff":Action==SnailAction.DashPuff?"DashPuff":
                Action==SnailAction.Spit?(dashExpression?"Spit":"BasicSpit"):Action==SnailAction.Absorb?"BasicSpit":"Body";
            SnailBossArt.Set(Body,key+(Chocolate?"2":"1"),Width*1.12f);
        }
        void Update()
        {
            if(SnailBossRuntime.Paused) return;
            float dt=Time.deltaTime;clock+=dt;actionTime+=dt; ApplyPose(dt);
        }
        void ApplyPose(float dt)
        {
            if(facingRoot==null) return;
            facingRoot.localScale=new Vector3(faceRight?-1:1,1,1);
            if(stumble>0)
            {
                stumble=Mathf.Max(0,stumble-dt);float t=1-stumble/.65f;
                facingRoot.localRotation=Quaternion.Euler(0,0,t*360);
                facingRoot.localPosition=Vector3.up*Mathf.Sin(t*Mathf.PI)*.75f;
            }
            else {facingRoot.localRotation=Quaternion.identity;facingRoot.localPosition=Vector3.zero;}
            bool rolling=Action==SnailAction.Roll||Action==SnailAction.Transition;
            float breath=Mathf.Sin(clock*(Action==SnailAction.Walk?7:2.2f));
            // Body artwork has bottom-center pivot; shell uses center pivot.
            float bs=Width*1.12f/Body.sprite.bounds.size.x;
            float stretch=Action==SnailAction.Walk?.045f:.012f;
            Body.transform.localScale=new Vector3(bs*(1+breath*stretch),bs*(1-breath*stretch*.7f),1);
            // Equal 512x256 canvases: compensate original transparent bottom margins,
            // never rescale eyes/head just to align a different expression.
            bool loweredPose=Action==SnailAction.Spit||Action==SnailAction.Absorb||Action==SnailAction.Groggy||Action==SnailAction.Dead;
            float baselinePixels=(Chocolate?20:0)+(loweredPose?10:0);
            Body.transform.localPosition=new Vector3(0,-Width*.035f-baselinePixels*Width*1.12f/512f,0);
            Body.enabled=!rolling;
            float hop=Action==SnailAction.Roll?Mathf.Sin(Mathf.Clamp01(actionTime/.35f)*Mathf.PI)*.23f:0;
            Shell.transform.localPosition=new Vector3(Width*.11f,Width*.42f+hop,0);
            if(rolling) spin+=dt*1450;
            else spin=Mathf.LerpAngle(spin,0,Mathf.Min(1,dt*18));
            Shell.transform.localRotation=Quaternion.Euler(0,0,rolling?spin:Mathf.Sin(clock*3)* (Action==SnailAction.Walk?1.4f:.35f));
            for(int i=0;i<2;i++)
            {
                var ghost=i==0?ghostA:ghostB;
                ghost.enabled=rolling;ghost.transform.localPosition=Shell.transform.localPosition;
                ghost.transform.localRotation=Quaternion.Euler(0,0,spin+(ghost==ghostA?22:-22));ghost.color=new Color(1,1,1,.22f);
            }
            float target=rolling?Width*.18f:0;
            floatHeight=Mathf.MoveTowards(floatHeight,target,dt*Width*1.8f);
            float shellTop=Width*.42f+Shell.sprite.bounds.size.y*Shell.transform.localScale.y*.5f;
            Toppings.transform.localPosition=new Vector3(Width*.13f,shellTop-.15f+floatHeight+(rolling?Mathf.Sin(clock*9)*.025f:0),0);
            Toppings.transform.localRotation=Quaternion.Euler(0,0,rolling?Mathf.Sin(clock*5)*4:0);
            if(Action==SnailAction.Groggy) Body.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(clock*9)*2);
            else Body.transform.localRotation=Quaternion.identity;
        }
        public void Flash(float amount) { var tint=Color.Lerp(Color.white,new Color(1,.55f,.5f),amount);Shell.color=tint;Body.color=tint; }
        void OnDestroy(){if(missingMaterial!=null){if(Application.isPlaying)Destroy(missingMaterial);else DestroyImmediate(missingMaterial);}}
    }
}
