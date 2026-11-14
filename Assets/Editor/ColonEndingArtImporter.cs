using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    /// <summary>Consistent alpha-safe import for the approved ending props.</summary>
    public sealed class ColonEndingArtImporter : AssetPostprocessor
    {
        const string Root = "Assets/Resources/ColonEnding/";
        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(Root, StringComparison.Ordinal)) Configure((TextureImporter)assetImporter, assetPath);
        }
        static void Configure(TextureImporter importer, string path)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.spritePivot = new Vector2(.5f, .5f);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            // Bound the opaque painted prop, not the generated low-alpha halo.
            // Keep the source file unchanged and retain antialiased edges in the rect.
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                source.LoadImage(File.ReadAllBytes(path));
                var pixels = source.GetPixels32();
                int minX = source.width, minY = source.height, maxX = -1, maxY = -1;
                for (int y = 0; y < source.height; y++) for (int x = 0; x < source.width; x++)
                    if (pixels[y * source.width + x].a >= 220)
                    { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
                minX = Mathf.Max(0, minX - 4); minY = Mathf.Max(0, minY - 4);
                maxX = Mathf.Min(source.width - 1, maxX + 4); maxY = Mathf.Min(source.height - 1, maxY + 4);
                var rect = maxX >= minX && maxY >= minY ? new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1)
                    : new Rect(0, 0, source.width, source.height);
                importer.spritesheet = new[] { new SpriteMetaData { name = Path.GetFileNameWithoutExtension(path), rect = rect,
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) } };
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        [MenuItem("Tools/24 Hours/Import Colon Ending Art")]
        public static void Prepare()
        {
            if (!Directory.Exists(Root)) return;
            foreach (var file in Directory.GetFiles(Root, "*.png"))
            {
                var path = file.Replace('\\', '/');
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer) { Configure(importer, path); importer.SaveAndReimport(); }
            }
        }
    }
}
