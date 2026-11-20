using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class SpatialHashGridTests
    {
        sealed class Client : ISpatialHashGridClient
        {
            public Vector2 position, size = Vector2.one;
            public int positionReads, sizeReads;
            public Vector2 Position { get { positionReads++; return position; } }
            public Vector2 Size { get { sizeReads++; return size; } }
            public Dictionary<int,int> ListIndexByCellIndex { get; set; }
            public int QueryID { get; set; } = -1;
        }

        static SpatialHashGrid Create() =>
            new SpatialHashGrid(new[] { Vector2.one*-10, Vector2.one*10 }, new Vector2Int(11,11));

        [Test] public void MovingAcrossCellsReusesIndexAndSamplesBoundsOnce()
        {
            var grid = Create();
            var first = new Client { position = Vector2.zero, size = Vector2.one*3 };
            var second = new Client { position = Vector2.zero };
            grid.InsertClient(first); grid.InsertClient(second);
            var index = first.ListIndexByCellIndex;
            first.position = new Vector2(8,8);
            first.positionReads = first.sizeReads = 0;
            grid.UpdateClient(first);
            Assert.AreSame(index,first.ListIndexByCellIndex);
            Assert.AreEqual(1,first.positionReads);
            Assert.AreEqual(1,first.sizeReads);
            CollectionAssert.AreEqual(new[] {second},grid.FindNearbyInRadius(Vector2.zero,3));
            grid.RemoveClient(second);
            grid.Rebuild(new Vector2(3,0));
            CollectionAssert.AreEqual(new[] {first},grid.FindNearbyInRadius(first.position,2));
            grid.Rebuild(new[] {Vector2.one*-30,Vector2.one*30},new Vector2Int(17,9));
            Assert.AreSame(index,first.ListIndexByCellIndex);
            Assert.AreSame(first,grid.FindClosestInRadius(first.position,1));
        }

        [Test] public void ClosestPreservesTieOrderAcrossMultipleCellsAndExcludesExactRadius()
        {
            var grid = Create();
            var left = new Client { position = Vector2.left, size = Vector2.one*3 };
            var right = new Client { position = Vector2.right, size = Vector2.one*3 };
            grid.InsertClient(right); grid.InsertClient(left);
            Assert.IsNull(grid.FindClosestInRadius(Vector2.zero,1));
            Assert.IsNull(grid.FindClosestInRadius(Vector2.zero,0));
            var ordered = grid.FindNearbyInRadius(Vector2.zero,2);
            Assert.AreEqual(2,ordered.Distinct().Count());
            Assert.AreSame(ordered[0],grid.FindClosestInRadius(Vector2.zero,2));
            grid.RemoveClient(left);
            Assert.AreSame(right,grid.FindClosestInRadius(Vector2.zero,2));
        }

        [Test] public void LootQueriesMatchSceneOrderThroughPoolActivationAndComponentDisable()
        {
            var root = new GameObject("Loot query test");
            var gems = new List<ExpGem>(); var coins = new List<Coin>();
            void CheckScene()
            {
                StageActorRegistry.CopyActiveGems(gems);
                StageActorRegistry.CopyActiveCoins(coins);
                CollectionAssert.AreEqual(Object.FindObjectsOfType<ExpGem>(),gems);
                CollectionAssert.AreEqual(Object.FindObjectsOfType<Coin>(),coins);
            }
            try
            {
                var one = new GameObject("Visible pickups");
                one.transform.SetParent(root.transform);
                one.AddComponent<ExpGem>().enabled = false;
                one.AddComponent<Coin>().enabled = false;
                var pool = new GameObject("Inactive loot pool");
                pool.transform.SetParent(root.transform);
                pool.AddComponent<ExpGem>(); pool.AddComponent<Coin>(); pool.SetActive(false);
                ResetActorRegistry();
                CheckScene();
                pool.SetActive(true);
                CheckScene();
                Object.DestroyImmediate(one);
                CheckScene();
            }
            finally { Object.DestroyImmediate(root); ResetActorRegistry(); }
        }

        [Test] public void ForagerTiesPreferGemsThenOriginalInstanceOrder()
        {
            var root = new GameObject("Forager target test");
            try
            {
                root.transform.position = new Vector2(4200,3200);
                var forager = root.AddComponent<NutritionThiefBacteriaMonster>();
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var type = typeof(NutritionThiefBacteriaMonster);
                var search = type.GetMethod("FindNearestCollectableTarget",flags);
                var gemField = type.GetField("targetGem",flags);
                var coinField = type.GetField("targetCoin",flags);
                var pickups = new GameObject[4];
                for (int i=0;i<pickups.Length;i++)
                {
                    pickups[i] = new GameObject("Forager candidate "+i);
                    pickups[i].transform.SetParent(root.transform,false);
                    pickups[i].transform.localPosition = i%2==0 ? Vector3.left : Vector3.right;
                    if (i<2) pickups[i].AddComponent<Coin>();
                    else pickups[i].AddComponent<ExpGem>();
                }
                ResetActorRegistry();
                search.Invoke(forager,null);
                Assert.AreSame(Object.FindObjectsOfType<ExpGem>().First(g=>g.transform.parent==root.transform),gemField.GetValue(forager));
                Assert.IsNull(coinField.GetValue(forager));
                pickups[2].SetActive(false); pickups[3].SetActive(false);
                search.Invoke(forager,null);
                Assert.IsNull(gemField.GetValue(forager));
                Assert.AreSame(Object.FindObjectsOfType<Coin>().First(c=>c.transform.parent==root.transform),coinField.GetValue(forager));
            }
            finally { Object.DestroyImmediate(root); ResetActorRegistry(); }
        }

        static void ResetActorRegistry() =>
            typeof(StageActorRegistry).GetMethod("Reset",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
    }
}
