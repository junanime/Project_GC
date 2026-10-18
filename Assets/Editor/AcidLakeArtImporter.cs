using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    // Preserve approved source PNGs; only configure their Unity import settings.
    public sealed class AcidLakeArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/AcidLake/")) return;
            Configure((TextureImporter)assetImporter);
        }
        static void Configure(TextureImporter importer)
        {
            importer.textureType=TextureImporterType.Default;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=4096;importer.mipmapEnabled=false;
            importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=true;
        }
        public static void Prepare()
        {
            AssetDatabase.Refresh();
            foreach(var name in new[]{"Water","Fx"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/AcidLake/"+name+".png");
                Configure(importer);importer.SaveAndReimport();
            }
        }
    }
}
