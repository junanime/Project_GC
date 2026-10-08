using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class StageEventPresentationTests
    {
        [Test] public void EveryOriginalHasShortRepeatTextAndUnchangedProgress()
        {
            for (int i = 0; i < 63; i++)
            for (int count = 0; count < 3; count++)
            {
                string text = OriginalAugmentDescription.Effect(i,count);
                Assert.IsFalse(string.IsNullOrWhiteSpace(text));
                if (count > 0) Assert.IsFalse(text.Contains("\n"), Ver4AugmentCatalog.OriginalNames[i]);
                int parentLevel = 6 + count;
                string card = OriginalAugmentDescription.Card(i,count,parentLevel);
                StringAssert.Contains($"이 항목 {count} → {count+1}/3",card);
                StringAssert.Contains($"오리지널 Lv.{parentLevel} → {parentLevel+1}/9",card);
            }
        }
        [Test] public void SpecialMechanicsOnlyExplainTheCurrentSelection()
        {
            StringAssert.Contains("음·양",OriginalAugmentDescription.Effect(38,0));
            Assert.AreEqual("낙인 추가 피해 40% → 60%",OriginalAugmentDescription.Effect(38,1));
            Assert.AreEqual("낙인 추가 피해 60% → 80%",OriginalAugmentDescription.Effect(38,2));
            Assert.AreEqual("앞뒤 침 피해 +25%",OriginalAugmentDescription.Effect(36,1));
            Assert.AreEqual("좌우 침 피해 +25%",OriginalAugmentDescription.Effect(36,2));
            StringAssert.DoesNotContain("대쉬",OriginalAugmentDescription.Effect(21,2));
            StringAssert.Contains("31% → 40%",OriginalAugmentDescription.Effect(23,2));
            StringAssert.Contains("5 → 6",OriginalAugmentDescription.Effect(53,2));
            Assert.Throws<ArgumentOutOfRangeException>(() => OriginalAugmentDescription.Effect(1,3));
        }
        [Test] public void ArtUsesRealGameplayBubbleFramesAndAllSevenStylesExist()
        {
            var art = Resources.Load<StageEventCornerArt>("StageEventCornerArt"); Assert.NotNull(art);
            foreach (StageEventVisualKind kind in Enum.GetValues(typeof(StageEventVisualKind)))
            { Assert.IsNotEmpty(art.Frames(kind)); Assert.IsTrue(art.Frames(kind).All(s => s != null),kind.ToString()); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Junhan/Prefabs/StageEvents/AntacidBubbleZone.prefab");
            var so = new SerializedObject(prefab.GetComponent<AntacidBubbleZone>());
            var frames = so.FindProperty("bubblePopFrames");
            Assert.AreEqual(frames.arraySize,art.antacid.Length);
            for (int i = 0; i < frames.arraySize; i++) Assert.AreSame(frames.GetArrayElementAtIndex(i).objectReferenceValue,art.antacid[i]);
            Assert.AreSame(prefab.GetComponentInChildren<SpriteRenderer>().sprite,art.antacidFoam);
            Assert.NotNull(Resources.Load<Font>("TrainingUI/Cafe24Ssurround"));
        }
        [Test] public void CollectorsFollowActualCompletionAndDriftDirection()
        {
            var go = new GameObject("Presentation test director"); go.SetActive(false);
            var host = go.AddComponent<StageEventDirector>();
            var basic = new StageEventSchedule();
            var advanced = new AdvancedStageFieldEventDirector();
            var bubble = new AntacidBubbleSurgeEventController();
            foreach (var module in new RuntimeModule[] {basic,advanced,bubble})
            { Set(module,"Host",host); Set(module,"running",true); }
            // Host must be active for collectors; no schedule configuration is changed on disk.
            Set(host,"useWindowSchedule",false); go.SetActive(true);
            var infection = new StageEventSchedule.MonsterSurgeEvent {started=true};
            Set(basic,"monsterSurgeEvents",new List<StageEventSchedule.MonsterSurgeEvent>{infection});
            var reflux = new AdvancedStageFieldEventDirector.AcidRefluxWaveEvent {started=true};
            var drift = new AdvancedStageFieldEventDirector.PeristalsisDriftEvent {started=true,elapsed=0,directionSwitchInterval=4};
            Set(advanced,"acidRefluxWaveEvents",new List<AdvancedStageFieldEventDirector.AcidRefluxWaveEvent>{reflux});
            Set(advanced,"peristalsisDriftEvents",new List<AdvancedStageFieldEventDirector.PeristalsisDriftEvent>{drift});
            Set(bubble,"started",true);
            try
            {
                var items = new List<StageEventPresentation>();
                basic.CollectPresentations(items); advanced.CollectPresentations(items); bubble.CollectPresentations(items);
                Assert.AreEqual(4,items.Count); Assert.AreEqual(-1,items.Single(x=>x.kind==StageEventVisualKind.Drift).direction);
                infection.finished=true; reflux.finished=true; drift.elapsed=5; Set(bubble,"finished",true);
                items.Clear(); basic.CollectPresentations(items); advanced.CollectPresentations(items); bubble.CollectPresentations(items);
                Assert.AreEqual(1,items.Count); Assert.AreEqual(1,items[0].direction);
                advanced.enabled=false; items.Clear(); advanced.CollectPresentations(items); Assert.IsEmpty(items);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        internal static FieldInfo Field(object o,string name)
        {
            for(var t=o.GetType();t!=null;t=t.BaseType)
            { var f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); if(f!=null)return f; }
            throw new MissingFieldException(name);
        }
        internal static void Set(object o,string name,object value) => Field(o,name).SetValue(o,value);
        public static void Run()
        {
            try
            {
                var t=new StageEventPresentationTests();
                t.EveryOriginalHasShortRepeatTextAndUnchangedProgress(); t.SpecialMechanicsOnlyExplainTheCurrentSelection();
                t.ArtUsesRealGameplayBubbleFramesAndAllSevenStylesExist(); t.CollectorsFollowActualCompletionAndDriftDirection();
                var existing=new Ver4FoundationTests(); existing.OriginalProgressIsParentLocalCappedAndPreviewDoesNotMutate();
                existing.RestoreIsAtomicDeepCopiedAndIdempotent();
                Debug.Log("[StageEventPresentationTests] PASS 6 suites (189 original selection variants, art identity, lifecycle, progression restore)");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
