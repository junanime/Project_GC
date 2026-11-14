using System.Collections;
using UnityEngine;

namespace Vampire
{
    public static class ColonBossArrival
    {
        // Spawn animation is harmless. Collider/targetability becomes live only after the final settle.
        public static IEnumerator Play(ColonBossRuntime b)
        {
            b.Habitat?.BeginEmergence();var v=b.Visual;
            if(b.Kind==ColonBossKind.Shark)
            {
                b.SetHidden(true);var fin=ColonBossEffects.Prop(b,"Fin",1.8f,b.transform.position);
                Vector2 center=b.Player.transform.position,origin=b.transform.position;
                yield return b.Tween(1.7f,q=>{float a=q*Mathf.PI*2;fin.transform.position=FiniteStomachMap.SafeSpawn(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*3.3f,.8f);fin.flipX=Mathf.Sin(a)>0;});
                Object.Destroy(fin.gameObject);Vector2 start=FiniteStomachMap.SafeSpawn(center+Vector2.right*4,1.5f),end=FiniteStomachMap.ClampDash(start,center+Vector2.left*3,1.5f);
                b.transform.position=start;b.SetHidden(false);v.Pose=2;v.Face(Vector2.left);b.Sound("Dash",.35f);
                yield return b.Tween(.9f,q=>{b.MoveTo(Vector2.Lerp(start,end,q));v.Lift=Mathf.Sin(q*Mathf.PI)*2;});
                var final=FiniteStomachMap.SafeSpawn(center+Vector2.right*4.1f,1.5f);var from=(Vector2)b.transform.position;
                yield return b.Tween(.9f,q=>{b.MoveTo(Vector2.Lerp(from,final,Mathf.SmoothStep(0,1,q)));v.Pose=-1;});
                yield return b.Wait(.35f);yield break;
            }
            if(b.Kind==ColonBossKind.Pudding)
            {
                b.SetHidden(true);Vector2 at=b.transform.position;
                var mold=ColonBossEffects.Prop(b,"Mold",b.Definition.Size*1.1f,at,517);
                yield return b.Tween(.85f,q=>mold.transform.position=at+Vector2.right*Mathf.Sin(q*35)*.06f*Mathf.Sin(q*Mathf.PI));
                b.SetHidden(false);b.Sound("Land",.6f);v.Summon(.5f);
                yield return b.Tween(.8f,q=>{mold.transform.position=at+new Vector2(q*2,Mathf.Sin(q*Mathf.PI*.62f)*6);mold.transform.rotation=Quaternion.Euler(0,0,q*120);mold.color=new Color(1,1,1,1-q);v.Summon(.5f+q*.5f);});
                Object.Destroy(mold.gameObject);v.ResetPose();
                yield return b.Tween(.95f,q=>{v.Squash=Mathf.Sin(q*5*Mathf.PI)*.10f*(1-q);v.Stretch=-v.Squash*.35f;});yield break;
            }
            if(b.Kind==ColonBossKind.Manatee&&b.Habitat!=null)
            {
                b.SetHidden(true);
                for(int i=0;i<3;i++)
                {
                    b.Habitat.Door(i,1);b.PeekFromDoor(i,true);b.SetHidden(false);
                    yield return b.Tween(.55f,q=>{v.Opacity=Mathf.Sin(q*Mathf.PI);v.Lift=-.4f+Mathf.Sin(q*Mathf.PI)*.55f;});b.SetHidden(true);
                }
                var start=b.Habitat.DoorExit(1);var target=FiniteStomachMap.SafeSpawn(start+b.Habitat.WallNormal*2,1.5f);
                b.PeekFromDoor(1,false);b.SetHidden(false);v.Opacity=1;
                yield return b.Tween(1.1f,q=>{b.MoveTo(Vector2.Lerp(start,target,Mathf.SmoothStep(0,1,q)));v.Summon(.45f+.55f*q);v.Lift=Mathf.Sin(q*Mathf.PI)*.35f;});yield break;
            }
            float duration=b.Kind==ColonBossKind.Flamingo?2.35f:b.Kind==ColonBossKind.Hedgehog?2.6f:2.2f;
            b.SetHidden(false);
            yield return b.Tween(duration,q=>{
                v.Summon(q);
                if(b.Kind==ColonBossKind.Hedgehog&&q<.42f)v.Roll=Mathf.Sin(q/.42f*Mathf.PI*2)*7;
                else v.Roll=0;
                // Turtle extends its original flippers without stretching the body or lifting the belly.
                if(b.Kind==ColonBossKind.Turtle)v.Lift=0;
            });
            b.Sound("Land",.28f);yield return b.Wait(.35f);
        }
    }
}
