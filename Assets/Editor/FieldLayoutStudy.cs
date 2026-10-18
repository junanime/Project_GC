using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Editor
{
    // Archived QA comparison candidate. The approved default is now FieldObjectLayout.
    public static class FieldLayoutStudy
    {
        public enum Kind { Chest, Merchant, BloodClot, EliteAltar }
        [Serializable] public sealed class Settings
        {
            public int chests=20, merchants=16, bloodClots=12, eliteAltars=6;
            public float wallInset=10, separation=8, merchantSeparation=13, clotSeparation=17;
            public float startExclusion=7, discoveryRadius=11;
        }
        public struct Point { public Kind kind; public Vector2 position; }
        public static readonly Settings Candidate = new Settings();
        public static List<Point> Generate(FiniteStomachMap map, Vector2 start, int seed, Settings settings)
        {
            var rng=new System.Random(seed);var points=new List<Point>();
            var kinds=new[]{Kind.BloodClot,Kind.Merchant,Kind.Chest,Kind.EliteAltar};
            var counts=new[]{settings.bloodClots,settings.merchants,settings.chests,settings.eliteAltars};
            // One of each essential interaction near the start; the rest are spread by sector.
            float rotation=(float)rng.NextDouble()*Mathf.PI*2;
            foreach(var kind in new[]{Kind.Chest,Kind.Merchant,Kind.BloodClot})
            {
                float radius=kind==Kind.Chest?9:kind==Kind.Merchant?15:21;
                for(int n=0;n<24;n++)
                {
                    float a=rotation+n*Mathf.PI/12+(int)kind*2.1f;
                    var p=start+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                    if(Allowed(map,start,p,kind,points,settings)){points.Add(new Point{kind=kind,position=p});break;}
                }
            }
            for(int k=0;k<kinds.Length;k++)for(int i=points.Count(p=>p.kind==kinds[k]);i<counts[k];i++)
            {
                bool placed=false;int sector=(i+seed%8)%8;
                for(int attempt=0;attempt<4000;attempt++)
                {
                    var rect=map.WorldRect;
                    var p=new Vector2(Mathf.Lerp(rect.xMin,rect.xMax,(float)rng.NextDouble()),Mathf.Lerp(rect.yMin,rect.yMax,(float)rng.NextDouble()));
                    var delta=p-rect.center;
                    int actual=Mathf.FloorToInt((Mathf.Atan2(delta.y,delta.x)+Mathf.PI)/(Mathf.PI*2)*8)%8;
                    if(attempt<1800&&actual!=sector)continue;
                    if(!Allowed(map,start,p,kinds[k],points,settings))continue;
                    points.Add(new Point{kind=kinds[k],position=p});placed=true;break;
                }
                if(!placed)throw new InvalidOperationException("No safe layout slot for "+kinds[k]+" seed="+seed);
            }
            return points;
        }
        static bool Allowed(FiniteStomachMap map,Vector2 start,Vector2 p,Kind kind,List<Point> points,Settings s)
        {
            if(map.DistanceToRim(p,out _) < s.wallInset || Vector2.Distance(p,start)<s.startExclusion)return false;
            foreach(var point in points)
            {
                float min=s.separation;
                if(kind==point.kind)min=kind==Kind.BloodClot?s.clotSeparation:kind==Kind.Merchant?s.merchantSeparation:min;
                if((point.position-p).sqrMagnitude<min*min)return false;
            }
            return true;
        }
        [MenuItem("Tools/QA/Spawn exploration layout in current Play Mode")]
        public static void Preview()
        {
            var level=Object.FindObjectOfType<LevelManager>();
            if(!EditorApplication.isPlaying||level?.PlayerCharacter==null||FiniteStomachMap.Instance==null)
            {Debug.LogWarning("Start Level 1 Play Mode, then run this preview.");return;}
            Spawn(level,Generate(FiniteStomachMap.Instance,level.PlayerCharacter.transform.position,Environment.TickCount&int.MaxValue,Candidate));
        }
        public static List<GameObject> Spawn(LevelManager level,List<Point> points)
        {
            var existing=GameObject.Find("QA exploration layout");if(existing!=null)throw new InvalidOperationException("Layout already installed for this run");
            var root=new GameObject("QA exploration layout");var objects=new List<GameObject>();
            var host=Object.FindObjectOfType<TimedSpecialMonsterSpawner>();
            foreach(string field in new[]{"bloodClotSpawns","summonObjectSpawns"})
                if(host!=null&&OctoberQaSweep.FindField(host,field)?.GetValue(host) is System.Collections.IEnumerable modules)
                    foreach(RuntimeModule module in modules)module.enabled=false;
            var spawners=Object.FindObjectsOfType<NPCSpawner>();
            var merchant=spawners.Select(s=>OctoberQaSweep.FindField(s,"merchantPrefab")?.GetValue(s) as GameObject).FirstOrDefault(p=>p!=null);
            foreach(var s in spawners){s.StopAllCoroutines();s.enabled=false;}
            foreach(var c in Object.FindObjectsOfType<Chest>())level.EntityManager.DespawnChest(c);
            foreach(var npc in Object.FindObjectsOfType<MerchantNPC>())Object.Destroy(npc.gameObject);
            foreach(var altar in Object.FindObjectsOfType<EliteMonsterSummonInteractable>())Object.Destroy(altar.gameObject);
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())if(m is BloodClotMonster&&m.HP>0)m.DespawnForStageTransfer();
            var blueprint=Object.Instantiate(level.CurrentLevelBlueprint);blueprint.chestSpawnAmount=0;
            OctoberQaSweep.FindField(level,"levelBlueprint").SetValue(level,blueprint);
            var elitePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Junhan/Interaction/EliteSummonInteractableObject.prefab");
            int clotPool=Array.FindIndex(blueprint.monsters,c=>c.monstersPrefab.GetComponent<BloodClotMonster>()!=null);
            var director=Object.FindObjectOfType<MiniStageDirector>();
            foreach(var point in points)
            {
                GameObject obj=null;
                if(point.kind==Kind.Chest)obj=level.EntityManager.SpawnChest(blueprint.chestBlueprint,point.position).gameObject;
                if(point.kind==Kind.Merchant&&merchant!=null)obj=Object.Instantiate(merchant,point.position,Quaternion.identity);
                if(point.kind==Kind.EliteAltar&&elitePrefab!=null)obj=Object.Instantiate(elitePrefab,point.position,Quaternion.identity);
                if(point.kind==Kind.BloodClot&&clotPool>=0)
                {
                    var clot=level.EntityManager.SpawnMonster(clotPool,point.position,blueprint.monsters[clotPool].monsterBlueprints[0]) as BloodClotMonster;
                    if(clot!=null){clot.ConfigureMiniStage(director,BloodClotMiniStageRole.EnterMiniStageOnDeath);obj=clot.gameObject;}
                }
                if(obj!=null){objects.Add(obj);if(point.kind==Kind.Merchant||point.kind==Kind.EliteAltar)obj.transform.SetParent(root.transform,true);}
            }
            return objects;
        }
        // Route estimates use limited visibility, 60% of field time for travel, and actual map shape.
        // They are availability estimates, not proof of purchase affordability or human clear rate.
        public static void Simulate(FiniteStomachMap map,Vector2 start,string output,int seeds=300,Settings settings=null)
        {
            settings=settings??Candidate;
            Directory.CreateDirectory(output);
            File.WriteAllText(output+"/layout-routes.csv","seed,policy,chests,merchants,bloodClots,eliteAltars,distance,unsafe,minSpacing\n");
            foreach(int seed in Enumerable.Range(0,seeds))
            {
                var points=Generate(map,start,seed,settings);
                int unsafeCount=points.Count(p=>map.ExactDistance(p.position,out _)<settings.wallInset-.2f);
                float spacing=float.MaxValue;
                for(int i=0;i<points.Count;i++)for(int j=i+1;j<points.Count;j++)spacing=Mathf.Min(spacing,Vector2.Distance(points[i].position,points[j].position));
                foreach(bool informed in new[]{false,true})
                {
                    var rng=new System.Random(seed+917);var taken=new HashSet<int>();var seen=new HashSet<int>();var pos=start;float elapsed=0,distance=0;
                    Vector2 waypoint=start;
                    while(elapsed<900)
                    {
                        for(int i=0;i<points.Count;i++)if(informed||Vector2.Distance(pos,points[i].position)<=settings.discoveryRadius)seen.Add(i);
                        int next=-1;float nearest=float.MaxValue;
                        foreach(int i in seen)if(!taken.Contains(i)){float d=Vector2.Distance(pos,points[i].position);if(d<nearest){nearest=d;next=i;}}
                        if(next>=0)waypoint=points[next].position;
                        else if(Vector2.Distance(pos,waypoint)<2)
                        {
                            for(int n=0;n<200;n++)
                            {var r=map.WorldRect;waypoint=new Vector2(Mathf.Lerp(r.xMin,r.xMax,(float)rng.NextDouble()),Mathf.Lerp(r.yMin,r.yMax,(float)rng.NextDouble()));if(map.DistanceToRim(waypoint,out _)>settings.wallInset)break;}
                        }
                        var to=Vector2.MoveTowards(pos,waypoint,1.15f*.60f*2f);
                        if(map.DistanceToRim(to,out _)<settings.wallInset)to=map.ClampToField(to,settings.wallInset);
                        distance+=Vector2.Distance(pos,to);pos=to;elapsed+=2;
                        if(next>=0&&Vector2.Distance(pos,points[next].position)<1)
                        {taken.Add(next);elapsed+=points[next].kind==Kind.EliteAltar?20:points[next].kind==Kind.Merchant?4:2;}
                    }
                    var count=Enum.GetValues(typeof(Kind)).Cast<Kind>().Select(k=>taken.Count(i=>points[i].kind==k));
                    File.AppendAllText(output+"/layout-routes.csv",$"{seed},{(informed?"map-informed":"local-vision")},{string.Join(",",count)},{distance:0.0},{unsafeCount},{spacing:0.00}\n");
                }
            }
            File.WriteAllText(output+"/layout-settings.json",JsonUtility.ToJson(settings,true));
        }
    }
}
