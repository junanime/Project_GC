using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Optional, individually activated encounters. No encounter grants stage completion.
    public sealed class ColonBossDirector : MonoBehaviour
    {
        public struct HabitatPlacement
        {
            public ColonBossKind kind;
            public Vector2 position, wallNormal;
            public bool clustered;
            public int partner;
        }
        public const float PreferredSeparation=20.8f, MinimumSeparation=10f, ClosePairChance=.025f;
        public const float EntryClearance=14f, PropClearance=5.6f;
        public const float ManateeHouseHalfWidth=6.5f, ManateeHouseHalfDepth=2.4f;
        public bool Ready { get; private set; }
        public bool Failed { get; private set; }
        public int Seed { get; private set; }
        public int DefeatedCount => defeated.Count;
        public string Objective => "선택 미니보스 토벌 "+DefeatedCount+"/6 · 전부 처치할 필요는 없습니다";
        public static void ShowNotice(string title,string detail){var toast=UnityEngine.Object.FindObjectOfType<StageEventToastUI>();if(toast!=null)toast.Show(title+"|"+detail);}

        public IReadOnlyList<ColonBossHabitat> Habitats => habitats;
        public IReadOnlyList<HabitatPlacement> Placements => placements;
        public LevelManager Level => level;
        readonly List<ColonBossHabitat> habitats=new List<ColonBossHabitat>();
        readonly List<HabitatPlacement> placements=new List<HabitatPlacement>();
        readonly HashSet<ColonBossHabitat> claimed=new HashSet<ColonBossHabitat>();
        readonly HashSet<ColonBossHabitat> defeated=new HashSet<ColonBossHabitat>();
        readonly Dictionary<ColonBossHabitat,ColonBossRuntime> active=new Dictionary<ColonBossHabitat,ColonBossRuntime>();
        LevelManager level;
        Transform root;

        public static ColonBossDirector Ensure(LevelManager owner)
        {
            var d=owner.GetComponent<ColonBossDirector>()??owner.gameObject.AddComponent<ColonBossDirector>();
            d.level=owner;return d;
        }
        public void PrepareStage(Vector2 entry)
        {
            var map=FiniteStomachMap.Instance;
            if(map==null||!map.IsColon) return;
            if(Ready)return; // A mini-stage visit never rerolls the six homes.
            Ready=false;Failed=false;
            try
            {
                Clear();
                var layout=level.GetComponent<FieldObjectLayout>();
                // Reuse the habitat seed validated with the field layout under the loading screen.
                Seed=layout!=null?layout.Seed^0x62B7:UnityEngine.Random.Range(0,int.MaxValue);
                placements.AddRange(Generate(map,entry,Seed,layout?.Slots));
                if(placements.Count!=6)throw new InvalidOperationException("Colon encounter layout must contain six homes.");
                root=new GameObject("Stage 2 · six optional boss habitats").transform;root.SetParent(transform,false);
                foreach(var slot in placements)
                {
                    var h=ColonBossHabitat.Create(this,slot.kind,slot.position,slot.wallNormal);
                    if(h==null)throw new InvalidOperationException("Missing colon habitat: "+slot.kind);
                    h.transform.SetParent(root,true);habitats.Add(h);
                }
                Physics2D.SyncTransforms();Ready=habitats.Count==6;
            }
            catch {Failed=true;Clear();throw;}
        }

        public bool CanSummon(ColonBossHabitat habitat)
        {
            return Ready&&!Failed&&habitat!=null&&habitats.Contains(habitat)&&!claimed.Contains(habitat)&&
                level!=null&&!level.IsLevelEnded&&!level.IsRunFlowPaused&&Time.timeScale>0&&
                !MiniStageRuntimeState.IsInsideMiniStage&&FiniteStomachMap.Instance!=null&&FiniteStomachMap.Instance.IsColon;
        }
        public bool TrySummon(ColonBossHabitat habitat,Character player)
        {
            if(player==null||!CanSummon(habitat))return false;
            // Claim before creating anything: repeated clicks and simultaneous input cannot duplicate a boss.
            claimed.Add(habitat);
            try
            {
                var boss=ColonBossRuntime.Spawn(habitat.Kind,player,habitat.transform.position,habitat);
                if(boss==null){claimed.Remove(habitat);return false;}
                active[habitat]=boss;
                return true;
            }
            catch {claimed.Remove(habitat);throw;}
        }
        public void NotifyDefeated(ColonBossHabitat habitat)
        {
            if(habitat==null||!claimed.Contains(habitat)||!defeated.Add(habitat))return;
            active.Remove(habitat);
            // Byunbi King at the lower-left opening remains the mandatory final encounter.
            // These six must not call LevelPassed, FinalBossDefeated, or create a stage-exit portal.
        }
        public bool IsDefeated(ColonBossHabitat habitat)=>habitat!=null&&defeated.Contains(habitat);

        public static List<HabitatPlacement> Generate(FiniteStomachMap map,Vector2 entry,int seed,IReadOnlyList<FieldObjectLayout.Slot> props=null)
        {
            if(map==null||!map.IsColon)throw new ArgumentException("Colon geometry required.",nameof(map));
            var rng=new System.Random(seed);var result=new List<HabitatPlacement>();
            var finalThrone=ColonOpeningGeometry.ThroneCenter(map);
            float Roll()=>(float)rng.NextDouble();
            bool ClearProps(Vector2 p,float radius)
            {
                if(props==null)return true;
                foreach(var prop in props)if((prop.position-p).sqrMagnitude<radius*radius)return false;
                return true;
            }
            bool EntrySafe(Vector2 p)=>Vector2.Distance(p,entry)>=EntryClearance&&(p-finalThrone).sqrMagnitude>=81;
            if(props!=null) {
                var reserved=Generate(map,entry,seed);
                if(reserved.TrueForAll(h=>ClearProps(h.position,PropClearance+(h.kind==ColonBossKind.Manatee?2:0))))return reserved;
            }

            // FieldObjectLayout reserves at least one of these narrow wall opportunities first.
            var candidates=FindManateeSites(map,entry);
            candidates.RemoveAll(h=>!ClearProps(h.position,PropClearance+2));
            if(candidates.Count==0)throw new InvalidOperationException("No central ridge segment can fit the complete manatee home.");
            result.Add(candidates[rng.Next(candidates.Count)]);

            var pending=new List<ColonBossKind>{ColonBossKind.Flamingo,ColonBossKind.Shark,ColonBossKind.Pudding,ColonBossKind.Hedgehog,ColonBossKind.Turtle};
            for(int i=pending.Count-1;i>0;i--){int j=rng.Next(i+1);var k=pending[i];pending[i]=pending[j];pending[j]=k;}
            float rotation=Roll()*Mathf.PI*2;
            var paired=new HashSet<int>();
            for(int k=0;k<pending.Count;k++)
            {
                var kind=pending[k];var definition=ColonBossDefinition.Get(kind);
                int partner=-1;
                if(Roll()<ClosePairChance)
                {
                    var anchors=new List<int>();for(int j=0;j<result.Count;j++)if(!paired.Contains(j))anchors.Add(j);
                    if(anchors.Count>0)partner=anchors[rng.Next(anchors.Count)];
                }
                bool placed=false;
                for(int attempt=0;attempt<10000;attempt++)
                {
                    if(attempt==400)partner=-1;
                    Vector2 p;
                    if(partner>=0)
                    {
                        float a=Roll()*Mathf.PI*2,r=Mathf.Lerp(MinimumSeparation+1,14,Roll());
                        p=result[partner].position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;
                    }
                    else
                    {
                        var r=map.WorldRect;p=new Vector2(Mathf.Lerp(r.xMin,r.xMax,Roll()),Mathf.Lerp(r.yMin,r.yMax,Roll()));
                        if(attempt<4000)
                        {
                            var delta=p-map.WorldRect.center;
                            float a=Mathf.Repeat(Mathf.Atan2(delta.y,delta.x)-rotation,Mathf.PI*2);
                            if(Mathf.FloorToInt(a/(Mathf.PI*2)*5)!=k)continue;
                        }
                    }
                    if(!EntrySafe(p)||!ClearProps(p,PropClearance)||!GroundFits(map,p,definition.HabitatRadius))continue;
                    bool valid=true;
                    for(int j=0;j<result.Count;j++)
                    {
                        float gap=j==partner?MinimumSeparation:PreferredSeparation;
                        if((result[j].position-p).sqrMagnitude<gap*gap){valid=false;break;}
                    }
                    if(!valid)continue;
                    if(partner>=0){paired.Add(partner);paired.Add(result.Count);}
                    result.Add(new HabitatPlacement{kind=kind,position=p,wallNormal=Vector2.up,clustered=partner>=0,partner=partner});
                    placed=true;break;
                }
                if(!placed)throw new InvalidOperationException("No safe colon boss habitat: "+kind+" seed="+seed);
            }
            return result;
        }

        static FiniteStomachMap siteMap;
        static StomachMapDefinition siteDefinition;
        static Rect siteRect;
        static Vector2 siteEntry;
        static List<HabitatPlacement> siteCache;
        public static List<HabitatPlacement> FindManateeSites(FiniteStomachMap map,Vector2 entry)
        {
            var candidates=new List<HabitatPlacement>();
            if(map==null||!map.IsColon)return candidates;
            if(siteCache!=null&&siteMap==map&&siteDefinition==map.Definition&&siteRect==map.WorldRect&&siteEntry==entry)
                return new List<HabitatPlacement>(siteCache);
            // Both detached central ridges provide sockets; attached side branches do not.
            foreach(var ridge in ColonMapGeometry.HabitatRidges(map))
            for(int i=3;i<ridge.Length-4;i++)
            {
                var tangent=(ridge[Mathf.Min(ridge.Length-1,i+2)]-ridge[Mathf.Max(0,i-2)]).normalized;
                if(tangent.sqrMagnitude<.5f)continue;
                var normal=new Vector2(-tangent.y,tangent.x);
                for(int side=-1;side<=1;side+=2)
                {
                    var n=normal*side;var p=ridge[i]+n*(map.WallWidth+2.5f);
                    if(Vector2.Distance(p,entry)<EntryClearance||!HouseFits(map,p,n))continue;
                    candidates.Add(new HabitatPlacement{kind=ColonBossKind.Manatee,position=p,wallNormal=n,partner=-1});
                }
            }
            siteMap=map;siteDefinition=map.Definition;siteRect=map.WorldRect;siteEntry=entry;
            siteCache=new List<HabitatPlacement>(candidates);
            return candidates;
        }

        public static bool GroundFits(FiniteStomachMap map,Vector2 p,float radius)
        {
            if(map.FloorClearance(p)<radius-.35f||map.FloorClearance(p,true)<radius)return false;
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI*2/12;var q=p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                if(map.FloorClearance(q,true)<0)return false;
            }
            return true;
        }
        public static bool HouseFits(FiniteStomachMap map,Vector2 p,Vector2 outward)
        {
            Vector2 n=outward.sqrMagnitude>.1f?outward.normalized:Vector2.down,t=new Vector2(-n.y,n.x);
            // This structure is inset into the ridge. Its rear is allowed inside the
            // solid wall, while every usable entrance below must reach the flat floor.
            float insetLimit=Mathf.Max(.5f,map.WallWidth-map.Definition.climbableWidth)+1.5f;
            for(int x=-2;x<=2;x++)for(int y=-1;y<=1;y++)
            {
                var q=p+t*(x*ManateeHouseHalfWidth/2)+n*(y*ManateeHouseHalfDepth);
                if(ColonMapGeometry.Distance(map,q,out _,out _)<insetLimit)return false;
                // Keep extra corner clearance around diagonal homes as well as their aligned doors.
                q=p+new Vector2(x*ManateeHouseHalfWidth/2,y*ManateeHouseHalfDepth);
                if(ColonMapGeometry.Distance(map,q,out _,out _)<insetLimit)return false;
            }
            for(int door=0;door<3;door++)for(int step=0;step<=3;step++)
            {
                // Use the actual art sockets, including the raised center opening. The first
                // sample is precisely the arrival/exit point used by the live habitat.
                var q=ManateeDoorPosition(p,n,door)+n*(step==0?1.5f:step*2.5f);
                if(map.FloorClearance(q,true)<1.5f)return false;
            }
            return true;
        }
        public static Vector2 ManateeDoorPosition(Vector2 center,Vector2 outward,int index)
        {
            Vector2 n=outward.sqrMagnitude>.1f?outward.normalized:Vector2.down;
            Vector2 right=new Vector2(n.y,-n.x);
            // Socket centers measured from the current house/mask pixels (720 x 240).
            Vector2 local=index<=0?new Vector2(-4.64271f,1.55056f):index>=2?new Vector2(4.58318f,1.55084f):new Vector2(-.02678f,2.54156f);
            return center+(right*local.x+n*local.y)*(13f/14f);
        }
        void Clear()
        {
            Ready=false;
            var remaining=new List<ColonBossRuntime>(active.Values);
            active.Clear();
            foreach(var boss in remaining)if(boss!=null){boss.gameObject.SetActive(false);Destroy(boss.gameObject);}
            active.Clear();claimed.Clear();defeated.Clear();habitats.Clear();placements.Clear();
            if(root!=null){root.gameObject.SetActive(false);Destroy(root.gameObject);root=null;}
        }
        void OnDestroy(){Clear();}
    }
}
