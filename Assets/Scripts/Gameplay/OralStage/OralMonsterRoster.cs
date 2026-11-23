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
        // The ordinary oral monster health curve applies a further .70 HP modifier on spawn.
        static readonly float[] HealthBySpecies = { 23, 34, 66, 99, 132, 167 };
        static readonly float[] SpeedBySpecies = { .69f, .79f, .66f, .78f, .92f, .85f };
        static readonly float[] Heights = { .34f, .38f, .34f, .37f, .36f, .40f };
        static readonly Dictionary<MonsterBlueprint, int> kinds = new Dictionary<MonsterBlueprint, int>();
        static readonly Dictionary<MonsterBlueprint, OralFoodSkin[]> skins = new Dictionary<MonsterBlueprint, OralFoodSkin[]>();
        static readonly Dictionary<MonsterBlueprint,MonsterBlueprint> replacements=new Dictionary<MonsterBlueprint,MonsterBlueprint>();
        public static MonsterBlueprint Resolve(MonsterBlueprint source) => source!=null&&replacements.TryGetValue(source,out var value)?value:source;
        static readonly Dictionary<LevelBlueprint, List<UnityEngine.Object>> owned = new Dictionary<LevelBlueprint, List<UnityEngine.Object>>();
        static readonly Dictionary<LevelBlueprint, Dictionary<int, int>> eliteIndices = new Dictionary<LevelBlueprint, Dictionary<int, int>>();
        static readonly Dictionary<LevelBlueprint, Dictionary<int, int>> sourceEliteIndices = new Dictionary<LevelBlueprint, Dictionary<int, int>>();

        static readonly LootTable<GemType>[] oralGemTables = {
            GemTable(0, 0), GemTable(.15f, 0), GemTable(.29f, .01f), GemTable(.37f, .02f)
        };

        static LootTable<GemType> GemTable(float blue, float green) => new LootTable<GemType>
        {
            lootTable = new[] {
                new Loot<GemType> { item = GemType.Green10, dropChance = green },
                new Loot<GemType> { item = GemType.Blue2, dropChance = blue },
                new Loot<GemType> { item = GemType.White1, dropChance = 1f - green - blue }
            }
        };

        public static LootTable<GemType> ExperienceTable(MonsterBlueprint loot, Monster actor)
        {
            var runtime = OralStageRuntime.Instance;
            if (runtime == null || runtime.Level == null || !GStarFieldBalance.IsOrdinary(actor) || !IsBlueprint(actor.Blueprint)) return loot.gemLootTable;
            float t = runtime.Level.CurrentLevelTime;
            return oralGemTables[t < 180f ? 0 : t < 300f ? 1 : t < 420f ? 2 : 3];
        }

        static readonly float[] UnlockAt = { 0, 45, 130, 225, 315, 400 };
        static readonly HashSet<MonsterBlueprint> shownBase = new HashSet<MonsterBlueprint>();
        static readonly float[] TierHealth = { 1f, 1.20f, 1.50f };
        static readonly float[] TierSpeed = { 1f, 1.045f, 1.09f };
        static readonly float[] TierAttack = { 1f, 1.10f, 1.22f };

        // The two ramps let the player learn a newly introduced silhouette in its base palette
        // even if its species first appears well into the run.
        internal static int ChoosePalette(Monster actor, int kind, int available)
        {
            if (actor.OralPaletteTier >= 0) return actor.OralPaletteTier;
            var runtime = OralStageRuntime.Instance;
            if (!GStarFieldBalance.IsOrdinary(actor) || runtime?.Level == null || available < 1) return 0;
            float elapsed = runtime.Level.CurrentLevelTime;
            float age = elapsed - UnlockAt[kind];
            float mid = .34f * Mathf.InverseLerp(75, 310, elapsed) * Mathf.InverseLerp(35, 155, age);
            float high = .16f * Mathf.InverseLerp(210, 470, elapsed) * Mathf.InverseLerp(90, 190, age);
            float roll = UnityEngine.Random.value;
            int tier = !shownBase.Add(actor.Blueprint) && age >= 35
                ? (roll < high ? 2 : roll < high + mid ? 1 : 0) : 0;
            tier = Mathf.Min(tier, available - 1);
            actor.ApplyOralPalette(tier, TierHealth[tier], TierSpeed[tier], TierAttack[tier]);
            return tier;
        }

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
            var flavors = Resources.Load<Texture2D>("OralMap/MonsterFlavors");
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
                    copy.hp = HealthBySpecies[kind]; copy.atk = 4 + kind; copy.armor = 0; copy.recovery = 0;
                    copy.movespeed = SpeedBySpecies[kind]; copy.acceleration = 4f;
                    copy.walkSpriteSequence = new[] { sprite }; copy.walkFrameTime = .14f; copy.resultSprite = sprite;
                    copy.useGroundShadowFootprint = true; copy.groundShadowCenterUV = new Vector2(.5f, .06f);
                    copy.groundShadowSizeUV = new Vector2(.68f, .13f); copy.preserveGroundShadowHeight = false;
                    copy.gemLootTable = new LootTable<GemType> { lootTable = new[] { new Loot<GemType> { item = GemType.White1, dropChance = 1 } } };
                    copy.coinLootTable = new LootTable<CoinType> { lootTable = new[] { new Loot<CoinType> { item = CoinType.Bronze1, dropChance = .12f } } };
                    kinds.Add(copy, kind);
                    skins[copy] = OralFoodSkin.Build(sprite, flavors, kind, objects);
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
            void Mix(float seconds, params float[] food)
            {
                var w = new float[flat];
                for (int i=0;i<food.Length;i++) w[flatIndices[i]]=food[i]*.01f;
                keys.Add(new MonsterSpawnTable.SpawnChanceKeyframe { t=seconds/OralSpecialSpawnProfile.Duration, spawnChances=w });
            }
            Mix(0, 100); Mix(45, 100);
            Mix(75, 62,38); Mix(130, 50,50);
            Mix(160, 30,36,34); Mix(225, 25,35,40);
            Mix(250, 18,25,35,22); Mix(315, 15,22,31,32);
            Mix(335, 10,18,26,32,14); Mix(400, 8,14,24,30,24);
            Mix(420, 7,13,20,27,25,8); Mix(480, 5,10,18,25,25,17);
            Mix(555, 4,8,13,23,28,24); Mix(600, 4,8,13,23,28,24);
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
                if (value is MonsterBlueprint bp) { kinds.Remove(bp); skins.Remove(bp); shownBase.Remove(bp); }
                if (value != null) UnityEngine.Object.Destroy(value);
            }
            owned.Remove(level); UnityEngine.Object.Destroy(level);
            eliteIndices.Remove(level); sourceEliteIndices.Remove(level);
            replacements.Clear();
        }
    }

}
