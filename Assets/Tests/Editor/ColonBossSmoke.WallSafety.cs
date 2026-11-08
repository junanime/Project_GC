#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor {
 public static partial class ColonBossSmoke {
  static IEnumerator ChameleonScoreChecks(LevelManager level,Character player) {
   var pools=level.CurrentLevelBlueprint.monsters;int index=Array.FindIndex(pools,p=>p.monstersPrefab.GetComponent<AcidToadMonster>()!=null);
   Check(index>=0,"stage-one chameleon pool exists");
   for(int k=0;k<4;k++) {
    int mini=RunScore.Current.Data.miniBoss,finalBoss=RunScore.Current.Data.boss,total=RunScore.Current.Data.Total;
    var bp=Object.Instantiate(pools[index].monsterBlueprints[0]);bp.name=ChameleonArt.Names[k];
    var m=(AcidToadMonster)level.EntityManager.SpawnMonster(index,(Vector2)player.transform.position+Vector2.right*5,bp,0,false);m.AutoPatterns=false;m.enabled=false;
    yield return null;m.TakeDamage(10000000);m.TakeDamage(10000000);yield return new WaitForSeconds(1.5f);
    Check(RunScore.Current.Data.miniBoss==mini+1&&RunScore.Current.Data.boss==finalBoss&&RunScore.Current.Data.Total==total+500,"chameleon scores exactly 500 once: "+ChameleonArt.Names[k]);
    Object.Destroy(bp);
   }
  }
  static IEnumerator SpawnFloorSafety(LevelManager level,Character player) {
   var map=FiniteStomachMap.Instance;var roster=ColonMonsterRoster.Active;
   var ridge=ColonMapGeometry.CentralRidge(map);var points=new[]{ridge[ridge.Length/3],ridge[ridge.Length*2/3],map.WorldRect.min,map.WorldRect.max};
   int spawned=0;bool sizeSafe=true;
   foreach(var pool in level.CurrentLevelBlueprint.monsters.Select((p,i)=>new{p,i}))
    foreach(var bp in pool.p.monsterBlueprints) {
     if(!roster.Allowed(bp)||bp.name=="필드 미니 롤케이크")continue;
     foreach(var point in points) {
      var m=level.EntityManager.SpawnMonster(pool.i,point,bp,0,false);if(m==null)continue;
      float radius=Mathf.Max(.8f,m.Size.magnitude*.5f);
      sizeSafe&=map.FloorClearance(m.transform.position,true)>=radius-.06f;
      spawned++;m.DespawnForStageTransfer();
     }
    }
   Check(spawned>=80&&sizeSafe,"real monster pool projects wall/outer-edge spawns to footprint-safe floor ("+spawned+" instances)");
   yield return null;
  }
  static IEnumerator WallSafety(LevelManager level,Character player) {
   Silence(level);ClearProjectiles();var map=FiniteStomachMap.Instance;
   var ridge=ColonMapGeometry.CentralRidge(map);Vector2 a=default,b=default;
   for(int i=3;i<ridge.Length-3;i++){
    Vector2 n=Vector2.Perpendicular((ridge[i+1]-ridge[i-1]).normalized);
    var x=ridge[i]+n*(map.WallWidth+2.1f);var y=ridge[i]-n*(map.WallWidth+2.1f);
    if(map.FloorClearance(x,true)<1||map.FloorClearance(y,true)<1)continue;
    a=x;b=y;break;
   }
   Check(a!=b&&!ColonAttackWall.Clear(a,b),"opposite floor points are occluded by central wall");
   Vector2 dir=(b-a).normalized;
   Check(ColonAttackWall.Clear(a,a-dir*.5f),"same-side open floor remains attackable");
   foreach(string shaderName in new[]{"Hazard","LiquidFlow","SoftFlow"}) {
    var shader=Resources.Load<Shader>("FoodBosses/"+shaderName);
    Check(shader!=null&&shader.isSupported&&!ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity.ToString()=="Error"),"wall-clipped shader compiles: "+shaderName);
   }
   Position(player,b);var boss=ColonBossRuntime.Spawn(ColonBossKind.Turtle,player,a,null);boss.AutoPatterns=false;
   float until=Time.time+20;while(boss.Arriving&&Time.time<until)yield return null;
   boss.enabled=false;boss.PlaceAt(a);Set(player,"isInvincible",false);float hp=player.CurrentHealth;
   yield return ColonBossEffects.Wave(boss,a,dir,90,Vector2.Distance(a,b)+1,1,10);
   Check(Mathf.Approximately(player.CurrentHealth,hp),"ground wave cannot damage through wall");
   var shot=ColonBossEffects.Projectile(boss,a,dir,1000,10,.2f,"swept-wall-QA");
   yield return new WaitForSeconds(.2f);
   Check(shot==null&&Mathf.Approximately(player.CurrentHealth,hp),"fast projectile stops at wall without tunnelling damage");
   var weapon=SyringeAbilityResolver.FindOwnedOrFirst(Object.FindObjectOfType<AbilityManager>());
   int projectileIndex=(int)Field(weapon,"projectileIndex").GetValue(weapon);
   var needle=level.EntityManager.SpawnProjectile(projectileIndex,a,10,0,1000,0);
   Set(needle,"maxDistance",30f);needle.Launch(dir);yield return new WaitForSeconds(.3f);
   Check(!needle.gameObject.activeInHierarchy,"real player needle is returned to pool on wall impact");
   var warning=ColonBossEffects.Warning(boss,ColonBossEffects.Circle(a,Vector2.Distance(a,b)+2),a);
   CheckMaskPixels(warning,a-dir*.6f,b);
   Capture("wall-telegraph-actual-play");Object.Destroy(warning.gameObject);
   yield return ColonBossEffects.Liquid(boss,a,b,.7f,1.8f,10);
   Check(Mathf.Approximately(player.CurrentHealth,hp),"growing liquid stops on near side of wall");
   Position(player,a-dir*.7f);Set(player,"isInvincible",false);hp=player.CurrentHealth;
   yield return ColonBossEffects.Wave(boss,a,-dir,120,2,1,10);
   Check(player.CurrentHealth<hp,"same-side wave still deals real damage");
   Set(player,"isInvincible",true);player.GainHealth(player.MaxHealth);Object.Destroy(boss.gameObject);yield return null;
  }
  static void CheckMaskPixels(LineRenderer warning,Vector2 visible,Vector2 hidden) {
   foreach(var t in warning.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
   var c=new GameObject("Wall shader readback").AddComponent<Camera>();c.enabled=false;c.orthographic=true;c.cullingMask=1<<31;
   c.transform.position=(Vector3)((visible+hidden)*.5f)+Vector3.back*10;c.orthographicSize=Vector2.Distance(visible,hidden)*.6f+2;c.aspect=1;
   c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.clear;
   var rt=RenderTexture.GetTemporary(512,512,24);var old=RenderTexture.active;c.targetTexture=rt;c.Render();RenderTexture.active=rt;
   var tex=new Texture2D(512,512,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,512,512),0,0);tex.Apply();
   Color Sample(Vector2 p){var v=c.WorldToViewportPoint(p);return tex.GetPixel((int)(v.x*512),(int)(v.y*512));}
   float near=Sample(visible).a,far=Sample(hidden).a;File.WriteAllBytes(Output+"/wall-mask-readback.png",tex.EncodeToPNG());
   c.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.Destroy(c.gameObject);Object.Destroy(tex);
   foreach(var t in warning.GetComponentsInChildren<Transform>())t.gameObject.layer=0;
   Check(near>.02f&&far<.01f,"actual rendered warning fills safe floor and clips wall shadow ("+near+", "+far+")");
  }
 }
}
#endif
