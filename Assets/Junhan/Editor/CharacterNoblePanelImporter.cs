using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    // Approved source previews are preserved byte-for-byte. Import only their illustration
    // window; frame, typography and ability descriptions are shared live UI elements.
    public sealed class CharacterNoblePanelImporter : AssetPostprocessor
    {
        const string Root = "Assets/Resources/CharacterNoblePanels/";
        static readonly Dictionary<string, int> ArtBottom = new Dictionary<string, int>
        {
            { "Hyuki/LifeBurn", 966 },
            { "Hyuki/CloneCulture", 964 },
            { "Hyuki/HedgehogNeedle", 966 },
            { "Hyuki/HeavySnipe", 965 },
            { "Hyuki/CursorControl", 994 },
            { "Hyuki/NeuralBlock", 966 },
            { "Hyuki/OrganCompression", 966 },
            { "Hyuki/GastricPeristalsisWave", 966 },
            { "Hyuki/MucosalFortress", 965 },
            { "Hyuki/HungrySpirit", 964 },
            { "Ashi/LifeBurn", 965 },
            { "Ari/LifeBurn", 1005 },
            { "Shini/LifeBurn", 984 },
            { "Ashi/CloneCulture", 966 },
            { "Ari/CloneCulture", 965 },
            { "Shini/CloneCulture", 967 },
            { "Ashi/HedgehogNeedle", 966 },
            { "Ari/HedgehogNeedle", 967 },
            { "Shini/HedgehogNeedle", 967 },
            { "Ashi/HeavySnipe", 971 },
            { "Ari/HeavySnipe", 969 },
            { "Shini/HeavySnipe", 1020 },
            { "Ashi/CursorControl", 999 },
            { "Ari/CursorControl", 1002 },
            { "Shini/CursorControl", 1011 },
            { "Ashi/NeuralBlock", 999 },
            { "Ari/NeuralBlock", 982 },
            { "Shini/NeuralBlock", 963 },
            { "Ashi/OrganCompression", 977 },
            { "Ari/OrganCompression", 966 },
            { "Shini/OrganCompression", 1009 },
            { "Ashi/GastricPeristalsisWave", 1005 },
            { "Ari/GastricPeristalsisWave", 1072 },
            { "Shini/GastricPeristalsisWave", 1020 },
            { "Ashi/MucosalFortress", 967 },
            { "Ari/MucosalFortress", 969 },
            { "Shini/MucosalFortress", 977 },
            { "Ashi/HungrySpirit", 993 },
            { "Ari/HungrySpirit", 966 },
            { "Shini/HungrySpirit", 969 },
        };
        static readonly Dictionary<string, Rect> CutoutRects = new Dictionary<string, Rect>
        {
            { "Cutouts/Ashi/HeavySnipe", new Rect(68,144,1163,961) },
            { "Cutouts/Hyuki/GastricPeristalsisWave", new Rect(44,104,1188,1023) },
            { "Cutouts/Hyuki/HeavySnipe", new Rect(86,203,1145,796) },
            { "Cutouts/Hyuki/HedgehogNeedle", new Rect(30,144,1195,978) },
            { "Cutouts/Hyuki/MucosalFortress", new Rect(23,177,1174,864) },
            { "Cutouts/Shini/CloneCulture", new Rect(86,221,1138,862) },
            { "Cutouts/Shini/HeavySnipe", new Rect(102,160,1140,986) },
            { "Cutouts/Shini/OrganCompression", new Rect(106,94,1054,1097) },
        };
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root) || !assetPath.EndsWith(".png")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            string key = assetPath.Substring(Root.Length).Replace(".png", "");
            Rect rect;
            if (!CutoutRects.TryGetValue(key, out rect))
            {
                if (!ArtBottom.TryGetValue(key, out var bottom)) return;
                rect = new Rect(132, 1536 - bottom, 760, bottom - 254);
            }
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = new[] { new SpriteMetaData {
                name = Path.GetFileNameWithoutExtension(assetPath),
                rect = rect,
                alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * .5f
            }};
        }
    }
}
