using UnityEngine;

namespace Vampire
{
    public sealed partial class GStarControlPractice
    {
        public Chest ItemChest { get; private set; }
        public Health PracticeHeart { get; private set; }
        public bool CrateInRange { get; private set; }
        internal LootInteraction ItemInteraction { get; private set; }
        float practiceInjury, contactStartsAt, tipEndsAt;

        internal static bool HoldsForContact(Character actor) =>
            Active && Instance.player == actor && Instance.Step == 5;

        internal static bool ReservesHeartRecovery(Character actor) =>
            Active && Instance.player == actor && Instance.practiceInjury > 0;

        void BeginItemLesson()
        {
            Vector2 origin = player.Position;
            var body = player.GetComponent<Rigidbody2D>();
            var roomOnRight = origin + Vector2.right * 4.7f;
            if ((FiniteStomachMap.SafeSpawn(roomOnRight,.8f)-roomOnRight).sqrMagnitude > .04f ||
                (BloodClotObstacle.ClampDash(body,roomOnRight)-roomOnRight).sqrMagnitude > .04f)
            {
                origin = initialPosition;
                player.transform.position = origin;
                body.position = origin;
            }
            body.velocity = Vector2.zero;
            player.Move(Vector2.zero);
            player.LookDirection = Vector2.right;
            Advance();
            SpawnContactMonster();
        }

        void SpawnContactMonster()
        {
            SpawnTarget();
            if (targetBody == null) return;
            // Take control of the same pooled melee monster as the skill lesson.
            // At this step Protects prevents leftover summons or ice from killing it.
            targetBody.position = player.Position + Vector2.right * 3.4f;
            targetBody.velocity = Vector2.zero;
            if (Target.BodyRenderer != null) Target.BodyRenderer.flipX = true;
            contactStartsAt = Time.time + .45f;
        }

        void UpdateItemLesson()
        {
            if (Step == 5)
            {
                if (Target == null || Target.SpawnGeneration != targetGeneration || !Target.gameObject.activeInHierarchy)
                    SpawnContactMonster();
                if (targetBody == null || Time.time < contactStartsAt) return;
                var hurt = player.PracticeContactHitbox;
                // The hit follows visible hitbox contact, independently of the normal
                // monster AI, player armor, shields, or the selected character's active.
                if (!Target.BodyHitbox.IsTouching(hurt) && Target.BodyHitbox.Distance(hurt).distance > .025f)
                {
                    targetBody.position = Vector2.MoveTowards(targetBody.position, player.Position, 3.8f * Time.deltaTime);
                    return;
                }
                practiceInjury = player.TakePracticeBump();
                ReleaseTargetPose();
                Target.DespawnForStageTransfer();
                Target = null;
                SpawnItemChest();
                Advance();
            }
            else if (Step == 6)
            {
                bool inRange = ItemInteraction != null && InteractionFocus.IsFocused(ItemInteraction);
                if (CrateInRange != inRange) { CrateInRange = inRange; Draw(); }
            }
            else if (Step == 7)
            {
                if (PracticeHeart == null || PracticeHeart.IsBeingCollected) return;
                var heartBox = PracticeHeart.GetComponent<Collider2D>();
                // A magnet or a large pickup radius must not complete the lesson at
                // the unopened chest. Let the real pop and pickup flight play in full.
                if (!heartBox.enabled || heartBox.Distance(player.PracticeContactHitbox).distance > .22f) return;
                player.CollectableCollider.enabled = true;
                PracticeHeart.Collect();
            }
            else if (Time.time >= tipEndsAt)
            {
                departureReleased = false;
                Advance();
            }
        }

        void SpawnItemChest()
        {
            var blueprint = level.CurrentLevelBlueprint.chestBlueprint;
            var drops = blueprint.lootTable.lootTable;
            var heart = System.Array.Find(drops, drop => drop.item != null && drop.item.TryGetComponent<Health>(out _));
            ItemChest = level.EntityManager.SpawnChest(blueprint, player.Position + Vector2.right * 2.55f);
            ItemChest.GuaranteeItem(heart.item, SetupHeart);
            ItemInteraction = ItemChest.GetComponentInChildren<LootInteraction>(true);
        }

        void SetupHeart(Collectable drop)
        {
            PracticeHeart = (Health)drop;
            var from = (Vector2)ItemChest.transform.position;
            var away = (from - player.Position).normalized;
            if (away.sqrMagnitude < .1f) away = Vector2.right;
            var p = FiniteStomachMap.SafeSpawn(from + away * 1.35f,.32f);
            drop.transform.position = new Vector3(p.x,p.y,drop.transform.position.z);
            PracticeHeart.PracticePickup = RecoverFromHeart;
            ItemInteraction = null;
            CrateInRange = false;
            Advance();
        }

        void RecoverFromHeart()
        {
            if (Step != 7) return;
            player.RestorePracticeBump(practiceInjury);
            practiceInjury = 0;
            player.CollectableCollider.enabled = false;
            PracticeHeart = null;
            tipEndsAt = Time.time + 2.6f;
            Advance();
        }

        void ClearItemLesson()
        {
            if (PracticeHeart != null)
            {
                PracticeHeart.PracticePickup = null;
                if (level != null && level.EntityManager != null) PracticeHeart.DissolveInAcidLake();
                else Destroy(PracticeHeart.gameObject);
            }
            if (ItemInteraction != null && ItemChest != null && ItemChest.gameObject.activeSelf && ItemChest.IsExpectingItem(SetupHeart) && level != null && level.EntityManager != null)
                level.EntityManager.DespawnChest(ItemChest);
            if (player != null && practiceInjury > 0) player.RestorePracticeBump(practiceInjury);
            practiceInjury = 0;
        }
    }
}
