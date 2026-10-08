using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public static class RunBookContentInstaller
    {
        public static void Install()
        {
            AssetDatabase.Refresh();
            foreach(string name in new[]{"Backplate","Icons"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/RunBook/"+name+".png");
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.isReadable=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=8192;
                importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();Debug.Log("RUNBOOK_ART_INSTALLED");
        }
    }
}
