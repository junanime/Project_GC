using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.EditorTools
{
    // Offline atlas packing of approved artwork. One head calibration per pose;
    // animation never fits individual frames to their changing opaque bounds.
    public static class HyukiActionAtlasBaker
    {
        const int N=512;
        static Texture2D Read(string p){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(p));return t;}
        static Color32[] Cell(Texture2D t,int f)=>ToBytes(t.GetPixels(f%4*N,(3-f/4)*N,N,N));
        static Color32[] ToBytes(Color[] c){var a=new Color32[c.Length];for(int i=0;i<c.Length;i++)a[i]=c[i];return a;}
        static RectInt Bounds(Color32[] p,int w,Predicate<Color32> include)
        {
            int l=w,r=-1,b=p.Length/w,t=-1;
            for(int i=0;i<p.Length;i++)if(include(p[i])){int x=i%w,y=i/w;l=Math.Min(l,x);r=Math.Max(r,x);b=Math.Min(b,y);t=Math.Max(t,y);}
            if(r<l)throw new Exception("Empty pose");return new RectInt(l,b,r-l+1,t-b+1);
        }
        static RectInt Head(Color32[] p,int w)
        {
            // Largest connected blue region is the head, never a blue glove or VFX.
            var mask=new bool[p.Length];for(int i=0;i<p.Length;i++)mask[i]=p[i].a>128&&p[i].b>p[i].r*1.25f&&p[i].b>p[i].g*1.05f;
            var queue=new Queue<int>();int best=0;RectInt result=default;
            for(int i=0;i<p.Length;i++)if(mask[i])
            {
                int count=0,l=w,r=0,b=p.Length/w,t=0;mask[i]=false;queue.Enqueue(i);
                while(queue.Count>0)
                {
                    int k=queue.Dequeue(),x=k%w,y=k/w;count++;l=Math.Min(l,x);r=Math.Max(r,x);b=Math.Min(b,y);t=Math.Max(t,y);
                    if(x>0)Visit(k-1);if(x+1<w)Visit(k+1);if(k>=w)Visit(k-w);if(k+w<p.Length)Visit(k+w);
                }
                if(count>best){best=count;result=new RectInt(l,b,r-l+1,t-b+1);}
            }
            if(best<100)throw new Exception("Missing head");return result;
            void Visit(int n){if(mask[n]){mask[n]=false;queue.Enqueue(n);}}
        }
        static Color32[] Pose(string path,RectInt targetHead,int floor)
        {
            var source=Read(path);var p=source.GetPixels32();var h=Head(p,source.width);var a=Bounds(p,source.width,c=>c.a>128);
            float scale=targetHead.height/(float)h.height,dx=targetHead.center.x-h.center.x*scale,dy=floor-a.yMin*scale;
            var result=new Color32[N*N];
            for(int y=0;y<N;y++)for(int x=0;x<N;x++)
            {
                int sx=Mathf.FloorToInt((x-dx)/scale),sy=Mathf.FloorToInt((y-dy)/scale);
                if(sx>=0&&sx<source.width&&sy>=0&&sy<source.height)result[y*N+x]=p[sy*source.width+sx];
            }
            var actual=Head(result,N);Debug.Log("[HyukiAction] "+Path.GetFileName(path)+" head="+actual+" scale="+scale+" body="+Bounds(result,N,c=>c.a>128));
            Object.DestroyImmediate(source);return result;
        }
        static Color32[] Shift(Color32[] p,int dx,int dy)
        {
            var r=new Color32[p.Length];for(int y=0;y<N;y++)for(int x=0;x<N;x++){int sx=x-dx,sy=y-dy;if(sx>=0&&sx<N&&sy>=0&&sy<N)r[y*N+x]=p[sy*N+sx];}return r;
        }
        static void Save(string path,Color32[][] frames)
        {
            int cols=Math.Min(4,frames.Length),rows=Mathf.CeilToInt(frames.Length/(float)cols);
            var t=new Texture2D(N*cols,N*rows,TextureFormat.RGBA32,false);
            t.SetPixels32(new Color32[t.width*t.height]);
            for(int i=0;i<frames.Length;i++)t.SetPixels32(i%cols*N,(rows-1-i/cols)*N,N,N,frames[i]);
            t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);
        }
        public static void Bake(string source)
        {
            var walk=Read(HyukiApprovedMotionInstaller.Walk);var idle=Read(HyukiApprovedMotionInstaller.Idle);
            var head=Head(Cell(walk,0),N);const int floor=32;
            var dash=Pose(Path.Combine(source,"dash.png"),head,floor);
            var captured=Pose(Path.Combine(source,"captured.png"),head,floor);
            var curl=Pose(Path.Combine(source,"curl.png"),head,floor);
            var d=new Color32[8][];var c=new[]{captured};
            for(int i=0;i<8;i++)d[i]=i<7?Shift(dash,0,Mathf.RoundToInt(Mathf.Sin(i/6f*Mathf.PI))):Cell(walk,0);
            var travel=new[]{Cell(walk,0),Cell(walk,8),Cell(walk,0),dash,curl};
            Save(HyukiApprovedMotionInstaller.Dash,d);Save(HyukiApprovedMotionInstaller.Captured,c);Save(HyukiApprovedMotionInstaller.Travel,travel);
            var rear=Pose(Path.Combine(source,"rear.png"),head,floor);var rearFrames=new Color32[8][];
            for(int f=0;f<8;f++)
            {
                var result=new Color32[N*N];float phase=f*Mathf.PI/4;
                for(int y=0;y<N;y++)for(int x=0;x<N;x++)
                {
                    float weight=1-Mathf.SmoothStep(0,1,(y-floor)/120f),step=Mathf.Sin(phase)*(x<head.center.x?-1:1);
                    int sx=Mathf.RoundToInt(x-step*5*weight),sy=Mathf.RoundToInt(y-Mathf.Max(0,step)*4*weight);
                    if(sx>=0&&sx<N&&sy>=0&&sy<N)result[y*N+x]=rear[sy*N+sx];
                }
                rearFrames[f]=result;
            }
            Save(HyukiApprovedMotionInstaller.Rear,rearFrames);
            // Freeze body and ice have separate calibration: the ice grows, the body does not.
            var prison=Read("Assets/Junhan/Art/PhoenixSkills/IcePrison.png");int iw=prison.width/4,ih=prison.height/2;
            var exit=new Color32[16][];var still=Pose(Path.Combine(source,"idle-pose.png"),head,floor);
            for(int f=0;f<16;f++)
            {
                exit[f]=(Color32[])still.Clone();if(f<6)continue;
                var ice=prison.GetPixels(f%4*iw,(1-(f%8)/4)*ih,iw,ih);
                float reveal=Mathf.SmoothStep(0,1,(f-5)/5f);int iceHeight=Mathf.Max(1,Mathf.RoundToInt(485*reveal)),top=12+iceHeight;
                for(int y=12;y<Math.Min(N,top);y++)for(int x=72;x<448;x++)
                {
                    // Grow the complete crystal contour from the floor, never a flat
                    // horizontal crop cutting through the textured ice.
                    int sx=Mathf.Clamp((x-72)*iw/376,0,iw-1),sy=Mathf.Clamp((y-12)*ih/iceHeight,0,ih-1);
                    var over=ice[sy*iw+sx];over.a*=.62f;var under=(Color)exit[f][y*N+x];
                    float a=over.a+under.a*(1-over.a);if(a<=0)continue;
                    var result=(over*over.a+under*(under.a*(1-over.a)))/a;result.a=a;exit[f][y*N+x]=result;
                }
            }
            Save("Assets/Resources/CharacterExits/hyuki.png",exit);
            var json=File.ReadAllText("Assets/Resources/CharacterExits/hyuki.json");
            var settings=JsonUtility.FromJson<ExitData>(json);settings.cellSize=N;settings.pivotY=floor/(float)N;settings.referenceHeight=Bounds(still,N,p=>p.a>128).height/100f;
            File.WriteAllText("Assets/Resources/CharacterExits/hyuki.json",JsonUtility.ToJson(settings,true));
            Object.DestroyImmediate(walk);Object.DestroyImmediate(idle);Object.DestroyImmediate(prison);
        }
        [Serializable]class ExitData{public int cellSize,columns,frameCount;public float pivotX,pivotY,referenceHeight,endHold;public int[] order;public float[] durations;public bool hideAtEnd;}
    }
}
