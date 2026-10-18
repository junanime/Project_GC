using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Vampire.Editor
{
    // Integration checks in a temporary Level 1 play session, not production behavior.
    public static class StomachMapChecks
    {
        const string Output="Library/StomachMapProof";
        static void Check(bool ok,string message)
        {
            if(!ok)throw new Exception("Stomach boundary check: "+message);
            Debug.Log("[StomachBoundary] PASS "+message);File.AppendAllText(Output+"/boundary-checks.txt",message+"\n");
        }
        public static IEnumerator Run(LevelManager level,FiniteStomachMap map)
        {
            File.WriteAllText(Output+"/boundary-checks.txt","");
            var player=level.PlayerCharacter;var body=player.GetComponent<Rigidbody2D>();
            var original=body.position;bool playerEnabled=player.enabled;
            player.enabled=false;player.Move(Vector2.zero);
            Vector2 rim=map.ClampToField(map.UVToWorld(new Vector2(.245f,1)),.1f);
            map.ExactDistance(rim,out var inward);
            Vector2 upper=map.ClampToField(rim,map.ClimbLimit+.45f);
            Check(Mathf.Abs(FiniteStomachMap.UphillMultiplier(.5f)-.5f)<.001f,"mid climb: 50% uphill speed");
            Check(Mathf.Abs(FiniteStomachMap.UphillMultiplier(1)-.1f)<.001f,"climb end: 10% uphill speed (90% reduction)");
            float last=1;
            for(int i=0;i<=100;i++){float f=FiniteStomachMap.UphillMultiplier(i/100f);CheckOnce(f<=last+.0001f,"monotonic slope resistance");last=f;}
            var random=UnityEngine.Random.state;UnityEngine.Random.InitState(14521);
            for(int i=0;i<500;i++)
            {
                var p=map.WorldRect.center+UnityEngine.Random.insideUnitCircle*map.WorldRect.width;
                var safe=FiniteStomachMap.SafeSpawn(p,1);
                CheckOnce(map.ExactDistance(safe,out _)>=map.WallWidth+.98f,"spawn projection remains on flat ground at every edge");
            }
            UnityEngine.Random.state=random;Check(true,"500 outside/inside spawn candidates constrained, including concave edges");
            Vector2 flat=rim+inward*(map.WallWidth+2);
            float radius=player.GetComponent<StomachSlopeBody>().Radius;
            Vector2 middle=rim+inward*(map.WallWidth-(map.Definition.climbableWidth-radius)*.5f-.1f);
            // Sample just before the hard stop, excluding collision's zero velocity.
            Vector2 end=rim+inward*(map.WallWidth-(map.Definition.climbableWidth-radius)*.95f-.1f);
            yield return SamplePlayerSpeed(player,body,flat,-inward);
            float flatSpeed=sampledSpeed;
            yield return SamplePlayerSpeed(player,body,middle,-inward);
            float middleRatio=sampledSpeed/flatSpeed;
            yield return SamplePlayerSpeed(player,body,end,-inward);
            float endRatio=sampledSpeed/flatSpeed;
            Check(Mathf.Abs(middleRatio-.5f)<.05f && Mathf.Abs(endRatio-.1f)<.04f,$"measured uphill velocity: middle {middleRatio:P1}, near end {endRatio:P1} of flat speed");
            player.Move(Vector2.zero);
            var direct=new GameObject("Direct velocity slope check");var directBody=direct.AddComponent<Rigidbody2D>();
            directBody.gravityScale=0;directBody.drag=0;var directSlope=StomachSlopeBody.Ensure(direct);
            Vector2 directPoint=rim+inward*(map.WallWidth-(map.Definition.climbableWidth-directSlope.Radius)*.5f-.1f);
            for(int i=0;i<10;i++)
            {
                directBody.position=directPoint;direct.transform.position=directPoint;directBody.velocity=-inward*2;
                yield return new WaitForFixedUpdate();
            }
            Check(Mathf.Abs(Vector2.Dot(directBody.velocity,-inward)-1)<.08f,"zero-drag / direct-velocity monster also slows to 50% uphill");
            Object.Destroy(direct);
            var chest=level.EntityManager.SpawnChest(level.CurrentLevelBlueprint.chestBlueprint,map.WorldRect.max+Vector2.one*10);
            Check(map.ExactDistance(chest.transform.position,out _)>=map.WallWidth+.65f,"actual chest outside request corrected");
            level.EntityManager.DespawnChest(chest);
            var npc=Object.FindObjectOfType<NPCSpawner>();
            body.position=upper;player.transform.position=upper;
            if(npc!=null)
            {
                typeof(NPCSpawner).GetMethod("SpawnMerchant",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(npc,new object[]{1f});
                Check(Object.FindObjectsOfType<MerchantNPC>().All(m=>map.ExactDistance(m.transform.position,out _)>=map.WallWidth+.8f),"merchant spawn respects field footprint");
            }
            Monster melee=null;
            for(int i=0;i<level.CurrentLevelBlueprint.monsters.Length;i++)
            foreach(var blueprint in level.CurrentLevelBlueprint.monsters[i].monsterBlueprints)
            {
                if(blueprint is SniperMonsterBlueprint)
                {
                    var sniper=level.EntityManager.SpawnMonster(i,map.WorldRect.max+Vector2.one*20,blueprint);
                    if(sniper!=null){Check(map.ExactDistance(sniper.transform.position,out _)>=map.WallWidth+.7f,"sniper Setup relocation also stays inside");sniper.DespawnForStageTransfer();}
                }
                if(melee==null && blueprint is MeleeMonsterBlueprint && !(blueprint is EliteMonsterBlueprint))
                    melee=level.EntityManager.SpawnMonster(i,original,blueprint);
            }
            body.position=upper;player.transform.position=upper;body.velocity=Vector2.zero;
            player.Move(-inward);
            int charges=player.CurrentDashCharges;
            Check(Mathf.Abs(map.Definition.climbableWidth/map.WallWidth-.75f)<.001f,"movement boundary at three quarters of wall height");
            Check(player.TryDash() && player.CurrentDashCharges==charges-1,"dash can activate on slope and consumes one charge");
            float dashUntil=Time.realtimeSinceStartup+3;
            while(player.IsDashing && Time.realtimeSinceStartup<dashUntil)
            {
                yield return new WaitForFixedUpdate();
                CheckOnce(map.ExactDistance(body.position,out _)>=map.ClimbLimit+radius-.015f,"dash cannot cross wall limit");
            }
            Check(!player.IsDashing,"slope dash completes normally inside the three-quarter limit");
            Vector2 downhill=FiniteStomachMap.ClampDash(upper,upper+inward*2,radius);
            Check(Vector2.Distance(downhill,upper+inward*2)<.001f,"downhill dash is not blocked by slope");
            Vector2 tangent=new Vector2(inward.y,-inward.x);
            Vector2 sideways=FiniteStomachMap.ClampDash(middle,middle+tangent,radius);
            Check(Vector2.Distance(sideways,middle)>.9f,"sideways dash moves along the slope");
            player.Move(Vector2.zero);body.position=upper;player.transform.position=upper;body.velocity=Vector2.zero;
            float before=map.DistanceToRim(body.position,out _);
            yield return new WaitForSeconds(.7f);
            Check(map.DistanceToRim(body.position,out _)>before+.1f,"released player slides down the curved wall");
            body.position=upper;player.transform.position=upper;body.velocity=Vector2.zero;
            level.SetRunFlowPaused(true);yield return new WaitForSeconds(.3f);
            Check(Vector2.Distance(body.position,upper)<.02f,"run-flow pause freezes slope simulation");level.SetRunFlowPaused(false);
            if(melee!=null)
            {
                melee.enabled=false;var mb=melee.GetComponent<Rigidbody2D>();mb.position=upper;melee.transform.position=upper;mb.velocity=Vector2.zero;
                body.position=original;player.transform.position=original;
                float mBefore=map.DistanceToRim(mb.position,out _);yield return new WaitForSeconds(.7f);
                Check(map.DistanceToRim(mb.position,out _)>mBefore+.1f,"monster uses the same downhill slide");
                melee.DespawnForStageTransfer();
            }
            // Actual travel animation, room creation, room-owned spawn and return.
            player.enabled=playerEnabled;player.Move(Vector2.zero);body.position=original;player.transform.position=original;body.velocity=Vector2.zero;
            var director=Object.FindObjectOfType<MiniStageDirector>();
            director.OpenEntrancePortal(original+Vector2.right);
            var portal=Object.FindObjectsOfType<BloodClotMiniStagePortal>().First(p=>!p.Reserved);
            director.EnterMiniStageFromPortal(portal);
            float until=Time.realtimeSinceStartup+20;
            while(!MiniStageRuntimeState.IsInsideMiniStage && Time.realtimeSinceStartup<until)yield return null;
            Check(MiniStageRuntimeState.IsInsideMiniStage && director.CurrentRoom!=null,"real mini-stage entrance and room initialization");
            yield return null;
            Check(!map.FieldVisible && !map.AppliesAt(body.position),"stomach wall and movement disabled in remote mini-stage");
            Vector2 roomPoint=body.position;
            Check(FiniteStomachMap.SafeSpawn(roomPoint)==roomPoint,"mini-stage spawn locations are not projected into main field");
            var roomChest=level.EntityManager.SpawnChest(level.CurrentLevelBlueprint.chestBlueprint,roomPoint+Vector2.right);
            Check(Vector2.Distance(roomChest.transform.position,roomPoint+Vector2.right)<.01f,"actual mini-stage reward chest stays in its room");
            level.EntityManager.DespawnChest(roomChest);
            var room=director.CurrentRoom;
            typeof(MiniStageRoomBase).GetMethod("UnlockOptionalReturn",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(room,null);
            body.position=room.ReturnInteractable.transform.position;player.transform.position=body.position;body.velocity=Vector2.zero;
            yield return new WaitForSeconds(.1f);
            director.ReturnToFieldFromInteractable(room.ReturnInteractable);
            until=Time.realtimeSinceStartup+20;
            while((MiniStageRuntimeState.IsInsideMiniStage || director.IsTransitioning) && Time.realtimeSinceStartup<until)yield return null;
            Check(!MiniStageRuntimeState.IsInsideMiniStage && !director.IsTransitioning && map.Contains(body.position),"real mini-stage return restores field movement");
            var ui=ApothecaryUI.Instance;ui.OpenRunBook();yield return null;yield return null;
            var raw=GameObject.Find("Live exploration map").GetComponent<RawImage>();
            Check(raw.texture==map.OverviewTexture && raw.uvRect==new Rect(0,0,1,1),"TAB shows entire stomach shape");
            Check(GameObject.Find("Stomach map marker")!=null,"TAB preserves positioned player/object icons");
            ScreenCapture.CaptureScreenshot(Output+"/05-tab-stomach-map.png");yield return null;yield return null;ui.CloseRunBook();
            body.position=original;player.transform.position=original;body.velocity=Vector2.zero;
            File.WriteAllText(Output+"/boundary-result.txt","PASS");
        }
        static void CheckOnce(bool condition,string message){if(!condition)throw new Exception(message);}
        static float sampledSpeed;
        static IEnumerator SamplePlayerSpeed(Character player,Rigidbody2D body,Vector2 position,Vector2 direction)
        {
            var step=typeof(Character).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            player.Move(direction);body.velocity=Vector2.zero;
            for(int i=0;i<80;i++)
            {
                body.position=position;player.transform.position=position;
                step.Invoke(player,null);
                yield return new WaitForFixedUpdate();
            }
            sampledSpeed=Mathf.Max(.001f,Vector2.Dot(body.velocity,direction));
        }
    }
}
