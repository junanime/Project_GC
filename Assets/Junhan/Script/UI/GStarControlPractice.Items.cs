using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class GStarControlPractice
    {
        public Chest ItemChest { get; private set; }
        public Health PracticeHeart { get; private set; }
        public bool CrateInRange { get; private set; }
        internal LootInteraction ItemInteraction { get; private set; }
        float practiceInjury, contactStartsAt, tipEndsAt;
        InventorySlot heartSlot;
        RectTransform itemTray;
        Transform trayParent;
        int traySibling;
        InteractionKeycapGraphic slotKey;
        Image heartFlight;
        Vector2 flightStart;
        float flightAge;

        internal static bool AllowsItemUse(InventorySlot slot) => !Active ||
            (Instance.Step == 8 && Instance.heartSlot == slot);

        static string HeartKeyLabel
        {
            get
            {
                var binding = GamePreferences.Current.keyboard;
                // Feature the number row in the booth. Either actual binding can be used.
                var second = binding.Get(GameKeyAction.Slot2,1);
                return second != KeyCode.None ? KeyboardBindingValues.KeyLabel(second) : KeyName(GameKeyAction.Slot2);
            }
        }

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
                if (PracticeHeart == null || PracticeHeart.gameObject.activeSelf) return;
                // Storage finalized on the real slot after the chest reward flew to the player.
                if (heartSlot.Count == 0) return;
                if (heartFlight == null) BeginSlotFlight();
                flightAge += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(flightAge / .35f);
                Vector2 end = TrayPoint(RectTransformUtility.WorldToScreenPoint(null,heartSlot.IconImage.transform.position));
                heartFlight.rectTransform.anchoredPosition = Vector2.Lerp(flightStart,end,t*t*(3-2*t)) + Vector2.up*(Mathf.Sin(t*Mathf.PI)*38);
                heartFlight.rectTransform.localScale = Vector3.one*Mathf.Lerp(.65f,1,t);
                if (t >= 1)
                {
                    Destroy(heartFlight.gameObject);
                    slotKey.gameObject.SetActive(true);
                    Advance();
                }
            }
            else if (Step == 9 && Time.time >= tipEndsAt)
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
            PracticeHeart.PracticeUse = RecoverFromHeart;
            level.PlayerInventory.TryGetInventorySlot(drop,out heartSlot);
            itemTray = (RectTransform)heartSlot.transform.parent;
            trayParent = itemTray.parent;
            traySibling = itemTray.GetSiblingIndex();
            itemTray.SetParent(transform,false);
            slotKey = InteractionKeycapGraphic.Create("회복 사용 키",heartSlot.transform,HeartKeyLabel,40);
            slotKey.Bounds.anchorMin = slotKey.Bounds.anchorMax = new Vector2(.84f,.96f);
            slotKey.ShowKey(HeartKeyLabel,40,true);
            slotKey.gameObject.SetActive(false);
            ItemInteraction = null;
            CrateInRange = false;
            Advance();
        }

        Vector2 TrayPoint(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,screen,null,out var point);
            return point;
        }

        void BeginSlotFlight()
        {
            flightStart = TrayPoint(Camera.main.WorldToScreenPoint(player.CenterTransform.position));
            heartFlight = AugmentPanelTheme.Fill("약함으로 들어가는 하트",transform,Color.white);
            AugmentPanelTheme.Box(heartFlight.rectTransform,flightStart,new Vector2(57,57));
            heartFlight.sprite = heartSlot.IconImage.sprite;
            heartFlight.preserveAspect = true;
            heartFlight.raycastTarget = false;
            flightAge = 0;
        }

        void RecoverFromHeart()
        {
            if (Step != 8) return;
            slotKey.gameObject.SetActive(false);
            player.RestorePracticeBump(practiceInjury);
            practiceInjury = 0;
            player.CollectableCollider.enabled = false;
            PracticeHeart = null;
            tipEndsAt = Time.time + 2.6f;
            Advance();
        }

        void RestoreItemTray()
        {
            if (itemTray == null || trayParent == null) return;
            itemTray.SetParent(trayParent,false);
            itemTray.SetSiblingIndex(traySibling);
            itemTray = null;
        }

        void ClearItemLesson()
        {
            if (slotKey != null) Destroy(slotKey.gameObject);
            if (PracticeHeart != null)
            {
                PracticeHeart.PracticeUse = null;
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
