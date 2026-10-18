using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        StitchPatchGraphic StitchBox(string name,float x,float y,float r,float t,Color color,bool corners=false)
        {
            var holder=Rect(name,content,x,y,r,t);
            // Draw seams above the paper, below labels, controls and bookmarks.
            holder.SetSiblingIndex(Page=="settings"?3:1);
            return StitchPatchTheme.Attach(holder,color,false,corners);
        }
        void StitchMotif(string key,string name,float x,float y,float size=25)
        {
            var holder=Rect("Character applique "+name,content,x,y,x,y);holder.sizeDelta=new Vector2(size,size);
            var image=ImageAt(holder,StitchPatchTheme.Motif(key),0,0,1,1);image.name="Motif "+key;
        }
        void Sticker(float x,float y,float size,Color color)
        {
            var h=Rect("Star applique",content,x,y,x,y);h.sizeDelta=Vector2.one*size;
            var g=h.gameObject.AddComponent<StitchPatchGraphic>();g.starOnly=true;g.accent=color;g.raycastTarget=false;
        }
        void ApplyStitchTheme()
        {
            if(content==null || Page!="main"&&Page!="prepare"&&Page!="run"&&Page!="settings")return;
            foreach(var f in content.GetComponentsInChildren<ApothecaryButtonFeedback>(true))
            {
                bool slot=f.name.StartsWith("Choice ")||f.name.StartsWith("Loadout ")||f.name.StartsWith("Record ")&&f.name.Contains("slot ")||f.name.Contains("skill slot");
                bool bookmark=f.name.EndsWith("bookmark");
                // Preserve the enlarged bookmark silhouette and inventory hit rectangles.
                if(bookmark)
                {
                    var g=StitchPatchTheme.Attach(f.visual,StitchPatchTheme.Thread);g.bookmarkOnly=true;g.accent=f.chosen?Color.white:new Color(.90f,.60f,.30f);
                    continue;
                }
                if(f.name=="Record close"||f.name.Contains("skill slot")||f.name.StartsWith("Record ")&&f.name.Contains("slot "))continue;
                bool smallToolbar=f.name=="Record consumables"||f.name=="Record guide";
                StitchPatchTheme.Button(f,f.primary||f.chosen,!slot&&!smallToolbar);
                if(slot)
                    foreach(var label in f.visual.GetComponentsInChildren<TextMeshProUGUI>())
                        if(!string.IsNullOrEmpty(label.text)&&label.rectTransform.anchorMax.y<.25f)
                        {var r=label.rectTransform;r.anchorMin+=Vector2.up*.025f;r.anchorMax+=Vector2.up*.025f;}
                if(!slot)
                    foreach(var shape in f.visual.GetComponentsInChildren<IdentityShape>())shape.enabled=false;
            }
            foreach(var image in content.GetComponentsInChildren<Image>().ToArray())
                if(image.sprite==Config.scrollPanel&&image.name=="Art"&&image.transform.parent==content)
                {
                    bool outer=Page=="prepare"&&image.rectTransform.anchorMin.x==.10f;
                    image.enabled=false;
                    var skin=StitchPatchTheme.Attach(image.transform,outer?StitchPatchTheme.Coral:StitchPatchTheme.Thread,true,outer);
                    skin.inset=outer?10:8;skin.radius=outer?18:12;skin.cornerButtons=outer;
                }
            foreach(var shape in content.GetComponentsInChildren<IdentityShape>().ToArray())
                if(shape.name.StartsWith("Category "))
                {
                    shape.enabled=false;
                    foreach(var text in shape.GetComponentsInChildren<TextMeshProUGUI>())text.color=Color.white;
                    var seam=StitchPatchTheme.Attach(shape.transform,StitchPatchTheme.Coral,true);seam.paper=StitchPatchTheme.Coral;seam.lightThread=true;seam.inset=5;seam.radius=13;seam.transform.SetAsFirstSibling();
                }
            if(Page=="prepare")
            {
                foreach(var banner in content.GetComponentsInChildren<Image>().Where(i=>i.sprite==OctoberArt.Get("OctoberUI/Banner")))
                    StitchPatchTheme.Attach(banner.transform,StitchPatchTheme.Coral).bannerOnly=true;
                string key=OctoberArt.CharacterKey(character);
                // Only these small appliques change with the character, never the shared panel skin.
                StitchMotif(key,"tab left",.466f,.793f,25);StitchMotif(key,"tab right",.884f,.793f,25);
                StitchMotif(key,"stats top left",.132f,.499f);StitchMotif(key,"stats top right",.433f,.499f);
                StitchMotif(key,"stats bottom left",.132f,.339f);StitchMotif(key,"stats bottom right",.433f,.339f);
                StitchMotif(key,"description top left",.470f,.352f,22);StitchMotif(key,"description bottom right",.874f,.171f);
                Sticker(.131f,.795f,22,new Color(1,.79f,.38f));Sticker(.133f,.728f,18,new Color(1,.57f,.63f));Sticker(.131f,.584f,17,StitchPatchTheme.Mint);
                Sticker(.882f,.623f,18,new Color(1,.79f,.38f));
                SilverStitchPanel();
            }
            if(Page=="run"||Page=="settings")
            {
                var outer=StitchBox("Book outer stitch",.023f,.031f,.976f,.907f,StitchPatchTheme.Coral,true);outer.titleGap=true;outer.inset=6;outer.cornerButtons=true;
                var bannerHolder=Rect("Book ribbon seam",content,.30f,.82f,.70f,1.01f);
                var bookRibbon=StitchPatchTheme.Attach(bannerHolder,StitchPatchTheme.Coral);bookRibbon.bannerOnly=true;bookRibbon.bannerStars=false;bookRibbon.bannerBottom=.44f;
                if(Page=="run")
                {
                    // Compartments match the approved exploration record backplate.
                    StitchBox("Character section stitch",.035f,.401f,.474f,.866f,StitchPatchTheme.Thread).hideBottom=true;
                    StitchBox("Map section stitch",.483f,.401f,.963f,.866f,StitchPatchTheme.Thread);
                    StitchBox("Map frame stitch",.493f,.415f,.855f,.799f,StitchPatchTheme.Thread);
                    foreach(var span in new[]{new Vector2(.035f,.347f),new Vector2(.355f,.645f),new Vector2(.653f,.964f)})
                    {StitchBox("Inventory stitch",span.x,.063f,span.y,.390f,StitchPatchTheme.Thread);}
                    BookAppliques();
                }
                else
                {
                    StitchBox("Settings section stitch",.267f,.227f,.918f,.843f,StitchPatchTheme.Thread);
                    StitchBox("Settings navigation stitch",.047f,.216f,.250f,.843f,StitchPatchTheme.Thread);
                    // Subtle parchment tone shared with the record page, no flat dark settings skin.
                    var parchment=content.GetComponentsInChildren<IdentityShape>().FirstOrDefault(s=>s.name=="Settings parchment");
                    if(parchment!=null){parchment.color=new Color(1,.951f,.837f);parchment.glossy=true;}
                }
            }
        }
        void BookAppliques()
        {
            // Small decorations live in the margins, never in the slot or description rectangles.
            foreach(var point in new[]{new Vector2(.046f,.850f),new Vector2(.461f,.850f),new Vector2(.046f,.425f),
                new Vector2(.951f,.850f),new Vector2(.951f,.425f),new Vector2(.331f,.365f),new Vector2(.627f,.365f),
                new Vector2(.950f,.365f),new Vector2(.048f,.079f),new Vector2(.330f,.079f),new Vector2(.950f,.079f)})
            {
                var holder=Rect("Book leaf applique",content,point.x,point.y,point.x,point.y);holder.sizeDelta=Vector2.one*20;
                ImageAt(holder,StitchPatchTheme.Motif("Ashi"),0,0,1,1).color=new Color(.80f,.88f,.75f,.85f);
            }
            foreach(var point in new[]{new Vector2(.051f,.832f),new Vector2(.496f,.837f),new Vector2(.049f,.363f),new Vector2(.371f,.365f),new Vector2(.670f,.364f)})
                {
                    var pad=Rect("Book emblem backing",content,point.x,point.y,point.x,point.y);pad.sizeDelta=Vector2.one*21;
                    var shape=pad.gameObject.AddComponent<IdentityShape>();shape.color=new Color(1,.954f,.84f);shape.radius=11;shape.raycastTarget=false;
                    Sticker(point.x,point.y,22,new Color(1,.78f,.36f));
                }
        }
        void SilverStitchPanel()
        {
            // A separate sibling dock: editing or moving the currency never changes the backplate.
            var dock=Rect("Silver currency dock",content,.739f,.857f,.905f,.945f);
            var frame=StitchPatchTheme.Attach(dock,new Color(.70f,.76f,.80f),true);frame.paper=new Color(1,.955f,.86f);frame.radius=13;
            ImageAt(dock,CurrencyJarArt.Coin(true),.065f,.19f,.27f,.81f).name="Silver foil coin";
            if(silverLabel!=null)
            {
                silverLabel.transform.SetParent(dock,false);silverLabel.rectTransform.anchorMin=new Vector2(.28f,.12f);silverLabel.rectTransform.anchorMax=new Vector2(.94f,.88f);
                silverLabel.rectTransform.offsetMin=silverLabel.rectTransform.offsetMax=Vector2.zero;
                silverLabel.fontSizeMax=20;silverLabel.fontSizeMin=12;silverLabel.enableWordWrapping=false;
            }
        }
    }
}
