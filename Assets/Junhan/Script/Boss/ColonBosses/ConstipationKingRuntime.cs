using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // A stage-final actor, deliberately separate from the six optional habitat encounters.
    public sealed partial class ConstipationKingRuntime : IDamageable
    {
        public const float Size = 4.8f, MaximumHealth = 34000f, BodyRadius = 1.3f;
        public const int PatternCount = 10;
        public static readonly List<ConstipationKingRuntime> Active = new List<ConstipationKingRuntime>();
        public Character Player { get; private set; }
        public LevelManager Level => level;
        public ConstipationKingVisual Visual { get; private set; }
        public Transform Effects { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth => MaximumHealth;
        public bool Dead { get; private set; }
        public bool Arriving { get; private set; }
        public bool Transitioning { get; private set; }
        public bool PhaseTwo { get; private set; }
        public bool Moving { get; private set; }
        public bool Busy => action != null;
        public bool AutoPatterns = true;
        public int CurrentPattern { get; private set; } = -1;
        public int CompletedPatterns { get; private set; }
        public int ProjectilesFired { get; private set; }
        public int ReflectedProjectiles { get; private set; }
        internal void RegisterReflection() { ReflectedProjectiles++; }
        public Vector2 HitCenter => hitbox != null ? (Vector2)transform.TransformPoint(hitbox.offset) : (Vector2)transform.position;
        public Vector2 Aim => AimFrom(HitCenter);
        public bool EncounterPaused => Time.timeScale <= 0 || MiniStageRuntimeState.IsInsideMiniStage || StageEntryLoading.IsLoading ||
            Player == null || !Player.IsAlive || level == null || level.IsLevelEnded || level.IsRunFlowPaused;
        public bool Immobilized => GetComponent<NeuralBlockedMonsterStatus>()?.Active == true;
        public bool Targetable => !Dead && !Arriving && !Transitioning && !EncounterPaused;
        public static readonly Color WarningColor = new Color(1f, .69f, .24f, .8f);
        LevelManager level;
        ConstipationKingDirector director;
        Rigidbody2D body;
        CapsuleCollider2D hitbox;
        Coroutine action;
        int sequence;
        float nextAction = 1.6f, nextHit;

        public static ConstipationKingRuntime Spawn(LevelManager level, Vector2 at, ConstipationKingDirector director = null)
        {
            if (level == null || level.PlayerCharacter == null) return null;
            var go = new GameObject("Stage 2 final boss · 변비왕");
            go.layer = LayerMask.NameToLayer("Monster Full");
            go.transform.position = FiniteStomachMap.SafeSpawn(at, BodyRadius + .2f);
            var boss = go.AddComponent<ConstipationKingRuntime>();
            boss.level = level; boss.director = director; boss.Player = level.PlayerCharacter; boss.Health = MaximumHealth;
            boss.body = go.AddComponent<Rigidbody2D>(); boss.body.gravityScale = 0; boss.body.bodyType = RigidbodyType2D.Kinematic;
            boss.body.constraints = RigidbodyConstraints2D.FreezeRotation;
            boss.hitbox = go.AddComponent<CapsuleCollider2D>(); boss.hitbox.isTrigger = true;
            boss.hitbox.direction = CapsuleDirection2D.Vertical; boss.hitbox.size = new Vector2(2.35f, 3.5f); boss.hitbox.offset = Vector2.up * 2.3f;
            boss.Effects = new GameObject("변비왕 · owned encounter effects").transform;
            boss.Visual = ConstipationKingVisual.Create(go.transform, Size);
            go.AddComponent<MapMarker>().Configure(MapMarkerKind.Boss, "변비왕 · 필수 최종보스", true);
            go.AddComponent<ConstipationKingHealthHud>().Initialize(boss);
            Active.Add(boss); boss.Arriving = true; boss.hitbox.enabled = false;
            boss.action = boss.StartCoroutine(boss.Arrival());
            return boss;
        }

        IEnumerator Arrival()
        {
            Visual.SetPose(KingPose.Idle); Visual.Opacity = 0;
            yield return Tween(1.6f, q => { Visual.Opacity = q; Visual.SetPoseProgress(q); });
            Arriving = false; hitbox.enabled = true; action = null;
        }

        void Update()
        {
            Moving = false;
            if (Dead) return;
            if (level == null || level.IsLevelEnded || Player == null || !Player.IsAlive)
            { CancelPattern(); return; }
            if (EncounterPaused || Immobilized) return;
            Visual.Face(Aim); Visual.Tick(Time.deltaTime);
            if (Arriving || Transitioning || Busy) return;
            nextAction -= Time.deltaTime;
            float distance = Vector2.Distance(transform.position, Player.transform.position);
            if (distance > 7f)
            {
                var status = GetComponent<FoodBossCombatStatus>();
                float slow = Mathf.Min(status?.SpeedMultiplier ?? 1, GetComponent<HoneySlowStatus>()?.SpeedMultiplier ?? 1) * (GetComponent<IceChillStatus>()?.SpeedMultiplier ?? 1);
                MoveTo((Vector2)transform.position + ColonBossNavigation.Toward(transform.position, Player.transform.position) * (PhaseTwo ? 1.12f : .95f) * slow * Time.deltaTime);
            }
            Visual.SetMoving(Moving);
            if (!AutoPatterns || nextAction > 0 || distance > 17) return;
            int index = sequence++ % PatternCount;
            if (!PhaseTwo && index == 1 || PhaseTwo && index == 7) index = sequence++ % PatternCount;
            PlayPattern(index);
        }

        public Vector2 AimFrom(Vector2 from)
        {
            var d = Player == null ? Vector2.left : (Vector2)Player.transform.position - from;
            return d.sqrMagnitude < .001f ? Vector2.left : d.normalized;
        }
        public bool PlayPattern(int index)
        {
            if (!Targetable || Immobilized || Busy || index < 0 || index >= PatternCount || index == 1 && !PhaseTwo || index == 7 && PhaseTwo) return false;
            CurrentPattern = index; Visual.Face(Aim); Visual.SetMoving(false); action = StartCoroutine(Perform(index)); return true;
        }
        IEnumerator Perform(int index)
        {
            yield return Pattern(index);
            if (Dead || Transitioning) yield break;
            Visual.ResetPose(); CompletedPatterns++; CurrentPattern = -1; action = null; nextAction = PhaseTwo ? 1.35f : 1.9f;
        }
        public void CancelPattern()
        {
            StopAllCoroutines(); action = null; CurrentPattern = -1; Moving = false;
            ClearEffects(); if (Visual != null) { Visual.ResetPose(); Visual.SetMoving(false); }
        }
        public void MoveTo(Vector2 target)
        {
            Vector2 before = transform.position;
            var map = FiniteStomachMap.Instance;
            if (map != null && map.IsColon && map.FieldActive && map.FloorClearance(before) < BodyRadius + .1f)
                before = map.ClampToFloor(before, BodyRadius + .2f);
            target = ColonAttackWall.Stop(before, target, BodyRadius);
            Moving = Vector2.Distance(before, target) > .0001f;
            if (Moving) Visual.Face(target - before);
            body.position = target; transform.position = target; Visual.SetMoving(Moving);
        }
        public IEnumerator Wait(float duration)
        {
            for (float elapsed = 0; elapsed < duration && !Dead;)
            { if (!EncounterPaused && !Immobilized) elapsed += Time.deltaTime; yield return null; }
        }
        public IEnumerator Tween(float duration, System.Action<float> sample)
        {
            for (float elapsed = 0; elapsed < duration && !Dead;)
            { if (!EncounterPaused && !Immobilized) { elapsed += Time.deltaTime; sample(Mathf.Clamp01(elapsed / duration)); } yield return null; }
        }
        public bool TryHit(float damage)
        {
            if (!Targetable || Immobilized || damage <= 0 || Time.time < nextHit) return false;
            Player.TakeDamage(damage); nextHit = Time.time + .32f; return true;
        }
        public void PushPlayer(Vector2 delta, Vector2 origin)
        {
            if (!Targetable || !ColonAttackWall.Clear(origin, Player.transform.position, .16f)) return;
            var from = (Vector2)Player.transform.position;
            var to = ColonAttackWall.Stop(from, from + delta, .24f);
            var rb = Player.GetComponent<Rigidbody2D>(); if (rb != null) { rb.position = to; rb.velocity = Vector2.zero; }
            Player.transform.position = to;
        }
        public bool BeginTributeMeal()
        {
            if (!Targetable || PhaseTwo || CurrentPattern != 7) return false;
            Visual.SetPose(KingPose.Eat); Visual.SetPoseProgress(0); return true;
        }
        public void FinishTributeMeal(float amount)
        {
            // A delivered tribute cannot heal after its pattern was cancelled or phase two began.
            if (!Targetable || PhaseTwo || CurrentPattern != 7 || amount <= 0) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
        }
        public override void TakeDamage(float amount, Vector2 knockback = default, bool isCritical = false)
        {
            if (!Targetable || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            var status = GetComponent<OctoberItemTargetStatus>();
            if (status != null && status.VulnerableUntil > Time.time && !SuppressHitFlash) amount *= 1.08f;
            Health = Mathf.Max(PhaseTwo ? 0 : MaxHealth * .5f, Health - amount);
            if (!SuppressHitFlash) Visual.Flash();
            if (!PhaseTwo && Health <= MaxHealth * .5f)
            {
                CancelPattern(); Transitioning = true; hitbox.enabled = false;
                action = StartCoroutine(PhaseTransition()); return;
            }
            if (Health > 0) return;
            Dead = true; hitbox.enabled = false; body.velocity = Vector2.zero; CancelPattern();
            GetComponent<FoodBossCombatStatus>()?.Defeated(); RunScore.Current?.BossKill();
            action = StartCoroutine(Defeat());
        }
        IEnumerator PhaseTransition()
        {
            Visual.SetPose(KingPose.Phase);
            var ring = ConstipationKingEffects.Warning(this, FoodBossEffects.Arc(transform.position, 3.5f), transform.position);
            yield return Tween(2.1f, q => Visual.SetPoseProgress(q));
            if (ring != null) Destroy(ring.gameObject);
            PhaseTwo = true; Visual.SetPhase(2); Visual.ResetPose(); Transitioning = false; hitbox.enabled = true;
            CurrentPattern = -1; action = null; nextAction = 1.1f;
            ColonBossDirector.ShowNotice("변비왕 · 2페이즈", "부채꼴 2연사와 되감기는 휴지를 조심하세요");
        }
        IEnumerator Defeat()
        {
            Visual.SetPose(KingPose.Death);
            for (float t = 0; t < 1.3f;)
            {
                if (!EncounterPaused) { t += Time.deltaTime; Visual.SetPoseProgress(t / 1.3f); Visual.Opacity = 1 - t / 1.3f; Visual.Tick(Time.deltaTime); }
                if (level == null || level.IsLevelEnded || Player == null || !Player.IsAlive) yield break;
                yield return null;
            }
            if (director != null) director.NotifyDefeated(this);
            else if (StageProgression.Ensure(level).StageNumber == 2 && !level.IsLevelEnded) level.LevelPassed(null);
            Destroy(gameObject);
        }
        public override void Knockback(Vector2 knockback) { }
        void OnTriggerStay2D(Collider2D other)
        {
            // Pickup reach is not part of the player's body contact area.
            if (Player == null || other == Player.CollectableCollider) return;
            if (other.GetComponentInParent<Character>() == Player && ColonAttackWall.Clear(HitCenter, Player.transform.position)) TryHit(18);
        }
        void ClearEffects()
        {
            if (Effects == null) return;
            foreach (Transform child in Effects) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        void OnDisable() { CancelPattern(); }
        void OnDestroy() { Active.Remove(this); if (Effects != null) Destroy(Effects.gameObject); }
    }
}
