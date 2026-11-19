using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public sealed partial class ColonBossRuntime
    {
        readonly List<Monster> chestnuts = new List<Monster>();
        readonly Dictionary<Monster,uint> chestnutGenerations = new Dictionary<Monster,uint>();
        ColonChestnutRack chestnutRack;
        void PruneChestnuts()
        {
            chestnuts.RemoveAll(n=>n==null||!n.gameObject.activeInHierarchy||!n.IsAlive||n.HP<=0||
                !chestnutGenerations.TryGetValue(n,out uint generation)||n.SpawnGeneration!=generation);
            var stale=new List<Monster>();foreach(var pair in chestnutGenerations)if(!chestnuts.Contains(pair.Key))stale.Add(pair.Key);
            foreach(var n in stale)chestnutGenerations.Remove(n);
        }
        Vector2 At => transform.position;
        float Hit => Definition.AttackDamage;
        static Vector2 Rotate(Vector2 v,float degrees)=>FoodBossRuntime.Rotate(v,degrees);
        static float Ease(float x)=>Mathf.SmoothStep(0,1,x);
        IEnumerator Pattern(int index)
        {
            Visual.Face(Aim);
            switch(Kind)
            {
                case ColonBossKind.Flamingo: yield return Flamingo(index);break;
                case ColonBossKind.Shark: yield return Shark(index);break;
                case ColonBossKind.Pudding: yield return Pudding(index);break;
                case ColonBossKind.Hedgehog: yield return Hedgehog(index);break;
                case ColonBossKind.Turtle: yield return Turtle(index);break;
                case ColonBossKind.Manatee: yield return Manatee(index);break;
            }
            yield return Wait(.35f);
        }
        IEnumerator Warn(Vector2[] outline,float seconds,int pose=1,Vector2? origin=null)
        {
            Visual.Pose=pose;var warning=ColonBossEffects.Warning(this,outline,origin);
            yield return Wait(seconds);ColonBossEffects.Fade(warning,.15f);
        }
        IEnumerator Dash(Vector2 direction,float distance,float duration,int pose,bool roll=false,bool trail=false,bool recover=true)
        {
            Vector2 start=At,end=FiniteStomachMap.ClampDash(start,start+direction*distance,Definition.Size*.32f);
            yield return Warn(ColonBossEffects.Lane(start,end,Definition.Size*.75f),1.0f,1);
            Visual.Pose=pose;Sound("Dash");bool hit=false;float step=0;
            yield return Tween(duration,q=>{
                var before=At;MoveTo(Vector2.Lerp(start,end,Ease(q)));
                if(roll)Visual.Roll=q*720;
                if(!hit&&ColonAttackWall.Clear(At,Player.transform.position)&&SnailBossProjectile.SegmentDistance(Player.transform.position,before,At)<Definition.Size*.4f){TryHit(Hit);hit=true;}
                if(trail&&q>step){step+=.12f;ColonBossEffects.Stain(this,At,Definition.Size*.52f);}
            });
            Visual.Roll=Visual.GroundSpin=0;
            if(recover){Sound("Land",.32f);yield return Tween(.40f,q=>Visual.Squash=Mathf.Sin(q*Mathf.PI)*.08f);}
        }
        IEnumerator AwaitPose(int pose)
        {
            // Release only after the authored contact pose crosses its existing blend boundary.
            for(float elapsed=0;!Visual.IsPoseDisplayed(pose)&&elapsed<.24f;)
            {if(!EncounterPaused&&!Immobilized)elapsed+=Time.deltaTime;yield return null;}
        }
        IEnumerator BalletRecovery(bool slipped = false)
        {
            Visual.Pose=7;Visual.Lift=.06f;yield return AwaitPose(7);
            if (slipped && GStarDemo.Enabled)
            {
                Sound("Land",.28f);
                var crumbs = new SpriteRenderer[5];
                int face = Visual.Facing;
                Vector2 heel = At + new Vector2(face*.35f,.3f);
                for (int i=0;i<crumbs.Length;i++)
                    crumbs[i] = ColonBossEffects.Prop(this,"Projectile",.11f+i%2*.035f,heel);
                yield return Tween(1.05f, q =>
                {
                    Visual.Roll = face*8*Mathf.Sin(q*Mathf.PI*2)*Mathf.Exp(-q*3);
                    Visual.BalletLanding = q;
                    for (int i=0;i<crumbs.Length;i++)
                    {
                        float arc = Mathf.Sin(Mathf.Min(1,q*1.3f)*Mathf.PI);
                        Vector2 p = heel + new Vector2((i-2)*q*.50f,arc*(.35f+(i%3)*.14f)-q*.24f);
                        crumbs[i].transform.position = p;
                        crumbs[i].transform.rotation = Quaternion.Euler(0,0,q*(150+i*45));
                        crumbs[i].color = new Color(1,1,1,Mathf.Clamp01((1-q)*4));
                    }
                });
                foreach (var crumb in crumbs) if (crumb != null) Destroy(crumb.gameObject);
                Visual.Roll=Visual.Lift=Visual.BalletLanding=0; Visual.Pose=0;
                yield return Wait(.18f);
                yield break;
            }
            yield return Tween(.10f,q=>Visual.Lift=.06f*(1-Ease(q)));
            Sound("Land",.24f);yield return Tween(.62f,q=>Visual.BalletLanding=q);
            Visual.BalletLanding=0;Visual.Lift=0;Visual.Pose=0;yield return Wait(.14f);
        }
        IEnumerator FanStrike(Vector2 origin,Vector2 dir,float range,float spread,float windup,int pose,float damage)
        {
            yield return Warn(ColonBossEffects.Fan(origin,dir,range,spread),windup,pose,origin);
            Sound("Skill1");
            yield return ColonBossEffects.Wave(this,origin,dir,spread,range,.65f,damage);
        }
        void ShootFan(Vector2 origin,Vector2 dir,int count,float spread,float speed,float damage,string shape="seed",float radius=.20f)
        {
            Sound("Shot",.3f);
            for(int i=0;i<count;i++)ColonBossEffects.Projectile(this,origin,Rotate(dir,count==1?0:(i/(float)(count-1)-.5f)*spread),speed,damage,radius,shape);
        }
        IEnumerator Impact(Vector2 position,float radius,float wait,int pose,float damage)
        {
            yield return Warn(ColonBossEffects.Circle(position,radius),wait,pose,position);
            Sound("Land");ColonBossEffects.Burst(this,position,radius);
            if(ColonAttackWall.Clear(position,Player.transform.position)&&Vector2.Distance(Player.transform.position,position)<radius+.15f)TryHit(damage);
        }
        IEnumerator Flamingo(int index)
        {
            if(index==0)
            {
                yield return Dash(Aim,7f,1.15f,1,false,false,false);
                yield return BalletRecovery(true);yield break;
            }
            if(index==1)
            {
                Vector2 dir=Aim,at=At;
                var w1=ColonBossEffects.Warning(this,ColonBossEffects.Fan(at,Rotate(dir,32),8,28));
                var w2=ColonBossEffects.Warning(this,ColonBossEffects.Fan(at,Rotate(dir,-32),8,28));
                Visual.Pose=2;yield return Wait(.85f);ColonBossEffects.Fade(w1);ColonBossEffects.Fade(w2);
                yield return Tween(.9f,q=>{Visual.Lift=Mathf.Sin(q*Mathf.PI)*.9f;Visual.Pose=q<.5f?2:3;});
                // Two crossed diagonals, followed by the opened-leg lower diagonals, not a radial volley.
                LineRenderer nextLeft=null,nextRight=null;
                for(int n=0;n<2;n++)
                {
                    if(n==1){ColonBossEffects.Fade(nextLeft,.12f);ColonBossEffects.Fade(nextRight,.12f);}
                    Visual.Pose=n==0?2:3;
                    for(int side=-1;side<=1;side+=2)ShootFan(At+Vector2.up*.7f,Rotate(dir,side*(n==0?32:58)),3,16,3.3f,Hit*.68f,"sprinkle",.15f);
                    if(n==0)
                    {
                        Vector2 origin=At+Vector2.up*.7f;
                        nextLeft=ColonBossEffects.Warning(this,ColonBossEffects.Fan(origin,Rotate(dir,-58),8,28),origin);
                        nextRight=ColonBossEffects.Warning(this,ColonBossEffects.Fan(origin,Rotate(dir,58),8,28),origin);
                        nextLeft.name="Flamingo second diagonal · left";nextRight.name="Flamingo second diagonal · right";
                    }
                    yield return Wait(.65f);
                }
                yield return BalletRecovery();yield break;
            }
            Vector2 target=At+Aim*3.4f;
            yield return Impact(target,1.5f,1.05f,4,Hit);
            Visual.Pose=5;yield return Wait(.35f);Visual.Pose=6;yield return Wait(.18f);
            ShootFan(Visual.Mouth,((Vector2)Player.transform.position-Visual.Mouth).normalized,1,0,3.8f,Hit,"donut",.38f);
            yield return Wait(.65f);
        }
        IEnumerator Shark(int index)
        {
            if(index==0)
            {
                var fin=ColonBossEffects.Prop(this,"Fin",1.55f,At);SetHidden(true);
                yield return Tween(.9f,q=>{MoveTo(At+Aim*Time.deltaTime*1.3f);fin.transform.position=At;});
                SetHidden(false);Destroy(fin.gameObject);yield return Dash(Aim,8.0f,1.0f,1);yield break;
            }
            if(index==1)
            {
                Vector2 direction=Aim,origin=At;
                yield return Warn(ColonBossEffects.Fan(origin,direction,5,80),1.1f,2);
                Visual.Pose=3;Sound("Skill1");
                yield return ColonBossEffects.Wave(this,origin,direction,80,5,.65f,Hit);
                yield return Wait(.3f);yield break;
            }
            if(index==2)
            {
                Vector2 center=Player.transform.position;var fins=new List<SpriteRenderer>();var directions=new List<Vector2>();
                float offset=Random.Range(0,360f);
                // Five fins with a deliberately missing sixth sector (one full escape corridor).
                for(int i=0;i<5;i++){var dir=FoodBossRuntime.Dir(offset+i*60);var at=FiniteStomachMap.SafeSpawn(center+dir*5.2f,.5f);fins.Add(ColonBossEffects.Prop(this,"Fin",1.15f,at));directions.Add((center-at).normalized);}
                Visual.Pose=2;
                for(int i=0;i<fins.Count;i++)
                {
                    var fin=fins[i];Vector2 start=fin.transform.position,end=ColonAttackWall.Stop(start,center+directions[i]*1.7f,.43f);
                    var warn=ColonBossEffects.Warning(this,ColonBossEffects.Lane(start,end,.85f),start);yield return Wait(.65f);ColonBossEffects.Fade(warn);
                    bool hit=false;yield return Tween(.65f,q=>{Vector2 before=fin.transform.position;fin.transform.position=Vector2.Lerp(start,end,q);if(!hit&&ColonAttackWall.Clear(fin.transform.position,Player.transform.position)&&SnailBossProjectile.SegmentDistance(Player.transform.position,before,fin.transform.position)<.6f){TryHit(Hit*.8f);hit=true;}});
                    Destroy(fin.gameObject);
                }
                yield break;
            }
            var movingFin=ColonBossEffects.Prop(this,"Fin",1.6f,At);SetHidden(true);
            yield return Tween(.8f,q=>{MoveTo(At+Aim*Time.deltaTime*2);movingFin.transform.position=At;});
            Destroy(movingFin.gameObject);Vector2 landing=FiniteStomachMap.SafeSpawn(Player.transform.position,Definition.Size*.35f);
            var warning=ColonBossEffects.Warning(this,ColonBossEffects.Circle(landing,2.4f),landing);yield return Wait(1.15f);
            // Ground-point is locked before the leap, so the final strike never follows a dodging player.
            body.position=landing;transform.position=landing;Visual.Pose=4;Visual.Lift=.04f;
            yield return AwaitPose(4);SetHidden(false);ColonBossEffects.Fade(warning,.10f);
            Sound("Skill2");ColonSharkBreachFoam.Create(this,landing,2.4f,true);
            if(ColonAttackWall.Clear(landing,Player.transform.position)&&Vector2.Distance(Player.transform.position,landing)<2.4f)TryHit(Hit*1.1f);
            yield return Tween(1.05f,q=>{Visual.Pose=q<.18f?4:q<.46f?5:q<.86f?6:7;Visual.Lift=Mathf.Sin(q*Mathf.PI)*1.2f;Visual.Roll=Mathf.Sin(q*Mathf.PI)*-6;});
            Visual.Lift=Visual.Roll=0;Sound("Land",.22f);ColonSharkBreachFoam.Create(this,landing,1.15f,false);yield return Wait(.5f);
        }
        IEnumerator Pudding(int index)
        {
            if(index==0){yield return Dash(Aim,6.5f,1.3f,1,false,true);yield break;}
            if(index==1)
            {
                Visual.Pose=2;var aim=(Vector2)Player.transform.position;
                for(int i=0;i<3;i++){Vector2 target=FiniteStomachMap.SafeSpawn(aim+Rotate(Aim,90)*(i-1)*1.8f,.5f);StartCoroutine(PuddingLob(At+Vector2.up*.7f,target,1.3f,Hit*.85f));yield return Wait(.28f);}
                yield return Wait(2.3f);yield break;
            }
            if(index==2)
            {
                Vector2 front=Aim;
                for(int i=0;i<2;i++)
                {Vector2 side=Rotate(front,i==0?85:-85);Visual.Roll=i==0?-5:5;yield return FanStrike(At,side,4.2f,100,.85f,3,Hit);yield return Tween(.3f,q=>{Visual.Stretch=Mathf.Sin(q*Mathf.PI)*.07f;Visual.Squash=Mathf.Sin(q*Mathf.PI)*.05f;Visual.Roll=(i==0?-5:5)*(1-q);});}yield break;
            }
            if(index==3)
            {
                Visual.Pose=3;yield return Wait(.25f);Visual.Pose=4;yield return Wait(.45f);Vector2 dir=Aim;
                for(int i=0;i<3;i++)
                {
                    var warning=ColonBossEffects.Warning(this,ColonBossEffects.Fan(At,dir,7f,75));yield return Wait(i==2?.8f:.35f);
                    Visual.Pose=5;yield return AwaitPose(5);ColonBossEffects.Fade(warning);
                    Sound("Land",.35f);StartCoroutine(ColonBossEffects.Wave(this,At,dir,75,7,1.4f,Hit*.75f));
                    yield return Tween(.34f,q=>Visual.BellyPulse=q);Visual.BellyPulse=0;Visual.Pose=4;
                }
                yield return Wait(.95f);Visual.Pose=3;yield return Wait(.25f);yield break;
            }
            Visual.Pose=6;Vector2 start=At+Vector2.up*1.3f;
            var ball=ColonBossEffects.PuddingBall(this,start,.9f);
            for(int i=0;i<2;i++){Sound("Land",.23f);yield return Tween(.55f,q=>ball.transform.position=start+Vector2.up*Mathf.Sin(q*Mathf.PI)*1.5f);}
            Destroy(ball.gameObject);Vector2 dir2=Aim;yield return Warn(ColonBossEffects.Lane(At,At+dir2*9,1.2f),.7f,7);
            ShootFan(At+Vector2.up*.4f,dir2,1,0,5,Hit*1.1f,"pudding",.55f);yield return Wait(.6f);
        }
        IEnumerator PuddingLob(Vector2 from,Vector2 target,float radius,float damage)
        {
            var marker=ColonBossEffects.Warning(this,ColonBossEffects.Circle(target,radius),target);
            var ball=ColonBossEffects.PuddingBall(this,from,.68f);
            yield return Tween(.95f,q=>ball.transform.position=Vector2.Lerp(from,target,q)+Vector2.up*Mathf.Sin(q*Mathf.PI)*2.4f);
            Sound("Land",.22f);yield return Tween(.5f,q=>ball.transform.position=target+Vector2.up*Mathf.Sin(q*Mathf.PI)*.9f);
            if(ball!=null)Destroy(ball.gameObject);ColonBossEffects.Fade(marker);ColonBossEffects.Burst(this,target,radius);
            if(ColonAttackWall.Clear(target,Player.transform.position)&&Vector2.Distance(Player.transform.position,target)<radius+.15f)TryHit(damage);
        }
        IEnumerator Hedgehog(int index)
        {
            if(index==0){yield return Dash(Aim,6.8f,1.25f,2,true);Visual.Pose=3;yield return Wait(.3f);yield break;}
            if(index==1)
            {
                PruneChestnuts();Visual.Pose=4;Visual.Bare=true;
                Vector2 releaseAim=Aim;
                var returning=new List<ColonChestnutRecall>();
                foreach(var n in chestnuts)
                {
                    var carrier=n.GetComponent<ColonChestnutRecall>();if(carrier!=null&&carrier.Docked)returning.Add(carrier);
                }
                for(int i=0;i<returning.Count;i++)
                {
                    Vector2 direction=Rotate(releaseAim,(i-1)*45);
                    var origin=FiniteStomachMap.SafeSpawn(At+direction*2.4f,.4f);
                    ColonBossEffects.Warning(this,ColonBossEffects.Lane(origin,origin+direction*5,.6f),origin)
                        .gameObject.AddComponent<FoodBossFade>().Lifetime=.85f;
                }
                yield return Wait(.85f);
                foreach(var carrier in returning)
                {
                    if(carrier==null||!carrier.Docked)continue;
                    int i=returning.IndexOf(carrier);var direction=Rotate(releaseAim,(i-1)*45);
                    carrier.Depart(At+direction*2.4f,direction);yield return Wait(.16f);
                }
                // Uses the actual stage-two suicide special blueprint, including status/VFX/loot rules.
                var entities=FindObjectOfType<EntityManager>();var roster=ColonMonsterRoster.Active;
                if(entities!=null&&roster!=null)
                {
                    var entry=roster.Specials.Find(e=>e.id=="S02");
                    if(entry!=null)for(int i=chestnuts.Count;i<3;i++)
                    {
                        var point=FiniteStomachMap.SafeSpawn(At+Rotate(Aim,(i-1)*50)*2.4f,.4f);
                        var bp=level.CurrentLevelBlueprint;int pool=-1;
                        for(int p=0;p<bp.monsters.Length;p++)foreach(var source in bp.monsters[p].monsterBlueprints)if(roster.Resolve(source)==entry.blueprint){pool=p;break;}
                        if(pool>=0){var n=entities.SpawnMonster(pool,point,entry.blueprint,0,false);if(n!=null){chestnuts.Add(n);chestnutGenerations[n]=n.SpawnGeneration;}}
                        ColonBossEffects.Burst(this,point,.65f);yield return Wait(.3f);
                    }
                }
                yield return Wait(.45f);yield break;
            }
            if(index==2)
            {
                Vector2 aim=Aim;
                for(int i=0;i<2;i++){Vector2 dir=Rotate(aim,i==0?42:-42);yield return Warn(ColonBossEffects.Fan(At,dir,8,55),.8f,5);ShootFan(At+Vector2.up*.7f,dir,7,55,3.4f,Hit*.65f,"spike",.14f);yield return Wait(.55f);}yield break;
            }
            Visual.Pose=6;Visual.Bare=true;PruneChestnuts();yield return AwaitPose(6);
            if(chestnutRack==null)chestnutRack=ColonChestnutRack.Create(this);
            for(int i=0;i<chestnuts.Count;i++)
                if(chestnuts[i] is ExplodingMonster nut&&Vector2.Distance(nut.transform.position,At)<14)
                    ColonChestnutRecall.Begin(this,chestnutRack,nut,i);
            // Walk around navigation walls, then climb into the spine sockets. A blocked or
            // killed actor is never replaced/teleported merely to finish the presentation.
            for(float elapsed=0;elapsed<4.5f&&chestnutRack.Members.Exists(n=>n!=null&&n.Holding&&!n.Docked);)
            {if(!EncounterPaused&&!Immobilized)elapsed+=Time.deltaTime;yield return null;}
            foreach(var n in chestnutRack.Members.ToArray())if(n!=null&&!n.Docked)n.Cancel();
            Sound("Skill2",.3f);yield return Wait(.55f);
        }
        IEnumerator Turtle(int index)
        {
            if(index==0){yield return TurtleSpinDash();yield break;}
            if(index==1)
            {
                Vector2 direction=Aim;yield return Warn(ColonBossEffects.Fan(At,direction,9,65),.95f,6);
                ShootFan(Visual.Mouth,direction,5,65,2.8f,Hit*.75f,"melon seed",.25f);yield return Wait(.7f);
                ShootFan(Visual.Mouth,direction,7,65,3.6f,Hit*.55f,"melon seed",.16f);yield return Wait(.7f);yield break;
            }
            if(index==2)
            {
                Vector2 dir=Aim,side=Rotate(dir,90);Vector2 a=At+side*1.1f,z=At-side*1.1f;
                Vector2 endA=FiniteStomachMap.ClampDash(a,At+dir*7-side*2,.3f),endB=FiniteStomachMap.ClampDash(z,At+dir*7+side*2,.3f);
                var w1=ColonBossEffects.Warning(this,ColonBossEffects.Lane(a,endA,.85f),a);var w2=ColonBossEffects.Warning(this,ColonBossEffects.Lane(z,endB,.85f),z);Visual.Pose=7;
                yield return Wait(1.0f);ColonBossEffects.Fade(w1);ColonBossEffects.Fade(w2);Sound("Skill2");
                StartCoroutine(ColonBossEffects.Liquid(this,a,endA,.7f,3.5f,Hit*.65f));yield return Wait(.25f);
                StartCoroutine(ColonBossEffects.Liquid(this,z,endB,.7f,3.5f,Hit*.65f));yield return Wait(3.1f);yield break;
            }
            Vector2 forward=Aim;
            for(int i=0;i<4;i++)
            {
                int side=i%2;Visual.FlipperSide=side;Visual.FlipperSweep=0;Visual.Pose=side==0?4:5;
                yield return AwaitPose(side==0?4:5);
                Vector2 direction=Rotate(forward,side==0?-7:7),origin=Visual.FlipperTip(side);
                var warning=ColonBossEffects.Warning(this,ColonBossEffects.Lane(origin,origin+direction*8,1.0f),origin);
                yield return Wait(i==0?.70f:.20f);
                yield return Tween(.20f,q=>{
                    Visual.FlipperSweep=Ease(q);origin=Visual.FlipperTip(side);
                    var points=ColonBossEffects.Lane(origin,origin+direction*8,1.0f);
                    for(int p=0;p<points.Length;p++)warning.SetPosition(p,points[p]);ColonAttackWall.Mask(warning.gameObject,origin);
                });
                // The shader and socket share a sweep angle; launch from the visible tip.
                origin=Visual.FlipperTip(side);ColonBossEffects.Fade(warning,.12f);
                ShootFan(origin,direction,1,0,4.4f,Hit*.65f,"crescent",.40f);
                yield return Tween(.28f,q=>Visual.FlipperSweep=1-Ease(q));Visual.FlipperSweep=0;
            }
        }
        IEnumerator TurtleSpinDash()
        {
            Vector2 direction=Aim,start=At,end=FiniteStomachMap.ClampDash(start,start+direction*6,Definition.Size*.32f);
            yield return Warn(ColonBossEffects.Lane(start,end,Definition.Size*.75f),1.0f,1);
            Visual.Pose=2;yield return Wait(.16f);
            Visual.Pose=3;yield return Wait(.10f);
            yield return Tween(.18f,q=>Visual.GroundSpinProjection=Ease(q));
            var wind=ColonBossEffects.SpinWind(this);
            // Six full revolutions finish at the original orientation, with continuous acceleration.
            // The wind uses the same unwrapped angle at a higher speed, so it never reverses.
            yield return Tween(ColonTurtleSpinWind.Windup,q=>{
                float seconds=q*ColonTurtleSpinWind.Windup;
                Visual.GroundSpin=ColonTurtleSpinWind.Angle(seconds);
                wind.Set(Visual.GroundSpin,Ease(q));
            });
            Sound("Dash");bool hit=false;
            yield return Tween(ColonTurtleSpinWind.Travel,q=>{
                Vector2 before=At;MoveTo(Vector2.Lerp(start,end,Ease(q)));
                Visual.GroundSpin=ColonTurtleSpinWind.Angle(ColonTurtleSpinWind.Windup+q*ColonTurtleSpinWind.Travel);
                wind.Set(Visual.GroundSpin,1);
                if(!hit&&ColonAttackWall.Clear(At,Player.transform.position)&&SnailBossProjectile.SegmentDistance(Player.transform.position,before,At)<Definition.Size*.4f){TryHit(Hit);hit=true;}
            });
            Sound("Land",.32f);
            yield return Tween(ColonTurtleSpinWind.Settle,q=>{
                Visual.GroundSpin=ColonTurtleSpinWind.Angle(ColonTurtleSpinWind.Windup+ColonTurtleSpinWind.Travel+q*ColonTurtleSpinWind.Settle);
                wind.Set(Visual.GroundSpin,1-Ease(q));
            });
            wind.gameObject.SetActive(false);Destroy(wind.gameObject);Visual.GroundSpin=0;
            yield return Tween(.14f,q=>Visual.GroundSpinProjection=1-Ease(q));Visual.GroundSpinProjection=0;
            Visual.Pose=2;yield return Wait(.14f);Visual.Pose=1;yield return Wait(.14f);
        }
        IEnumerator Manatee(int index)
        {
            if(Habitat==null){yield return Wait(.5f);yield break;}
            if(index==0)
            {
                int entrance=0;float nearest=float.MaxValue;for(int i=0;i<3;i++){float d=Vector2.Distance(At,Habitat.DoorExit(i));if(d<nearest){nearest=d;entrance=i;}}
                Vector2 start=At,end=Habitat.DoorExit(entrance);yield return Tween(.7f,q=>{MoveTo(Vector2.Lerp(start,end,Ease(q)));Visual.Opacity=1-q;});
                SetHidden(true);int exit=(entrance+Random.Range(1,3))%3;Habitat.Door(exit,1);yield return Wait(.45f);
                Vector2 point=Habitat.DoorExit(exit),dir=((Vector2)Player.transform.position-point).normalized;
                Vector2 dashEnd=FiniteStomachMap.ClampDash(point,Habitat.ClampTerritory(point+dir*5),Definition.Size*.32f);
                var warning=ColonBossEffects.Warning(this,ColonBossEffects.Lane(point,dashEnd,2.4f),point);yield return Wait(.9f);ColonBossEffects.Fade(warning);
                body.position=point;transform.position=point;Visual.Opacity=1;SetHidden(false);Visual.Pose=2;Sound("Dash");
                bool hit=false;
                yield return Tween(.9f,q=>{Vector2 before=At;MoveTo(Vector2.Lerp(point,dashEnd,Ease(q)));Visual.Lift=Mathf.Sin(q*Mathf.PI)*.4f;if(!hit&&ColonAttackWall.Clear(At,Player.transform.position)&&SnailBossProjectile.SegmentDistance(Player.transform.position,before,At)<1.2f){hit=TryHit(Hit);}});yield break;
            }
            if(index==1){yield return FanStrike(At,-Aim,4.3f,220,1.05f,5,Hit);yield break;}
            if(index==2)
            {Vector2 p=At+Aim*2.0f;yield return Impact(p,2.6f,1.1f,3,Hit*1.1f);Visual.Pose=4;yield return Tween(.45f,q=>Visual.Squash=Mathf.Sin(q*Mathf.PI)*.10f);yield break;}
            Vector2 originalPoint=At;SetHidden(true);var cleanup=ColonPipeSequenceCleanup.Create(this);
            int[] order={1,0,2};
            for(int i=0;i<3;i++)
            {
                int slot=order[i];cleanup.AtDoor(slot);Vector2 p=Habitat.DoorExit(slot);body.position=p;transform.position=p;
                Habitat.Door(slot,0);yield return Wait(.10f);yield return Habitat.SlideDoor(this,slot,1,.20f);
                PeekFromDoor(slot,true);Visual.Opacity=1;SetHidden(false);Visual.Pose=6;
                yield return Tween(.3f,q=>{Visual.Squash=(1-q)*.05f;Visual.Lift=(1-q)*-.28f;});
                // Head remains hittable during each peek; only underground traversal is invulnerable.
                if(index==3)
                {
                    ShootFan(HitCenter,Aim,3,18,3.1f,Hit*.65f,"pear seed",.18f);yield return Wait(.75f);
                    yield return RetractIntoPipe();yield return Habitat.SlideDoor(this,slot,0,.22f);
                }
                else
                {
                    var warn=ColonBossEffects.Warning(this,ColonBossEffects.Circle(p,1.8f),p);yield return Wait(.36f);
                    yield return RetractIntoPipe();yield return Habitat.SlideDoor(this,slot,0,.22f);
                    // Keep the warning until the door actually meets its frame. Retraction
                    // itself is harmless; this single beat owns the existing slam damage.
                    ColonBossEffects.Fade(warn,.10f);Sound("Land",.4f);ColonBossEffects.Burst(this,p,1.8f);
                    if(ColonAttackWall.Clear(p,Player.transform.position)&&Vector2.Distance(Player.transform.position,p)<1.8f)TryHit(Hit*.8f);
                }
                SetHidden(true);yield return Wait(.25f);
            }
            cleanup.Complete();RestoreBodyHitbox();body.position=Habitat.ClampTerritory(originalPoint);transform.position=body.position;SetHidden(false);Visual.ResetPose();yield return Wait(.45f);
        }
        IEnumerator RetractIntoPipe()
        {
            Vector2 from=At,normal=Habitat.WallNormal.sqrMagnitude>.1f?Habitat.WallNormal.normalized:Vector2.down;
            yield return Tween(.22f,q=>{PlaceAt(from-normal*(.55f*Ease(q)));Visual.Lift=-.10f*Ease(q);Visual.Opacity=1-.18f*q;});
            SetHidden(true);Visual.Lift=0;Visual.Opacity=1;
        }
    }
}
