using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Vampire.Editor
{
    // Offline packing of six authored north/south poses; side artwork is untouched.
    public static class ShiniVerticalCastInstaller
    {
        const int Cell=384,Floor=24;
        const string Folder="Assets/Junhan/Art/ShiniEmber/";
        static RectInt Bounds(Color[] p,int w)
        {
            int l=w,r=-1,b=p.Length/w,t=-1;
            for(int i=0;i<p.Length;i++)if(p[i].a>.75f){int x=i%w,y=i/w;l=Math.Min(l,x);r=Math.Max(r,x);b=Math.Min(b,y);t=Math.Max(t,y);}
            if(r<l)throw new Exception("Empty Shini pose");return new RectInt(l,b,r-l+1,t-b+1);
        }
        public static void Run()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-shiniVerticalSource");
            var art=AssetDatabase.LoadAssetAtPath<ShiniEmberArt>("Assets/Junhan/Resources/ShiniEmberArt.asset");
            if(at>=0)
            {
                var source=new Texture2D(2,2,TextureFormat.RGBA32,false);source.LoadImage(File.ReadAllBytes(args[at+1]));
                int w=source.width/3,h=source.height/2;
                for(int row=0;row<2;row++)
                {
                    var poses=Enumerable.Range(0,3).Select(c=>source.GetPixels(c*w,(1-row)*h,w,h)).ToArray();
                    var bounds=poses.Select(p=>Bounds(p,w)).ToArray();
                    // A single calibration per direction keeps head and body scale stable
                    // through anticipation, emission and recovery (open beak may be taller).
                    float scale=art.castHeight*100/bounds[0].height;
                    var atlas=new Texture2D(Cell*4,Cell*3,TextureFormat.RGBA32,false);atlas.SetPixels(new Color[atlas.width*atlas.height]);
                    for(int f=0;f<12;f++)
                    {
                        int key=f==0||f>=10?2:f>=5&&f<=8?1:0;
                        float sy=f==2?1.02f:f==3?1.04f:f==4?1.03f:f==6?.995f:f==8?1.005f:1;
                        var pixels=new Color[Cell*Cell];var b=bounds[key];
                        for(int y=0;y<Cell;y++)for(int x=0;x<Cell;x++)
                        {
                            int sx=Mathf.FloorToInt((x-Cell*.5f)/scale+b.center.x),yy=Mathf.FloorToInt((y-Floor)/(scale*sy)+b.yMin);
                            if(sx<0||sx>=w||yy<0||yy>=h)continue;
                            var c=poses[key][yy*w+sx];if(c.a>.75f){c.a=1;pixels[y*Cell+x]=c;}
                        }
                        atlas.SetPixels(f%4*Cell,(2-f/4)*Cell,Cell,Cell,pixels);
                    }
                    atlas.Apply();File.WriteAllBytes(Folder+(row==0?"CastUp":"CastDown")+".png",atlas.EncodeToPNG());UnityEngine.Object.DestroyImmediate(atlas);
                }
                UnityEngine.Object.DestroyImmediate(source);AssetDatabase.Refresh();
            }
            art.castUp=Import(Folder+"CastUp.png",true,out art.mouthsUp);
            art.castDown=Import(Folder+"CastDown.png",false,out art.mouthsDown);
            EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();Debug.Log("[ShiniVerticalCast] PASS 24 frames; side Cast/Flame unchanged");
        }
        static Sprite[] Import(string path,bool up,out Vector2[] mouths)
        {
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Multiple;
            imp.spritePixelsPerUnit=100;imp.filterMode=FilterMode.Point;imp.textureCompression=TextureImporterCompression.Uncompressed;
            imp.maxTextureSize=2048;imp.mipmapEnabled=false;imp.alphaIsTransparency=true;imp.isReadable=true;imp.SaveAndReimport();
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);mouths=new Vector2[12];
            var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(imp);provider.InitSpriteEditorDataProvider();
            var old=provider.GetSpriteRects();var rects=new SpriteRect[12];
            for(int f=0;f<12;f++)
            {
                int x=f%4*Cell,y=(2-f/4)*Cell;var p=tex.GetPixels(x,y,Cell,Cell);int l=Cell,r=-1,b=Cell,t=-1;
                for(int j=Floor+50;j<Cell;j++)for(int i=0;i<Cell;i++)
                {var c=p[j*Cell+i];if(c.a>.6f&&c.r>.59f&&c.g>.45f&&c.b<.37f&&c.g/c.r>.5f){l=Math.Min(l,i);r=Math.Max(r,i);b=Math.Min(b,j);t=Math.Max(t,j);}}
                if(r<l)throw new Exception("No beak on "+path+" frame "+f);
                // Existing side anchors are three source pixels beyond the yellow tip.
                mouths[f]=new Vector2((l+r)*.5f-Cell*.5f,(up?t+3:b-3)-Floor)/100f;
                string name="frame_"+f.ToString("D2");
                rects[f]=new SpriteRect{name=name,rect=new Rect(x,y,Cell,Cell),alignment=SpriteAlignment.Custom,pivot=new Vector2(.5f,Floor/(float)Cell),spriteID=old.FirstOrDefault(o=>o.name==name)?.spriteID??GUID.Generate()};
            }
            provider.SetSpriteRects(rects);var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();names?.SetNameFileIdPairs(rects.Select(s=>new SpriteNameFileIdPair(s.name,s.spriteID)));
            provider.Apply();imp.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        }
    }
}
