using UnityEngine;
namespace Vampire
{
    public sealed class FoodBossVisual:MonoBehaviour
    {
        public SpriteRenderer Body {get;private set;}
        public float Lift,Roll,Recoil,CreamPulse;public bool Airborne,Charge,Recovering,ClawsActive,Whipping,Curtaining;
        public Vector2 LeftClaw,RightClaw;public float ClawAngleLeft,ClawAngleRight;
        FoodBossRuntime boss;SpriteRenderer[] claws,arms;SpriteRenderer shadow;Transform rig;float clock,clawBlend;int facing=-1;
        SpriteRenderer suctionMouth,beetleWing;LineRenderer[] suction,cocoonBands;float beetleWalkClock,idleBlend,cocoonBlend;
        public bool IdlePlaying {get;private set;}
        public float BeetleArtRatio=>FoodBossArt.WalkScale(FoodBossKind.Tangdengi,boss.PhaseTwo)/(4.2f*100/244f);
        Material articulatedMaterial,bodyMaterial;MaterialPropertyBlock bodyProperties;
        public int Facing=>facing;
        public Vector2 SnailMouth=>(Vector2)transform.position+new Vector2(facing*1.35f,.70f);
        // Anatomical sockets are authored against each current left-facing illustration.
        public Vector2 HornTip=>(Vector2)transform.position+new Vector2(facing*(boss.PhaseTwo?1.91f:1.66f),boss.PhaseTwo?1.63f:1.51f)*BeetleArtRatio;
        public Vector2 BodyPixel(float x,float y)
        {
            var s=Body.sprite;
            return Body.transform.TransformPoint(new Vector3((x-s.pivot.x)/s.pixelsPerUnit,(s.rect.height-y-s.pivot.y)/s.pixelsPerUnit,0));
        }
        public Vector2 NoodleTip=>(Vector2)transform.position+new Vector2(-facing*(boss.PhaseTwo?.245f:.158f),2.90f);
        public Vector2 TongueSocket=>(Vector2)transform.position+new Vector2(facing*.57f,.48f);
        public Vector2 ShotOrigin=>boss.Kind==FoodBossKind.Rolpangi?SnailMouth:(Vector2)transform.position+(boss.Kind==FoodBossKind.Denikani?new Vector2(facing*.95f,1.05f):boss.Kind==FoodBossKind.Maribi?new Vector2(facing*.28f,.85f):boss.Kind==FoodBossKind.Tangdengi?new Vector2(facing*1.35f,.9f):new Vector2(0,1.05f));
        public Vector2 RestClaw(int i)=>new Vector2(facing*(i==0?1.35f:1.40f),i==0?.42f:1.15f);
        public Vector2 ClawShotOrigin(int i)
        {
            // Read the displayed claw transform so the muzzle stays attached throughout blending.
            var claw=claws[i];
            return (Vector2)claw.transform.TransformPoint(Vector3.left*(claw.sprite!=null?claw.sprite.bounds.size.x*.32f:0));
        }
        public void Initialize(FoodBossRuntime owner)
        {
            boss=owner;rig=new GameObject("Fixed-size art rig").transform;rig.SetParent(transform,false);
            Body=new GameObject("Body").AddComponent<SpriteRenderer>();Body.transform.SetParent(rig,false);Body.sortingOrder=510;
            bodyMaterial=Body.sharedMaterial;bodyProperties=new MaterialPropertyBlock();
            if(boss.Kind==FoodBossKind.Hurumi)articulatedMaterial=new Material(Resources.Load<Shader>("FoodBosses/ArticulatedBody"));
            if(boss.Kind==FoodBossKind.Tangdengi){
                beetleWing=FoodBossArt.Make(rig,"Wing",2.0f,509);beetleWing.enabled=false;
                cocoonBands=new LineRenderer[7];
                for(int i=0;i<cocoonBands.Length;i++){
                    var line=FoodBossEffects.Line(Body.transform,new[]{Vector2.zero,Vector2.zero},.035f,new Color(1,.79f,.30f,.75f),false,512);
                    line.name="Translucent sugar cocoon band";line.useWorldSpace=false;line.transform.localPosition=Vector3.zero;line.transform.localRotation=Quaternion.identity;line.transform.localScale=Vector3.one;line.positionCount=25;line.enabled=false;cocoonBands[i]=line;
                    for(int j=0;j<25;j++){float u=j/24f,a=(u-.5f)*Mathf.PI;line.SetPosition(j,new Vector3(.35f+i*.105f+Mathf.Cos(a)*.13f,.67f+Mathf.Sin(a)*(.37f-.035f*Mathf.Abs(i-3)),0));}
                }
            }
            shadow=new GameObject("Ground shadow").AddComponent<SpriteRenderer>();shadow.transform.SetParent(transform,false);shadow.sprite=BombCircleArt.Sprite;shadow.color=new Color(0,0,0,.22f);shadow.sortingOrder=479;
            claws=new[]{FoodBossArt.Make(transform,"Claw1",1.35f,512),FoodBossArt.Make(transform,"Claw1",1.1f,509)};
            foreach(var c in claws)c.enabled=false;
            arms=new SpriteRenderer[4];for(int i=0;i<4;i++)arms[i]=FoodBossArt.Make(transform,"CrabArm",.8f,510);
            suctionMouth=new GameObject("Inhaling mouth").AddComponent<SpriteRenderer>();suctionMouth.transform.SetParent(transform,false);suctionMouth.sprite=BombCircleArt.Sprite;suctionMouth.color=new Color(.24f,.10f,.055f);suctionMouth.sortingOrder=513;suctionMouth.enabled=false;
            suction=new LineRenderer[3];for(int i=0;i<3;i++){suction[i]=FoodBossEffects.Line(transform,new[]{Vector2.zero,Vector2.zero},.035f,new Color(1,.96f,.82f,.7f),false,512);suction[i].enabled=false;}
            ResetPose();Sample();
        }
        public void Face(Vector2 dir){if(Mathf.Abs(dir.x)>.05f)facing=dir.x<0?-1:1;}
        public void ResetPose(){Lift=Roll=Recoil=CreamPulse=0;Airborne=Charge=Recovering=ClawsActive=Whipping=Curtaining=false;LeftClaw=RestClaw(0);RightClaw=RestClaw(1);if(Body!=null){Body.color=Color.white;Body.enabled=true;Body.sharedMaterial=bodyMaterial;}}
        void LateUpdate(){if(boss==null||FoodBossRuntime.Paused)return;clock+=Time.deltaTime;Sample();}
        void Sample()
        {
            if(boss==null)return;bool p=boss.PhaseTwo;float width=FoodBossArt.Widths[(int)boss.Kind];
            int frame=boss.Moving||Charge?Mathf.FloorToInt(clock*(Charge?16:8))%8:0;
            if(boss.Kind==FoodBossKind.Tangdengi){
                if(boss.Moving&&!Charge)beetleWalkClock+=Time.deltaTime;
                else if(!Charge)beetleWalkClock=0;
                frame=Charge||Recovering?17:boss.Moving?1+Mathf.FloorToInt(beetleWalkClock*(16f/.96f))%16:0;
            }
            shadow.enabled=!Airborne;shadow.transform.localScale=new Vector3(width*.72f, .28f,1)/Mathf.Max(.01f,shadow.sprite.bounds.size.x);
            Body.sprite=FoodBossArt.Walk(boss.Kind,p,frame);Body.transform.localScale=Vector3.one*FoodBossArt.WalkScale(boss.Kind,p);
            Body.sharedMaterial=(Whipping||Curtaining)&&articulatedMaterial!=null?articulatedMaterial:bodyMaterial;
            if((Whipping||Curtaining)&&Body.sprite!=null){var rect=Body.sprite.rect;Body.GetPropertyBlock(bodyProperties);bodyProperties.SetVector("_Cell",new Vector4(rect.x,rect.y,rect.width,rect.height));bodyProperties.SetFloat("_Phase",p?1:0);bodyProperties.SetFloat("_TwoNoodles",Curtaining?1:0);Body.SetPropertyBlock(bodyProperties);}
            Body.transform.localPosition=Vector3.zero;Body.transform.localRotation=Quaternion.identity;
            string pose=null;float poseWidth=width;
            clawBlend=Mathf.MoveTowards(clawBlend,ClawsActive&&!Recovering?1:0,Time.deltaTime*6);
            if(boss.Kind==FoodBossKind.Denikani&&(clawBlend>0||Recovering)) {pose=Recovering?"CrabRecovery":"CrabBody"+(p?2:1);poseWidth=width*.81f;}
            if(boss.Kind==FoodBossKind.Maribi&&Recovering){pose="ScallopClosed";}
            if(boss.Kind==FoodBossKind.Tangdengi){
                // Fold the approved six legs beneath the same carapace, then wrap it in sugar.
                // The old unrelated four-leg pupa illustration must never replace this body.
                cocoonBlend=Mathf.MoveTowards(cocoonBlend,Recovering?1:0,Time.deltaTime*3);
                foreach(var band in cocoonBands){band.enabled=cocoonBlend>.01f&&!boss.Transitioning&&!boss.Dead;var color=band.startColor;color.a=.75f*cocoonBlend;band.startColor=band.endColor=color;}
                if(cocoonBlend>0){Body.transform.localRotation=Quaternion.Euler(0,0,-12*cocoonBlend);Body.transform.localPosition=Vector3.up*.12f*cocoonBlend;}
                beetleWing.enabled=Charge&&!Recovering&&!boss.Transitioning;
                if(beetleWing.enabled){
                    beetleWing.color=Body.color;
                    beetleWing.transform.localRotation=Quaternion.Euler(0,0,16+Mathf.Sin(clock*48)*12);
                    float ws=1.95f*BeetleArtRatio/beetleWing.sprite.bounds.size.x;
                    beetleWing.transform.localScale=new Vector3(-ws,ws*(.40f+.50f*Mathf.Abs(Mathf.Sin(clock*48))),1);
                    // Keep the membrane's base pinned beneath the single strawberry wing cover.
                    Vector3 hinge=new Vector3(beetleWing.sprite.bounds.size.x*.42f,-beetleWing.sprite.bounds.size.y*.33f,0);
                    beetleWing.transform.localPosition=new Vector3(.55f,1.05f,0)*BeetleArtRatio-beetleWing.transform.localRotation*Vector3.Scale(hinge,beetleWing.transform.localScale);
                    Body.transform.localPosition=Vector3.up*.12f;
                }
            }
            if(boss.Kind==FoodBossKind.Hurumi&&Airborne){pose="UdonAir"+(p?2:1);poseWidth=width*(p?1.07f:1);}
            if(pose!=null){Body.sprite=FoodBossArt.Get(pose);if(Body.sprite!=null){Body.transform.localScale=Vector3.one*(poseWidth/Body.sprite.bounds.size.x);Body.transform.localPosition=Vector3.up*(Body.sprite.bounds.size.y*Body.transform.localScale.x*.5f);}}
            float hop=boss.Kind==FoodBossKind.Maribi&&boss.Moving&&!Airborne?Mathf.Abs(Mathf.Sin(clock*5))*.22f:0;
            rig.localPosition=Vector3.up*(Lift+hop)+Vector3.right*(Recoil*facing);
            rig.localScale=new Vector3(-facing,1,1);
            ApplyIdle();
            if(Recovering&&boss.Kind==FoodBossKind.Denikani)Body.transform.localRotation=Quaternion.Euler(0,0,Roll);
            bool inhale=Recovering&&boss.Kind==FoodBossKind.Rolpangi;
            suctionMouth.enabled=inhale||CreamPulse>.01f;
            if(inhale){suctionMouth.transform.position=SnailMouth;suctionMouth.transform.localScale=new Vector3(.42f+.07f*Mathf.Sin(clock*12),.26f+.06f*Mathf.Sin(clock*12),1)/suctionMouth.sprite.bounds.size.x;}
            else if(CreamPulse>0){suctionMouth.transform.position=SnailMouth;suctionMouth.transform.localScale=new Vector3(.30f,.18f+.05f*Mathf.Sin(clock*12)*CreamPulse,1)/suctionMouth.sprite.bounds.size.x;}
            for(int i=0;i<3;i++){var r=suction[i];r.enabled=inhale;if(!inhale)continue;r.positionCount=16;
                float slide=Mathf.Repeat(clock*1.5f+i*.33f,1),reach=Mathf.Lerp(1.7f,.12f,slide);
                for(int j=0;j<16;j++){float u=j/15f;Vector2 v=new Vector2(facing*(reach+.42f*u),(i-1)*.30f*u+Mathf.Sin(u*Mathf.PI)*.06f);r.SetPosition(j,SnailMouth+v);}}
            for(int i=0;i<2;i++)
            {
                var c=claws[i];c.enabled=clawBlend>0&&!Recovering&&boss.Kind==FoodBossKind.Denikani;
                c.sprite=FoodBossArt.Get((Charge?"Claw":"ClawOpen")+(p&&i==0?2:1));
                float clawWidth=i==0&&p?1.68f:i==0?1.35f:1.1f;
                if(c.sprite!=null)c.transform.localScale=Vector3.one*(clawWidth/c.sprite.bounds.size.x);
                Vector2 center=Vector2.Lerp(RestClaw(i),i==0?LeftClaw:RightClaw,Mathf.SmoothStep(0,1,clawBlend));
                c.transform.localPosition=center;
                Vector2 shoulder=new Vector2(facing*(i==0?.20f:.70f),i==0?.68f:.95f);
                Vector2 axis=(center-shoulder).normalized;if(axis.sqrMagnitude<.1f)axis=Vector2.right*facing;
                float angle=Mathf.Atan2(axis.y,axis.x)*Mathf.Rad2Deg+180;
                c.transform.localRotation=Quaternion.Euler(0,0,angle);
                // The rightmost round socket of the original claw is its wrist, not the center of the pincer.
                Vector2 start=(Vector2)transform.position+shoulder,end=(Vector2)c.transform.position-axis*(clawWidth*.40f);
                Vector2 span=end-start,bend=FoodBossRuntime.Rotate(span.normalized,90)*(i==0?-1:1);
                Vector2 elbow=(start+end)*.5f+bend*Mathf.Lerp(.30f,.10f,Mathf.Clamp01(span.magnitude/4));
                for(int j=0;j<2;j++){var arm=arms[i*2+j];arm.enabled=c.enabled;arm.sortingOrder=i==0?511:508;if(arm.sprite==null)continue;Vector2 a=j==0?start:elbow,b=j==0?elbow:end,delta=b-a;arm.transform.position=(a+b)*.5f;arm.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);arm.transform.localScale=new Vector3((delta.magnitude+.18f)/arm.sprite.bounds.size.x,.32f/arm.sprite.bounds.size.y,1);}
            }
        }
        void ApplyIdle()
        {
            IdlePlaying=!boss.Dead&&!boss.Transitioning&&!boss.Busy&&!boss.Moving&&!Recovering&&!Airborne&&!Charge;
            // An attack owns the pose immediately; only entering rest blends in. No rescaling of the boss.
            idleBlend=IdlePlaying?Mathf.MoveTowards(idleBlend,1,Time.deltaTime*5):0;
            if(idleBlend<=0)return;
            float t=clock,dy=0,angle=0;
            switch(boss.Kind){
                case FoodBossKind.Rolpangi:dy=.018f*(1-Mathf.Cos(t*2.6f));angle=.65f*Mathf.Sin(t*1.3f);break;
                case FoodBossKind.Denikani:dy=.023f*(1-Mathf.Cos(t*3.1f));angle=.45f*Mathf.Sin(t*1.55f);break;
                case FoodBossKind.Maribi:dy=.065f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*2.4f)),4);angle=1.2f*Mathf.Sin(t*1.2f);break;
                case FoodBossKind.Tangdengi:dy=.018f*(1-Mathf.Cos(t*3.5f));angle=.35f*Mathf.Sin(t*1.75f);break;
                case FoodBossKind.Hurumi:dy=.065f*Mathf.Sin(t*2);angle=1.1f*Mathf.Sin(t);break;
            }
            Body.transform.localPosition+=Vector3.up*(dy*idleBlend);
            Body.transform.localRotation=Quaternion.Euler(0,0,angle*idleBlend);
        }
        void OnDestroy(){if(articulatedMaterial!=null)Destroy(articulatedMaterial);}
    }
}
