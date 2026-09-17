using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class DirectorConsolidationTests
    {
        [Test]
        public void InternalSchedulesAreNotIndependentSceneComponents()
        {
            foreach (var type in new[] { typeof(StageEventSchedule), typeof(AdvancedStageFieldEventDirector),
                typeof(AntacidBubbleSurgeEventController), typeof(TimedSpecialSpawnSchedule),
                typeof(DigestiveEnzymeFieldSpawner), typeof(EliteMonsterSpawner), typeof(BloodClotFieldSpawner),
                typeof(MiniBossSpawner), typeof(BossLevelSpawner), typeof(EliteSummonObjectSpawner) })
            {
                Assert.That(typeof(RuntimeModule).IsAssignableFrom(type), Is.True, type.Name);
                Assert.That(typeof(Component).IsAssignableFrom(type), Is.False, type.Name);
                foreach (string message in new[] { "Awake", "Start", "Update", "LateUpdate", "FixedUpdate", "OnEnable", "OnDisable", "OnDestroy" })
                    Assert.That(type.GetMethod(message, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), Is.Null,
                        type.Name + " has an unforwarded Unity callback: " + message);
            }
        }

        [Test]
        public void MainSceneHasOneOwnerPerCategoryAndNoNewMissingScripts()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            // These two inactive map UI components were already missing at a89aaf0.
            // Their original serialized blocks are unchanged; do not delete unrelated user content.
            foreach (var transform in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (missing == 0) continue;
                ulong id = GlobalObjectId.GetGlobalObjectIdSlow(transform.gameObject).targetObjectId;
                Assert.That(id == 349207336UL || id == 724292426UL, Is.True, "New missing script: " + transform.name);
                Assert.That(missing, Is.EqualTo(1), transform.name);
            }
            Assert.That(all.OfType<StageEventDirector>().Count(), Is.EqualTo(1));
            Assert.That(all.OfType<TimedSpecialMonsterSpawner>().Count(), Is.EqualTo(1));
            var events = new SerializedObject(all.OfType<StageEventDirector>().Single());
            foreach (string field in new[] { "basicEvents", "advancedEvents", "antacidEvents" })
                Assert.That(events.FindProperty(field).arraySize, Is.EqualTo(1), field);
            var spawns = new SerializedObject(all.OfType<TimedSpecialMonsterSpawner>().Single());
            Assert.That(spawns.FindProperty("timedSpawns").arraySize, Is.EqualTo(2));
            Assert.That(spawns.FindProperty("timedSpawns").GetArrayElementAtIndex(1).FindPropertyRelative("moduleEnabled").boolValue, Is.False,
                "Inactive legacy trap configuration must remain disabled");
            foreach (string field in new[] { "enzymeSpawns", "eliteSpawns", "bloodClotSpawns", "summonObjectSpawns", "miniBossSpawns", "bossSpawns" })
                Assert.That(spawns.FindProperty(field).arraySize, Is.EqualTo(1), field);
        }

        [Test]
        public void CollectionPrefabsShareLogicButPreserveRewardKindAndNumbers()
        {
            foreach (bool gold in new[] { true, false })
            {
                string path = "Assets/Prefabs/MiniStages/MiniStage" + (gold ? "Gold" : "Exp") + "TimeAttackRoom.prefab";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var room = go.GetComponent<MiniStageCollectionRoom>();
                Assert.That(room, Is.Not.Null, path);
                Assert.That(room.GetType(), Is.EqualTo(typeof(MiniStageCollectionRoom)));
                Assert.That(room.RewardKind, Is.EqualTo(gold ? MiniStageCollectionRoom.CollectionRewardKind.Gold : MiniStageCollectionRoom.CollectionRewardKind.Experience));
                var data = new SerializedObject(room);
                Assert.That(data.FindProperty("timeLimit").floatValue, Is.GreaterThan(0));
                Assert.That(data.FindProperty(gold ? "coinSpawnWeights" : "gemSpawnWeights").arraySize, Is.GreaterThan(0));
                Assert.That(room.ReturnInteractable, Is.Not.Null);
            }
        }

        [Test]
        public void TrapSpawningCallsRealEntityApiWithoutReflection()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/Monsters/TimedSpecialSpawnSchedule.cs");
            Assert.That(source, Does.Contain("entityManager.SpawnMonster(poolIndex, position, blueprint, hpBuff)"));
            Assert.That(source, Does.Not.Contain("spawnMonsterMethod"));
            Assert.That(System.IO.File.Exists("Assets/Scripts/Monsters/TrapMonsterFieldSpawner.cs"), Is.False);
        }

        [Test]
        public void BossSchedulingHasOneOwnerAndSummonCancelsOnlyFinalBossModules()
        {
            string level = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/LevelManager.cs");
            Assert.That(level, Does.Not.Contain("HandleBossSpawn"));
            string summon = System.IO.File.ReadAllText("Assets/Junhan/Interaction/FinalBossSummonInteractable.cs");
            Assert.That(summon, Does.Contain("DisableScheduledFinalBosses()"));
            Assert.That(summon, Does.Not.Contain("bossSpawners[i].enabled = false"));
        }
    }

    public static class DirectorConsolidationTestRunner
    {
        public static void RunAll()
        {
            int count = 0;
            foreach (var type in new[] { typeof(DirectorConsolidationTests), typeof(MiniStageRewardPlacementTests),
                typeof(MonsterRemakeTests), typeof(AshiAnimationTests) })
                foreach (var method in type.GetMethods().Where(m => Attribute.IsDefined(m, typeof(TestAttribute))))
                {
                    method.Invoke(Activator.CreateInstance(type), null);
                    Debug.Log("[ConsolidationTests] PASS " + method.Name);
                    count++;
                }
            Debug.Log("[ConsolidationTests] ALL PASS " + count);
        }

        // Only grouping placement-only objects; do not remove transforms referenced by serialized settings.
        public static void GroupPlacementAnchors()
        {
            foreach (string path in new[] { "Assets/Scenes/Game/Level 1.unity", "Assets/Scenes/Game/Level1_TAB_RECOVERY.unity", "Assets/Scripts/boyoung/test/Level 1.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                foreach (var host in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<RuntimeModuleHost>(true)))
                {
                    var serialized = new SerializedObject(host);
                    var iterator = serialized.GetIterator();
                    var origins = new HashSet<Transform>();
                    while (iterator.Next(true))
                        if (iterator.name == "origin" && iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue is Transform origin)
                            origins.Add(origin);
                    foreach (var origin in origins)
                    {
                        if (origin == host.transform || origin.GetComponents<Component>().Length != 1 || host.transform.IsChildOf(origin)) continue;
                        origin.SetParent(host.transform, true);
                        if (!origin.name.StartsWith("[Placement] ")) origin.name = "[Placement] " + origin.name;
                    }
                }
                EditorSceneManager.SaveScene(scene);
            }
            RunAll();
        }
    }
}
