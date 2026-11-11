using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public sealed class OralWhirlpoolState
    {
        public Vector2 Position;
        public float Radius, Angle;
        internal Vector2 Direction;
        internal float TurnRemaining;
    }

    // Finale time is gameplay time, independent of the debug shortcut's starting clock.
    [DisallowMultipleComponent, DefaultExecutionOrder(12000)]
    public sealed class OralSwallowFinale : MonoBehaviour
    {
        public const float StartTime = 555f, WaterTime = 10f, SurvivalTime = 20f, SweepTime = 40f, TransferTime = 43f;
        public const float VortexSpeed = 1.15f * .7f; // Ashi's unmodified movement stat, not the selected hero's bonuses.
        public static OralSwallowFinale Instance { get; private set; }
        public bool Active { get; private set; }
        public bool Pending { get; private set; }
        public float Age => age;
        public float Water01 => Active ? Mathf.Clamp01((age - WaterTime) / 10f) : 0;
        public float SurvivalRemaining => Mathf.Clamp(SweepTime - age, 0, 20);
        public float JawClosure01 => Active ? Mathf.SmoothStep(0, 1, age - 19f) : 0;
        public bool IsSwallowing => Active && age >= SweepTime;
        public float Swallow01 => Active ? Mathf.Clamp01((age - SweepTime) / 3f) : 0;
        public bool VorticesActive => Active && age >= SurvivalTime && age < SweepTime;
        public IReadOnlyList<OralWhirlpoolState> Whirlpools => whirlpools;

        readonly List<OralWhirlpoolState> whirlpools = new List<OralWhirlpoolState>();
        readonly List<SweptMonster> swept = new List<SweptMonster>();
        OralStageRuntime stage;
        OralFieldEvents events;
        LevelManager level;
        Character player;
        float age, spawnClock, pullGrace;
        bool shortcutHeld, sweeping, ownsPause, priorPause, transferring;
        Character dashOwner;
        Vector2 dashDirection, sweepOrigin;
        SpriteRenderer playerVisual;
        Vector3 playerVisualScale;
        Quaternion playerVisualRotation;
        struct SweptMonster
        {
            public Monster Monster;
            public Vector2 Start;
            public bool WasSuspended;
        }

        public void Configure(OralStageRuntime owner)
        {
            CancelForTransition();
            stage = owner; level = owner.Level; player = level.PlayerCharacter;
            events = owner.GetComponent<OralFieldEvents>();
            age = 0; spawnClock = 0; shortcutHeld = false; transferring = false;
            Instance = this;
        }

        bool StageAvailable => stage != null && stage.Ready && level != null && player != null && player.IsAlive &&
            !level.IsLevelEnded && (level.GetComponent<StageProgression>()?.StageNumber ?? -1) == 0;

        public bool StartForTesting()
        {
            if (!StageAvailable || Active || Pending) return false;
            RunScore.Exclude("꿀꺽 이벤트 F1+F2 테스트");
            Pending = true;
            TryBegin();
            return true;
        }

        // Shared by the keyboard path and the editor regression harness; held keys start once.
        public bool HandleTestShortcut(bool f1Held, bool f2Held)
        {
            bool both = f1Held && f2Held;
            bool pressed = both && !shortcutHeld;
            shortcutHeld = both;
            return pressed && StartForTesting();
        }

        void Update()
        {
            if (!StageAvailable) { if (Active || Pending) CancelForTransition(); return; }
            HandleTestShortcut(!GameInput.KeyboardBlocked && GameInput.GetKey(KeyCode.F1),
                !GameInput.KeyboardBlocked && GameInput.GetKey(KeyCode.F2));
            if (transferring) return;
            if (!Active)
            {
                if (level.CurrentLevelTime >= StartTime) Pending = true;
                if (Pending) TryBegin();
                return;
            }
            if (Time.deltaTime <= 0 || player.IsChoosingAugment || player.IsPortalTravelling ||
                StageEntryLoading.IsLoading || MiniStageRuntimeState.IsInsideMiniStage ||
                (level.IsRunFlowPaused && !ownsPause)) return;
            float dt = Time.deltaTime;
            age += dt;
            pullGrace = Mathf.Max(0, pullGrace - dt);
            if (age >= 16f && !sweeping) stage.ReturnToTongueForSwallow();
            if (age < SweepTime)
            {
                spawnClock -= dt;
                if (spawnClock <= 0)
                {
                    spawnClock = .75f;
                    // Reuse the original oral roster, rewards, health and field population cap.
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Random.Range(0, Mathf.PI * 2);
                        Vector2 q = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(.72f, .94f);
                        Vector2 p = OralMapGeometry.TonguePoint(stage.Map, q);
                        if (Vector2.Distance(p, player.Position) > 3f) events.SpawnFinaleMonster(p);
                    }
                }
                if (VorticesActive) MoveWhirlpools(dt);
            }
            else
            {
                if (!sweeping) BeginSweep();
                if (age >= TransferTime) BeginTransfer();
            }
        }

        void TryBegin()
        {
            if (!Pending || !StageAvailable || Time.timeScale <= 0 || level.IsRunFlowPaused ||
                StageEntryLoading.IsLoading || MiniStageRuntimeState.IsInsideMiniStage || player.IsChoosingAugment ||
                player.IsPortalTravelling || stage.OnCeiling || stage.IsAirborne ||
                (level.GetComponent<StageProgression>()?.Travelling ?? false)) return;
            events.PrepareForSwallow();
            Pending = false; Active = true; age = 0; spawnClock = 0;
            BuildWhirlpools();
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.FieldEventStart);
        }

        void BuildWhirlpools()
        {
            whirlpools.Clear();
            // Scattered centers across the ENTIRE tongue; no corridor is authored or reserved.
            Vector3[] layout = {
                new Vector3(-.43f,.73f,1.6f), new Vector3(-.05f,.75f,2.55f), new Vector3(.38f,.72f,1.9f),
                new Vector3(-.68f,.38f,2.2f), new Vector3(-.3f,.40f,2.9f), new Vector3(.13f,.38f,1.45f), new Vector3(.61f,.37f,3.05f),
                new Vector3(-.77f,-.04f,1.25f), new Vector3(-.47f,-.06f,2.05f), new Vector3(-.04f,-.01f,3.5f), new Vector3(.44f,-.02f,2.55f), new Vector3(.78f,-.06f,1.4f),
                new Vector3(-.73f,-.43f,2.75f), new Vector3(-.30f,-.43f,1.35f), new Vector3(.15f,-.42f,2.2f), new Vector3(.65f,-.44f,3.35f),
                new Vector3(-.8f,-.79f,1.35f), new Vector3(-.46f,-.78f,2.6f), new Vector3(-.04f,-.82f,1.7f), new Vector3(.32f,-.78f,2.9f), new Vector3(.78f,-.8f,1.4f)
            };
            float scale = OralMapGeometry.TongueRadii(stage.Map).x / 16.27f;
            for (int i = 0; i < layout.Length; i++)
            {
                var l = layout[i];
                var w = new OralWhirlpoolState { Radius = l.z * scale, Angle = Random.Range(0, Mathf.PI * 2) };
                w.Position = ClampVortex(OralMapGeometry.TonguePoint(stage.Map, new Vector2(l.x, l.y)), w.Radius);
                Turn(w); whirlpools.Add(w);
            }
        }

        Vector2 ClampVortex(Vector2 point, float radius)
        {
            for (int i = 0; i < 16; i++)
            {
                float d = OralMapGeometry.TongueDistance(stage.Map, point, out var inward);
                if (d >= radius + .1f) break;
                point += inward * (radius + .12f - d);
            }
            return point;
        }

        static void Turn(OralWhirlpoolState w)
        {
            float a = Random.Range(0, 8) * Mathf.PI / 4;
            w.Direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            w.TurnRemaining = Random.Range(1.2f, 3f);
        }

        void MoveWhirlpools(float dt)
        {
            foreach (var w in whirlpools)
            {
                w.TurnRemaining -= dt;
                if (w.TurnRemaining <= 0) Turn(w);
                Vector2 next = w.Position + w.Direction * VortexSpeed * dt;
                Vector2 bounded = ClampVortex(next, w.Radius);
                if ((bounded - next).sqrMagnitude > .00001f) { Turn(w); next = bounded; }
                w.Position = next;
                w.Angle += dt * (1.7f + .8f / w.Radius);
            }
        }

        void LateUpdate()
        {
            if (!Active || player == null || !player.IsAlive || Time.deltaTime <= 0 || transferring) return;
            if (sweeping) { TickSweep(); return; }
            if (!VorticesActive || level.IsRunFlowPaused || player.IsChoosingAugment || player.IsDashing ||
                pullGrace > 0 || stage.IsAirborne || StageEntryLoading.IsLoading) return;
            OralWhirlpoolState source = null; float deepest = 0;
            foreach (var w in whirlpools)
            {
                float penetration = 1 - Vector2.Distance(player.Position, w.Position) / w.Radius;
                if (penetration > deepest) { source = w; deepest = penetration; }
            }
            if (source == null) return;
            Vector2 target = Vector2.MoveTowards(player.Position, source.Position,
                (player.CurrentMoveSpeed + 1f) * Time.deltaTime * Mathf.Lerp(.8f, 1.6f, deepest));
            target = OralMapGeometry.Clamp(stage.Map, target, PlayerRadius, OralZone.Tongue);
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) { target = BloodClotObstacle.ClampDash(body, target); body.position = target; }
            player.transform.position = new Vector3(target.x, target.y, player.transform.position.z);
        }

        float PlayerRadius => player != null ? (player.GetComponent<StomachSlopeBody>()?.Radius ?? .25f) : .25f;

        public bool TryGetDashExit(Character actor, Vector2 direction, out Vector2 exit)
        {
            exit = actor != null ? actor.Position : Vector2.zero;
            if (!VorticesActive || actor != player || OutsideAll(exit, 0)) return false;
            dashOwner = actor; dashDirection = direction.sqrMagnitude > .01f ? direction.normalized : Vector2.down;
            exit = FindExit(actor.Position, dashDirection);
            return true;
        }

        public Vector2 ResolveDashExit(Character actor, Vector2 proposed)
        {
            if (actor != dashOwner || !VorticesActive) return proposed;
            if (OutsideAll(proposed, PlayerRadius + .15f) && SafeDashPoint(proposed)) return proposed;
            return FindExit(actor.Position, dashDirection);
        }

        public void NotifyDashFinished(Character actor)
        {
            if (actor != dashOwner) return;
            dashOwner = null; pullGrace = .15f;
        }

        bool OutsideAll(Vector2 p, float margin)
        {
            foreach (var w in whirlpools)
                if ((p - w.Position).sqrMagnitude < (w.Radius + margin) * (w.Radius + margin)) return false;
            return true;
        }

        bool SafeDashPoint(Vector2 p)
        {
            if (OralMapGeometry.TongueDistance(stage.Map, p, out _) < PlayerRadius + .03f) return false;
            var body = player.GetComponent<Rigidbody2D>();
            return body == null || (BloodClotObstacle.ClampDash(body, p) - p).sqrMagnitude < .0025f;
        }

        Vector2 FindExit(Vector2 origin, Vector2 direction)
        {
            float margin = PlayerRadius + .18f;
            float containingRadius = 0;
            foreach (var w in whirlpools)
                if (Vector2.Distance(origin, w.Position) <= w.Radius + margin)
                    containingRadius = Mathf.Max(containingRadius, w.Radius);
            Vector2 best = origin;
            float bestScore = float.PositiveInfinity;
            // Preserve the requested direction for an ordinary single-vortex exit. If a
            // long overlapping chain extends it across the map, use a nearby safe edge.
            // A second, fine search catches narrow gaps between persistent blood clots.
            for (int search = 0; search < 2; search++)
            {
                int rays = search == 0 ? 32 : 256;
                for (int attempt = 0; attempt < rays; attempt++)
                {
                    int sign = (attempt & 1) == 0 ? -1 : 1;
                    float angle = ((attempt + 1) / 2) * (360f / rays) * sign;
                    Vector2 dir = Quaternion.Euler(0, 0, angle) * direction;
                    float distance = 0;
                    for (int pass = 0; pass <= whirlpools.Count; pass++)
                    {
                        float previous = distance;
                        foreach (var w in whirlpools)
                        {
                            Vector2 delta = w.Position - origin;
                            float along = Vector2.Dot(delta, dir);
                            float perpendicular = delta.sqrMagnitude - along * along;
                            float r = w.Radius + margin;
                            if (perpendicular >= r * r) continue;
                            float half = Mathf.Sqrt(r * r - perpendicular);
                            float near = along - half, far = along + half;
                            if (far >= 0 && near <= distance + .02f) distance = Mathf.Max(distance, far + .025f);
                        }
                        if (Mathf.Approximately(previous, distance)) break;
                    }
                    Vector2 candidate = origin + dir * distance;
                    if (!OutsideAll(candidate, margin) || !SafeDashPoint(candidate)) continue;
                    if (search == 0 && attempt == 0 && distance <= containingRadius * 2 + margin * 2) return candidate;
                    float score = distance + (1 - Vector2.Dot(direction, dir)) * containingRadius * .35f;
                    if (score < bestScore) { bestScore = score; best = candidate; }
                }
                if (!float.IsPositiveInfinity(bestScore)) return best;
            }
            // Degenerate blocked directions: retain the closest safe position, never teleport through walls.
            return OralMapGeometry.Clamp(stage.Map, origin, PlayerRadius, OralZone.Tongue);
        }

        void BeginSweep()
        {
            sweeping = true; dashOwner = null;
            stage.ReturnToTongueForSwallow();
            OralStageRuntime.CancelPlayerTravel(player);
            priorPause = level.IsRunFlowPaused; ownsPause = true; level.SetRunFlowPaused(true);
            player.Skills?.Breath?.Cancel();
            foreach (var projectile in FindObjectsOfType<Projectile>()) projectile.RemoveForItem();
            foreach (var projectile in new List<OctoberEnemyProjectile>(OctoberEnemyProjectile.Active))
                if (projectile != null && projectile.IsHostile) projectile.Remove();
            sweepOrigin = player.Position;
            PlayerMiniStageStunRuntime.GetOrCreate(player).BeginTravelLock(this);
            playerVisual = player.GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            if (playerVisual != null) { playerVisualScale = playerVisual.transform.localScale; playerVisualRotation = playerVisual.transform.localRotation; }
            foreach (var monster in FindObjectsOfType<Monster>())
            {
                if (monster == null || !monster.gameObject.activeInHierarchy || monster.IsMiniStageOwned) continue;
                swept.Add(new SweptMonster { Monster = monster, Start = monster.Position, WasSuspended = monster.IsFieldRuntimeSuspended });
                monster.SetFieldRuntimeSuspended(true);
            }
        }

        void TickSweep()
        {
            float t = Swallow01;
            Vector2 end = OralMapGeometry.PixelToWorld(stage.Map, new Vector2(710, 1045));
            float eased = t * t;
            Vector2 position = Vector2.Lerp(sweepOrigin, end, eased);
            position.x += Mathf.Sin(t * Mathf.PI * 2) * (1 - t) * 1.2f;
            player.GetComponent<PlayerMiniStageStunRuntime>()?.MoveTravelLock(this, new Vector3(position.x, position.y, player.transform.position.z));
            if (playerVisual != null)
            {
                playerVisual.transform.localRotation = playerVisualRotation * Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI) * 28f);
                playerVisual.transform.localScale = playerVisualScale * Mathf.Lerp(1, .55f, Mathf.SmoothStep(0, 1, (t - .6f) / .4f));
            }
            foreach (var entry in swept)
            {
                var m = entry.Monster; if (m == null || !m.gameObject.activeInHierarchy) continue;
                Vector2 p = Vector2.Lerp(entry.Start, end, eased);
                p.x += Mathf.Sin(t * Mathf.PI * 2 + entry.Start.x) * (1 - t);
                m.transform.position = new Vector3(p.x, p.y, m.transform.position.z);
                var rb = m.GetComponent<Rigidbody2D>(); if (rb != null) rb.position = p;
            }
        }

        void BeginTransfer()
        {
            if (transferring) return;
            ReleaseSweep();
            if (StageProgression.Ensure(level).BeginOralToStomach()) transferring = true;
        }

        void ReleaseSweep()
        {
            if (player != null) player.GetComponent<PlayerMiniStageStunRuntime>()?.EndTravelLock(this);
            if (playerVisual != null) { playerVisual.transform.localScale = playerVisualScale; playerVisual.transform.localRotation = playerVisualRotation; }
            playerVisual = null;
            foreach (var entry in swept)
                if (entry.Monster != null && !entry.WasSuspended) entry.Monster.SetFieldRuntimeSuspended(false);
            swept.Clear();
            if (ownsPause && level != null) level.SetRunFlowPaused(priorPause);
            ownsPause = false;
        }

        public void CancelForTransition()
        {
            ReleaseSweep(); Active = Pending = sweeping = false; dashOwner = null; pullGrace = 0;
            whirlpools.Clear();
        }
        void OnDisable() { CancelForTransition(); }
        void OnDestroy() { CancelForTransition(); if (Instance == this) Instance = null; }
    }
}
