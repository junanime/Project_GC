using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    public sealed partial class FoodBossRuntime
    {
        IEnumerator Dash()
        {
            if(Kind==FoodBossKind.Maribi||Kind==FoodBossKind.Hurumi){yield return Jump();yield break;}
            if(Kind==FoodBossKind.Rolpangi){yield return RollDash();yield break;}
            int count=Kind==FoodBossKind.Tangdengi&&PhaseTwo?2:1;
            for(int n=0;n<count;n++)
            {
                Vector2 from=transform.position,dir=Aim,to=FiniteStomachMap.ClampDash(from,from+dir*8,1.3f),side=Rotate(dir,90);
                var warning=FoodBossEffects.Line(Effects,new[]{from,to},1.25f,new Color(Warning.r,Warning.g,Warning.b,.5f));
                Visual.Face(dir);
                if(Kind==FoodBossKind.Denikani){Visual.LeftClaw=Visual.RestClaw(0);Visual.RightClaw=Visual.RestClaw(1);Visual.ClawsActive=true;}
                yield return Wait(n==0?1.1f:.8f);Destroy(warning.gameObject);
                // The selected short flutter belongs to the launch, not the warning.
                Audio.Play(FoodBossSound.Dash);
                Visual.Charge=true;
                Vector2 restLeft=Visual.LeftClaw,restRight=Visual.RightClaw;
                for(float t=0;t<.85f;t+=Time.deltaTime){float u=t/.85f;MoveToward(Vector2.Lerp(from,to,u));
                    float stride=t*24;
                    Vector2 l=Vector2.up*.7f+dir*(1.65f+Mathf.Cos(stride)*.25f)+side*.55f+Vector2.up*Mathf.Max(0,Mathf.Sin(stride))*.28f;
                    Vector2 r=Vector2.up*.7f+dir*(1.55f-Mathf.Cos(stride)*.25f)-side*.55f+Vector2.up*Mathf.Max(0,-Mathf.Sin(stride))*.28f;
                    Visual.LeftClaw=Vector2.Lerp(restLeft,l,Mathf.Clamp01(t/.15f));Visual.RightClaw=Vector2.Lerp(restRight,r,Mathf.Clamp01(t/.15f));yield return null;}
                MoveToward(to);
                if(Kind==FoodBossKind.Tangdengi)Audio.Stop(FoodBossSound.Dash,.14f);
                if(Kind==FoodBossKind.Tangdengi&&PhaseTwo&&n==0)
                {
                    Audio.Play(FoodBossSound.Skid);var start=(Vector2)transform.position;var end=FiniteStomachMap.ClampDash(start,start+dir*1.4f,1.3f);
                    FoodBossEffects.Path(this,new[]{start,end},.7f,1.5f);float nextDust=0;
                    for(float t=0;t<.55f;t+=Time.deltaTime){MoveToward(Vector2.Lerp(start,end,1-Mathf.Pow(1-t/.55f,3)));Visual.Recoil=-Mathf.Sin(t/.55f*Mathf.PI)*.15f;
                        if(t>=nextDust){FoodBossDust.Emit(this,(Vector2)transform.position-dir*.45f,-dir);nextDust+=.055f;}yield return null;}
                    MoveToward(end);Visual.Recoil=0;
                }
                Visual.Charge=false;
            }
            if(Kind==FoodBossKind.Denikani)
            {
                if(PhaseTwo){yield return PoseClaws(Visual.RestClaw(0),Visual.RestClaw(1),.18f);Vector2 dir=Aim;
                    Visual.ClawsActive=false;yield return Wait(.18f);Visual.Face(dir);Visual.LeftClaw=Visual.RestClaw(0);Visual.RightClaw=Visual.RestClaw(1);Visual.ClawsActive=true;
                    var w=FoodBossEffects.Line(Effects,FoodBossEffects.FanOutline(transform.position,dir,3.75f,150),.10f,Warning);
                    yield return PoseClaws(Rotate(dir,-75)*3.0f,Visual.RestClaw(1),.65f);Destroy(w.gameObject);yield return ClawSweep(dir,150,.55f);}
                yield return PoseClaws(Visual.RestClaw(0),Visual.RestClaw(1),.35f);
            }
        }
        IEnumerator RollDash()
        {
            Vector2 start=transform.position,dir=Aim;var points=new List<Vector2>{start};
            if(PhaseTwo)
            {
                // Lock a slider trajectory through the player's position before the telegraph starts.
                Vector2 target=Player.transform.position,side=Rotate(dir,90);
                float distanceToTarget=Mathf.Max(2,Vector2.Distance(start,target));
                Vector2 control=start+dir*(distanceToTarget*.38f)+side*Mathf.Min(3.2f,distanceToTarget*.65f);
                for(int i=1;i<=40;i++){float u=i/40f;points.Add((1-u)*(1-u)*start+2*(1-u)*u*control+u*u*target);}
                points.Add(target+(target-control).normalized*2.2f);
            }
            else points.Add(start+dir*8);
            for(int i=0;i<points.Count;i++)points[i]=FiniteStomachMap.ClampDash(start,points[i],1.3f);
            var w=FoodBossEffects.Line(Effects,points.ToArray(),1.3f,new Color(Warning.r,Warning.g,Warning.b,.38f));yield return Wait(1.15f);Destroy(w.gameObject);
            // Roll only the cake shell; toppings float separately, then settle back.
            Audio.Play(FoodBossSound.Dash);Visual.Body.enabled=false;var shell=new GameObject("Rolling cake").AddComponent<SpriteRenderer>();shell.transform.SetParent(Effects,false);shell.sprite=FoodBossArt.Get("RollShell"+(PhaseTwo?2:1));shell.sortingOrder=511;
            var toppings=new GameObject("Floating toppings").AddComponent<SpriteRenderer>();toppings.transform.SetParent(Effects,false);toppings.sprite=FoodBossArt.Get("RollToppings"+(PhaseTwo?2:1));toppings.sortingOrder=512;
            if(shell.sprite!=null)shell.transform.localScale=Vector3.one*(3.4f/shell.sprite.bounds.size.x);if(toppings.sprite!=null)toppings.transform.localScale=Vector3.one*(2.3f/toppings.sprite.bounds.size.x);
            shell.transform.position=transform.position+Vector3.up*1.5f;
            for(float t=0;t<.18f;t+=Time.deltaTime){toppings.transform.position=transform.position+Vector3.up*Mathf.Lerp(2.5f,3.2f,Mathf.SmoothStep(0,1,t/.18f));yield return null;}
            float distance=0;for(int i=1;i<points.Count;i++)distance+=Vector2.Distance(points[i-1],points[i]);float progress=0;int segment=1;float segmentProgress=0;
            while(progress<distance&&segment<points.Count)
            {
                float step=8*Time.deltaTime;progress+=step;segmentProgress+=step;float len=Vector2.Distance(points[segment-1],points[segment]);
                while(segmentProgress>len&&segment<points.Count-1){segmentProgress-=len;segment++;len=Vector2.Distance(points[segment-1],points[segment]);}
                MoveToward(Vector2.Lerp(points[segment-1],points[segment],len<.001f?1:segmentProgress/len));shell.transform.position=transform.position+Vector3.up*1.5f;shell.transform.Rotate(0,0,-Time.deltaTime*800);toppings.transform.position=transform.position+Vector3.up*(3.2f+Mathf.Sin(progress*3)*.12f);yield return null;
            }
            Audio.Stop(FoodBossSound.Dash,.1f);Audio.Play(FoodBossSound.Land,.45f);
            Quaternion rolled=shell.transform.rotation;
            for(float t=0;t<.35f;t+=Time.deltaTime){float u=Mathf.SmoothStep(0,1,t/.35f);toppings.transform.position=transform.position+Vector3.up*Mathf.Lerp(3.2f,2.5f,u);shell.transform.rotation=Quaternion.Slerp(rolled,Quaternion.identity,u);yield return null;}
            Destroy(shell.gameObject);Destroy(toppings.gameObject);Visual.Body.enabled=true;
        }
        IEnumerator Jump()
        {
            int count=Kind==FoodBossKind.Hurumi&&PhaseTwo?2:1;
            for(int n=0;n<count;n++)
            {
                Vector2 target=FiniteStomachMap.SafeSpawn(Player.transform.position,2.2f);float radius=n==0&&count==2?1.7f:2.2f;
                var w=FoodBossEffects.Ring(this,target,radius);var shadow=FoodBossEffects.Line(Effects,FoodBossEffects.Arc(target,.7f),.7f,new Color(0,0,0,.24f),true,478);
                var splashWarnings=new List<LineRenderer>();
                if(Kind==FoodBossKind.Maribi&&PhaseTwo)for(int i=0;i<4;i++)splashWarnings.Add(FoodBossEffects.Line(Effects,FoodBossEffects.FanOutline(target,Dir(45+i*90),3.8f,45),.09f,Warning));
                yield return Wait(.4f);Audio.Play(FoodBossSound.Dash);Visual.Airborne=true;hitbox.enabled=false;Vector2 start=transform.position;
                float height=Camera.main!=null?Camera.main.orthographicSize*2+8:20;
                for(float t=0;t<.4f;t+=Time.deltaTime){Visual.Lift=height*Mathf.Sin(t/.4f*Mathf.PI*.5f);yield return null;}
                Visual.Body.enabled=false;body.position=target;transform.position=target;
                yield return Wait(.55f);
                Visual.Body.enabled=true;
                for(float t=0;t<.28f;t+=Time.deltaTime){Visual.Lift=height*(1-t/.28f)*(1-t/.28f);yield return null;}
                Visual.Lift=0;Visual.Airborne=false;hitbox.enabled=true;Destroy(w.gameObject);Destroy(shadow.gameObject);
                foreach(var splashWarning in splashWarnings)Destroy(splashWarning.gameObject);
                Audio.Play(FoodBossSound.Land,n==0&&count==2?.6f:1);HitCircle(target,radius,n==0&&count==2?10:18);FoodBossEffects.Burst(this,target,radius);ToadCameraShake.Play(.18f,.10f);
                if(Kind==FoodBossKind.Maribi&&PhaseTwo){Audio.Play(FoodBossSound.Skill2,.7f);for(int i=0;i<4;i++){var direction=Dir(45+i*90);FoodBossEffects.Fan(this,target,direction,3.8f,45,1.1f,10);}yield return Wait(1.1f);}
                yield return Wait(n+1<count?.35f:.5f);
            }
        }
        void HitCircle(Vector2 at,float radius,float damage){if(Player!=null&&Vector2.Distance(Player.transform.position,at)<=radius+.18f)Player.TakeDamage(damage);}
        IEnumerator UniqueOne()
        {
            if(Kind==FoodBossKind.Maribi||Kind==FoodBossKind.Tangdengi)Audio.Play(FoodBossSound.Skill1);
            switch(Kind){case FoodBossKind.Rolpangi:yield return CreamSpiral();break;case FoodBossKind.Denikani:yield return Grab();break;case FoodBossKind.Maribi:yield return Seeds();break;case FoodBossKind.Tangdengi:yield return GrapeBeam();break;case FoodBossKind.Hurumi:yield return Curtain();break;}
        }
        IEnumerator UniqueTwo()
        {
            if(Kind!=FoodBossKind.Maribi&&Kind!=FoodBossKind.Hurumi)Audio.Play(FoodBossSound.Skill2);
            switch(Kind){case FoodBossKind.Rolpangi:yield return Toppings();break;case FoodBossKind.Denikani:yield return ClawWaves();break;case FoodBossKind.Maribi:yield return Juice();break;case FoodBossKind.Tangdengi:yield return StrawberrySeeds();break;case FoodBossKind.Hurumi:yield return Whips();break;}
        }
        IEnumerator CreamSpiral()
        {
            Visual.Face(Aim);yield return null;
            Vector2 origin=transform.position,mouth=Visual.SnailMouth;
            Vector2 firstCurl=mouth+new Vector2(Visual.Facing*1.45f,-.22f);
            float mouthAngle=Mathf.Atan2(firstCurl.y-origin.y,firstCurl.x-origin.x)*Mathf.Rad2Deg;
            float innerRadius=Vector2.Distance(origin,firstCurl);int arms=PhaseTwo?2:1;
            for(int arm=0;arm<arms;arm++)
            {
                var points=new List<Vector2>{mouth};var gaps=new List<bool>{false};
                for(int i=1;i<=16;i++){float u=i/16f;points.Add(Vector2.Lerp(mouth,firstCurl,u)+Vector2.down*(.10f*Mathf.Sin(u*Mathf.PI)));gaps.Add(false);}
                // The second branch also comes from the mouth: it first curls around the inner shell.
                if(arm==1)for(int i=1;i<=32;i++){points.Add(origin+Dir(mouthAngle+180*i/32f)*innerRadius);gaps.Add(false);}
                for(int i=1;i<=240;i++)
                {
                    float u=i/240f;points.Add(origin+Dir(mouthAngle+arm*180+u*600)*Mathf.Lerp(innerRadius,6.6f,u));
                    gaps.Add(PhaseTwo&&(Mathf.FloorToInt(u*150)%38>=28));
                }
                var warnings=new List<LineRenderer>();var segment=new List<Vector2>();
                for(int i=0;i<points.Count;i++)
                {
                    if(gaps[i]){if(segment.Count>1)warnings.Add(FoodBossEffects.Line(Effects,segment.ToArray(),.65f,new Color(Warning.r,Warning.g,Warning.b,.35f)));segment.Clear();}
                    else segment.Add(points[i]);
                }
                if(segment.Count>1)warnings.Add(FoodBossEffects.Line(Effects,segment.ToArray(),.65f,new Color(Warning.r,Warning.g,Warning.b,.35f)));
                yield return Wait(.9f);foreach(var w in warnings)Destroy(w.gameObject);Audio.Play(FoodBossSound.Skill1);
                FoodBossCreamTrail.Create(this,points.ToArray(),gaps.ToArray());
                for(float t=0;t<FoodBossCreamTrail.ExtrusionSeconds;t+=Time.deltaTime){Visual.CreamPulse=Mathf.Sin(t*Mathf.PI/FoodBossCreamTrail.ExtrusionSeconds);yield return null;}
                Visual.CreamPulse=0;yield return Wait(.12f);
            }
        }
        IEnumerator Toppings()
        {
            var target=(Vector2)Player.transform.position;bool strawberry=PhaseTwo||Random.value<.5f;bool berries=PhaseTwo||!strawberry;
            if(strawberry)StartCoroutine(Bomb(target,1.4f,"Strawberry",18));
            if(berries)for(int i=0;i<5;i++)StartCoroutine(Bomb(FiniteStomachMap.SafeSpawn(target+Dir(i*72)*2.8f,.5f),.65f,"Blueberry",9));
            yield return Wait(1.65f);
        }
        IEnumerator Bomb(Vector2 target,float radius,string art,float damage)
        {
            var warning=FoodBossEffects.Ring(this,target,radius);var r=new GameObject("Arcing "+art).AddComponent<SpriteRenderer>();r.transform.SetParent(Effects,false);r.sprite=FoodBossArt.Get(art);r.sortingOrder=530;if(r.sprite!=null)r.transform.localScale=Vector3.one*((radius>.8f?.8f:.38f)/r.sprite.bounds.size.x);
            Vector2 from=Mouth;for(float t=0;t<1.35f;t+=Time.deltaTime){if(Dead||Transitioning||r==null)yield break;float u=t/1.35f;r.transform.position=Vector2.Lerp(from,target,u)+Vector2.up*(Mathf.Sin(u*Mathf.PI)*3.5f);yield return null;}
            if(r==null||warning==null)yield break;Audio.Play(FoodBossSound.Land,.35f);Destroy(r.gameObject);Destroy(warning.gameObject);FoodBossEffects.Burst(this,target,radius);HitCircle(target,radius,damage);
        }
        IEnumerator PoseClaws(Vector2 left,Vector2 right,float seconds)
        {
            Vector2 fromLeft=Visual.LeftClaw,fromRight=Visual.RightClaw;
            for(float t=0;t<seconds;t+=Time.deltaTime){float u=Mathf.SmoothStep(0,1,t/seconds);Visual.LeftClaw=Vector2.Lerp(fromLeft,left,u);Visual.RightClaw=Vector2.Lerp(fromRight,right,u);yield return null;}
            Visual.LeftClaw=left;Visual.RightClaw=right;
        }
        IEnumerator Grab()
        {
            Vector2 origin=transform.position,dir=Aim,side=Rotate(dir,90);Visual.Face(dir);Visual.ClawsActive=true;
            Visual.LeftClaw=Visual.RestClaw(0);Visual.RightClaw=Visual.RestClaw(1);
            Vector2 ready=Vector2.up*.65f+dir*.85f,leftEnd=dir*4+side*(PhaseTwo?1.8f:.40f),rightEnd=dir*4-side*(PhaseTwo?1.8f:.40f);
            var a=FoodBossEffects.Line(Effects,new[]{origin+Visual.LeftClaw,origin+leftEnd},1.1f,new Color(Warning.r,Warning.g,Warning.b,.5f));
            var b=FoodBossEffects.Line(Effects,new[]{origin+Visual.RightClaw,origin+rightEnd},1.0f,new Color(Warning.r,Warning.g,Warning.b,.5f));
            LineRenderer converge=null;
            if(PhaseTwo)converge=FoodBossEffects.Line(Effects,new[]{origin+leftEnd,origin+rightEnd},1.1f,new Color(Warning.r,Warning.g,Warning.b,.45f));
            yield return PoseClaws(ready+side*.65f,ready-side*.65f,.45f);yield return Wait(1.55f);
            Destroy(a.gameObject);Destroy(b.gameObject);if(converge!=null)Destroy(converge.gameObject);
            Audio.Play(FoodBossSound.Skill1);
            bool caught=false;Vector2 beginL=Visual.LeftClaw,beginR=Visual.RightClaw,prevL=beginL,prevR=beginR;
            for(float t=0;t<.85f;t+=Time.deltaTime)
            {
                float u=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.30f)),gather=PhaseTwo?Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.36f)/.49f)):0;
                Visual.LeftClaw=Vector2.Lerp(beginL,Vector2.Lerp(leftEnd,dir*4+side*.18f,gather),u);
                Visual.RightClaw=Vector2.Lerp(beginR,Vector2.Lerp(rightEnd,dir*4-side*.18f,gather),u);
                if(Player!=null&&!Player.IsDashing&&!caught){Vector2 at=(Vector2)Player.transform.position-origin;
                    if(SnailBossProjectile.SegmentDistance(at,prevL,Visual.LeftClaw)<.60f||SnailBossProjectile.SegmentDistance(at,prevR,Visual.RightClaw)<.55f)caught=true;}
                prevL=Visual.LeftClaw;prevR=Visual.RightClaw;yield return null;
            }
            if(caught&&Player!=null&&Player.IsAlive)
            {
                Visual.Charge=true;
                var slow=Player.GetComponent<SnailSlowStatus>()??Player.gameObject.AddComponent<SnailSlowStatus>();slow.Enter(GetInstanceID(),.95f);
                var rb=Player.GetComponent<Rigidbody2D>();Vector2 held=Player.transform.position,hold=origin+dir*2.2f;float nextTick=0;
                Vector2 l=Visual.LeftClaw,r=Visual.RightClaw;
                for(float t=0;t<1.05f;t+=Time.deltaTime)
                {
                    if(!Player.IsAlive||Player.IsDashing)break;
                    float u=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.35f));Vector2 at=Vector2.Lerp(held,hold,u);
                    Visual.LeftClaw=Vector2.Lerp(l,at-origin,u);Visual.RightClaw=Vector2.Lerp(r,at-origin+side*.35f,u);
                    if(rb!=null){rb.position=at;rb.velocity=Vector2.zero;}Player.transform.position=at;
                    if(t>=nextTick){Player.TakeDamage(4);nextTick+=.35f;}yield return null;
                }
                slow.Exit(GetInstanceID(),0);
                if(Player.IsAlive&&!Player.IsDashing){Vector2 from=Player.transform.position,landing=FiniteStomachMap.ClampDash(from,from+dir*1.8f,.25f);
                    for(float t=0;t<.25f;t+=Time.deltaTime){float u=t/.25f;Vector2 at=Vector2.Lerp(from,landing,u)+Vector2.up*Mathf.Sin(u*Mathf.PI)*.4f;if(rb!=null)rb.position=at;Player.transform.position=at;yield return null;}
                    if(rb!=null)rb.position=landing;Player.transform.position=landing;Audio.Play(FoodBossSound.Land,.45f);FoodBossEffects.Burst(this,landing,.6f);}
                Visual.Charge=false;
            }
            yield return PoseClaws(Visual.RestClaw(0),Visual.RestClaw(1),.45f);
        }
        IEnumerator ClawSweep(Vector2 dir,float sweep,float duration)
        {
            Audio.Play(FoodBossSound.Skid,.8f);
            bool hit=false;float a=Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg;Vector2 previous=Visual.LeftClaw;
            for(float t=0;t<duration;t+=Time.deltaTime){float u=Mathf.SmoothStep(0,1,t/duration);var p=Dir(a-sweep*.5f+sweep*u)*3.0f;Visual.LeftClaw=p;
                if(!hit&&SnailBossProjectile.SegmentDistance((Vector2)Player.transform.position-(Vector2)transform.position,previous,p)<.72f){Player.TakeDamage(14);hit=true;}previous=p;yield return null;}
        }
        IEnumerator ClawWaves()
        {
            Visual.ClawsActive=true;Visual.LeftClaw=Visual.RestClaw(0);Visual.RightClaw=Visual.RestClaw(1);
            int count=PhaseTwo?2:1;
            for(int n=0;n<count;n++)
            {
                Vector2 origin=transform.position;float start=PhaseTwo?(n==0?90:-90):0,sweep=PhaseTwo?180:360;
                var w=FoodBossEffects.Ring(this,origin,4.2f,start,sweep);
                Vector2 left=PhaseTwo&&n==1?Visual.RestClaw(0):new Vector2(-1.1f,2.5f),right=PhaseTwo&&n==0?Visual.RestClaw(1):new Vector2(1.1f,2.5f);
                yield return PoseClaws(left,right,.45f);yield return Wait(n==0?1.37f:.27f);
                yield return PoseClaws(new Vector2(left.x,.3f),new Vector2(right.x,.3f),.18f);Destroy(w.gameObject);
                FoodBossImpactWave.Create(this,origin,4.2f,start,sweep);
                Vector2 delta=(Vector2)Player.transform.position-origin;if(delta.magnitude<=4.2f&&(sweep==360||n==0&&delta.x<=0||n==1&&delta.x>=0))Player.TakeDamage(14);
                Audio.Play(FoodBossSound.Land,.7f);ToadCameraShake.Play(.12f,.06f);yield return Wait(.42f);
            }
            yield return PoseClaws(Visual.RestClaw(0),Visual.RestClaw(1),.35f);
        }
        IEnumerator Seeds()
        {
            Visual.Face(Aim);
            Vector2 origin=Mouth,dir=FireAim;float angle=PhaseTwo?85:0;
            var w=FoodBossEffects.Line(Effects,FoodBossEffects.FanOutline(origin,dir,8,PhaseTwo?100:22),.12f,Warning);yield return Wait(1);Destroy(w.gameObject);
            int count=PhaseTwo?14:10;for(int i=0;i<count;i++){Vector2 d=Rotate(dir,PhaseTwo?-angle*.5f+angle*i/(count-1):0);Vector2 from=origin+Rotate(d,90)*Random.Range(-.7f,.7f);Audio.Play(FoodBossSound.Shot,.6f);FoodBossBullet.Fire(this,from,d,2.5f,6,false,FoodBossArt.Get("LemonSeed"));Visual.Recoil=-.08f;yield return Wait(.15f);Visual.Recoil=0;}
        }
        IEnumerator Juice()
        {
            int count=PhaseTwo?2:1;for(int i=0;i<count;i++){Visual.Face(Aim);Vector2 origin=Mouth,dir=FireAim;float reach=PhaseTwo?(i==0?7:3.7f):4.7f,angle=PhaseTwo?(i==0?28:120):70;
                var w=FoodBossEffects.Line(Effects,FoodBossEffects.FanOutline(origin,dir,reach,angle),.12f,Warning);yield return Wait(.9f);Destroy(w.gameObject);Audio.Play(FoodBossSound.Skill2);FoodBossEffects.Fan(this,origin,dir,reach,angle,1.15f,12);yield return Wait(1.35f);}
        }
        IEnumerator GrapeBeam()
        {
            Visual.Face(Aim);yield return null;
            Vector2 origin=Visual.HornTip,dir=((Vector2)Player.transform.position-origin).normalized;int count=PhaseTwo?3:1;var warnings=new List<LineRenderer>();
            for(int i=0;i<count;i++){Vector2 d=Rotate(dir,(i-(count-1)*.5f)*32);warnings.Add(FoodBossEffects.Line(Effects,new[]{origin,origin+d*9},1.0f,new Color(Warning.r,Warning.g,Warning.b,.5f)));}
            var orb=FoodBossArt.Make(Effects,"Grape",.65f,522);orb.transform.position=origin;Vector3 size=orb.transform.localScale;
            var charge=FoodBossEffects.Line(Effects,FoodBossEffects.Arc(origin,.6f),.055f,new Color(.85f,1,.42f),false,523);
            float chargeDuration=Mathf.Max(1.3f,Audio.Duration(FoodBossSound.Skill1)+.065f);
            for(float t=0;t<chargeDuration;t+=Time.deltaTime){orb.transform.localScale=size*(.30f+.70f*t/chargeDuration)*(1+.06f*Mathf.Sin(t*24));var arc=FoodBossEffects.Arc(origin,Mathf.Lerp(1,.30f,t/chargeDuration));for(int j=0;j<arc.Length;j++)charge.SetPosition(j,arc[j]);yield return null;}
            Destroy(orb.gameObject);Destroy(charge.gameObject);foreach(var w in warnings)Destroy(w.gameObject);
            Audio.Stop(FoodBossSound.Skill1,.035f);Audio.Play(FoodBossSound.Beam,.8f);
            for(int i=0;i<count;i++)FoodBossEnergyBeam.Create(this,origin,Rotate(dir,(i-(count-1)*.5f)*32));
            yield return Wait(1.25f);
        }
        IEnumerator StrawberrySeeds()
        {
            Visual.Charge=true;var w=FoodBossEffects.Ring(this,transform.position,2);yield return Wait(1.15f);Destroy(w.gameObject);Visual.Charge=false;
            Audio.Play(FoodBossSound.Radial);for(int i=0;i<14;i++){var d=Dir(i*360f/14);FoodBossBullet.Fire(this,(Vector2)transform.position+d*1.3f,d,1.65f,7,PhaseTwo,PhaseTwo?null:FoodBossArt.Get("StrawberrySeed"));}yield return Wait(.8f);
        }
        IEnumerator Curtain() { yield return ConnectedCurtain(); }
        IEnumerator Whips()
        {
            int count=PhaseTwo?2:1;Visual.Face(Aim);Visual.Whipping=true;float facing=Aim.x>=0?1:-1;yield return null;
            for(int n=0;n<count;n++)
            {
                Vector2 root=Visual.NoodleTip,dir=((Vector2)Player.transform.position-root).normalized;float reach=n==0?4.2f:5.8f,sweep=n==0?90:150;
                var warning=FoodBossEffects.Line(Effects,FoodBossEffects.FanOutline(root,dir,reach+.25f,sweep+8),.10f,Warning);
                var noodle=FoodBossNoodle.Create(Effects,.66f,514);var rest=RestNoodle(root,facing);float baseAngle=Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg,sign=n==0?-1:1;
                // A short curl grows from the tip of the existing raised noodle, winds up, lashes, then retracts.
                float wind=n==0?.85f:1.2f;
                for(float t=0;t<wind;t+=Time.deltaTime){float u=Mathf.SmoothStep(0,1,t/wind);Vector2 tip=root+Dir(baseAngle-sign*sweep*.5f)*(reach*u);var curve=WhipCurve(root,tip,Vector2.up*.6f,u);for(int j=0;j<curve.Length;j++)curve[j]=Vector2.Lerp(rest[j],curve[j],u);noodle.Set(curve);yield return null;}
                Destroy(warning.gameObject);Audio.Play(FoodBossSound.Skill2);bool hit=false;Vector2[] previous=null;
                for(float t=0;t<.60f;t+=Time.deltaTime){float u=Mathf.SmoothStep(0,1,t/.60f),angle=baseAngle+sign*(-sweep*.5f+sweep*u);Vector2 tip=root+Dir(angle)*reach;
                    var points=WhipCurve(root,tip,Rotate(dir,90)*(-sign*.48f*Mathf.Sin(u*Mathf.PI)),1);noodle.Set(points);
                    Vector2 playerDelta=Player!=null?(Vector2)Player.transform.position-root:Vector2.zero;
                    bool insideWarning=playerDelta.magnitude<=reach+.25f&&Vector2.Angle(dir,playerDelta)<=sweep*.5f+4;
                    if(!hit&&Player!=null&&insideWarning)for(int j=1;j<points.Length;j++){
                        bool contact=SnailBossProjectile.SegmentDistance(Player.transform.position,points[j-1],points[j])<.40f;
                        if(previous!=null)contact|=SnailBossProjectile.SegmentDistance(Player.transform.position,previous[j],points[j])<.40f;
                        if(contact){Player.TakeDamage(14);hit=true;break;}}
                    previous=points;yield return null;}
                Vector2 end=root+Dir(baseAngle+sign*sweep*.5f)*reach;
                for(float t=0;t<.38f;t+=Time.deltaTime){float u=1-Mathf.SmoothStep(0,1,t/.38f);var curve=WhipCurve(root,end,Vector2.up*.5f,1);for(int j=0;j<curve.Length;j++)curve[j]=Vector2.Lerp(rest[j],curve[j],u);noodle.Set(curve);yield return null;}
                Visual.Whipping=false;Destroy(noodle.gameObject);yield return Wait(.20f);if(n+1<count)Visual.Whipping=true;
            }
        }
        static Vector2[] RestNoodle(Vector2 root,float facing)
        {
            var p=new Vector2[40];Vector2 end=root+new Vector2(facing*.45f,.85f),c1=root+new Vector2(-facing*.35f,.65f),c2=root+new Vector2(facing*.35f,1.25f);
            for(int j=0;j<p.Length;j++){float u=j/(float)(p.Length-1),v=1-u;p[j]=v*v*v*root+3*v*v*u*c1+3*v*u*u*c2+u*u*u*end;}return p;
        }
        static Vector2[] WhipCurve(Vector2 root,Vector2 end,Vector2 bend,float extension)
        {
            var p=new Vector2[40];Vector2 c1=root+Vector2.up*1.1f,c2=Vector2.Lerp(root,end,.55f)+Vector2.up*1.3f+bend;
            for(int j=0;j<p.Length;j++){float u=j/(float)(p.Length-1),v=1-u;p[j]=v*v*v*root+3*v*v*u*c1+3*v*u*u*c2+u*u*u*end;}return p;
        }
    }
}
