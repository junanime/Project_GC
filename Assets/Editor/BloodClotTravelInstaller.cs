using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public static class BloodClotTravelInstaller
    {
        public static void Install()
        {
            foreach (string key in new[] { "Shini", "Hyuki", "HyukiApproach", "Ari", "Ashi", "Overcharged" })
            {
                string path = "Assets/Resources/BloodClotTravel/" + key + ".png";
                AssetDatabase.ImportAsset(path);
                var texture = new Texture2D(2, 2); texture.LoadImage(File.ReadAllBytes(path));
                int grid = key == "Hyuki" || key == "Overcharged" ? 1 : 4;
                var pixels = texture.GetPixels32();
                var metadata = new SpriteMetaData[key == "HyukiApproach" ? 4 : grid * grid];
                for (int i = 0; i < metadata.Length; i++)
                {
                    int x0 = i % grid * texture.width / grid, x1 = (i % grid + 1) * texture.width / grid;
                    int y0 = (grid - 1 - i / grid) * texture.height / grid, y1 = (grid - i / grid) * texture.height / grid;
                    int left = x1, right = x0, bottom = y1, top = y0;
                    for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                        if (pixels[y * texture.width + x].a > 32) { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
                    metadata[i] = new SpriteMetaData { name = i.ToString("D2"), rect = new Rect(left, bottom, right - left + 1, top - bottom + 1), alignment = 0, pivot = Vector2.one * .5f };
                }
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.isReadable = true; importer.maxTextureSize = 2048;
                importer.spritePixelsPerUnit = metadata[0].rect.height / (key == "Hyuki" ? .72f : 1f); // Shared PPU; crouching must not grow back to standing height.
                importer.spritesheet = metadata; importer.SaveAndReimport();
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("BLOOD_CLOT_ART_IMPORTED");
        }
    }
}
