using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Editor
{
    public static partial class ChameleonSmoke
    {
        static object Call(object o,string name,params object[] args)
        {for(Type t=o.GetType();t!=null;t=t.BaseType){var m=t.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(m!=null)return m.Invoke(o,args);}throw new Exception(name);}
        static IEnumerator Integration(LevelManager level,StageEventDirector director,AcidToadSpawn schedule,int bossIndex)
        {
            var player=level.PlayerCharacter;var containers=level.CurrentLevelBlueprint.monsters;
            int normalIndex=Array.FindIndex(containers,c=>c.monstersPrefab!=null&&c.monstersPrefab.GetComponent<MeleeMonster>()!=null&&c.monstersPrefab.GetComponent<MiniBossMonster>()==null&&c.monstersPrefab.GetComponent<BossMonster>()==null&&c.monsterBlueprints.Length>0);
            var dummy=level.EntityManager.SpawnMonster(normalIndex,(Vector2)player.transform.position+Vector2.right*6,containers[normalIndex].monsterBlueprints[0],100000,false);
            var latte=(AcidToadMonster)level.EntityManager.SpawnMonster(bossIndex,(Vector2)player.transform.position+Vector2.left*6,containers[bossIndex].monsterBlueprints[3],100000,false);latte.AutoPatterns=false;
            latte.UsePattern(AcidToadMonster.Pattern.Jet);yield return new WaitForSeconds(2.25f);
            Check(dummy.GetComponent<CoffeeMonsterBuffRuntime>()==null,"world coffee waits for falling fountain before buff");
            yield return new WaitForSeconds(.6f);
            Check(dummy.GetComponent<CoffeeMonsterBuffRuntime>()!=null&&latte.GetComponent<CoffeeMonsterBuffRuntime>()==null,"world coffee buffs other monsters only");CheckNoPortrait();
            while(latte.Busy)yield return null;
            var foam=ChameleonFoamSkill.Create(latte.transform,player);yield return null;
            var zone=foam.GetComponentInChildren<AntacidBubbleZone>();Vector3 at=zone.transform.position;latte.transform.position+=Vector3.right*5;yield return null;yield return null;
            Check(((Vector2)(zone.transform.position-at)).sqrMagnitude<.0001f,"foam floor stays at its spawn position when boss moves");
            Check(Mathf.Abs(foam.transform.lossyScale.x-1)<.001f,"foam preserves existing field art world scale");Object.Destroy(foam.gameObject);
            foreach(var buff in Object.FindObjectsOfType<CoffeeMonsterBuffRuntime>())buff.RemoveBuffAndDestroy();yield return null;
            var module=new AdvancedStageFieldEventDirector();Call(module,"Bind",director);Set(module,"levelManager",level);Set(module,"mainCamera",Camera.main);Set(module,"runtimeVisualRoot",director.transform);
            schedule.ResetForStage();var coffee=new AdvancedStageFieldEventDirector.CoffeeTransfusionEvent{resolvedStartTime=0,duration=4};
            Call(module,"UpdateCoffeeTransfusionEvent",coffee,level.CurrentLevelTime);yield return new WaitForSeconds(1.2f);
            Check(coffee.eventName=="커피수혈"&&schedule.Lottery.Weight(ChameleonKind.Latte)==45,"coffee event name and probability hooked to real start");
            Check(!coffee.presentationComplete&&dummy.GetComponent<CoffeeMonsterBuffRuntime>()==null,"field coffee has no buff during screen cover");Capture("coffee-before-buff");
            yield return new WaitForSeconds(1.9f);
            Check(coffee.presentationComplete&&dummy.GetComponent<CoffeeMonsterBuffRuntime>()!=null,"field coffee applies buff after screen clears");
            Check(Object.FindObjectsOfType<ChameleonCoffeeWash>().Length==0,"coffee cover gone before buff phase");Capture("coffee-after-buff");
            Call(module,"UpdateCoffeeTransfusionEvent",coffee,coffee.buffStartedAt+coffee.duration+.01f);yield return null;
            Check(dummy.GetComponent<CoffeeMonsterBuffRuntime>()==null,"field coffee expiry removes buff");
            var overlap=dummy.gameObject.AddComponent<CoffeeMonsterBuffRuntime>();overlap.ApplySkill(player,8,2.5f,2);overlap.ApplyOrRefresh(player,1,2.5f,2,Color.white);overlap.RemoveEventBuff();yield return null;
            Check(dummy.GetComponent<CoffeeMonsterBuffRuntime>()!=null,"ending coffee field does not erase ongoing miniboss coffee buff");overlap.RemoveBuffAndDestroy();yield return null;
            schedule.ResetForStage();var drift=new AdvancedStageFieldEventDirector.PeristalsisDriftEvent{resolvedStartTime=0,duration=6,directionSwitchInterval=2,enableCameraTilt=false};
            Call(module,"UpdatePeristalsisDriftEvent",drift,level.CurrentLevelTime);
            Check(drift.elapsed<0&&Object.FindObjectsOfType<ChameleonPortrait>().Length==1,"drift opens with clap before force begins");
            Check(schedule.Lottery.Weight(ChameleonKind.Drift)==45,"drift event changes its own spawn weight");yield return new WaitForSeconds(.9f);
            drift.elapsed=1.2f;Call(module,"UpdatePeristalsisDriftEvent",drift,level.CurrentLevelTime);
            Check(drift.clapPhase==1&&Object.FindObjectsOfType<ChameleonPortrait>().Length==1,"drift claps before next direction change");
            Vector2 first=(Vector2)Call(module,"GetCurrentDriftDirection",drift);drift.elapsed=2.01f;Vector2 second=(Vector2)Call(module,"GetCurrentDriftDirection",drift);
            Check(first.x==-second.x,"clap precedes alternating drift direction");yield return new WaitForSeconds(.9f);
            schedule.ResetForStage();var reflux=new AdvancedStageFieldEventDirector.AcidRefluxWaveEvent{resolvedStartTime=0};Call(module,"UpdateAcidRefluxWaveEvent",reflux,level.CurrentLevelTime);
            Check(schedule.Lottery.Weight(ChameleonKind.Fanta)==45,"Fanta field event changes spawn weight");Call(module,"Dispose");yield return null;
            var antacid=new AntacidBubbleSurgeEventController();Call(antacid,"Bind",director);Set(antacid,"levelManager",level);Set(antacid,"bubblePrefab",ChameleonSettings.Current.foamPrefab);
            schedule.ResetForStage();Call(antacid,"StartEvent",level.CurrentLevelTime);
            Check(schedule.Lottery.Weight(ChameleonKind.Foam)==45,"foam field event changes spawn weight");Call(antacid,"Dispose");
            // Exercise actual alpha contact and pause on one actor; all four share the same routine.
            latte.UsePattern(AcidToadMonster.Pattern.Leap);yield return new WaitForSeconds(.5f);Time.timeScale=0;Vector3 frozen=latte.transform.position;
            yield return new WaitForSecondsRealtime(.3f);Check(latte.Invisible&&((Vector2)(latte.transform.position-frozen)).sqrMagnitude<.0001f,"teleport pause holds invisible phase");Time.timeScale=1;
            yield return new WaitForSeconds(2.95f);
            Vector3 center=latte.Motion.Art.bounds.center;Vector3 centerOffset=player.CenterTransform.position-player.transform.position;player.transform.position=center-centerOffset;player.GetComponent<Rigidbody2D>().position=player.transform.position;
            Check(latte.OccupiesSilhouette(player.CenterTransform.position),"silhouette alpha accepts interior point");Check(!latte.OccupiesSilhouette(center+Vector3.right*10),"silhouette rejects outside point");
            float hp=player.CurrentHealth;while(latte.Busy)yield return null;
            Check(player.CurrentHealth<hp,"materializing on player inflicts damage");
            latte.StartCoroutine(latte.Killed(false));dummy.StartCoroutine(dummy.Killed(false));yield return new WaitForSeconds(2);
            foreach(var p in Object.FindObjectsOfType<ChameleonPortrait>())Object.Destroy(p.gameObject);
            foreach(var z in Object.FindObjectsOfType<AntacidBubbleZone>())Object.Destroy(z.gameObject);
            schedule.ResetForStage();yield return null;
        }
    }
}
