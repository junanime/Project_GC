using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class PrescriptionRemakeTests
    {
        [Test] public void DrawsThreeUniqueGoalsOnlyFromDesignatedTag()
        {
            var go=new GameObject("quest test");var q=go.AddComponent<PrescriptionRuntime>();
            try
            {
                for(int tag=0;tag<3;tag++) for(int seed=0;seed<50;seed++)
                {
                    q.StartPrescription(tag,seed);var s=q.Capture();
                    Assert.AreEqual(tag,s.tag);Assert.AreEqual(3,s.quests.Distinct().Count());
                    Assert.IsTrue(s.quests.All(x=>x/4==tag));
                }
            }
            finally {Object.DestroyImmediate(go);}
        }
        [Test] public void ProgressClampsAndSnapshotIsIndependent()
        {
            var go=new GameObject("quest test");var q=go.AddComponent<PrescriptionRuntime>();
            try
            {
                q.StartPrescription(1,42);var original=q.Capture();
                foreach(int goal in original.quests)q.Record((PrescriptionRuntime.Goal)goal,10000);
                Assert.IsTrue(q.Ready);Assert.AreEqual(3,q.Completed);
                Assert.IsTrue(original.progress.All(x=>x==0));
                var completed=q.Capture();q.StartPrescription(0,4);q.Restore(completed);
                Assert.IsTrue(q.Ready);Assert.AreEqual(1,q.Tag);
                completed.claimed=true;q.Restore(completed);Assert.IsFalse(q.Ready);
                q.Restore(new PrescriptionState{tag=1,quests=new[]{0,1,2},progress=new float[3]});
                Assert.IsTrue(q.Claimed,"Invalid cross-tag data must not replace the saved prescription");
            }
            finally {Object.DestroyImmediate(go);}
        }
        [Test] public void SkillLevelsRestoreWithinOneToFive()
        {
            var go=new GameObject("skill level test");var s=go.AddComponent<CharacterSkillRuntime>();
            try
            {
                s.RestoreLevels(0,99);Assert.AreEqual(1,s.PassiveLevel);Assert.AreEqual(5,s.ActiveLevel);
                Assert.AreEqual(2.8561f,s.ActivePower,.0001f);
                s.RestoreLevels(3,3);Assert.AreEqual(1.69f,s.PassivePower,.0001f);
                s.RestoreLevels(3,3);Assert.AreEqual(1.69f,s.PassivePower,.0001f);
            }
            finally {Object.DestroyImmediate(go);}
        }
        [Test] public void ExcludedRoomsStayOutOfBothDifficultyPools()
        {
            int included=0,excluded=0;
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/MiniStages"}))
            {
                var room=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)).GetComponent<MiniStageRoomBase>();
                if(room==null)continue;
                bool disabled=room is MiniStageAcidBalanceRoom || room is MiniStageAcidLureRoom || room is MiniStageDigestiveWaveReflectRoom;
                Assert.AreEqual(!disabled,MiniStageDirector.RoomEnabled(room));
                if(disabled)excluded++;else included++;
            }
            Assert.GreaterOrEqual(included,6);Assert.GreaterOrEqual(excluded,3);
        }
        [Test] public void LegacyLegendaryDropsAreDisabledAndNormalChestIsConfigured()
        {
            var old=Resources.Load<Ver4AugmentBalance>("Ver4AugmentBalance");
            Assert.AreEqual(0,old.miniBossLegendaryChestChance);Assert.IsFalse(old.antacidCompletionLegendaryChest);
            var balance=Resources.Load<RemakeBalance>("RemakeBalance");Assert.NotNull(balance);
            Assert.IsTrue(balance.levelUpChest.abilityChest);Assert.IsFalse(balance.levelUpChest.legendaryAugmentChest);
            Assert.AreEqual(25,balance.overchargeKills);Assert.AreEqual(1.2f,balance.fastBomberSpeed);
        }
    }
}
