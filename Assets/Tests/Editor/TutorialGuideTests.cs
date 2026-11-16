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
                Debug.Log("[TutorialGuide] PASS: expanded gameplay guides, atlas metadata, unique IDs, monster mapping");
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
        [System.Serializable] private sealed class Catalog { public TutorialGuide.Entry[] entries; }
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
            Assert.GreaterOrEqual(catalog.entries.Length,80);
            var ids = new HashSet<string>();
            var effects = new HashSet<string>();
            int weapons = 0, events = 0, monsters = 0, mechanics = 0, items = 0;
            foreach (var entry in catalog.entries)
            {
                Assert.IsTrue(ids.Add(entry.id), "Duplicate tutorial ID: " + entry.id);
                Assert.IsTrue(effects.Add(entry.effect), "Duplicate clip: " + entry.effect);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.title));
                var sheet = Resources.Load<Texture2D>("TutorialClips/Sheets/" + entry.effect);
                if (!FoodTutorialPreview.Supports(entry.effect)) {
                Assert.NotNull(sheet, "Missing loop sheet for " + entry.id);
                Assert.AreEqual(entry.gameplay?entry.columns*384:1024, sheet.width);
                Assert.AreEqual(entry.gameplay?entry.rows*216:432, sheet.height);
                if(entry.gameplay){Assert.Greater(entry.frames,1);Assert.LessOrEqual(entry.frames,entry.columns*entry.rows);Assert.Greater(entry.fps,0);}
                Assert.AreEqual(FilterMode.Point, sheet.filterMode);
                Assert.AreEqual(1, sheet.mipmapCount);
                Resources.UnloadAsset(sheet);
                }
                if (entry.kind == "weapon") weapons++;
                else if (entry.kind == "event") events++;
                else if (entry.kind == "monster") monsters++;
                else if (entry.kind == "mechanic") mechanics++;
                else if (entry.kind == "item") items++;
                else Assert.Fail("Unknown kind: " + entry.kind);
            }
            Assert.AreEqual(21, weapons);
            Assert.GreaterOrEqual(events,29);
            Assert.GreaterOrEqual(monsters,25);
            Assert.AreEqual(3, items);
            Assert.GreaterOrEqual(mechanics,6);
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
