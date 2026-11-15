using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Shared by all five guards: a volley or one monster touching two members is
    // still one hit. This state has no dependency on the convoy's selected art.
    public sealed class OralGuardHealth
    {
        public const float Maximum = 80f, HitInterval = .65f;
        public float Current { get; private set; } = Maximum;
        public float HitFlash { get; private set; }
        public bool Failed { get; private set; }
        public bool Completed { get; private set; }
        float cooldown;

        public void Advance(float dt)
        {
            cooldown = Mathf.Max(0, cooldown - Mathf.Max(0, dt));
            HitFlash = Mathf.Max(0, HitFlash - Mathf.Max(0, dt));
        }
        public bool Damage(float amount)
        {
            if (Failed || Completed || cooldown > 0 || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0) return false;
            Current = Mathf.Max(0, Current - amount);
            cooldown = HitInterval; HitFlash = .22f;
            if (Current <= 0) Failed = true;
            return true;
        }
        public bool Complete()
        {
            if (Failed || Completed) return false;
            Completed = true; return true;
        }
        public void Fail() { if (!Completed) Failed = true; }
    }

    public readonly struct OralGuardRoute
    {
        public readonly int Tooth;
        public readonly Vector2 Launch, Landing;
        public OralGuardRoute(int tooth, Vector2 launch, Vector2 landing)
        { Tooth = tooth; Launch = launch; Landing = landing; }

        public static bool TryCreate(FiniteStomachMap map, int tooth, out OralGuardRoute route)
        {
            route = default;
            if (map == null || !OralMapGeometry.CanJumpToTooth(tooth)) return false;
            var landing = OralMapGeometry.ToothWalkCenter(map, tooth);
            var launch = OralMapGeometry.Clamp(map, landing, .65f, OralZone.Tongue);
            // Use the same permitted tongue edge and molar surface as player jumps.
            if (!OralMapGeometry.TryJump(map, launch, landing - launch, out var checkedLanding) ||
                OralMapGeometry.NearestTooth(map, checkedLanding) != tooth) return false;
            route = new OralGuardRoute(tooth, launch, landing); return true;
        }
        public static bool SegmentOnTongue(FiniteStomachMap map, Vector2 from, Vector2 to, float clearance = .28f)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / .15f));
            for (int i = 0; i <= samples; i++)
                if (OralMapGeometry.TongueDistance(map, Vector2.Lerp(from, to, i / (float)samples), out _) < clearance) return false;
            return true;
        }
    }

    // AmbientContent owns pause/lifetime and reward delivery. This actor owns
    // escort movement, member hitboxes and combat, independent of the sprite.
    public sealed class OralGuardEscort : MonoBehaviour
    {
        public const int MemberCount = 5;
        public const float MoveSpeed = 1.2f, JumpDuration = .8f, Radius = .28f;
        public static OralGuardEscort Active { get; private set; }
        public OralGuardHealth Health { get; private set; }
        public OralGuardRoute Route { get; private set; }
        public bool Suspended { get; private set; }
        public bool Complete => Health != null && Health.Completed;
        public bool Failed => Health != null && Health.Failed;
        public float Age { get; private set; }
        public float Lifetime { get; private set; }
        public int ArrivedCount { get; private set; }
        public int HitCount { get; private set; }
        public Vector2 Position => members.Count > 0 ? members[0].Ground : Route.Launch;
        public Vector3 HealthAnchor { get; private set; }
        public bool CanBeAttacked => isActiveAndEnabled && !Suspended && !Complete && !Failed &&
            !OralEventArt.Paused(level) && !(OralFieldEvents.Instance?.OnCeiling ?? false);

        sealed class Member
        {
            public OralGuardMember hit;
            public SpriteRenderer body, shadow;
            public Vector2 Start, Ground;
            public float WalkDistance;
            public bool Walking, Arrived;
        }
        struct Contact { public uint Generation; public float Next; }
        readonly List<Member> members = new List<Member>(MemberCount);
        readonly List<Monster> monsters = new List<Monster>();
        readonly Dictionary<Monster, Contact> contacts = new Dictionary<Monster, Contact>();
        LevelManager level; FiniteStomachMap map;
        SpriteRenderer launchMarker, landingMarker;
        LineRenderer jumpGuide;
        OralGuardHealthBar bar;
        float height, spacing, travelled, doneAge, nextContactScan;
        Sprite[] frames;
        Vector2 heading;

        public bool Configure(LevelManager owner, FiniteStomachMap field, Vector2 near, float playerHeight, Sprite sprite)
        {
            if (owner == null || field == null || sprite == null) return false;
            var routes = new List<OralGuardRoute>();
            for (int tooth = 0; tooth < OralMapGeometry.ToothCount; tooth++)
                if (OralGuardRoute.TryCreate(field, tooth, out var candidate)) routes.Add(candidate);
            if (routes.Count == 0) return false;
            Route = routes[Random.Range(0, routes.Count)];
            level = owner; map = field; Health = new OralGuardHealth(); Active = this;
            frames = OralGuardArt.Frames;
            height = Mathf.Clamp(playerHeight * .72f, .25f, 1.1f);
            spacing = Mathf.Max(.52f, height * .85f);
            var back = OralMapGeometry.Clamp(map, near + new Vector2(1.3f, .6f), .75f, OralZone.Tongue);
            float length = spacing * (MemberCount - 1);
            if (Vector2.Distance(back, Route.Launch) < length + 5f)
                back = OralMapGeometry.Clamp(map, OralMapGeometry.TongueCenter(map) +
                    (OralMapGeometry.TongueCenter(map) - Route.Launch).normalized * 3f, .75f, OralZone.Tongue);
            heading = (Route.Launch - back).normalized;
            var front = Vector2.MoveTowards(back, Route.Launch, length);
            // The tongue is a convex walkable surface; a validated segment is its
            // shortest safe route. Do not follow the player or cross a chasm.
            if (!OralGuardRoute.SegmentOnTongue(map, back, Route.Launch)) return false;
            Lifetime = Mathf.Max(OralAmbientContent.GuardLifetime,
                Vector2.Distance(back, Route.Launch) / MoveSpeed + JumpDuration + 12f);
            for (int i = 0; i < MemberCount; i++)
            {
                var go = new GameObject("민트 수호대 " + (i + 1)); go.transform.SetParent(transform, false);
                var hit = go.AddComponent<OralGuardMember>();
                hit.Configure(this, i, height);
                var body = OralEventArt.Sprite(go.transform, "대원", sprite, 24);
                body.transform.localScale = Vector3.one * (height / sprite.bounds.size.y);
                if (OralGuardArt.Material != null) body.sharedMaterial = OralGuardArt.Material;
                var shadow = OralEventArt.Sprite(transform, "대원 발밑", OralEventArt.Disc, 7);
                GroundVisualSorting.Apply(shadow, 7); shadow.transform.localScale = new Vector3(height * .8f, height * .23f, 1);
                var start = front - heading * spacing * i;
                members.Add(new Member { hit = hit, body = body, shadow = shadow, Start = start, Ground = start,
                    WalkDistance = Vector2.Distance(start, Route.Launch), Walking = true });
            }
            launchMarker = Marker("수호대 혀 점프 위치", Route.Launch, .8f);
            landingMarker = Marker("수호대 목표 어금니", Route.Landing, 1.1f);
            jumpGuide = OralEventArt.Line(transform, "수호대 도약 안내", new Color(.45f, 1, .8f, .45f), .035f, 10, true);
            jumpGuide.positionCount = 16;
            for (int i = 0; i < 16; i++) { float t = i / 15f; jumpGuide.SetPosition(i, Vector2.Lerp(Route.Launch, Route.Landing, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * .8f); }
            bar = gameObject.AddComponent<OralGuardHealthBar>(); bar.Bind(this, owner.PlayerCharacter);
            Pose(); return true;
        }
        SpriteRenderer Marker(string name, Vector2 point, float size)
        {
            var r = OralEventArt.Sprite(transform, name, OralEventArt.Disc, 10);
            GroundVisualSorting.Apply(r, 10); r.transform.position = point;
            r.transform.localScale = new Vector3(size, size * .5f, 1); r.color = new Color(.45f, 1, .8f, .36f); return r;
        }
        public void SetSuspended(bool suspended, bool hidden)
        {
            Suspended = suspended;
            foreach (var member in members) member.hit.SetEnabled(!suspended && member.Walking && !Failed && !Complete);
            foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.forceRenderingOff = hidden;
            if (bar != null) bar.SetVisible(!hidden && !suspended);
        }
        public void Tick(float dt)
        {
            if (Health == null || Suspended || dt <= 0) return;
            // Match movement and combat clocks when the editor or OS stalls.
            dt = Mathf.Min(dt, .25f); Age += dt; Health.Advance(dt);
            if (Complete || Failed) { doneAge += dt; Pose(); return; }
            if (Age >= Lifetime) { Health.Fail(); Pose(); return; }
            travelled += MoveSpeed * dt;
            ArrivedCount = 0;
            foreach (var member in members)
            {
                float afterLaunch = (travelled - member.WalkDistance) / MoveSpeed;
                member.Walking = afterLaunch < 0;
                member.Arrived = afterLaunch >= JumpDuration;
                member.Ground = member.Walking ? Vector2.MoveTowards(member.Start, Route.Launch, travelled)
                    : Vector2.Lerp(Route.Launch, Route.Landing, Mathf.SmoothStep(0, 1, afterLaunch / JumpDuration));
                member.hit.SetEnabled(member.Walking);
                if (member.Arrived) ArrivedCount++;
            }
            // Tail members remain vulnerable while the leader is already safe.
            if (Age >= nextContactScan) { nextContactScan = Age + .08f; CheckMonsterContact(); }
            if (!Failed && ArrivedCount == MemberCount) Health.Complete();
            Pose();
        }
        public bool ReadyToRemove => (Complete && doneAge >= 1.3f) || (Failed && doneAge >= 1f);
        public Vector2 MemberPosition(int index) => members[index].Ground;
        public bool MemberWalking(int index) => index >= 0 && index < members.Count && members[index].Walking && CanBeAttacked;
        public bool TakeMemberDamage(int index, float damage)
        {
            if (!MemberWalking(index) || !Health.Damage(damage)) return false;
            HitCount++; GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.MonsterHit);
            if (Failed) foreach (var m in members) m.hit.SetEnabled(false);
            return true;
        }
        static bool Eligible(Monster m) => m != null && m.gameObject.activeInHierarchy && m.IsAlive && m.HP > 0 &&
            !m.IsMiniStageOwned && !m.IsFieldRuntimeSuspended && m is MeleeMonster && !(m is BloodClotMonster) && !(m is TrapMonster) &&
            (CombatStatusRules.ActiveTags(m) & (CombatStatusTag.Freeze | CombatStatusTag.Stun)) == 0;
        void CheckMonsterContact()
        {
            if (!CanBeAttacked || level.EntityManager == null) return;
            monsters.Clear(); foreach (var m in level.EntityManager.LivingMonsters) monsters.Add(m);
            foreach (var m in monsters)
            {
                if (!Eligible(m) || m.Blueprint == null || m.Blueprint.atk <= 0 || OralMapGeometry.ZoneAt(map, m.Position) != OralZone.Tongue) continue;
                if (contacts.TryGetValue(m, out var prior) && prior.Generation == m.SpawnGeneration && Age < prior.Next) continue;
                float radius = Radius + Mathf.Clamp(m.Size.x * .5f, .12f, .8f);
                for (int i = 0; i < members.Count; i++)
                {
                    if (!members[i].Walking || (members[i].Ground - m.Position).sqrMagnitude > radius * radius ||
                        !OralGuardRoute.SegmentOnTongue(map, m.Position, members[i].Ground, 0)) continue;
                    if (TakeMemberDamage(i, m.Blueprint.atk)) contacts[m] = new Contact { Generation = m.SpawnGeneration,
                        Next = Age + Mathf.Max(OralGuardHealth.HitInterval, m.Blueprint.atkspeed > 0 ? 1f / m.Blueprint.atkspeed : 1f) };
                    break;
                }
                if (Failed) break;
            }
        }
        public static bool TryTarget(Monster enemy, Vector2 playerPosition, out Vector2 target)
        {
            target = playerPosition; var escort = Active;
            if (escort == null || !escort.CanBeAttacked || !Eligible(enemy) || OralMapGeometry.ZoneAt(escort.map, enemy.Position) != OralZone.Tongue) return false;
            float closest = Mathf.Min(7f, Vector2.Distance(enemy.Position, playerPosition) + 2f);
            bool found = false;
            foreach (var member in escort.members)
            {
                if (!member.Walking) continue;
                float distance = Vector2.Distance(enemy.Position, member.Ground);
                if (distance >= closest) continue;
                closest = distance; target = member.Ground; found = true;
            }
            return found;
        }
        void Pose()
        {
            if (Health == null) return;
            float fade = Complete ? 1 - Mathf.Clamp01((doneAge - .5f) / .8f) : Failed ? 1 - Mathf.Clamp01(doneAge) : Mathf.Clamp01(Age / .25f);
            Vector3 barPoint = Vector3.zero; int grounded = 0;
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i]; float jumpT = Mathf.Clamp01((travelled - m.WalkDistance) / (MoveSpeed * JumpDuration));
                float jump = !m.Walking && !m.Arrived ? Mathf.Sin(jumpT * Mathf.PI) * 1.8f : 0;
                float bob = m.Walking && !Failed ? Mathf.Abs(Mathf.Sin(Age * 10 - i * .75f)) * height * .075f : 0;
                m.hit.transform.position = m.Ground;
                m.body.transform.localPosition = Vector3.up * (jump + bob);
                if (frames != null && frames.Length > 0) m.body.sprite = frames[m.Walking && !Failed ? (Mathf.FloorToInt(Age * 10) + i) % frames.Length : 0];
                m.body.transform.localRotation = Quaternion.Euler(0, 0, Failed ? Mathf.Sin(doneAge * 12 + i) * 12 : m.Walking ? Mathf.Sin(Age * 10 - i * .75f) * 3 : 0);
                m.body.flipX = heading.x > 0;
                float arrivedAge = (travelled - m.WalkDistance) / MoveSpeed - JumpDuration;
                float memberFade = fade * (m.Arrived ? 1 - Mathf.Clamp01(arrivedAge / .6f) : 1);
                m.body.color = Health.HitFlash > 0 ? new Color(1, .45f, .45f, memberFade) : new Color(1, 1, 1, memberFade);
                m.shadow.transform.position = m.Ground; m.shadow.color = new Color(.18f, .035f, .07f, .25f * memberFade * (1 - jumpT * (1 - jumpT)));
                if (m.Walking) { barPoint += (Vector3)m.Ground; grounded++; }
                if (!m.Walking || Failed || Complete) m.hit.SetEnabled(false);
            }
            HealthAnchor = (grounded > 0 ? barPoint / grounded : (Vector3)Route.Landing) + Vector3.up * (height + .16f);
            if (launchMarker != null) launchMarker.color = new Color(.45f, 1, .8f, .32f * fade);
            if (landingMarker != null) landingMarker.color = new Color(.45f, 1, .8f, (.25f + .1f * Mathf.Sin(Age * 3)) * fade);
            if (jumpGuide != null) jumpGuide.enabled = !Failed && !Complete;
        }
        void OnDisable()
        {
            if (Active == this) Active = null;
            foreach (var m in members) if (m.hit != null) m.hit.SetEnabled(false);
        }
    }
}
