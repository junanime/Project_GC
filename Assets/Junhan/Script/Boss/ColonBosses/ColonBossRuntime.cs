using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Optional, player-awakened encounters. They deliberately do not inherit the stage-clear path.
    public sealed partial class ColonBossRuntime : IDamageable
    {
        public static readonly List<ColonBossRuntime> Active = new List<ColonBossRuntime>();
        public ColonBossKind Kind { get; private set; }
        public ColonBossDefinition Definition { get; private set; }
        public Character Player { get; private set; }
        public ColonBossHabitat Habitat { get; private set; }
        public ColonBossVisual Visual { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public bool Dead { get; private set; }
        public bool Arriving { get; private set; }
        public bool Moving { get; private set; }
        public bool Busy => action != null;
        public bool AutoPatterns = true;
        public bool Hidden { get; private set; }
        public int CurrentPattern { get; private set; } = -1;
        public int CompletedPatterns { get; private set; }
        public Transform Effects { get; private set; }
        public Vector2 HitCenter => hitbox != null ? (Vector2)transform.TransformPoint(hitbox.offset) : (Vector2)transform.position;
        public Vector2 Aim => Player == null ? Vector2.left : ((Vector2)Player.transform.position - (Vector2)transform.position).normalized;
        public static bool Paused => Time.timeScale <= 0 || MiniStageRuntimeState.IsInsideMiniStage;
        public bool EncounterPaused => Paused || StageEntryLoading.IsLoading || Player == null || !Player.IsAlive || level != null && (level.IsRunFlowPaused || level.IsLevelEnded);
        public bool Immobilized => GetComponent<NeuralBlockedMonsterStatus>()?.Active == true;
        Rigidbody2D body;
        CapsuleCollider2D hitbox;
        LevelManager level;
        Coroutine action;
        float nextAction = 2.2f, nextContact, damagedAt;
        int sequence;
        AudioSource voice;

        public static ColonBossRuntime Spawn(ColonBossKind kind, Character player, Vector2 position, ColonBossHabitat habitat)
        {
            var go = new GameObject("Stage 2 · " + ColonBossDefinition.Get(kind).Name);
            go.layer = LayerMask.NameToLayer("Monster Full");
            go.transform.position = position;
            var b = go.AddComponent<ColonBossRuntime>();
            b.Kind = kind; b.Definition = ColonBossDefinition.ForCurrentRun(kind); b.Player = player; b.Habitat = habitat;
            b.level = FindObjectOfType<LevelManager>(); b.MaxHealth = b.Health = b.Definition.Health;
            b.body = go.AddComponent<Rigidbody2D>(); b.body.gravityScale = 0; b.body.constraints = RigidbodyConstraints2D.FreezeRotation;
            b.body.bodyType = RigidbodyType2D.Kinematic;
            b.hitbox = go.AddComponent<CapsuleCollider2D>(); b.hitbox.isTrigger = true;
            b.hitbox.direction = CapsuleDirection2D.Horizontal;
            b.hitbox.size = new Vector2(b.Definition.Size * .71f, b.Definition.Size * .40f);
            b.hitbox.offset = Vector2.up * b.Definition.Size * .23f;
            b.Effects = new GameObject(b.Definition.Name + " · encounter effects").transform;
            b.Visual = new GameObject("Approved fixed-scale artwork").AddComponent<ColonBossVisual>(); b.Visual.transform.SetParent(go.transform, false); b.Visual.Initialize(b);
            b.voice = go.AddComponent<AudioSource>(); b.voice.playOnAwake = false; b.voice.spatialBlend = 0;
            go.AddComponent<MapMarker>().Configure(MapMarkerKind.MiniBoss, b.Definition.Name);
            go.AddComponent<ColonBossHealthHud>().Initialize(b);
            Active.Add(b); b.BeginArrival(); return b;
        }
        public void BeginArrival() { Arriving = true; hitbox.enabled = false; action = StartCoroutine(Arrival()); }
        IEnumerator Arrival()
        {
            yield return ColonBossArrival.Play(this);
            if (Dead) yield break;
            Visual.ResetPose(); Arriving = false; SetHidden(false); action = null; nextAction = 1.4f;
            Habitat?.OnArrivalComplete();
        }
        void Update()
        {
            Moving = false;
            if (Dead || EncounterPaused || Immobilized || Arriving) return;
            if (Busy) return;
            nextAction -= Time.deltaTime;
            float distance = Vector2.Distance(transform.position, Player.transform.position);
            if (distance > 3.8f)
            {
                Vector2 target = Player.transform.position;
                if (Kind == ColonBossKind.Manatee && Habitat != null) target = Habitat.ClampTerritory(target);
                Vector2 direction = ColonBossNavigation.Toward(transform.position, target);
                var status = GetComponent<FoodBossCombatStatus>();
                float slow=Mathf.Min(status == null ? 1 : status.SpeedMultiplier,GetComponent<HoneySlowStatus>()?.SpeedMultiplier??1);
                MoveTo((Vector2)transform.position + direction * Definition.MoveSpeed * slow * (GetComponent<IceChillStatus>()?.SpeedMultiplier ?? 1) * Time.deltaTime);
            }
            Visual.Face(Aim);
            // No passive barrage. Rotate through the authored techniques, without duplicate random spam.
            if (AutoPatterns && nextAction <= 0 && distance < 13f) PlayPattern(sequence++ % Definition.PatternCount);
        }
        public bool PlayPattern(int index)
        {
            if (Dead || EncounterPaused || Immobilized || Arriving || Busy || index < 0 || index >= Definition.PatternCount) return false;
            CurrentPattern = index; action = StartCoroutine(Perform(index)); return true;
        }
        IEnumerator Perform(int index)
        {
            yield return Pattern(index);
            RestoreBodyHitbox(); SetHidden(false); Visual.ResetPose(); CompletedPatterns++; CurrentPattern = -1;
            action = null; nextAction = Random.Range(1.8f, 2.6f);
        }
        public void MoveTo(Vector2 target)
        {
            Vector2 before = transform.position;
            if (Kind == ColonBossKind.Manatee && Habitat != null) target = Habitat.ClampTerritory(target);
            var map=FiniteStomachMap.Instance;
            if(map!=null&&map.IsColon&&map.FieldActive){
                if(map.FloorClearance(before)<Definition.Size*.32f+.1f)before=map.ClampToFloor(before,Definition.Size*.32f+.15f);
                target=ColonAttackWall.Stop(before,target,Definition.Size*.32f);
            }else target=FiniteStomachMap.ClampDash(before,target,Definition.Size*.32f);
            Moving = Vector2.Distance(before, target) > .0001f;
            if (Moving) Visual.Face(target - before);
            body.position = target; transform.position = target;
        }
        public void SetHidden(bool hidden)
        {
            Hidden = hidden; hitbox.enabled = !hidden && !Arriving && !Dead;
            if (Visual != null) Visual.Visible = !hidden;
        }
        public void PlaceAt(Vector2 position) { body.position=position;transform.position=position; }
        public void PeekFromDoor(int index,bool peek)
        {
            if(Habitat==null)return;
            if(peek)Habitat.SetActivePipe(index);
            Habitat.ClipToPipe(Visual,peek);
            if(peek)
            {
                Visual.PreparePipePeek();
                Vector2 faceOffset=Visual.PipeFace-(Vector2)transform.position;
                PlaceAt(Habitat.Doors[index]-faceOffset);
                // The hidden belly is behind the wall; only the exposed head can be hit.
                hitbox.offset=Habitat.Doors[index]-(Vector2)transform.position;
                hitbox.size=new Vector2(Mathf.Min(2f,Definition.Size*.45f),1.3f);
            }
            else { RestoreBodyHitbox(); PlaceAt(FiniteStomachMap.SafeSpawn(Habitat.DoorExit(index),Definition.Size*.32f)); }
        }
        void RestoreBodyHitbox()
        {
            hitbox.offset=Vector2.up*Definition.Size*.23f;
            hitbox.size=new Vector2(Definition.Size*.71f,Definition.Size*.40f);
        }
        public IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds && !Dead;)
            { if (!EncounterPaused && !Immobilized) t += Time.deltaTime; yield return null; }
        }
        public IEnumerator Tween(float seconds, System.Action<float> sample)
        {
            for (float t = 0; t < seconds && !Dead;)
            { if (!EncounterPaused && !Immobilized) { t += Time.deltaTime; sample(Mathf.Clamp01(t / seconds)); } yield return null; }
        }
        public override void TakeDamage(float amount, Vector2 knockback = default, bool isCritical = false)
        {
            if (Dead || Arriving || Hidden || EncounterPaused || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            var status = GetComponent<OctoberItemTargetStatus>();
            if (status != null && status.VulnerableUntil > Time.time && !SuppressHitFlash) amount *= 1.08f;
            Health = Mathf.Max(0, Health - amount); damagedAt = Time.time;
            if (!SuppressHitFlash) Visual.Flash();
            if (Health > 0) return;
            Dead = true; hitbox.enabled = false; body.velocity = Vector2.zero;
            StopAllCoroutines(); action = null;
            ClearEffects(); GetComponent<FoodBossCombatStatus>()?.Defeated(); RunScore.Current?.MiniBossKill();
            Habitat?.OnDefeated(); StartCoroutine(Defeat());
        }
        IEnumerator Defeat()
        {
            Visual.ResetPose(); Sound("Death");
            for (float t = 0; t < .8f;) { if (!Paused) { t += Time.deltaTime; Visual.Opacity = 1 - t / .8f; Visual.Lift = Mathf.Sin(t / .8f * Mathf.PI) * .25f; } yield return null; }
            var entities = FindObjectOfType<EntityManager>();
            if (entities != null)
            {
                // A manatee can be defeated while its root is behind a pipe wall.
                // Rewards must land on reachable ground rather than inside that wall.
                for (int i = 0; i < Definition.ExperienceReward / 50; i++) entities.SpawnExpGem(FiniteStomachMap.SafeSpawn((Vector2)transform.position + Random.insideUnitCircle * 1.4f,.18f), GemType.Red50, true);
                int remainingExperience=Definition.ExperienceReward%50;
                while(remainingExperience>=10){entities.SpawnExpGem(FiniteStomachMap.SafeSpawn((Vector2)transform.position+Random.insideUnitCircle*1.4f,.18f),GemType.Green10,true);remainingExperience-=10;}
                while(remainingExperience>0){entities.SpawnExpGem(FiniteStomachMap.SafeSpawn((Vector2)transform.position+Random.insideUnitCircle*1.4f,.18f),GemType.White1,true);remainingExperience--;}
                for (int i = 0; i < Definition.GoldReward / 5; i++) entities.SpawnCoin(FiniteStomachMap.SafeSpawn((Vector2)transform.position + Random.insideUnitCircle * 1.4f,.18f), CoinType.Gold5, true);
            }
            Destroy(gameObject);
        }
        public override void Knockback(Vector2 knockback) { }
        void OnTriggerStay2D(Collider2D other)
        {
            if (Dead || Hidden || Arriving || EncounterPaused || Immobilized || Time.time < nextContact || Visual.Lift > 1f) return;
            var p = other.GetComponentInParent<Character>();
            // Pickup reach is not part of the player's body contact area.
            if (p == null || !p.IsAlive || other == p.CollectableCollider) return;
            if (ColonAttackWall.Clear(HitCenter,Player.transform.position)&&TryHit(Definition.ContactDamage)) nextContact = Time.time + .9f;
        }
        public void Sound(string actionName, float gain = .5f)
        {
            // Reuse approved material sounds without changing pitch. No new ambient noise layer.
            int source = Kind == ColonBossKind.Shark || Kind == ColonBossKind.Hedgehog ? 1 : Kind == ColonBossKind.Turtle ? 2 : Kind == ColonBossKind.Pudding ? 0 : 4;
            var clip = Resources.Load<AudioClip>("FoodBosses/Audio/" + source + "-" + actionName);
            if (clip == null || voice == null) return;
            float distance = Player == null ? 0 : Vector2.Distance(Player.transform.position, transform.position);
            voice.PlayOneShot(clip, gain * GamePreferences.Current.effects * Mathf.Clamp01(1 - distance / 24));
        }
        void ClearEffects() { if (Effects != null) foreach (Transform child in Effects) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
        void OnDisable() { StopAllCoroutines(); action = null; ClearEffects(); }
        void OnDestroy() { Active.Remove(this); if (Effects != null) Destroy(Effects.gameObject); }
    }
}
