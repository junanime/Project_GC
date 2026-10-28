using System;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public static class MotionVfxImporter
    {
        [MenuItem("Tools/Motion VFX/Import approved October 20 art")]
        public static void Import()
        {
            ImportSheet("CharacterTrails", new[] { "Ashi", "Ari", "Hyuki", "Shini" }, 0);
            ImportSheet("NeedleEnd", new[] { "NeedleEnd" }, 4);
            AssetDatabase.SaveAssets();
        }

        static void ImportSheet(string name, string[] names, int offset)
        {
            string path = "Assets/Junhan/Resources/MotionVfx/" + name + ".png";
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.isReadable = true;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var pixels = texture.GetPixels32();
            int width = texture.width / 3, height = texture.height / 2;
            var sprites = new SpriteMetaData[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int cell = i + offset, sx = cell % 3 * width, sy = (1 - cell / 3) * height;
                int x0 = sx + width, y0 = sy + height, x1 = sx, y1 = sy;
                for (int y = sy; y < sy + height; y++)
                    for (int x = sx; x < sx + width; x++)
                        if (pixels[y * texture.width + x].a > 230)
                        { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
                x0 = Math.Max(sx, x0 - 8); y0 = Math.Max(sy, y0 - 8);
                x1 = Math.Min(sx + width - 1, x1 + 8); y1 = Math.Min(sy + height - 1, y1 + 8);
                sprites[i] = new SpriteMetaData { name = names[i], alignment = 9, pivot = new Vector2(.5f, .5f), rect = new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1) };
            }
            importer.spritesheet = sprites;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
    }
}
