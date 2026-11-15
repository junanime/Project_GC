#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    public sealed class AcquisitionIconImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/AcquisitionIcons/"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=256;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=512;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteExtrude=0;
            importer.SetTextureSettings(settings);
        }
    }
}
#endif
