#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static partial class ColonSpecialSmoke
    {
        static IEnumerator BeetleWallWarning(LevelManager level,ColonMonsterRoster roster,Character player)
        {
            var map=FiniteStomachMap.Instance;var ridge=ColonMapGeometry.CentralRidge(map);
            Vector2 start=default,target=default;
            for(int i=3;i<ridge.Length-3;i++)
            {
                Vector2 normal=Vector2.Perpendicular((ridge[i+1]-ridge[i-1]).normalized);
                var a=ridge[i]+normal*(map.WallWidth+2.1f);var b=ridge[i]-normal*(map.WallWidth+2.1f);
                if(map.FloorClearance(a,true)<1||map.FloorClearance(b,true)<1)continue;
                start=a;target=b;break;
            }
            Check(start!=target&&!ColonAttackWall.Clear(start,target),"beetle wall fixture uses the actual central wall");
            Position(player,target);var beetle=Spawn(level,roster.NewSpecials[4],start);
            var runtime=beetle.GetComponent<ColonSpecialRuntime>();Set(runtime,"cooldown",100f);
            runtime.StartCoroutine((IEnumerator)Call(runtime,"Throw"));
            yield return new WaitForSeconds(.2f);
            var warning=(LineRenderer)Field(runtime,"throwWarning").GetValue(runtime);Check(warning!=null,"beetle warning exists during windup");
            Vector2 from=(Vector2)beetle.transform.position+Vector2.up*.4f;
            Vector2 landing=ColonSpecialEffects.ResolveLanding(from,player.transform.position);
            Check(Vector2.Distance(landing,player.transform.position)>1,"wall clips requested bomb landing before target");
            Check(Vector2.Distance(warning.transform.position,landing)<.02f,"warning is drawn at the real wall-clipped landing");
            Check(map.FloorClearance(landing)>=.22f&&ColonAttackWall.Clear(from,landing,.13f),"warning landing stays on reachable floor");
            Capture("beetle-wall-warning");
            yield return new WaitForSeconds(2.9f);
            var bombArt=ColonSpecialEffects.Art("beetle",11);
            var shot=Object.FindObjectsOfType<ColonSpecialEffects>().FirstOrDefault(e=>e.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.sprite==bombArt));
            Check(shot!=null,"real beetle projectile launches after three-second windup");
            Check(warning!=null&&Vector2.Distance(warning.transform.position,shot.LandingPoint)<.02f,"released projectile and retained warning share the exact clipped landing");
            Check(Vector2.Distance(shot.LandingPoint,landing)<.02f,"stationary wall target does not drift on release");
            Capture("beetle-wall-flight");beetle.DespawnForStageTransfer();yield return new WaitForSeconds(.9f);
        }
    }
}
#endif
