using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Vampire.Tests.Editor
{
    public class Ver4FoundationTests
    {
        [Test] public void EveryBasisPointResolvesToConfirmedGrade()
        {
            var odds = new AugmentUpgradeOdds();
            int[] actual = new int[6];
            for (int i=0; i<10000; i++)
            {
                Assert.IsTrue(odds.TryRoll((i+.5)/10000, _=>true, out var grade));
                actual[(int)grade]++;
            }
            CollectionAssert.AreEqual(new[]{3437,2749,1718,1400,516,180}, actual);
            Assert.IsFalse(AugmentUpgradeOdds.IsNumeric(AugmentUpgradeGrade.Original));
        }

        [Test] public void EachPanelUsesItsOwnSampleAndInvalidPoolDoesNotFallbackToLegend()
        {
            var odds = new AugmentUpgradeOdds();
            foreach (var sample in new[]{.0,.4,.65,.82,.95,.99})
                Assert.IsTrue(odds.TryRoll(sample, _=>true, out _));
            Assert.IsFalse(odds.TryRoll(.5, _=>false, out _));
            Assert.IsTrue(odds.TryRoll(.8, g=>g==AugmentUpgradeGrade.Epic, out var result));
            Assert.AreEqual(AugmentUpgradeGrade.Epic,result);
            for (int i=0;i<1000;i++)
            {
                Assert.IsTrue(odds.TryRoll((i+.5)/1000,g=>g!=AugmentUpgradeGrade.Original,out result));
                Assert.AreNotEqual(AugmentUpgradeGrade.Original,result);
            }
            Assert.Throws<ArgumentOutOfRangeException>(()=>odds.TryRoll(1,_=>true,out _));
            Assert.Throws<ArgumentOutOfRangeException>(()=>odds.TryRoll(double.NaN,_=>true,out _));
        }

        [Test] public void OriginalProgressIsParentLocalCappedAndPreviewDoesNotMutate()
        {
            var progress = new OriginalAugmentProgress();
            Assert.IsFalse(progress.TrySelect("Poison",0));
            progress.RegisterOwnedParent("Poison"); progress.RegisterOwnedParent("Explosion");
            for(int i=0;i<100;i++) Assert.IsTrue(progress.CanSelect("Poison",0));
            Assert.AreEqual(0,progress.Level("Poison"));
            for(int effect=0;effect<3;effect++)
            {
                for(int i=0;i<3;i++) Assert.IsTrue(progress.TrySelect("Poison",effect));
                Assert.IsFalse(progress.TrySelect("Poison",effect));
            }
            Assert.AreEqual(9,progress.Level("Poison"));
            Assert.AreEqual(0,progress.Level("Explosion"));
            Assert.AreEqual(AugmentUpgradeGrade.Supreme,progress.ResolveNumericGrade("Poison",AugmentUpgradeGrade.Common));
            Assert.AreEqual(AugmentUpgradeGrade.Common,progress.ResolveNumericGrade("Explosion",AugmentUpgradeGrade.Common));
            Assert.Throws<ArgumentException>(()=>progress.ResolveNumericGrade("Poison",AugmentUpgradeGrade.Original));
        }

        [Test] public void RestoreIsAtomicDeepCopiedAndIdempotent()
        {
            var progress = new OriginalAugmentProgress();
            progress.RegisterOwnedParent("Poison"); progress.TrySelect("Poison",1);
            var snapshot = progress.Capture();
            Assert.IsTrue(progress.TryRestore(snapshot,id=>id=="Poison"));
            Assert.IsTrue(progress.TryRestore(snapshot,id=>id=="Poison"));
            Assert.AreEqual(1,progress.Level("Poison"));
            snapshot[0].selections[1]=3;
            Assert.AreEqual(1,progress.Level("Poison"));
            snapshot.Add(new OriginalAugmentProgress.ParentSnapshot{parentId="Unowned", selections=new[]{0,0,0}});
            Assert.IsFalse(progress.TryRestore(snapshot,id=>id=="Poison"));
            Assert.AreEqual(1,progress.Level("Poison"));
        }

        [Test] public void TouchPointerOwnershipAndCancellationPreventCrossFingerRelease()
        {
            var systemObject = new GameObject("Test EventSystem",typeof(EventSystem));
            var controlObject = new GameObject("Test Control",typeof(RectTransform),typeof(MobileTouchControl));
            float oldScale=Time.timeScale;
            try
            {
                Time.timeScale=1;
                var system=systemObject.GetComponent<EventSystem>();
                var control=controlObject.GetComponent<MobileTouchControl>();
                int pressed=0,released=0; control.Pressed=()=>pressed++; control.Released=()=>released++;
                var first=new PointerEventData(system){pointerId=1};
                var second=new PointerEventData(system){pointerId=2};
                control.OnPointerDown(first); control.OnPointerDown(second); control.OnPointerUp(second);
                Assert.AreEqual(1,pressed); Assert.AreEqual(0,released);
                control.OnPointerUp(first); Assert.AreEqual(1,released);
                control.OnPointerDown(second); control.Cancel(); control.Cancel(); Assert.AreEqual(2,released);
                Time.timeScale=0; control.OnPointerDown(first); Assert.AreEqual(2,pressed);
            }
            finally { Time.timeScale=oldScale; UnityEngine.Object.DestroyImmediate(controlObject); UnityEngine.Object.DestroyImmediate(systemObject); }
        }

        public static void Run()
        {
            try
            {
                var test=new Ver4FoundationTests();
                test.EveryBasisPointResolvesToConfirmedGrade();
                test.EachPanelUsesItsOwnSampleAndInvalidPoolDoesNotFallbackToLegend();
                test.OriginalProgressIsParentLocalCappedAndPreviewDoesNotMutate();
                test.RestoreIsAtomicDeepCopiedAndIdempotent();
                test.TouchPointerOwnershipAndCancellationPreventCrossFingerRelease();
                Debug.Log("[Ver4 Foundation] PASS 5 tests. Domain and touch pointer checks only; not full gameplay validation.");
                EditorApplication.Exit(0);
            }
            catch(Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
