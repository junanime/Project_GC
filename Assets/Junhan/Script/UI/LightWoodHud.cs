using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public static class LightWoodHud
    {
        public static Sprite Art(int index) => AcidToadArt.Frame("LightWoodUI",index);
        public static void Install(InventorySlot[] slots)
        {
            if(slots==null||slots.Length!=4||slots[0]==null)return;
            if(GamePlatform.UsesTouchControls||MobileGameplayInput.Active){InstallMobile(slots);return;}
            var parent=slots[0].transform.parent as RectTransform;if(parent==null)return;
            if(parent.Find("Light wood medicine tray")!=null)return;
            var layout=parent.GetComponent<LayoutGroup>();if(layout!=null)layout.enabled=false;
            var fitter=parent.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
            parent.sizeDelta=new Vector2(340,116);
            var tray=AugmentPanelTheme.Rect("Light wood medicine tray",parent);
            AugmentPanelTheme.Anchors(tray,Vector2.zero,Vector2.one);tray.SetAsFirstSibling();
            var image=tray.gameObject.AddComponent<Image>();image.sprite=Art(0);image.raycastTarget=false;
            for(int i=0;i<4;i++)
            {
                var rect=(RectTransform)slots[i].transform;
                AugmentPanelTheme.Anchors(rect,new Vector2(.15f+i*.18f,.23f),new Vector2(.31f+i*.18f,.8f));
                var background=slots[i].GetComponent<Image>();if(background!=null)background.color=Color.clear;
                var label=AugmentPanelTheme.Label("Item key",tray,TrainingUITheme.Font,(i+1).ToString(),13);
                AugmentPanelTheme.Anchors(label.rectTransform,new Vector2(.15f+i*.18f,.025f),new Vector2(.31f+i*.18f,.22f));
                label.color=OctoberArt.Ink;label.fontStyle=FontStyles.Bold;
            }
        }

        public static void InstallMobile(InventorySlot[] slots)
        {
            if(slots==null||slots.Length!=4||slots[0]==null)return;
            var parent=slots[0].transform.parent as RectTransform;if(parent==null)return;
            var layout=parent.GetComponent<LayoutGroup>();if(layout!=null)layout.enabled=false;
            var fitter=parent.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
            parent.sizeDelta=new Vector2(352,352);
            // Old directional decor and the desktop tray are replaced, not their item slots.
            foreach(Transform child in parent)
                if(child.GetComponent<InventorySlot>()==null)child.gameObject.SetActive(false);
            Vector2[] offsets={new Vector2(0,116),new Vector2(116,0),new Vector2(0,-116),new Vector2(-116,0)};
            for(int i=0;i<slots.Length;i++)
            {
                var rect=(RectTransform)slots[i].transform;
                rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
                rect.anchoredPosition=offsets[i];rect.sizeDelta=Vector2.one*112;rect.localScale=Vector3.one;
                var background=rect.GetComponent<Image>();if(background!=null)background.color=Color.clear;
                if(rect.Find("Mobile translucent socket")==null)
                {
                    var socket=AugmentPanelTheme.Rect("Mobile translucent socket",rect);
                    AugmentPanelTheme.Anchors(socket,Vector2.zero,Vector2.one);socket.SetAsFirstSibling();
                    var disc=socket.gameObject.AddComponent<MobileControlDisc>();
                    disc.color=new Color(.15f,.20f,.19f,.42f);disc.raycastTarget=false;
                }
                var group=rect.GetComponent<CanvasGroup>();if(group==null)group=rect.gameObject.AddComponent<CanvasGroup>();
                group.alpha=.85f;
                var button=rect.GetComponent<Button>();
                if(button!=null)button.targetGraphic=rect.Find("Mobile translucent socket").GetComponent<MobileControlDisc>();
                var icon=rect.Find("Image") as RectTransform;
                if(icon!=null){icon.localScale=Vector3.one*.68f;var image=icon.GetComponent<Image>();if(image!=null)image.preserveAspect=true;}
            }
        }
    }
    // Temporary shared dash glyph, authored as simple geometry rather than a character-specific icon.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SharedDashIcon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();
            for(int i=0;i<2;i++)
            {
                Vector2[] points={new Vector2(.12f+i*.38f,.2f),new Vector2(.30f+i*.38f,.5f),new Vector2(.12f+i*.38f,.8f),new Vector2(.29f+i*.38f,.8f),new Vector2(.49f+i*.38f,.5f),new Vector2(.29f+i*.38f,.2f)};
                int start=vh.currentVertCount;
                foreach(var p in points)vh.AddVert(new Vector3(r.x+p.x*r.width,r.y+p.y*r.height),color,Vector2.zero);
                vh.AddTriangle(start,start+1,start+5);vh.AddTriangle(start+1,start+4,start+5);vh.AddTriangle(start+1,start+2,start+3);vh.AddTriangle(start+1,start+3,start+4);
            }
        }
    }
}
