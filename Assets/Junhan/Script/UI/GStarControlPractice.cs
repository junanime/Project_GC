using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Rehearse on the real stomach map with the selected character and the normal input system.
    // The loading screen creates a fresh combat scene when the three actions are complete.
    public sealed class GStarControlPractice : MonoBehaviour
    {
        public static GStarControlPractice Instance { get; private set; }
        public static bool Active => Instance != null;
        public static bool AllowsDash => !Active || Instance.Step >= 1;
        public static bool AllowsSkill => !Active || Instance.Step >= 2;
        public int Step { get; private set; }
        public bool Complete => Step == 3 && Time.time >= finishAt;

        static readonly Color Ink = new Color(.14f,.075f,.15f);
        static readonly Color Cream = new Color(1f,.963f,.86f);
        static readonly Color Mint = new Color(.62f,.95f,.81f);
        readonly List<Canvas> hidden = new List<Canvas>();
        readonly IdentityShape[] progress = new IdentityShape[3];
        Character player;
        Vector2 last;
        float walked, finishAt;
        TextMeshProUGUI key, action, counter;
        int firstDash;
        string[] keys;
        string skillName;

        public static GStarControlPractice Show(LevelManager level)
        {
            var holder = new GameObject("지스타 조작 연습", typeof(RectTransform));
            var view = holder.AddComponent<GStarControlPractice>();
            Instance = view;
            view.player = level.PlayerCharacter;
            view.firstDash = view.player.Skills.DashSequence;
            view.last = view.player.Position;
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

        static string KeyName(GameKeyAction action)
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

            var panel = new GameObject("연습 안내", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(transform,false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f,0);
            panel.anchoredPosition = new Vector2(0,24);
            panel.sizeDelta = new Vector2(614,110);
            var plate = panel.gameObject.AddComponent<IdentityShape>();
            plate.frame = true; plate.radius = 20; plate.color = Ink; plate.insetColor = new Color(.235f,.14f,.23f,.97f);
            plate.raycastTarget = false;

            var keycap = TrainingUITheme.Pill("조작 키",panel,Mint,new Vector2(.036f,.20f),new Vector2(.286f,.80f));
            key = Label(keycap.transform,Vector2.zero,Vector2.one,26,Ink,TextAlignmentOptions.Center);
            counter = Label(panel,new Vector2(.325f,.57f),new Vector2(.80f,.87f),17,Mint,TextAlignmentOptions.Left);
            action = Label(panel,new Vector2(.325f,.16f),new Vector2(.965f,.57f),28,Cream,TextAlignmentOptions.Left);
            for (int i = 0; i < progress.Length; i++)
            {
                float x = .814f + .05f * i;
                progress[i] = TrainingUITheme.Pill("연습 진도",panel,Mint,new Vector2(x,.669f),new Vector2(x+.038f,.727f));
            }
            keys = new[] {
                string.Join(" ", new[]{KeyName(GameKeyAction.MoveUp),KeyName(GameKeyAction.MoveLeft),KeyName(GameKeyAction.MoveDown),KeyName(GameKeyAction.MoveRight)}),
                KeyName(GameKeyAction.Dash), KeyName(GameKeyAction.Active)
            };
            skillName = player.Skills.Definition.activeName;
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
            if (Step == 0)
            {
                if (player.EffectiveMoveDirection.sqrMagnitude > .04f)
                    walked += Vector2.Distance(player.Position,last);
                last = player.Position;
                if (walked >= .75f) Advance();
            }
            else if (Step == 1 && player.Skills.DashSequence != firstDash) Advance();
            else if (Step == 2 && player.Skills.Active)
            {
                finishAt = Time.time + (player.Skills.IsShini ? 2.6f : Mathf.Max(1.5f,player.Skills.SummonRemaining+.35f));
                Advance();
            }
        }

        void Advance() { Step++; Draw(); }
        void Draw()
        {
            bool done = Step == 3;
            key.text = done ? "완료" : keys[Step];
            counter.text = done ? "조작 연습 완료" : $"조작 연습   {Step+1} / 3";
            action.text = Step == 0 ? "캐릭터를 움직여 보세요" : Step == 1 ? "짧게 대시해 보세요" :
                Step == 2 ? $"{skillName} 사용하기" : "첫 도전 · 구강으로 출발!";
            for (int i = 0; i < progress.Length; i++)
                progress[i].color = i <= Step ? Mint : new Color(.44f,.33f,.42f);
        }

        public void Finish() { gameObject.SetActive(false); Destroy(gameObject); }
        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            foreach (var layer in hidden) if (layer != null) layer.enabled = true;
            hidden.Clear();
        }
    }
}
