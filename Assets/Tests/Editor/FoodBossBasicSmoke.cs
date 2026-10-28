using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static partial class FoodBossSmoke
    {
        sealed class BasicShotProof
        {
            public FoodBossBullet Bullet;
            public Vector2 Origin,Direction;
            public float At;
        }

        static IEnumerator BasicReview(Character player,Vector2 origin)
        {
            Time.captureDeltaTime=1f/60;
            int[] signatureCounts={8,6,6,6,8};
            foreach(FoodBossKind kind in Enum.GetValues(typeof(FoodBossKind)))
            {
                Position(player,origin+new Vector2(5,.8f));
                var boss=FoodBossRuntime.Spawn(kind,player,origin);boss.AutoPatterns=false;
                // Leave Update enabled to exercise normal cleanup, idle movement and phase lifecycle.
                Check(boss.Audio.HasAllClips,"basic attack existing audio available "+kind);
                for(int phase=0;phase<2;phase++)
                {
                    if(phase==1)
                    {
                        // Interrupt a signature volley, verify cancellation and phase-two reset.
                        Check(boss.Play(FoodBossAttack.Basic),"pre-transition signature starts "+kind);
                        yield return new WaitForSeconds(1.1f);
                        Check(boss.Effects.GetComponentsInChildren<FoodBossBullet>().Length>0,"signature has live projectiles before transition "+kind);
                        boss.SetPhaseTwoForTest();yield return null;
                        Check(boss.Effects.GetComponentsInChildren<FoodBossBullet>().Length==0,"transition clears signature bullets "+kind);
                        while(boss.Busy)yield return null;
                        Check(boss.PhaseTwo&&!boss.Transitioning,"phase two ready "+kind);
                    }
                    for(int turn=0;turn<3;turn++)
                    {
                        bool signature=turn==1;
                        Position(player,origin+new Vector2(5,.8f));
                        Check(boss.Play(FoodBossAttack.Basic),"start alternating basic "+kind+" P"+(phase+1)+" turn "+turn);
                        float begin=Time.time;bool paused=false,moved=false,captured=false;
                        var shots=new List<BasicShotProof>();var seen=new HashSet<int>();
                        bool straight=true,spriteMatches=true,clawSocket=true;
                        while(boss.Busy&&Time.time-begin<12)
                        {
                            foreach(var bullet in boss.Effects.GetComponentsInChildren<FoodBossBullet>())
                            {
                                if(seen.Add(bullet.GetInstanceID()))
                                {
                                    shots.Add(new BasicShotProof{Bullet=bullet,Origin=bullet.transform.position,Direction=bullet.Velocity.normalized,At=Time.time});
                                    var r=bullet.GetComponent<SpriteRenderer>();
                                    spriteMatches&=r.sprite==FoodBossArt.Projectile(kind)&&Mathf.Abs(r.sprite.bounds.size.x*bullet.transform.localScale.x-.28f)<.001f;
                                    if(signature&&kind==FoodBossKind.Denikani)
                                        clawSocket&=Mathf.Min(Vector2.Distance(bullet.transform.position,boss.Visual.ClawShotOrigin(0)),Vector2.Distance(bullet.transform.position,boss.Visual.ClawShotOrigin(1)))<.12f;
                                }
                            }
                            foreach(var shot in shots)if(shot.Bullet!=null&&!(signature&&kind==FoodBossKind.Hurumi))
                                straight&=Vector2.Angle(shot.Direction,shot.Bullet.Velocity)<.05f;
                            if(signature&&!paused&&shots.Count>=2)
                            {
                                paused=true;Time.timeScale=0;
                                var bullets=boss.Effects.GetComponentsInChildren<FoodBossBullet>();var at=bullets.Select(b=>(Vector2)b.transform.position).ToArray();
                                yield return new WaitForSecondsRealtime(.12f);
                                Check(bullets.Length==boss.Effects.GetComponentsInChildren<FoodBossBullet>().Length&&bullets.Select((b,i)=>b!=null&&Vector2.Distance(b.transform.position,at[i])<.00001f).All(v=>v),"pause freezes emission and motion "+kind+" P"+(phase+1));
                                Time.timeScale=1;
                            }
                            if(signature&&kind==FoodBossKind.Maribi&&!moved&&shots.Count==1)
                            {moved=true;Position(player,origin+new Vector2(5,3.5f));}
                            if(signature&&!captured&&Time.time-begin>1.8f)
                            {captured=true;Capture("basic-"+(int)kind+"-p"+(phase+1));}
                            yield return null;
                        }
                        Check(!boss.Busy,"basic finishes without stuck pose "+kind+" P"+(phase+1)+" turn "+turn);
                        int expected=signature?signatureCounts[(int)kind]*(phase==0?1:kind==FoodBossKind.Maribi||kind==FoodBossKind.Tangdengi?1:2):(phase==0?3:5);
                        if(signature&&phase==1&&(kind==FoodBossKind.Maribi||kind==FoodBossKind.Tangdengi))expected=9;
                        Check(shots.Count==expected,"alternation projectile count "+kind+" P"+(phase+1)+" turn "+turn+" = "+shots.Count);
                        Check(spriteMatches&&straight,"existing sprite size and straight headings preserved "+kind+" P"+(phase+1)+" turn "+turn);
                        Check(Mathf.Abs(boss.Visual.Recoil)<.001f&&!boss.Visual.ClawsActive,"basic releases recoil and claw pose "+kind);
                        Check(shots.All(s=>s.Bullet==null||Mathf.Approximately((float)Field(s.Bullet,"damage").GetValue(s.Bullet),signature?6:8)),"classic damage 8 signature damage 6 "+kind);
                        if(signature)
                        {
                            if(kind==FoodBossKind.Rolpangi)
                            {
                                Check(Vector2.SignedAngle(shots[0].Direction,shots[7].Direction)<-139,"clockwise partial spiral "+phase);
                                if(phase==1)Check(Vector2.SignedAngle(shots[8].Direction,shots[15].Direction)>139&&shots[8].At-shots[7].At>.95f,"reverse spiral after recovery gap");
                            }
                            if(kind==FoodBossKind.Denikani)
                            {
                                Check(clawSocket,"crossfire originates at displayed claw tips "+phase);
                                Check(Vector2.Distance(shots[0].Origin,shots[1].Origin)>1.4f&&Vector2.Dot(shots[1].Origin-shots[0].Origin,shots[0].Direction-shots[1].Direction)>0,"separate claw lanes converge "+phase);
                                if(phase==1)Check(Vector2.Distance(shots[6].Origin,shots[1].Origin)<.15f,"second crossfire reverses leading claw");
                            }
                            if(kind==FoodBossKind.Maribi)
                                Check(Vector2.Angle(shots[0].Direction,shots[2].Direction)<.1f&&Vector2.Angle(shots[0].Direction,shots[3].Direction)>15&&shots[3].At-shots[2].At>.85f,"rhythm locks burst then reacquires after pause "+phase);
                            if(kind==FoodBossKind.Tangdengi)
                                Check(Vector2.Angle(shots[0].Direction,shots[2].Direction)<.1f&&shots[3].At-shots[2].At>.95f&&shots[1].At-shots[0].At<.3f,"grapes stay grouped with a clear inter-bundle gap "+phase);
                            if(kind==FoodBossKind.Hurumi)
                            {
                                Position(player,origin+Vector2.up*12);
                                var first=shots[0];float low=0,high=0;bool corridor=true;float end=Time.time+2.8f;
                                while(Time.time<end&&first.Bullet!=null)
                                {
                                    Vector2 shift=(Vector2)first.Bullet.transform.position-(Vector2)Field(first.Bullet,"launch").GetValue(first.Bullet);
                                    float lateral=Vector2.Dot(shift,FoodBossRuntime.Rotate(first.Direction,90));low=Mathf.Min(low,lateral);high=Mathf.Max(high,lateral);
                                    for(int i=0;i+1<shots.Count;i+=2)if(shots[i].Bullet!=null&&shots[i+1].Bullet!=null)
                                        corridor&=Mathf.Abs(Vector2.Distance(shots[i].Bullet.transform.position,shots[i+1].Bullet.transform.position)-1.9f)<.01f;
                                    yield return null;
                                }
                                Check(corridor&&low<-.2f&&high>.2f,"wave curves both ways and preserves paired safe corridor "+phase);
                            }
                        }
                        Call(boss,"ClearEffects");yield return null;
                    }
                    Check(boss.Play(FoodBossAttack.Radial),"radial remains separately available "+kind+" P"+(phase+1));
                    var radial=new HashSet<int>();while(boss.Busy){foreach(var p in boss.Effects.GetComponentsInChildren<FoodBossBullet>())radial.Add(p.GetInstanceID());yield return null;}
                    Check(radial.Count==FoodBossRuntime.RingCount*(phase+1),"radial count unchanged "+kind+" P"+(phase+1));
                    Call(boss,"ClearEffects");yield return null;
                }
                Check(boss.Play(FoodBossAttack.Basic),"signature starts before disable "+kind);yield return new WaitForSeconds(1.1f);
                boss.gameObject.SetActive(false);yield return null;
                Check(!boss.Busy&&boss.Effects.GetComponentsInChildren<FoodBossBullet>().Length==0,"disable clears ongoing signature "+kind);
                Object.Destroy(boss.gameObject);yield return null;
            }
            yield return Avoidance(player,origin);
            Time.captureDeltaTime=0;
        }
    }
}
