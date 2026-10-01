using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Vampire;

public static class SnailMiniSetup
{
    public static readonly string[] Names = { "Blueberry", "Strawberry", "Melon", "Mango", "Ball", "Kisses", "Tablet", "Bar" };
    [MenuItem("Tools/Junhan2/Install mini snail art")]
    public static void Install()
    {
        foreach (string name in Names)
        {
            string path = "Assets/Resources/SnailBoss/Minis/" + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new Exception("Missing mini art " + path);
            // Read generated alpha only to calibrate import dimensions. Original PNG pixels are unchanged.
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(path));
            var pixels = source.GetPixels32();
            int minX=source.width, minY=source.height, maxX=0, maxY=0;
            for (int y=0; y<source.height; y++)
                for (int x=0; x<source.width; x++)
                    if (pixels[y*source.width+x].a > 32)
                    { minX=Mathf.Min(minX,x); minY=Mathf.Min(minY,y); maxX=Mathf.Max(maxX,x); maxY=Mathf.Max(maxY,y); }
            if (maxX <= minX || minY == 0 || minX == 0)
                throw new Exception("Mini shell requires padded transparent background: " + name);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=(maxX-minX+1)/SnailMiniVisual.ShellWidth;
            importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;
            importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048;
            var settings=new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;
            settings.spritePivot=new Vector2((minX+maxX+1f)*.5f/source.width,(float)minY/source.height);
            settings.spriteMeshType=SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            UnityEngine.Object.DestroyImmediate(source);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("MINI_SNAIL_INSTALL_PASS");
    }

    [MenuItem("Tools/Junhan2/Export mini snail walk")]
    public static void Export()
    {
        Install();
        var root=new GameObject("Mini walk preview");
        var cameraObject=new GameObject("Mini preview camera");
        var camera=cameraObject.AddComponent<Camera>();
        camera.orthographic=true; camera.orthographicSize=1.35f;
        camera.transform.position=new Vector3(0,.47f,-30);
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(1,.98f,.95f);
        camera.cullingMask=1<<30;
        var target=new RenderTexture(1600,900,24);
        camera.targetTexture=target;
        Directory.CreateDirectory("Logs/SnailMinis");
        var rigs=new SnailMiniVisual[8];
        try
        {
            for(int i=0;i<8;i++)
            {
                var actor=new GameObject(Names[i]); actor.transform.SetParent(root.transform,false);
                actor.transform.localPosition=new Vector3((i%4-1.5f)*1.18f,i<4?.52f:-.65f,0);
                rigs[i]=actor.AddComponent<SnailMiniVisual>(); rigs[i].Configure(actor.transform,i>=4,i%4);
                foreach(var renderer in actor.GetComponentsInChildren<SpriteRenderer>()) renderer.gameObject.layer=30;
            }
            for(int f=0;f<16;f++)
            {
                for(int i=0;i<8;i++) rigs[i].ApplyPose(f/16f,true);
                camera.Render();
                var old=RenderTexture.active; RenderTexture.active=target;
                var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply();
                File.WriteAllBytes("Logs/SnailMinis/walk-"+f.ToString("00")+".png",image.EncodeToPNG());
                RenderTexture.active=old; UnityEngine.Object.DestroyImmediate(image);
            }
            Debug.Log("MINI_SNAIL_EXPORT_PASS");
        }
        finally
        {
            camera.targetTexture=null; UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
