using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    /// <summary>Imports the approved A body and fixed-seat V3 poses without rewriting source pixels.</summary>
    public sealed class ConstipationKingArtImporter : AssetPostprocessor
    {
        const string Root = "Assets/Resources/ConstipationKing/";
        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(Root, StringComparison.Ordinal)) Configure((TextureImporter)assetImporter, assetPath);
        }

        static SpriteMetaData Frame(string name, Rect rectangle, Vector2 pivot) => new SpriteMetaData
        {
            name = name, rect = rectangle, alignment = (int)SpriteAlignment.Custom, pivot = pivot
        };

        public static void Configure(TextureImporter importer, string path)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            string name = Path.GetFileNameWithoutExtension(path);
            if (name == "Body")
            {
                // A uses the same 480-pixel reference frame as V3. Keep the exact source body.
                importer.spritePixelsPerUnit = 1254f / 480f * 100;
                importer.spritesheet = new[]
                {
                    Frame("Body", new Rect(0, 0, 1254, 1254), new Vector2(.5f, 14f / 480)),
                    Frame("Outlet", new Rect(570, 91, 180, 39), new Vector2(.5f, .5f))
                };
            }
            else if (name.StartsWith("Pose", StringComparison.Ordinal))
            {
                // Only the obsolete twin underside nozzles are excluded. Every upper pose
                // retains the authored 480-pixel frame and identical porcelain-seat scale.
                var body = Frame(name, new Rect(0, 44, 480, 436), new Vector2(.5f, 0));
                importer.spritesheet = name == "Pose6"
                    ? new[] { body, Frame("StaffFoot6", new Rect(330, 0, 150, 44), new Vector2(.5f, 0)) }
                    : new[] { body };
            }
            else if (name == "Booster")
            {
                var frames = new SpriteMetaData[4];
                for (int i = 0; i < frames.Length; i++) frames[i] = Frame("Booster" + i, new Rect(i * 543, 0, 543, 724), new Vector2(522f / 543, 344f / 724));
                importer.spritesheet = frames;
            }
            else if (name == "Tribute")
            {
                var frames = new SpriteMetaData[3];
                for (int i = 0; i < frames.Length; i++) frames[i] = Frame("Tribute" + i, new Rect(i * 724, 0, 724, 724), new Vector2(.5f, .5f));
                importer.spritesheet = frames;
            }
            else if (name == "Crown") importer.spritesheet = new[] { Frame("Crown", new Rect(0, 0, 322, 327), new Vector2(.5f, .5f)) };
        }

        [MenuItem("Tools/24 Hours/Import Constipation King Approved Art")]
        public static void Prepare()
        {
            foreach (string file in Directory.GetFiles(Root, "*.png"))
            {
                string path = file.Replace('\\', '/');
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                Configure(importer, path);
                importer.SaveAndReimport();
            }
            ConstipationKingNeedleSurfaceBaker.Bake();
            AssetDatabase.SaveAssets();
            Debug.Log("Constipation King: original A body, eleven fixed-seat poses, single central outlet, four authored hydrant booster frames.");
        }
    }
}
