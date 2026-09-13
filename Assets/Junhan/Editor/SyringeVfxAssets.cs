using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.U2D.Sprites;
using Vampire;

public static class SyringeVfxAssets
{
    [MenuItem("Tools/Syringe VFX/Rebuild sprite assets")]
    public static void Build()
    {
        BuildSheet("Explosion", 2, 443.5f / 1.1f, 0.06f, 0.75f);
        BuildSheet("Poison", 3, 362f, 0.09f, 0.55f);
        AssetDatabase.SaveAssets();
        Debug.Log("SYRINGE_VFX_ASSETS_OK: 8 explosion frames, 12 poison frames, prefabs validated.");
    }

    private static void BuildSheet(string name, int rows, float ppu, float frameTime, float alpha)
    {
        string path = "Assets/Junhan/Resources/SyringeVfx/" + name;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path + ".png");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.spritePixelsPerUnit = ppu;
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var existing = provider.GetSpriteRects().ToDictionary(s => s.name, s => s.spriteID);
        var slices = new SpriteRect[rows * 4];
        for (int row = 0; row < rows; row++)
        for (int col = 0; col < 4; col++)
        {
            int left = Mathf.RoundToInt(col * width / 4f);
            int right = Mathf.RoundToInt((col + 1) * width / 4f);
            int top = Mathf.RoundToInt(row * height / (float)rows);
            int bottom = Mathf.RoundToInt((row + 1) * height / (float)rows);
            string frameName = name + "_" + (row * 4 + col).ToString("D2");
            slices[row * 4 + col] = new SpriteRect {
                name = frameName,
                spriteID = existing.TryGetValue(frameName, out var id) ? id : GUID.Generate(),
                rect = new Rect(left, height - bottom, right - left, bottom - top),
                alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
        }
        provider.SetSpriteRects(slices);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            slices.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        var frames = AssetDatabase.LoadAllAssetsAtPath(path + ".png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
        if (frames.Length != rows * 4) throw new Exception("Incorrect sprite count: " + name);
        var go = new GameObject(name);
        try
        {
            var effect = go.AddComponent<SyringeAugmentVfx>();
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingLayerName = "Monster Full";
            renderer.sortingOrder = 5;
            var serialized = new SerializedObject(effect);
            var array = serialized.FindProperty("frames");
            array.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            serialized.FindProperty("frameTime").floatValue = frameTime;
            serialized.FindProperty("loop").boolValue = rows == 3;
            serialized.FindProperty("opacity").floatValue = alpha;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(go, path + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
