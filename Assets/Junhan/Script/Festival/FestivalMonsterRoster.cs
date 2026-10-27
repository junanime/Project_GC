using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire
{
    [Serializable]
    public sealed class FestivalMonsterStats
    {
        public string blueprint;
        public float speed, damage;
    }

    // Per-run copies keep festival tuning out of shared assets and the original game.
    public sealed class FestivalMonsterRoster : IDisposable
    {
        readonly Dictionary<MonsterBlueprint, MonsterBlueprint> copies = new();
        readonly int[] normalIndices = new int[9];
        public static int Phase(float seconds) => Mathf.Clamp(Mathf.FloorToInt(seconds / 12f), 0, 2);

        public FestivalMonsterRoster(LevelBlueprint level)
        {
            var rules = FestivalDemo.Settings;
            if (rules.normalMonsters == null || rules.normalMonsters.Length != 9 ||
                rules.normalMonsters.Select(s => s.blueprint).Distinct().Count() != 9)
                throw new InvalidOperationException("Festival requires nine unique normal species.");
            for (int i = 0; i < normalIndices.Length; i++)
            {
                var spec = rules.normalMonsters[i];
                int found = -1, index = 0;
                MonsterBlueprint source = null;
                foreach (var pool in level.monsters)
                foreach (var b in pool.monsterBlueprints)
                {
                    if (b != null && ((Object)b).name == spec.blueprint) { found = index; source = b; }
                    index++;
                }
                if (found < 0 || source is not MeleeMonsterBlueprint || source is EliteMonsterBlueprint)
                    throw new InvalidOperationException("Festival normal species missing: " + spec.blueprint);
                normalIndices[i] = found;
                var copy = Object.Instantiate(source);
                ((Object)copy).name = ((Object)source).name;
                copy.hp = rules.normalBaseHealth * Mathf.Pow(1.25f, i / 3);
                copy.movespeed = spec.speed;
                copy.atk = spec.damage;
                if (copy is SugarCubeMonsterBlueprint sugar)
                {
                    // The existing three-piece body/24 walk frames, without any children.
                    sugar.splitChildBlueprint = null;
                    sugar.splitChildCount = 0;
                }
                copies.Add(source, copy);
            }
        }

        public int Select(float seconds, float sample) =>
            normalIndices[Phase(seconds) * 3 + Mathf.Min(2, Mathf.FloorToInt(Mathf.Clamp01(sample) * 3))];
        public MonsterBlueprint Resolve(MonsterBlueprint source) =>
            source != null && copies.TryGetValue(source, out var copy) ? copy : source;
        public void Dispose()
        {
            foreach (var copy in copies.Values) Object.Destroy(copy);
            copies.Clear();
        }
    }
}
