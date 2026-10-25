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
        public float duration = 60, minimumLoadingSeconds = 3, eventSeconds = 25;
        public float startingSpawnRate = 10, finalSpawnRate = 16;
        public float startingHealthMultiplier = 1.35f, finalHealthMultiplier = 1.85f;
        public int initialEnemies = 18, populationLimit = 110, spawnBudgetPerFrame = 6;
        public int questKills = 20, questDashes = 4, questActiveUses = 2;
        public FestivalPreset[] presets;
    }

    // Branch-specific defaults live in Resources; normal gameplay never writes these settings.
    public static class FestivalDemo
    {
        public const string Rules = "festival_pc_v1";
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
        public bool Ready { get; private set; }
        public string EventKind { get; set; }
        LevelManager level;
        int initialRemaining;
        float recycleClock;
        Camera worldCamera;
        readonly List<Monster> distant = new List<Monster>(12);

        public static void Install(LevelManager owner, AbilityManager manager)
        {
            if (!FestivalDemo.Enabled || owner.GetComponent<FestivalRunController>() != null) return;
            var run = owner.gameObject.AddComponent<FestivalRunController>();
            run.level = owner;
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
        // Bounded growth keeps the four starting effects intact without interrupting a short run.
        public static void AutoLevel(Character player)
        {
            if (player.CurrentLevel <= 6) player.AddDamageMultiplier(.035f);
        }
    }
}
