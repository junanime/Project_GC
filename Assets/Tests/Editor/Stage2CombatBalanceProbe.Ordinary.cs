#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static partial class Stage2CombatBalanceProbe
    {
        const string OrdinaryFixtures = "Library/Stage2FreshBalanceProof/variants/seed-261113-x1";
        const float OrdinarySeconds = 60;
        static readonly MethodInfo OrdinarySpawn = typeof(LevelManager).GetMethod("HandleNormalMonsterSpawn", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void RunOrdinaryAB()
        {
            foreach (string character in Characters)
                if (!File.Exists(OrdinaryFixtures + "/" + character + "-stage2-t450-build.json"))
                    throw new Exception("Missing required normal-speed earlier-growth fixture: " + character);
            string previous = Environment.GetEnvironmentVariable("GC_COMBAT_FIXTURES");
            try
            {
                Environment.SetEnvironmentVariable("GC_COMBAT_FIXTURES", OrdinaryFixtures);
                Begin("ordinary-ab", 8, null, "ordinary-ab-450", "Paired ordinary HP comparison; four fixed t450 builds, 75% and 80%, 60 seconds each.");
            }
            finally { Environment.SetEnvironmentVariable("GC_COMBAT_FIXTURES", previous); }
            File.WriteAllText(DirectoryPath + "/method.txt",
                "Eight independent actual Stage2 entries: Main/Ari/Hyuki/Shini, each at ordinary HP ratios .75 and .80 of corresponding Stage1 source assets, 60 fixed game seconds at timeScale1. Source: " + OrdinaryFixtures + ".\n" +
                "The shared Restore rebuilds and verifies the complete earlier observed t450 ability graph and numeric character/general snapshot, restores owned items, resets skill/dash/proc state and saves/restores the user profile. These are fixed earlier-growth snapshots, NOT fresh growth under the new HP values and NOT human win-rate or clear-time estimates.\n" +
                "Only runtime-owned ordinary and elite blueprint HP and matching roster entry HP are changed. Serialized Stage1 assets, attack/speed, spawn weights/rates, elite multiplier, specials and boss HP are untouched. Source HP and final unchanged assets are checked.\n" +
                "LevelManager Update is disabled to suppress extra chest logic. Its unchanged private HandleNormalMonsterSpawn runs once per normal physics tick, with its ordinary spawn timer and field clock advancing450→510. The configured rate curve,1.08 multiplier,242 cap and native480-second1.3 multiplier remain active. Each fixed tick uses its own paired seed around the real spawn call, restoring global RNG afterward; combat/drop randomness cannot shift future spawn selection. Actual spawn IDs/positions and counts are recorded.\n" +
                "The actual FieldObjectLayout rebuild runs with a paired seed around each coroutine step; actual layout seed/slot hash must match within each character pair. Existing field props remain, but clots are cleared with other preexisting monsters. All field special/event/optional/final-boss scheduling is disabled. No optional boss is summoned.\n" +
                "Real automatic owned weapons, collisions, ordinary monster AI and owned passive/item procs stay active. No active skill/dash/interaction is triggered. The invulnerable player follows the same time-authored circular waypoint route with normal Move input; no measurement teleport, target-aware evasion or steering from combat RNG. Physical positions are logged and may differ through combat. Pickup collider is disabled and loot dissolved; level/EXP, complete ability signature and owned item IDs must stay unchanged. Native time/kill-triggered combat item procs remain active, not additional item acquisitions.\n" +
                "Population mean is weighted per fixed tick. HP removed is killed initial HP plus clamped HP lost by surviving tracked monsters, excluding overkill; raw StatsManager damage is separately labelled including overkill. Spawn-to-death duration includes approach/travel and is NOT hit-to-kill TTK. Normal spawn counter, deaths and final population must reconcile. Pair rows compare actual spawn type/time sequence; positions need not be identical. Empty initial population is a controlled ordinary-flow window, not the accumulated population of an uninterrupted450-second run.\n" +
                "Every window records actual wall duration, engine frames, maximum frame game time and frame caps. A pair is a single controlled diagnostic per character, not a statistical confidence interval.\n");
            File.WriteAllText(DirectoryPath + "/ordinary-results.csv", "character,hp_ratio,seed,layout_seed,layout_sha256,fixed_seconds,field_start,field_end,wall_seconds,frames,max_frame_game_seconds,capped_frames,normal_spawned,normal_killed,population_mean,population_max,population_end,hp_spawned,hp_removed_excluding_overkill,raw_damage_including_overkill,mean_spawn_to_death_seconds,player_distance,minimum_floor_clearance,spawn_sequence_sha256,ability_graph_unchanged,level_exp_unchanged,source_assets_unchanged\n");
            File.WriteAllText(DirectoryPath + "/ordinary-spawns.csv", "character,hp_ratio,sequence,fixed_seconds,id,hp,x,y,player_x,player_y\n");
            File.WriteAllText(DirectoryPath + "/ordinary-samples.csv", "character,hp_ratio,fixed_seconds,field_seconds,population,killed,normal_spawned,player_x,player_y,raw_damage_including_overkill,base_spawn_rate\n");
            File.WriteAllText(DirectoryPath + "/ordinary-pairs.csv", "character,layout_equal,spawn_type_time_sequence_equal,spawned75,spawned80\n");
            File.WriteAllText(DirectoryPath + "/ordinary-hp.csv", "character,hp_ratio,tier,source_asset,source_hp,normal_clone_hp,elite_clone_base_hp,elite_multiplier\n");
        }

        static int OrdinarySeed(int round) => 261113 + CharacterIndex(round) * 1009;
        static string OrdinaryHash(string text)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant(); }

        static IEnumerator OrdinaryProbe(LevelManager level, int round)
        {
            string character = Characters[CharacterIndex(round)]; float ratio = round % 2 == 0 ? .75f : .80f; int seed = OrdinarySeed(round);
            var player = level.PlayerCharacter; var roster = ColonMonsterRoster.Active; var map = FiniteStomachMap.Instance;
            if (roster == null || !map.IsColon || OrdinarySpawn == null) throw new Exception("Ordinary A/B requires the live colon roster and original spawn handler");
            var fieldEvents = level.GetComponent<ColonFieldEvents>();
            if (fieldEvents != null) { fieldEvents.Cancel(); fieldEvents.AutoSchedule = false; fieldEvents.enabled = false; }
            foreach (var special in Object.FindObjectsOfType<ColonSpecialDirector>()) { special.enabled = false; special.StopAllCoroutines(); }
            Silence(level); Clear(level); Fire(false);
            var layout = level.GetComponent<FieldObjectLayout>();
            if (layout == null) throw new Exception("Missing actual field layout");
            var homes = level.GetComponent<ColonBossDirector>();
            if (homes == null) throw new Exception("Missing actual optional habitat director");
            typeof(ColonBossDirector).GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(homes, null);
            // Seed each actual coroutine step rather than allowing unrelated Update RNG to
            // consume the seed between its teardown and placement yields.
            var rebuild = layout.RebuildForStage(); bool next;
            do
            {
                var rng = UnityEngine.Random.state;
                try { UnityEngine.Random.InitState(seed); next = rebuild.MoveNext(); }
                finally { UnityEngine.Random.state = rng; }
                if (next) yield return rebuild.Current;
            } while (next);
            if (!layout.Ready || layout.Failed) throw new Exception("Paired ordinary layout rebuild failed");
            Silence(level); Clear(level);
            if (!homes.Ready || homes.Habitats.Count != 6) throw new Exception("Paired actual optional homes were not rebuilt");
            string layoutHash = OrdinaryHash(string.Join(";", layout.Slots.Select(s => s.kind + ":" + s.position.x.ToString("R", CultureInfo.InvariantCulture) + ":" + s.position.y.ToString("R", CultureInfo.InvariantCulture) + ":" + s.partner)) + "|homes|" +
                string.Join(";", homes.Placements.Select(h => h.kind + ":" + h.position.x.ToString("R", CultureInfo.InvariantCulture) + ":" + h.position.y.ToString("R", CultureInfo.InvariantCulture) + ":" + h.wallNormal.x.ToString("R", CultureInfo.InvariantCulture) + ":" + h.wallNormal.y.ToString("R", CultureInfo.InvariantCulture))));
            string pairKey = Key + ".Ordinary." + character;
            if (round % 2 == 0) SessionState.SetString(pairKey + ".Layout", layoutHash);
            else if (SessionState.GetString(pairKey + ".Layout", "") != layoutHash) throw new Exception("Paired actual field layouts differ: " + character);
            // Layout reconstruction can take several render frames. Normalize the exact
            // fixture again now, so none of that setup time ages a combat proc/cooldown.
            yield return Restore(level, character, 450, false); Fire(false); Clear(level);

            var source = roster.Level.monsters.Take(roster.PoolIndex).SelectMany(x => x.monsterBlueprints).ToArray();
            var originals = ColonMonsterRoster.DirectNormalSources.Select(i => source[i]).ToArray();
            var originalHp = originals.Select(x => x.hp).ToArray();
            for (int i = 0; i < 8; i++)
            {
                var ordinary = roster.Normals[i]; var elite = roster.Elites[i];
                if (AssetDatabase.Contains(ordinary.blueprint) || AssetDatabase.Contains(elite.blueprint) || ReferenceEquals(ordinary.blueprint, originals[i]))
                    throw new Exception("A/B may modify runtime clones only");
                ordinary.hp = ordinary.blueprint.hp = originalHp[i] * ratio;
                elite.hp = elite.blueprint.hp = originalHp[i] * ratio;
                File.AppendAllText(DirectoryPath + "/ordinary-hp.csv", Row(character, ratio, ordinary.id, AssetDatabase.GetAssetPath(originals[i]), originalHp[i], ordinary.hp, elite.hp, ((EliteMonsterBlueprint)elite.blueprint).hpMultiplier));
            }
            if (roster.Level.normalSpawnPopulationLimit != 242 || level.SpawnPotionRemaining > 0 || !Mathf.Approximately(DigestiveEnzymeDifficultyManager.SpawnRateMultiplier, 1))
                throw new Exception("Unexpected spawn cap or external spawn-rate modifier");
            var opening = StomachOpening.Find(false);
            Vector2 center = map.ClampToFloor(opening.FrontLip + opening.Inward * 3, 6.5f);
            if (map.FloorClearance(center, true) < 6.45f) throw new Exception("Ordinary route needs the verified open-floor footprint");
            PlacePlayer(player, center + Vector2.right * 2.5f); player.Move(Vector2.up);
            var body = player.GetComponent<Rigidbody2D>(); body.velocity = Vector2.zero;
            ColonContentSmoke.Set(player, "isInvincible", true); player.CollectableCollider.enabled = false;
            ColonContentSmoke.Set(level, "levelTime", 450f); ColonContentSmoke.Set(level, "timeSinceLastMonsterSpawned", 0f);
            var before = player.CaptureRunSceneState(); var manager = Object.FindObjectOfType<AbilityManager>();
            string abilities = AbilitySignature(manager.CaptureRunSceneAbilities());
            string items = string.Join(";", player.GetComponent<OctoberItemRuntime>().State.owned.OrderBy(x => x));
            var stats = Object.FindObjectOfType<StatsManager>(); float damageStart = stats.DamageDealt;
            int normalStart = level.NormalMonstersSpawned, frames = 0, cappedFrames = 0, killed = 0, spawned = 0, populationMax = 0;
            float maxFrame = 0, killedHp = 0, spawnedHp = 0, lifetimes = 0, distance = 0, minimumFloor = float.MaxValue;
            double populationSum = 0; var tickets = new Dictionary<Monster, OrdinaryTicket>(); var sequence = new StringBuilder();
            Vector2 previousPosition = player.transform.position; float elapsed = 0, wall = Time.realtimeSinceStartup;
            void Death(Monster monster)
            {
                if (!tickets.TryGetValue(monster, out var ticket)) return;
                if (!monster.WasKilledByPlayer) throw new Exception("Tracked ordinary monster vanished without a combat kill");
                killed++; killedHp += ticket.Hp; lifetimes += elapsed - ticket.Born; tickets.Remove(monster);
            }
            var driver = level.gameObject.AddComponent<OrdinaryBalanceFixedDriver>();
            driver.Limit = Mathf.RoundToInt(OrdinarySeconds / Time.fixedDeltaTime);
            driver.Step = tick =>
            {
                elapsed = tick * Time.fixedDeltaTime; ColonContentSmoke.Set(level, "levelTime", 450f + elapsed);
                Vector2 desired = center + new Vector2(Mathf.Cos(elapsed * .2f), Mathf.Sin(elapsed * .2f)) * 2.5f;
                Vector2 delta = desired - (Vector2)player.transform.position;
                player.Move(delta.magnitude > .12f ? delta.normalized : Vector2.zero);
                var rng = UnityEngine.Random.state;
                try { UnityEngine.Random.InitState(unchecked(seed * 397 ^ tick * 104729)); OrdinarySpawn.Invoke(level, null); }
                finally { UnityEngine.Random.state = rng; }
                int population = 0;
                foreach (var monster in level.EntityManager.LivingMonsters)
                {
                    if (monster == null || !monster.IsAlive) continue;
                    if (!roster.TryGet(monster.Blueprint, out var entry) || entry.elite || !entry.id.StartsWith("N", StringComparison.Ordinal))
                        throw new Exception("Non-ordinary actor leaked into isolated A/B window: " + monster.name);
                    population++;
                    if (tickets.ContainsKey(monster)) continue;
                    tickets.Add(monster, new OrdinaryTicket { Hp = monster.HP, Born = elapsed }); spawned++; spawnedHp += monster.HP;
                    sequence.Append(tick).Append(':').Append(entry.id).Append(';');
                    var p = monster.transform.position; var q = player.transform.position;
                    File.AppendAllText(DirectoryPath + "/ordinary-spawns.csv", Row(character, ratio, spawned, elapsed, entry.id, monster.HP, p.x, p.y, q.x, q.y));
                }
                populationSum += population; populationMax = Mathf.Max(populationMax, population);
                Vector2 position = player.transform.position; distance += Vector2.Distance(previousPosition, position); previousPosition = position;
                minimumFloor = Mathf.Min(minimumFloor, map.FloorClearance(position, true));
                if (tick % Mathf.RoundToInt(1 / Time.fixedDeltaTime) == 0)
                    File.AppendAllText(DirectoryPath + "/ordinary-samples.csv", Row(character, ratio, elapsed, level.CurrentLevelTime, population, killed, level.NormalMonstersSpawned - normalStart, position.x, position.y, stats.DamageDealt - damageStart, level.GetCurrentBaseMonsterSpawnRate()));
                if (level.IsRunFlowPaused || !player.IsAlive || player.IsChoosingAugment || Time.timeScale != 1)
                    throw new Exception("Ordinary measurement was interrupted or attempted new growth");
            };
            Monster.Died += Death; UnityEngine.Random.InitState(seed ^ 0x41F1); Fire(true); driver.enabled = true;
            try
            {
                while (!driver.Done && driver.Error == null)
                {
                    frames++; maxFrame = Mathf.Max(maxFrame, Time.deltaTime); if (Time.deltaTime >= Time.maximumDeltaTime * .99f) cappedFrames++;
                    foreach (var loot in Object.FindObjectsOfType<Collectable>()) loot.DissolveInAcidLake();
                    yield return null;
                }
                if (driver.Error != null) throw new Exception("Ordinary fixed driver failed", driver.Error);
                Fire(false); player.Move(Vector2.zero);
                int populationEnd = tickets.Count;
                float removedHp = killedHp + tickets.Sum(t => Mathf.Clamp(t.Value.Hp - t.Key.HP, 0, t.Value.Hp));
                var after = player.CaptureRunSceneState();
                bool graphUnchanged = abilities == AbilitySignature(manager.CaptureRunSceneAbilities());
                bool growthUnchanged = before.CurrentLevel == after.CurrentLevel && Mathf.Approximately(before.CurrentExp, after.CurrentExp) && items == string.Join(";", player.GetComponent<OctoberItemRuntime>().State.owned.OrderBy(x => x));
                bool sourcesUnchanged = originals.Select((bp, i) => Mathf.Approximately(bp.hp, originalHp[i])).All(x => x);
                if (!graphUnchanged || !growthUnchanged || !sourcesUnchanged || spawned != level.NormalMonstersSpawned - normalStart || spawned != killed + populationEnd)
                    throw new Exception("Ordinary A/B invariant failed: graph=" + graphUnchanged + " growth=" + growthUnchanged + " assets=" + sourcesUnchanged + " counts=" + spawned + "/" + killed + "/" + populationEnd);
                string sequenceHash = OrdinaryHash(sequence.ToString());
                File.AppendAllText(DirectoryPath + "/ordinary-results.csv", Row(character, ratio, seed, layout.Seed, layoutHash, elapsed, 450, level.CurrentLevelTime, Time.realtimeSinceStartup - wall, frames, maxFrame, cappedFrames, spawned, killed, populationSum / driver.Count, populationMax, populationEnd, spawnedHp, removedHp, stats.DamageDealt - damageStart, killed > 0 ? lifetimes / killed : 0, distance, minimumFloor, sequenceHash, graphUnchanged, growthUnchanged, sourcesUnchanged));
                if (round % 2 == 0) { SessionState.SetString(pairKey + ".Sequence", sequenceHash); SessionState.SetInt(pairKey + ".Spawned", spawned); }
                else
                {
                    bool sameSequence = SessionState.GetString(pairKey + ".Sequence", "") == sequenceHash;
                    File.AppendAllText(DirectoryPath + "/ordinary-pairs.csv", Row(character, true, sameSequence, SessionState.GetInt(pairKey + ".Spawned", -1), spawned));
                    if (!sameSequence && populationMax < roster.Level.normalSpawnPopulationLimit) throw new Exception("Uncapped paired ordinary spawn type/time sequence changed");
                }
            }
            finally { Monster.Died -= Death; driver.enabled = false; Object.Destroy(driver); Fire(false); player.Move(Vector2.zero); }
        }

        sealed class OrdinaryTicket { public float Hp, Born; }
    }

    [DefaultExecutionOrder(-31000)]
    public sealed class OrdinaryBalanceFixedDriver : MonoBehaviour
    {
        public int Limit, Count;
        public Action<int> Step;
        public Exception Error;
        public bool Done => Count >= Limit;
        void FixedUpdate()
        {
            if (Step == null || Done || Error != null) return;
            try { Step(++Count); }
            catch (Exception error) { Error = error; enabled = false; }
        }
    }
}
#endif
