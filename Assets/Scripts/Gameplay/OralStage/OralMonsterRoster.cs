using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    /// <summary>Runtime copies only: the original stage-one roster and prefabs are never edited.</summary>
    public static class OralMonsterRoster
    {
        static readonly string[] Names = { "줄무늬 사탕", "구미 곰", "말랑 찹쌀떡", "초코 샌드", "쿠키 조각", "면발 뭉치" };
        static readonly float[] Health = { 6, 9, 11, 15, 18, 22 };
        static readonly float[] Speeds = { .68f, .72f, .63f, .67f, .76f, .80f };
        static readonly float[] Heights = { .34f, .38f, .34f, .37f, .36f, .40f };
        static readonly Dictionary<MonsterBlueprint, int> kinds = new Dictionary<MonsterBlueprint, int>();
        static readonly Dictionary<MonsterBlueprint, OralFoodSkin[]> skins = new Dictionary<MonsterBlueprint, OralFoodSkin[]>();
        static readonly Dictionary<MonsterBlueprint,MonsterBlueprint> replacements=new Dictionary<MonsterBlueprint,MonsterBlueprint>();
        public static MonsterBlueprint Resolve(MonsterBlueprint source) => source!=null&&replacements.TryGetValue(source,out var value)?value:source;
        static readonly Dictionary<LevelBlueprint, List<UnityEngine.Object>> owned = new Dictionary<LevelBlueprint, List<UnityEngine.Object>>();
        static readonly Dictionary<LevelBlueprint, Dictionary<int, int>> eliteIndices = new Dictionary<LevelBlueprint, Dictionary<int, int>>();
        static readonly Dictionary<LevelBlueprint, Dictionary<int, int>> sourceEliteIndices = new Dictionary<LevelBlueprint, Dictionary<int, int>>();

        public static bool IsBlueprint(MonsterBlueprint blueprint) => blueprint != null && kinds.ContainsKey(blueprint);
        public static bool IsLevel(LevelBlueprint level) => level != null && owned.ContainsKey(level);
        public static bool IsEliteBlueprint(MonsterBlueprint blueprint) => blueprint is EliteMonsterBlueprint elite && IsBlueprint(elite.sourceNormalBlueprint);
        public static bool TryEliteIndex(LevelBlueprint level, int normalIndex, out int eliteIndex)
        {
            eliteIndex = -1;
            return level != null && eliteIndices.TryGetValue(level, out var indices) && indices.TryGetValue(normalIndex, out eliteIndex);
        }
        public static bool TryRemapSourceEliteIndex(LevelBlueprint level, int sourceIndex, out int eliteIndex)
        {
            eliteIndex = -1;
            return level != null && sourceEliteIndices.TryGetValue(level, out var indices) && indices.TryGetValue(sourceIndex, out eliteIndex);
        }

        public static LevelBlueprint Create(LevelBlueprint source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var atlas = Resources.Load<Texture2D>("OralMap/Monsters");
            if (atlas == null) throw new InvalidOperationException("Stage 0 food monster atlas is missing: Resources/OralMap/Monsters");
            var result = UnityEngine.Object.Instantiate(source);
            result.name = "Stage 0 · 구강 · 음식 몬스터 6종";
            var objects = new List<UnityEngine.Object>(); owned.Add(result, objects);
            var flavors = GStarDemo.Enabled ? Resources.Load<Texture2D>("OralMap/MonsterFlavors") : null;
            var originals=source.monsters.SelectMany(g=>g.monsterBlueprints).Where(b=>b!=null&&b.GetType()==typeof(MeleeMonsterBlueprint)).ToArray();
            var normals=new List<MeleeMonsterBlueprint>();
            result.monsters = new LevelBlueprint.MonstersContainer[source.monsters.Length];
            var flatIndices = new List<int>();
            int flat = 0, kind = 0;
            bool adapted = false;
            for (int i = 0; i < source.monsters.Length; i++)
            {
                var original = source.monsters[i];
                var group = new LevelBlueprint.MonstersContainer { monstersPrefab = original.monstersPrefab,
                    monsterBlueprints = (MonsterBlueprint[])original.monsterBlueprints.Clone() };
                result.monsters[i] = group;
                // SugarCubeMonster and other subclasses have their own animation and
                // death/splitting behavior. All six food species use an ordinary pool.
                var component = original.monstersPrefab != null ? original.monstersPrefab.GetComponent<Monster>() : null;
                MeleeMonsterBlueprint template = null;
                if (!adapted && component != null && component.GetType() == typeof(MeleeMonster))
                    foreach (var candidate in original.monsterBlueprints)
                        if (candidate != null && candidate.GetType() == typeof(MeleeMonsterBlueprint))
                        { template = (MeleeMonsterBlueprint)candidate; break; }
                if (template == null) { flat += group.monsterBlueprints.Length; continue; }
                adapted = true;
                group.monsterBlueprints = new MonsterBlueprint[6];
                for (int j = 0; j < 6; j++, flat++)
                {
                    var copy = UnityEngine.Object.Instantiate(template);
                    var sprite = CreateBody(atlas, kind); objects.Add(copy); objects.Add(sprite);
                    copy.name = Names[kind]; copy.description = "구강에 들어온 음식 조각. 입안의 환경 이벤트를 이용해 처치할 수 있습니다.";
                    copy.hp = originals[Mathf.Min(kind,originals.Length-1)].hp*.9f; copy.atk = 4 + kind; copy.armor = 0; copy.recovery = 0;
                    copy.movespeed = Speeds[kind]; copy.acceleration = 4f;
                    copy.walkSpriteSequence = new[] { sprite }; copy.walkFrameTime = .14f; copy.resultSprite = sprite;
                    copy.useGroundShadowFootprint = true; copy.groundShadowCenterUV = new Vector2(.5f, .06f);
                    copy.groundShadowSizeUV = new Vector2(.68f, .13f); copy.preserveGroundShadowHeight = false;
                    copy.gemLootTable = new LootTable<GemType> { lootTable = new[] { new Loot<GemType> { item = GemType.White1, dropChance = 1 } } };
                    copy.coinLootTable = new LootTable<CoinType> { lootTable = new[] { new Loot<CoinType> { item = CoinType.Bronze1, dropChance = .12f } } };
                    kinds.Add(copy, kind);
                    if (GStarDemo.Enabled) skins[copy] = OralFoodSkin.Build(sprite, flavors, kind, objects);
                    group.monsterBlueprints[j] = copy; flatIndices.Add(flat); kind++;
                    normals.Add(copy);
                }
            }
            if (kind != 6) { Release(result); throw new InvalidOperationException("Oral roster requires an ordinary MeleeMonster pool and MeleeMonsterBlueprint"); }
            int eliteKind=0;
            foreach(var group in result.monsters)for(int j=0;j<group.monsterBlueprints.Length;j++) {
                if(!(group.monsterBlueprints[j] is EliteMonsterBlueprint original))continue;
                int index=eliteKind++%normals.Count;var normal=normals[index];
                var elite=UnityEngine.Object.Instantiate(original);objects.Add(elite);
                elite.name=normal.name+" · 엘리트";elite.sourceNormalBlueprint=normal;
                elite.useSourceVisual=true;elite.useSourceCombatStats=false;elite.useSourceLootTables=false;
                elite.hp=normal.hp;elite.hpMultiplier=4.5f;elite.scaleMultiplier=2;
                elite.atk=normal.atk*1.45f;elite.movespeed=normal.movespeed*1.06f;
                elite.resultSprite=normal.resultSprite;elite.walkSpriteSequence=normal.walkSpriteSequence;
                group.monsterBlueprints[j]=elite;replacements[original]=elite;
            }
            var keys = new List<MonsterSpawnTable.SpawnChanceKeyframe>();
            for (int tier = 0; tier < 3; tier++)
            {
                var weights = new float[flat];
                for (int i = 0; i < (tier + 1) * 2; i++) weights[flatIndices[i]] = i >= tier * 2 ? 3 : 1;
                if (tier > 0) keys.Add(new MonsterSpawnTable.SpawnChanceKeyframe { t = tier / 3f, spawnChances = (float[])keys[keys.Count - 1].spawnChances.Clone() });
                keys.Add(new MonsterSpawnTable.SpawnChanceKeyframe { t = tier == 0 ? 0 : tier / 3f + .035f, spawnChances = weights });
            }
            keys.Add(new MonsterSpawnTable.SpawnChanceKeyframe { t = 1, spawnChances = (float[])keys[keys.Count - 1].spawnChances.Clone() });
            result.normalSpawnPopulationLimit = 96;
            result.monsterSpawnTable = new MonsterSpawnTable
            {
                spawnChanceKeyframes = keys.ToArray(),
                spawnRateKeyframes = new[]
                {
                    new MonsterSpawnTable.SpawnRateKeyframe { t = 0, spawnRate = 1.4f },
                    new MonsterSpawnTable.SpawnRateKeyframe { t = .33f, spawnRate = 2.3f },
                    new MonsterSpawnTable.SpawnRateKeyframe { t = .67f, spawnRate = 3f },
                    new MonsterSpawnTable.SpawnRateKeyframe { t = 1, spawnRate = 3.5f }
                }
            };
            // Preserve stage-one special species and their actual introduction times.
            // The six oral normals replace only the ordinary portion of each wave.
            var originalFlat=source.monsters.SelectMany(g=>g.monsterBlueprints).ToArray();
            var resultFlat=result.monsters.SelectMany(g=>g.monsterBlueprints).ToArray();
            var byNormal=new Dictionary<int,int>();
            var bySource=new Dictionary<int,int>();
            for(int i=0;i<resultFlat.Length;i++)
            {
                if(!(resultFlat[i] is EliteMonsterBlueprint elite) || !IsBlueprint(elite.sourceNormalBlueprint))continue;
                int normalIndex=Array.IndexOf(resultFlat,elite.sourceNormalBlueprint);
                if(normalIndex>=0 && !byNormal.ContainsKey(normalIndex))byNormal.Add(normalIndex,i);
            }
            for(int i=0;i<originalFlat.Length;i++)
                if(originalFlat[i] is EliteMonsterBlueprint && replacements.TryGetValue(originalFlat[i],out var elite))
                {
                    int index=Array.IndexOf(resultFlat,elite);
                    if(index>=0)bySource.Add(i,index);
                }
            if(flatIndices.Any(index=>!byNormal.ContainsKey(index)))
            { Release(result); throw new InvalidOperationException("Every oral normal must have a matching oral elite"); }
            eliteIndices.Add(result,byNormal);
            sourceEliteIndices.Add(result,bySource);
            var specialPairs=new List<KeyValuePair<int,int>>();int resultIndex=0;
            foreach(var group in result.monsters)foreach(var bp in group.monsterBlueprints) {
                var actor=group.monstersPrefab.GetComponent<Monster>();
                int sourceIndex=Array.IndexOf(originalFlat,bp);
                if(sourceIndex>=0&&bp!=null&&bp.GetType()!=typeof(MeleeMonsterBlueprint)&&
                    !(bp is EliteMonsterBlueprint)&&!(bp is BossMonsterBlueprint)&&!(bp is MiniBossMonsterBlueprint)&&
                    !(actor is BloodClotMonster)&&!(actor is BossMonster)&&!(actor is MiniBossMonster))
                    specialPairs.Add(new KeyValuePair<int,int>(sourceIndex,resultIndex));
                resultIndex++;
            }
            var oralTable=result.monsterSpawnTable;
            float sourceDuration=Mathf.Max(1,source.levelTime);
            var times=keys.Select(k=>k.t).Concat(source.monsterSpawnTable.spawnChanceKeyframes.Select(k=>k.t*sourceDuration/OralSpecialSpawnProfile.Duration)).Where(t=>t>=0&&t<=1).Distinct().OrderBy(t=>t);
            result.monsterSpawnTable.spawnChanceKeyframes=times.Select(t=> {
                var weights=new float[flat];float specialTotal=0;
                foreach(var pair in specialPairs){float probability=source.monsterSpawnTable.GetProbability(t*OralSpecialSpawnProfile.Duration/sourceDuration,pair.Key);weights[pair.Value]=probability;specialTotal+=probability;}
                foreach(int index in flatIndices)weights[index]=oralTable.GetProbability(t,index)*Mathf.Max(0,1-specialTotal);
                return new MonsterSpawnTable.SpawnChanceKeyframe{t=t,spawnChances=weights};
            }).ToArray();
            return result;
        }

        // The alpha-trimmed source rectangles preserve foot pivots without reading back
        // a 6 MB texture or scanning 1.6 million pixels at every scene entry.
        static readonly RectInt[] FoodRects = {
            new RectInt(47,557,465,301), new RectInt(596,556,345,380), new RectInt(1051,556,422,329),
            new RectInt(89,100,364,359), new RectInt(569,99,374,360), new RectInt(1059,96,398,363)
        };
        public static Sprite CreateBody(Texture2D atlas, int kind)
        {
            var source = FoodRects[kind];
            var rect = OralEventArt.ImportedRect(atlas,1536,1024,new Rect(source.x,source.y,source.width,source.height));
            var sprite = Sprite.Create(atlas, rect, new Vector2(.5f, 0), rect.height / Heights[kind], 0, SpriteMeshType.FullRect);
            sprite.name = "OralFood" + kind;
            return sprite;
        }

        public static void ApplySpawn(Monster monster)
        {
            if (monster == null || monster.Blueprint == null || !kinds.TryGetValue(monster.Blueprint, out var index)) return;
            skins.TryGetValue(monster.Blueprint, out var variants);
            (monster.GetComponent<OralMonsterMotion>() ?? monster.gameObject.AddComponent<OralMonsterMotion>()).Configure(monster, index, variants);
        }

        public static void Release(LevelBlueprint level)
        {
            if (level == null || !owned.TryGetValue(level, out var objects)) return;
            foreach (var value in objects)
            {
                if (value is MonsterBlueprint bp) { kinds.Remove(bp); skins.Remove(bp); }
                if (value != null) UnityEngine.Object.Destroy(value);
            }
            owned.Remove(level); UnityEngine.Object.Destroy(level);
            eliteIndices.Remove(level); sourceEliteIndices.Remove(level);
            replacements.Clear();
        }
    }

}
