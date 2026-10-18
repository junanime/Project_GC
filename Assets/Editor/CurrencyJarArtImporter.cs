using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Vampire.Editor
{
    public sealed class CurrencyJarArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Junhan/Resources/CurrencyJars/") || !assetPath.EndsWith(".png")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Sprite;
            if (t.spriteImportMode == SpriteImportMode.None) t.spriteImportMode = SpriteImportMode.Single;
            t.filterMode = FilterMode.Point; t.mipmapEnabled = false; t.isReadable = true;
            t.alphaIsTransparency = true; t.textureCompression = TextureImporterCompression.Uncompressed;
            t.maxTextureSize = 2048; t.spritePixelsPerUnit = 100;
        }
        public static void Prepare()
        {
            foreach (var name in new[] { "Jars", "Coins" })
            {
                string path = "Assets/Junhan/Resources/CurrencyJars/" + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path); var pixels = t.GetPixels32();
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.spriteImportMode = SpriteImportMode.Multiple;
                var factory = new SpriteDataProviderFactories(); factory.Init();
                var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                var old = provider.GetSpriteRects();
                var sprites = new SpriteRect[2];
                for (int side = 0; side < 2; side++)
                {
                    int x0 = t.width, y0 = t.height, x1 = -1, y1 = -1;
                    for (int y = 0; y < t.height; y++) for (int x = side * t.width / 2; x < (side + 1) * t.width / 2; x++)
                        if (pixels[y * t.width + x].a > 48) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
                    string spriteName = side == 0 ? "Gold" : "Silver";
                    sprites[side] = new SpriteRect { name = spriteName, spriteID = old.FirstOrDefault(s => s.name == spriteName)?.spriteID ?? GUID.Generate(), rect = new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1), alignment = SpriteAlignment.Custom, pivot = name == "Jars" ? new Vector2(.5f, 0) : new Vector2(.5f, .5f) };
                }
                provider.SetSpriteRects(sprites);
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(sprites.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
                provider.Apply();
                importer.SaveAndReimport();
                if (AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() != 2)
                    throw new System.InvalidOperationException("Currency jar sprite slicing failed: " + path);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
