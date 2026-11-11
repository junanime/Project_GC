using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public sealed class OralWhirlpoolState
    {
        public Vector2 Position;
        public float Radius, Angle;
        public float CollisionRadius => Radius * .5f;
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
        public bool SuppressMonsterSpawns => Active && age >= SurvivalTime;
        public bool IsPlayerCaptured => playerCapture != null && VorticesActive && player != null && !player.IsDashing;
        public bool PlayerAtCenter => IsPlayerCaptured && playerCapture.AtCenter;
        public int PlayerCoreDamageTicks { get; private set; }
        public int SwallowedMonsters { get; private set; }
        public int CapturedMonsterCount => monsterCaptures.Count;
        public bool CapturesPlayer(Character actor) => actor == player && IsPlayerCaptured;
        public IReadOnlyList<OralWhirlpoolState> Whirlpools => whirlpools;
        public int PairBounces { get; private set; }
        public int BoundaryBounces { get; private set; }

        readonly List<OralWhirlpoolState> whirlpools = new List<OralWhirlpoolState>();
        readonly List<SweptMonster> swept = new List<SweptMonster>();
        readonly Dictionary<Monster, SpiralCapture> monsterCaptures = new Dictionary<Monster, SpiralCapture>();
        readonly List<Monster> monsterScratch = new List<Monster>();
        readonly List<Monster> releasedMonsters = new List<Monster>();
        SpiralCapture playerCapture;
        const float SpiralSeconds = 2.6f, SpiralAngularSpeed = 3.2f, CoreRadius = .16f;
        sealed class SpiralCapture
        {
            public OralWhirlpoolState Source;
            public float Radius, Angle, DamageClock;
            public bool AtCenter, WasSuspended;
            public uint Generation;
        }
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
            PairBounces = BoundaryBounces = 0;
            PlayerCoreDamageTicks = SwallowedMonsters = 0;
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
            if (Time.timeScale <= 0 || Time.deltaTime <= 0 || player.IsChoosingAugment || player.IsPortalTravelling ||
                StageEntryLoading.IsLoading || MiniStageRuntimeState.IsInsideMiniStage ||
                (level.IsRunFlowPaused && !ownsPause)) return;
            float dt = Time.deltaTime;
            age += dt;
            pullGrace = Mathf.Max(0, pullGrace - dt);
            if (age >= 16f && !sweeping) stage.ReturnToTongueForSwallow();
            if (age < SweepTime)
            {
                spawnClock -= dt;
                if (age < SurvivalTime && spawnClock <= 0)
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
                // Keep the approved interior placement; new discs fill the unused regions.
                w.Position = ClampVortex(OralMapGeometry.TonguePoint(stage.Map, new Vector2(l.x, l.y)), w.Radius, 1f);
                Turn(w); whirlpools.Add(w);
            }
            // 21 original discs + 26 additional discs: 18 around the rim and 8 in broad gaps.
            // Coordinates are offsets from the tongue center, at its authored map scale.
            Vector3[] additions = {
                new Vector3(-14.235f,2.977f,2.3f), new Vector3(-12.095f,5.474f,2.4f),
                new Vector3(-9.055f,7.456f,2.3f), new Vector3(-4.353f,8.941f,2.35f),
                new Vector3(1.421f,9.382f,2.2f), new Vector3(7.019f,8.225f,2.4f),
                new Vector3(10.975f,6.323f,2.4f), new Vector3(14.127f,3.117f,2.35f),
                new Vector3(-14.979f,-.197f,2.4f), new Vector3(-14.879f,-2.867f,2.6f),
                new Vector3(-15.029f,-5.783f,2.3f), new Vector3(-14.879f,-8.256f,2.6f),
                new Vector3(-5.716f,-8.584f,2.4f), new Vector3(.899f,-8.585f,2.4f),
                new Vector3(6.610f,-8.534f,2.5f), new Vector3(14.904f,-8.509f,2.55f),
                new Vector3(14.929f,-5.319f,2.5f), new Vector3(15.004f,-1.194f,2.35f),
                new Vector3(-2.513f,-5.119f,1.8f), new Vector3(5.287f,3.581f,2.1f),
                new Vector3(-4.913f,-2.719f,2.5f), new Vector3(-9.713f,-3.319f,1.95f),
                new Vector3(8.587f,-3.619f,2.3f), new Vector3(3.390f,6.256f,2.6f),
                new Vector3(10.087f,-8.719f,1.8f), new Vector3(3.787f,.881f,2.1f)
            };
            Vector2 center = OralMapGeometry.TongueCenter(stage.Map);
            float additionScale = OralMapGeometry.TongueRadii(stage.Map).x / 16.263145f;
            foreach (var l in additions)
            {
                var w = new OralWhirlpoolState { Radius = l.z * additionScale, Angle = Random.Range(0, Mathf.PI * 2) };
                w.Position = ClampVortex(center + new Vector2(l.x, l.y) * additionScale, w.Radius);
                Turn(w); whirlpools.Add(w);
            }
            // Settle the half-radius cores before the warning becomes visible.
            for (int pass = 0; pass < 32; pass++) SeparateCores(false);
        }

        Vector2 ClampVortex(Vector2 point, float radius, float footprintRatio = .5f)
        {
            for (int i = 0; i < 16; i++)
            {
                float d = OralMapGeometry.TongueDistance(stage.Map, point, out var inward);
                float clearance = radius * footprintRatio + .1f;
                if (d >= clearance) break;
                point += inward * (clearance + .02f - d);
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
            // Substeps prevent a long frame from tunnelling through another core.
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 30f)));
            float step = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                foreach (var w in whirlpools)
                {
                    w.TurnRemaining -= step;
                    if (w.TurnRemaining <= 0) Turn(w);
                    w.Position += w.Direction * VortexSpeed * step;
                    BounceAtBoundary(w, true);
                    w.Angle += step * (1.7f + .8f / w.Radius);
                }
                for (int pass = 0; pass < 6; pass++) SeparateCores(true);
            }
        }

        static Vector2 EightDirection(Vector2 direction)
        {
            float angle = Mathf.Round(Mathf.Atan2(direction.y, direction.x) / (Mathf.PI / 4)) * (Mathf.PI / 4);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        static bool ReflectOutward(OralWhirlpoolState w, Vector2 outward)
        {
            float approach = Vector2.Dot(w.Direction, outward);
            if (approach >= -.001f) return false;
            Vector2 reflected = EightDirection(w.Direction - 2 * approach * outward);
            // Quantization must never turn the reflected velocity back into the contact.
            if (Vector2.Dot(reflected, outward) < .05f) reflected = EightDirection(outward);
            w.Direction = reflected;
            w.TurnRemaining = Mathf.Max(w.TurnRemaining, .65f);
            return true;
        }

        void BounceAtBoundary(OralWhirlpoolState w, bool animate)
        {
            float clearance = w.CollisionRadius + .1f;
            float d = OralMapGeometry.TongueDistance(stage.Map, w.Position, out var inward);
            if (d >= clearance) return;
            w.Position = ClampVortex(w.Position, w.Radius);
            if (animate && ReflectOutward(w, inward)) BoundaryBounces++;
        }

        void SeparateCores(bool animate)
        {
            for (int i = 0; i < whirlpools.Count; i++)
            {
                var a = whirlpools[i];
                for (int j = i + 1; j < whirlpools.Count; j++)
                {
                    var b = whirlpools[j];
                    Vector2 delta = b.Position - a.Position;
                    float contact = a.CollisionRadius + b.CollisionRadius;
                    if (delta.sqrMagnitude >= contact * contact) continue;
                    float distance = delta.magnitude;
                    Vector2 normal = distance > .0001f ? delta / distance : EightDirection(new Vector2(i + 1, j + 1));
                    Vector2 separation = normal * ((contact - distance) * .5f + .0005f);
                    a.Position -= separation; b.Position += separation;
                    // Corrections from a third disc can overlap a pair that is already
                    // separating. Reflect only an approaching pair to avoid contact jitter.
                    if (animate && Vector2.Dot(b.Direction - a.Direction, normal) < -.001f)
                    {
                        bool first = ReflectOutward(a, -normal);
                        bool second = ReflectOutward(b, normal);
                        if (first || second) PairBounces++;
                    }
                    BounceAtBoundary(a, animate); BounceAtBoundary(b, animate);
                }
            }
        }

        void LateUpdate()
        {
            if (!Active || player == null || !player.IsAlive || Time.timeScale <= 0 || Time.deltaTime <= 0 || transferring) return;
            if (sweeping) { TickSweep(); return; }
            if (!VorticesActive || level.IsRunFlowPaused || player.IsChoosingAugment ||
                StageEntryLoading.IsLoading || MiniStageRuntimeState.IsInsideMiniStage) return;
            TickMonsterCaptures(Time.deltaTime);
            if (player.IsDashing || pullGrace > 0 || stage.IsAirborne) return;
            if (playerCapture == null)
            {
                var source = FindContainingVortex(player.Position);
                if (source == null) return;
                playerCapture = Capture(source, player.Position);
                player.StartIdleAnimation();
            }
            var capture = playerCapture;
            Vector2 target = SpiralPosition(capture, Time.deltaTime);
            target = OralMapGeometry.Clamp(stage.Map, target, PlayerRadius, OralZone.Tongue);
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                target = BloodClotObstacle.ClampDash(body, target);
                body.velocity = Vector2.zero; body.position = target;
            }
            player.transform.position = new Vector3(target.x, target.y, player.transform.position.z);
            UpdatePolar(capture, target);
            bool centered = Vector2.Distance(target, capture.Source.Position) <= CoreRadius;
            // Moving centers can briefly meet an obstacle. Re-contact within the same
            // capture must never reset the pulse cooldown and deal rapid duplicate hits.
            capture.DamageClock = Mathf.Max(0, capture.DamageClock - Time.deltaTime);
            capture.AtCenter = centered;
            if (!centered) return;
            capture.Radius = 0;
            if (capture.DamageClock <= 0)
            {
                capture.DamageClock = 1.5f;
                if (player.TakeWhirlpoolCoreDamage()) PlayerCoreDamageTicks++;
            }
        }

        OralWhirlpoolState FindContainingVortex(Vector2 position)
        {
            OralWhirlpoolState source = null; float deepest = 0;
            foreach (var w in whirlpools)
            {
                float penetration = 1 - Vector2.Distance(position, w.Position) / w.Radius;
                if (penetration > deepest) { source = w; deepest = penetration; }
            }
            return source;
        }

        static SpiralCapture Capture(OralWhirlpoolState source, Vector2 position)
        {
            var capture = new SpiralCapture { Source = source };
            UpdatePolar(capture, position);
            return capture;
        }

        static void UpdatePolar(SpiralCapture capture, Vector2 position)
        {
            var offset = position - capture.Source.Position;
            capture.Radius = offset.magnitude;
            if (offset.sqrMagnitude > .000001f) capture.Angle = Mathf.Atan2(offset.y, offset.x);
        }

        static Vector2 SpiralPosition(SpiralCapture capture, float dt)
        {
            capture.Radius = Mathf.Max(0, capture.Radius - capture.Source.Radius / SpiralSeconds * dt);
            // Positive texture sampling rotation looks clockwise, so orbit clockwise too.
            capture.Angle -= SpiralAngularSpeed * dt;
            return capture.Source.Position + new Vector2(Mathf.Cos(capture.Angle), Mathf.Sin(capture.Angle)) * capture.Radius;
        }

        void TickMonsterCaptures(float dt)
        {
            monsterScratch.Clear();
            foreach (var monster in level.EntityManager.LivingMonsters) monsterScratch.Add(monster);
            foreach (var monster in monsterScratch)
            {
                if (monster == null || !monster.IsAlive || !monster.gameObject.activeInHierarchy ||
                    monster.IsMiniStageOwned || monster is BossMonster || monster is BloodClotMonster ||
                    monster.IsFieldRuntimeSuspended || monsterCaptures.ContainsKey(monster)) continue;
                var source = FindContainingVortex(monster.Position);
                if (source == null) continue;
                var capture = Capture(source, monster.Position);
                capture.WasSuspended = monster.IsFieldRuntimeSuspended;
                capture.Generation = monster.SpawnGeneration;
                monsterCaptures.Add(monster, capture);
                monster.SetFieldRuntimeSuspended(true);
            }
            releasedMonsters.Clear();
            foreach (var entry in monsterCaptures)
            {
                var monster = entry.Key; var capture = entry.Value;
                if (monster == null || monster.SpawnGeneration != capture.Generation || !monster.IsAlive || !monster.gameObject.activeInHierarchy)
                { releasedMonsters.Add(monster); continue; }
                var target = SpiralPosition(capture, dt);
                target = OralMapGeometry.Clamp(stage.Map, target, .2f, OralZone.Tongue);
                monster.transform.position = new Vector3(target.x, target.y, monster.transform.position.z);
                var body = monster.GetComponent<Rigidbody2D>(); if (body != null) body.position = target;
                level.EntityManager.Grid.UpdateClient(monster);
                UpdatePolar(capture, target);
                if (capture.Radius > CoreRadius) continue;
                // Environmental death bypasses needle shields and does not trigger player kill attacks/splits.
                monster.SwallowedByVortex();
                SwallowedMonsters++;
                releasedMonsters.Add(monster);
            }
            foreach (var monster in releasedMonsters) monsterCaptures.Remove(monster);
        }

        void ReleaseCaptures()
        {
            playerCapture = null;
            foreach (var entry in monsterCaptures)
            {
                var monster = entry.Key;
                if (monster != null && monster.SpawnGeneration == entry.Value.Generation && monster.IsAlive &&
                    monster.gameObject.activeInHierarchy && !entry.Value.WasSuspended)
                    monster.SetFieldRuntimeSuspended(false);
            }
            monsterCaptures.Clear(); monsterScratch.Clear(); releasedMonsters.Clear();
        }

        float PlayerRadius => player != null ? (player.GetComponent<StomachSlopeBody>()?.Radius ?? .25f) : .25f;

        public bool TryGetDashExit(Character actor, Vector2 direction, out Vector2 exit)
        {
            exit = actor != null ? actor.Position : Vector2.zero;
            if (!VorticesActive || actor != player || (playerCapture == null && OutsideAll(exit, 0))) return false;
            dashOwner = actor; dashDirection = direction.sqrMagnitude > .01f ? direction.normalized : Vector2.down;
            exit = FindExit(actor.Position, dashDirection);
            playerCapture = null;
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
            dashOwner = null; playerCapture = null; pullGrace = .15f;
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
            ReleaseCaptures();
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
            ReleaseCaptures();
            ReleaseSweep(); Active = Pending = sweeping = false; dashOwner = null; pullGrace = 0;
            whirlpools.Clear();
        }
        void OnDisable() { CancelForTransition(); }
        void OnDestroy() { CancelForTransition(); if (Instance == this) Instance = null; }
    }
}
