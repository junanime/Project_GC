using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public sealed class NoblePanelImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/NoblePanels/") || !assetPath.EndsWith(".png")) return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.mipmapEnabled=false; importer.alphaIsTransparency=true;
            importer.npotScale=TextureImporterNPOTScale.None; importer.maxTextureSize=2048;
            importer.filterMode=FilterMode.Bilinear; importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            // Import the entire image: cropping to the inner frame would cut off the liquid/weapon.
        }
    }
}
