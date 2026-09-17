using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public class PacingPatchTests
    {
        internal const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        internal static object Get(object obj, string field)
        {
            for (Type t = obj.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(field, Hidden);
                if (f != null) return f.GetValue(obj);
            }
            throw new Exception(field);
        }
        internal static void Set(object obj, string field, object value)
        {
            for (Type t = obj.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(field, Hidden);
                if (f != null) { f.SetValue(obj, value); return; }
            }
            throw new Exception(field);
        }

        [Test] public void SpawnDensityAndChestSupplyMatchRequestedMultipliers()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelBlueprint>("Assets/Blueprints/Levels/Level 1.asset");
            float[] previous = { .5f,.55f,.65f,.85f,1f,1.25f,.7f,.45f,1.25f,1.35f,1.45f,1.6f,1.8f,2.1f,2.4f,2.6f };
            for (int m = 0; m <= 15; m++) Assert.That(level.monsterSpawnTable.GetSpawnRate(m / 15f), Is.EqualTo(previous[m] * 1.4f).Within(.0001f));
            Assert.That(level.chestSpawnAmount / level.chestSpawnDelay, Is.EqualTo(1f / 30f).Within(.0001f));
            Assert.That(level.normalSpawnPopulationLimit, Is.EqualTo(224));
            var boss=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Junhan/Prefabs/Boss/Test/BossPartDamageTestRoot.prefab");
            foreach(var pattern in boss.GetComponentsInChildren<BossAbsorbMinionHealPattern>(true))
                Assert.That((float)Get(pattern,"minionMoveSpeed"),Is.EqualTo(1.2f));
        }

        [Test] public void EncounterCountRampsWithoutExceedingOneToThree()
        {
            var random = new System.Random(120);
            int first = 0, late = 0;
            for (int i = 0; i < 10000; i++)
            {
                Assert.AreEqual(1, TimedSpecialSpawnSchedule.SelectRampedCount(1,3,0,.85f,()=> (float)random.NextDouble()));
                int a = TimedSpecialSpawnSchedule.SelectRampedCount(1,3,.1f,.85f,()=> (float)random.NextDouble());
                int b = TimedSpecialSpawnSchedule.SelectRampedCount(1,3,1,.85f,()=> (float)random.NextDouble());
                Assert.That(a, Is.InRange(1,3)); Assert.That(b, Is.InRange(1,3));
                if (a == 3) first++; if (b == 3) late++;
            }
            Assert.Less(first, 150); Assert.Greater(late, 6900);
        }

        [Test] public void ActualScenesScheduleRecurringSporeAndPairedBuffers()
        {
            foreach (string path in new[] { "Assets/Scenes/Game/Level 1.unity", "Assets/Scenes/Game/Level1_TAB_RECOVERY.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var host = scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<TimedSpecialMonsterSpawner>(true)).First();
                var schedule = (TimedSpecialSpawnSchedule)((IList)Get(host,"timedSpawns"))[0];
                var entries = (TimedSpecialSpawnSchedule.TimedSpawnEntry[])Get(schedule,"spawnEntries");
                var sporeData = AssetDatabase.LoadAssetAtPath<MonsterBlueprint>("Assets/Prefabs/Monsters/genral Monster BP/Acid Spore Blueprint.asset");
                var spore = entries.Single(x=>x.monsterBlueprint == sporeData);
                Assert.IsTrue(spore.repeatAtRandomInterval); Assert.AreEqual(180,spore.spawnTimeSeconds);
                Assert.AreEqual(40,spore.minRepeatInterval); Assert.AreEqual(80,spore.maxRepeatInterval);
                Assert.AreEqual(3,spore.randomMaxSpawnCount);
                var buffers = entries.Single(x=>x.companionBlueprint != null);
                Assert.AreEqual(420,buffers.spawnTimeSeconds); Assert.AreEqual(2,buffers.randomMaxSpawnCount);
                typeof(TimedSpecialSpawnSchedule).GetMethod("BuildRuntimeSchedule",Hidden).Invoke(schedule,null);
                var events = ((IList)Get(schedule,"runtimeTimedSpawnEvents")).Cast<object>().Where(x=>Get(x,"entry") == spore).ToArray();
                Assert.GreaterOrEqual(events.Length,9);
                Assert.AreEqual(180f,Get(events[0],"scheduledTime"));
                for(int i=1;i<events.Length;i++) Assert.That((float)Get(events[i],"scheduledTime")-(float)Get(events[i-1],"scheduledTime"),Is.InRange(40f,80.001f));
                var elite = ((IList)Get(host,"eliteSpawns"))[0]; Assert.AreEqual(50,Get(elite,"normalSpawnsPerElite"));
                foreach(var phase in (EliteMonsterSpawner.EliteSpawnPhase[])Get(elite,"spawnPhases"))
                    Assert.That(phase.normalToEliteMappings,Has.Length.EqualTo(6),"Unity must deserialize every phase mapping (no YAML aliases)");
            }
        }

        [Test] public void DashBlocksWeaponEmissionAndRestoresItAfterward()
        {
            var go = new GameObject("Dash gate test"); go.SetActive(false);
            try
            {
                var player = go.AddComponent<Character>(); var weapon = go.AddComponent<SyringeDartAbility>();
                Set(weapon,"playerCharacter",player); Set(player,"isDashing",true);
                Assert.IsTrue(weapon.AttacksBlocked);
                Assert.IsNull(weapon.SpawnPlayerProjectile(-1,Vector2.zero,10,0,1,0));
                Set(player,"isDashing",false); Assert.IsFalse(weapon.AttacksBlocked);
                Set(weapon,"projectileSpawnForwardOffset",.28f); Set(weapon,"projectileSpawnDownOffset",.18f);
                Assert.That(weapon.GetReturnCatchPosition(Vector2.right),Is.EqualTo((Vector2)player.transform.position+new Vector2(.28f,-.18f)));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void OwnershipRestoreKeepsDuplicatesAndDoesNotReapplyStats()
        {
            var go = new GameObject("Ownership test"); go.SetActive(false);
            try
            {
                var owner = go.AddComponent<SynergyManager>();
                var item = AssetDatabase.LoadAssetAtPath<MerchantItemBlueprint>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:MerchantItemBlueprint")[0]));
                var snapshot = new MerchantOwnershipSnapshot(); snapshot.items.Add(item); snapshot.items.Add(item);
                snapshot.activated.Add(item.itemTag); snapshot.tags.Add(item.itemTag); snapshot.names.Add("test synergy");
                owner.RestoreOwnership(snapshot); owner.RestoreOwnership(snapshot);
                Assert.AreEqual(2,owner.OwnedItems.Count); Assert.AreEqual(1,owner.ActiveSynergyNames.Count);
                Assert.AreEqual(1,owner.CaptureOwnership().activated.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        public static void RunAll()
        {
            Balance15MinuteTestRunner.RunAll();
            var tests = new PacingPatchTests();
            foreach(var method in typeof(PacingPatchTests).GetMethods().Where(x=>x.IsDefined(typeof(TestAttribute),true)))
            { method.Invoke(tests,null); Debug.Log("[PacingPatch] PASS " + method.Name); }
        }
    }
}
