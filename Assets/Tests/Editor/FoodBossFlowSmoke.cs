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
        // No image, video or GIF capture. Exercise the actual Level1 combat routines.
        static IEnumerator FlowReview(Character player,Vector2 origin)
        {
            var level=Object.FindObjectOfType<LevelManager>();level.EntityManager.enabled=false;level.EntityManager.StopAllCoroutines();
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.DespawnForStageTransfer();
            foreach(var c in Object.FindObjectsOfType<Collectable>())c.gameObject.SetActive(false);
            player.CollectableCollider.enabled=false;
            Time.captureDeltaTime=1f/50;Time.timeScale=1;
            yield return CreamFlowChecks(player,origin);
            foreach(FoodBossKind kind in Enum.GetValues(typeof(FoodBossKind)))
            {
                for(int phase=1;phase<=2;phase++)
                {
                    foreach(var attack in new[]{FoodBossAttack.Basic,FoodBossAttack.Basic,FoodBossAttack.Radial,FoodBossAttack.Dash,FoodBossAttack.UniqueOne,FoodBossAttack.UniqueTwo,FoodBossAttack.Recovery})
                    {
                        if(phase==2&&attack==FoodBossAttack.Recovery)continue;
                        Position(player,origin+new Vector2(5,1));player.Move(Vector2.zero);
                        var boss=FoodBossRuntime.Spawn(kind,player,origin);boss.AutoPatterns=false;
                        if(phase==2){boss.SetPhaseTwoForTest();while(boss.Busy)yield return null;Check(!boss.Play(FoodBossAttack.Recovery),"P2 excludes recovery "+kind);}
                        else boss.TakeDamage(boss.MaxHealth*.3f);
                        bool signature=attack==FoodBossAttack.Basic&&basicFlowTurn++%2==1;
                        if(attack==FoodBossAttack.Basic)Set(boss,"basicSequence",signature?1:0);
                        Check(boss.Play(attack),"Start flow review "+kind+" P"+phase+" "+attack+(signature?" signature":""));
                        float start=Time.time,finished=-1,nextHit=2;int hit=0;
                        var seen=new HashSet<int>();bool speed=true,finite=true,shader=true;
                        for(int tick=0;tick<1600;tick++)
                        {
                            foreach(var bullet in boss.Effects.GetComponentsInChildren<FoodBossBullet>())
                            {
                                if(!seen.Add(bullet.GetInstanceID()))continue;
                                float actual=bullet.Velocity.magnitude;
                                if((float)Field(bullet,"waveLength").GetValue(bullet)>0)actual=(float)Field(bullet,"forwardSpeed").GetValue(bullet);
                                float expected=attack==FoodBossAttack.Radial?2.38f:attack==FoodBossAttack.UniqueOne&&kind==FoodBossKind.Maribi?3.5f:
                                    attack==FoodBossAttack.UniqueTwo&&kind==FoodBossKind.Tangdengi?(phase==2&&!bullet.Sugar?2.66f:2.31f):2.52f;
                                speed&=Mathf.Abs(actual-expected)<.002f;
                            }
                            if(tick%10==0)
                            {
                                foreach(var f in boss.Effects.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh!=null)
                                    foreach(var v in f.sharedMesh.vertices)finite&=!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y);
                                foreach(var r in boss.Effects.GetComponentsInChildren<MeshRenderer>())shader&=r.sharedMaterial!=null&&r.sharedMaterial.shader.isSupported;
                            }
                            if(attack==FoodBossAttack.Recovery&&boss.Recovering&&Time.time-start>=nextHit)
                            {
                                if(kind==FoodBossKind.Denikani){boss.TakeDamage(1,-(Vector2)Field(boss,"recoveryDirection").GetValue(boss));nextHit+=.85f;}
                                else
                                {
                                    var node=boss.Effects.GetComponentsInChildren<FoodBossWeakpoint>().FirstOrDefault(n=>n.Health>0);
                                    if(node!=null&&(kind!=FoodBossKind.Rolpangi||hit<2)){node.TakeDamage(200);hit++;}nextHit+=.45f;
                                }
                            }
                            if(!boss.Busy&&finished<0)finished=Time.time;
                            yield return null;
                            // Observe sugar-shell split children as well as the original volley.
                            float tail=kind==FoodBossKind.Tangdengi&&attack==FoodBossAttack.UniqueTwo?2.2f:.5f;
                            if(finished>=0&&Time.time-finished>=tail)break;
                        }
                        Check(!boss.Busy&&finite&&shader,"Pattern completes with finite supported geometry "+kind+" P"+phase+" "+attack);
                        Check(speed,"All observed projectiles use exactly 140% base speed "+kind+" P"+phase+" "+attack);
                        if(attack==FoodBossAttack.Basic)
                        {
                            int[] counts={8,6,6,6,8};int expected=signature?(phase==1?counts[(int)kind]:kind==FoodBossKind.Maribi||kind==FoodBossKind.Tangdengi?9:counts[(int)kind]*2):(phase==1?3:5);
                            Check(seen.Count==expected,"Basic projectile count unchanged: "+seen.Count);
                        }
                        if(attack==FoodBossAttack.Radial)Check(seen.Count==12*phase,"Radial count unchanged: "+seen.Count);
                        if(attack==FoodBossAttack.Recovery)Check(!boss.Recovering,"Recovery exits normally "+kind);
                        Object.Destroy(boss.gameObject);yield return null;
                    }
                }
            }
            Check(Object.FindObjectsOfType<FoodBossCreamTrail>().Length==0&&Object.FindObjectsOfType<FoodBossFlowRibbon>().Length==0,"All flow meshes and cream sources cleaned after boss removal");
            Time.captureDeltaTime=0;
        }
        static int basicFlowTurn;
        static IEnumerator CreamFlowChecks(Character player,Vector2 origin)
        {
            basicFlowTurn=0;Position(player,origin+Vector2.right*5);
            var boss=FoodBossRuntime.Spawn(FoodBossKind.Rolpangi,player,origin);boss.AutoPatterns=false;
            var path=new[]{origin+new Vector2(-3,-2),origin+new Vector2(0,-2),origin+new Vector2(3,-2)};
            var cream=FoodBossCreamTrail.Create(boss,path,new[]{false,false,false});
            for(int i=0;i<25;i++)yield return null;
            Check(cream.Reveal>.30f&&cream.Reveal<.36f,"Cream advances by distance during its 1.5-second extrusion");
            Check(cream.Contains(path[0]+Vector2.right)&&!cream.Contains(path[2]),"Only revealed cream has collision");
            float age=cream.Age;Time.timeScale=0;for(int i=0;i<4;i++)yield return null;
            Check(Mathf.Approximately(age,cream.Age),"Pause freezes cream extrusion and material");Time.timeScale=1;
            while(cream.Age<1.44f)yield return null;
            Check(!cream.ReachedEnd,"Cream incomplete before 1.5 seconds");
            while(cream.Age<1.51f)yield return null;
            Check(cream.ReachedEnd&&cream.Contains(path[2]),"Cream reaches endpoint at 1.5 seconds");
            Object.Destroy(cream.gameObject);yield return null;
            var p=new[]{origin+new Vector2(-3,-2),origin+new Vector2(-2,-2),origin+new Vector2(-1,-2),origin+new Vector2(0,-2),origin+new Vector2(1,-2),origin+new Vector2(2,-2),origin+new Vector2(3,-2)};
            var gap=FoodBossCreamTrail.Create(boss,p,new[]{false,false,false,true,false,false,false});
            while(!gap.ReachedEnd)yield return null;
            Check(!gap.Contains(p[3])&&gap.Contains(p[1]),"P2 cream gaps remain safe");
            Object.Destroy(boss.gameObject);yield return null;
        }
    }
}
