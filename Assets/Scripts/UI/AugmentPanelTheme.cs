using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Art is imported as cropped sprites; the original generated PNGs stay untouched.
    public static class AugmentPanelTheme
    {
        public static readonly Color Paper = new Color(1f, .95f, .82f);
        public static readonly Color Jade = new Color(.52f, .79f, .67f);
        public static Sprite Frame(AugmentUpgradeGrade grade)
        {
            return Resources.Load<Sprite>("AugmentPanels/" + grade);
        }
        public static Color Accent(AugmentUpgradeGrade grade)
        {
            switch (grade)
            {
                case AugmentUpgradeGrade.Rare: return new Color(.32f,.65f,1);
                case AugmentUpgradeGrade.Epic: return new Color(.79f,.49f,1);
                case AugmentUpgradeGrade.Supreme: return new Color(.92f,.19f,.28f);
                case AugmentUpgradeGrade.Original: return Jade;
                case AugmentUpgradeGrade.Legendary: return new Color(1,.8f,.3f);
                default: return new Color(.76f,.81f,.81f);
            }
        }
        public static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }
        public static void Anchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = Vector2.one * .5f;
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
        }
        public static void Box(RectTransform rect, Vector2 position, Vector2 size)
        {
            Anchors(rect, Vector2.one * .5f, Vector2.one * .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        public static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, string value, float size)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = value; label.fontSize = size; label.color = Paper;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.enableAutoSizing = true; label.fontSizeMin = size * .75f; label.fontSizeMax = size;
            return label;
        }
        public static Image Fill(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }
    }
}
