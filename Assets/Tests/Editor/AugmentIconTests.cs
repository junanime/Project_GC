using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public class AugmentIconTests
    {
        const string Root = "Assets/Junhan/Art/AugmentIcons/";
        static readonly string[] Legendary = { "NeuralBlock", "OrganCompression", "HungrySpirit", "NeedleShotgun", "GastricPeristalsisWave", "MucosalFortress" };
        static string Expected(Ability ability)
        {
            var so = new SerializedObject(ability);
            var prop = so.FindProperty("augmentType");
            if (ability is SyringeSpecialAugmentAbility)
                return Root + "Special/" + ((SyringeSpecialAugmentAbility.SpecialAugmentType)prop.intValue) + ".png";
            if (ability is SyringeLegendaryAugmentAbility)
            {
                string name = ((SyringeLegendaryAugmentAbility.LegendaryAugmentType)prop.intValue).ToString();
                if (Legendary.Contains(name)) return Root + "Legendary/" + name + ".png";
                if (name == "PoisonContagion") return Root + "Special/Poison.png";
            }
            return null;
        }
        static Ability[] AllAbilities() => AssetDatabase.FindAssets("t:Prefab", new[]{"Assets/Junhan/Prefabs/Abilities"})
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .SelectMany(go => go.GetComponentsInChildren<Ability>(true)).ToArray();

        public static void InstallAndValidate()
        {
            foreach (string path in Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            var abilities = AllAbilities();
            foreach (var ability in abilities)
            {
                string path = Expected(ability); if (path == null) continue;
                var so = new SerializedObject(ability);
                so.FindProperty("image").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(ability);
            }
            foreach (var ability in abilities)
            {
                var so = new SerializedObject(ability); var entries = so.FindProperty("sourceIconEntries");
                if (entries == null || !entries.isArray) continue;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    var source = entry.FindPropertyRelative("sourceAbility").objectReferenceValue as Ability;
                    string path = source != null ? Expected(source) : null;
                    if (path != null) entry.FindPropertyRelative("fallbackSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(ability);
            }
            AssetDatabase.SaveAssets(); ValidateAll();
        }

        [Test] public void IconsImportAndResolveWithoutDuplicates()
        {
            int special = 0, legendary = 0;
            var paths = new System.Collections.Generic.HashSet<string>();
            foreach (var ability in AllAbilities())
            {
                string expected = Expected(ability); if (expected == null) continue;
                Assert.IsNotNull(ability.Image, ability.name);
                Assert.AreEqual(expected, AssetDatabase.GetAssetPath(ability.Image), ability.name);
                if (ability is SyringeSpecialAugmentAbility) { special++; Assert.IsTrue(paths.Add(expected)); }
                else if (expected.Contains("/Legendary/")) { legendary++; Assert.IsTrue(paths.Add(expected)); }
            }
            Assert.AreEqual(16, special); Assert.AreEqual(6, legendary);
            var icons = Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories);
            Assert.AreEqual(27,icons.Length);
            foreach(var path in icons)
            {
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\','/'));
                Assert.IsNotNull(sprite,path); Assert.AreEqual(1254,sprite.rect.width); Assert.AreEqual(1254,sprite.rect.height);
            }
            foreach(var ability in AllAbilities()) Assert.IsFalse(AssetDatabase.GetAssetPath(ability.Image).Contains("/Planned/"));
        }
        [Test] public void ConditionalFallbacksMatchTheirParents()
        {
            foreach(var ability in AllAbilities())
            {
                var entries=new SerializedObject(ability).FindProperty("sourceIconEntries");
                if(entries==null || !entries.isArray) continue;
                for(int i=0;i<entries.arraySize;i++)
                {
                    var entry=entries.GetArrayElementAtIndex(i);
                    var parent=entry.FindPropertyRelative("sourceAbility").objectReferenceValue as Ability;
                    if(parent!=null && Expected(parent)!=null)
                        Assert.AreEqual(parent.Image,entry.FindPropertyRelative("fallbackSprite").objectReferenceValue,ability.name);
                }
            }
        }
        public static void ValidateAll()
        {
            var tests=new AugmentIconTests(); tests.IconsImportAndResolveWithoutDuplicates(); tests.ConditionalFallbacksMatchTheirParents();
            Debug.Log("[AugmentIcons] PASS 27 imported sprites; 16 special + 6 legendary bindings; conditional sharing; planned assets unbound.");
        }
    }
}
