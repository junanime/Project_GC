using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class TutorialGuideTests
    {
        public static void RunBatch()
        {
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/TutorialClips/Sheets" }))
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
                var tests = new TutorialGuideTests();
                tests.EveryFirstDiscoveryHasItsOwnPlayableClip();
                tests.MonstersHaveDistinctStableDiscoveryIds();
                Debug.Log("[TutorialGuide] PASS: 39 clips, unique IDs, monster mapping");
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
        [System.Serializable] private sealed class Catalog { public Entry[] entries; }
        [System.Serializable] private sealed class Entry
        {
            public string id;
            public string kind;
            public string title;
            public string effect;
        }

        [Test]
        public void EveryFirstDiscoveryHasItsOwnPlayableClip()
        {
            var text = Resources.Load<TextAsset>("TutorialClips/catalog");
            Assert.NotNull(text);
            var catalog = JsonUtility.FromJson<Catalog>(text.text);
            Assert.NotNull(catalog);
            Assert.AreEqual(39, catalog.entries.Length);
            var ids = new HashSet<string>();
            var effects = new HashSet<string>();
            int weapons = 0, events = 0, monsters = 0, mechanics = 0;
            foreach (var entry in catalog.entries)
            {
                Assert.IsTrue(ids.Add(entry.id), "Duplicate tutorial ID: " + entry.id);
                Assert.IsTrue(effects.Add(entry.effect), "Duplicate clip: " + entry.effect);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.title));
                var sheet = Resources.Load<Texture2D>("TutorialClips/Sheets/" + entry.effect);
                Assert.NotNull(sheet, "Missing loop sheet for " + entry.id);
                Assert.AreEqual(1024, sheet.width);
                Assert.AreEqual(432, sheet.height);
                Assert.AreEqual(FilterMode.Point, sheet.filterMode);
                Assert.AreEqual(1, sheet.mipmapCount);
                if (entry.kind == "weapon") weapons++;
                else if (entry.kind == "event") events++;
                else if (entry.kind == "monster") monsters++;
                else if (entry.kind == "mechanic") mechanics++;
                else Assert.Fail("Unknown kind: " + entry.kind);
            }
            Assert.AreEqual(21, weapons);
            Assert.AreEqual(7, events);
            Assert.AreEqual(10, monsters);
            Assert.AreEqual(1, mechanics);
            Assert.AreNotEqual(TutorialGuide.SeenKey("mechanic/blood-clot"), TutorialGuide.SeenKey("monster/sniper"));
        }

        [Test]
        public void MonstersHaveDistinctStableDiscoveryIds()
        {
            Assert.AreEqual("sniper", TutorialGuide.MonsterId("Sniper Monster"));
            Assert.AreEqual("speed-buffer", TutorialGuide.MonsterId("MonsterBuffer_MoveSpeed_Blueprint"));
            Assert.AreEqual("armor-buffer", TutorialGuide.MonsterId("MonsterBuffer_DamageReduction_Blueprint"));
            Assert.AreEqual("trap", TutorialGuide.MonsterId("Trap Monster"));
            Assert.IsNull(TutorialGuide.MonsterId("Ordinary Monster"));
            Assert.AreNotEqual(TutorialGuide.SeenKey("monster/sniper"), TutorialGuide.SeenKey("monster/speed-buffer"));
        }
    }
}
