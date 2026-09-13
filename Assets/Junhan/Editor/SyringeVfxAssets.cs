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

    [MenuItem("Tools/Syringe VFX/Build batch 2")]
    public static void BuildBatch2()
    {
        BuildSheet("Pierce", 2, 443.5f / 1.1f, 0.045f, 0.7f);
        BuildSheet("Homing", 2, 443.5f / 0.7f, 0.035f, 0.55f);
        BuildSheet("PressureNeedle", 2, 443.5f / 1.2f, 0.045f, 0.6f);
        BuildSheet("MarkNeedle", 3, 362f, 0.08f, 0.85f);
        BuildSheet("CorrosionNeedle", 3, 362f, 0.09f, 0.45f);
        BuildSheet("Honey", 3, 362f, 0.12f, 0.55f);
        BuildSheet("Mosquito", 2, 443.5f / 0.65f, 0.065f, 0.7f);
        BuildSheet("ReturnNeedle", 2, 443.5f / 0.9f, 0.045f, 0.65f);
        BuildSheet("AcupunctureFormation", 2, 443.5f / 2f, 0.055f, 0.55f);
        BuildSheet("DigestiveAcidSacNeedle", 3, 362f, 0.1f, 0.5f);
        AssetDatabase.SaveAssets();
        Debug.Log("SYRINGE_BATCH2_ASSETS_OK");
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
        bool registerFrames = name == "MarkNeedle" || name == "CorrosionNeedle" || name == "Honey" || name == "DigestiveAcidSacNeedle";
        Color32[] pixels = null;
        if (registerFrames)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try { source.LoadImage(System.IO.File.ReadAllBytes(path + ".png")); pixels = source.GetPixels32(); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
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
                alignment = registerFrames ? SpriteAlignment.Custom : SpriteAlignment.Center,
                pivot = registerFrames ? FindPivot(pixels, width, left, height - bottom, right - left, bottom - top, name == "DigestiveAcidSacNeedle") : new Vector2(0.5f, 0.5f) };
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
            serialized.FindProperty("worldSpace").boolValue = name == "DigestiveAcidSacNeedle";
            serialized.FindProperty("overhead").boolValue = name == "MarkNeedle";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(go, path + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    // Register persistent art without changing the PNG or the shipped Explosion/Poison slices.
    private static Vector2 FindPivot(Color32[] pixels, int sheetWidth, int left, int bottom, int width, int height, bool useCentroid)
    {
        int minX = width, minY = height, maxX = -1, maxY = -1;
        double sumX = 0, sumY = 0, weight = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            byte alpha = pixels[(bottom + y) * sheetWidth + left + x].a;
            if (alpha < 128) continue;
            minX = Math.Min(minX, x); minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            sumX += (x + 0.5) * alpha; sumY += (y + 0.5) * alpha; weight += alpha;
        }
        if (maxX < 0) return new Vector2(0.5f, 0.5f);
        return useCentroid ? new Vector2((float)(sumX / weight / width), (float)(sumY / weight / height)) :
            new Vector2((minX + maxX + 1f) / (2f * width), (minY + maxY + 1f) / (2f * height));
    }

}
