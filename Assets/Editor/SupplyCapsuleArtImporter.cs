using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.EditorTools
{
    public sealed class SupplyCapsuleArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Junhan/Resources/SupplyCapsule/")||!assetPath.EndsWith(".png"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Sprite;
            if(importer.spriteImportMode==SpriteImportMode.None)importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;importer.isReadable=true;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
        }
        public static void Prepare()
        {
            foreach(string name in new[]{"ClosedClean","Parachute"})
            {
                string path="Assets/Junhan/Resources/SupplyCapsule/"+name+".png";
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var pixels=tex.GetPixels32();
                int minX=tex.width,minY=tex.height,maxX=0,maxY=0;
                for(int y=0;y<tex.height;y++)for(int x=0;x<tex.width;x++)if(pixels[y*tex.width+x].a>48)
                {minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.spriteImportMode=SpriteImportMode.Multiple;
#pragma warning disable 618
                importer.spritesheet=new[]{new SpriteMetaData{name=name,rect=new Rect(minX,minY,maxX-minX+1,maxY-minY+1),alignment=(int)SpriteAlignment.Custom,pivot=name=="Parachute"?new Vector2(.5f,0):new Vector2(.5f,.5f)}};
#pragma warning restore 618
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }
    }
}
