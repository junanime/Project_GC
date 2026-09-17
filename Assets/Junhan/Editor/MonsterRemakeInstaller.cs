using System;
using UnityEditor;
using UnityEngine;

namespace Vampire.EditorTools
{
    public static class MonsterRemakeInstaller
    {
        public const string Art = "Assets/Art/MonsterRemake/";
        public static Sprite[] Frames(string prefix, int count)
        {
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(Art + prefix + "_" + i.ToString("00") + ".png");
                if (frames[i] == null) throw new Exception("Missing sprite " + prefix + i);
            }
            return frames;
        }

        [MenuItem("Tools/Monster Remake/Install Approved Art")]
        public static void Install()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 675f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = path.Contains("Sniper_") ? new Vector2(0.5f, 0.075f) : new Vector2(0.5f, 0.5f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            ApplyData();
        }

        public static void ApplyData()
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            character.capturedSpriteSequence = Frames("Capture", 3);
            character.capturedFrameTime = 0.09f;
            EditorUtility.SetDirty(character);
            foreach (string guid in AssetDatabase.FindAssets("t:TrapMonsterBlueprint"))
            {
                var trap = AssetDatabase.LoadAssetAtPath<TrapMonsterBlueprint>(AssetDatabase.GUIDToAssetPath(guid));
                trap.captureRearSprites = Frames("SeaweedRear", 8);
                trap.captureFrontSprites = Frames("SeaweedFront", 8);
                trap.dormantSprites = new[] { trap.captureRearSprites[0] };
                trap.resultSprite = trap.captureRearSprites[0];
                trap.animationFrameTime = 0.11f;
                // Escape is exclusively the arrow puzzle, not damage or a timeout.
                trap.useArrowEscapeMiniGame = true;
                trap.bindPlayerOnActivate = true;
                trap.bindDuration = 0f;
                EditorUtility.SetDirty(trap);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:SniperMonsterBlueprint"))
            {
                var sniper = AssetDatabase.LoadAssetAtPath<SniperMonsterBlueprint>(AssetDatabase.GUIDToAssetPath(guid));
                sniper.fireSprites = Frames("Sniper", 8);
                sniper.fireFrameTime = 0.09f;
                sniper.walkSpriteSequence = new[] { sniper.fireSprites[0] };
                sniper.walkFrameTime = 0.2f;
                sniper.resultSprite = sniper.fireSprites[0];
                EditorUtility.SetDirty(sniper);
            }
            string prefabPath = "Assets/Junhan/Prefabs/Projectiles/SniperBulletProjectile.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                prefab.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Takoyaki.png");
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            UpdatePrefabVisual("Assets/Prefabs/Monsters/Trap Monster.prefab", Frames("SeaweedRear", 8)[0]);
            UpdatePrefabVisual("Assets/Prefabs/Monsters/SniperMonster.prefab", Frames("Sniper", 8)[0]);
            AssetDatabase.SaveAssets();
            Debug.Log("[MonsterRemake] Installed approved art, layered trap, arrow-only escape and takoyaki.");
        }

        private static void UpdatePrefabVisual(string path, Sprite sprite)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (!renderer.name.ToLowerInvariant().Contains("shadow"))
                    {
                        renderer.sprite = sprite;
                        break;
                    }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
