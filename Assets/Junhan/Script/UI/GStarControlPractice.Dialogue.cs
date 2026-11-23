using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class GStarControlPractice
    {
        static readonly Color BalloonInk = new Color32(58, 37, 47, 255);
        static readonly Color BalloonPaper = new Color32(255, 248, 227, 255);
        static readonly Color CaptionInk = new Color32(133, 65, 59, 255);
        readonly RectTransform[,] directionCaps = new RectTransform[2, Directions.Length];
        RectTransform bubble, tail, primaryKeys, alternateKeys;
        TextMeshProUGUI alternativeHint;
        InteractionKeycapGraphic headKey;
        TextMeshProUGUI caption, action, departHint;
        PlayerCombatBars bars;
        readonly Vector3[] meterCorners = new Vector3[4];

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32755;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            bars = player.GetComponent<PlayerCombatBars>();

            bubble = AugmentPanelTheme.Rect("연습 말풍선", transform);
            bubble.anchorMin = bubble.anchorMax = Vector2.one * .5f;
            bubble.pivot = new Vector2(.5f, 0);
            var paper = bubble.gameObject.AddComponent<IdentityShape>();
            paper.frame = true;
            paper.radius = 16;
            paper.color = BalloonInk;
            paper.insetColor = BalloonPaper;
            paper.raycastTarget = false;
            tail = AugmentPanelTheme.Rect("말풍선 꼬리", bubble);
            tail.pivot = new Vector2(.5f, 1);
            tail.sizeDelta = new Vector2(28, 22);
            var pointer = tail.gameObject.AddComponent<GStarSpeechPointer>();
            pointer.color = BalloonInk;
            pointer.Paper = BalloonPaper;
            pointer.raycastTarget = false;

            caption = BubbleText("연습 항목", 17, CaptionInk, TextAlignmentOptions.MidlineLeft);
            action = BubbleText("연습 대사", 23, BalloonInk, TextAlignmentOptions.MidlineLeft);
            action.enableWordWrapping = true;
            action.fontSizeMin = 17;
            departHint = BubbleText("출발 키 안내", 15, CaptionInk, TextAlignmentOptions.Center);
            departHint.text = "눌러서 출발";

            primaryKeys = BuildDirectionKeys("머리 위 방향 키", 0);
            alternateKeys = BuildDirectionKeys("머리 위 보조 방향 키", 1);
            alternativeHint = BubbleText("이동 키 구분", 15, CaptionInk, TextAlignmentOptions.Center);
            alternativeHint.text = "또는";
            var separator = alternativeHint.rectTransform;
            separator.anchorMin = separator.anchorMax = new Vector2(0, .5f);
            separator.sizeDelta = new Vector2(36, 25);

            headKey = InteractionKeycapGraphic.Create("머리 위 조작 키", bubble, KeyName(GameKeyAction.Dash), 43);
            headKey.Bounds.anchorMin = headKey.Bounds.anchorMax = new Vector2(0, .5f);
        }

        RectTransform BuildDirectionKeys(string name, int slot)
        {
            var group = AugmentPanelTheme.Rect(name, bubble);
            group.anchorMin = group.anchorMax = group.pivot = new Vector2(0, .5f);
            var widths = new float[Directions.Length];
            for (int i = 0; i < Directions.Length; i++)
            {
                var key = GamePreferences.Current.keyboard.Get(Directions[i], slot);
                if (key == KeyCode.None) continue;
                string legend = KeyboardBindingValues.KeyLabel(key);
                var rect = InteractionKeycapGraphic.Create(legend, group, legend, 35).Bounds;
                rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
                widths[i] = rect.sizeDelta.x;
                directionCaps[slot, i] = rect;
            }
            float middle = Mathf.Max(widths[0], widths[2]);
            float left = widths[1] > 0 ? widths[1] + 2 : 0;
            float right = widths[3] > 0 ? widths[3] + 2 : 0;
            group.sizeDelta = new Vector2(left + middle + right, 104);
            for (int i = 0; i < Directions.Length; i++)
            {
                var rect = directionCaps[slot, i];
                if (rect == null) continue;
                float x = i == 1 ? widths[1] / 2 : i == 3 ? left + middle + 2 + widths[3] / 2 : left + middle / 2;
                // The alternate bank matches the inverted T printed on a physical keyboard.
                float y = slot == 1 ? (i == 0 ? 18 : -18) : (i == 0 ? 36 : i == 2 ? -36 : 0);
                rect.anchoredPosition = new Vector2(x, y);
            }
            return group;
        }

        TextMeshProUGUI BubbleText(string name, float size, Color tint, TextAlignmentOptions alignment)
        {
            var rect = AugmentPanelTheme.Rect(name, bubble);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            TrainingUITheme.Text(label, size, tint);
            label.enableWordWrapping = false;
            label.alignment = alignment;
            return label;
        }

        static void Insets(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        void PositionKeys()
        {
            var cam = Camera.main;
            if (cam == null || bubble == null || !bubble.gameObject.activeSelf) return;
            var surface = (RectTransform)transform;
            Vector2 point = cam.WorldToScreenPoint(player.CenterTransform.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, point, null, out var actor)) return;

            // Keep the speech tail just above the health bar, even for different-sized characters.
            float top = actor.y + 44;
            if (bars != null && bars.HealthRect != null && bars.HealthRect.gameObject.activeInHierarchy)
            {
                bars.HealthRect.GetWorldCorners(meterCorners);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,
                    RectTransformUtility.WorldToScreenPoint(null, meterCorners[1]), null, out var meter))
                    top = Mathf.Max(top, meter.y);
            }

            var bounds = surface.rect;
            float halfWidth = bubble.sizeDelta.x * .5f;
            float height = bubble.sizeDelta.y;
            float x = Mathf.Clamp(actor.x, bounds.xMin + 14 + halfWidth, bounds.xMax - 14 - halfWidth);
            float y = top + 20;
            bool below = y + height > bounds.yMax - 12;
            if (below) y = actor.y - 68 - height;
            y = Mathf.Clamp(y, bounds.yMin + 14, bounds.yMax - 12 - height);
            bubble.anchoredPosition = new Vector2(x, y);
            tail.anchorMin = tail.anchorMax = new Vector2(.5f, below ? 1 : 0);
            tail.localScale = new Vector3(1, below ? -1 : 1, 1);
            tail.anchoredPosition = new Vector2(Mathf.Clamp(actor.x - x, 28 - halfWidth, halfWidth - 28), below ? -8 : 8);
        }

        void Draw()
        {
            // The open character record supplies its own key on the page being read.
            bubble.gameObject.SetActive(Step != 4);
            if (Step == 4) return;

            string title, line;
            GameKeyAction? binding = null;
            switch (Step)
            {
                case 0:
                    title = "이동 연습";
                    line = $"네 방향으로 움직여 보세요.   {CompletedDirections}/4";
                    break;
                case 1:
                    title = "대시 연습";
                    line = $"두 번 대시해 보세요.   {DashUses}/2";
                    binding = GameKeyAction.Dash;
                    break;
                case 2:
                    title = player.Skills.Definition.activeName;
                    if (!SkillUsed)
                    {
                        line = "오른쪽 적에게 스킬을 써 보세요.";
                        binding = GameKeyAction.Active;
                    }
                    else if (player.Skills.IsAri)
                    {
                        line = "변신했어요! 오른쪽 적에게 대시하세요.";
                        binding = GameKeyAction.Dash;
                    }
                    else if (player.Skills.IsShini)
                    {
                        line = "적을 향해 키를 길게 눌러 불을 뿜으세요.";
                        binding = GameKeyAction.Active;
                    }
                    else if (needle != null && needle.HasCursorControlLegendary())
                        line = "적 곁에서 회전하는 침을 맞히세요.";
                    else
                        line = player.Skills.IsHyuki ? "적이 얼면 침으로 마무리하세요." : "강화된 침으로 적을 처치하세요.";
                    break;
                case 3:
                    title = "캐릭터 상태";
                    line = "상태 창을 열어 보세요.";
                    binding = GameKeyAction.Journal;
                    break;
                case 5:
                    title = "체력과 아이템";
                    line = "몬스터와 부딪히면 체력이 줄어요.";
                    break;
                case 6:
                    title = "아이템 상자";
                    line = CrateInRange ? "키를 눌러 상자를 열어 보세요." : "오른쪽 상자로 다가가세요.";
                    if (CrateInRange) binding = GameKeyAction.Interact;
                    break;
                case 7:
                    title = "아이템 획득";
                    line = "하트가 아래 아이템 칸에 들어가요.";
                    break;
                case 8:
                    title = "체력 회복";
                    line = "아래 칸의 하트를 사용해 보세요.";
                    binding = GameKeyAction.Slot2;
                    break;
                case 9:
                    title = "체력을 회복했어요!";
                    line = "필드의 다른 아이템도 찾아보세요.";
                    break;
                default:
                    title = "튜토리얼 완료!";
                    line = "이제 " + StageEntryLoading.DestinationName + " 탐험을 시작해 볼까요?";
                    binding = GameKeyAction.Active;
                    break;
            }

            bool move = Step == 0;
            bool departure = Step == 10;
            caption.text = title;
            action.text = line;
            primaryKeys.gameObject.SetActive(move);
            alternateKeys.gameObject.SetActive(move);
            bool bothBanks = primaryKeys.sizeDelta.x > 0 && alternateKeys.sizeDelta.x > 0;
            alternativeHint.gameObject.SetActive(move && bothBanks);
            headKey.gameObject.SetActive(binding.HasValue);
            departHint.gameObject.SetActive(departure);
            float left = 22;
            float height = move ? 119 : 90;
            if (binding.HasValue)
            {
                headKey.ShowKey(Step == 8 ? HeartKeyLabel : KeyName(binding.Value), departure ? 47 : 43, departure || Step == 8);
                float width = headKey.Bounds.sizeDelta.x;
                headKey.Bounds.anchoredPosition = new Vector2(left + width * .5f, departure ? 8 : 0);
                if (departure)
                {
                    var hint = departHint.rectTransform;
                    hint.anchorMin = hint.anchorMax = new Vector2(0, .5f);
                    hint.sizeDelta = new Vector2(Mathf.Max(106, width), 20);
                    hint.anchoredPosition = new Vector2(left + Mathf.Max(106, width) * .5f, -27);
                    width = Mathf.Max(106, width);
                }
                left += width + 17;
            }
            float bodyWidth = Mathf.Clamp(action.GetPreferredValues(line, 1000, 100).x, 205, 360);
            float bodyEnd = left + bodyWidth;
            float end = bodyEnd;
            if (move)
            {
                end += 15;
                primaryKeys.anchoredPosition = new Vector2(end, 0);
                end += primaryKeys.sizeDelta.x;
                if (bothBanks)
                {
                    float gap = alternativeHint.rectTransform.sizeDelta.x + 6;
                    alternativeHint.rectTransform.anchoredPosition = new Vector2(end + gap * .5f, 0);
                    end += gap;
                }
                alternateKeys.anchoredPosition = new Vector2(end, 0);
                end += alternateKeys.sizeDelta.x;
            }
            bubble.sizeDelta = new Vector2(end + 22, height);
            float rightInset = move ? end + 22 - bodyEnd : 17;
            Insets(caption.rectTransform, left, height * .5f + 1, rightInset, height * .5f - 23);
            Insets(action.rectTransform, left, 10, rightInset, height * .5f - 3);
            PositionKeys();
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GStarSpeechPointer : MaskableGraphic
    {
        public Color Paper;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = rectTransform.rect;
            Triangle(mesh, r.xMin, r.xMax, r.yMax - 7, r.yMin, color);
            Triangle(mesh, r.xMin + 3, r.xMax - 3, r.yMax, r.yMin + 5, Paper);
        }

        static void Triangle(VertexHelper mesh, float left, float right, float top, float bottom, Color tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector2(left, top), tint, Vector2.zero);
            mesh.AddVert(new Vector2(right, top), tint, Vector2.zero);
            mesh.AddVert(new Vector2((left + right) * .5f, bottom), tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
        }
    }
}
