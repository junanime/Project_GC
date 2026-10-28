using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.EditorTools
{
    // Imports the approved 2026-10-24 walk/idle, preserving one scale and ground anchor.
    public static class HyukiApprovedMotionInstaller
    {
        public const string Art="Assets/Junhan/Art/CharacterDesigns/";
        public const string Blueprint="Assets/Blueprints/Characters/Hyuki Character Blueprint.asset";
        public const string Walk=Art+"Hyuki_ApprovedWalk.png",Idle=Art+"Hyuki_ApprovedIdle.png";
        public const string Dash=Art+"Hyuki_ApprovedDash.png",Captured=Art+"Hyuki_ApprovedCaptured.png";
        public const string Travel="Assets/Resources/BloodClotTravel/HyukiApproved.png";
        public const string Rear="Assets/Resources/StageEntryLoading/HyukiApproved.png";
        const int Cell=512;
        const float TargetHeight=.59f;
        public static void Run()
        {
            string[] args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-hyukiMotionSource");
            if(arg>=0&&arg+1<args.Length){Pack(args[arg+1],"frame",Walk);Pack(args[arg+1],"idle",Idle);}
            arg=Array.IndexOf(args,"-hyukiActionSource");
            if(arg>=0&&arg+1<args.Length)HyukiActionAtlasBaker.Bake(args[arg+1]);
            Install();
        }
        static void Pack(string source,string prefix,string output)
        {
            var atlas=new Texture2D(Cell*4,Cell*4,TextureFormat.RGBA32,false);
            for(int f=0;f<16;f++)
            {
                var frame=new Texture2D(2,2,TextureFormat.RGBA32,false);
                frame.LoadImage(File.ReadAllBytes(Path.Combine(source,prefix+"-"+f.ToString("00")+".png")));
                var raw=frame.GetPixels32();var pixels=new Color32[Cell*Cell];
                // Uniform nearest-neighbour sampling. The 1230x1278 canvas is padded,
                // never stretched into a square, and every frame shares the same view.
                for(int y=0;y<Cell;y++)for(int x=0;x<Cell;x++)
                {
                    int sx=Mathf.FloorToInt((x-10)/.4f),sy=Mathf.FloorToInt(y/.4f);
                    if(sx>=0&&sx<frame.width&&sy<frame.height)pixels[y*Cell+x]=raw[sy*frame.width+sx];
                }
                atlas.SetPixels32(f%4*Cell,(3-f/4)*Cell,Cell,Cell,pixels);Object.DestroyImmediate(frame);
            }
            atlas.Apply();File.WriteAllBytes(output,atlas.EncodeToPNG());Object.DestroyImmediate(atlas);
        }
        static Rect Alpha(Texture2D image,int f,bool omitBreath)
        {
            var pixels=image.GetPixels32();int columns=image.width/Cell,ox=f%columns*Cell,oy=(image.height/Cell-1-f/columns)*Cell,l=Cell,r=-1,b=Cell,t=-1;
            for(int y=0;y<Cell;y++)for(int x=0;x<Cell;x++)
            {
                if(omitBreath&&x>348&&y>252)continue;
                if(pixels[(oy+y)*image.width+ox+x].a<128)continue;
                l=Math.Min(l,x);r=Math.Max(r,x);b=Math.Min(b,y);t=Math.Max(t,y);
            }
            if(r<l)throw new InvalidOperationException("Empty frame "+f);
            return Rect.MinMaxRect(l,b,r+1,t+1);
        }
        static Texture2D Read(string path){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(path));return t;}
        [MenuItem("Tools/Characters/Install approved Hyuki motions")]
        public static void Install()
        {
            AssetDatabase.Refresh();var walkImage=Read(Walk);var idleImage=Read(Idle);
            var walkBounds=Enumerable.Range(0,16).Select(f=>Alpha(walkImage,f,false)).ToArray();
            var idleBounds=Enumerable.Range(0,16).Select(f=>Alpha(idleImage,f,true)).ToArray();
            float ppu=walkBounds.Average(b=>b.height)/TargetHeight;
            float floor=walkBounds.Concat(idleBounds).Min(b=>b.yMin);
            var pivot=new Vector2(.5f,(floor+ppu*.27f)/Cell);
            var walk=Import(Walk,ppu,pivot);var idle=Import(Idle,ppu,pivot);
            var dash=Import(Dash,ppu,pivot,8);var captured=Import(Captured,ppu,pivot,1);var travel=Import(Travel,ppu,pivot,5);
            var rear=Import(Rear,ppu,pivot,8);
            var loading=AssetDatabase.LoadAssetAtPath<StageEntryLoadingArt>("Assets/Resources/StageEntryLoadingArt.asset");
            loading.hyuki=rear;EditorUtility.SetDirty(loading);
            var bp=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>(Blueprint);
            bp.walkSpriteSequence=walk;bp.walkFrameTime=.06f;
            bp.idleSpriteSequence=idle;bp.idleFrameTime=.05f;
            bp.resultIdleSpriteSequence=idle;bp.resultIdleFrameTime=.05f;
            bp.dashSpriteSequence=dash;bp.dashFrameTime=.22f/8;bp.fitDashAnimationToDuration=true;bp.dashArtMovesRight=true;
            bp.capturedSpriteSequence=captured;bp.capturedFrameTime=.06f;
            EditorUtility.SetDirty(bp);
            var geometry=AssetDatabase.LoadAssetAtPath<VisibleBodyGeometry>("Assets/Resources/VisibleBodyGeometry.asset");
            var entries=new List<VisibleBodyGeometry.Entry>(geometry.entries);
            var replaced=new[]{Walk,Idle,Dash,Captured,Travel,Art+"Hyuki_Walk.png",Art+"Hyuki_ColdIdle.png"};
            entries.RemoveAll(e=>e.sprite!=null&&replaced.Contains(AssetDatabase.GetAssetPath(e.sprite)));
            for(int i=0;i<16;i++){Add(entries,walk[i],walkBounds[i]);Add(entries,idle[i],idleBounds[i]);}
            foreach(var set in new[]{dash,captured,travel})
            {
                var texture=Read(AssetDatabase.GetAssetPath(set[0]));
                for(int i=0;i<set.Length;i++)Add(entries,set[i],Alpha(texture,i,false));
                Object.DestroyImmediate(texture);
            }
            geometry.entries=entries.ToArray();EditorUtility.SetDirty(geometry);AssetDatabase.SaveAssets();
            Object.DestroyImmediate(walkImage);Object.DestroyImmediate(idleImage);
            Debug.Log("[HyukiMotion] Installed 16 walk + 16 idle; loops 0.96/0.80 s; height="+TargetHeight+"; PPU="+ppu+"; floor="+floor);
        }
        static Sprite[] Import(string path,float ppu,Vector2 pivot,int count=16)
        {
            var i=(TextureImporter)AssetImporter.GetAtPath(path);i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Multiple;
            i.spritePixelsPerUnit=ppu;i.mipmapEnabled=false;i.alphaIsTransparency=true;i.filterMode=FilterMode.Point;i.textureCompression=TextureImporterCompression.Uncompressed;i.maxTextureSize=2048;i.isReadable=false;
            int columns=Math.Min(4,count),rows=Mathf.CeilToInt(count/(float)columns);
            i.spritesheet=Enumerable.Range(0,count).Select(f=>new SpriteMetaData{name=Path.GetFileNameWithoutExtension(path)+"_"+f.ToString("00"),rect=new Rect(f%columns*Cell,(rows-1-f/columns)*Cell,Cell,Cell),alignment=9,pivot=pivot}).ToArray();
            var settings=new TextureImporterSettings();i.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;i.SetTextureSettings(settings);i.SaveAndReimport();
            var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
            if(sprites.Length!=count)throw new InvalidOperationException("Unexpected sprite count: "+path);return sprites;
        }
        static void Add(List<VisibleBodyGeometry.Entry> entries,Sprite sprite,Rect pixels)
        {
            var r=new Rect((pixels.position-sprite.pivot)/sprite.pixelsPerUnit,pixels.size/sprite.pixelsPerUnit);
            entries.Add(new VisibleBodyGeometry.Entry{sprite=sprite,bounds=r,outline=new[]{r.min,new Vector2(r.xMax,r.yMin),r.max,new Vector2(r.xMin,r.yMax)}});
        }
    }
}
