using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public sealed class AugmentSelectionImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/AugmentSelection/")) return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;
            importer.mipmapEnabled=false; importer.isReadable=false;
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.wrapMode=TextureWrapMode.Clamp; importer.filterMode=FilterMode.Bilinear;
            importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.alphaIsTransparency=assetPath.EndsWith(".png");
        }
    }
}
