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
            var parent=slots[0].transform.parent as RectTransform;if(parent==null)return;
            var layout=parent.GetComponent<LayoutGroup>();if(layout!=null)layout.enabled=false;
            var fitter=parent.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
            parent.anchorMin=parent.anchorMax=parent.pivot=new Vector2(.5f,0);
            parent.anchoredPosition=new Vector2(0,48);parent.localScale=Vector3.one;
            parent.sizeDelta=new Vector2(660,218);
            var tray=parent.Find("Light wood medicine tray") as RectTransform;
            foreach(Transform child in parent)
                if(child.GetComponent<InventorySlot>()==null&&child!=tray)child.gameObject.SetActive(false);
            if(tray==null)
            {
                tray=AugmentPanelTheme.Rect("Light wood medicine tray",parent);
                AugmentPanelTheme.Anchors(tray,Vector2.zero,Vector2.one);tray.SetAsFirstSibling();
                var image=tray.gameObject.AddComponent<Image>();image.sprite=Art(0);image.raycastTarget=false;
            }
            tray.gameObject.SetActive(true);
            bool mobile=GamePlatform.UsesTouchControls||MobileGameplayInput.Active;
            for(int i=0;i<4;i++)
            {
                var rect=(RectTransform)slots[i].transform;
                // Retain slot order, inventory counts, UseItem callbacks and desktop hotkeys.
                AugmentPanelTheme.Anchors(rect,new Vector2(.16f+i*.178f,.08f),new Vector2(.318f+i*.178f,.79f));
                var background=slots[i].GetComponent<Image>();if(background!=null)background.color=Color.clear;
                var icon=rect.Find("Image") as RectTransform;
                if(icon!=null){AugmentPanelTheme.Anchors(icon,new Vector2(.12f,.27f),new Vector2(.88f,.98f));icon.GetComponent<Image>().preserveAspect=true;}
                var group=rect.GetComponent<CanvasGroup>();if(group!=null)group.alpha=1;
                var oldSocket=rect.Find("Mobile translucent socket");if(oldSocket!=null)oldSocket.gameObject.SetActive(false);
                var button=rect.GetComponent<Button>();if(button!=null)button.targetGraphic=background;
                var label=rect.Find("Item caption")?.GetComponent<TextMeshProUGUI>();
                if(label==null)label=AugmentPanelTheme.Label("Item caption",rect,TrainingUITheme.Font,"",20);
                label.fontSize=label.fontSizeMax=mobile?20:13;label.fontSizeMin=mobile?15:10;
                string type=slots[i].CollectableType!=null?slots[i].CollectableType.name.ToLowerInvariant():"";
                string name=type.Contains("bomb")?"폭탄":type.Contains("magnet")?"자석":type.Contains("health")||type.Contains("heart")?"회복":"물약";
                label.text=mobile?name:$"{i+1} · {name}";
                AugmentPanelTheme.Anchors(label.rectTransform,new Vector2(-.02f,0),new Vector2(1.02f,.24f));
                label.color=OctoberArt.Ink;label.fontStyle=FontStyles.Bold;
            }
        }
        public static void InstallMobile(InventorySlot[] slots) => Install(slots);
    }

    // Same glyph in the mini-stage reward and the gameplay HUD.
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

    // Pixel hand geometry avoids loading another large texture on phones.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudInteractionIcon : MaskableGraphic
    {
        static readonly string[] Pixels={
            "....##......", "...#ww#.....", "...#ww#.....", "...#ww#.....",
            "...#ww###...", "...#ww#ww##.", ".###wwwwww#.", "#ww#wwwwww#.",
            "#wwwwwwwww#.", ".#wwwwwwww#.", "..#wwwwwww#.", "...#wwwww#..",
            "...#bbbbb#..", "...#######.."};
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();float cell=Mathf.Min(r.width/12,r.height/14);
            Vector2 origin=r.center-new Vector2(6,7)*cell;
            for(int y=0;y<Pixels.Length;y++)for(int x=0;x<Pixels[y].Length;x++)
            {
                char pixel=Pixels[y][x];if(pixel=='.')continue;
                Color tint=pixel=='#'?new Color(.035f,.07f,.12f):pixel=='b'?new Color(.62f,.78f,.9f):Color.white;
                int start=vh.currentVertCount;Vector2 p=origin+new Vector2(x,13-y)*cell;
                vh.AddVert(p,tint,Vector2.zero);vh.AddVert(p+Vector2.right*cell,tint,Vector2.zero);
                vh.AddVert(p+Vector2.one*cell,tint,Vector2.zero);vh.AddVert(p+Vector2.up*cell,tint,Vector2.zero);
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
