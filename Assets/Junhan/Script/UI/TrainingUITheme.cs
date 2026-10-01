using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // One dynamic atlas is shared by the card and compact HUD labels.
    public static class TrainingUITheme
    {
        static TMP_FontAsset font;
        public static TMP_FontAsset Font
        {
            get
            {
                if (font != null) return font;
                var source = Resources.Load<Font>("TrainingUI/Cafe24Ssurround");
                if (source == null) return TMP_Settings.defaultFontAsset;
                font = TMP_FontAsset.CreateFontAsset(source);
                font.name = "Cafe24 Ssurround Runtime";
                font.isMultiAtlasTexturesEnabled = true;
                return font;
            }
        }

        public static IdentityShape Pill(string name, Transform parent, Color color, Vector2 min, Vector2 max)
        {
            var rect = AugmentPanelTheme.Rect(name,parent);
            AugmentPanelTheme.Anchors(rect,min,max);
            var shape = rect.gameObject.AddComponent<IdentityShape>();
            shape.color=color; shape.radius=24; shape.raycastTarget=false;
            return shape;
        }

        public static void Text(TextMeshProUGUI text, float maximum, Color color)
        {
            text.font=Font; text.fontSharedMaterial=Font.material; text.fontStyle=FontStyles.Normal; text.color=color;
            text.enableAutoSizing=true; text.fontSizeMin=12; text.fontSizeMax=maximum;
            text.raycastTarget=false; text.characterSpacing=0; text.margin=Vector4.zero;
        }

        // Keep existing serialized counters alive so all gameplay updates still target them.
        public static void Counter(TextMeshProUGUI text, string iconName, float rightOffset)
        {
            if (text==null || text.transform.parent==null) return;
            foreach (Transform old in text.transform) old.gameObject.SetActive(false);
            var parent=text.transform.parent;
            var plate=AugmentPanelTheme.Rect("Training HUD "+iconName,parent);
            plate.anchorMin=plate.anchorMax=plate.pivot=new Vector2(1,1);
            plate.anchoredPosition=new Vector2(-rightOffset,-42); plate.sizeDelta=new Vector2(122,34);
            var shape=plate.gameObject.AddComponent<IdentityShape>();
            shape.frame=true;shape.radius=14;shape.color=new Color(.15f,.08f,.16f,.90f);
            shape.insetColor=new Color(.25f,.12f,.23f,.80f);shape.raycastTarget=false;
            var icon=AugmentPanelTheme.Fill("Counter icon",plate,Color.white);
            icon.sprite=OctoberArt.Get("TrainingUI/"+iconName);icon.preserveAspect=true;
            AugmentPanelTheme.Anchors(icon.rectTransform,new Vector2(.04f,.08f),new Vector2(.28f,.92f));
            text.transform.SetParent(plate,false);
            AugmentPanelTheme.Anchors(text.rectTransform,new Vector2(.32f,.04f),new Vector2(.95f,.96f));
            Text(text,23,new Color(1,.97f,.82f));text.alignment=TextAlignmentOptions.Right;
            text.enableWordWrapping=false;text.overflowMode=TextOverflowModes.Ellipsis;
        }

        public static void LevelBadge(TextMeshProUGUI text)
        {
            if(text==null || text.transform.parent==null)return;
            var plate=AugmentPanelTheme.Rect("Training HUD level",text.transform.parent);
            plate.anchorMin=plate.anchorMax=plate.pivot=new Vector2(1,1);
            plate.anchoredPosition=new Vector2(-22,-8);plate.sizeDelta=new Vector2(86,30);
            var shape=plate.gameObject.AddComponent<IdentityShape>();shape.frame=true;shape.radius=11;
            shape.color=new Color(.26f,.16f,.51f);shape.insetColor=new Color(.42f,.29f,.71f);shape.raycastTarget=false;
            text.transform.SetParent(plate,false);
            AugmentPanelTheme.Anchors(text.rectTransform,new Vector2(.04f,.04f),new Vector2(.96f,.96f));
            Text(text,21,new Color(1,.97f,.83f));text.alignment=TextAlignmentOptions.Center;text.enableWordWrapping=false;
        }
    }
}
