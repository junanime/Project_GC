using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Weapon = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;
using Noble = Vampire.SyringeLegendaryAugmentAbility.LegendaryAugmentType;

namespace Vampire
{
    [Serializable]
    public sealed class FestivalPreset
    {
        public string character, description, relic, noble;
        public int[] weapons, items;
        public float damageBonus, attackSpeedBonus, healthBonus;
    }

    [Serializable]
    public sealed class FestivalSettings
    {
        public bool enabled;
        public float duration = 45, minimumLoadingSeconds = 3, eventSeconds = 20;
        public float startingSpawnRate = 10, finalSpawnRate = 18;
        public float startingHealthMultiplier = 1.25f, finalHealthMultiplier = 2.4f;
        public int initialEnemies = 18, populationLimit = 95, spawnBudgetPerFrame = 6;
        public float experienceGemMultiplier = 0;
        public float ariActiveCooldownMultiplier = .6f, hyukiActiveCooldownMultiplier = .6f, shiniActiveDamageMultiplier = 1.8f;
        public float startingMoveMultiplier = 1.05f, finalMoveMultiplier = 1.6f;
        public int specialPopulationLimit = 8;
        public float startingDamageMultiplier = 1, finalDamageMultiplier = 3, damageRampStart = 12;
        public float chestSpawnInterval = 15f;
        public int questKills = 20, questDashes = 4, questActiveUses = 2;
        public FestivalPreset[] presets;
    }

    // Branch-specific defaults live in Resources; normal gameplay never writes these settings.
    public static class FestivalDemo
    {
        public const string Rules = "festival_pc_v3_45";
        static FestivalSettings settings;
        public static bool? EnabledOverride { get; set; }
        public static FestivalSettings Settings
        {
            get
            {
                if (settings == null)
                {
                    var asset = Resources.Load<TextAsset>("FestivalDemoSettings");
                    settings = asset != null ? JsonUtility.FromJson<FestivalSettings>(asset.text) : new FestivalSettings();
                }
                return settings;
            }
        }
        public static bool Enabled => EnabledOverride ?? Settings.enabled;
        public static float ActiveCooldown(float original, CharacterSkillDefinition.SkillKind kind)
        {
            if (!Enabled) return original;
            float multiplier = kind == CharacterSkillDefinition.SkillKind.Ari ? Settings.ariActiveCooldownMultiplier
                : kind == CharacterSkillDefinition.SkillKind.Hyuki ? Settings.hyukiActiveCooldownMultiplier : 1;
            return Mathf.Min(24, original) * Mathf.Clamp(multiplier, .1f, 2);
        }
        public static float ShiniActiveDamageMultiplier => Enabled ? Mathf.Clamp(Settings.shiniActiveDamageMultiplier, .1f, 5) : 1;
        public static FestivalPreset Preset(CharacterBlueprint character) =>
            Settings.presets?.FirstOrDefault(p => p.character == OctoberArt.CharacterKey(character));
        public static ApothecaryUIConfig Catalog => Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
        public static bool Includes(Weapon type, CharacterBlueprint character) =>
            Preset(character)?.weapons?.Contains((int)type) == true;
        public static RelicBlueprint Relic(CharacterBlueprint character) =>
            Catalog?.relics.FirstOrDefault(r => r != null && r.relicId == Preset(character)?.relic);
        public static MerchantItemBlueprint[] Items(CharacterBlueprint character)
        {
            var ids = Preset(character)?.items ?? Array.Empty<int>();
            var catalog = Catalog;
            return ids.Select(id => catalog?.items.FirstOrDefault(i => i != null && i.octoberId == id)).Where(i => i != null).ToArray();
        }
        public static string Validate(CharacterBlueprint character)
        {
            var preset = Preset(character);
            if (preset == null || preset.weapons == null || preset.weapons.Length != 4 || preset.weapons.Distinct().Count() != 4)
                return "캐릭터의 시작 증강 4종을 확인해 주세요.";
            if (preset.weapons.Any(id => !StartingNeedleSelection.Available((Weapon)id) || Catalog.weapons.All(w => w == null || (int)w.Type != id)))
                return "시작 무기 정보를 불러오지 못했습니다.";
            if (Relic(character) == null || Items(character).Length != preset.items.Length)
                return "시작 유물·아이템 정보를 불러오지 못했습니다.";
            if (!Enum.TryParse<Noble>(preset.noble, out _)) return "고귀 증강 보상 정보를 확인해 주세요.";
            return null;
        }
        public static void ApplyWeapons(AbilityManager manager, CharacterBlueprint character)
        {
            var preset = Preset(character);
            if (manager == null || preset == null) return;
            var abilities = manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true);
            foreach (int id in preset.weapons)
            {
                var ability = abilities.FirstOrDefault(a => (int)a.Type == id);
                if (ability == null || !ability.Owned && !manager.AcquireVer4Ability(ability))
                    throw new InvalidOperationException("Festival starting augment unavailable: " + (Weapon)id);
            }
            foreach (int id in preset.weapons)
                manager.Ver4.GrantInitialOriginals(abilities.First(a => (int)a.Type == id));
        }
        public static bool GrantNoble(AbilityManager manager, CharacterBlueprint character, out string name)
        {
            name = null;
            if (manager == null || !Enum.TryParse<Noble>(Preset(character)?.noble, out var type)) return false;
            var ability = manager.GetComponentsInChildren<SyringeLegendaryAugmentAbility>(true).FirstOrDefault(a => a.Type == type);
            if (ability == null) return false;
            if (!ability.Owned && !manager.AcquireVer4Ability(ability)) return false;
            name = ability.Name;
            return true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { settings = null; EnabledOverride = null; }
    }

    public sealed class FestivalRunController : MonoBehaviour
    {
        public static FestivalRunController Active { get; private set; }
        public static float EnemyDamageMultiplier
        {
            get
            {
                if (!FestivalDemo.Enabled || Active == null || Active.level == null) return 1;
                var rules=FestivalDemo.Settings;
                return Mathf.SmoothStep(rules.startingDamageMultiplier,rules.finalDamageMultiplier,
                    Mathf.InverseLerp(rules.damageRampStart,rules.duration,Active.level.CurrentLevelTime));
            }
        }
        void OnDestroy() { if (Active == this) Active = null; }
        public bool Ready { get; private set; }
        public string EventKind { get; set; }
        LevelManager level;
        int initialRemaining;
        float recycleClock;
        float nextChestTime;
        Camera worldCamera;
        int specialWave;
        public int SpecialSpawned { get; private set; }
        readonly HashSet<Monster> specials = new HashSet<Monster>();
        static readonly float[] WaveTimes = { 7, 13, 20, 27, 34, 40 };
        static readonly string[][] WaveTypes = {
            new[] { "Sniper Monster" },
            new[] { "MonsterBuffer_MoveSpeed_Blueprint", "Sniper Monster" },
            new[] { "MonsterBuffer_DamageReduction_Blueprint", "Sniper Monster" },
            new[] { "MonsterDebuffer_AttackSpeed_Blueprint", "MonsterBuffer_MoveSpeed_Blueprint" },
            new[] { "Sniper Monster", "MonsterBuffer_DamageReduction_Blueprint" },
            new[] { "Sniper Monster", "MonsterDebuffer_AttackSpeed_Blueprint" }
        };
        readonly List<Monster> distant = new List<Monster>(12);

        public static void Install(LevelManager owner, AbilityManager manager)
        {
            if (!FestivalDemo.Enabled || owner.GetComponent<FestivalRunController>() != null) return;
            var run = owner.gameObject.AddComponent<FestivalRunController>();
            run.level = owner;
            Active = run;
            run.worldCamera = Camera.main;
            run.initialRemaining = FestivalDemo.Settings.initialEnemies;
            var player = owner.PlayerCharacter;
            var preset = FestivalDemo.Preset(player.Blueprint);
            if (preset == null) throw new InvalidOperationException("Festival preset is missing.");
            FestivalDemo.ApplyWeapons(manager, player.Blueprint);
            player.AddDamageMultiplier(preset.damageBonus);
            player.AddAttackSpeed(preset.attackSpeedBonus);
            player.AddMaxHealthBonus(preset.healthBonus);
            foreach (var item in FestivalDemo.Items(player.Blueprint))
                MerchantItemEffectUtility.ApplyToCharacter(item, player, manager, false);
            player.GainHealth(player.MaxHealth);
            var fuel = player.GetComponent<ShiniEmberRuntime>();
            if (fuel != null) fuel.RestoreFuel(18);
            CrossSceneData.ClearStartingLobbyItems();
            run.Ready = true;
        }
        public void Tick()
        {
            if (!Ready || level.IsLevelEnded || Time.deltaTime <= 0 || StageEntryLoading.IsLoading) return;
            // Use active run time, so loading and reward selection never advance the schedule.
            if (level.CurrentLevelTime >= nextChestTime && TrySpawnItemChest(nextChestTime == 0))
            {
                float interval = Mathf.Max(1, FestivalDemo.Settings.chestSpawnInterval);
                nextChestTime = (Mathf.Floor(level.CurrentLevelTime / interval) + 1) * interval;
            }
            if (specialWave < WaveTimes.Length && level.CurrentLevelTime >= WaveTimes[specialWave])
            {
                SpawnSpecialWave(specialWave);
                specialWave++;
            }
            recycleClock += Time.deltaTime;
            if (recycleClock >= .5f)
            {
                recycleClock = 0;
                RecycleDistantEnemies();
            }
            int count = Mathf.Min(initialRemaining, FestivalDemo.Settings.spawnBudgetPerFrame);
            for (int i = 0; i < count; i++) level.SpawnMonsterFromCurrentSpawnTable();
            initialRemaining -= count;
        }
        void SpawnSpecialWave(int wave)
        {
            specials.RemoveWhere(m => m == null || m.HP <= 0 || !m.gameObject.activeInHierarchy);
            var pools = level.CurrentLevelBlueprint.monsters;
            foreach (string kind in WaveTypes[wave])
            {
                if (specials.Count >= FestivalDemo.Settings.specialPopulationLimit) break;
                // Reserve space for the scheduled threats without exceeding the total cap.
                if (level.EntityManager.LivingMonsters.Count >= FestivalDemo.Settings.populationLimit)
                {
                    var farthest = level.EntityManager.LivingMonsters.Where(m => m != null && !specials.Contains(m)
                        && !(m is BossMonster) && !(m is MiniBossMonster))
                        .OrderByDescending(m => (m.transform.position-level.PlayerCharacter.transform.position).sqrMagnitude).FirstOrDefault();
                    if (farthest == null) break;
                    farthest.DespawnForStageTransfer();
                }
                for (int pool = 0; pool < pools.Length; pool++)
                {
                    var blueprint = pools[pool].monsterBlueprints.FirstOrDefault(b => b != null && ((UnityEngine.Object)b).name == kind);
                    if (blueprint == null) continue;
                    Vector2 center = level.PlayerCharacter.transform.position;
                    Vector2 position = center;
                    bool valid = false;
                    for (int attempt=0; attempt<24; attempt++)
                    {
                        float angle = UnityEngine.Random.Range(0f, Mathf.PI*2);
                        position = FiniteStomachMap.SafeSpawn(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))*UnityEngine.Random.Range(5.5f,7.5f), .8f);
                        if (Vector2.Distance(center,position) < 5) continue;
                        bool blocked = Physics2D.OverlapCircleAll(position,.6f).Any(h => !h.isTrigger
                            && h.GetComponentInParent<Monster>() == null && h.GetComponentInParent<Character>() == null);
                        if (!blocked) { valid=true; break; }
                    }
                    if (!valid) break;
                    float hp = Mathf.Lerp(1.2f,1.8f,level.CurrentLevelTime/level.LevelDuration);
                    var enemy = level.EntityManager.SpawnMonster(pool, position, blueprint, blueprint.hp*(hp-1));
                    if (enemy != null) { specials.Add(enemy); SpecialSpawned++; }
                    break;
                }
            }
        }
        bool TrySpawnItemChest(bool initial)
        {
            Vector2 center = level.PlayerCharacter.transform.position;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
                float distance = initial ? 2f : UnityEngine.Random.Range(4f, 7f);
                Vector2 position = FiniteStomachMap.SafeSpawn(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, .7f);
                float actualDistance = Vector2.Distance(center, position);
                if (actualDistance < (initial ? 1.2f : 4f) || actualDistance > (initial ? 2.1f : 7.1f)) continue;
                bool blocked = false;
                foreach (var hit in Physics2D.OverlapCircleAll(position, .7f))
                    if (hit.GetComponentInParent<Chest>() != null ||
                        (!hit.isTrigger && hit.GetComponentInParent<Character>() == null && hit.GetComponentInParent<Monster>() == null))
                    { blocked = true; break; }
                if (blocked) continue;
                var chest = level.EntityManager.SpawnChest(level.CurrentLevelBlueprint.chestBlueprint, position);
                if (initial) chest.CompleteInitialPlacement();
                return true;
            }
            return false;
        }
        void RecycleDistantEnemies()
        {
            // Pool off-screen stragglers without kill credit. They must not exhaust the cap
            // while the player moves away, leaving the remaining minute empty.
            float halfHeight = worldCamera != null ? worldCamera.orthographicSize : 6;
            float aspect = worldCamera != null ? worldCamera.aspect : 16f/9;
            float radius = Mathf.Max(14, halfHeight * Mathf.Sqrt(1+aspect*aspect) * 1.25f + 2);
            Vector2 origin = level.PlayerCharacter.transform.position;
            distant.Clear();
            foreach (var monster in level.EntityManager.LivingMonsters)
            {
                if (monster == null || monster is BossMonster || monster is MiniBossMonster || monster.IsMiniStageOwned || monster.Blueprint is EliteMonsterBlueprint) continue;
                if (((Vector2)monster.transform.position-origin).sqrMagnitude <= radius*radius) continue;
                distant.Add(monster);
                if (distant.Count == 12) break;
            }
            foreach (var monster in distant) monster.DespawnForStageTransfer();
        }
    }
}
