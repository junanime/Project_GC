using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class AugmentPanelImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/AugmentPanels/") || !assetPath.EndsWith(".png")) return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.alphaIsTransparency=true; importer.mipmapEnabled=false;
            importer.npotScale=TextureImporterNPOTScale.None; importer.maxTextureSize=4096;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.filterMode=FilterMode.Bilinear; importer.wrapMode=TextureWrapMode.Clamp;
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            string name=Path.GetFileNameWithoutExtension(assetPath);
            // Trim excess glow padding at import time, without resampling/editing the approved source art.
            importer.spritesheet=new[]{new SpriteMetaData {
                name=name, alignment=9, pivot=new Vector2(.5f,.5f),
                rect=name=="Reroll" ? new Rect(110,190,1840,390) : new Rect(176,68,672,1400)
            }};
        }
    }
}
