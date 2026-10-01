using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vampire.EditorTools
{
    // Imports approved source sheets without redrawing or baking preview GIF backgrounds.
    public static class CharacterDesignInstaller
    {
        public const string Art = "Assets/Junhan/Art/CharacterDesigns/";
        public const string Blueprints = "Assets/Blueprints/Characters/";
        [MenuItem("Tools/Characters/Install approved designs")]
        public static void Install()
        {
            AssetDatabase.Refresh();
            var ashi = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>(Blueprints + "Main Character Blueprint.asset");
            if (ashi == null) throw new Exception("Missing Ashi baseline");
            ashi.profileSprite = Profile("Ashi");
            ashi.useCharacterHandAnchor = true;
            ashi.idleHandOffset = ashi.walkHandOffset = new Vector2(.25f, -.14f);
            EditorUtility.SetDirty(ashi);
            var result = new[] { ashi, Build("Ari", "아리", "작지만 호기심만큼은 누구보다 큰 아기 봉황.", .43f, .18f, ashi),
                Build("Hyuki", "혁이", "느긋한 낮잠을 즐기는 재능 있는 친구.", .59f, .26f, ashi),
                Build("Shini", "신이", "끝까지 도전하는 열혈 노력파 봉황.", .51f, .23f, ashi) };
            var config = AssetDatabase.LoadAssetAtPath<ApothecaryUIConfig>(ApothecaryUIInstaller.ConfigPath);
            config.characters = result; EditorUtility.SetDirty(config);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
                var selector = UnityEngine.Object.FindObjectOfType<CharacterSelector>(true);
                var serialized = new SerializedObject(selector);
                var list = serialized.FindProperty("characterBlueprints");
                list.arraySize = result.Length;
                for (int i = 0; i < result.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = result[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterDesign] Installed four cosmetic-only characters");
        }
        public static void InstallBatch() { Install(); EditorApplication.Exit(0); }
        static CharacterBlueprint Build(string id, string name, string description, float height, float idleTime, CharacterBlueprint baseline)
        {
            string path = Blueprints + id + " Character Blueprint.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>(path);
            if (data == null) { data = ScriptableObject.CreateInstance<CharacterBlueprint>(); AssetDatabase.CreateAsset(data, path); }
            data.name = name; data.description = description; data.owned = true; data.cost = 0;
            // Cosmetic preview release: no character-specific combat or passive effects.
            data.hp = baseline.hp; data.recovery = baseline.recovery; data.armor = baseline.armor;
            data.movespeed = baseline.movespeed; data.luck = baseline.luck; data.acceleration = baseline.acceleration;
            data.startingAbilities = baseline.startingAbilities.ToArray();
            data.profileSprite = Profile(id);
            data.useCharacterHandAnchor = true;
            data.walkHandOffset = id == "Ari" ? new Vector2(.19f, -.18f)
                : id == "Hyuki" ? new Vector2(.25f, -.06f) : new Vector2(.23f, -.12f);
            data.idleHandOffset = id == "Hyuki" ? new Vector2(.43f, -.24f) : data.walkHandOffset;
            float headWorldWidth = 0;
            data.walkSpriteSequence = Sequence(id, "Walk", 4, 2, height, ref headWorldWidth);
            data.idleSpriteSequence = Sequence(id, "Idle", 4, 2, height, ref headWorldWidth);
            data.dashSpriteSequence = Sequence(id, "Dash", id == "Hyuki" ? 2 : 4, id == "Hyuki" ? 1 : 2, height, ref headWorldWidth);
            data.capturedSpriteSequence = Sequence(id, "Captured", 1, 1, height, ref headWorldWidth);
            data.walkFrameTime = .08f; data.idleFrameTime = idleTime;
            data.fitDashAnimationToDuration = true; data.dashArtMovesRight = true;
            data.dashFrameTime = .22f / data.dashSpriteSequence.Length;
            data.capturedFrameTime = .09f;
            data.resultIdleSpriteSequence = data.idleSpriteSequence; data.resultIdleFrameTime = idleTime;
            data.resultActionASpriteSequence = Array.Empty<Sprite>(); data.resultActionBSpriteSequence = Array.Empty<Sprite>();
            EditorUtility.SetDirty(data); return data;
        }
        static Sprite Profile(string id)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + id + "_Profile.png");
            Configure(importer); importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(importer.assetPath);
        }
        static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        }
        static Sprite[] Sequence(string id, string state, int cols, int rows, float height, ref float headWorldWidth)
        {
            string path = Art + id + "_" + state + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Configure(importer); importer.isReadable = true; importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var bounds = new Rect[cols * rows]; var heads = new Rect[bounds.Length]; var cells = new Rect[bounds.Length];
            for (int i = 0; i < bounds.Length; i++)
            {
                int x0 = i % cols * texture.width / cols, x1 = (i % cols + 1) * texture.width / cols;
                int y0 = (rows - 1 - i / cols) * texture.height / rows, y1 = (rows - i / cols) * texture.height / rows;
                cells[i] = new Rect(x0, y0, x1 - x0, y1 - y0);
                var pixels = texture.GetPixels(x0, y0, x1 - x0, y1 - y0);
                bounds[i] = Bounds(pixels, x1 - x0, y1 - y0, c => c.a > .7f);
                // Exclude Hyuki's blue sleeping mat when measuring the blue head.
                heads[i] = Bounds(pixels, x1 - x0, y1 - y0, c => c.a > .7f && (id == "Ari" ? c.r > .75f && c.g > .48f && c.b > .48f && c.r > c.g * 1.06f : id == "Hyuki" ? c.g > .38f && c.b > .55f && c.b > c.r * 1.25f && c.b > c.g * 1.08f : c.r > .65f && c.g > .18f && c.g < .65f && c.b > .14f && c.b < .60f && c.r > c.g * 1.25f), id == "Hyuki" && (state == "Walk" || state == "Captured") ? .45f : 0);
            }
            float ppu = state == "Walk" ? bounds.Average(b => b.height) / height : heads.Average(b => b.width) / headWorldWidth;
            // Shini's swept-back crest exaggerates head width during the kick.
            // Preserve the approved pixels/aspect; enlarge the dash uniformly by 16%.
            if (id == "Shini" && state == "Dash") ppu /= 1.16f;
            if (state == "Walk") headWorldWidth = heads.Average(b => b.width) / ppu;
            // The sleep quilt is wider than an upright body; its head retains the same scale.
            var sprites = new SpriteMetaData[bounds.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                Rect b = bounds[i], cell = cells[i];
                float centerX = state == "Dash" ? heads[i].center.x : b.center.x;
                sprites[i] = new SpriteMetaData { name = id + "_" + state + "_" + i.ToString("00"), rect = cell,
                    alignment = 9, pivot = new Vector2(centerX / cell.width, (b.yMin + ppu * .27f) / cell.height) };
            }
            importer.spriteImportMode = SpriteImportMode.Multiple; importer.spritePixelsPerUnit = ppu;
            importer.spritesheet = sprites; importer.isReadable = false; importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        }
        static Rect Bounds(Color[] pixels, int width, int height, Func<Color, bool> include, float minimumY = 0)
        {
            int minX = width, maxX = -1, minY = height, maxY = -1;
            for (int y = Mathf.CeilToInt(height * minimumY); y < height; y++) for (int x = 0; x < width; x++)
                if (include(pixels[y * width + x])) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
            if (maxX < 0) throw new Exception("Character frame has no visible pixels");
            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
