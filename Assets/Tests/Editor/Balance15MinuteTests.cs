using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public class Balance15MinuteTests
    {
        private static LevelBlueprint Level => AssetDatabase.LoadAssetAtPath<LevelBlueprint>("Assets/Blueprints/Levels/Level 1.asset");
        [Test]
        public void SpawnWeightsHandleBoundariesAndUnnormalizedInspectorValues()
        {
            var t = new MonsterSpawnTable { spawnChanceKeyframes = new[] {
                new MonsterSpawnTable.SpawnChanceKeyframe { t = 0, spawnChances = new[] { 2f, 0f } },
                new MonsterSpawnTable.SpawnChanceKeyframe { t = 1, spawnChances = new[] { 0f, 4f } } } };
            Assert.AreEqual(0, t.SelectMonster(-1, .99f));
            Assert.AreEqual(1, t.SelectMonster(2, 1));
            Assert.AreEqual(1f / 3f, t.GetProbability(.5f, 0), .0001f);
            t.spawnChanceKeyframes = new[] { t.spawnChanceKeyframes[0] };
            Assert.AreEqual(0, t.SelectMonster(1));
            t.spawnChanceKeyframes[0].spawnChances = new[] { 0f, 0f };
            Assert.AreEqual(-1, t.SelectMonster(.5f));
            Assert.AreEqual(-1, new MonsterSpawnTable().SelectMonster(0));
        }
        [Test]
        public void RepeatedKeysNeverDivideByZeroOrSelectZeroWeight()
        {
            var t = new MonsterSpawnTable { spawnChanceKeyframes = new[] {
                new MonsterSpawnTable.SpawnChanceKeyframe { t = 0, spawnChances = new[] { 1f, 0f } },
                new MonsterSpawnTable.SpawnChanceKeyframe { t = 0, spawnChances = new[] { 0f, 1f } },
                new MonsterSpawnTable.SpawnChanceKeyframe { t = 1, spawnChances = new[] { 0f, 1f } } } };
            for (int i = 0; i <= 100; i++) Assert.AreEqual(1, t.SelectMonster(i / 100f, i / 100f));
        }
        [Test]
        public void HighestNormalTierHasExactLateGameMixAndNoEarlyLeak()
        {
            var t = Level.monsterSpawnTable;
            Assert.AreEqual(900f, Level.levelTime);
            Assert.AreEqual(0, t.GetProbability(659f / 900f, 7));
            Assert.AreEqual(0, t.GetProbability(660f / 900f, 7));
            Assert.Greater(t.GetProbability(661f / 900f, 7), 0);
            Assert.AreEqual(.60f, t.GetProbability(790f / 900f, 7), .0001f);
            Assert.AreEqual(.85f, t.GetProbability(860f / 900f, 7), .0001f);
            float previous = 0;
            var normals = new HashSet<int> { 0, 1, 4, 5, 6, 7 };
            for (int sec = 0; sec <= 1000; sec++)
            {
                float p = t.GetProbability(sec / 900f, 7);
                Assert.GreaterOrEqual(p + .00001f, previous); previous = p;
                float sum = 0;
                for (int i = 0; i < 27; i++)
                {
                    float chance = t.GetProbability(sec / 900f, i);
                    sum += chance;
                    if (!normals.Contains(i)) Assert.AreEqual(0, chance, "Special/elite leaked into normal pool");
                }
                Assert.AreEqual(1f, sum, .0001f);
            }
            Assert.Greater(t.GetSpawnRate(1), 0, "Boss phase must retain normal pressure");
            Assert.Less(t.GetSpawnRate(7f / 15), t.GetSpawnRate(5f / 15));
        }
        [Test]
        public void ProfileAndBakedAssetAgreeAtEverySecond()
        {
            var profile = AssetDatabase.LoadAssetAtPath<LevelSpawnBalanceProfile>("Assets/Junhan/Script/Spawn/Level1_15Min_SpawnBalanceProfile.asset");
            Assert.NotNull(profile);
            var built = profile.BuildSpawnTable(Level);
            for (int sec = 0; sec <= 900; sec++)
            {
                float t = sec / 900f;
                Assert.AreEqual(Level.monsterSpawnTable.GetSpawnRate(t), built.GetSpawnRate(t), .001f);
                for (int i = 0; i < 27; i++) Assert.AreEqual(Level.monsterSpawnTable.GetProbability(t, i), built.GetProbability(t, i), .001f);
            }
        }
        [Test]
        public void TwoIndependentWindowTracksDoNotDriftAndAllowOverlap()
        {
            var tracks = new[] { new StageEventWindowTrack { windowSeconds = 120 }, new StageEventWindowTrack { windowSeconds = 360 } };
            for (int seed = 0; seed < 1000; seed++)
            {
                var random = new System.Random(seed);
                var slots = StageEventWindowPlanner.Build(900, tracks, () => (float)random.NextDouble());
                Assert.AreEqual(11, slots.Count);
                Assert.AreEqual(8, slots.Count(s => s.track == 0));
                Assert.AreEqual(3, slots.Count(s => s.track == 1));
                foreach (var group in slots.GroupBy(s => s.track))
                {
                    int window = 0;
                    foreach (var slot in group.OrderBy(s => s.time))
                    {
                        Assert.GreaterOrEqual(slot.time, window * tracks[group.Key].windowSeconds);
                        Assert.Less(slot.time, Math.Min(900, (window + 1) * tracks[group.Key].windowSeconds));
                        window++;
                    }
                }
            }
            var coincident = StageEventWindowPlanner.Build(900, tracks, () => 0f);
            Assert.AreEqual(2, coincident.Count(s => s.time == 0));
        }
        [Test]
        public void FixedDamageAugmentsAndSugarChildrenUseTheNewScale()
        {
            var weapon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Junhan/Prefabs/Abilities/Syringe Dart Ability.prefab");
            var settings = new SerializedObject(weapon.GetComponent<SyringeDartAbility>());
            foreach (string field in new[] { "poisonTickDamage", "explosionDamage", "fiberTrailDamagePerSecond", "digestiveAcidPuddleDamagePerSecond", "organCompressionDamagePerTick" })
                Assert.AreEqual(4, settings.FindProperty(field).floatValue, field);
            foreach (string name in new[] { "SugarCube_Normal_Split1", "SugarCube_Elite_Split2" })
            {
                var child = AssetDatabase.LoadAssetAtPath<SugarCubeMonsterBlueprint>("Assets/Prefabs/Monsters/Sugar Cube Monster_for/" + name + ".asset");
                Assert.AreEqual(25, child.hp);
                Assert.IsFalse(child.CanSplit());
            }
            var root = Level.finalBoss.bossPrefab.GetComponentInChildren<BossPartDamageTestRootController>(true);
            Assert.AreEqual(9000, new SerializedObject(root).FindProperty("fiveCoreMaxHealth").floatValue);
        }
        [Test]
        public void InitialCombatAndSummonBindingsAreValid()
        {
            Assert.AreEqual(25, Level.monsters[0].monsterBlueprints[0].hp);
            var weapon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Junhan/Prefabs/Abilities/Syringe Dart Ability.prefab");
            var serialized = new SerializedObject(weapon.GetComponent<SyringeDartAbility>());
            Assert.AreEqual(10, serialized.FindProperty("damage.value").floatValue);
            Assert.NotNull(Level.finalBoss.bossPrefab.GetComponentInChildren<BossController>(true));
            Assert.IsNull(Level.finalBoss.bossPrefab.GetComponent<BossMonster>());
            foreach (var path in new[] { "Assets/Scenes/Game/Level 1.unity", "Assets/Scenes/Game/Level1_TAB_RECOVERY.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var roots = scene.GetRootGameObjects();
                Assert.AreEqual(1, roots.SelectMany(r => r.GetComponentsInChildren<FinalBossSummonInteractable>(true)).Count());
                var director = roots.SelectMany(r => r.GetComponentsInChildren<StageEventDirector>(true)).Single();
                var settings = new SerializedObject(director);
                Assert.AreEqual(120, settings.FindProperty("primaryTrack.windowSeconds").floatValue);
                Assert.AreEqual(360, settings.FindProperty("secondaryTrack.windowSeconds").floatValue);
            }
        }
    }
    public static class Balance15MinuteTestRunner
    {
        public static void RunAll()
        {
            var tests = new Balance15MinuteTests();
            int count = 0;
            foreach (var method in typeof(Balance15MinuteTests).GetMethods().Where(m => m.GetCustomAttributes(typeof(TestAttribute), true).Length > 0))
            {
                method.Invoke(tests, null); count++;
                Debug.Log("[Balance15] PASS " + method.Name);
            }
            Debug.Log("[Balance15] ALL PASS " + count);
        }
    }
}
