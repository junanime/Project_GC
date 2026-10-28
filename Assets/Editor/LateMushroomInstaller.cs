using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Editor
{
    // Approved eight-frame walk, appended pools preserve all serialized special/elite indices.
    public static class LateMushroomInstaller
    {
        public const string Folder = "Assets/Blueprints/Monsters/LateMushrooms";
        public const string ProfilePath = "Assets/Junhan/Script/Spawn/Level1_15Min_SpawnBalanceProfile.asset";
        public const string LevelPath = "Assets/Blueprints/Levels/Level 1.asset";
        static readonly float[] Minutes = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 13.166667f, 14, 14.25f, 14.333333f, 14.5f, 14.75f, 15 };

        [MenuItem("Tools/24tu/Install approved late mushrooms")]
        public static void Install()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var level = AssetDatabase.LoadAssetAtPath<LevelBlueprint>(LevelPath);
            var profile = AssetDatabase.LoadAssetAtPath<LevelSpawnBalanceProfile>(ProfilePath);
            var geometry = AssetDatabase.LoadAssetAtPath<VisibleBodyGeometry>("Assets/Resources/VisibleBodyGeometry.asset");
            var bounds = geometry.entries.ToList();
            var blueprints = new List<MonsterBlueprint>();
            string[] ids = { "king-oyster", "red-cap" };
            string[] names = { "새송이 대장", "붉은갓 대장" };
            for (int kind = 0; kind < ids.Length; kind++)
            {
                var frames = new List<Sprite>();
                foreach (var path in Directory.GetFiles("Assets/Art/LateMushrooms/" + ids[kind], "*.png").OrderBy(p => p))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 300;
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.maxTextureSize = 256;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(.5f, 36f / 256f);
                    importer.SetTextureSettings(settings); importer.SaveAndReimport();
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    frames.Add(sprite); BakeBounds(sprite, path, bounds);
                }
                string assetPath = Folder + "/" + ids[kind] + ".asset";
                var bp = AssetDatabase.LoadAssetAtPath<MeleeMonsterBlueprint>(assetPath);
                if (bp == null)
                {
                    bp = Object.Instantiate((MeleeMonsterBlueprint)level.monsters[6].monsterBlueprints[0]);
                    AssetDatabase.CreateAsset(bp, assetPath);
                }
                ((Object)bp).name = ids[kind]; bp.name = names[kind];
                bp.hp = kind == 0 ? 220 : 260; bp.atk = kind == 0 ? 13 : 14;
                bp.movespeed = kind == 0 ? .88f : .82f; bp.acceleration = 5;
                bp.armor = 0; bp.recovery = 0; bp.atkspeed = 2;
                bp.walkSpriteSequence = frames.ToArray(); bp.walkFrameTime = .12f; bp.resultSprite = frames[0];
                bp.description = kind == 0 ? "14분 이후 등장하는 새송이. 갓을 좌우로 흔들며 다가온다." : "14분 30초 이후 등장하는 붉은갓. 느리지만 튼튼한 후반 일반 몬스터.";
                bp.useGroundShadowFootprint = true; bp.preserveGroundShadowHeight = false;
                bp.groundShadowCenterUV = new Vector2(.5f, 38f / 256f);
                bp.groundShadowSizeUV = new Vector2(kind == 0 ? .43f : .46f, .07f);
                bp.gemLootTable = new LootTable<GemType> { lootTable = kind == 0
                    ? new[] { new Loot<GemType> { item = GemType.Green10, dropChance = 1 } }
                    : new[] { new Loot<GemType> { item = GemType.Green10, dropChance = .9f }, new Loot<GemType> { item = GemType.Red50, dropChance = .1f } } };
                bp.coinLootTable = new LootTable<CoinType> { lootTable = kind == 0
                    ? new[] { new Loot<CoinType> { item = CoinType.Silver2, dropChance = .1f }, new Loot<CoinType> { item = CoinType.Gold5, dropChance = .9f } }
                    : new[] { new Loot<CoinType> { item = CoinType.Gold5, dropChance = 1 } } };
                EditorUtility.SetDirty(bp); blueprints.Add(bp);
            }
            geometry.entries = bounds.ToArray(); EditorUtility.SetDirty(geometry);
            if (!level.monsters.Any(p => p.monsterBlueprints.Contains(blueprints[0])))
                level.monsters = level.monsters.Concat(new[] { new LevelBlueprint.MonstersContainer
                { monstersPrefab = level.monsters[0].monstersPrefab, monsterBlueprints = blueprints.ToArray() } }).ToArray();

            // Preserve the old mixture exactly until 14:00. Replace fractions, never add extra volume.
            if (!profile.normalMonsterEntries.Any(e => e.monsterBlueprint == blueprints[0]))
            {
                var old = profile.normalMonsterEntries.ToArray();
                var curves = old.Select(e => e.spawnWeightByMinute).ToArray();
                for (int i = 0; i < old.Length; i++)
                {
                    int index = i;
                    old[i].spawnWeightByMinute = Curve(m =>
                    {
                        float total = curves.Sum(c => Mathf.Max(0, c.Evaluate(m)));
                        return total <= 0 ? 0 : Mathf.Max(0, curves[index].Evaluate(m)) / total * (1 - King(m) - Red(m));
                    });
                }
                profile.normalMonsterEntries.Add(new LevelSpawnBalanceProfile.MonsterSpawnEntry
                { memo = "후반 7: 새송이 대장 / 14:00 이후", monsterBlueprint = blueprints[0], spawnWeightByMinute = Curve(King) });
                profile.normalMonsterEntries.Add(new LevelSpawnBalanceProfile.MonsterSpawnEntry
                { memo = "후반 8: 붉은갓 대장 / 14:30 이후", monsterBlueprint = blueprints[1], spawnWeightByMinute = Curve(Red) });
                profile.chanceSampleMinutes = Minutes;
            }
            profile.ApplyTo(level); EditorUtility.SetDirty(profile); EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            Debug.Log("[LateMushrooms] Installed approved walk, late-stage blend, shared melee/status/barrier pipeline.");
        }
        static float King(float m) => m <= 14 ? 0 : m <= 14.25f ? Mathf.Lerp(0,.3f,(m-14)*4) :
            m <= 14.5f ? Mathf.Lerp(.3f,.4f,(m-14.25f)*4) : Mathf.Lerp(.4f,.35f,Mathf.Clamp01((m-14.5f)*4));
        static float Red(float m) => m <= 14.5f ? 0 : m <= 14.75f ? Mathf.Lerp(0,.35f,(m-14.5f)*4) : Mathf.Lerp(.35f,.45f,Mathf.Clamp01((m-14.75f)*4));
        static AnimationCurve Curve(Func<float,float> value)
        {
            var curve = new AnimationCurve(Minutes.Select(m => new Keyframe(m,value(m))).ToArray());
            for(int i=0;i<curve.length;i++)
            { AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear); }
            return curve;
        }
        static void BakeBounds(Sprite sprite, string path, List<VisibleBodyGeometry.Entry> entries)
        {
            var texture = new Texture2D(2,2); texture.LoadImage(File.ReadAllBytes(path));
            var pixels = texture.GetPixels32(); var points = new List<Vector2>();
            for(int y=0;y<texture.height;y++)
            {
                int left=texture.width,right=-1;
                for(int x=0;x<texture.width;x++) if(pixels[y*texture.width+x].a>128) {left=Mathf.Min(left,x);right=Mathf.Max(right,x);}
                if(right<0)continue;
                points.Add((new Vector2(left,y)-sprite.pivot)/sprite.pixelsPerUnit);
                points.Add((new Vector2(right+1,y+1)-sprite.pivot)/sprite.pixelsPerUnit);
            }
            Object.DestroyImmediate(texture); entries.RemoveAll(e=>e.sprite==sprite);
            entries.Add(new VisibleBodyGeometry.Entry { sprite=sprite, outline=DiscoveryArtInstaller.Hull(points),
                bounds=Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y)) });
        }
    }
}
