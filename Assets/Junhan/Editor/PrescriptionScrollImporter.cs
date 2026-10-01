using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public sealed class PrescriptionScrollImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/PrescriptionScroll/")||!assetPath.EndsWith(".png"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=4096;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.spritesheet=assetPath.EndsWith("Rolled.png") ? new[]{Sprite("Rolled",new Rect(8,88,2154,598))} : new[]{
                Sprite("Paper",new Rect(88,143,910,1160)), Sprite("Roller",new Rect(20,40,1048,104))};
        }
        static SpriteMetaData Sprite(string name,Rect rect)=>new SpriteMetaData{name=name,rect=rect,pivot=Vector2.one*.5f,alignment=9};
    }
}
