using UnityEngine;
namespace Vampire {
 public sealed class ColonFinWake:MonoBehaviour {
  SpriteRenderer fin;LineRenderer[] ripples;SpriteRenderer[] foam;float clock;Vector2 last,direction=Vector2.right;
  void Start(){fin=GetComponent<SpriteRenderer>();last=transform.position;ripples=new LineRenderer[3];for(int i=0;i<3;i++)ripples[i]=FoodBossEffects.Line(transform,new[]{last,last},i==0?.045f:.025f,new Color(1,.87f,.64f,i==0?.8f:.45f),false,fin.sortingOrder-1,true);
   foam=new SpriteRenderer[7];for(int i=0;i<foam.Length;i++){var r=new GameObject("Fin contact foam").AddComponent<SpriteRenderer>();r.transform.SetParent(transform,false);r.sprite=BombCircleArt.Sprite;r.color=new Color(1,.94f,.8f,.8f);r.sortingOrder=fin.sortingOrder+1;foam[i]=r;}
  }
  void LateUpdate(){if(fin==null)return;if(Time.timeScale<=0||MiniStageRuntimeState.IsInsideMiniStage||FoodBossRuntime.Paused)return;clock+=Time.deltaTime;Vector2 p=new Vector2(fin.bounds.center.x,fin.bounds.min.y+.025f);Vector2 delta=(Vector2)transform.position-last;if(delta.sqrMagnitude>.00001f)direction=Vector2.Lerp(direction,delta.normalized,.2f).normalized;last=transform.position;
   var side=Vector2.Perpendicular(direction);float width=fin.bounds.size.x*.6f;
   // Always stay on the shallow ground plane, including a stationary fin.
   for(int k=0;k<3;k++){var points=new Vector3[33];float pulse=Mathf.Repeat(clock*.7f+k/3f,1);for(int i=0;i<33;i++){float a=i/32f*Mathf.PI*2;points[i]=p-direction*(pulse*.18f)+new Vector2(Mathf.Cos(a)*width*(1+pulse*.4f),Mathf.Sin(a)*width*(.15f+pulse*.10f));}ripples[k].positionCount=33;ripples[k].SetPositions(points);ripples[k].enabled=fin.enabled;}
   for(int i=0;i<foam.Length;i++){float a=i*Mathf.PI*2/foam.Length+clock*.25f;foam[i].transform.position=p+new Vector2(Mathf.Cos(a)*width,Mathf.Sin(a)*width*.13f);float size=.035f+.018f*(1+Mathf.Sin(clock*5+i));foam[i].transform.localScale=new Vector3(size/foam[i].sprite.bounds.size.x/transform.lossyScale.x,size/foam[i].sprite.bounds.size.y/transform.lossyScale.y,1);foam[i].enabled=fin.enabled;}
  }
 }
}
