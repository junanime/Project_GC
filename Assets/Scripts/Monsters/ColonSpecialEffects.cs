using System.Collections;
using UnityEngine;
namespace Vampire {
 public sealed class ColonSpecialEffects:MonoBehaviour {
  FiniteStomachMap map; LevelManager level;
  public static ColonSpecialEffects Create(){var g=new GameObject("Colon special effect");var e=g.AddComponent<ColonSpecialEffects>();e.map=FiniteStomachMap.Instance;e.level=FindObjectOfType<LevelManager>();return e;}
  public bool Paused=>MiniStageRuntimeState.IsInsideMiniStage||(level!=null&&level.IsRunFlowPaused);
  void Update(){if(map!=FiniteStomachMap.Instance||map!=null&&!map.IsColon)Destroy(gameObject);}
  public static Sprite Art(string id,int frame)=>Resources.Load<Sprite>("ColonSpecials/"+id+"/"+frame.ToString("00"));
  public SpriteRenderer Sprite(Sprite art,Vector2 p,float h,Color color,int order=120){var r=new GameObject("Effect sprite").AddComponent<SpriteRenderer>();r.transform.SetParent(transform);r.transform.position=p;r.sprite=art;r.color=color;r.sortingOrder=order;if(art!=null)r.transform.localScale=Vector3.one*h/art.bounds.size.y;return r;}
  public IEnumerator Wait(float seconds){for(float t=0;t<seconds;){if(!Paused)t+=Time.deltaTime;yield return null;}}
  public static void Puff(Vector2 at,Color color,float radius=.45f){var e=Create();e.StartCoroutine(e.PuffRoutine(at,color,radius));}
  IEnumerator PuffRoutine(Vector2 p,Color c,float radius){var dots=new SpriteRenderer[8];for(int i=0;i<8;i++)dots[i]=Sprite(BombCircleArt.Sprite,p,.09f,c);for(float t=0;t<.6f;){if(!Paused){t+=Time.deltaTime;for(int i=0;i<8;i++){float a=i*Mathf.PI/4;dots[i].transform.position=ColonAttackWall.Stop(p,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(radius*t/.6f));dots[i].color=new Color(c.r,c.g,c.b,1-t/.6f);}}yield return null;}Destroy(gameObject);}
  public static void Shot(Sprite art,Vector2 from,Vector2 to,float travel,float damage,MonsterBlueprint source,bool dung=false,bool dropped=false){var e=Create();e.StartCoroutine(e.Flight(art,from,to,travel,damage,source,dung,dropped));}
  IEnumerator Flight(Sprite art,Vector2 from,Vector2 to,float duration,float damage,MonsterBlueprint source,bool dung,bool dropped){if(!dropped)OctoberEnemyProjectile.Register(gameObject,true);to=ColonAttackWall.Stop(from,to,.13f);var r=Sprite(art,from,dung?.42f:.20f,Color.white);var player=FindObjectOfType<Character>();bool hit=false;Vector2 prev=from;for(float t=0;t<duration;){if(!Paused){t+=Time.deltaTime;float q=Mathf.Clamp01(t/duration);Vector2 at=Vector2.Lerp(from,to,q);r.transform.position=at+Vector2.up*(dung?Mathf.Sin(q*Mathf.PI)*.9f:0);r.transform.Rotate(0,0,Time.deltaTime*200);if(!dung&&player!=null&&!hit&&ColonAttackWall.Clear(at,player.transform.position)&&SnailBossProjectile.SegmentDistance(player.transform.position,prev,at)<.24f){player.TakeDamageFromMonster(damage,Vector2.zero,source);hit=true;}prev=at;}yield return null;}
   if(dropped){yield return Wait(2.6f);for(float t=0;t<.4f;){if(!Paused){t+=Time.deltaTime;r.color=new Color(1,1,1,1-t/.4f);}yield return null;}}
   else if(dung){Puff(to,new Color(.52f,.29f,.12f),.85f);if(player!=null&&Vector2.Distance(to,player.transform.position)<.9f&&ColonAttackWall.Clear(to,player.transform.position)){player.TakeDamageFromMonster(damage,Vector2.zero,source);var slow=player.GetComponent<SnailSlowStatus>()??player.gameObject.AddComponent<SnailSlowStatus>();slow.Enter(GetInstanceID(),.3f);slow.Exit(GetInstanceID(),2.5f);var stain=Sprite(art,player.transform.position,.28f,new Color(.6f,.4f,.18f,.6f),150);stain.transform.SetParent(player.transform);stain.transform.localPosition=Vector3.up*.15f;Destroy(stain.gameObject,2.5f);}}
   Destroy(gameObject);
  }
  public static void Bubble(Vector2 p,MonsterBlueprint source,float damage){var e=Create();e.StartCoroutine(e.BubbleRoutine(p,source,damage));}
  IEnumerator BubbleRoutine(Vector2 p,MonsterBlueprint source,float damage){var r=Sprite(Art("yogurt",7),p,.22f,new Color(1,.9f,1,.75f),80);for(float t=0;t<2;){if(!Paused){t+=Time.deltaTime;float h=.22f+t*.12f;r.transform.localScale=Vector3.one*h/r.sprite.bounds.size.y;}yield return null;}Puff(p,new Color(1,.8f,.9f),.6f);var player=FindObjectOfType<Character>();if(player!=null&&Vector2.Distance(p,player.transform.position)<.6f&&ColonAttackWall.Clear(p,player.transform.position))player.TakeDamageFromMonster(damage,Vector2.zero,source);Destroy(gameObject);}
 }
}
