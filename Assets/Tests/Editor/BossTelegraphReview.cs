#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Reads actual live warning vertices and emitted velocities, not a duplicate of the skill formula.
    public static class BossTelegraphReview
    {
        static string report;
        static void Check(bool value,string text)
        {
            if(!value)throw new Exception("BOSS_TELEGRAPH: "+text);
            File.AppendAllText(report,"PASS "+text+"\n");
        }
        static Vector2[] Vertices(LineRenderer line)
        {
            var p=new Vector2[line.positionCount];for(int i=0;i<p.Length;i++)p[i]=line.GetPosition(i);return p;
        }
        static bool Contains(Vector2[] polygon,Vector2 point)
        {
            bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
            {
                Vector2 a=polygon[i],b=polygon[j];
                if((a.y>point.y)!=(b.y>point.y)&&point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }
            return inside;
        }
        static LineRenderer Find(Transform root,string name)=>root.GetComponentsInChildren<LineRenderer>().FirstOrDefault(l=>l.name==name);
        public static IEnumerator Capture(LevelManager level,string output,Action<string> frame)
        {
            report=output+"/telegraph-checks.txt";File.WriteAllText(report,"");
            var map=FiniteStomachMap.Instance;var player=level.PlayerCharacter;
            Vector2 arena=map.ClampToFloor(map.UVToWorld(new Vector2(.3f,.25f)),8);
            Check(map.FloorClearance(arena,true)>6,"actual stage-two open floor fixture");
            ColonContentSmoke.Position(player,arena+Vector2.right*6);
            var flamingo=ColonBossRuntime.Spawn(ColonBossKind.Flamingo,player,arena,null);flamingo.AutoPatterns=false;
            float deadline=Time.time+12;while(flamingo.Arriving&&Time.time<deadline)yield return null;
            Check(!flamingo.Arriving,"flamingo arrival completed");flamingo.enabled=false;
            Vector2 aim=flamingo.Aim;Check(flamingo.PlayPattern(1),"real flamingo diagonal pattern started");
            LineRenderer left=null,right=null;deadline=Time.time+6;
            while(Time.time<deadline)
            {
                left=Find(flamingo.Effects,"Flamingo second diagonal · left");right=Find(flamingo.Effects,"Flamingo second diagonal · right");
                if(left!=null&&right!=null)break;yield return null;
            }
            Check(left!=null&&right!=null,"both second-volley sectors are independently telegraphed");
            var lp=Vertices(left);var rp=Vertices(right);float shownAt=Time.time;
            frame("flamingo-second-diagonal-warning");var secondShots=new HashSet<int>();
            while(flamingo.Busy&&Time.time<deadline)
            {
                foreach(var shot in flamingo.Effects.GetComponentsInChildren<ColonBossProjectile>())
                {
                    if(Mathf.Abs(Vector2.SignedAngle(aim,shot.Velocity))<46||!secondShots.Add(shot.GetInstanceID()))continue;
                    Check(Time.time-shownAt>=.60f,"second diagonal projectile waited for its visible warning");
                    Vector2 point=lp[0]+shot.Velocity.normalized*3;
                    Check(Contains(lp,point)||Contains(rp,point),"actual second-volley direction lies inside a rendered warning");
                }
                if(secondShots.Count==6)break;yield return null;
            }
            Check(secondShots.Count==6,"all six unchanged second-diagonal projectiles observed");
            Object.Destroy(flamingo.gameObject);yield return null;

            var king=ConstipationKingRuntime.Spawn(level,arena);king.AutoPatterns=false;
            deadline=Time.time+12;while(king.Arriving&&Time.time<deadline)yield return null;
            Check(!king.Arriving,"king arrival completed");
            try
            {
                foreach(int phase in new[]{1,2})
                {
                    // Isolate pattern geometry; phase-transition combat is covered by KingSmoke.
                    ColonContentSmoke.Set(king,"<PhaseTwo>k__BackingField",phase==2);king.Visual.SetPhase(phase);
                    ColonContentSmoke.Position(player,arena+Vector2.right*6);
                    Check(king.PlayPattern(4),"real phase-"+phase+" hook started");yield return new WaitForSeconds(.15f);
                    var warning=Find(king.Effects,"King plunger · locked launch warning");Check(warning!=null,"hook has a locked launch warning");
                    var polygon=Vertices(warning);Vector2 from=(polygon[0]+polygon[3])*.5f,to=(polygon[1]+polygon[2])*.5f;
                    yield return new WaitForSeconds(.95f);
                    foreach(int side in new[]{-1,1})
                    {
                        ColonContentSmoke.Position(player,arena+Vector2.right*6*side);yield return new WaitForSeconds(.15f);
                        var tether=Find(king.Effects,"King plunger · current staff tether");Check(tether!=null,"hook tether exists during launch");
                        Check(king.Visual.Facing==side,"live target crossing really mirrors the boss");
                        Check(Vector2.Distance(tether.GetPosition(0),king.Visual.StaffTip)<.06f,"rope remains attached to the current mirrored/hovered staff");
                        Check(SnailBossProjectile.SegmentDistance(tether.GetPosition(1),from,to)<.035f,"outgoing cup stays on the originally warned trajectory");
                        frame("king-hook-phase-"+phase+(side<0?"-left":"-right"));
                    }
                    king.CancelPattern();yield return null;Check(king.Effects.childCount==0,"hook cancellation removes tether and props");
                }
                ColonContentSmoke.Position(player,arena+Vector2.right*6);Check(king.PlayPattern(1),"real phase-two double fan started");yield return new WaitForSeconds(.15f);
                var fan=Find(king.Effects,"King double fan · combined warning");Check(fan!=null,"combined fan warning exists");
                var fanPolygon=Vertices(fan);frame("king-combined-fan-warning");
                var velocities=new HashSet<int>();deadline=Time.time+5;
                while(king.Busy&&Time.time<deadline)
                {
                    foreach(var shot in king.Effects.GetComponentsInChildren<ConstipationKingProjectile>())
                    {
                        int heading=Mathf.RoundToInt(Mathf.Atan2(shot.Velocity.y,shot.Velocity.x)*Mathf.Rad2Deg*1000);
                        if(!velocities.Add(heading))continue;
                        Check(Contains(fanPolygon,fanPolygon[0]+shot.Velocity.normalized*3),"actual fan projectile direction lies inside the combined warning");
                    }
                    yield return null;
                }
                Check(!king.Busy&&velocities.Count==14,"both seven-shot volleys complete inside their common telegraph");
                king.CancelPattern();yield return null;Check(king.Effects.childCount==0,"fan cleanup leaves no stale warnings or bullets");
            }
            finally{if(king!=null)Object.Destroy(king.gameObject);}
        }
    }
}
#endif
