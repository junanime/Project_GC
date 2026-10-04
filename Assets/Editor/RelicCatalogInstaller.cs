using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vampire;

public static class RelicCatalogInstaller
{
    [Serializable] public class Catalog { public Entry[] entries; }
    [Serializable] public class Entry
    { public string id, name, effect, description; public float value; public int price, sheet, cell; }
    [MenuItem("Tools/24tu/Install reviewed relic catalog")]
    public static void Install()
    {
        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText("Tools/Relics/catalog.json"));
        if (catalog.entries.Length != 34 || catalog.entries.Select(e => e.id).Distinct().Count() != 34)
            throw new Exception("Expected 34 unique relics.");
        string folder = "Assets/hyeon/Relic/OctoberExpansion";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/hyeon/Relic", "OctoberExpansion");
        for (int sheet = 1; sheet <= 6; sheet++)
        {
            string path = "Assets/Resources/RelicCatalog/Sheet" + sheet + ".png";
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            importer.spritesheet = catalog.entries.Where(e => e.sheet == sheet).Select(e =>
                new SpriteMetaData { name = e.id, rect = IconRect(sheet, e.cell, height), pivot = new Vector2(.5f,.5f), alignment = 9 }).ToArray();
            importer.SaveAndReimport();
        }
        var old = AssetDatabase.FindAssets("t:RelicBlueprint").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<RelicBlueprint>).ToArray();
        var relics = catalog.entries.Select(e =>
        {
            var relic = old.FirstOrDefault(r => r.relicId == e.id);
            if (relic == null)
            { relic = ScriptableObject.CreateInstance<RelicBlueprint>(); AssetDatabase.CreateAsset(relic, folder + "/" + e.id + ".asset"); }
            relic.relicId = e.id; relic.relicName = e.name; relic.price = e.price;
            relic.effectType = (RelicBlueprint.RelicEffectType)Enum.Parse(typeof(RelicBlueprint.RelicEffectType), e.effect);
            relic.effectValue = e.value; relic.effectDescription = e.description;
            relic.icon = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/RelicCatalog/Sheet" + e.sheet + ".png").OfType<Sprite>().Single(s => s.name == e.id);
            EditorUtility.SetDirty(relic); AssetDatabase.SaveAssetIfDirty(relic);
            return relic;
        }).ToArray();
        var config = AssetDatabase.LoadAssetAtPath<ApothecaryUIConfig>("Assets/Junhan/Resources/ApothecaryUIConfig.asset");
        config.relics = relics;
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        Debug.Log("[RelicCatalog] Installed 34 relics; existing IDs retained.");
    }
    // Rects select only icons, without resampling or modifying the supplied sheet pixels.
    // Coordinates below are top-left based; Unity sprites use a bottom-left origin.
    static Rect IconRect(int sheet, int cell, int height)
    {
        int col, row; float x, y, w, h;
        if (sheet == 5) { x=22; y=cell==0?37:408; w=292; h=cell==0?248:242; }
        else if (sheet == 6)
        { col=cell%4; row=cell/4; x=24+col*334; y=row==0?55:419; w=286; h=238; }
        else
        {
            col=cell%3; row=cell/3;
            float step=sheet==1?453:sheet==2?445:450;
            x=(sheet==1?67:sheet==2?35:42)+col*step;
            y=row==0?(sheet==2||sheet==4?48:30):(sheet==2||sheet==4?416:398);
            w=350; h=sheet==1?246:250;
            if(sheet==2 && cell==2) { x=945; y=48; w=345; h=252; }
        }
        return new Rect(x,height-y-h,w,h);
    }
}
