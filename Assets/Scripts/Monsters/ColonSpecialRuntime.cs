using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Vampire {
 [DefaultExecutionOrder(700)]
 public sealed class ColonSpecialRuntime:MonoBehaviour {
  public string Kind{get;private set;} public bool ControlsMovement=>enabled&&Kind!=null;
  public int Pose{get;private set;} public int CrackStage{get;private set;}
  Monster monster;Character player;LevelManager level;EntityManager entities;Rigidbody2D body;float clock,timer,cooldown,lastHp,frame;Vector2 aim;bool busy;SpriteRenderer carried;LineRenderer throwWarning;readonly List<Monster> links=new List<Monster>();readonly List<LineRenderer> lines=new List<LineRenderer>();
  public void Configure(Monster m,string kind){ResetForPool();monster=m;Kind=kind;enabled=true;player=FindObjectOfType<Character>();level=FindObjectOfType<LevelManager>();entities=FindObjectOfType<EntityManager>();body=m.GetComponent<Rigidbody2D>();lastHp=m.HP;cooldown=2;}
  public void ResetForPool(){StopAllCoroutines();foreach(var m in links)if(m!=null){var l=m.GetComponent<ColonRevivalLink>();if(l!=null&&l.Owner==this)l.Owner=null;}foreach(var l in lines)if(l!=null)Destroy(l.gameObject);links.Clear();lines.Clear();if(carried!=null)Destroy(carried.gameObject);if(throwWarning!=null)Destroy(throwWarning.gameObject);Kind=null;busy=false;clock=timer=frame=0;CrackStage=Pose=0;enabled=false;}
  public bool Active=>enabled&&monster!=null&&monster.HP>0&&monster.gameObject.activeInHierarchy;
  public bool Paused=>MiniStageRuntimeState.IsInsideMiniStage||(level!=null&&level.IsRunFlowPaused)||monster.IsFieldRuntimeSuspended||!monster.enabled;
  void Update(){if(!Active||Paused||Time.timeScale<=0)return;clock+=Time.deltaTime;cooldown-=Time.deltaTime;frame+=Time.deltaTime;
   if(Kind=="salt"){CrackStage=monster.HP<=monster.SpawnMaxHealth*.3f?2:monster.HP<=monster.SpawnMaxHealth*.6f?1:0;Pose=CrackStage==0?(int)(frame/.16f)%4:CrackStage+4;}
   else if(!busy)Pose=Kind=="beetle"&&cooldown>2?8+(int)(frame/.12f)%2:(int)(frame/.16f)%4;
   if(Kind=="beetle"&&busy&&monster.HP<lastHp){StopAllCoroutines();DropAndFlee();}lastHp=monster.HP;
   if(Kind=="stitch"&&cooldown<=0){LinkTargets();cooldown=1;}
   if(Kind=="gas"&&!busy&&cooldown<=0){StartCoroutine(Gas());cooldown=6;}
   if(Kind=="beetle"&&!busy&&cooldown<=0&&player!=null&&Vector2.Distance(player.transform.position,transform.position)<=2.1f){StartCoroutine(Throw());}
   if(Kind=="yogurt"&&player!=null&&Vector2.Distance(player.transform.position,transform.position)<3.5f){timer+=Time.deltaTime;if(timer>=.85f){timer=0;ColonSpecialEffects.Bubble(transform.position,monster.Blueprint,Damage(14));}}
  }
  float Damage(float n)=>n*.65f;
  void FixedUpdate(){if(!Active||player==null)return;if(Paused){body.velocity=Vector2.zero;return;}if(busy){body.velocity=Vector2.zero;return;}Vector2 goal=player.transform.position;float speed=monster.moveSpeed;float distance=Vector2.Distance(goal,transform.position);
   if(Kind=="beetle"&&cooldown>2){goal=(Vector2)transform.position+((Vector2)transform.position-goal).normalized*3;speed*=1.35f;}
   if(Kind=="yogurt"&&distance<3.7f){Vector2 radial=((Vector2)transform.position-goal).normalized;goal+=(radial*3+Vector2.Perpendicular(radial)*1.2f);}
   if(Kind=="stitch"&&distance<4)goal=(Vector2)transform.position+((Vector2)transform.position-goal).normalized;
   Vector2 delta=ColonNavigation.Toward(transform.position,goal)*speed*monster.IceMoveMultiplier*Time.fixedDeltaTime;
   Vector2 next=ColonAttackWall.Stop(transform.position,(Vector2)transform.position+delta,.25f);body.velocity=(next-(Vector2)transform.position)/Time.fixedDeltaTime;entities.Grid?.UpdateClient(monster);
  }
  IEnumerator Wait(float d){for(float t=0;t<d;){if(!Active)yield break;if(!Paused)t+=Time.deltaTime;yield return null;}}
  IEnumerator Gas(){busy=true;Pose=4;yield return Wait(.7f);Pose=5;yield return Wait(.3f);Pose=6;aim=player!=null?((Vector2)player.transform.position-(Vector2)transform.position).normalized:Vector2.right;
   for(int k=0;k<6&&Active;k++){ColonSpecialEffects.Puff((Vector2)transform.position+aim*.35f,new Color(.7f,.83f,.31f),.6f);foreach(var m in entities.LivingMonsters){if(m==null||m==monster||m.HP<=0||m is BloodClotMonster||m is BossMonster||m is MiniBossMonster)continue;Vector2 d=(Vector2)m.transform.position-(Vector2)transform.position;if(d.magnitude>2.7f||Vector2.Angle(aim,d)>55||!ColonAttackWall.Clear(transform.position,m.transform.position))continue;var rb=m.GetComponent<Rigidbody2D>();Vector2 next=ColonAttackWall.Stop(m.transform.position,(Vector2)m.transform.position+aim*.22f,Mathf.Max(.2f,m.Size.magnitude*.45f));if(rb!=null)rb.position=next;m.transform.position=next;}yield return Wait(.12f);}Pose=7;yield return Wait(.4f);busy=false;}
  IEnumerator Throw(){busy=true;timer=0;Vector2 target=player.transform.position;throwWarning=FoodBossEffects.Line(transform,FoodBossEffects.Arc(Vector2.zero,.9f),.04f,new Color(.6f,.36f,.18f,.65f));throwWarning.useWorldSpace=false;throwWarning.transform.position=target;for(float t=0;t<3&&Active;){if(!Paused){t+=Time.deltaTime;Pose=t<1.5f?4:t<2.8f?5:6;if(t<2.75f)target=player.transform.position;throwWarning.transform.position=target;}yield return null;}if(!Active)yield break;busy=false;cooldown=6;if(throwWarning!=null){throwWarning.transform.SetParent(null);throwWarning.gameObject.AddComponent<FoodBossFade>().Lifetime=.75f;throwWarning=null;}ColonSpecialEffects.Shot(ColonSpecialEffects.Art("beetle",11),(Vector2)transform.position+Vector2.up*.4f,target,.75f,Damage(24),monster.Blueprint,true);}
  void DropAndFlee(){if(throwWarning!=null)Destroy(throwWarning.gameObject);busy=false;cooldown=6;Pose=7;ColonSpecialEffects.Shot(ColonSpecialEffects.Art("beetle",11),(Vector2)transform.position+Vector2.up*.4f,(Vector2)transform.position+Vector2.right*.25f,.35f,0,monster.Blueprint,true,true);}
  void LinkTargets(){for(int i=links.Count-1;i>=0;i--)if(links[i]==null||!links[i].gameObject.activeInHierarchy||links[i].GetComponent<ColonRevivalLink>()?.Owner!=this){if(lines[i]!=null)Destroy(lines[i].gameObject);links.RemoveAt(i);lines.RemoveAt(i);}
   if(links.Count>=3)return;foreach(var m in entities.LivingMonsters){if(links.Count>=3)break;if(m==null||m.HP<=0||m==monster||!(m is MeleeMonster)||m.Blueprint is EliteMonsterBlueprint||m.GetComponent<MonsterSupportCaster>()!=null)continue;var r=ColonMonsterRoster.Active;if(r==null||!r.TryGet(m.Blueprint,out var e)||!e.id.StartsWith("N")||m.GetComponent<MonsterCombatBuffRuntime>()?.HealthBuffActive==true)continue;if(Vector2.Distance(transform.position,m.transform.position)>3||!ColonAttackWall.Clear(transform.position,m.transform.position))continue;var l=m.GetComponent<ColonRevivalLink>()??m.gameObject.AddComponent<ColonRevivalLink>();if(l.Used||l.Owner!=null)continue;l.Owner=this;links.Add(m);var line=FoodBossEffects.Line(transform,new[]{(Vector2)transform.position,(Vector2)m.transform.position},.035f,new Color(1,.9f,.64f),false,115,true);lines.Add(line);}}
  void LateUpdate(){if(!Active||Paused)return;var r=monster.BodyRenderer;if(r==null)return;var art=ColonSpecialEffects.Art(Kind,Pose);if(art!=null)r.sprite=art;
   if(Kind=="beetle"){if(carried==null){carried=new GameObject("Held dung").AddComponent<SpriteRenderer>();carried.transform.SetParent(transform);carried.sprite=ColonSpecialEffects.Art("beetle",11);carried.transform.localScale=Vector3.one*.4f/carried.sprite.bounds.size.y;}carried.enabled=cooldown<=2||busy;carried.sortingOrder=r.sortingOrder+1;carried.transform.position=r.bounds.center+Vector3.up*r.bounds.extents.y*.85f;}
   for(int i=0;i<links.Count;i++)if(links[i]!=null&&lines[i]!=null){var a=monster.CenterTransform.position;var b=links[i].CenterTransform.position;lines[i].SetPosition(0,a);lines[i].SetPosition(1,b);lines[i].enabled=true;}
  }
  public void OnDeath(){if(Kind=="salt"){for(int i=0;i<4;i++){Vector2 dir=new Vector2(Mathf.Cos(i*Mathf.PI/2),Mathf.Sin(i*Mathf.PI/2));ColonSpecialEffects.Shot(ColonSaltParts.Get(i),monster.CenterTransform.position,(Vector2)monster.CenterTransform.position+dir*4,1.15f,Damage(18),monster.Blueprint);}}if(Kind=="beetle"&&busy)DropAndFlee();ResetForPool();}
  void OnDisable()=>ResetForPool();
 }
 public sealed class ColonRevivalLink:MonoBehaviour {
  public ColonSpecialRuntime Owner;public bool Used;public bool CanRevive=>!Used&&Owner!=null&&Owner.Active;
  public void ResetForSpawn(){Owner=null;Used=false;}
  public IEnumerator Revive(Monster m){Used=true;var source=Owner;var rb=m.GetComponent<Rigidbody2D>();if(rb!=null)rb.velocity=Vector2.zero;float t=0;while(t<1.2f&&source!=null&&source.Active){if(!m.IsFieldRuntimeSuspended&&!MiniStageRuntimeState.IsInsideMiniStage&&!source.Paused)t+=Time.deltaTime;yield return null;}if(source!=null&&source.Active){m.RestoreLinkedHealth(.3f);ColonSpecialEffects.Puff(m.transform.position,new Color(.8f,1,.6f));}Owner=null;}
 }
}
