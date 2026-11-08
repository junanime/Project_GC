using UnityEngine;
namespace Vampire {
 // One floor/occlusion rule for warning pixels, travelling effects and damage.
 public static class ColonAttackWall {
  public static bool Clear(Vector2 a,Vector2 b,float radius=0){return (Stop(a,b,radius)-b).sqrMagnitude<.00001f;}
  public static Vector2 Stop(Vector2 a,Vector2 b,float radius=0){
   var m=FiniteStomachMap.Instance;if(m==null||!m.IsColon||!m.FieldActive||MiniStageRuntimeState.IsInsideMiniStage)return b;
   int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.15f));Vector2 last=a;
   if(m.FloorClearance(a)<radius+.1f)return a;
   for(int i=1;i<=steps;i++){var p=Vector2.Lerp(a,b,i/(float)steps);if(m.FloorClearance(p)<radius+.1f)return last;last=p;}return b;
  }
  public static void Mask(GameObject root,Vector2 origin){
   var m=FiniteStomachMap.Instance;if(m==null||!m.IsColon||m.FloorTexture==null)return;
   var block=new MaterialPropertyBlock();
   foreach(var r in root.GetComponentsInChildren<Renderer>()){
    r.GetPropertyBlock(block);block.SetFloat("_ColonClip",1);block.SetTexture("_ColonFloorTex",m.FloorTexture);
    block.SetVector("_ColonFloorRect",new Vector4(m.WorldRect.xMin,m.WorldRect.yMin,m.WorldRect.width,m.WorldRect.height));
    block.SetVector("_ColonOrigin",origin);r.SetPropertyBlock(block);}
  }
 }
}
