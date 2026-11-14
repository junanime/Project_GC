using UnityEngine;
namespace Vampire {
 [DefaultExecutionOrder(720)]
 public sealed class SupportCastMotion:MonoBehaviour {
  MonsterSupportCaster caster;SpriteRenderer body;Vector3 scale,position;Quaternion rotation;bool captured;float powder;
  SpriteRenderer[] folds;
  public void Configure(MonsterSupportCaster c){Restore();caster=c;body=c.BodyRenderer;}
  void Restore(){if(captured&&body!=null){body.transform.localScale=scale;body.transform.localPosition=position;body.transform.localRotation=rotation;}captured=false;if(folds!=null)foreach(var f in folds)if(f!=null){Destroy(f.sprite);Destroy(f.gameObject);}folds=null;}
  void LateUpdate(){if(caster==null||body==null)return;if(!caster.IsCasting||caster.HP<=0){Restore();return;}if(caster.IsFieldRuntimeSuspended||!caster.enabled||Time.timeScale<=0)return;
   bool mushroom=ColonMonsterRoster.InColon&&!caster.IsMiniStageOwned;
   if(!captured){scale=body.transform.localScale;position=body.transform.localPosition;rotation=body.transform.localRotation;captured=true;
    if(mushroom){folds=new SpriteRenderer[2];for(int i=0;i<2;i++){var f=new GameObject("Spreading mushroom fold").AddComponent<SpriteRenderer>();f.transform.SetParent(body.transform,false);var rect=body.sprite.rect;rect.y+=rect.height*.44f;rect.height*=.56f;f.sprite=Sprite.Create(body.sprite.texture,rect,new Vector2(.5f,0),body.sprite.pixelsPerUnit);f.sortingOrder=body.sortingOrder-1;f.color=new Color(.92f,.92f,.95f);folds[i]=f;}}
   }
   float p=caster.CastPhase;float shake=Mathf.Sin(p*Mathf.PI*8)*Mathf.Sin(p*Mathf.PI);float spread=Mathf.Sin(p*Mathf.PI);
   body.transform.localRotation=rotation*Quaternion.Euler(0,0,shake*(mushroom?9:24));body.transform.localScale=Vector3.Scale(scale,new Vector3(1+spread*(mushroom?.16f:.03f),1-spread*.05f,1));body.transform.localPosition=position+Vector3.up*spread*.025f;
   if(folds!=null)for(int i=0;i<2;i++){float sign=i==0?-1:1;folds[i].transform.localScale=Vector3.one*.62f;folds[i].transform.localPosition=new Vector3(sign*spread*body.sprite.bounds.size.x*.24f,body.sprite.bounds.size.y*.20f,0);folds[i].transform.localRotation=Quaternion.Euler(0,0,sign*(spread*24+shake*8));}
   powder-=Time.deltaTime;if(powder<=0){powder=.10f;SupportPowder.Spawn(body.bounds.center+Vector3.up*body.bounds.extents.y*.65f,caster.SpeedBuff?new Color(.65f,.85f,1):new Color(1,.72f,.68f),body.sortingOrder+1);}
  }
  void OnDisable()=>Restore();
 }
 public sealed class SupportPowder:MonoBehaviour {
  SpriteRenderer r;Vector2 velocity;float age;
  public static void Spawn(Vector2 p,Color c,int order){for(int i=0;i<4;i++){var g=new GameObject("Support powder");g.transform.position=p;var e=g.AddComponent<SupportPowder>();e.r=g.AddComponent<SpriteRenderer>();e.r.sprite=BombCircleArt.Sprite;e.r.color=c;e.r.sortingOrder=order;g.transform.localScale=Vector3.one*Random.Range(.025f,.045f)/e.r.sprite.bounds.size.y;e.velocity=new Vector2(Random.Range(-.8f,.8f),Random.Range(.2f,.65f));}}
  void Update(){if(MiniStageRuntimeState.IsInsideMiniStage)return;age+=Time.deltaTime;velocity+=Vector2.down*Time.deltaTime;transform.position+=(Vector3)velocity*Time.deltaTime;var c=r.color;c.a=Mathf.Clamp01(1-age/.7f);r.color=c;if(age>=.7f)Destroy(gameObject);}
 }
}
