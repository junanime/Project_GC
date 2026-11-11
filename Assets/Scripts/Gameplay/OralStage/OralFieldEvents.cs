using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public enum OralEventKind { None, Jaw, Food, Water, Coffee, Soda, Sneeze, Ceiling, Hiccup, Brush, BalloonDelivery, Sweet, Salty, Sour, Bitter, Spicy, Boulder, Guard }

    [Serializable]
    public sealed class OralCavityState
    {
        public int ToothIndex;
        public Vector2 Position;
        public float Progress;
        public bool Active, Healed;
        public Transform Anchor;
        internal float SpawnTimer;
        internal readonly List<Monster> Children = new List<Monster>();
    }

    [Serializable]
    public sealed class OralBubbleState
    {
        public Vector2 Position;
        public float Age, Lifetime = 1.4f, Radius = 2.2f;
        public bool Burst;
    }

    /// <summary>
    /// Stage-zero gameplay, separate from its renderer. All timelines use scaled gameplay time,
    /// one disruptive event owns the field at a time, and every temporary state is restored.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(11000)]
    public sealed class OralFieldEvents : MonoBehaviour
    {
        public static OralFieldEvents Instance { get; private set; }
        public OralEventKind CurrentEvent { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public float NormalizedTime => Duration > 0 ? Mathf.Clamp01(Elapsed / Duration) : 0;
        public Vector2 TargetPosition { get; private set; }
        public float JawAmount { get; private set; }
        public bool OnCeiling => CurrentEvent == OralEventKind.Ceiling && CeilingPhase >= 1;
        public int CeilingPhase { get; private set; }
        public int CeilingRemaining => CountLive(ceilingMonsters);
        public string Banner { get; private set; } = "";
        public bool AutoSchedule = true;
        public OralEventAdditions Additions { get; private set; }
        public OralTasteEvents Tastes { get; private set; }
        public OralAmbientContent Ambient { get; private set; }
        public int CompletedEvents { get; private set; }
        public int HealedCavities { get { int count = 0; foreach (var c in cavities) if (c.Healed) count++; return count; } }
        public IReadOnlyList<OralCavityState> Cavities => cavities;
        public IReadOnlyList<OralBubbleState> Bubbles => bubbles;
        public IReadOnlyList<Monster> CeilingMonsters => ceilingMonsters;
        public float CeilingTravel01 { get; private set; }
        public Vector2 FlowDirection => flowDirection;
        public const float TreatmentSeconds = 7f, TreatmentRadius = 3.6f;
        public const float FoodImpactTime = 2.2f;

        readonly List<OralCavityState> cavities = new List<OralCavityState>();
        readonly List<OralBubbleState> bubbles = new List<OralBubbleState>();
        readonly List<Monster> ceilingMonsters = new List<Monster>();
        MeleeMonsterBlueprint ceilingBatBlueprint;
        int ceilingBatPool=-1;
        readonly List<Monster> monsterScratch = new List<Monster>();
        readonly List<Monster> coffeeMonsters = new List<Monster>();
        readonly List<SuspendedMonster> suspended = new List<SuspendedMonster>();
        readonly List<InvertedSprite> inverted = new List<InvertedSprite>();
        readonly List<LowerPickup> lowerPickups = new List<LowerPickup>();
        readonly List<LowerProp> lowerProps = new List<LowerProp>();
        readonly List<HurtboxSnapshot> playerHurtboxes = new List<HurtboxSnapshot>();
        readonly List<SpawnChoice> choices = new List<SpawnChoice>();
        LevelManager level;
        FiniteStomachMap map;
        Character player;
        float nextEvent = 12f, cooldown, toothDwell, spawnClock, coffeeClock, ceilingPhaseAge;
        int scheduleIndex, foodSpawned;
        float contentClock = 24f;
        int contentIndex;
        readonly OralEventKind[] contentBag = { OralEventKind.Sweet, OralEventKind.Boulder, OralEventKind.Salty,
            OralEventKind.Sour, OralEventKind.Guard, OralEventKind.Bitter, OralEventKind.Spicy };
        bool triggered, ownsPause, priorPause, configured, quitting;
        Vector2 flowDirection, ceilingLanding, ceilingReturnOrigin;
        float currentCeilingLift;
        SpriteRenderer ceilingPlayerVisual;
        readonly OralEventKind[] cycle = { OralEventKind.Food, OralEventKind.Hiccup, OralEventKind.BalloonDelivery,
            OralEventKind.Water, OralEventKind.Brush, OralEventKind.Sneeze,
            OralEventKind.Soda, OralEventKind.Food, OralEventKind.Coffee, OralEventKind.Ceiling };

        struct SpawnChoice { public int Pool; public MonsterBlueprint Blueprint; }
        sealed class SuspendedMonster
        {
            public Monster Monster;
            public SpriteRenderer[] Renderers;
            public bool[] Hidden;
            public bool WasSuspended;
        }
        sealed class InvertedSprite
        {
            public SpriteRenderer Sprite;
            public bool Flip;
            public Vector3 Local;
        }
        sealed class LowerPickup
        {
            public Collectable Pickup;
            public uint Generation;
            public SpriteRenderer[] Renderers;
            public bool[] Hidden;
        }
        sealed class LowerProp
        {
            public Renderer[] Renderers;
            public bool[] Hidden;
            public Collider2D[] Colliders;
            public bool[] ColliderEnabled;
            public MapMarker Marker;
            public bool MarkerEnabled;
        }
        sealed class HurtboxSnapshot
        {
            public BoxCollider2D Collider;
            public Vector2 Offset, Size, Pivot;
            public bool FlipY;
        }

        public bool IsLowerPickup(Collectable pickup)
        {
            if (!OnCeiling || pickup == null) return false;
            foreach (var entry in lowerPickups)
                if (entry.Pickup == pickup && entry.Generation == pickup.SpawnGeneration) return true;
            return false;
        }

        public void Configure(LevelManager owner)
        {
            if (configured) CancelAll();
            level = owner; player = owner != null ? owner.PlayerCharacter : null;
            map = FiniteStomachMap.Instance;
            if (level == null || player == null || map == null) return;
            Instance = this; configured = true; quitting = false;
            choices.Clear();
            var data = level.CurrentLevelBlueprint;
            if (data != null && data.monsters != null)
                for (int i = 0; i < data.monsters.Length; i++)
                {
                    var group = data.monsters[i];
                    if (group.monstersPrefab == null || !(group.monstersPrefab.GetComponent<Monster>() is MeleeMonster)) continue;
                    foreach (var bp in group.monsterBlueprints)
                        if (bp != null && OralMonsterRoster.IsBlueprint(bp)) choices.Add(new SpawnChoice { Pool = i, Blueprint = bp });
                    if (choices.Count >= 6) break;
                }
            BuildCavities();
            var additionsObject = new GameObject("구강 추가 이벤트");
            additionsObject.transform.SetParent(transform, false);
            Additions = additionsObject.AddComponent<OralEventAdditions>(); Additions.Configure(this, level);
            var tasteObject = new GameObject("구강 맛 이벤트"); tasteObject.transform.SetParent(additionsObject.transform, false);
            Tastes = tasteObject.AddComponent<OralTasteEvents>(); Tastes.Configure(level);
            var ambientObject = new GameObject("구강 선택 상호작용"); ambientObject.transform.SetParent(additionsObject.transform, false);
            Ambient = ambientObject.AddComponent<OralAmbientContent>(); Ambient.Configure(level);
            Monster.Died += OnMonsterDied;
            nextEvent = 12; cooldown = 0; scheduleIndex = 0;
            contentClock = 24; contentIndex = 0;
            Shuffle(cycle); Shuffle(contentBag);
        }

        void BuildCavities()
        {
            var centers = OralMapGeometry.ToothCenters(map);
            int third = centers.Length / 3;
            for (int i = 0; i < 3; i++)
            {
                int first = i * third;
                int index = UnityEngine.Random.Range(first, i == 2 ? centers.Length : first + third);
                var anchor = new GameObject("충치 치료 " + (i + 1)).transform;
                anchor.SetParent(transform, false); anchor.position = centers[index];
                var state = new OralCavityState { ToothIndex = index, Position = centers[index], Anchor = anchor, SpawnTimer = 5 + i * 3 };
                cavities.Add(state);
                anchor.gameObject.AddComponent<OralCavityInteractable>().Configure(this, i);
            }
        }

        bool Available => configured && !quitting && level != null && player != null && player.IsAlive && !level.IsLevelEnded &&
            map != null && OralStageRuntime.Instance != null && !StageEntryLoading.IsLoading && !MiniStageRuntimeState.IsInsideMiniStage;

        void Update()
        {
            if (!Available)
            {
                if (configured && (player == null || !player.IsAlive || level == null || level.IsLevelEnded)) CancelAll();
                return;
            }
            if (Time.deltaTime <= 0 || player.IsChoosingAugment || player.IsPortalTravelling || (level.GetComponent<StageProgression>()?.Travelling??false) || (level.IsRunFlowPaused && !ownsPause)) return;
            float dt = Time.deltaTime;
            if (AutoSchedule)
            {
                nextEvent -= dt;
                TickContentSchedule(dt);
            }
            cooldown = Mathf.Max(0, cooldown - dt);
            if (!OnCeiling) UpdateCavities(dt);
            if (CurrentEvent == OralEventKind.None)
            {
                if (!AutoSchedule) return;
                toothDwell = IsTeeth(player.Position) ? toothDwell + dt : 0;
                if (cooldown <= 0 && toothDwell >= 3f && ForceEvent(OralEventKind.Jaw)) return;
                if (nextEvent <= 0)
                {
                    var kind = cycle[scheduleIndex % cycle.Length];
                    // Ceiling travel requires the tongue. Skip its slot while the player
                    // stays on teeth instead of blocking every later field event forever.
                    bool skipCeiling = kind == OralEventKind.Ceiling &&
                        OralMapGeometry.ZoneAt(map, player.Position) != OralZone.Tongue;
                    if (skipCeiling || ForceEvent(kind))
                    {
                        scheduleIndex++;
                        if (scheduleIndex % cycle.Length == 0) Shuffle(cycle);
                        if (skipCeiling) nextEvent = 3;
                    }
                    else nextEvent = 3;
                }
                return;
            }
            float previous = Elapsed;
            Elapsed += dt;
            switch (CurrentEvent)
            {
                case OralEventKind.Jaw: TickJaw(previous); break;
                case OralEventKind.Food: TickFood(); break;
                case OralEventKind.Water: if (Elapsed > 1.2f) ApplyFlow(dt, 3.1f); break;
                case OralEventKind.Coffee: TickCoffee(dt); break;
                case OralEventKind.Soda: TickSoda(dt); break;
                case OralEventKind.Sneeze: if (Elapsed >= 1.6f && Elapsed < 3.1f) ApplyFlow(dt, 8f); break;
                case OralEventKind.Ceiling: TickCeiling(dt); return;
                case OralEventKind.Hiccup: case OralEventKind.Brush: case OralEventKind.BalloonDelivery:
                    Additions.Tick(CurrentEvent, Elapsed); break;
            }
            if (Elapsed >= Duration) FinishEvent();
        }

        public bool ForceEvent(OralEventKind kind)
        {
            if (kind >= OralEventKind.Sweet) return BeginContent(kind);
            if (!Available || kind == OralEventKind.None || CurrentEvent != OralEventKind.None || player.IsPortalTravelling ||
                player.IsChoosingAugment || OralStageRuntime.Instance.IsAirborne || level.IsRunFlowPaused || (level.GetComponent<StageProgression>()?.Travelling??false)) return false;
            if (kind == OralEventKind.Jaw && !IsTeeth(player.Position)) return false;
            // Full-tongue fire must leave the teeth safe; do not close the escape route.
            if (kind == OralEventKind.Jaw && Tastes != null && Tastes.Active && Tastes.Kind == OralTasteKind.Spicy) return false;
            // Coffee's pursuit acceleration must not remove the full-floor fire's escape window.
            if (kind == OralEventKind.Coffee && Tastes != null && Tastes.Active && Tastes.Kind == OralTasteKind.Spicy) return false;
            if (kind == OralEventKind.Ceiling && OralMapGeometry.ZoneAt(map, player.Position) != OralZone.Tongue) return false;
            if (kind == OralEventKind.Ceiling && !PrepareCeilingBats()) return false;
            if (kind == OralEventKind.Ceiling) Tastes?.CancelBounces();
            CurrentEvent = kind; sodaCoated.Clear(); Elapsed = 0; triggered = false; foodSpawned = 0; spawnClock = coffeeClock = 0;
            nextEvent = UnityEngine.Random.Range(18f, 27f);
            JawAmount = 0; CeilingPhase = 0; ceilingPhaseAge = 0; CeilingTravel01 = 0;
            TargetPosition = OralMapGeometry.Clamp(map, player.Position + player.EffectiveMoveDirection.normalized * 3f, 2.5f, OralZone.Tongue);
            flowDirection = new Vector2(UnityEngine.Random.value > .5f ? .45f : -.45f, -1).normalized;
            switch (kind)
            {
                case OralEventKind.Jaw: Duration = 3.6f; TargetPosition = player.Position; Banner = "와작! | 치아 사이의 빈틈이나 혀로 피하세요"; break;
                case OralEventKind.Food: Duration = 6.5f; Banner = "한입 들어온다! | 그림자를 피하고 쏟아지는 음식 몬스터를 처치하세요"; break;
                case OralEventKind.Water: Duration = 8f; Banner = "물이 들어온다! | 물살을 피해 아랫니로 이동할 수 있어요"; break;
                case OralEventKind.Coffee: Duration = 20f; Banner = "커피수혈! | 커피가 지나가면 몬스터들이 빨라집니다"; break;
                case OralEventKind.Soda: Duration = 10f; Banner = "탄산 보호막! | 탄산에 닿은 몬스터의 막은 침 3회로 깨세요"; break;
                case OralEventKind.Sneeze: Duration = 4.3f; Banner = "에취! | 치아 주변으로 피하거나 바람을 타세요"; break;
                case OralEventKind.Ceiling: Duration = 6f; Banner = "혀가 뒤집힌다! | 입천장 몬스터를 모두 처치하면 내려옵니다"; break;
                case OralEventKind.Hiccup: Duration = Additions.Begin(kind); Banner = "딸꾹! | 몬스터들이 3초 동안 공중에 뜹니다"; break;
                case OralEventKind.Brush: Duration = Additions.Begin(kind); Banner = "칫솔 청소! | 가로 → 세로 → 가로, 세 번의 경로를 피하세요"; break;
                case OralEventKind.BalloonDelivery: Duration = Additions.Begin(kind); Banner = "풍선껌 보물배송! | 풍선을 맞히면 레벨업 상자가 내려옵니다"; break;
            }
            GameAudioManager.PlaySfx(kind == OralEventKind.Jaw ? GameAudioManager.GameSfxId.DangerWave :
                kind == OralEventKind.Ceiling ? GameAudioManager.GameSfxId.PeristalsisTilt :
                kind == OralEventKind.Coffee ? GameAudioManager.GameSfxId.CoffeeTransfusionPour :
                kind == OralEventKind.Water || kind == OralEventKind.Sneeze ? GameAudioManager.GameSfxId.AcidRefluxWavePass :
                GameAudioManager.GameSfxId.FieldEventStart);
            Notify(Banner);
            return true;
        }

        void TickContentSchedule(float dt)
        {
            if (CurrentEvent == OralEventKind.Ceiling) return;
            contentClock -= dt;
            if (contentClock > 0) return;
            if (BeginContent(contentBag[contentIndex % contentBag.Length]))
            {
                contentIndex++;
                if (contentIndex % contentBag.Length == 0) Shuffle(contentBag);
                contentClock = UnityEngine.Random.Range(20f, 28f);
            }
            else contentClock = 2f; // Retry one entry, never repay missed waves in a burst.
        }

        static void Shuffle(OralEventKind[] bag)
        {
            for (int i = bag.Length - 1; i > 0; i--)
            { int j = UnityEngine.Random.Range(0, i + 1); var temp = bag[i]; bag[i] = bag[j]; bag[j] = temp; }
        }

        bool BeginContent(OralEventKind kind)
        {
            if (!Available || OralEventArt.Paused(level) || CurrentEvent == OralEventKind.Ceiling ||
                OralStageRuntime.Instance.IsAirborne) return false;
            if (kind == OralEventKind.Spicy && (CurrentEvent == OralEventKind.Jaw || CurrentEvent == OralEventKind.Coffee)) return false;
            bool begun;
            if (kind == OralEventKind.Boulder) begun = Ambient != null && Ambient.BeginBoulder();
            else if (kind == OralEventKind.Guard) begun = Ambient != null && Ambient.BeginGuard();
            else
            {
                if (Tastes == null || Tastes.Active) return false;
                begun = Tastes.Begin((OralTasteKind)((int)kind - (int)OralEventKind.Sweet));
            }
            if (begun) NotifyContent(kind, kind == OralEventKind.Spicy ? "매운맛! | 혀 전체가 달아오릅니다 · 치아로 피하세요" :
                kind == OralEventKind.Sweet ? "벌꿀집 발견! | 먹으러 모인 몬스터를 먼저 처치하세요" :
                kind == OralEventKind.Salty ? "짠맛! | 소금결정 무리가 몰려옵니다" :
                kind == OralEventKind.Sour ? "신맛! | 통레몬 스프링을 밟아 더 멀리 튀어 오르세요" :
                kind == OralEventKind.Bitter ? "쓴 한입! | 선택해서 먹고 버티면 회복합니다" :
                kind == OralEventKind.Boulder ? "치석 굴림돌! | 걸어서 밀고 대쉬로 날려보세요" :
                "구강 수호대! | 오토바이를 치아까지 안내하세요");
            return begun;
        }

        void NotifyContent(OralEventKind kind, string message)
        {
            var toast = FindObjectOfType<StageEventToastUI>();
            if (toast == null) return;
            if (kind == OralEventKind.Spicy) toast.ShowImmediateEncounter(message);
            else Notify(message);
        }

        void TickJaw(float previous)
        {
            if (Elapsed < 2) JawAmount = 0;
            else if (Elapsed < 2.38f) JawAmount = Mathf.SmoothStep(0, 1, (Elapsed - 2) / .38f);
            else if (Elapsed < 2.86f) JawAmount = 1;
            else JawAmount = 1 - Mathf.SmoothStep(0, 1, (Elapsed - 2.86f) / .65f);
            if (triggered || Elapsed < 2.37f) return;
            triggered = true;
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.TrapActivate);
            if (!OralStageRuntime.Instance.IsAirborne && JawThreatens(player.Position))
                JawCrushVisual.ApplyDamage(player, player.MaxHealth * .25f);
            SnapshotMonsters();
            foreach (var monster in monsterScratch)
                if (Live(monster) && !monster.IsFieldRuntimeSuspended && JawThreatens(monster.Position))
                {
                    bool boss = monster is BossMonster || monster.Blueprint is EliteMonsterBlueprint;
                    JawCrushVisual.ApplyDamage(monster, boss ? monster.SpawnMaxHealth * .12f : monster.HP + 1f);
                }
        }

        public bool JawThreatens(Vector2 point)
        {
            // Warning contours and damage share the exact same per-tooth polygon.
            // Foot position, rather than the entire sprite, decides safety in the seams.
            return map!=null && OralMapGeometry.IsJawDanger(map,point);
        }

        void TickFood()
        {
            if (!triggered && Elapsed >= FoodImpactTime)
            {
                triggered = true;
                if (!OralStageRuntime.Instance.IsAirborne && (player.Position - TargetPosition).sqrMagnitude < 3f * 3f)
                    player.TakeDamage(Mathf.Max(4, player.MaxHealth * .09f), (player.Position - TargetPosition).normalized * 3);
            }
            while (foodSpawned < 12 && Elapsed >= FoodImpactTime + .12f + foodSpawned * .16f)
            {
                float angle = foodSpawned * 2.399963f;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (2.4f + foodSpawned % 3 * .55f);
                SpawnAt(TargetPosition + offset, OralZone.Tongue, false);
                foodSpawned++;
            }
        }

        void TickCoffee(float dt)
        {
            if (Elapsed < 2f) return;
            coffeeClock -= dt;
            if (coffeeClock > 0) return;
            coffeeClock = .5f;
            SnapshotMonsters();
            foreach (var monster in monsterScratch)
            {
                if (!Live(monster) || monster.IsMiniStageOwned || monster.IsFieldRuntimeSuspended) continue;
                (monster.GetComponent<CoffeeMonsterBuffRuntime>() ?? monster.gameObject.AddComponent<CoffeeMonsterBuffRuntime>())
                    .ApplyOrRefresh(player, Mathf.Max(.1f, Duration - Elapsed), 2.5f, 2f, Color.white);
                if (!coffeeMonsters.Contains(monster)) coffeeMonsters.Add(monster);
            }
        }

        readonly HashSet<Monster> sodaCoated=new HashSet<Monster>();
        void TickSoda(float dt) {
            if(Elapsed<=1.2f||Elapsed>=8f)return;
            float progress=Mathf.Clamp01((Elapsed-1.2f)/6.8f),front=1.15f-progress*1.5f;
            SnapshotMonsters();
            foreach(var monster in monsterScratch) {
                if(!Live(monster)||monster.IsMiniStageOwned||monster.IsFieldRuntimeSuspended||sodaCoated.Contains(monster)||monster is BloodClotMonster)continue;
                var rect=map.WorldRect;var uv=new Vector2((monster.Position.x-rect.xMin)/rect.width,(monster.Position.y-rect.yMin)/rect.height);
                float wave=Mathf.Sin(uv.x*26+Elapsed*3)*.015f+Mathf.Sin(uv.x*51-Elapsed*4)*.006f;
                float d=uv.y+(uv.x-.5f)*FlowDirection.x-front+wave;
                if(OralMapGeometry.ZoneAt(map,monster.Position)!=OralZone.Tongue||d<0||d>.24f)continue;
                var shield=monster.GetComponent<MonsterNeedleBarrier>()??monster.gameObject.AddComponent<MonsterNeedleBarrier>();
                shield.Arm();shield.SodaTint=true;sodaCoated.Add(monster);
            }
        }
        void ApplyFlow(float dt, float force)
        {
            dt = Mathf.Min(dt, .1f); // A stalled frame must not throw actors across the arena.
            if (!OralStageRuntime.Instance.IsAirborne && !player.IsDashing)
                PushActor(player.transform, flowDirection * (force * dt * (IsTeeth(player.Position) ? .18f : 1)));
            SnapshotMonsters();
            foreach (var monster in monsterScratch)
                if (Live(monster) && !monster.IsFieldRuntimeSuspended)
                    PushActor(monster.transform, flowDirection * (force * dt * (IsTeeth(monster.Position) ? .18f : 1)));
        }

        void PushActor(Transform actor, Vector2 delta)
        {
            var old = (Vector2)actor.position;
            var zone = OralMapGeometry.ZoneAt(map, old);
            var position = actor==player.transform && OralStageRuntime.Instance!=null
                ? OralStageRuntime.Instance.ConstrainPlayer(old+delta,.55f)
                : OralMapGeometry.Clamp(map, old + delta, .55f, zone == OralZone.Teeth ? OralZone.Teeth : OralZone.Tongue);
            var body = actor.GetComponent<Rigidbody2D>();
            // Add environmental displacement to locomotion; clearing velocity here would
            // stop normal pursuit on every flow tick and during ceiling bounds checks.
            if (body != null) body.position = position;
            actor.position = new Vector3(position.x, position.y, actor.position.z);
        }

        void UpdateCavities(float dt)
        {
            foreach (var cavity in cavities)
            {
                cavity.Children.RemoveAll(m => !Live(m));
                if (cavity.Healed) continue;
                cavity.SpawnTimer -= dt;
                if (cavity.SpawnTimer <= 0)
                {
                    cavity.SpawnTimer = cavity.Active ? 4f : 9f;
                    if (cavity.Children.Count < 4 && FieldPopulation < 96)
                    {
                        var m = SpawnAt(cavity.Position + UnityEngine.Random.insideUnitCircle * .65f, OralZone.Teeth, false);
                        if (m != null) cavity.Children.Add(m);
                    }
                }
                if (!cavity.Active || !IsTeeth(player.Position) || OralStageRuntime.Instance.IsAirborne ||
                    (player.Position - cavity.Position).sqrMagnitude > TreatmentRadius * TreatmentRadius ||
                    (CurrentEvent == OralEventKind.Jaw && Elapsed > 1.5f && Elapsed < 3.2f)) continue;
                cavity.Progress = Mathf.Min(1, cavity.Progress + dt / TreatmentSeconds);
                if (cavity.Progress >= 1) HealCavity(cavity);
            }
        }

        public bool TryStartTreatment(int index)
        {
            if (!Available || OnCeiling || index < 0 || index >= cavities.Count || level.IsRunFlowPaused ||
                OralStageRuntime.Instance.IsAirborne || !IsTeeth(player.Position)) return false;
            var cavity = cavities[index];
            if (cavity.Healed || (player.Position - cavity.Position).sqrMagnitude > TreatmentRadius * TreatmentRadius) return false;
            cavity.Active = true;
            Notify("충치 치료 시작 | 치아 주변에서 싸우세요. 피했다 돌아와도 진행도는 유지됩니다");
            return true;
        }

        void OnMonsterDied(Monster monster)
        {
            // Pooled objects may be reused by a later event before the next update.
            // Release spawn ownership at death, not by observing activeSelf later.
            foreach (var cavity in cavities) cavity.Children.Remove(monster);
            if (!Available || OnCeiling || monster == null || !monster.WasKilledByPlayer) return;
            foreach (var cavity in cavities)
                if (cavity.Active && !cavity.Healed && (player.Position - cavity.Position).sqrMagnitude <= TreatmentRadius * TreatmentRadius &&
                    (monster.Position - cavity.Position).sqrMagnitude <= (TreatmentRadius + 1) * (TreatmentRadius + 1))
                    cavity.Progress = Mathf.Min(1, cavity.Progress + .09f);
        }

        void HealCavity(OralCavityState cavity)
        {
            cavity.Healed = true; cavity.Active = false; cavity.Progress = 1;
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.RewardEvent);
            SnapshotMonsters();
            foreach (var monster in monsterScratch)
                if (Live(monster) && !monster.IsFieldRuntimeSuspended && (monster.Position - cavity.Position).sqrMagnitude < 25)
                    PushActor(monster.transform, (monster.Position - cavity.Position).normalized * 2);
            player.GainPickupHealth(player.MaxHealth * .08f);
            for (int i = 0; i < 5; i++)
                level.EntityManager.SpawnExpGem(OralMapGeometry.Clamp(map, cavity.Position + UnityEngine.Random.insideUnitCircle, .4f, OralZone.Teeth));
            if (HealedCavities == 3 && level.CurrentLevelBlueprint.chestBlueprint != null)
                level.EntityManager.SpawnChest(level.CurrentLevelBlueprint.chestBlueprint, OralMapGeometry.Clamp(map, cavity.Position, 1f, OralZone.Teeth));
            Notify("충치 치료 완료! | " + HealedCavities + "/3 · 몬스터 생성 종료 · 회복과 경험치 획득");
        }

        void TickCeiling(float dt)
        {
            ceilingPhaseAge += dt;
            if (CeilingPhase == 0 && ceilingPhaseAge >= 1.4f)
            {
                if (OralStageRuntime.Instance.IsAirborne || OralMapGeometry.ZoneAt(map, player.Position) != OralZone.Tongue)
                { FinishEvent(); return; }
                BeginCeiling(); CeilingPhase = 1; ceilingPhaseAge = 0;
            }
            else if (CeilingPhase == 1)
            {
                CeilingTravel01 = Mathf.SmoothStep(0, 1, ceilingPhaseAge / 1.1f);
                currentCeilingLift = Mathf.Sin(CeilingTravel01 * Mathf.PI) * 2.2f;
                MoveCeilingPlayer(Vector2.Lerp(ceilingLanding,OralCeilingArenaGeometry.Center(map),CeilingTravel01));
                if (ceilingPhaseAge >= 1.1f)
                {
                    currentCeilingLift = 0; CeilingPhase = 2; ceilingPhaseAge = 0;
                    OralStageRuntime.Instance.SetCeiling(true);
                    player.GetComponent<PlayerMiniStageStunRuntime>()?.EndTravelLock(this);
                    Banner = "입천장 전투 | 매달린 박쥐 10마리가 깨어납니다 · 모두 처치하면 혀로 돌아갑니다";
                }
            }
            else if (CeilingPhase == 2)
            {
                // Spawn failures cannot strand the player. Despawned/dead pool members no longer count.
                if (CeilingRemaining == 0)
                {
                    ceilingReturnOrigin=player.Position;
                    CeilingPhase = 3; ceilingPhaseAge = 0; Banner = "입천장 탈출! | 혀로 안전하게 내려갑니다";
                    PlayerMiniStageStunRuntime.GetOrCreate(player).BeginTravelLock(this);
                }
                else
                {
                    // Keep all encounter actors reachable even after knockback/oversized hitboxes.
                    foreach (var m in ceilingMonsters) if (Live(m))
                    {
                        var point=OralCeilingArenaGeometry.Clamp(map,m.Position,.30f);
                        var rb=m.GetComponent<Rigidbody2D>();if(rb!=null)rb.position=point;
                        m.transform.position=new Vector3(point.x,point.y,m.transform.position.z);
                        level.EntityManager.Grid?.UpdateClient(m);
                    }
                }
            }
            else if (CeilingPhase == 3)
            {
                CeilingTravel01 = 1 - Mathf.SmoothStep(0, 1, ceilingPhaseAge / 1.1f);
                currentCeilingLift = Mathf.Sin(CeilingTravel01 * Mathf.PI) * 1.4f;
                MoveCeilingPlayer(Vector2.Lerp(ceilingReturnOrigin,ceilingLanding,1-CeilingTravel01));
                if (ceilingPhaseAge >= 1.1f)
                {
                    RestoreCeiling(); FinishEvent();
                }
            }
        }

        void MoveCeilingPlayer(Vector2 position)
        {
            if(player==null)return;
            var world=new Vector3(position.x,position.y,player.transform.position.z);
            var inputLock=player.GetComponent<PlayerMiniStageStunRuntime>();
            if(inputLock!=null&&inputLock.MoveTravelLock(this,world))return;
            var body=player.GetComponent<Rigidbody2D>();
            if(body!=null){body.position=position;body.velocity=Vector2.zero;}
            player.transform.position=world;
        }

        void BeginCeiling()
        {
            ceilingLanding = OralMapGeometry.Clamp(map, player.Position, 1, OralZone.Tongue);
            priorPause = level.IsRunFlowPaused; ownsPause = true; level.SetRunFlowPaused(true);
            ClearSurfaceProjectiles();
            SnapshotMonsters();
            foreach (var monster in monsterScratch)
            {
                if (!Live(monster) || monster.IsMiniStageOwned) continue;
                var entry = new SuspendedMonster { Monster = monster, WasSuspended = monster.IsFieldRuntimeSuspended,
                    Renderers = monster.GetComponentsInChildren<SpriteRenderer>(true) };
                entry.Hidden = new bool[entry.Renderers.Length];
                for (int i = 0; i < entry.Renderers.Length; i++)
                { entry.Hidden[i] = entry.Renderers[i].forceRenderingOff; entry.Renderers[i].forceRenderingOff = true; }
                monster.SetFieldRuntimeSuspended(true); suspended.Add(entry);
            }
            foreach (var pickup in FindObjectsOfType<Collectable>())
            {
                if (pickup.IsBeingCollected) continue;
                var entry = new LowerPickup { Pickup = pickup, Generation = pickup.SpawnGeneration,
                    Renderers = pickup.GetComponentsInChildren<SpriteRenderer>(true) };
                entry.Hidden = new bool[entry.Renderers.Length];
                for (int i = 0; i < entry.Renderers.Length; i++)
                { entry.Hidden[i] = entry.Renderers[i].forceRenderingOff; entry.Renderers[i].forceRenderingOff = true; }
                lowerPickups.Add(entry);
            }
            // A ceiling fight occupies a different surface. Lower chests and jars must
            // not intercept its projectiles or become invisible physical obstacles.
            var layout = level.GetComponent<FieldObjectLayout>();
            var props = new HashSet<GameObject>();
            if (layout != null) foreach (var prop in layout.SpawnedObjects) if (prop != null) props.Add(prop);
            foreach (var chest in FindObjectsOfType<Chest>()) props.Add(chest.gameObject);
            foreach (var portal in FindObjectsOfType<BloodClotMiniStagePortal>()) props.Add(portal.gameObject);
            foreach (var exit in FindObjectsOfType<OralSwallowExit>()) props.Add(exit.gameObject);
            foreach (var prop in props)
                {
                    if (prop == null || !prop.activeInHierarchy || prop.GetComponent<Monster>() != null) continue;
                    var entry = new LowerProp { Renderers = prop.GetComponentsInChildren<Renderer>(true),
                        Colliders = prop.GetComponentsInChildren<Collider2D>(true), Marker = prop.GetComponent<MapMarker>() };
                    entry.Hidden = new bool[entry.Renderers.Length];
                    for (int i = 0; i < entry.Renderers.Length; i++)
                    { entry.Hidden[i] = entry.Renderers[i].forceRenderingOff; entry.Renderers[i].forceRenderingOff = true; }
                    entry.ColliderEnabled = new bool[entry.Colliders.Length];
                    for (int i = 0; i < entry.Colliders.Length; i++)
                    { entry.ColliderEnabled[i] = entry.Colliders[i].enabled; entry.Colliders[i].enabled = false; }
                    if (entry.Marker != null) { entry.MarkerEnabled = entry.Marker.enabled; entry.Marker.enabled = false; }
                    lowerProps.Add(entry);
                }
            ceilingPlayerVisual = player.GetComponentInChildren<SpriteAnimator>(true)?.GetComponent<SpriteRenderer>();
            AddInversion(ceilingPlayerVisual);
            playerHurtboxes.Clear();
            foreach (var box in player.GetComponentsInChildren<BoxCollider2D>(true))
                if (box.isTrigger && box.name == "Full Hitbox")
                    playerHurtboxes.Add(new HurtboxSnapshot { Collider = box, Offset = box.offset, Size = box.size,
                        Pivot = ceilingPlayerVisual != null ? (Vector2)box.transform.InverseTransformPoint(ceilingPlayerVisual.transform.position) : Vector2.zero,
                        FlipY = ceilingPlayerVisual != null && ceilingPlayerVisual.flipY });
            PlayerMiniStageStunRuntime.GetOrCreate(player).BeginTravelLock(this);
            SpawnCeilingBats();
        }

        bool PrepareCeilingBats()
        {
            if(ceilingBatBlueprint!=null)return true;
            if(!OralCeilingBatArt.Load()||choices.Count==0)return false;
            foreach(var choice in choices)
            {
                if(!(choice.Blueprint is MeleeMonsterBlueprint template))continue;
                ceilingBatBlueprint=Instantiate(template);ceilingBatPool=choice.Pool;
                ceilingBatBlueprint.name="입천장 박쥐";
                ceilingBatBlueprint.description="입천장에 거꾸로 매달렸다가 날개를 펴고 날아오는 박쥐.";
                ceilingBatBlueprint.atk=4;ceilingBatBlueprint.movespeed=.9f;ceilingBatBlueprint.acceleration=5;
                ceilingBatBlueprint.useGroundShadowFootprint=false;
                ceilingBatBlueprint.walkSpriteSequence=new[]{OralCeilingBatArt.Body};
                ceilingBatBlueprint.resultSprite=OralCeilingBatArt.Body;ceilingBatBlueprint.walkFrameTime=.1f;
                return true;
            }
            return false;
        }

        void SpawnCeilingBats()
        {
            if(!PrepareCeilingBats())return;
            for(int i=0;i<OralCeilingBatRuntime.Count;i++)
            {
                float angle=i*Mathf.PI*2/OralCeilingBatRuntime.Count;
                var point=OralCeilingArenaGeometry.Point(map,new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(i%2==0?.72f:.83f));
                var monster=level.EntityManager.SpawnMonster(ceilingBatPool,point,ceilingBatBlueprint);
                if(monster==null)continue;
                point=OralCeilingArenaGeometry.Clamp(map,point,.30f);
                monster.transform.position=new Vector3(point.x,point.y,monster.transform.position.z);
                var rb=monster.GetComponent<Rigidbody2D>();if(rb!=null){rb.position=point;rb.velocity=Vector2.zero;}
                (monster.GetComponent<OralCeilingBatRuntime>()??monster.gameObject.AddComponent<OralCeilingBatRuntime>()).Configure(this,monster,i);
                ceilingMonsters.Add(monster);level.EntityManager.Grid?.UpdateClient(monster);
            }
        }

        void AddInversion(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            inverted.Add(new InvertedSprite { Sprite = renderer, Flip = renderer.flipY, Local = renderer.transform.localPosition });
        }

        void LateUpdate()
        {
            if (!OnCeiling) return;
            foreach (var entry in inverted)
            {
                if (entry.Sprite == null || !entry.Sprite.gameObject.activeInHierarchy) continue;
                bool upsideDown = CeilingPhase == 2 || (CeilingPhase == 1 && CeilingTravel01 > .5f) || (CeilingPhase == 3 && CeilingTravel01 > .5f);
                entry.Sprite.flipY = upsideDown ? !entry.Flip : entry.Flip;
                entry.Sprite.transform.localPosition = entry.Local + Vector3.up * currentCeilingLift;
            }
            // Reflect the existing idle-fit hurtbox rather than refitting transient
            // walking frames. Its size stays stable; only its position follows the
            // visual's flip and lift. Foot collisions and pickup range stay unchanged.
            if (ceilingPlayerVisual != null)
            {
                foreach (var box in playerHurtboxes)
                {
                    if (box.Collider == null) continue;
                    Vector2 center = box.Offset;
                    if (ceilingPlayerVisual.flipY != box.FlipY) center.y = 2 * box.Pivot.y - center.y;
                    Vector2 pivot = box.Collider.transform.InverseTransformPoint(ceilingPlayerVisual.transform.position);
                    box.Collider.offset = center + pivot - box.Pivot;
                    box.Collider.size = box.Size;
                }
            }
        }

        void RestoreCeiling()
        {
            if (!ownsPause && inverted.Count == 0 && suspended.Count == 0 && lowerPickups.Count == 0 && lowerProps.Count == 0 && ceilingMonsters.Count == 0) return;
            ClearSurfaceProjectiles();
            foreach (var entry in inverted)
                if (entry.Sprite != null) { entry.Sprite.flipY = entry.Flip; entry.Sprite.transform.localPosition = entry.Local; }
            inverted.Clear(); currentCeilingLift = 0;
            foreach (var box in playerHurtboxes)
                if (box.Collider != null) { box.Collider.offset = box.Offset; box.Collider.size = box.Size; }
            playerHurtboxes.Clear(); ceilingPlayerVisual = null;
            if (OralStageRuntime.Instance != null) OralStageRuntime.Instance.SetCeiling(false);
            foreach (var entry in suspended)
            {
                if (entry.Monster == null) continue;
                for (int i = 0; i < entry.Renderers.Length; i++) if (entry.Renderers[i] != null) entry.Renderers[i].forceRenderingOff = entry.Hidden[i];
                if (!entry.WasSuspended) entry.Monster.SetFieldRuntimeSuspended(false);
            }
            suspended.Clear();
            foreach (var entry in lowerPickups)
                if (entry.Pickup != null && entry.Pickup.SpawnGeneration == entry.Generation)
                    for (int i = 0; i < entry.Renderers.Length; i++)
                        if (entry.Renderers[i] != null) entry.Renderers[i].forceRenderingOff = entry.Hidden[i];
            lowerPickups.Clear();
            foreach (var entry in lowerProps)
            {
                for (int i = 0; i < entry.Renderers.Length; i++)
                    if (entry.Renderers[i] != null) entry.Renderers[i].forceRenderingOff = entry.Hidden[i];
                for (int i = 0; i < entry.Colliders.Length; i++)
                    if (entry.Colliders[i] != null) entry.Colliders[i].enabled = entry.ColliderEnabled[i];
                if (entry.Marker != null) entry.Marker.enabled = entry.MarkerEnabled;
            }
            lowerProps.Clear();
            if (ownsPause && level != null) level.SetRunFlowPaused(priorPause);
            ownsPause = false;
            foreach (var monster in ceilingMonsters) if (monster!=null&&monster.gameObject.activeInHierarchy) monster.DespawnForStageTransfer();
            ceilingMonsters.Clear();
            if (player != null && map != null)
                MoveCeilingPlayer(OralMapGeometry.Clamp(map,ceilingLanding,1,OralZone.Tongue));
            if(player!=null)player.GetComponent<PlayerMiniStageStunRuntime>()?.EndTravelLock(this);
        }

        static void ClearSurfaceProjectiles()
        {
            foreach (var projectile in FindObjectsOfType<Projectile>()) projectile.RemoveForItem();
        }

        Monster SpawnAt(Vector2 position, OralZone zone, bool ceiling)
        {
            if (choices.Count == 0 || level == null || level.EntityManager == null || (!ceiling && FieldPopulation >= 112)) return null;
            var choice = choices[UnityEngine.Random.Range(0, choices.Count)];
            position = OralMapGeometry.Clamp(map, position, .8f, zone);
            var monster = level.EntityManager.SpawnMonster(choice.Pool, position, choice.Blueprint);
            if (monster == null) return null;
            // Use the selected surface even when a generic spawn safety check preferred the tongue.
            position = OralMapGeometry.Clamp(map, position, Mathf.Max(.55f, monster.Size.magnitude * .35f), zone);
            monster.transform.position = new Vector3(position.x, position.y, monster.transform.position.z);
            var body = monster.GetComponent<Rigidbody2D>(); if (body != null) body.position = position;
            return monster;
        }

        int FieldPopulation => level != null && level.EntityManager != null && level.EntityManager.LivingMonsters != null ? level.EntityManager.LivingMonsters.Count : 0;
        bool IsTeeth(Vector2 point) => map != null && OralMapGeometry.ZoneAt(map, point) == OralZone.Teeth;
        static bool Live(Monster monster) => monster != null && monster.gameObject.activeInHierarchy && monster.HP > 0;
        static int CountLive(List<Monster> list) { int n = 0; foreach (var m in list) if (Live(m)) n++; return n; }
        void SnapshotMonsters()
        {
            monsterScratch.Clear();
            if (level == null || level.EntityManager == null || level.EntityManager.LivingMonsters == null) return;
            foreach (var monster in level.EntityManager.LivingMonsters) monsterScratch.Add(monster);
        }
        void Notify(string message)
        {
            var toast = FindObjectOfType<StageEventToastUI>();
            if (toast == null) return;
            if (CurrentEvent!=OralEventKind.None&&message==Banner) toast.ShowImmediateEncounter(message);
            else toast.Show(message);
        }
        void FinishEvent()
        {
            Additions?.EndEvent();
            foreach (var monster in coffeeMonsters) if (monster != null) monster.GetComponent<CoffeeMonsterBuffRuntime>()?.RemoveEventBuff();
            coffeeMonsters.Clear(); bubbles.Clear();
            CurrentEvent = OralEventKind.None; JawAmount = 0; Elapsed = 0; Duration = 0; CeilingPhase = 0;
            cooldown = 10; nextEvent = Mathf.Max(2f, nextEvent); toothDwell = 0; CompletedEvents++;
            Banner = "혀와 아랫니를 탐험하세요 · 가장자리 대쉬로 점프 · 충치 치료 " + HealedCavities + "/3";
        }
        public void CancelAll()
        {
            Tastes?.CancelAll(); Tastes = null;
            Ambient?.CancelAll(); Ambient = null;
            if (Additions != null) { Additions.EndEvent(); Additions.gameObject.SetActive(false); Destroy(Additions.gameObject); Additions = null; }
            Monster.Died -= OnMonsterDied;
            RestoreCeiling();
            if(ceilingBatBlueprint!=null){Destroy(ceilingBatBlueprint);ceilingBatBlueprint=null;}ceilingBatPool=-1;
            foreach (var monster in coffeeMonsters) if (monster != null) monster.GetComponent<CoffeeMonsterBuffRuntime>()?.RemoveEventBuff();
            coffeeMonsters.Clear(); bubbles.Clear();
            foreach (var cavity in cavities) if (cavity.Anchor != null) Destroy(cavity.Anchor.gameObject);
            cavities.Clear();
            CurrentEvent = OralEventKind.None; JawAmount = 0; CeilingPhase = 0; Elapsed = Duration = 0;
            configured = false;
            if (Instance == this) Instance = null;
        }
        void OnDisable() { if (configured) CancelAll(); }
        void OnDestroy() { if (configured) CancelAll(); }
        void OnApplicationQuit() { quitting = true; }
    }

    /// <summary>Uses the same focus/keyboard/touch registry as existing NPC interactions.</summary>
    public sealed class OralCavityInteractable : MonoBehaviour
    {
        OralFieldEvents owner;
        int index;
        public Component VisualOwner;
        public void Configure(OralFieldEvents events, int cavityIndex)
        {
            owner = events; index = cavityIndex;
            InteractionFocus.Register(this, () => Available, () => VisualOwner != null ? VisualOwner : this);
        }
        public bool Available
        {
            get
            {
                var stage = OralStageRuntime.Instance;
                if (!isActiveAndEnabled || owner == null || owner.OnCeiling || index >= owner.Cavities.Count || stage == null || stage.Level == null) return false;
                var p = stage.Level.PlayerCharacter;
                var cavity = owner.Cavities[index];
                return p != null && p.IsAlive && !p.IsChoosingAugment && !stage.Level.IsRunFlowPaused && !stage.IsAirborne &&
                    !cavity.Healed && !cavity.Active && (p.Position - cavity.Position).sqrMagnitude <= OralFieldEvents.TreatmentRadius * OralFieldEvents.TreatmentRadius;
            }
        }
        void Update()
        {
            if (Available && Time.timeScale > 0 && GameInput.TryConsumeInteraction(KeyCode.E, this)) owner.TryStartTreatment(index);
        }
    }
}
