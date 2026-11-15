using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // The rack owns control, never the pooled monster GameObject or its rewards.
    public sealed class ColonChestnutRack : MonoBehaviour
    {
        public readonly List<ColonChestnutRecall> Members = new List<ColonChestnutRecall>();
        ColonBossRuntime owner;
        public static ColonChestnutRack Create(ColonBossRuntime boss)
        {
            var rack = new GameObject("Living chestnut rack").AddComponent<ColonChestnutRack>();
            rack.transform.SetParent(boss.Effects, false); rack.owner = boss; return rack;
        }
        public void Restore()
        {
            foreach (var member in Members.ToArray()) if (member != null) member.Cancel();
            Members.Clear(); if (owner != null && owner.Visual != null) owner.Visual.ExternalChestnuts = false;
        }
        void LateUpdate()
        {
            Members.RemoveAll(m => m == null || !m.Holding);
            if (owner != null && owner.Visual != null)
                owner.Visual.ExternalChestnuts = Members.Exists(m => m.Attached);
        }
        void OnDisable() => Restore();
        void OnDestroy() => Restore();
    }

    // Runs after ColonMonsterVisual (600): it keeps the real S02 artwork, hitbox,
    // HP and spawn generation, only moving that one actor into an authored socket.
    [DefaultExecutionOrder(1100)]
    public sealed class ColonChestnutRecall : MonoBehaviour
    {
        public enum RecallState { Walking, Climbing, Docked, Leaving, Released }
        public RecallState State { get; private set; }
        public ExplodingMonster Monster { get; private set; }
        public uint Generation { get; private set; }
        public bool Holding => initialized && State != RecallState.Released;
        public bool Attached => Holding && State != RecallState.Walking;
        public bool Docked => Holding && State == RecallState.Docked;
        ColonBossRuntime owner; ColonChestnutRack rack; int slot; EntityManager entities;
        Rigidbody2D physicsBody; RigidbodyType2D savedBodyType;
        CircleCollider2D feet; bool savedFeet;
        SpriteRenderer body; Vector3 savedScale, savedPosition; Quaternion savedRotation;
        int savedOrder, savedLayer; bool savedFlip;
        readonly List<SpriteRenderer> shadows = new List<SpriteRenderer>();
        readonly List<bool> shadowStates = new List<bool>();
        bool initialized; float clock, progress, attachedBlend; Vector2 climbFrom, departure, releaseDirection;

        public static ColonChestnutRecall Begin(ColonBossRuntime boss, ColonChestnutRack rack, ExplodingMonster monster, int slot)
        {
            if (boss == null || rack == null || monster == null || monster.BodyRenderer == null) return null;
            foreach (var previous in monster.GetComponents<ColonChestnutRecall>())
            {
                if (previous.Holding && previous.Generation == monster.SpawnGeneration) return null;
                previous.Cancel(); // Released/pool-old components may still await end-of-frame destruction.
            }
            if (!monster.SetCarrierControlled(true)) return null;
            var c = monster.gameObject.AddComponent<ColonChestnutRecall>();
            c.owner = boss; c.rack = rack; c.Monster = monster; c.Generation = monster.SpawnGeneration;
            c.entities = FindObjectOfType<EntityManager>();
            c.slot = Mathf.Clamp(slot, 0, 2); c.body = monster.BodyRenderer;
            c.savedScale = c.body.transform.localScale; c.savedPosition = c.body.transform.localPosition;
            c.savedRotation = c.body.transform.localRotation; c.savedOrder = c.body.sortingOrder;
            c.savedLayer = c.body.sortingLayerID; c.savedFlip = c.body.flipX;
            c.physicsBody = monster.GetComponent<Rigidbody2D>();
            if (c.physicsBody != null) { c.savedBodyType = c.physicsBody.bodyType; c.physicsBody.bodyType = RigidbodyType2D.Kinematic; c.physicsBody.velocity = Vector2.zero; }
            c.feet = monster.GetComponent<CircleCollider2D>();
            if (c.feet != null) { c.savedFeet = c.feet.enabled; c.feet.enabled = false; }
            foreach (var renderer in monster.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer != c.body && renderer.name.ToLowerInvariant().Contains("shadow"))
                { c.shadows.Add(renderer); c.shadowStates.Add(renderer.enabled); }
            c.State = RecallState.Walking; c.initialized = true; rack.Members.Add(c);
            return c;
        }

        bool SameSpawn => Monster != null && Monster.SpawnGeneration == Generation;
        bool Living => SameSpawn && Monster.gameObject.activeInHierarchy && Monster.IsAlive && Monster.HP > 0;
        bool Paused => owner.EncounterPaused || owner.Immobilized || Monster.IsFieldRuntimeSuspended || Time.timeScale <= 0;
        Vector2 Ground => transform.position;
        Vector2 ApproachPoint => FiniteStomachMap.SafeSpawn((Vector2)owner.transform.position +
            new Vector2((slot - 1) * .65f, -.55f), .28f);

        void Update()
        {
            if (!Holding) return;
            if (!Living || owner == null || owner.Dead || !owner.gameObject.activeInHierarchy || rack == null || !rack.gameObject.activeInHierarchy)
            { Cancel(); return; }
            if (Paused) return;
            clock += Time.deltaTime;
            if (State == RecallState.Walking)
            {
                var target = ApproachPoint; var direction = ColonNavigation.Toward(Ground, target);
                var step = Vector2.MoveTowards(Ground, Ground + direction, Time.deltaTime * 4.3f);
                Move(ColonAttackWall.Stop(Ground, step, .28f));
                if (direction.sqrMagnitude > .001f) body.flipX = direction.x > 0;
                if (Vector2.Distance(Ground, target) < .42f)
                { State = RecallState.Climbing; progress = 0; climbFrom = body.transform.position; }
            }
            else if (State == RecallState.Climbing)
            {
                progress = Mathf.Min(1, progress + Time.deltaTime / .42f); attachedBlend = Mathf.SmoothStep(0, 1, progress);
                if (progress >= 1) State = RecallState.Docked;
            }
            else if (State == RecallState.Leaving)
            {
                progress = Mathf.Min(1, progress + Time.deltaTime / .32f); attachedBlend = 1 - Mathf.SmoothStep(0, 1, progress);
                if (progress >= 1)
                { Move(departure); Restore(); if (Living) Monster.ReleaseFromCarrier(releaseDirection); Destroy(this); }
            }
        }

        void LateUpdate()
        {
            if (!Holding || !Living || owner == null || body == null) return;
            if (State == RecallState.Walking)
            {
                // Small planted-foot cadence, without changing the native chestnut silhouette.
                body.transform.localPosition = savedPosition + Vector3.up * (Mathf.Max(0, Mathf.Sin(clock * 16)) * .035f);
                body.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(clock * 8) * 3);
            }
            else
            {
                Vector2 socket = owner.Visual.ChestnutSocket(slot);
                Vector2 anchor = State == RecallState.Climbing ? Vector2.Lerp(climbFrom, socket, attachedBlend) :
                    State == RecallState.Leaving ? Vector2.Lerp(departure, socket, attachedBlend) : socket;
                if (State == RecallState.Climbing || State == RecallState.Leaving)
                    anchor += Vector2.up * Mathf.Sin(progress * Mathf.PI) * .13f;
                // Ground bookkeeping stays on the floor; only the original body/hitbox rides the spine.
                Vector2 floor = State == RecallState.Leaving ? departure : (Vector2)owner.transform.position;
                Move(floor); body.transform.localRotation = Quaternion.identity;
                float parentScale = body.transform.parent == null ? 1 : Mathf.Abs(body.transform.parent.lossyScale.y);
                float scale = owner.Visual.ChestnutHeight(slot) / Mathf.Max(.001f, body.sprite.bounds.size.y * parentScale);
                body.transform.localScale = Vector3.Lerp(savedScale, new Vector3(scale, scale, 1), attachedBlend);
                var position = (Vector3)anchor; position.z = body.transform.position.z; body.transform.position = position;
                body.sortingLayerID = owner.Visual.Main.sortingLayerID; body.sortingOrder = owner.Visual.ChestnutsExposed ? 511 : 509;
                body.flipX = owner.Visual.Main.flipX;
            }
            for (int i = 0; i < shadows.Count; i++) if (shadows[i] != null) shadows[i].enabled = State == RecallState.Walking && shadowStates[i];
            Monster.FitVisibleBody();
            if (Monster.CenterTransform != null && Monster.BodyHitbox != null) Monster.CenterTransform.position = Monster.BodyHitbox.bounds.center;
        }

        public void Depart(Vector2 destination, Vector2 direction)
        {
            if (!Docked) { Cancel(); return; }
            departure = FiniteStomachMap.SafeSpawn(destination, .3f);
            departure = ColonAttackWall.Stop(owner.transform.position, departure, .3f);
            releaseDirection = direction; State = RecallState.Leaving; progress = 0;
        }

        void Move(Vector2 point)
        {
            var p = transform.position; p.x = point.x; p.y = point.y; transform.position = p;
            if (physicsBody != null) { physicsBody.position = point; physicsBody.velocity = Vector2.zero; }
            if (entities != null && entities.Grid != null) entities.Grid.UpdateClient(Monster);
        }

        void Restore()
        {
            if (!Holding) return;
            State = RecallState.Released;
            if (!SameSpawn) return; // A pooled respawn owns its fresh renderer/physics, never this old snapshot.
            Monster.SetCarrierControlled(false);
            if (physicsBody != null) physicsBody.bodyType = savedBodyType;
            // Pool deactivation also resets ColonMonsterVisual to its prefab baseline.
            // Do not overwrite that reset with the smaller stage-two carried snapshot.
            if (body != null && (gameObject.activeInHierarchy || GetComponent<ColonMonsterVisual>() == null))
            {
                body.transform.localScale = savedScale; body.transform.localPosition = savedPosition; body.transform.localRotation = savedRotation;
            }
            // ColonMonsterVisual resets geometry on pool deactivation, but it does not
            // own renderer ordering, facing or the shadow's enabled flag. Restore those
            // even while inactive, before the same pooled actor is spawned again.
            if (body != null) { body.sortingOrder = savedOrder; body.sortingLayerID = savedLayer; body.flipX = savedFlip; }
            for (int i = 0; i < shadows.Count; i++) if (shadows[i] != null) shadows[i].enabled = shadowStates[i];
            if (Living)
            {
                if (feet != null) feet.enabled = savedFeet;
                Monster.FitVisibleBody();
            }
        }
        public void Cancel()
        {
            if (Holding && Living && Attached)
            {
                Vector2 floor = FiniteStomachMap.SafeSpawn(Ground, .32f); Move(floor);
            }
            Restore(); Destroy(this);
        }
        void OnDisable() { Restore(); Destroy(this); }
        void OnDestroy() => Restore();
    }
}
