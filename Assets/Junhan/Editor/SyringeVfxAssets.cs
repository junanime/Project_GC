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

    [MenuItem("Tools/Syringe VFX/Build batch 3")]
    public static void BuildBatch3()
    {
        BuildSheet("HungerNeedle", 2, 443.5f / 0.85f, 0.045f, 0.65f);
        BuildSheet("GutBacteriaNeedle", 3, 362f, 0.09f, 0.55f);
        BuildSheet("BipolarNeedle", 4, 600f, 0.075f, 0.55f, 3);
        BuildSheet("HeavySnipe", 3, 362f / 0.85f, 0.075f, 0.65f);
        BuildVariant("HeavySnipe", "HeavySnipeFullCharge", 4, 4, true, 0.075f);
        BuildVariant("HeavySnipe", "HeavySnipeImpact", 8, 4, false, 0.045f);
        BuildVariant("HeavySnipe", "HeavySnipe", 0, 4, true, 0.075f);
        BuildSheet("HungrySpirit", 3, 362f, 0.10f, 0.42f);
        BuildSheet("NeedleShotgun", 2, 443.5f / 1.1f, 0.035f, 0.60f);
        BuildSheet("PoisonContagion", 2, 443.5f / 0.6f, 0.065f, 0.65f);
        BuildSheet("OrganCompression", 3, 362f, 0.075f, 0.27f);
        BuildVariant("OrganCompression", "OrganCompressionHit", 0, 8, false, 0.035f);
        BuildSheet("GastricPeristalsisWave", 3, 362f, 0.07f, 0.65f);
        BuildSheet("NeuralBlock", 3, 362f, 0.065f, 0.42f);
        AssetDatabase.SaveAssets();
        Debug.Log("SYRINGE_BATCH3_ASSETS_OK: 10 sheets, 13 prefabs, 108 source frames");
    }

    [MenuItem("Tools/Syringe VFX/Build batch 4")]
    public static void BuildBatch4()
    {
        BuildSheet("LifeBurn", 4, 313.5f, 0.08f, 0.38f);
        BuildVariant("LifeBurn", "LifeBurnConsume", 0, 4, false, 0.06f);
        BuildVariant("LifeBurn", "LifeBurn", 4, 12, true, 0.08f);
        ConfigureBatch4Prefab("LifeBurn", false, -2, 0.38f);
        BuildSheet("CloneCulture", 4, 313.5f, 0.09f, 0.25f);
        BuildVariant("CloneCulture", "CloneSpawn", 0, 4, false, 0.06f);
        BuildVariant("CloneCulture", "CloneDisappear", 12, 4, false, 0.06f);
        BuildVariant("CloneCulture", "CloneCulture", 4, 8, true, 0.09f);
        ConfigureBatch4Prefab("CloneCulture", false, 2, 0.25f);
        BuildSheet("HedgehogNeedle", 2, 443.5f, 0.05f, 0.5f);
        BuildVariant("HedgehogNeedle", "HedgehogBurst", 4, 4, false, 0.05f);
        BuildVariant("HedgehogNeedle", "HedgehogNeedle", 0, 4, false, 0.05f);
        BuildSheet("CursorControl", 3, 362f, 0.08f, 0.2f);
        ConfigureBatch4Prefab("CursorControl", true, -3, 0.2f);
        BuildVariant("CursorControl", "CursorControlSpawn", 0, 4, false, 0.05f);
        BuildVariant("CursorControl", "CursorControlGlow", 0, 12, true, 0.06f);
        BuildVariant("CursorControl", "CursorControlHit", 0, 4, false, 0.045f);
        ConfigureBatch4Prefab("CursorControlGlow", true, 5, 0.5f);
        ConfigureBatch4Prefab("CursorControlHit", true, 5, 0.65f);
        BuildSheet("MucosalFortress", 3, 362f, 0.09f, 0.28f);
        ConfigureBatch4Prefab("MucosalFortress", true, 3, 0.28f);
        BuildVariant("MucosalFortress", "MucosalFortressCreate", 0, 4, false, 0.05f);
        ConfigureBatch4Prefab("MucosalFortressCreate", true, 3, 0.4f);
        BuildSheet("MucosalFortressBreak", 2, 443.5f, 0.045f, 0.5f);
        BuildSheet("FiberNeedle", 3, 362f, 0.12f, 0.8f);
        ConfigureBatch4Prefab("FiberNeedle", true, 5, 0.8f);
        AssetDatabase.SaveAssets();
        Debug.Log("SYRINGE_BATCH4_ASSETS_OK: 7 sheets, 15 prefabs, 84 source frames");
    }

    private static void ConfigureBatch4Prefab(string name, bool worldSpace, int orderOffset, float opacity)
    {
        string path = "Assets/Junhan/Resources/SyringeVfx/" + name + ".prefab";
        var go = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var serialized = new SerializedObject(go.GetComponent<SyringeAugmentVfx>());
            serialized.FindProperty("worldSpace").boolValue = worldSpace;
            serialized.FindProperty("sortingOrderOffset").intValue = orderOffset;
            serialized.FindProperty("opacity").floatValue = opacity;
            serialized.FindProperty("groundEffect").boolValue = name == "DigestiveAcidSacNeedle" || name == "FiberNeedle" || name == "OrganCompression" || name == "GastricPeristalsisWave";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (serialized.FindProperty("groundEffect").boolValue) GroundVisualSorting.ApplyHierarchy(go);
            PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(go); }
    }

    private static void BuildVariant(string source, string name, int start, int count, bool loop, float frameTime)
    {
        string root = "Assets/Junhan/Resources/SyringeVfx/";
        var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(root + source + ".prefab"));
        try
        {
            go.name = name;
            var effect = go.GetComponent<SyringeAugmentVfx>();
            var all = AssetDatabase.LoadAllAssetsAtPath(root + source + ".png").OfType<Sprite>().OrderBy(x => x.name).ToArray();
            var selected = all.Skip(start).Take(count).ToArray();
            var serialized = new SerializedObject(effect);
            var frames = serialized.FindProperty("frames");
            frames.arraySize = selected.Length;
            for (int i = 0; i < selected.Length; i++) frames.GetArrayElementAtIndex(i).objectReferenceValue = selected[i];
            serialized.FindProperty("loop").boolValue = loop;
            serialized.FindProperty("frameTime").floatValue = frameTime;
            if (name == "OrganCompressionHit") serialized.FindProperty("opacity").floatValue = 0.38f;
            serialized.FindProperty("groundEffect").boolValue = name == "DigestiveAcidSacNeedle" || name == "FiberNeedle" || name == "OrganCompression" || name == "GastricPeristalsisWave";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (serialized.FindProperty("groundEffect").boolValue) GroundVisualSorting.ApplyHierarchy(go);
            go.GetComponent<SpriteRenderer>().sprite = selected[0];
            PrefabUtility.SaveAsPrefabAsset(go, root + name + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void BuildSheet(string name, int rows, float ppu, float frameTime, float alpha, int columns = 4)
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
        var slices = new SpriteRect[rows * columns];
        bool batch3 = new[] { "HungerNeedle", "GutBacteriaNeedle", "BipolarNeedle", "HeavySnipe", "HungrySpirit", "NeedleShotgun", "PoisonContagion", "OrganCompression", "GastricPeristalsisWave", "NeuralBlock" }.Contains(name);
        bool batch4 = new[] { "LifeBurn", "CloneCulture", "HedgehogNeedle", "CursorControl", "MucosalFortress", "MucosalFortressBreak", "FiberNeedle" }.Contains(name);
        bool registerFrames = batch4 || batch3 || name == "MarkNeedle" || name == "CorrosionNeedle" || name == "Honey" || name == "DigestiveAcidSacNeedle";
        Color32[] pixels = null;
        if (registerFrames)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try { source.LoadImage(System.IO.File.ReadAllBytes(path + ".png")); pixels = source.GetPixels32(); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        for (int row = 0; row < rows; row++)
        for (int col = 0; col < columns; col++)
        {
            int left = Mathf.RoundToInt(col * width / (float)columns);
            int right = Mathf.RoundToInt((col + 1) * width / (float)columns);
            // The generated bipolar sheet has three poses in each of four color rows.
            if (name == "BipolarNeedle")
            {
                int[] edges = { 0, 400, 870, width };
                left = edges[col]; right = edges[col + 1];
            }
            int top = Mathf.RoundToInt(row * height / (float)rows);
            int bottom = Mathf.RoundToInt((row + 1) * height / (float)rows);
            string frameName = name + "_" + (row * columns + col).ToString("D2");
            Vector2 pivot = registerFrames ? FindPivot(pixels, width, left, height - bottom, right - left, bottom - top, name == "DigestiveAcidSacNeedle") : new Vector2(0.5f, 0.5f);
            if (name == "NeedleShotgun" || name == "BipolarNeedle")
            {
                int minX = right - left;
                for (int y = height - bottom; y < height - top; y++)
                for (int x = left; x < right; x++)
                    if (pixels[y * width + x].a >= 128) minX = Mathf.Min(minX, x - left);
                pivot.x = (minX + 8f) / (right - left);
            }
            slices[row * columns + col] = new SpriteRect {
                name = frameName,
                spriteID = existing.TryGetValue(frameName, out var id) ? id : GUID.Generate(),
                rect = new Rect(left, height - bottom, right - left, bottom - top),
                alignment = registerFrames ? SpriteAlignment.Custom : SpriteAlignment.Center,
                pivot = pivot };
        }
        provider.SetSpriteRects(slices);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            slices.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        var frames = AssetDatabase.LoadAllAssetsAtPath(path + ".png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
        if (frames.Length != rows * columns) throw new Exception("Incorrect sprite count: " + name);
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
            serialized.FindProperty("loop").boolValue = rows == 3 || name == "BipolarNeedle";
            serialized.FindProperty("opacity").floatValue = alpha;
            serialized.FindProperty("worldSpace").boolValue = name == "DigestiveAcidSacNeedle" || name == "BipolarNeedle" || name == "HeavySnipe" || name == "OrganCompression" || name == "GastricPeristalsisWave";
            serialized.FindProperty("overhead").boolValue = name == "MarkNeedle";
            serialized.FindProperty("groundEffect").boolValue = name == "DigestiveAcidSacNeedle" || name == "FiberNeedle" || name == "OrganCompression" || name == "GastricPeristalsisWave";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (serialized.FindProperty("groundEffect").boolValue) GroundVisualSorting.ApplyHierarchy(go);
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
