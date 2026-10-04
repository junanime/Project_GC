using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.EditorTools
{
    public static class OctoberContentInstaller
    {
        [MenuItem("Tools/October Content/Import approved artwork")]
        public static void Install()
        {
            AssetDatabase.Refresh();
            foreach(string folder in new[]{"Assets/Resources/OctoberUI","Assets/Resources/OctoberContent","Assets/Resources/AugmentPanels"})
                foreach(string path in Directory.GetFiles(folder,"*.png",SearchOption.AllDirectories))Import(path.Replace('\\','/'));
            var config=AssetDatabase.LoadAssetAtPath<ApothecaryUIConfig>(ApothecaryUIInstaller.ConfigPath);
            config.mainBackground=OctoberArt.Get("OctoberUI/MainBackground");
            config.panelBackground=config.bookBackground=config.mainBackground;
            config.scrollPanel=config.inventorySlot=OctoberArt.Get("OctoberUI/Panel");
            config.primaryButton=OctoberArt.Button(true);config.buttonBody=OctoberArt.Button(false);
            config.sectionRibbon=OctoberArt.Get("OctoberUI/Banner");
            InstallItems(config);
            config.failureAshi=OctoberArt.Character(config.characters.First(),1);
            foreach(var relic in config.relics)
            {
                if (!string.IsNullOrEmpty(relic.effectDescription)) continue; // Preserve the reviewed 34-relic catalog.
                string key=relic.effectType==RelicBlueprint.RelicEffectType.MaxHealth?"RelicHealth":relic.effectType==RelicBlueprint.RelicEffectType.MoveSpeed?"RelicSpeed":"RelicCrit";
                relic.icon=OctoberArt.Get("OctoberUI/"+key);
                relic.relicName=key=="RelicHealth"?"튼튼 하트":key=="RelicSpeed"?"산들 날개":"반짝 급소";
                EditorUtility.SetDirty(relic);
            }
            config.weapons=AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Select(g=>g.GetComponent<SyringeSpecialAugmentAbility>())
                .Where(a=>a!=null).GroupBy(a=>a.Type).Select(g=>g.First()).OrderBy(a=>(int)a.Type).ToArray();
            config.basicNeedle=AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Select(g=>g.GetComponent<SyringeDartAbility>()).Where(a=>a!=null&&a.Image!=null).Select(a=>a.Image).FirstOrDefault();
            EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
            Debug.Log("[October] Approved artwork imported and shared UI configured.");
        }
        static Rect Bounds(Texture2D texture,Rect cell)
        {
            int x0=(int)cell.x,y0=(int)cell.y,w=(int)cell.width,h=(int)cell.height;
            var pixels=texture.GetPixels32();int left=w,right=-1,bottom=h,top=-1;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(pixels[(y0+y)*texture.width+x0+x].a>12)
            {left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
            return right<left?cell:new Rect(x0+left,y0+bottom,right-left+1,top-bottom+1);
        }
        static void Import(string path)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.isReadable=true;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=4096;importer.spritePixelsPerUnit=100;importer.SaveAndReimport();
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);string name=Path.GetFileNameWithoutExtension(path);
            SpriteMetaData[] slices=null;
            if(name=="MapSymbols")
            {
                string[] names={"Boss","Player","Portal","Merchant","Vending","Germ"};
                slices=Enumerable.Range(0,6).Select(i=>new SpriteMetaData{name=names[i],rect=Bounds(tex,new Rect((i%3)*(tex.width/3),(1-i/3)*(tex.height/2),tex.width/3,tex.height/2)),alignment=9,pivot=Vector2.one*.5f}).ToArray();
            }
            else if(name.EndsWith("States")&&name!="ButtonStates")
            {
                // Equal cells and pivots preserve scale and placement across character expressions.
                slices=Enumerable.Range(0,3).Select(i=>new SpriteMetaData{name=i.ToString(),rect=name=="WarningStates"?Bounds(tex,new Rect(i*(tex.width/3),0,tex.width/3,tex.height)):new Rect(i*(tex.width/3),0,tex.width/3,tex.height),alignment=9,pivot=Vector2.one*.5f}).ToArray();
            }
            else if(name=="ButtonStates")
                slices=Enumerable.Range(0,2).Select(i=>new SpriteMetaData{name=i==0?"Active":"Idle",rect=Bounds(tex,new Rect(0,(1-i)*tex.height/2,tex.width,tex.height/2)),alignment=9,pivot=Vector2.one*.5f,border=new Vector4(140,50,140,50)}).ToArray();
            else if(!name.Contains("Background")&&!path.Contains("/Rooms/"))
            {
                var rect=Bounds(tex,new Rect(0,0,tex.width,tex.height));
                slices=new[]{new SpriteMetaData{name=name,rect=rect,alignment=9,pivot=Vector2.one*.5f,border=name=="Panel"?new Vector4(100,100,100,100):Vector4.zero}};
            }
            if(slices!=null){importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritesheet=slices;}
            // Icons never render larger than ~220 pixels. Retain source pixels on disk,
            // but avoid loading 66 full-resolution 1254px textures into a mobile run.
            importer.maxTextureSize=path.Contains("/OctoberContent/Items/")||path.Contains("/OctoberContent/Food/")?512:4096;
            importer.isReadable=false;importer.SaveAndReimport();
        }
        public static void InstallBatch(){Install();EditorApplication.Exit(0);}
        [Serializable] class SourceCatalog{public SourceItem[] items;}
        [Serializable] class SourceItem{public string id,name,rarity,effect,role;public int sourceSheet,sourceRow;}
        static void InstallItems(ApothecaryUIConfig config)
        {
            string dir="Assets/Resources/OctoberContent/Catalog";Directory.CreateDirectory(dir);AssetDatabase.Refresh();
            var source=JsonUtility.FromJson<SourceCatalog>(File.ReadAllText("Assets/Resources/OctoberContent/ItemCatalogSource.json"));
            if(source.items.Length!=66||OctoberItemBalance.Description.Length!=67)throw new Exception("Item source/implementation count mismatch");
            var result=new MerchantItemBlueprint[66];
            for(int i=0;i<66;i++)
            {
                var entry=source.items[i];string path=dir+"/"+entry.id+".asset";
                var item=AssetDatabase.LoadAssetAtPath<MerchantItemBlueprint>(path);
                if(item==null){item=ScriptableObject.CreateInstance<MerchantItemBlueprint>();AssetDatabase.CreateAsset(item,path);}
                item.octoberId=i+1;item.itemName=entry.name;item.name=entry.id;
                item.itemRarity=entry.rarity=="전설"?MerchantItemBlueprint.Rarity.Legendary:entry.rarity=="영웅"?MerchantItemBlueprint.Rarity.Rare:entry.rarity=="희귀"?MerchantItemBlueprint.Rarity.Uncommon:MerchantItemBlueprint.Rarity.Common;
                item.description=OctoberItemBalance.Description[i+1];item.itemIcon=OctoberArt.Get("OctoberContent/Items/"+entry.id);
                item.canBuyInLobby=true;int grade=(int)item.itemRarity;
                item.cost=new[]{25,45,80,130}[grade];item.silverCost=new[]{80,150,320,650}[grade];
                item.itemTag=(ItemTag)(entry.role.Contains("회복")?1:entry.role.Contains("생존")||entry.role.Contains("방어")?2:entry.role.Contains("공격")?0:4);
                EditorUtility.SetDirty(item);result[i]=item;
            }
            config.items=result;
        }
    }
}
