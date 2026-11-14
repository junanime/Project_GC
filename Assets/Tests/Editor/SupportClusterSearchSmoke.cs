#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Vampire.Tests.Editor
{
    // Edit-mode only: real Monster components and the production search, no run/save/profile mutation.
    public static class SupportClusterSearchSmoke
    {
        const string Output = "Library/SupportClusterSearchProof";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly FieldInfo Health = typeof(Monster).GetField("currentHealth", Private);
        static readonly FieldInfo Manager = typeof(Monster).GetField("entityManager", Private);
        static readonly FieldInfo Player = typeof(Monster).GetField("playerCharacter", Private);
        static readonly FieldInfo Living = typeof(EntityManager).GetField("livingMonsters", Private);
        static readonly FieldInfo Radius = typeof(MonsterSupportCaster).GetField("clusterScanRadius", Private);
        static int checks;

        public static void Run()
        {
            Directory.CreateDirectory(Output); checks = 0;
            File.WriteAllText(Output + "/checks.txt", "");
            File.WriteAllText(Output + "/errors.txt", "");
            var originalRandom = Random.state;
            var scene = default(Scene);
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || FiniteStomachMap.Instance != null)
                    throw new InvalidOperationException("Run in idle edit mode with no active runtime map.");
                scene = EditorSceneManager.NewPreviewScene();
                var manager = Make<EntityManager>(scene, "Isolated manager");
                var caster = Make<MonsterSupportCaster>(scene, "Isolated support caster");
                var player = Make<Character>(scene, "Fallback RNG target");
                player.transform.position = new Vector2(12, -7);
                Manager.SetValue(caster, manager); Player.SetValue(caster, player); Radius.SetValue(caster, 3f);
                var valid = (Func<Monster, bool>)Delegate.CreateDelegate(typeof(Func<Monster, bool>), caster, typeof(MonsterSupportCaster).GetMethod("IsValidClusterMonster", Private));
                var fallback = (Func<Vector2>)Delegate.CreateDelegate(typeof(Func<Vector2>), caster, typeof(MonsterSupportCaster).GetMethod("GetFallbackPosition", Private));
                var actual = (Func<Vector2>)Delegate.CreateDelegate(typeof(Func<Vector2>), caster, typeof(MonsterSupportCaster).GetMethod("FindDensestMonsterClusterPosition", Private));
                var a = Make<Monster>(scene, "A"); var b = Make<Monster>(scene, "B");
                var c = Make<Monster>(scene, "C"); var d = Make<Monster>(scene, "D");
                var dead = Make<Monster>(scene, "Dead"); Health.SetValue(dead, 0f);
                var inactive = Make<Monster>(scene, "Inactive"); inactive.gameObject.SetActive(false);
                var boss = Make<BossMonster>(scene, "Boss");
                var clot = Make<BloodClotMonster>(scene, "Clot");
                var colonBoss = Make<Monster>(scene, "Colon boss"); colonBoss.gameObject.AddComponent<ColonBossRuntime>();
                Check(!valid(caster) && !valid(dead) && !valid(inactive) && !valid(boss) && !valid(clot) && !valid(colonBoss), "Actual exclusion predicates reject self/dead/inactive/boss/clot/colon boss");

                Action<string, Monster[], Vector2?> compare = (name, items, expected) =>
                {
                    var list = new FastList<Monster>(); foreach (var item in items) list.Add(item); Living.SetValue(manager, list);
                    Compare(name, list, valid, fallback, actual, 3f, expected);
                };
                compare("Empty list fallback", Array.Empty<Monster>(), null);
                compare("All invalid fallback", new Monster[] { caster, dead, inactive, boss, clot, colonBoss }, null);
                a.transform.position = Vector2.zero;
                compare("Single candidate", new[] { a }, Vector2.zero);
                b.transform.position = new Vector2(3, 0);
                compare("Inclusive radius boundary", new[] { a, b }, new Vector2(1.5f, 0));
                b.transform.position = new Vector2(3.0001f, 0);
                compare("Just outside radius chooses first", new[] { a, b }, Vector2.zero);
                a.transform.position = new Vector2(-10, 0); b.transform.position = new Vector2(-9, 0);
                c.transform.position = new Vector2(10, 0); d.transform.position = new Vector2(11, 0);
                compare("Equal clusters retain original order", new[] { a, b, c, d }, new Vector2(-9.5f, 0));
                compare("Reversed tie follows list order", new[] { c, d, a, b }, new Vector2(10.5f, 0));
                compare("Exclusions do not alter chosen cluster", new Monster[] { dead, boss, a, inactive, b, clot, caster, colonBoss }, new Vector2(-9.5f, 0));
                // A new call must immediately see changed health and positions; no across-frame cache.
                Health.SetValue(a, 0f); b.transform.position = new Vector2(4, 7);
                compare("Next call refreshes health and positions", new[] { a, b }, new Vector2(4, 7));

                var random = new System.Random(261113); var crowd = new List<Monster>();
                for (int i = 0; i < 220; i++)
                {
                    var monster = Make<Monster>(scene, "Crowd " + i);
                    monster.transform.position = new Vector2((float)random.NextDouble() * 20 - 10, (float)random.NextDouble() * 20 - 10);
                    crowd.Add(monster);
                }
                var crowdList = new FastList<Monster>(); foreach (var monster in crowd) crowdList.Add(monster); Living.SetValue(manager, crowdList);
                for (int i = 0; i < 12; i++)
                {
                    Compare("Crowd layout " + i, crowdList, valid, fallback, actual, 3, null);
                    foreach (var monster in crowd) monster.transform.position += new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f);
                }
                Func<Vector2> legacy = () => Legacy(crowdList, valid, fallback, 3);
                legacy(); actual(); // JIT/warm-up excluded; delegates/reflection binding excluded from both timed loops.
                var rows = new List<string> { "pass,monsters,iterations,legacy_ms,optimized_ms,legacy_over_optimized" };
                for (int pass = 0; pass < 5; pass++)
                {
                    const int iterations = 12;
                    double oldMs, newMs;
                    if ((pass & 1) == 0) { oldMs = Measure(legacy, iterations); newMs = Measure(actual, iterations); }
                    else { newMs = Measure(actual, iterations); oldMs = Measure(legacy, iterations); }
                    rows.Add(string.Join(",", new[] { pass.ToString(), "220", iterations.ToString(), F(oldMs), F(newMs), F(oldMs / Math.Max(.000001, newMs)) }));
                }
                File.WriteAllLines(Output + "/cpu.csv", rows);
                // Delegate calls in Legacy add a small known cost. Field replay remains the authoritative performance check.
                File.WriteAllText(Output + "/result.txt", "PASS checks=" + checks + "; CPU comparison is isolated edit-mode search cost, not full-field FPS");
            }
            catch (Exception ex)
            {
                File.WriteAllText(Output + "/errors.txt", ex.ToString());
                File.WriteAllText(Output + "/result.txt", "FAIL checks=" + checks); throw;
            }
            finally
            {
                Random.state = originalRandom;
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static T Make<T>(Scene scene, string name) where T : Component
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(go, scene);
            var component = go.AddComponent<T>();
            if (component is Monster monster) Health.SetValue(monster, 100f);
            return component;
        }
        static void Compare(string name, FastList<Monster> list, Func<Monster, bool> valid, Func<Vector2> fallback, Func<Vector2> actual, float radius, Vector2? expected)
        {
            var before = Random.state;
            var oldResult = Legacy(list, valid, fallback, radius); var oldState = JsonUtility.ToJson(Random.state);
            Random.state = before;
            var newResult = actual(); var newState = JsonUtility.ToJson(Random.state);
            Check(oldResult.x.Equals(newResult.x) && oldResult.y.Equals(newResult.y), name + ": identical coordinates");
            Check(oldState == newState, name + ": identical RNG consumption");
            if (expected.HasValue) Check(newResult.x.Equals(expected.Value.x) && newResult.y.Equals(expected.Value.y), name + ": expected center");
        }
        static Vector2 Legacy(FastList<Monster> list, Func<Monster, bool> valid, Func<Vector2> fallback, float radius)
        {
            if (list == null || list.Count <= 0) return fallback();
            int bestCount = 0; Vector2 bestCenter = fallback();
            foreach (var candidate in list)
            {
                if (!valid(candidate)) continue;
                int count = 0; Vector2 sum = Vector2.zero;
                foreach (var other in list)
                {
                    if (!valid(other)) continue;
                    float distance = Vector2.Distance(candidate.transform.position, other.transform.position);
                    if (distance <= radius) { count++; sum += (Vector2)other.transform.position; }
                }
                if (count > bestCount) { bestCount = count; bestCenter = sum / Mathf.Max(1, count); }
            }
            return bestCenter;
        }
        static double Measure(Func<Vector2> action, int iterations)
        {
            long before = Stopwatch.GetTimestamp();
            for (int i = 0; i < iterations; i++) action();
            return (Stopwatch.GetTimestamp() - before) * 1000.0 / Stopwatch.Frequency;
        }
        static string F(double value) => value.ToString("F5", CultureInfo.InvariantCulture);
        static void Check(bool ok, string text)
        {
            if (!ok) throw new Exception("SUPPORT_CLUSTER_FAIL " + text);
            checks++; File.AppendAllText(Output + "/checks.txt", text + "\n");
        }
    }
}
#endif
