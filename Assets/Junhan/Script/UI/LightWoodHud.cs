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
            parent.anchoredPosition=new Vector2(0,40);parent.localScale=Vector3.one;
            parent.sizeDelta=new Vector2(560,154);
            foreach(Transform child in parent)
                if(child.GetComponent<InventorySlot>()==null)child.gameObject.SetActive(false);
            var character=CrossSceneData.CharacterBlueprint;
            for(int i=0;i<4;i++)
            {
                var slot=slots[i];var rect=(RectTransform)slot.transform;
                AugmentPanelTheme.Anchors(rect,new Vector2(.055f+i*.225f,.04f),new Vector2(.27f+i*.225f,.97f));
                var background=slot.GetComponent<Image>();if(background!=null)background.color=Color.clear;
                var face=rect.Find("Character item card")?.GetComponent<HudPaperGraphic>();
                if(face==null)face=CharacterHudTheme.PaperAt("Character item card",rect,CharacterHudTheme.Edge(character));
                AugmentPanelTheme.Anchors(face.rectTransform,Vector2.zero,Vector2.one);face.transform.SetAsFirstSibling();
                var icon=slot.IconImage;
                if(icon!=null)
                {
                    var ir=icon.rectTransform;ir.anchorMin=ir.anchorMax=ir.pivot=new Vector2(.5f,.5f);
                    ir.anchoredPosition=new Vector2(0,20);ir.sizeDelta=new Vector2(66,66);ir.localScale=Vector3.one;
                    icon.preserveAspect=true;icon.raycastTarget=false;
                }
                // The original counter still maintains inventory state; the presentation displays 0/capacity too.
                if(slot.CountRect!=null)foreach(var graphic in slot.CountRect.GetComponentsInChildren<Graphic>(true))graphic.enabled=false;
                var count=rect.Find("Card stock")?.GetComponent<TextMeshProUGUI>();
                if(count==null)count=AugmentPanelTheme.Label("Card stock",rect,TrainingUITheme.Font,"",19);
                AugmentPanelTheme.Anchors(count.rectTransform,new Vector2(.08f,.20f),new Vector2(.92f,.40f));
                count.color=OctoberArt.Ink;count.fontStyle=FontStyles.Bold;
                var oldSocket=rect.Find("Mobile translucent socket");if(oldSocket!=null)oldSocket.gameObject.SetActive(false);
                var group=rect.GetComponent<CanvasGroup>();if(group!=null)group.alpha=1;
                var button=rect.GetComponent<Button>();if(button!=null)button.targetGraphic=background;
                var key=rect.Find("Card hotkey")?.GetComponent<HudPaperGraphic>();
                if(key==null)key=CharacterHudTheme.PaperAt("Card hotkey",rect,new Color(.79f,.34f,.35f));
                key.color=CharacterHudTheme.Coral;
                AugmentPanelTheme.Anchors(key.rectTransform,new Vector2(.15f,-.04f),new Vector2(.85f,.17f));
                var label=rect.Find("Item caption")?.GetComponent<TextMeshProUGUI>();
                if(label==null)label=AugmentPanelTheme.Label("Item caption",rect,TrainingUITheme.Font,"",16);
                AugmentPanelTheme.Anchors(label.rectTransform,new Vector2(.16f,-.035f),new Vector2(.84f,.17f));
                label.color=Color.white;label.fontStyle=FontStyles.Bold;label.transform.SetAsLastSibling();
                label.fontSize=label.fontSizeMax=16;label.fontSizeMin=12;label.enableAutoSizing=true;
                label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Ellipsis;
                var presentation=slot.GetComponent<InventoryHudCard>();if(presentation==null)presentation=slot.gameObject.AddComponent<InventoryHudCard>();
                presentation.Configure(slot,face,count,label,i);
                int index=i;HudTooltip.Bind(slot.gameObject,()=>ConsumableHelp.Title(slot),()=>ConsumableHelp.Body(slot,index));
            }
            for(int side=0;side<2;side++)
            {
                string name="Character inventory ornament "+side;
                var ornament=parent.Find(name)?.GetComponent<HudOrnament>();
                if(ornament==null)ornament=CharacterHudTheme.Ornament(name,parent,character);
                ornament.gameObject.SetActive(true);
                var r=ornament.rectTransform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(side,0);
                r.sizeDelta=new Vector2(44,44);r.anchoredPosition=new Vector2(side==0?10:-10,6);
                r.localRotation=Quaternion.Euler(0,0,side==0?30:-30);
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
