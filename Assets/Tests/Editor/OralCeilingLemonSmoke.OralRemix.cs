#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public static partial class OralCeilingLemonSmoke
    {
        static IEnumerator CheckOralRemix(LevelManager level, OralFieldEvents events)
        {
            const string folder = "field-balance/oral-remix/";
            Directory.CreateDirectory("Library/GStarUIQA/" + folder);
            var stage = OralStageRuntime.Instance;
            var ambient = events.Ambient;
            var player = level.PlayerCharacter;
            var entry = player.Position;
            var layout = level.GetComponent<FieldObjectLayout>();
            Check(ambient.Stones.Count >= 6 && ambient.Stones.Count <= 8, "scattered permanent stones on entry: " + ambient.Stones.Count);
            var positions = ambient.Stones.Select(stone => stone.Position).ToArray();
            for (int i = 0; i < positions.Length; i++)
            {
                var spot = positions[i];
                Check(OralMapGeometry.TongueDistance(stage.Map,spot,out _) >= 1.39f &&
                    layout.Slots.All(slot => Vector2.Distance(spot, slot.position) >= 1.79f) &&
                    positions.Skip(i+1).All(other => Vector2.Distance(spot,other) >= 2.99f), "stone lies on clear spaced tongue " + i);
            }
            Check(!events.ForceEvent(OralEventKind.Boulder), "timed boulder event is retired");

            var group = level.CurrentLevelBlueprint.monsters.Select((g,pool) => new {g,pool}).Single(item =>
                item.g.monsterBlueprints.Count(OralMonsterRoster.IsBlueprint)==6);
            var species = group.g.monsterBlueprints;
            Vector2 center = OralMapGeometry.TongueCenter(stage.Map);
            var subjectCenter=center;
            float best=float.NegativeInfinity;
            foreach (int dx in new[]{0,-2,2,-4,4}) foreach(int dy in new[]{0,-2,2,-4,4})
            {
                Vector2 at=center+new Vector2(dx,dy);
                if (OralMapGeometry.TongueDistance(stage.Map,at,out _) < 4) continue;
                float clearance=float.PositiveInfinity;
                foreach (var point in positions.Concat(layout.Slots.Select(slot=>slot.position)))
                {
                    var d=point-at;
                    float outside=Mathf.Max(Mathf.Abs(d.x)-2.8f,Mathf.Abs(d.y)-1.15f);
                    clearance=Mathf.Min(clearance,outside);
                }
                if (clearance<=best) continue;
                best=clearance;subjectCenter=at;
            }
            Place(player,subjectCenter + Vector2.down * 2f);
            var frozen = new Dictionary<Rigidbody2D,RigidbodyConstraints2D>();
            var rng = Random.state;
            Random.InitState(91327);
            for (int i=0;i<species.Length;i++)
            {
                var seen = new HashSet<string>();
                for (int sample=0;sample<48 && seen.Count<3;sample++)
                {
                    var spot = subjectCenter + new Vector2((i-2.5f)*.94f,(seen.Count-1)*.85f);
                    var m = level.EntityManager.SpawnMonster(group.pool,spot,species[i],0,false);
                    var sprite = m.BodyRenderer.sprite;
                    if (!seen.Add(sprite.name)) { m.DespawnForStageTransfer(); continue; }
                    var rb = m.GetComponent<Rigidbody2D>(); frozen[rb]=rb.constraints;
                    rb.constraints=RigidbodyConstraints2D.FreezeAll;
                    var source = m.Blueprint.resultSprite;
                    Check(sprite.rect.size == source.rect.size && Mathf.Abs(sprite.bounds.size.y-source.bounds.size.y)<.001f,
                        "flavour preserves source silhouette and foot scale " + sprite.name);
                }
                Check(seen.Count==3,"one original and two flavours in ordinary spawn pool " + species[i].name);
            }
            Random.state=rng;
            yield return new WaitForSeconds(.3f);
            GStarUISmoke.Capture(folder+"six-foods",1280,720);
            foreach (var pair in frozen) pair.Key.constraints=pair.Value;
            Clear(level); Place(player,entry);
            var rock = ambient.Stones.OrderBy(s=>(s.Position-player.Position).sqrMagnitude).First();
            var origin = rock.Position;
            bool lane = ambient.TryHoneyLane(out var target,out var axis);
            if (!lane)
            {
                // The visitor may have moved out of the opening sector while inspecting food.
                var aim = (center-origin).normalized;
                Place(player, origin-aim*1.05f);
                lane = ambient.TryHoneyLane(out target,out axis);
            }
            Check(lane && events.ForceEvent(OralEventKind.Sweet), "first honey opens an optional bowling line");
            var honey = events.Tastes.HoneyPositions;
            Check(honey.Count==3 && Vector2.Distance(target,honey[1])<.1f, "three honey pieces lead forward from a real field rock");
            var targets = new List<Monster>(); frozen.Clear();
            for (int i=0;i<3;i++)
            {
                var m=level.EntityManager.SpawnMonster(group.pool,origin+axis*(1.5f+i*.83f),species[i%2],0,false);
                var rb=m.GetComponent<Rigidbody2D>(); frozen[rb]=rb.constraints;
                rb.constraints=RigidbodyConstraints2D.FreezeAll;targets.Add(m);
            }
            Place(player,origin-axis*.65f);
            int before=ambient.RockHits;
            player.Move(axis);
            GStarUISmoke.Capture(folder+"bowling-ready",960,540);
            yield return new WaitForFixedUpdate();
            Check(player.TryDash(),"actual player dash launches the bowling stone");
            int oldRate=Time.captureFramerate;Time.captureFramerate=18;
            for(int frame=0;frame<32;frame++)
            {
                yield return null;
                if (frame%2==0) GStarUISmoke.Capture(folder+"bowling-"+frame.ToString("D2"),960,540);
            }
            Time.captureFramerate=oldRate; player.Move(Vector2.zero);
            Check(ambient.RockHits>=before+2 && targets.Count(m=>!m.IsAlive)>=2,
                "one propelled stone hits and kills multiple ordinary foods: hits="+(ambient.RockHits-before)+
                " kills="+targets.Count(m=>!m.IsAlive)+" travel="+Vector2.Distance(origin,rock.Position)+" axis="+axis);
            foreach (var pair in frozen) if(pair.Key!=null) pair.Key.constraints=pair.Value;
            events.Tastes.Configure(level); Clear(level); Place(player,center);
        }
    }
}
#endif
