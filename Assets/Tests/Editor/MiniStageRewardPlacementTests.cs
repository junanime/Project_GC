using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public class MiniStageRewardPlacementTests
    {
        private const string MiniStagePrefabRoot = "Assets/Prefabs/MiniStages";
        private static readonly Type[] CurrentRoomTypes =
        {
            typeof(MiniStageAcidBalanceRoom),
            typeof(MiniStageAcidLureRoom),
            typeof(MiniStageBonusChestRoom),
            typeof(MiniStageDigestiveWaveReflectRoom),
            typeof(MiniStageExplodingRushRoom),
            typeof(MiniStageCollectionRoom),
            typeof(MiniStageFallingFoodRoom),
            typeof(MiniStageSniperRoom)
        };

        [Test]
        public void EveryMiniStageRoomPrefabHasReturnPortal()
        {
            List<MiniStageRoomBase> rooms = LoadRoomPrefabs();

            foreach (Type roomType in CurrentRoomTypes)
            {
                Assert.That(rooms.Exists(room => room.GetType() == roomType), Is.True,
                    roomType.Name);
            }

            foreach (MiniStageRoomBase room in rooms)
            {
                Assert.That(room.ReturnInteractable, Is.Not.Null, room.gameObject.name);
            }
        }

        [Test]
        public void EveryMiniStageRewardPositionIsPortalRight()
        {
            List<MiniStageRoomBase> rooms = LoadRoomPrefabs();

            foreach (MiniStageRoomBase room in rooms)
            {
                Assert.That(room.ReturnInteractable, Is.Not.Null, room.gameObject.name);

                Vector3 expected = room.ReturnInteractable.transform.position
                    + Vector3.right * MiniStageRoomBase.RewardChestRightOffset;

                Assert.That(Vector3.Distance(room.RewardChestSpawnPosition, expected),
                    Is.LessThan(0.0001f), room.gameObject.name);
            }
        }

        [Test]
        public void RoomCompletionCannotAcceptPerRoomRewardPosition()
        {
            MethodInfo completeRoom = typeof(MiniStageRoomBase).GetMethod(
                "CompleteRoom", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo spawnRewardChest = typeof(MiniStageRoomBase).GetMethod(
                "SpawnRewardChest", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(completeRoom, Is.Not.Null);
            Assert.That(completeRoom.GetParameters(), Is.Empty,
                "Rooms must not override the common portal-relative reward position.");
            Assert.That(spawnRewardChest, Is.Not.Null);
            Assert.That(spawnRewardChest.GetParameters(), Is.Empty);
            Assert.That(MiniStageRoomBase.RewardChestRightOffset, Is.GreaterThan(0f));
        }

        private static List<MiniStageRoomBase> LoadRoomPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab", new[] { MiniStagePrefabRoot });
            List<MiniStageRoomBase> rooms = new List<MiniStageRoomBase>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                MiniStageRoomBase room = prefab != null
                    ? prefab.GetComponent<MiniStageRoomBase>()
                    : null;

                if (room != null)
                {
                    rooms.Add(room);
                }
            }

            return rooms;
        }
    }

    public static class MiniStageRewardPlacementTestRunner
    {
        public static void RunAll()
        {
            MiniStageRewardPlacementTests tests =
                new MiniStageRewardPlacementTests();
            Action[] cases =
            {
                tests.EveryMiniStageRoomPrefabHasReturnPortal,
                tests.EveryMiniStageRewardPositionIsPortalRight,
                tests.RoomCompletionCannotAcceptPerRoomRewardPosition
            };

            int failed = 0;

            foreach (Action test in cases)
            {
                try
                {
                    test();
                    Debug.Log($"[MiniStageRewardTests][PASS] {test.Method.Name}");
                }
                catch (Exception exception)
                {
                    failed++;
                    Debug.LogException(new Exception(
                        $"[MiniStageRewardTests][FAIL] {test.Method.Name}", exception));
                }
            }

            Debug.Log($"[MiniStageRewardTests] Completed | Total={cases.Length}, Failed={failed}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }
    }
}
