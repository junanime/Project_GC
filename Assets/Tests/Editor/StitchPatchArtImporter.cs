using UnityEditor;
using UnityEngine;
namespace Vampire.Tests.Editor
{
    public sealed class StitchPatchArtImporter:AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/StitchPatch/"))return;
            var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;
            t.alphaIsTransparency=true;t.mipmapEnabled=false;t.maxTextureSize=2048;t.npotScale=TextureImporterNPOTScale.None;
            t.textureCompression=TextureImporterCompression.Uncompressed;t.filterMode=FilterMode.Bilinear;
        }
    }
}
