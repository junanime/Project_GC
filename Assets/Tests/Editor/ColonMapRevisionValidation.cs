#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
 public static class ColonMapRevisionValidation
 {
  const string Output="Library/ColonMapRevision";
  static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
  static bool presentationOnly;
  public static void RenderOnly(){presentationOnly=true;Run();}
  static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
  public static void Run()
  {
   Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var errors=new List<string>();int homes=0;float minGap=999;
   var go=new GameObject("Colon map revision validation");var map=go.AddComponent<FiniteStomachMap>();
   try
   {
    var def=Resources.Load<StomachMapDefinition>("ColonMap/Stage2");
    var one=Resources.Load<StomachMapDefinition>("StomachMap/Stage1");
    Require(Mathf.Abs(def.worldWidth/one.worldWidth-.8f)<.0001f,"Map width must be 80% of stage one");
    typeof(FiniteStomachMap).GetProperty("Definition").SetValue(map,def);
    typeof(FiniteStomachMap).GetProperty("WorldRect").SetValue(map,new Rect(Vector2.zero,def.Size));
    var mat=new Material(def.terrainMaterial);mat.SetVector("_MapRect",new Vector4(0,0,def.Size.x,def.Size.y));mat.SetFloat("_PixelSize",def.pixelWorldSize);mat.SetVector("_Inlet",def.inlet);mat.SetVector("_Outlet",def.outlet);
    typeof(FiniteStomachMap).GetField("material",Flags).SetValue(map,mat);
    typeof(FiniteStomachMap).GetField("boundaryLength",Flags).SetValue(map,1f);
    typeof(FiniteStomachMap).GetMethod("BuildDistanceField",Flags).Invoke(map,null);
    var entry=map.ClampToField(map.UVToWorld(def.startUV),map.WallWidth+2);
    const float cell=1f;int w=Mathf.CeilToInt(def.Size.x),h=Mathf.CeilToInt(def.Size.y);
    var safe=new bool[w*h];var reached=new bool[w*h];var q=new Queue<int>();
    Func<int,Vector2> point=i=>new Vector2(i%w+.5f,i/w+.5f)*cell;
    for(int i=0;i<safe.Length;i++)safe[i]=map.NavigationDistance(point(i),out _)>=map.ClimbLimit+2.3f;
    int start=Mathf.Clamp((int)entry.y,0,h-1)*w+Mathf.Clamp((int)entry.x,0,w-1);Require(safe[start],"Entry not boss-navigable");q.Enqueue(start);reached[start]=true;
    while(q.Count>0){int at=q.Dequeue();foreach(int off in new[]{-1,1,-w,w}){int n=at+off;if(n<0||n>=safe.Length||Mathf.Abs(n%w-at%w)+Mathf.Abs(n/w-at/w)!=1||!safe[n]||reached[n])continue;reached[n]=true;q.Enqueue(n);}}
    int islands=safe.Where((v,i)=>v&&!reached[i]).Count();Require(islands==0,"Disconnected boss-floor cells: "+islands);
    var passage=map.UVToWorld(new Vector2(410f/1536,1-824f/1024));Require(map.NavigationDistance(passage,out _,true)>map.ClimbLimit+2.3f,"Outlet-right passage blocked");
    for(int sample=0;sample<(presentationOnly?0:256);sample++)
    {
     File.WriteAllText(Output+"/progress.txt",sample+"/256"); int seed=11717+sample*7919;
     try{
      var props=FieldObjectLayout.Generate(map,entry,seed);Require(props.Count==86,"Missing field object");
      var bosses=ColonBossDirector.Generate(map,entry,seed^0x62B7,props);Require(bosses.Count==6,"Missing habitat");
      foreach(var b in bosses){homes++;Require(b.kind==ColonBossKind.Manatee?ColonBossDirector.HouseFits(map,b.position,b.wallNormal):ColonBossDirector.GroundFits(map,b.position,ColonBossDefinition.Get(b.kind).HabitatRadius),"Unsafe habitat");}
      foreach(var p in props){Require(map.FloorClearance(p.position,true)>=1.99f,"Prop overlaps visible wall");int at=(int)p.position.y*w+(int)p.position.x;Require(at>=0&&at<reached.Length&&reached[at],"Unreachable prop");}
      for(int i=0;i<bosses.Count;i++)for(int j=0;j<i;j++){float gap=Vector2.Distance(bosses[i].position,bosses[j].position);minGap=Mathf.Min(minGap,gap);Require(gap+0.001f>=(bosses[i].partner==j?ColonBossDirector.MinimumSeparation:ColonBossDirector.PreferredSeparation),"Home spacing failure");}
     }catch(Exception ex){if(sample==0)throw;errors.Add(seed+": "+ex.Message);}
    }
    for(int i=0;i<1024;i++){var at=map.UVToWorld(new Vector2((i%32)/31f,(i/32)/31f));var safePoint=map.ClampToFloor(at,1.2f);
     Require(map.FloorClearance(safePoint,true)>=1.199f,"Unsafe corner projection "+i);}
    // Render exactly the material used by the game, at overview and gameplay scale.
    var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.position=def.Size*.5f;quad.transform.localScale=new Vector3(def.Size.x,def.Size.y,1);quad.GetComponent<Renderer>().sharedMaterial=mat;
    var c=new GameObject("Map render camera").AddComponent<Camera>();c.enabled=false;c.orthographic=true;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.29f,.065f,.13f);c.nearClipPlane=.1f;c.farClipPlane=50;
    Render(c,def.Size*.5f,def.Size.y*.5f,"overview",1536,1024);
    Render(c,map.UVToWorld(new Vector2(.48f,.56f)),4.5f,"floor-detail",1280,720);
    Render(c,map.UVToWorld(new Vector2(.49f,.65f)),6,"inner-wall-detail",1280,720);
    Render(c,map.UVToWorld(new Vector2(.48f,.80f)),6,"outer-wall-detail",1280,720);
    Render(c,map.UVToWorld(new Vector2(def.outlet.x,def.outlet.y)),8,"outlet-closed",1280,720);
    mat.SetFloat("_OutletOpen",1);
    Render(c,map.UVToWorld(new Vector2(def.outlet.x,def.outlet.y)),8,"outlet-open",1280,720);
    mat.SetFloat("_InletOpen",1);
    Render(c,map.UVToWorld(new Vector2(def.inlet.x,def.inlet.y)),8,"inlet-open",1280,720);
    Object.DestroyImmediate(c.gameObject);Object.DestroyImmediate(quad);
    File.WriteAllText(Output+"/report.txt",$"{(presentationOnly?0:256)} seeds; presentationOnly={presentationOnly}; {homes} homes; disconnected cells={islands}; minimum home gap={minGap:F3}; errors={errors.Count}\n"+string.Join("\n",errors));
    Require(errors.Count==0,"Placement failures: "+string.Join(";",errors.Take(3)));
    File.WriteAllText(Output+"/result.txt","PASS");Debug.Log("COLON_MAP_REVISION_PASS");EditorApplication.Exit(0);
   }catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
   finally{Object.DestroyImmediate(go);}
  }
  static void Render(Camera c,Vector2 p,float size,string name,int width,int height)
  {
   c.transform.position=new Vector3(p.x,p.y,-10);c.orthographicSize=size;c.aspect=width/(float)height;
   var rt=RenderTexture.GetTemporary(width,height,24);var old=RenderTexture.active;c.targetTexture=rt;c.Render();RenderTexture.active=rt;
   var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+name+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);c.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);
  }
 }
}
#endif
