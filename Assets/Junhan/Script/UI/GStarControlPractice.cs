using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Rehearse on the real stomach map with the selected character and the normal input system.
    // The first scored run starts only after the visitor confirms departure.
    public sealed class GStarControlPractice : MonoBehaviour
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
        static readonly Vector2[] KeyOffsets = {
            new Vector2(0,31), new Vector2(-44,0), new Vector2(0,-31), new Vector2(44,0)
        };

        static readonly Color Ink = new Color(.14f,.075f,.15f);
        static readonly Color Cream = new Color(1f,.963f,.86f);
        static readonly Color Mint = new Color(.62f,.95f,.81f);
        readonly List<Canvas> hidden = new List<Canvas>();
        readonly IdentityShape[] progress = new IdentityShape[5];
        Character player;
        LevelManager level;
        Vector2 initialPosition;
        RectTransform cross, headKey, panel;
        TextMeshProUGUI headKeyLabel;
        readonly GameObject[] caps = new GameObject[4];
        int directionMask;
        float followupAt, refillDashAt;
        bool departureReleased;
        bool pendingDashRefill;
        bool pickupWasEnabled;
        Rigidbody2D targetBody;
        RigidbodyConstraints2D previousConstraints;
        uint targetGeneration;
        TextMeshProUGUI key, action, counter;
        int firstDash;
        string[] keys;
        string skillName;
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

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32755;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            panel = new GameObject("연습 안내", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(transform,false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f,0);
            panel.anchoredPosition = new Vector2(0,24);
            panel.sizeDelta = new Vector2(736,110);
            var plate = panel.gameObject.AddComponent<IdentityShape>();
            plate.frame = true; plate.radius = 20; plate.color = Ink; plate.insetColor = new Color(.235f,.14f,.23f,.97f);
            plate.raycastTarget = false;

            var keycap = TrainingUITheme.Pill("조작 키",panel,Mint,new Vector2(.036f,.20f),new Vector2(.286f,.80f));
            key = Label(keycap.transform,Vector2.zero,Vector2.one,26,Ink,TextAlignmentOptions.Center);
            counter = Label(panel,new Vector2(.325f,.57f),new Vector2(.80f,.87f),17,Mint,TextAlignmentOptions.Left);
            action = Label(panel,new Vector2(.325f,.16f),new Vector2(.965f,.57f),28,Cream,TextAlignmentOptions.Left);
            for (int i = 0; i < progress.Length; i++)
            {
                float x = .80f + .035f * i;
                progress[i] = TrainingUITheme.Pill("연습 진도",panel,Mint,new Vector2(x,.669f),new Vector2(x+.025f,.727f));
            }
            keys = new[] {
                "이동",
                KeyName(GameKeyAction.Dash), KeyName(GameKeyAction.Active), KeyName(GameKeyAction.Journal)
            };
            skillName = player.Skills.Definition.activeName;
            cross = AugmentPanelTheme.Rect("머리 위 방향 키", transform);
            cross.anchorMin = cross.anchorMax = new Vector2(.5f,.5f);
            cross.sizeDelta = Vector2.zero;
            for (int i = 0; i < Directions.Length; i++)
            {
                var cap = TrainingUITheme.Pill(KeyName(Directions[i]),cross,Cream,Vector2.zero,Vector2.zero);
                var rect = (RectTransform)cap.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
                rect.sizeDelta = new Vector2(43,29);
                rect.anchoredPosition = KeyOffsets[i];
                var glyph = Label(rect,Vector2.zero,Vector2.one,20,Ink,TextAlignmentOptions.Center);
                glyph.enableAutoSizing = true; glyph.fontSizeMin = 10; glyph.fontSizeMax = 20;
                glyph.text = KeyName(Directions[i]);
                caps[i] = rect.gameObject;
            }
            var capBase = TrainingUITheme.Pill("머리 위 조작 키",transform,Ink,Vector2.zero,Vector2.zero);
            capBase.radius = 9;
            headKey = (RectTransform)capBase.transform;
            headKey.anchorMin = headKey.anchorMax = headKey.pivot = new Vector2(.5f,.5f);
            headKey.sizeDelta = new Vector2(112,47);
            var face = TrainingUITheme.Pill("키캡",headKey,Mint,new Vector2(.027f,.15f), new Vector2(.973f,.96f));
            face.radius = 7;
            headKeyLabel = Label(face.transform,new Vector2(.04f,0),new Vector2(.96f,1),23,Ink,TextAlignmentOptions.Center);
            headKeyLabel.enableAutoSizing = true; headKeyLabel.fontSizeMin=13; headKeyLabel.fontSizeMax=23;
        }

        static TextMeshProUGUI Label(Transform parent, Vector2 min, Vector2 max, float size, Color color, TextAlignmentOptions align)
        {
            var rect = AugmentPanelTheme.Rect("안내 문구", parent);
            AugmentPanelTheme.Anchors(rect,min,max);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            TrainingUITheme.Text(label,size,color);
            label.alignment = align; label.enableWordWrapping = false;
            return label;
        }

        void LateUpdate()
        {
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
                if (book.Page == "hud") Advance();
            }
            else if (Step == 5)
            {
                if (!GameInput.Held(GameKeyAction.Active)) departureReleased = true;
                else if (departureReleased && GameInput.Pressed(GameKeyAction.Active)) Complete = true;
            }
        }

        void PositionKeys()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var canvasRect = (RectTransform)transform;
            var screen = cam.WorldToScreenPoint(player.CenterTransform.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,screen,null,out var local)) return;
            float x = Mathf.Clamp(local.x,canvasRect.rect.xMin+72,canvasRect.rect.xMax-72);
            float y = Mathf.Clamp(local.y+140,canvasRect.rect.yMin+52,canvasRect.rect.yMax-52);
            cross.anchoredPosition = new Vector2(x,y);
            headKey.anchoredPosition = new Vector2(x,Mathf.Clamp(local.y+110,canvasRect.rect.yMin+36,canvasRect.rect.yMax-36));
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
            Vector2 pos = player.Position + Vector2.right*2.15f;
            if (map != null && (map.ClampToField(pos,1f)-pos).sqrMagnitude > .04f)
            {
                player.GetComponent<Rigidbody2D>().position = initialPosition;
                player.transform.position = new Vector3(initialPosition.x,initialPosition.y,player.transform.position.z);
                pos = initialPosition + Vector2.right*2.15f;
            }
            var body = Target.GetComponent<Rigidbody2D>();
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
                view.Advance();
            }
            return true;
        }

        void Advance() { Step++; Draw(); }
        void Draw()
        {
            bool done = Step == 5;
            key.text = done ? keys[2] : Step >= 3 ? keys[3] :
                Step == 2 && SkillUsed ? (player.Skills.IsAri ? keys[1] : "처치") : keys[Step];
            counter.text = done ? "튜토리얼 완료!" : $"조작 연습   {Step+1} / 5";
            action.text = Step == 0 ? $"네 방향으로 움직여 보세요   {CompletedDirections}/4" :
                Step == 1 ? $"두 번 대시해 보세요   {DashUses}/2" :
                done ? "이제 구강으로 출발해 볼까요?" :
                Step == 3 ? $"{keys[3]}을 눌러 캐릭터 상태를 확인하세요!" :
                Step == 4 ? $"{keys[3]}을 다시 눌러 닫으세요." :
                !SkillUsed ? $"오른쪽 적에게 {skillName} 사용" :
                player.Skills.IsAri ? "변신했어요! 적을 향해 대시하세요" :
                player.Skills.IsShini ? "적을 바라보고 불을 뿜으세요" :
                needle != null && needle.HasCursorControlLegendary() ? "적 곁에서 회전하는 침을 맞히세요" :
                player.Skills.IsHyuki ? "적이 얼면 침으로 마무리하세요" : "강화된 침으로 적을 처치하세요";
            action.enableAutoSizing = true; action.fontSizeMin = 18; action.fontSizeMax = 28;
            panel.gameObject.SetActive(Step != 4);
            cross.gameObject.SetActive(Step == 0);
            headKey.gameObject.SetActive(Step != 0 && Step != 4 && (Step != 2 || !SkillUsed || player.Skills.IsAri));
            headKeyLabel.text = key.text;
            headKey.sizeDelta = new Vector2(key.text == "Space" ? 152 : Mathf.Clamp(24 + key.text.Length*16,58,144),47);
            for (int i = 0; i < progress.Length; i++)
                progress[i].color = i <= Step ? Mint : new Color(.44f,.33f,.42f);
        }

        public void Finish() { gameObject.SetActive(false); Destroy(gameObject); }
        void OnDisable()
        {
            if (Instance != this) return;
            var book = ApothecaryUI.Instance;
            if (Step == 4 && book != null && book.Page == "run") book.CloseRunBook();
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
