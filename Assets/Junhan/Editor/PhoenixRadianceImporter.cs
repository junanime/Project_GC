using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    // Keep generated alpha and original pixels. Animation cells are sampled directly
    // by the VFX shader, independently of sprite slicing and UI texture import settings.
    public sealed class PhoenixRadianceImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/PhoenixRadiance/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
        }
    }
}
