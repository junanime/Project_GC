using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vampire;
using Object = UnityEngine.Object;

public static class StageTwoInstaller
{
    const string Root = "Assets/Resources/Stage2Snails/";
    public static readonly string[] Keys = { "00-cinnamon", "01-strawberry-donut", "02-pistachio-macaron", "03-honey-waffle", "04-kimbap", "05-steamed-dumpling", "06-cheeseburger", "07-watermelon", "08-caramel-pudding" };
    public static readonly string[] Names = { "시나몬롤", "딸기 도넛", "피스타치오 마카롱", "꿀버터 와플", "김밥", "찐만두", "치즈버거", "수박", "캐러멜 푸딩" };
    [MenuItem("Tools/Junhan2/Install stage 2")]
    public static void Install()
    {
        AssetDatabase.Refresh();
        var body = Import("Body", FoodSnailMonster.BodyWidth);
        var shells = Keys.Select(key => Import(key, FoodSnailMonster.ShellWidth)).ToArray();
        var crater = Import("Crater", 4.8f);
        string creamPath = Root + "Cream.png";
        var ci = (TextureImporter)AssetImporter.GetAtPath(creamPath);
        ci.textureType = TextureImporterType.Default; ci.mipmapEnabled = true;
        ci.wrapMode = TextureWrapMode.Mirror; ci.filterMode = FilterMode.Bilinear; ci.maxTextureSize = 1024;
        ci.textureCompression = TextureImporterCompression.Compressed; ci.SaveAndReimport();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Default Melee Monster.prefab");
        var go = Object.Instantiate(source); go.name = "Food snail";
        var old = go.GetComponent<MeleeMonster>();
        var monster = go.AddComponent<FoodSnailMonster>();
        EditorUtility.CopySerializedManagedFieldsOnly(old, monster);
        Object.DestroyImmediate(old);
        var renderer = go.GetComponentInChildren<SpriteRenderer>(); renderer.sprite = body;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "FoodSnail.prefab"); Object.DestroyImmediate(go);
        const string defPath = "Assets/Resources/StageTwoDefinition.asset";
        var definition = AssetDatabase.LoadAssetAtPath<StageTwoDefinition>(defPath);
        if (definition == null) { definition = ScriptableObject.CreateInstance<StageTwoDefinition>(); AssetDatabase.CreateAsset(definition, defPath); }
        definition.snailPrefab = prefab; definition.body = body; definition.shells = shells; definition.names = Names;
        definition.crater = crater; definition.cream = AssetDatabase.LoadAssetAtPath<Texture2D>(creamPath);
        definition.rewardChest = AssetDatabase.LoadAssetAtPath<ChestBlueprint>("Assets/Blueprints/Chests/Boss Chest.asset");
        EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
        BakeBounds(new[] { body, crater }.Concat(shells).ToArray());
        Debug.Log("STAGE_TWO_INSTALL_PASS");
    }
    static Sprite Import(string key, float width)
    {
        string path = Root + key + ".png";
        var source = new Texture2D(2, 2); source.LoadImage(File.ReadAllBytes(path));
        var pixels = source.GetPixels32(); int x0=source.width,y0=source.height,x1=0,y1=0;
        for(int y=0;y<source.height;y++) for(int x=0;x<source.width;x++)
            if(pixels[y*source.width+x].a>32){x0=Mathf.Min(x0,x);x1=Mathf.Max(x1,x);y0=Mathf.Min(y0,y);y1=Mathf.Max(y1,y);}
        if(x0<=0||y0<=0||x1<=x0)throw new Exception("Expected padded transparent sprite: "+key);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.npotScale=TextureImporterNPOTScale.None;
        importer.filterMode=FilterMode.Point; importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=512; importer.SaveAndReimport();
        // Unity scales the runtime sprite PPU along with a downsampled texture.
        // Import settings therefore use source pixels, not the 512 px runtime image.
        importer.spritePixelsPerUnit=(x1-x0+1)/width;
        var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteAlignment=(int)SpriteAlignment.Custom;
        settings.spritePivot=new Vector2((x0+x1+1f)*.5f/source.width,(float)y0/source.height);
        settings.spriteMeshType=SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings); importer.SaveAndReimport(); Object.DestroyImmediate(source);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static void BakeBounds(Sprite[] sprites)
    {
        var data=AssetDatabase.LoadAssetAtPath<VisibleBodyGeometry>("Assets/Resources/VisibleBodyGeometry.asset");
        var entries=data.entries.Where(e=>!sprites.Contains(e.sprite)).ToList();
        foreach(var sprite in sprites)
        {
            var source=new Texture2D(2,2);source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
            var p=source.GetPixels32();int x0=source.width,y0=source.height,x1=0,y1=0;
            for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++)if(p[y*source.width+x].a>32)
            {x0=Mathf.Min(x0,x);x1=Mathf.Max(x1,x);y0=Mathf.Min(y0,y);y1=Mathf.Max(y1,y);}
            float sx=(float)sprite.texture.width/source.width,sy=(float)sprite.texture.height/source.height,ppu=sprite.pixelsPerUnit;
            var r=new Rect((x0*sx-sprite.pivot.x)/ppu,(y0*sy-sprite.pivot.y)/ppu,(x1-x0+1)*sx/ppu,(y1-y0+1)*sy/ppu);
            entries.Add(new VisibleBodyGeometry.Entry{sprite=sprite,bounds=r}); Object.DestroyImmediate(source);
        }
        data.entries=entries.ToArray();EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
    }
}
