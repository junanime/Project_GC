using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    // One layout per stage-one run. Mini stages reuse the same field and do not reroll it.
    public sealed class FieldObjectLayout : MonoBehaviour
    {
        public enum Kind { Chest, Merchant, BloodClot, EliteAltar, GoldJar, SilverJar }
        public struct Slot { public Kind kind; public Vector2 position; public int partner; }
        public static readonly int[] Counts = { 24, 12, 14, 8, 20, 8 };
        public const float WallInset = 10f, StartClearance = 7f, ClusterChance = .20f;
        public IReadOnlyList<Slot> Slots => slots;
        public bool Ready { get; private set; }
        public bool Failed { get; private set; }
        public bool IsPlacing {get;private set;}
        public IReadOnlyList<GameObject> SpawnedObjects => objects;
        public int Seed { get; private set; }
        readonly List<Slot> slots = new List<Slot>();
        readonly List<Chest> chests = new List<Chest>();
        readonly List<GameObject> objects = new List<GameObject>();
        LevelManager level;
        Transform root;

        public static void Ensure(LevelManager owner)
        {
            if (!owner.UsesInitialFieldLayout || owner.GetComponent<FieldObjectLayout>() != null) return;
            owner.gameObject.AddComponent<FieldObjectLayout>().level = owner;
        }
        public IEnumerator RebuildForStage()
        {
            Ready=false;Failed=false;enabled=true;
            foreach(var chest in chests)if(chest!=null&&chest.gameObject.activeSelf)level.EntityManager.DespawnChest(chest);
            foreach(var o in objects)if(o!=null&&o.GetComponent<Chest>()==null&&o.GetComponent<Monster>()==null){o.SetActive(false);Destroy(o);}
            if(root!=null)Destroy(root.gameObject);
            slots.Clear();chests.Clear();objects.Clear();yield return null;
            IsPlacing=true;
            try{SpawnInitialObjects();}catch(Exception e){Failed=true;Debug.LogException(e,this);}finally{IsPlacing=false;}
            yield return null;
            foreach(var c in chests)if(c!=null)c.CompleteInitialPlacement();Physics2D.SyncTransforms();
            Ready=!Failed&&objects.Count==Counts.Sum()&&objects.All(o=>o!=null&&o.activeInHierarchy);
        }

        public static List<Slot> Generate(FiniteStomachMap map, Vector2 start, int seed, Func<Vector2, bool> blocked = null)
        {
            var rng = new System.Random(seed);
            var result = new List<Slot>();
            var paired = new HashSet<int>();
            float roll() => (float)rng.NextDouble();
            int Sector(Vector2 p)
            {
                var d = p - map.WorldRect.center;
                return Mathf.FloorToInt((Mathf.Atan2(d.y, d.x) + Mathf.PI) / (Mathf.PI * 2) * 8) % 8;
            }
            bool Allowed(Vector2 p, Kind kind, int partner)
            {
                if (map.NavigationDistance(p, out _) < WallInset || Vector2.Distance(p, start) < StartClearance || blocked?.Invoke(p) == true) return false;
                for (int j = 0; j < result.Count; j++)
                {
                    var other = result[j];
                    float gap = j == partner ? 4f : 8f;
                    if (j != partner && kind == other.kind)
                        gap = kind == Kind.BloodClot ? 16 : kind == Kind.Merchant || kind == Kind.EliteAltar ? 13 : 8;
                    if (j == partner && (kind == Kind.BloodClot || kind == Kind.EliteAltar || other.kind == Kind.BloodClot || other.kind == Kind.EliteAltar)) gap = 6;
                    if ((p - other.position).sqrMagnitude < gap * gap) return false;
                }
                return true;
            }
            // Short first exploration goals, rotated each run; no objects under the spawn point.
            float rotation = roll() * Mathf.PI * 2;
            foreach (var kind in new[] { Kind.Chest, Kind.Merchant, Kind.BloodClot })
                for (int attempt = 0; attempt < 48; attempt++)
                {
                    float a = rotation + attempt * Mathf.PI / 24 + (int)kind * 2.1f;
                    float r = kind == Kind.Chest ? 9 : kind == Kind.Merchant ? 15 : 21;
                    var p = start + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (!Allowed(p, kind, -1)) continue;
                    result.Add(new Slot { kind = kind, position = p, partner = -1 }); break;
                }
            // Shuffle types before balancing sectors so scarce objects are not pushed to leftovers.
            var pending = new List<Kind>();
            foreach (Kind kind in Enum.GetValues(typeof(Kind)))
                for (int i = result.Count(s => s.kind == kind); i < Counts[(int)kind]; i++) pending.Add(kind);
            for (int i = pending.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var k = pending[i]; pending[i] = pending[j]; pending[j] = k; }
            foreach (var kind in pending)
            {
                int sector = rng.Next(8);
                int minCount = Enumerable.Range(0, 8).Min(s => result.Count(p => Sector(p.position) == s));
                for (int i = 0; i < 8; i++) if (result.Count(p => Sector(p.position) == sector) <= minCount + 1) break; else sector = (sector + 1) % 8;
                int partner = -1;
                var anchors = Enumerable.Range(0, result.Count).Where(i => !paired.Contains(i) && Sector(result[i].position) == sector).ToArray();
                if (anchors.Length > 0 && roll() < ClusterChance) partner = anchors[rng.Next(anchors.Length)];
                bool placed = false;
                for (int attempt = 0; attempt < 6000; attempt++)
                {
                    if (attempt == 150) partner = -1; // A blocked pair becomes an ordinary isolated placement.
                    Vector2 p;
                    if (partner >= 0)
                    {
                        float a = roll() * Mathf.PI * 2, r = Mathf.Lerp(6.05f, 7.5f, roll());
                        p = result[partner].position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    }
                    else
                    {
                        var rect = map.WorldRect;
                        p = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, roll()), Mathf.Lerp(rect.yMin, rect.yMax, roll()));
                    }
                    if (attempt < 3000 && Sector(p) != sector || !Allowed(p, kind, partner)) continue;
                    if (partner >= 0) { paired.Add(partner); paired.Add(result.Count); }
                    result.Add(new Slot { kind = kind, position = p, partner = partner }); placed = true; break;
                }
                if (!placed) throw new InvalidOperationException("No safe field layout slot: " + kind + " seed=" + seed);
            }
            return result;
        }

        IEnumerator Start()
        {
            yield return null; // All scene Start hooks (mini-stage director and NPC references) have run.
            if (level == null || !level.UsesInitialFieldLayout) yield break;
            IsPlacing=true;
            try { SpawnInitialObjects(); }
            catch (Exception error) { Failed=true; Debug.LogException(error,this); }
            finally{IsPlacing=false;}
            if(Failed)yield break;
            // NPC Start hooks and interaction registration finish under the loading overlay.
            yield return null;
            foreach(var chest in chests)chest.CompleteInitialPlacement();
            Physics2D.SyncTransforms();
            Ready = objects.Count == Counts.Sum() && objects.All(o=>o!=null&&o.activeInHierarchy);
            if(!Ready){Failed=true;Debug.LogError("Initial field objects did not finish placing.",this);}
        }
        void SpawnInitialObjects()
        {
            var host = FindObjectOfType<TimedSpecialMonsterSpawner>();
            var merchant = FindObjectsOfType<NPCSpawner>().Select(n => n.MerchantPrefab).FirstOrDefault(p => p != null);
            var altar = host != null ? host.EliteAltarPrefab : null;
            var bp = level.CurrentLevelBlueprint;
            int clotIndex = Array.FindIndex(bp.monsters, m => m.monstersPrefab.GetComponent<BloodClotMonster>() != null);
            if (merchant == null || altar == null || clotIndex < 0) throw new InvalidOperationException("Field layout references are incomplete.");
            Seed = UnityEngine.Random.Range(0, int.MaxValue);
            bool Blocked(Vector2 p) => Physics2D.OverlapCircleAll(p, 2f).Any(c => !c.isTrigger && c.GetComponentInParent<Character>() == null && c.GetComponentInParent<Monster>() == null);
            slots.AddRange(Generate(FiniteStomachMap.Instance, level.PlayerCharacter.transform.position, Seed, Blocked));
            root = new GameObject("Initial field objects").transform;
            root.SetParent(transform, false);
            var director = FindObjectOfType<MiniStageDirector>();
            foreach (var slot in slots)
            {
                GameObject placed;
                switch (slot.kind)
                {
                    case Kind.Chest:
                        var chest=level.EntityManager.SpawnChest(bp.chestBlueprint, slot.position);chests.Add(chest);placed=chest.gameObject;break;
                    case Kind.Merchant: placed=Instantiate(merchant, slot.position, Quaternion.identity, root); break;
                    case Kind.EliteAltar: placed=Instantiate(altar, slot.position, Quaternion.identity, root); break;
                    case Kind.BloodClot:
                        var clot = level.EntityManager.SpawnMonster(clotIndex, slot.position, bp.monsters[clotIndex].monsterBlueprints[0]) as BloodClotMonster;
                        clot.ConfigureMiniStage(director, BloodClotMiniStageRole.EnterMiniStageOnDeath);placed=clot.gameObject;break;
                    default: placed=CurrencyJar.Create(level, slot.position, slot.kind == Kind.SilverJar, root).gameObject;break;
                }
                objects.Add(placed);
                var marker=placed.GetComponent<MapMarker>()??placed.AddComponent<MapMarker>();
                var kind=slot.kind==Kind.Chest?MapMarkerKind.Chest:slot.kind==Kind.Merchant?MapMarkerKind.Shop:
                    slot.kind==Kind.EliteAltar?MapMarkerKind.EliteSpawner:slot.kind==Kind.BloodClot?MapMarkerKind.Portal:MapMarkerKind.Object;
                marker.Configure(kind,slot.kind==Kind.GoldJar?"골드 초콜릿 병":slot.kind==Kind.SilverJar?"실버 초콜릿 병":placed.name);
                marker.RequireNearbyDiscovery=true;marker.ResetDiscovery();
            }
            if(level.GetComponent<StageProgression>()?.Travelling==true)level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
        }

        void Update()
        {
            if (!Ready || level == null || level.UsesInitialFieldLayout) return;
            // A real stage transition clears stage-one props; a mini-stage visit never reaches here.
            foreach (var chest in chests) if (chest != null && chest.gameObject.activeSelf) level.EntityManager.DespawnChest(chest);
            if (root != null) Destroy(root.gameObject);
            Ready = false; enabled = false;
        }
    }
}
