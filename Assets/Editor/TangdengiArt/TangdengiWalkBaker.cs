using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Editor
{
    // Deterministic UV animation of the approved illustration. No generated in-between poses.
    public static class TangdengiWalkBaker
    {
        public const int Width=320,Height=224,Columns=4,Rows=5;
        const string Folder="Assets/Editor/TangdengiArt/";
        const string Output="Assets/Resources/FoodBosses/";
        sealed class Layer {public Mesh mesh;public Vector3[] rest,posed;public Vector2[] pixels;public int part;public Material material;}
        [MenuItem("24tu/Art/Bake approved six-leg Tangdengi")]
        public static void Bake()
        {
            try {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                for(int phase=0;phase<2;phase++)BakePhase(phase);
                AssetDatabase.Refresh();Debug.Log("TANGDENGI_BAKE_PASS: two phases, rest + sixteen walk frames + folded flight pose");
                if(Application.isBatchMode)EditorApplication.Exit(0);
            } catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);throw;}
        }
        static void BakePhase(int phase)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(File.ReadAllBytes(Folder+$"ApprovedPhase{phase+1}.png"));texture.filterMode=FilterMode.Bilinear;
            // Equal shoulder diameter and fixed ground plane in both phases. Horn growth never rescales the torso.
            float scale=phase==0?244f/1496f:244f/1496f*1.055f;
            float center=phase==0?823:824,bottom=phase==0?871:856;
            // One connected UV surface avoids gaps at occluded joints. Six weighted leg fields
            // alternate while the approved torso remains rigid; no raster pieces are cut apart.
            var root=new GameObject("Approved Tangdengi bake rig");var layers=new Layer[1];
            for(int part=0;part<1;part++){
                var go=new GameObject("Continuous six-leg surface");go.transform.SetParent(root.transform,false);
                var l=new Layer{part=part,mesh=new Mesh(),material=new Material(Shader.Find("Hidden/Tangdengi/ApprovedArtBake"))};layers[part]=l;
                l.material.mainTexture=texture;l.material.SetFloat("_Part",-1);l.material.SetFloat("_Phase",phase);
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=l.material;go.AddComponent<MeshFilter>().sharedMesh=l.mesh;
                const int nx=210,ny=120;l.rest=new Vector3[(nx+1)*(ny+1)];l.posed=new Vector3[l.rest.Length];l.pixels=new Vector2[l.rest.Length];var uv=new Vector2[l.rest.Length];
                for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++){
                    int i=y*(nx+1)+x;float px=x*texture.width/(float)nx,py=y*texture.height/(float)ny;
                    l.pixels[i]=new Vector2(px,py);l.rest[i]=new Vector3(Width*.5f+(px-center)*scale,4+(bottom-py)*scale,0);uv[i]=new Vector2(px/texture.width,1-py/texture.height);
                }
                var triangles=new int[nx*ny*6];int t=0;
                for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){int n=y*(nx+1)+x;triangles[t++]=n;triangles[t++]=n+1;triangles[t++]=n+nx+1;triangles[t++]=n+1;triangles[t++]=n+nx+2;triangles[t++]=n+nx+1;}
                l.mesh.vertices=l.rest;l.mesh.uv=uv;l.mesh.triangles=triangles;l.mesh.MarkDynamic();l.mesh.RecalculateBounds();
            }
            var camera=new GameObject("Bake camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=Height*.5f;camera.aspect=Width/(float)Height;camera.transform.position=new Vector3(Width*.5f,Height*.5f,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.allowHDR=false;camera.allowMSAA=false;
            var rt=new RenderTexture(Width*2,Height*2,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;
            var atlas=new Texture2D(Width*Columns,Height*Rows,TextureFormat.RGBA32,false);atlas.SetPixels32(new Color32[atlas.width*atlas.height]);
            Directory.CreateDirectory("Library/TangdengiBake");
            for(int frame=0;frame<18;frame++){
                float angle=(frame-1)*Mathf.PI*2/16;float bob=frame==0||frame==17?0:.35f*(1-Mathf.Cos(angle*2));
                foreach(var l in layers){
                    for(int i=0;i<l.rest.Length;i++){
                        var v=l.rest[i];var pixel=l.pixels[i];float total=0;
                        for(int leg=1;leg<=6;leg++){
                            float rootY=phase==0?(leg==1?645:leg==2?602:leg==3?700:leg==4?610:leg==5?702:615):(leg==1?642:leg==2?601:leg==3?697:leg==4?604:leg==5?701:610);
                            float left=leg==1?1:Mathf.SmoothStep(0,1,Mathf.InverseLerp(Boundary(phase,leg-2,pixel.y)-32,Boundary(phase,leg-2,pixel.y)+32,pixel.x));
                            float right=leg==6?0:Mathf.SmoothStep(0,1,Mathf.InverseLerp(Boundary(phase,leg-1,pixel.y)-32,Boundary(phase,leg-1,pixel.y)+32,pixel.x));
                            float w=Mathf.Max(0,left-right)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(rootY+12,phase==0?835:820,pixel.y));
                            float a=angle+(leg==1||leg==4||leg==5?Mathf.PI:0);
                            float stride=frame==0||frame==17?0:-Mathf.Sin(a)*20;
                            float lift=frame==0?0:frame==17?35:Mathf.Pow(Mathf.Max(0,Mathf.Cos(a)),2)*15;
                            v.x+=stride*scale*w;v.y+=lift*scale*w;total+=w;
                        }
                        v.y+=bob*(1-total);l.posed[i]=v;
                    }
                    l.mesh.vertices=l.posed;l.mesh.RecalculateBounds();
                }
                camera.Render();RenderTexture.active=rt;var high=new Texture2D(Width*2,Height*2,TextureFormat.RGBA32,false);high.ReadPixels(new Rect(0,0,Width*2,Height*2),0,0);high.Apply();
                // Resolve premultiplied camera pixels to straight-alpha PNG, averaging the 2x coverage samples.
                var source=high.GetPixels();var pixels=new Color[Width*Height];
                for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){
                    int i=y*2*Width*2+x*2;Color c=(source[i]+source[i+1]+source[i+Width*2]+source[i+Width*2+1])*.25f;
                    if(c.a>.001f){c.r/=c.a;c.g/=c.a;c.b/=c.a;}else c=Color.clear;pixels[y*Width+x]=c;
                }
                atlas.SetPixels(frame%4*Width,(Rows-1-frame/4)*Height,Width,Height,pixels);
                var proof=new Texture2D(Width,Height,TextureFormat.RGBA32,false);proof.SetPixels(pixels);proof.Apply();File.WriteAllBytes($"Library/TangdengiBake/p{phase+1}-{frame:D2}.png",proof.EncodeToPNG());Object.DestroyImmediate(proof);Object.DestroyImmediate(high);
            }
            atlas.Apply();File.WriteAllBytes(Output+$"Walk3P{phase+1}.png",atlas.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(root);Object.DestroyImmediate(texture);Object.DestroyImmediate(atlas);
            foreach(var l in layers){Object.DestroyImmediate(l.mesh);Object.DestroyImmediate(l.material);}
        }
        static readonly float[][] edges={new[]{680f,535,872,780,946,993,1184,1240,1290,1374},new[]{773f,646,944,850,1014,1065,1215,1278,1310,1408}};
        static float Boundary(int phase,int index,float y)=>Mathf.Lerp(edges[phase][index*2],edges[phase][index*2+1],Mathf.Clamp01((y-680)/180));
    }
}
