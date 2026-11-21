using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Rehearse on the real stomach map with the selected character and the normal input system.
    // The first scored run starts only after the visitor confirms departure.
    public sealed partial class GStarControlPractice : MonoBehaviour
    {
        public static GStarControlPractice Instance { get; private set; }
        public static bool Active => Instance != null;
        public static bool AllowsDash => !Active || Instance.Step == 1 || Instance.Step == 2;
        public static bool AllowsSkill => !Active || (Instance.Step == 2 && Instance.Target != null);
        public static bool BlocksBasicAttack => Active && !Instance.AllowFollowup;
        public int Step { get; private set; }
        public int CompletedDirections { get; private set; }
        public int DashUses { get; private set; }
        public Monster Target { get; private set; }
        public bool SkillUsed { get; private set; }
        public bool Complete { get; private set; }

        // Give each active its visible payoff before the equipped weapon can finish the target.
        bool AllowFollowup => Step == 2 && SkillUsed && Time.time >= followupAt && !player.Skills.IsSummoning;
        static readonly GameKeyAction[] Directions = {
            GameKeyAction.MoveUp, GameKeyAction.MoveLeft, GameKeyAction.MoveDown, GameKeyAction.MoveRight
        };
        readonly List<Canvas> hidden = new List<Canvas>();
        Character player;
        LevelManager level;
        Vector2 initialPosition;
        int directionMask;
        float followupAt, refillDashAt;
        public bool PracticingAugment { get; private set; }
        bool openAugmentAfterKill;
        Transform dialogParent;
        int dialogSibling;
        bool departureReleased;
        bool pendingDashRefill;
        bool pickupWasEnabled;
        Rigidbody2D targetBody;
        RigidbodyConstraints2D previousConstraints;
        uint targetGeneration;
        int firstDash, lastAriPracticeDash;
        SyringeDartAbility needle;

        public static GStarControlPractice Show(LevelManager level)
        {
            var holder = new GameObject("지스타 조작 연습", typeof(RectTransform));
            var view = holder.AddComponent<GStarControlPractice>();
            Instance = view;
            view.level = level;
            view.player = level.PlayerCharacter;
            view.needle = FindObjectOfType<SyringeDartAbility>();
            view.firstDash = view.player.Skills.DashSequence;
            view.initialPosition = view.player.Position;
            view.pickupWasEnabled = view.player.CollectableCollider.enabled;
            view.player.CollectableCollider.enabled = false;
            foreach (var item in FindObjectsOfType<Collectable>()) item.DissolveInAcidLake();
            if (view.player.Skills.IsShini) view.player.Skills.Breath.RestoreFuel(view.player.Skills.Breath.Capacity);
            foreach (var layer in FindObjectsOfType<Canvas>())
            {
                if (!layer.enabled || !layer.isRootCanvas || layer.renderMode == RenderMode.WorldSpace) continue;
                layer.enabled = false;
                view.hidden.Add(layer);
            }
            view.Build();
            view.Draw();
            return view;
        }

        internal static string KeyName(GameKeyAction action)
        {
            var binding = GamePreferences.Current.keyboard;
            var key = binding.Get(action);
            if (key == KeyCode.None) key = binding.Get(action,1);
            if (key == KeyCode.LeftShift || key == KeyCode.RightShift) return "Shift";
            return KeyboardBindingValues.KeyLabel(key);
        }

        void LateUpdate()
        {
            if (openAugmentAfterKill)
            {
                openAugmentAfterKill = false;
                var dialog = level.EntityManager.AbilitySelectionDialog;
                dialogParent = dialog.transform.parent;
                dialogSibling = dialog.transform.GetSiblingIndex();
                dialog.transform.SetParent(transform,false);
                bubble.gameObject.SetActive(false);
                dialog.OpenPractice(this);
                return;
            }
            if (PracticingAugment) return;
            if (Step != 4) PositionKeys();
            if (Step == 0)
            {
                for (int i = 0; i < Directions.Length; i++)
                {
                    int bit = 1 << i;
                    if ((directionMask & bit) != 0 || !GameInput.Pressed(Directions[i])) continue;
                    directionMask |= bit;
                    CompletedDirections++;
                    caps[i].SetActive(false);
                    Draw();
                }
                if (CompletedDirections == Directions.Length) Advance();
            }
            else if (Step == 1)
            {
                int used = Mathf.Min(2, player.Skills.DashSequence - firstDash);
                if (used != DashUses)
                {
                    DashUses = used;
                    pendingDashRefill = used == 1;
                    refillDashAt = Time.time + .65f;
                    Draw();
                }
                if (pendingDashRefill && !player.IsDashing && Time.time >= refillDashAt)
                {
                    pendingDashRefill = false;
                    player.RefreshSkillDashRecharge(true);
                }
                if (DashUses == 2 && !player.IsDashing)
                {
                    SpawnTarget();
                    Advance();
                }
            }
            else if (Step == 2)
            {
                if (Target == null || Target.SpawnGeneration != targetGeneration || !Target.gameObject.activeInHierarchy)
                    SpawnTarget();
                if (!SkillUsed && player.Skills.Active) RecordCast();
                if (SkillUsed && player.Skills.IsAri && Target != null && Target.IsAlive && !player.IsDashing)
                {
                    if (lastAriPracticeDash != player.Skills.DashSequence)
                    {
                        lastAriPracticeDash = player.Skills.DashSequence;
                        PositionTarget();
                        if (player.CurrentDashCharges == 0) player.RefreshSkillDashRecharge(true);
                    }
                    if (!player.Skills.Active)
                    {
                        // A visitor who missed the form's window can cast again immediately in rehearsal.
                        player.Skills.Restore(0,0,0);
                        SkillUsed = false;
                        PositionTarget();
                        Draw();
                    }
                }
                if (SkillUsed && player.Skills.IsShini && !player.Skills.Breath.Busy && !player.Skills.Breath.CanStart)
                    player.Skills.Breath.RestoreFuel(player.Skills.Breath.Capacity);
                if (Target != null && Target.IsAlive && Vector2.Distance(Target.transform.position,player.Position) > 4.2f)
                    PositionTarget();
            }
            else if (Step == 3 && GameInput.Pressed(GameKeyAction.Journal))
            {
                var book = ApothecaryUI.Instance;
                if (book != null && book.OpenPracticeBook()) Advance();
            }
            else if (Step == 4)
            {
                var book = ApothecaryUI.Instance;
                if (book == null) return;
                if (book.Page == "run" && GameInput.Pressed(GameKeyAction.Journal)) book.CloseRunBook();
                if (book.Page == "hud") BeginItemLesson();
            }
            else if (Step >= 5 && Step <= 9) UpdateItemLesson();
            else if (Step == 10)
            {
                if (!GameInput.Held(GameKeyAction.Active)) departureReleased = true;
                else if (departureReleased && GameInput.Pressed(GameKeyAction.Active)) Complete = true;
            }
        }

        void SpawnTarget()
        {
            ReleaseTargetPose();
            var groups = level.CurrentLevelBlueprint.monsters;
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i]?.monsterBlueprints == null) continue;
                foreach (var normal in groups[i].monsterBlueprints)
                {
                    if (normal == null || normal.GetType() != typeof(MeleeMonsterBlueprint)) continue;
                    Target = level.EntityManager.SpawnMonster(i,player.Position+Vector2.right*2.15f,normal,0,true);
                    if (Target == null) continue;
                    Target.ScaleHealthCapacity(12f/Mathf.Max(.01f,Target.SpawnMaxHealth));
                    targetGeneration = Target.SpawnGeneration;
                    targetBody = Target.GetComponent<Rigidbody2D>();
                    previousConstraints = targetBody.constraints;
                    targetBody.constraints = RigidbodyConstraints2D.FreezeAll;
                    PositionTarget();
                    return;
                }
            }
        }

        void PositionTarget()
        {
            var map = FiniteStomachMap.Instance;
            bool rollingLesson = player.Skills.IsAri && Step <= 2;
            float gap = rollingLesson ? Mathf.Clamp(player.DashDistance * .44f,.8f,1.55f) : 2.15f;
            var playerBody = player.GetComponent<Rigidbody2D>();
            Vector2 pos = player.Position + Vector2.right * gap;
            if ((map != null && (map.ClampToField(pos,1f)-pos).sqrMagnitude > .04f) ||
                (rollingLesson && (BloodClotObstacle.ClampDash(playerBody,pos)-pos).sqrMagnitude > .04f))
            {
                playerBody.position = initialPosition;
                player.transform.position = new Vector3(initialPosition.x,initialPosition.y,player.transform.position.z);
                Physics2D.SyncTransforms();
                pos = initialPosition + Vector2.right * gap;
            }
            var body = Target.GetComponent<Rigidbody2D>();
            if (rollingLesson)
            {
                // Ari's swept strike follows the rigidbody, not either sprite's painted center.
                pos = FiniteStomachMap.ClampDash(playerBody.position,BloodClotObstacle.ClampDash(playerBody,pos));
                pos.y -= Target.BodyHitbox.bounds.center.y - body.position.y;
            }
            body.position = pos;
            Target.transform.position = new Vector3(pos.x,pos.y,Target.transform.position.z);
            player.LookDirection = Vector2.right;
        }

        void ReleaseTargetPose()
        {
            if (targetBody != null && Target != null && Target.SpawnGeneration == targetGeneration)
                targetBody.constraints = previousConstraints;
            targetBody = null;
        }

        void RecordCast()
        {
            SkillUsed = true;
            if (player.Skills.IsAri && Target != null && Target.IsAlive)
            {
                lastAriPracticeDash = player.Skills.DashSequence;
                PositionTarget();
            }
            float presentation = player.Skills.IsAri ? 3.5f : player.Skills.IsShini ? 2.4f :
                Mathf.Max(.65f,player.Skills.SummonRemaining+.65f);
            followupAt = Time.time + presentation;
            Draw();
        }

        static bool Owns(Monster monster) => Active && Instance.Target == monster && monster.SpawnGeneration == Instance.targetGeneration;

        public static bool Protects(Monster monster) => Owns(monster) &&
            (Instance.Step != 2 || (!Instance.SkillUsed && !Instance.player.Skills.Active));

        public static bool HandleTargetDeath(Monster monster, bool killedByPlayer)
        {
            if (!Owns(monster)) return false;
            var view = Instance;
            view.ReleaseTargetPose();
            if (killedByPlayer && monster.HP <= 0 && view.Step == 2 && (view.SkillUsed || view.player.Skills.Active))
            {
                if (!view.SkillUsed) view.RecordCast();
                view.PracticingAugment = true;
                view.openAugmentAfterKill = true;
            }
            return true;
        }

        internal void FinishAugmentLesson()
        {
            if (!PracticingAugment) return;
            var dialog = level.EntityManager.AbilitySelectionDialog.transform;
            dialog.SetParent(dialogParent,false);
            dialog.SetSiblingIndex(dialogSibling);
            PracticingAugment = false;
            Advance();
        }

        void Advance() { Step++; Draw(); }
        public void Finish()
        {
            // Unity forbids reparenting a child during its parent's OnDisable.
            RestoreItemTray();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
        void OnDisable()
        {
            if (Instance != this) return;
            var book = ApothecaryUI.Instance;
            if (Step == 4 && book != null && book.Page == "run") book.CloseRunBook();
            ClearItemLesson();
            Instance = null;
            ReleaseTargetPose();
            if (Target != null && Target.SpawnGeneration == targetGeneration && Target.gameObject.activeSelf)
                Target.DespawnForStageTransfer();
            if (player != null && player.CollectableCollider != null) player.CollectableCollider.enabled = pickupWasEnabled;
            foreach (var layer in hidden) if (layer != null) layer.enabled = true;
            hidden.Clear();
        }
    }
}
