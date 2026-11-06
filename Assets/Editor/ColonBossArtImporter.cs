using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    /// <summary>Imports the approved fixed-pivot boss sheets without per-frame fitting.</summary>
    public sealed class ColonBossArtImporter : AssetPostprocessor
    {
        const string Root = "Assets/Resources/ColonBosses/";

        [Serializable] sealed class Frame
        {
            public string name;
            public int x, y, width, height;
        }
        [Serializable] sealed class Sheet
        {
            public float pixelsPerUnit = 125;
            public float pivotX = .5f, pivotY;
            public Frame[] frames;
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(Root, StringComparison.Ordinal))
                Configure((TextureImporter)assetImporter, assetPath);
        }

        static void Configure(TextureImporter importer, string path)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 125;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(.5f, 0);
            importer.SetTextureSettings(settings);

            string json = Path.ChangeExtension(path, ".json");
            if (!File.Exists(json)) return;
            Sheet sheet = JsonUtility.FromJson<Sheet>(File.ReadAllText(json));
            if (sheet == null || sheet.frames == null || sheet.frames.Length == 0) return;
            importer.spritePixelsPerUnit = Mathf.Max(1, sheet.pixelsPerUnit);
            importer.spriteImportMode = SpriteImportMode.Multiple;
            var frames = new SpriteMetaData[sheet.frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                Frame f = sheet.frames[i];
                frames[i] = new SpriteMetaData
                {
                    name = f.name,
                    rect = new Rect(f.x, f.y, f.width, f.height),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(sheet.pivotX, sheet.pivotY)
                };
            }
            importer.spritesheet = frames;
        }

        [MenuItem("Tools/24 Hours/Import Stage 2 Boss Art")]
        public static void Prepare()
        {
            if (!Directory.Exists(Root)) return;
            foreach (string file in Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                Configure(importer, path);
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Colon boss art: fixed anchors, point filtering, uncompressed alpha, no frame size fitting.");
        }
    }
}
