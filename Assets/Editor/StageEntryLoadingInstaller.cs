using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public static class StageEntryLoadingInstaller
    {
        const string Root="Assets/Resources/StageEntryLoading/";
        [MenuItem("24투/Install stage-entry loading art")]
        public static void Install()
        {
            Import("Corridor",false);
            foreach(var key in new[]{"Hyuki","Shini","Ari","Ashi"}) Import(key,true);
            const string assetPath="Assets/Resources/StageEntryLoadingArt.asset";
            var data=AssetDatabase.LoadAssetAtPath<StageEntryLoadingArt>(assetPath);
            if(data==null){data=ScriptableObject.CreateInstance<StageEntryLoadingArt>();AssetDatabase.CreateAsset(data,assetPath);}
            data.corridor=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"Corridor.png");
            data.hyuki=Frames("Hyuki");data.shini=Frames("Shini");data.ari=Frames("Ari");data.ashi=Frames("Ashi");
            data.bacteria=AssetDatabase.LoadAssetAtPath<MonsterBlueprint>("Assets/Prefabs/Monsters/genral Monster BP/1_1.asset").walkSpriteSequence;
            data.slime=AssetDatabase.LoadAssetAtPath<MonsterBlueprint>("Assets/Prefabs/Monsters/genral Monster BP/1.asset").walkSpriteSequence;
            EditorUtility.SetDirty(data);
            BakeIdleBounds();
            AssetDatabase.SaveAssets();
            Debug.Log("STAGE_ENTRY_ART_INSTALLED: rear run 4 x 8, original scene monsters, baked idle silhouettes");
        }
        static Sprite[] Frames(string key)=>AssetDatabase.LoadAllAssetsAtPath(Root+key+".png").OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        static void Import(string key,bool sheet)
        {
            string path=Root+key+".png";AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=sheet?SpriteImportMode.Multiple:SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=sheet;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.isReadable=false;
            importer.npotScale=TextureImporterNPOTScale.None;importer.spritePixelsPerUnit=100;
            if(sheet)
            {
                var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes(path));var pixels=image.GetPixels32();
                int cw=image.width/4,ch=image.height/2;
                // Shared crop dimensions preserve one body scale throughout the loop. Each
                // cell's opaque-foot baseline is aligned, without stretching individual frames.
                var bounds=new RectInt[8];int width=0,height=0;
                for(int i=0;i<8;i++)
                {
                    int x0=i%4*cw,y0=(1-i/4)*ch;
                    bounds[i]=Alpha(pixels,image.width,new RectInt(x0,y0,cw,ch));
                    width=Mathf.Max(width,bounds[i].width);height=Mathf.Max(height,bounds[i].height);
                }
                width=Mathf.Min(cw,width+8);height=Mathf.Min(ch,height+8);
                var meta=new SpriteMetaData[8];
                for(int i=0;i<8;i++)
                {
                    int x0=i%4*cw,y0=(1-i/4)*ch;
                    int x=Mathf.Clamp(Mathf.RoundToInt(bounds[i].center.x-width*.5f),x0,x0+cw-width);
                    int y=Mathf.Clamp(bounds[i].yMin-4,y0,y0+ch-height);
                    meta[i]=new SpriteMetaData{name=i.ToString("D2"),rect=new Rect(x,y,width,height),alignment=9,pivot=new Vector2(.5f,4f/height)};
                }
                importer.spritesheet=meta;UnityEngine.Object.DestroyImmediate(image);
            }
            importer.SaveAndReimport();
        }
        static RectInt Alpha(Color32[] pixels,int stride,RectInt cell)
        {
            int left=cell.xMax,right=cell.xMin-1,bottom=cell.yMax,top=cell.yMin-1;
            for(int y=cell.yMin;y<cell.yMax;y++)for(int x=cell.xMin;x<cell.xMax;x++)if(pixels[y*stride+x].a>32)
            {left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
            if(right<left)throw new InvalidOperationException("Empty sprite cell: "+cell);
            return new RectInt(left,bottom,right-left+1,top-bottom+1);
        }
        static void BakeIdleBounds()
        {
            var data=AssetDatabase.LoadAssetAtPath<VisibleBodyGeometry>("Assets/Resources/VisibleBodyGeometry.asset");
            var entries=new List<VisibleBodyGeometry.Entry>(data.entries);
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            foreach(var character in config.characters)
            {
                var sprite=character.idleSpriteSequence.First(s=>s!=null);
                var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
                float sx=(float)image.width/sprite.texture.width,sy=(float)image.height/sprite.texture.height;
                var rect=sprite.rect;
                var cell=new RectInt(Mathf.RoundToInt(rect.x*sx),Mathf.RoundToInt(rect.y*sy),Mathf.RoundToInt(rect.width*sx),Mathf.RoundToInt(rect.height*sy));
                var alpha=Alpha(image.GetPixels32(),image.width,cell);
                var bounds=new Rect((alpha.x/sx-rect.x-sprite.pivot.x)/sprite.pixelsPerUnit,(alpha.y/sy-rect.y-sprite.pivot.y)/sprite.pixelsPerUnit,
                    alpha.width/(sx*sprite.pixelsPerUnit),alpha.height/(sy*sprite.pixelsPerUnit));
                entries.RemoveAll(e=>e.sprite==sprite);
                entries.Add(new VisibleBodyGeometry.Entry{sprite=sprite,bounds=bounds,outline=sprite.vertices});
                UnityEngine.Object.DestroyImmediate(image);
                Debug.Log("TRAVEL_CALIBRATION "+character.name+" opaque/rectangle="+(bounds.height/sprite.bounds.size.y).ToString("F3"));
            }
            data.entries=entries.ToArray();EditorUtility.SetDirty(data);
        }
    }
}
