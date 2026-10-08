using System;
using UnityEngine;

namespace Vampire
{
    /// <summary>One entry snapshot. Later gains never increase the same enemy's HP.</summary>
    public static class StageTwoBalance
    {
        public static float EntryHealth(float lateMean, float bossDps, float rewardGrowth)
        {
            lateMean = Mathf.Max(1, lateMean);
            if (float.IsNaN(bossDps) || float.IsInfinity(bossDps)) bossDps = 0;
            return Mathf.Max(lateMean * 1.15f, Mathf.Clamp(bossDps * .85f, lateMean, lateMean * 2.5f)) *
                Mathf.Clamp(rewardGrowth, .8f, 1.5f);
        }

        public static LevelBlueprint Build(LevelBlueprint source, StageTwoDefinition art, float bossDps,
            float rewardGrowth, out float entryHealth)
        {
            float sum = 0, weight = 0, low = float.MaxValue, high = 0;
            MonsterBlueprint donor = null;
            foreach (var pair in source.MonsterIndexMap)
            {
                var bp = source.monsters[pair.Value.Item1].monsterBlueprints[pair.Value.Item2];
                if (bp == null || bp.hp <= 0) continue;
                float p = source.monsterSpawnTable.GetProbability(.95f, pair.Key);
                if (p <= 0) continue;
                sum += bp.hp * p; weight += p; low = Mathf.Min(low, bp.hp); high = Mathf.Max(high, bp.hp);
                if (donor == null || bp.hp > donor.hp) donor = bp;
            }
            if (donor == null) throw new InvalidOperationException("Stage 1 has no late normal enemy table.");
            entryHealth = EntryHealth(sum / Mathf.Max(.001f, weight), bossDps, rewardGrowth);
            var stage = ScriptableObject.CreateInstance<LevelBlueprint>();
            stage.name = "Stage 2 · Whipped cream (run)";
            stage.levelTime = 900; stage.normalSpawnPopulationLimit = source.normalSpawnPopulationLimit;
            stage.backgroundTexture = art.cream; stage.abilityPrefabs = source.abilityPrefabs;
            stage.miniBosses = source.miniBosses; stage.finalBoss = source.finalBoss;
            stage.chestBlueprint = source.chestBlueprint; stage.chestSpawnDelay = source.chestSpawnDelay;
            stage.chestSpawnAmount = source.chestSpawnAmount;
            stage.monsters = new LevelBlueprint.MonstersContainer[source.monsters.Length + 1];
            Array.Copy(source.monsters, stage.monsters, source.monsters.Length);
            var snails = new MonsterBlueprint[9];
            float spread = Mathf.Clamp(high / Mathf.Max(1, low), 2.4f, 3.8f);
            for (int i = 0; i < snails.Length; i++)
            {
                var bp = ScriptableObject.CreateInstance<FoodSnailBlueprint>();
                bp.name = art.names[i] + " 달팽이";
                bp.hp = Mathf.Ceil(entryHealth * .8f * Mathf.Pow(spread, i / 8f));
                bp.atk = donor.atk; bp.atkspeed = Mathf.Max(.5f, donor.atkspeed);
                bp.movespeed = Mathf.Clamp(donor.movespeed, .65f, 1.35f);
                bp.acceleration = Mathf.Max(3, donor.acceleration);
                bp.gemLootTable = donor.gemLootTable; bp.coinLootTable = donor.coinLootTable;
                bp.walkSpriteSequence = new[] { art.body }; bp.walkFrameTime = .15f;
                bp.shell = art.shells[i]; bp.resultSprite = art.shells[i];
                bp.description = "휘핑크림 지대의 " + art.names[i] + " 껍질 달팽이";
                bp.meleeLayer = LayerMask.GetMask("Player Full", "Player Legs");
                snails[i] = bp;
            }
            stage.monsters[source.monsters.Length] = new LevelBlueprint.MonstersContainer
                { monstersPrefab = art.snailPrefab, monsterBlueprints = snails };
            int offset = source.MonsterIndexMap.Count;
            var chances = new MonsterSpawnTable.SpawnChanceKeyframe[5];
            for (int k = 0; k < chances.Length; k++)
            {
                float[] values = new float[offset + 9];
                // The centre of the population shifts from tier 1 to tier 8 over 15 minutes.
                float centre = .5f + k * 1.75f;
                for (int i = 0; i < 9; i++)
                    values[offset + i] = Mathf.Max(0, 2.3f - Mathf.Abs(i - centre));
                chances[k] = new MonsterSpawnTable.SpawnChanceKeyframe { t = k / 4f, spawnChances = values };
            }
            stage.monsterSpawnTable = new MonsterSpawnTable {
                spawnChanceKeyframes = chances,
                spawnRateKeyframes = new[] {
                    new MonsterSpawnTable.SpawnRateKeyframe { t = 0, spawnRate = Mathf.Max(1.2f, source.monsterSpawnTable.GetSpawnRate(.5f)) },
                    new MonsterSpawnTable.SpawnRateKeyframe { t = .5f, spawnRate = source.monsterSpawnTable.GetSpawnRate(.8f) },
                    new MonsterSpawnTable.SpawnRateKeyframe { t = 1, spawnRate = source.monsterSpawnTable.GetSpawnRate(1) }
                }
            };
            return stage;
        }
    }
}
