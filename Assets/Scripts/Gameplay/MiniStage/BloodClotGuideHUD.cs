using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // One contextual guide for the current room or nearest entrance. It never takes control.
    [DisallowMultipleComponent]
    public sealed class BloodClotGuideHUD : MonoBehaviour
    {
        static readonly Color Ink = new Color(.16f, .10f, .16f, .94f);
        static readonly Color Gold = new Color(1f, .82f, .36f);
        static readonly Color Mint = new Color(.58f, .96f, .78f);
        static readonly Color Rose = new Color(1f, .56f, .62f);
        MiniStageDirector director;
        LevelManager level;
        Camera view;
        RectTransform surface, safe, card, pointer, tip, bar;
        TextMeshProUGUI heading, detail, buttonText, pointerText;
        IdentityShape frame;
        Button chargeButton;
        Image progress;
        TMP_FontAsset font;
        Transform target;
        BloodClotOvercharge charge;
        float nextText;
        public bool Visible => card != null && card.gameObject.activeSelf;
        public string Heading => Visible ? heading.text : string.Empty;
        public string Detail => Visible ? detail.text : string.Empty;
        public bool PointerVisible => pointer != null && pointer.gameObject.activeSelf;

        void Awake() { director = GetComponent<MiniStageDirector>(); }

        bool InGame()
        {
            if (level == null) level = FindObjectOfType<LevelManager>();
            return level != null && level.PlayerCharacter != null && level.PlayerCharacter.IsAlive &&
                director.isActiveAndEnabled && !level.IsLevelEnded && !director.IsTransitioning && !StageEntryLoading.IsLoading &&
                Time.timeScale > 0f && !TutorialGuide.IsOpen && !GStarGaugeIntro.IsOpen && !GStarControlPractice.Active &&
                !(level.PlayerCharacter.Skills?.IsCutin ?? false) &&
                !(level.EntityManager?.AbilitySelectionDialog?.MenuOpen ?? false) &&
                (ApothecaryUI.Instance == null || ApothecaryUI.Instance.Page == "hud");
        }

        void LateUpdate()
        {
            if (!InGame())
            {
                Hide();
                return;
            }

            bool pressed = GameInput.Pressed(GameKeyAction.Overcharge);
            if (pressed || Time.unscaledTime >= nextText)
            {
                nextText = Time.unscaledTime + .1f;
                var room = director.CurrentRoom;
                charge = null;
                if (director.IsInsideMiniStage && room != null && room.RoomStarted)
                    ShowRoom(room);
                else
                {
                    charge = BloodClotOvercharge.NearestGuide(level.PlayerCharacter.transform.position);
                    if (charge == null || (!charge.Started && !director.CanEnterFromPortal)) { Hide(); return; }
                    if (pressed && director.CanEnterFromPortal) charge.Begin();
                    ShowField(charge);
                }
            }
            if (!Visible) return;
            var area = Screen.safeArea;
            Vector2 screen = new Vector2(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height));
            safe.anchorMin = area.min / screen;
            safe.anchorMax = area.max / screen;
            float width = Mathf.Min(540f, safe.rect.width * .82f);
            card.sizeDelta = new Vector2(width, 74f);
            PositionPointer();
        }

        void Hide()
        {
            charge = null;
            target = null;
            if (card != null)
            {
                card.gameObject.SetActive(false);
                pointer.gameObject.SetActive(false);
            }
        }

        void ShowRoom(MiniStageRoomBase room)
        {
            var exit = room.ReturnInteractable;
            bool open = exit != null && exit.IsUnlocked && room.CanReturnFrom(exit);
            bool atExit = open && exit.CanInteract && InteractionFocus.IsFocused(exit);
            string title, hint;
            target = null;
            if (room.AwaitingSkillReward)
            {
                title = "도전 성공";
                hint = "보상을 하나 선택하세요";
            }
            else if (room.RoomCleared && room.RewardChest != null && !room.RewardChestOpened && !open)
            {
                title = room.Enhanced ? "과충전 보상 상자 도착!" : "보상 상자 도착!";
                hint = $"혈전 오른쪽 상자에 다가가 [{GameInput.Label(GameKeyAction.Interact)}]로 여세요";
                target = room.RewardChest.transform;
            }
            else if (open)
            {
                title = room.EndedWithoutReward ? "보상 없이 귀환 · 혈전 구멍이 열렸어요" :
                    room.RoomCleared ? "도전 완료 · 혈전 구멍이 열렸어요!" : "도전 중 · 지금 귀환할 수도 있어요";
                hint = atExit ? $"[{GameInput.Label(GameKeyAction.Interact)}] 원래 필드로 돌아가기" : "화살표가 가리키는 혈전으로 이동하세요";
                if (!room.RoomCleared && !atExit) hint = room.QuestHint + "  ·  중도 귀환 가능";
                if (!atExit) target = exit.transform;
            }
            else
            {
                title = (room.Enhanced ? "격화 혈전  ·  " : "혈전  ·  ") + room.QuestName;
                hint = room.QuestHint;
            }
            Show(title, hint, open ? Mint : room.Enhanced ? Gold : Rose);
            pointerText.text = open ? "출구" : "보상";
        }

        void ShowField(BloodClotOvercharge current)
        {
            target = null;
            string use = GameInput.Label(GameKeyAction.Interact);
            if (current.Enhanced)
            {
                Show("과충전 성공!  ·  격화 혈전", current.AtEntrance ?
                    $"[{use}] 입장  ·  클리어하면 스킬 보상과 추가 상자를 받아요" : "열린 혈전 구멍에 다가가세요  ·  클리어 보상에 상자 추가", Mint);
            }
            else if (!current.Started)
            {
                string ordinary = current.AtEntrance ? $"[{use}] 일반 입장" : "구멍 근처에서 일반 입장";
                Show("혈전 과충전  ·  추가 보상", current.Objective + "  /  " + ordinary, Gold);
                if (current.CanBegin)
                {
                    chargeButton.gameObject.SetActive(true);
                    buttonText.text = $"[{GameInput.Label(GameKeyAction.Overcharge)}] 과충전 시작";
                }
            }
            else
            {
                string task = current.Objective;
                if (!current.PlayerInside)
                {
                    task = "원 안으로 돌아오세요  ·  범위 밖에서는 집계가 멈춰요";
                    target = current.transform;
                }
                else if (current.Kind == BloodClotOvercharge.Challenge.DashKills)
                    task = current.DashRemaining > 0f ? $"원 안에서 처치하세요  ·  이번 대시 남은 {current.DashRemaining:0.0}초" :
                        current.DashExpired ? $"5초가 지났어요  ·  [{GameInput.Label(GameKeyAction.Dash)}] 대시로 재도전" :
                        $"[{GameInput.Label(GameKeyAction.Dash)}] 대시 후 5초 안에 처치하세요";
                else task = "원 안에서 " + task;
                Show($"혈전 과충전 중  ·  {(int)current.Progress}/{current.Target:0}마리", task, Gold);
                pointerText.text = "혈전";
                bar.gameObject.SetActive(true);
                progress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(current.Progress / Mathf.Max(1,current.Target)), 1f);
            }
        }

        void Show(string title, string body, Color accent)
        {
            if (card == null) Build();
            card.gameObject.SetActive(true);
            heading.text = title;
            heading.color = accent;
            detail.text = body;
            frame.color = Color.Lerp(Ink, accent, .55f);
            bar.gameObject.SetActive(charge != null && charge.Started && !charge.Enhanced);
            bool offer = charge != null && charge.CanBegin;
            chargeButton.gameObject.SetActive(offer);
            heading.rectTransform.anchorMax = new Vector2(offer ? .685f : .97f, .94f);
        }

        void BeginFromHUD()
        {
            if (InGame() && director.CanEnterFromPortal && charge != null &&
                charge == BloodClotOvercharge.NearestGuide(level.PlayerCharacter.transform.position) && charge.Begin())
                ShowField(charge);
        }

        void PositionPointer()
        {
            if (view == null) view = Camera.main;
            if (target == null || view == null || safe.rect.width < 80f || safe.rect.height < 120f)
            {
                pointer.gameObject.SetActive(false);
                return;
            }
            // Inside the view, point down at the portal. Outside, follow its bearing along the safe edge.
            var point = view.WorldToScreenPoint(target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, point, null, out var local);
            if (point.z < 0f) local = -local;
            var bounds = safe.rect;
            var half = new Vector2(bounds.width * .5f - 44f, bounds.height * .5f - 52f);
            var desired = local + Vector2.up * 44f;
            float ratio = Mathf.Max(Mathf.Abs(desired.x) / half.x, Mathf.Abs(desired.y) / half.y);
            var position = desired / Mathf.Max(1f, ratio);
            var direction = ratio > 1f ? local - position : Vector2.down;
            pointer.anchoredPosition = position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            tip.localRotation = Quaternion.Euler(0,0,angle + 90f);
            pointer.gameObject.SetActive(true);
        }

        void Build()
        {
            surface = AugmentPanelTheme.Rect("혈전 행동 안내", transform);
            var canvas = surface.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 215;
            var scaler = surface.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720);
            scaler.matchWidthOrHeight = .5f;
            surface.gameObject.AddComponent<GraphicRaycaster>();
            safe = AugmentPanelTheme.Rect("안전 영역", surface);
            AugmentPanelTheme.Anchors(safe, Vector2.zero, Vector2.one);
            font = ApothecaryUI.Instance != null ? ApothecaryUI.Instance.HudFont : Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig")?.font;
            card = AugmentPanelTheme.Rect("혈전 목표", safe);
            card.anchorMin = card.anchorMax = new Vector2(.5f,.79f);
            card.pivot = new Vector2(.5f,1);
            card.anchoredPosition = Vector2.zero;
            frame = Shape(card, Ink, true);
            var icon = AugmentPanelTheme.Fill("혈전 아이콘",card,Color.white);
            AugmentPanelTheme.Anchors(icon.rectTransform,new Vector2(.018f,.16f),new Vector2(.12f,.84f));
            icon.sprite = OctoberArt.Get("OctoberUI/MapSymbols","Portal");
            icon.preserveAspect = true;
            heading = Label("제목",card,new Vector2(.14f,.50f),new Vector2(.97f,.94f),22);
            detail = Label("현재 행동",card,new Vector2(.14f,.10f),new Vector2(.97f,.54f),19);
            var buttonRect = AugmentPanelTheme.Rect("과충전 시작",card);
            AugmentPanelTheme.Anchors(buttonRect,new Vector2(.70f,.55f),new Vector2(.975f,.93f));
            var face = Shape(buttonRect, Gold, false);
            face.raycastTarget = true;
            chargeButton = buttonRect.gameObject.AddComponent<Button>();
            chargeButton.targetGraphic = face;
            chargeButton.onClick.AddListener(BeginFromHUD);
            buttonText = Label("과충전 키",buttonRect,new Vector2(.025f,0),new Vector2(.975f,1),17);
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Ink;
            bar = AugmentPanelTheme.Rect("과충전 진행",card);
            AugmentPanelTheme.Anchors(bar,new Vector2(.14f,.065f),new Vector2(.96f,.11f));
            var track = bar.gameObject.AddComponent<Image>();
            track.color = new Color(.34f,.24f,.30f); track.raycastTarget = false;
            progress = AugmentPanelTheme.Fill("과충전 진행량",bar,Gold);
            AugmentPanelTheme.Anchors(progress.rectTransform,Vector2.zero,Vector2.one);
            pointer = AugmentPanelTheme.Rect("혈전 방향",safe);
            AugmentPanelTheme.Box(pointer,Vector2.zero,new Vector2(60,34));
            Shape(pointer, new Color(.19f,.27f,.28f), false);
            pointerText = Label("목적지",pointer,Vector2.zero,Vector2.one,18);
            pointerText.alignment = TextAlignmentOptions.Center; pointerText.color = Mint;
            tip = AugmentPanelTheme.Rect("방향",pointer);
            AugmentPanelTheme.Box(tip,Vector2.zero,new Vector2(30,30));
            var arrow = Label("삼각 화살표",tip,new Vector2(.1f,-.37f),new Vector2(.9f,.21f),15);
            arrow.alignment = TextAlignmentOptions.Center; arrow.color = Mint; arrow.text = "▼";
            pointer.gameObject.SetActive(false);
        }

        TextMeshProUGUI Label(string name, Transform parent, Vector2 min, Vector2 max, float size)
        {
            var label = AugmentPanelTheme.Label(name,parent,font,"",size);
            AugmentPanelTheme.Anchors(label.rectTransform,min,max);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = false;
            label.fontSizeMin = size * .72f;
            return label;
        }

        static IdentityShape Shape(RectTransform rect, Color tone, bool framed)
        {
            var graphic = rect.gameObject.AddComponent<IdentityShape>();
            graphic.color = tone;
            graphic.frame = framed;
            graphic.insetColor = Ink;
            graphic.radius = framed ? 14f : 9f;
            graphic.raycastTarget = false;
            return graphic;
        }

        void OnDisable() { Hide(); }
    }
}
