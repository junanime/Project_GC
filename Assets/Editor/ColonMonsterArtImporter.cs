using System.IO;
using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    public static class ColonMonsterArtImporter
    {
        public static void Install()
        {
            const string folder="Assets/Resources/ColonMonsters";
            foreach(string path in Directory.GetFiles(folder,"*.png"))
            {
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=256;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteAlignment=(int)SpriteAlignment.BottomCenter;settings.spritePivot=new Vector2(.5f,0);importer.SetTextureSettings(settings);
                importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=512;importer.isReadable=false;
                if(Path.GetFileName(path)=="Extras.png")
                {
                    importer.maxTextureSize=1024;importer.spriteImportMode=SpriteImportMode.Multiple;
                    importer.spritesheet=new[]{new SpriteMetaData{name="Popcorn",rect=new Rect(204,154,518,507),alignment=(int)SpriteAlignment.Center,pivot=new Vector2(.5f,.5f)},new SpriteMetaData{name="Pea",rect=new Rect(1054,172,499,436),alignment=(int)SpriteAlignment.BottomCenter,pivot=new Vector2(.5f,0)}};
                }
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();Debug.Log("Colon monster sprites imported: point filtered, grounded pivots, no mipmaps.");
        }
    }
}
