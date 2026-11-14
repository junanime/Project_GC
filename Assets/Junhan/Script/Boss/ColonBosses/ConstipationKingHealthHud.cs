using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed class ConstipationKingHealthHud : MonoBehaviour
    {
        ConstipationKingRuntime boss; GameObject root; FoodBossHealthRail rail; TextMeshProUGUI title, detail;
        public void Initialize(ConstipationKingRuntime owner)
        {
            boss = owner; root = new GameObject("변비왕 · final boss health", typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 22;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("Final encounter", typeof(RectTransform)).GetComponent<RectTransform>(); panel.SetParent(root.transform, false);
            panel.anchorMin = new Vector2(.025f, .86f); panel.anchorMax = new Vector2(.34f, .86f); panel.pivot = new Vector2(.5f, 0); panel.offsetMin = Vector2.zero; panel.offsetMax = new Vector2(0, 22);
            rail = new GameObject("Health", typeof(RectTransform), typeof(CanvasRenderer), typeof(FoodBossHealthRail)).GetComponent<FoodBossHealthRail>(); rail.transform.SetParent(panel, false);
            rail.rectTransform.anchorMin = Vector2.zero; rail.rectTransform.anchorMax = Vector2.one; rail.rectTransform.offsetMin = rail.rectTransform.offsetMax = Vector2.zero; rail.raycastTarget = false;
            title = Label(panel, 26, 51, 20); detail = Label(panel, -25, -1, 12);
        }
        TextMeshProUGUI Label(RectTransform parent, float bottom, float top, float size)
        {
            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); label.transform.SetParent(parent, false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.right; label.rectTransform.offsetMin = new Vector2(0, bottom); label.rectTransform.offsetMax = new Vector2(0, top);
            if (ApothecaryUI.Instance != null) label.font = ApothecaryUI.Instance.Config.font;
            label.fontSize = size; label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Center; label.color = new Color(1, .95f, .8f); label.raycastTarget = false;
            return label;
        }
        void LateUpdate()
        {
            if (boss == null || root == null) return;
            root.SetActive(!boss.Dead && !MiniStageRuntimeState.IsInsideMiniStage && (ApothecaryUI.Instance == null || ApothecaryUI.Instance.Page == "hud"));
            rail.Set(boss.Health / boss.MaxHealth, boss.PhaseTwo ? new Color(.75f, .24f, .14f) : new Color(.76f, .53f, .16f), boss.Transitioning ? .45f : 0);
            title.text = "변비왕 · " + (boss.PhaseTwo ? "2" : "1") + "페이즈";
            detail.text = boss.Transitioning ? "왕의 수압 상승 · 잠시 무적" : boss.CurrentPattern == 7 ? "진상품 운반 몬스터를 처치해 회복을 막으세요" : "필수 최종보스 · 지팡이 열쇠로 출구 봉인 해제";
        }
        void OnDisable() { if (root != null) root.SetActive(false); }
        void OnDestroy() { if (root != null) Destroy(root); }
    }
}
