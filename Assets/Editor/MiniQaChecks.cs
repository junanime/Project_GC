using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Vampire.Editor
{
    public static class MiniQaChecks
    {
        static FieldInfo Field(object target,string name)
        {for(var t=target.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null)return f;}throw new Exception(name);}
        static object Call(object target,string name,params object[] args)
        {for(var t=target.GetType();t!=null;t=t.BaseType){var m=t.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);if(m!=null)return m.Invoke(target,args);}throw new Exception(name);}
        static void Check(bool ok,string message){if(!ok)throw new Exception("MINI_QA_FAIL "+message);Debug.Log("MINI_QA_PASS "+message);}
        static void Capture(string name)
        {var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("Library/ReadOnlyHelpProof/"+name+".png",image.EncodeToPNG());Object.Destroy(image);}
        public static IEnumerator Run(LevelManager level,Action done)
        {
            foreach(var entry in TutorialGuide.Instance.Entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(entry.id),1);
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            foreach(var slot in level.PlayerInventory.Slots)
            {
                slot.Init();
                for(int i=0;i<slot.Capacity;i++)slot.AddItem(new GameObject("Capacity sample").AddComponent<HelpSampleItem>());
                Check(slot.CountLabel==$"{slot.Capacity}/{slot.Capacity}"&&slot.CapacityDescription.Contains("최대 보유량"),"full capacity label "+slot.CollectableType.name);
                slot.UseItem();Check(!slot.CapacityDescription.Contains("최대 보유량"),"using item removes full label "+slot.CollectableType.name);
                slot.AddItem(new GameObject("Capacity refill").AddComponent<HelpSampleItem>());
            }
            yield return null;Capture("08-capacity");
            var bp=RemakeBalance.Current.levelUpChest;
            foreach(var source in new[]{bp.closedChest,bp.openingChest,bp.openChest})
                Check(source!=null&&source.texture.name=="ItemChestIcon"&&Mathf.Abs(bp.Visual(source).bounds.size.x-.6f)<.01f,"reward chest uses real art at correct size");
            var director=Object.FindObjectOfType<MiniStageDirector>();
            var room=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MiniStages/Room_BonusChest.prefab")).GetComponent<MiniStageBonusChestRoom>();
            room.transform.position=level.PlayerCharacter.transform.position;room.InitRoom(director,level.EntityManager,level.PlayerCharacter);
            var signatures=new HashSet<string>();
            foreach(bool enhanced in new[]{false,true})
            {
                Field(director,"<CurrentEnhanced>k__BackingField").SetValue(director,enhanced);
                for(int i=0;i<32;i++)
                {
                    Call(room,"SetupMysteryChests");
                    var outcomes=room.GetComponentsInChildren<MiniStageMysteryChestInteractable>().Select(c=>c.Outcome).ToArray();
                    if(outcomes.Length!=3||outcomes.Distinct().Count()!=3)throw new Exception("MINI_QA_FAIL roles");
                    signatures.Add(string.Join(",",outcomes));
                }
                Check(true,"one blank, win and mimic for "+(enhanced?"enhanced":"normal")+" room (32 shuffles)");
            }
            Check(signatures.Count>1,"chest positions randomized");
            room.BeginRoom();Call(room,"SpawnRewardChest");Time.timeScale=1;
            yield return new WaitForSecondsRealtime(.7f);
            var reward=(Chest)Field(room,"activeRewardChest").GetValue(room);
            Check(reward!=null&&reward.GetComponentInChildren<SpriteRenderer>().sprite!=null&&reward.CanInteract,"enhanced reward is visible and interactable");Capture("09-reward-chest");
            var mimicChest=room.GetComponentsInChildren<MiniStageMysteryChestInteractable>().First(c=>c.Outcome==MiniStageMysteryChestOutcome.Monster);
            room.NotifyMysteryChestSelected(mimicChest);
            var mimic=(Monster)Field(room,"spawnedMonsterFromChest").GetValue(room);
            Check(mimic!=null&&mimic.GetComponent<MimicChestVisual>()!=null,"mimic choice spawns chest monster");
            yield return null;Capture("10-mimic");
            Object.Destroy(room.gameObject);level.EntityManager.DespawnChest(reward);yield return null;
            Field(director,"<CurrentEnhanced>k__BackingField").SetValue(director,false);
            var pools=level.CurrentLevelBlueprint.monsters;int index=Array.FindIndex(pools,p=>p.monstersPrefab.GetComponent<TrapMonster>()!=null);
            Check(index>=0,"trap pool exists");
            typeof(MobileGameplayInput).GetProperty("Active").GetSetMethod(true).Invoke(null,new object[]{true});
            var trap=(TrapMonster)level.EntityManager.SpawnMonster(index,level.PlayerCharacter.transform.position+Vector3.right*2,pools[index].monsterBlueprints[0],0,true);
            Call(trap,"ActivateTrap",level.PlayerCharacter);yield return null;
            Check(trap.AcceptsMobileEscape&&trap.EscapeLength==4&&Object.FindObjectOfType<MobileTrapEscapeUI>()!=null,"mobile trap shows four-touch escape");Capture("11-mobile-trap");
            int revision=trap.EscapeRevision,target=trap.EscapeTarget;
            var targetButton=Object.FindObjectsOfType<Button>().First(b=>b.name=="Knot "+target);
            var touch=new PointerEventData(EventSystem.current){pointerId=19,button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(targetButton.gameObject,touch,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(targetButton.gameObject,touch,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(targetButton.gameObject,touch,ExecuteEvents.pointerClickHandler);
            Check(trap.EscapeProgress==1,"touch button events advance once");
            trap.SubmitMobileEscape(trap.EscapeTarget,revision);Check(trap.EscapeProgress==1,"stale multitouch cannot advance next step");
            Time.timeScale=0;trap.SubmitMobileEscape(trap.EscapeTarget,trap.EscapeRevision);Check(trap.EscapeProgress==1,"pause blocks escape input");Time.timeScale=1;
            trap.SubmitMobileEscape((trap.EscapeTarget+1)%4,trap.EscapeRevision);Check(trap.EscapeProgress==0,"wrong touch retains existing reset rule");
            for(int i=0;i<4;i++)trap.SubmitMobileEscape(trap.EscapeTarget,trap.EscapeRevision);
            yield return null;Check(!level.PlayerCharacter.IsTrapBound&&Object.FindObjectOfType<MobileTrapEscapeUI>()==null,"four correct touches release player and remove panel");
            typeof(MobileGameplayInput).GetProperty("Active").GetSetMethod(true).Invoke(null,new object[]{false});Time.timeScale=0;
            Debug.Log("MINI_QA_FINISHED PASS");done();
        }
    }
}
