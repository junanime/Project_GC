using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    public static class AmphibiousCapsuleInstaller
    {
        public static void Install()
        {
            foreach(var name in new[]{"Body","Doctor"})
            {
                string path="Assets/Resources/AmphibiousCapsule/"+name+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.spritePixelsPerUnit=100;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.filterMode=FilterMode.Bilinear;t.npotScale=TextureImporterNPOTScale.None;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=2048;t.isReadable=true;t.SaveAndReimport();
                var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var pixels=tex.GetPixels32();int x0=tex.width,y0=tex.height,x1=0,y1=0;
                for(int y=0;y<tex.height;y++)for(int x=0;x<tex.width;x++)if(pixels[y*tex.width+x].a>240){x0=Mathf.Min(x0,x);y0=Mathf.Min(y0,y);x1=Mathf.Max(x1,x);y1=Mathf.Max(y1,y);}
                t.spriteImportMode=SpriteImportMode.Multiple;t.spritesheet=new[]{new SpriteMetaData{name=name,rect=Rect.MinMaxRect(x0,y0,x1+1,y1+1),alignment=9,pivot=Vector2.one*.5f}};t.isReadable=false;t.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();Debug.Log("[Capsule] Imported capsule and doctor with native alpha crops.");
        }
    }
}
