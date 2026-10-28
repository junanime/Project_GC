using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    public sealed class FoodBossArtImporter:AssetPostprocessor
    {
        void OnPreprocessTexture(){if(assetPath.StartsWith("Assets/Resources/FoodBosses/")&&assetImporter is TextureImporter t)Configure(t);}
        static void Configure(TextureImporter t){t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.spritePixelsPerUnit=100;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.filterMode=FilterMode.Point;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=2048;}
        public static void Prepare(){foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/FoodBosses"})){var t=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;if(t!=null){Configure(t);t.SaveAndReimport();}}}
    }
}
