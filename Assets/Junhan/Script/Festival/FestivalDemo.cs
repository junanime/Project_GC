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
        public string character, description, relic;
        public int[] weapons, items;
        public float damageBonus, attackSpeedBonus, healthBonus;
    }

    [Serializable]
    public sealed class FestivalSettings
    {
        public bool enabled;
        public float duration = 45, minimumLoadingSeconds = 3, eventSeconds = 20;
        public float startingSpawnRate = 10, finalSpawnRate = 18;
        public float normalBaseHealth = 60;
        public FestivalMonsterStats[] normalMonsters;
        public int initialEnemies = 18, populationLimit = 95, spawnBudgetPerFrame = 6;
        public float experienceGemMultiplier = 0;
        public float ashiActiveDuration = 10, ariActiveCooldown = 20, hyukiActiveCooldown = 20;
        public float ashiActiveMoveMultiplier = 1f;
        public int shiniStartingFuel = 24;
        public float ariPhoenixDamage = 55, hyukiStatusBonus = .25f, hyukiFreezeDelay = .6f;
        public float shiniActiveDamageMultiplier = 4, shiniPreparation = .3f;
        public int specialPopulationLimit = 8;
        public float chestSpawnInterval = 15f;
        public FestivalPreset[] presets;
    }

    // Branch-specific defaults live in Resources; normal gameplay never writes these settings.
    public static class FestivalDemo
    {
        public const string Rules = "festival_pc_v5_45";
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
            if (kind == CharacterSkillDefinition.SkillKind.Ari) return Mathf.Max(1, Settings.ariActiveCooldown);
            if (kind == CharacterSkillDefinition.SkillKind.Hyuki) return Mathf.Max(1, Settings.hyukiActiveCooldown);
            return Mathf.Min(24, original);
        }
        public static float ActiveDuration(float original, CharacterSkillDefinition.SkillKind kind) =>
            Enabled && kind == CharacterSkillDefinition.SkillKind.Ashi ? Mathf.Max(1, Settings.ashiActiveDuration) : original;
        public static float ShiniActiveDamageMultiplier => Enabled ? Mathf.Clamp(Settings.shiniActiveDamageMultiplier, .1f, 5) : 1;
        public static string SkillDescription(CharacterSkillDefinition skill, bool active)
        {
            string original = active ? skill.activeDescription : skill.passiveDescription;
            if (!Enabled) return original;
            if (!active) return skill.kind == CharacterSkillDefinition.SkillKind.Hyuki
                ? $"상태이상에 걸린 적에게 피해 +{Settings.hyukiStatusBonus*100:0}%" : original;
            switch (skill.kind)
            {
                case CharacterSkillDefinition.SkillKind.Ashi:
                    return $"{Settings.ashiActiveDuration:0.#}초간 침 개수·침 속도 ×2 · 재사용 24초";
                case CharacterSkillDefinition.SkillKind.Ari:
                    return $"{skill.activeDuration:0.#}초간 암석 봉황으로 변신해 돌진합니다.\n변신 대시 기본 피해 {Settings.ariPhoenixDamage:0} · 재사용 {Settings.ariActiveCooldown:0}초";
                case CharacterSkillDefinition.SkillKind.Hyuki:
                    return $"{Settings.hyukiFreezeDelay:0.#}초 후 주변 적을 5초 동안 빙결합니다.\n재사용 {Settings.hyukiActiveCooldown:0}초";
                case CharacterSkillDefinition.SkillKind.Shini:
                    return $"불씨 6개로 전방에 1.5초간 화염을 뿜습니다.\n기본 초당 피해 {35*ShiniActiveDamageMultiplier:0} · 준비 {Settings.shiniPreparation:0.#}초\n길게 누르면 불씨를 추가 소비해 이어서 발사합니다.";
                default: return original;
            }
        }
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
        public static bool StartingNobleAllowed(SyringeLegendaryAugmentAbility ability) =>
            ability != null && ability.AvailableAsNoble && ability.Type != Noble.LifeBurn;
        public static SyringeLegendaryAugmentAbility GrantStartingNoble(AbilityManager manager)
        {
            var all = manager.GetComponentsInChildren<SyringeLegendaryAugmentAbility>(true);
            var owned = all.Where(a => a.Owned).ToArray();
            if (owned.Length == 1) return owned[0]; // Re-entry cannot roll a second reward.
            if (owned.Length != 0) throw new InvalidOperationException("Festival starts with exactly one noble.");
            var candidates = all.Where(a => StartingNobleAllowed(a) && a.RequirementsMet()).OrderBy(a => a.Type).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException("No eligible festival noble.");
            var selected = candidates[UnityEngine.Random.Range(0, candidates.Length)];
            if (!manager.AcquireVer4Ability(selected)) throw new InvalidOperationException("Starting noble acquisition failed.");
            return selected;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { settings = null; EnabledOverride = null; }
    }

    public sealed class FestivalRunController : MonoBehaviour
    {
        public static FestivalRunController Active { get; private set; }
        FestivalMonsterRoster roster;
        public int SelectNormal(float seconds, float sample) => roster.Select(seconds, sample);
        public MonsterBlueprint ResolveNormal(MonsterBlueprint source) => roster.Resolve(source);
        void OnDestroy() { roster?.Dispose(); if (Active == this) Active = null; }
        public bool Ready { get; private set; }
        public string StartingNobleName { get; private set; }
        public string StartingNobleType { get; private set; }
        public string EventKind { get; set; }
        LevelManager level;
        int initialRemaining;
        float recycleClock;
        float nextChestTime;
        Camera worldCamera;
        int specialWave;
        public int SpecialSpawned { get; private set; }
        readonly HashSet<Monster> specials = new HashSet<Monster>();
        static readonly float[] WaveTimes = { 8, 13, 16, 24, 30, 32, 40 };
        static readonly string[][] WaveTypes = {
            new[] { "Acid Spore Blueprint" },
            new[] { "Sniper Monster" },
            new[] { "Acid Spore Blueprint" },
            new[] { "Acid Spore Blueprint" },
            new[] { "Sniper Monster" },
            new[] { "Acid Spore Blueprint" },
            new[] { "Acid Spore Blueprint" }
        };
        float nextSpecialAttempt;
        public int SnipersSpawned { get; private set; }
        public int AcidSporesSpawned { get; private set; }
        public static event Action<Monster, float> SpecialAppeared;
        readonly List<Monster> distant = new List<Monster>(12);

        public static void Install(LevelManager owner, AbilityManager manager)
        {
            if (!FestivalDemo.Enabled || owner.GetComponent<FestivalRunController>() != null) return;
            var run = owner.gameObject.AddComponent<FestivalRunController>();
            run.level = owner;
            run.roster = new FestivalMonsterRoster(owner.CurrentLevelBlueprint);
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
            if (fuel != null) fuel.RestoreFuel(FestivalDemo.Settings.shiniStartingFuel);
            var noble = FestivalDemo.GrantStartingNoble(manager);
            run.StartingNobleName = noble.Name; run.StartingNobleType = noble.Type.ToString();
            player.GetComponent<PrescriptionRuntime>()?.SetFestivalStartingReward(noble.Name);
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
            if (specialWave < WaveTimes.Length && level.CurrentLevelTime >= WaveTimes[specialWave] && level.CurrentLevelTime >= nextSpecialAttempt)
            {
                if (SpawnSpecialWave(specialWave)) specialWave++;
                nextSpecialAttempt = level.CurrentLevelTime + .2f;
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
        bool SpawnSpecialWave(int wave)
        {
            specials.RemoveWhere(m => m == null || m.HP <= 0 || !m.gameObject.activeInHierarchy);
            var pools = level.CurrentLevelBlueprint.monsters;
            foreach (string kind in WaveTypes[wave])
            {
                if (specials.Count >= FestivalDemo.Settings.specialPopulationLimit) return false;
                // Reserve space for the scheduled threats without exceeding the total cap.
                if (level.EntityManager.LivingMonsters.Count >= FestivalDemo.Settings.populationLimit)
                {
                    var farthest = level.EntityManager.LivingMonsters.Where(m => m != null && !specials.Contains(m)
                        && !(m is BossMonster) && !(m is MiniBossMonster))
                        .OrderByDescending(m => (m.transform.position-level.PlayerCharacter.transform.position).sqrMagnitude).FirstOrDefault();
                    if (farthest == null) return false;
                    farthest.DespawnForStageTransfer();
                }
                for (int pool = 0; pool < pools.Length; pool++)
                {
                    var blueprint = pools[pool].monsterBlueprints.FirstOrDefault(b => b != null && ((UnityEngine.Object)b).name == kind);
                    if (blueprint == null) continue;
                    bool sniper = blueprint is SniperMonsterBlueprint;
                    float minimum = sniper ? 3.8f : 5.5f, maximum = sniper ? 5.2f : 7.5f;
                    Vector2 center = level.PlayerCharacter.transform.position;
                    Vector2 position = center;
                    bool valid = false;
                    for (int attempt=0; attempt<24; attempt++)
                    {
                        float angle = UnityEngine.Random.Range(0f, Mathf.PI*2);
                        position = FiniteStomachMap.SafeSpawn(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))*UnityEngine.Random.Range(minimum,maximum), .8f);
                        float distance = Vector2.Distance(center,position);
                        if (distance < minimum || distance > maximum) continue;
                        bool blocked = Physics2D.OverlapCircleAll(position,.6f).Any(h => !h.isTrigger
                            && h.GetComponentInParent<Monster>() == null && h.GetComponentInParent<Character>() == null);
                        if (!blocked) { valid=true; break; }
                    }
                    if (!valid) return false;
                    float hp = 1.2f; // Fixed species health; no late global stat ramp.
                    var enemy = level.EntityManager.SpawnMonster(pool, position, blueprint, blueprint.hp*(hp-1));
                    if (enemy == null) return false;
                    specials.Add(enemy); SpecialSpawned++;
                    if (sniper) SnipersSpawned++; else AcidSporesSpawned++;
                    SpecialAppeared?.Invoke(enemy, level.CurrentLevelTime);
                    return true;
                }
            }
            return false;
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
