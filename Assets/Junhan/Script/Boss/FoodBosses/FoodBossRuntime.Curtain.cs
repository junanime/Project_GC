using System.Collections;
using UnityEngine;
namespace Vampire
{
    public sealed partial class FoodBossRuntime
    {
        IEnumerator ConnectedCurtain()
        {
            Visual.Face(Aim);Visual.Curtaining=true;yield return null;
            Vector2 direction=Mathf.Abs(Aim.x)>Mathf.Abs(Aim.y)?Vector2.right:Vector2.up;if(Vector2.Dot(direction,Aim)<0)direction=-direction;
            bool vertical=Mathf.Abs(direction.y)>.5f;float spacing=vertical?1.75f:1.25f;
            Vector2 side=Rotate(direction,90),center=(Vector2)Player.transform.position+direction*.8f;
            var root=new FoodBossCurtainTube[2];var entry=new Vector2[2];var plantHoles=new LineRenderer[2];
            for(int i=0;i<2;i++){
                root[i]=FoodBossCurtainTube.Create(Effects,.69f,513+i*3);
                entry[i]=(Vector2)transform.position+new Vector2(Visual.Facing*(1.7f+i*.35f),.10f-i*.35f);
                plantHoles[i]=CurtainHole(entry[i]);plantHoles[i].enabled=false;
            }
            var warnings=new LineRenderer[7];var noodles=new FoodBossCurtainTube[7,3];var holes=new LineRenderer[7,5];
            for(int lane=0;lane<7;lane++){
                Vector2 row=center+side*((lane-3)*spacing);
                warnings[lane]=CurtainWarning(row,direction,.48f);warnings[lane].enabled=CurtainLane(lane,3);
                for(int k=0;k<3;k++)noodles[lane,k]=FoodBossCurtainTube.Create(Effects,.64f,501+lane*3);
                float[] ends={-3.35f,-1.0833f,.2667f,2.4667f,3.7667f};
                for(int k=0;k<5;k++){holes[lane,k]=CurtainHole(row+direction*ends[k]);holes[lane,k].enabled=false;}
            }
            for(float t=0;t<1.1f;t+=Time.deltaTime){DrawCurtainRoots(root,entry,Mathf.SmoothStep(0,1,t/1.1f),t);for(int i=0;i<2;i++)plantHoles[i].enabled=t>.75f;yield return null;}
            Audio.Play(FoodBossSound.Skill1);float hitAt=0;int gap=3;var levels=new float[7];
            for(float t=0;t<5.85f;t+=Time.deltaTime){
                DrawCurtainRoots(root,entry,1-Mathf.SmoothStep(0,1,(t-5.2f)/.65f),t);
                int nextGap=PhaseTwo?(t<1.7f?3:t<3.2f?4:2):3;
                float grace=PhaseTwo?(t<1.7f?1:t<3.2f?t-1.7f:t-3.2f):1;
                float distance=t<3.1f?Mathf.Clamp01(t/3.1f):1-Mathf.SmoothStep(0,1,(t-4)/1.2f);
                for(int lane=0;lane<7;lane++){
                    bool targeted=CurtainLane(lane,nextGap);
                    bool safe=!targeted||grace<.65f&&!CurtainLane(lane,gap);
                    levels[lane]=Mathf.MoveTowards(levels[lane],safe?0:1,Time.deltaTime*4);
                    Vector2 row=center+side*((lane-3)*spacing);
                    warnings[lane].enabled=t<5.2f&&targeted;
                    var color=warnings[lane].startColor;color.a=grace<.65f&&lane==gap?.48f:.18f;warnings[lane].startColor=warnings[lane].endColor=color;
                    float level=levels[lane];
                    for(int k=0;k<3;k++){
                        float start=k==0?-3.35f:k==1?.2667f:3.7667f;
                        float width=k==0?2.2667f:k==1?2.2f:1.1333f;
                        Vector2 a=row+direction*start,d=a+direction*width;
                        Vector2 rise=(vertical?side*(lane%2==0?1:-1)*1.55f+Vector2.up*.15f:Vector2.up*2.1f)*level;
                        if(k==2){rise*=.72f;d+=rise*.5f;}
                        float reveal=Mathf.Clamp01((distance-(k==0?.09f:k==1?.47f:.86f))/(k==0?.29f:k==1?.29f:.14f));
                        noodles[lane,k].Draw(a,a+direction*.17f+rise,d-direction*.20f+rise,d,reveal,t,level,k<2);
                        // Ground contact follows the revealed strip, never the buried link or an unannounced gap.
                        Vector2 end=row+direction*(start+width*reveal);
                        if(!safe&&level>.75f&&reveal>.12f&&Time.time>=hitAt&&SnailBossProjectile.SegmentDistance(Player.transform.position,a,end)<.40f){Player.TakeDamage(12);hitAt=Time.time+.7f;}
                    }
                    float[] arrival={.09f,.38f,.47f,.76f,.86f};for(int k=0;k<5;k++)holes[lane,k].enabled=level>.05f&&distance>arrival[k];
                }
                if(grace>=.65f)gap=nextGap;
                yield return null;
            }
            Visual.Curtaining=false;Audio.Stop(FoodBossSound.Skill1,.12f);
            foreach(var n in root)Destroy(n.gameObject);foreach(var n in noodles)Destroy(n.gameObject);foreach(var h in plantHoles)Destroy(h.gameObject);foreach(var h in holes)Destroy(h.gameObject);foreach(var w in warnings)Destroy(w.gameObject);
        }
        void DrawCurtainRoots(FoodBossCurtainTube[] tubes,Vector2[] entry,float blend,float time)
        {
            float dx=PhaseTwo?14:0,dy=PhaseTwo?5:0;
            for(int i=0;i<2;i++){
                Vector2 a=Visual.BodyPixel((i==0?162:96)+dx,(i==0?98:112)+dy);
                Vector2 b=Visual.BodyPixel((i==0?192:100)+dx,(i==0?30:50)+dy);
                Vector2 c=Visual.BodyPixel((i==0?145:69)+dx,(i==0?11:38)+dy);
                Vector2 d=Visual.BodyPixel((i==0?132:52)+dx,(i==0?34:63)+dy);
                tubes[i].Draw(a,Vector2.Lerp(b,a+new Vector2(Visual.Facing*1.8f,1.3f),blend),Vector2.Lerp(c,entry[i]+Vector2.up*1.2f,blend),Vector2.Lerp(d,entry[i],blend),1,time);
            }
        }
        static bool CurtainLane(int lane,int gap)=>gap==3?(lane==2||lane==4):gap==4?(lane==2||lane==5):(lane==1||lane==3);
        LineRenderer CurtainWarning(Vector2 row,Vector2 direction,float alpha)
        {return FoodBossEffects.Line(Effects,new[]{row-direction*3.35f,row+direction*4.9f},.80f,new Color(Warning.r,Warning.g,Warning.b,alpha));}
        LineRenderer CurtainHole(Vector2 at)
        {var oval=FoodBossEffects.Arc(Vector2.zero,.32f);for(int j=0;j<oval.Length;j++)oval[j]=at+new Vector2(oval[j].x,oval[j].y*.40f);return FoodBossEffects.Line(Effects,oval,.10f,new Color(.37f,.20f,.12f,.75f),true,487);}
    }
}
