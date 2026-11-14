using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public enum ColonEventKind { None, Expulsion, Absorption, Residue, Nutrients }

    /// <summary>Colon-only events; one disruptive event at a time, gameplay-time ownership.</summary>
    [DefaultExecutionOrder(12000), DisallowMultipleComponent]
    public sealed class ColonFieldEvents : MonoBehaviour
    {
        public static ColonFieldEvents Instance { get; private set; }
        public ColonEventKind Current { get; private set; }
        public float Age { get; private set; }
        public float Duration { get; private set; }
        public bool AutoSchedule = true;
        public const float WarningSeconds = 2.5f;
        public LevelManager Level { get; private set; }
        public FiniteStomachMap Map { get; private set; }
        public Character Player => Level != null ? Level.PlayerCharacter : null;
        public bool Paused => Level == null || Level.IsRunFlowPaused || Time.timeScale <= 0 ||
            StageEntryLoading.IsLoading || MiniStageRuntimeState.IsInsideMiniStage || Player == null || Player.IsChoosingAugment;
        public bool Running => Current != ColonEventKind.None && !Paused;
        public float Strength => Current == ColonEventKind.None ? 0 :
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(WarningSeconds, WarningSeconds + 1.3f, Age)) *
            (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Duration - 2, Duration, Age)));
        public readonly List<Vector2> DryCenters = new List<Vector2>();
        public readonly List<ColonNutrient> Nutrients = new List<ColonNutrient>();
        public readonly List<ColonResidue> Residues = new List<ColonResidue>();
        public float DryRadius = 1.4f;
        public int Collected { get; private set; }
        public int Broken { get; internal set; }
        public float Clock { get; private set; }
        public Vector2 Origin { get; private set; }
        public ColonFlowField Flow { get; private set; }
        ColonEventVisual visual;
        float nextEvent = 90, nextResidue, contactCooldown;
        int emitted, bagIndex;
        readonly List<Monster> scratch = new List<Monster>();
        readonly ColonEventKind[] bag = { ColonEventKind.Expulsion, ColonEventKind.Absorption, ColonEventKind.Residue, ColonEventKind.Nutrients };
        SnailSlowStatus slow;

        public static ColonFieldEvents Install(LevelManager level)
        {
            if (level == null) return null;
            var e = level.GetComponent<ColonFieldEvents>() ?? level.gameObject.AddComponent<ColonFieldEvents>();
            e.Cancel(); e.Level = level; e.Map = FiniteStomachMap.Instance; Instance = e;
            e.Flow = new ColonFlowField(e.Map); e.nextEvent = 90; e.bagIndex = 0;
            e.visual = level.GetComponent<ColonEventVisual>() ?? level.gameObject.AddComponent<ColonEventVisual>();
            e.visual.Configure(e); return e;
        }
        void Update()
        {
            if (Map == null || !Map.IsColon || Level == null || Level.IsLevelEnded || Player == null || !Player.IsAlive)
            { if (Current != ColonEventKind.None) Cancel(); return; }
            if (Paused) { slow?.Exit(GetInstanceID(), 0); return; }
            float dt = Time.deltaTime; Clock += dt;
            if (Current == ColonEventKind.None)
            {
                if (AutoSchedule && Level.CurrentLevelTime >= nextEvent)
                {
                    if (bagIndex == 0) for (int i = bag.Length - 1; i > 0; i--)
                    { int j = UnityEngine.Random.Range(0, i + 1); var k = bag[i]; bag[i] = bag[j]; bag[j] = k; }
                    Begin(bag[bagIndex]); bagIndex = (bagIndex + 1) % bag.Length;
                }
                return;
            }
            Age += dt; contactCooldown = Mathf.Max(0, contactCooldown - dt);
            if (Current == ColonEventKind.Absorption) UpdateDry();
            if (Current == ColonEventKind.Residue && Age >= WarningSeconds && Age < Duration - 3 && emitted < 20 && Age >= nextResidue)
            { SpawnResidue(); nextResidue = Age + .48f; }
            if (Age >= Duration)
            { Cancel(); nextEvent = Level.CurrentLevelTime + UnityEngine.Random.Range(65, 95); }
        }
        void FixedUpdate()
        {
            if (!Running || Current != ColonEventKind.Expulsion || Strength <= 0 || !Player.IsAlive) return;
            // A small backward slide against full movement input, but dash retains control.
            float speed = Mathf.Clamp(Player.CurrentMoveSpeed, .8f, 2.8f);
            if (!Player.IsDashing && !Player.IsPortalTravelling)
                Push(Player.transform, Flow.Direction(Player.Position) * speed * 1.16f * Strength * Time.fixedDeltaTime, .22f);
            scratch.Clear(); foreach (var m in Level.EntityManager.LivingMonsters) scratch.Add(m);
            foreach (var m in scratch)
            {
                if (m == null || !m.IsAlive || m.IsMiniStageOwned || m.IsFieldRuntimeSuspended || m is BossMonster || m is BloodClotMonster || m.Blueprint is BossMonsterBlueprint) continue;
                Push(m.transform, Flow.Direction(m.Position) * speed * 1.75f * Strength * Time.fixedDeltaTime, Mathf.Clamp(m.Size.magnitude * .2f, .15f, .5f));
            }
        }
        public Vector2 Push(Transform actor, Vector2 displacement, float radius)
        {
            Vector2 start = actor.position;
            Vector2 safe = ColonAttackWall.Stop(start, start + displacement, radius);
            actor.position = new Vector3(safe.x, safe.y, actor.position.z);
            var rb = actor.GetComponent<Rigidbody2D>(); if (rb != null) rb.position = safe;
            return safe;
        }
        public bool Begin(ColonEventKind kind)
        {
            if (Paused || Map == null || !Map.IsColon || !Player.IsAlive || Level.IsLevelEnded || kind == ColonEventKind.None) return false;
            Cancel(); Current = kind; Age = 0; Collected = Broken = emitted = 0; nextResidue = WarningSeconds;
            Origin = Map.ClampToFloor(Player.Position, 2); Duration = kind == ColonEventKind.Nutrients ? 24 : kind == ColonEventKind.Absorption ? 18 : 14;
            string title = kind == ColonEventKind.Expulsion ? "대변 배출|통로를 따라 강한 바람이 붑니다 · 반대로 이동해 저항하세요" :
                kind == ColonEventKind.Absorption ? "수분 흡수|마른 바닥은 느려집니다 · 촉촉한 길을 따라 이동하세요" :
                kind == ColonEventKind.Residue ? "찌꺼기 대이동|굴러오는 찌꺼기를 부숴 길을 여세요" :
                "잔여 영양분 노출|공격할 필요 없어요 · 가까이에서 상호작용해 바로 획득";
            var toast = FindObjectOfType<StageEventToastUI>() ?? gameObject.AddComponent<StageEventToastUI>(); toast.ShowImmediateEncounter(title);
            if (kind == ColonEventKind.Absorption) BuildDry();
            if (kind == ColonEventKind.Nutrients) BuildNutrients();
            visual?.Rebuild(); return true;
        }
        void BuildDry()
        {
            Vector2 forward = Flow.Direction(Origin); if (forward.sqrMagnitude < .1f) forward = Vector2.right;
            Vector2 across = new Vector2(-forward.y, forward.x);
            // Alternate dry islands preserve a continuous central damp lane.
            for (int i = -3; i <= 3; i++) for (int side = -1; side <= 1; side += 2)
            {
                var p = Origin + forward * i * 1.9f + across * (side * (2.05f + .22f * Mathf.Sin(i * 1.7f)));
                if (Map.FloorClearance(p) >= DryRadius + .1f) DryCenters.Add(p);
            }
        }
        public bool OnDry(Vector2 at)
        {
            if (Current != ColonEventKind.Absorption || Strength < .1f) return false;
            foreach (var p in DryCenters) if (((at - p) / new Vector2(1, .78f)).sqrMagnitude < DryRadius * DryRadius) return true;
            return false;
        }
        void UpdateDry()
        {
            if (slow == null) slow = Player.GetComponent<SnailSlowStatus>() ?? Player.gameObject.AddComponent<SnailSlowStatus>();
            if (OnDry(Player.Position) && !Player.IsDashing) slow.Enter(GetInstanceID(), .28f * Strength);
            else slow.Exit(GetInstanceID(), 0);
        }
        void BuildNutrients()
        {
            Vector2 forward = Flow.Direction(Origin); if (forward.sqrMagnitude < .1f) forward = Vector2.right;
            var side = new Vector2(-forward.y, forward.x);
            for (int i = 0; i < 3; i++)
            {
                var at = Map.ClampToFloor(Origin + forward * (1.5f + i * .7f) + side * ((i - 1) * 1.45f), .6f);
                if (Nutrients.Exists(n => Vector2.Distance(n.transform.position, at) < 1)) continue;
                Nutrients.Add(ColonNutrient.Create(this, at, i == 1));
            }
        }
        public bool Claim(ColonNutrient pickup)
        {
            if (!Running || Current != ColonEventKind.Nutrients || Age < .35f || pickup == null || pickup.Collected || !Nutrients.Contains(pickup) ||
                Vector2.Distance(Player.Position, pickup.transform.position) > 1.25f || !ColonAttackWall.Clear(Player.Position, pickup.transform.position)) return false;
            pickup.Collected = true; Collected++;
            if (pickup.Healing) Player.GainPickupHealth(Player.MaxHealth * .12f);
            else Player.GainExp(12);
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.GoldPickup);
            return true;
        }
        void SpawnResidue()
        {
            Vector2 flow = Flow.Direction(Origin); if (flow.sqrMagnitude < .1f) flow = Vector2.right;
            Vector2 at = Origin;
            // Walk upstream on the same safe flow field; do not spawn behind a ridge.
            for (int k = 0; k < 38; k++)
            { var dir = Flow.Direction(at); var next = ColonAttackWall.Stop(at, at - dir * .2f, .5f); if ((next - at).sqrMagnitude < .001f) break; at = next; }
            Vector2 side = new Vector2(-flow.y, flow.x);
            at = Map.ClampToFloor(at + side * ((emitted % 3 - 1) * .85f), .5f);
            Residues.Add(ColonResidue.Create(this, at, emitted++));
        }
        public void HitByResidue(Vector2 from)
        {
            if (!Running || contactCooldown > 0 || Player.IsDashing || !ColonAttackWall.Clear(from, Player.Position)) return;
            contactCooldown = .85f; Player.TakeDamage(Mathf.Max(4, Player.MaxHealth * .06f));
        }
        public void Cancel()
        {
            slow?.Exit(GetInstanceID(), 0); DryCenters.Clear();
            foreach (var p in Nutrients) if (p != null) Destroy(p.gameObject); Nutrients.Clear();
            foreach (var r in Residues) if (r != null) Destroy(r.gameObject); Residues.Clear();
            Current = ColonEventKind.None; Age = Duration = 0; visual?.Clear();
        }
        void OnDisable() { Cancel(); if (Instance == this) Instance = null; }
        void OnDestroy() { Cancel(); if (Instance == this) Instance = null; }
    }

    /// <summary>Floor-only flood field to the outlet. Diagonals never cut a wall corner.</summary>
    public sealed class ColonFlowField
    {
        const float Cell = 1.1f;
        readonly FiniteStomachMap map; readonly int w, h; readonly bool[] walk; readonly int[] costs;
        public readonly Vector2 Outlet;
        public ColonFlowField(FiniteStomachMap m)
        {
            map = m; w = Mathf.CeilToInt(m.WorldRect.width / Cell); h = Mathf.CeilToInt(m.WorldRect.height / Cell);
            walk = new bool[w * h]; costs = new int[walk.Length];
            Outlet = m.ClampToFloor(m.UVToWorld(new Vector2(.20f, .20f)), 1);
            int goal = -1; float best = float.MaxValue;
            for (int i = 0; i < walk.Length; i++)
            { walk[i] = m.FloorClearance(Point(i)) > .45f; costs[i] = int.MaxValue;
                if (walk[i] && (Point(i) - Outlet).sqrMagnitude < best) { best = (Point(i) - Outlet).sqrMagnitude; goal = i; } }
            if (goal < 0) return;
            var q = new Queue<int>(); costs[goal] = 0; q.Enqueue(goal);
            while (q.Count > 0)
            {
                int at = q.Dequeue(), x = at % w, y = at / w;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy; if (nx < 0 || nx >= w || ny < 0 || ny >= h || dx == 0 && dy == 0) continue;
                    int n = ny * w + nx;
                    if (!walk[n] || costs[n] != int.MaxValue || dx != 0 && dy != 0 && (!walk[y * w + nx] || !walk[ny * w + x])) continue;
                    costs[n] = costs[at] + 1; q.Enqueue(n);
                }
            }
        }
        Vector2 Point(int i) => map.WorldRect.min + new Vector2(i % w + .5f, i / w + .5f) * Cell;
        public Vector2 Direction(Vector2 p)
        {
            var local = (p - map.WorldRect.min) / Cell;
            int x = Mathf.Clamp((int)local.x, 0, w - 1), y = Mathf.Clamp((int)local.y, 0, h - 1), at = y * w + x;
            Vector2 sum = Vector2.zero; float weight = 0;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy; if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                int n = ny * w + nx;
                if (!walk[n] || costs[n] >= costs[at] || dx != 0 && dy != 0 && (!walk[y * w + nx] || !walk[ny * w + x])) continue;
                Vector2 v = Point(n) - p; if (v.sqrMagnitude < .001f) continue;
                float k = 1 / Mathf.Max(.3f, v.magnitude); sum += v.normalized * k; weight += k;
            }
            if (weight == 0) return Vector2.zero;
            var dir = sum.normalized;
            if (map.FloorClearance(p + dir * .5f) < .22f)
                return Vector2.zero;
            return dir;
        }
    }
}
