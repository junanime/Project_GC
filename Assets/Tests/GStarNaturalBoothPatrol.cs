#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire.Tests
{
    // Plays the normal booth field from its first frame. Frame gaps, menus and loading
    // are accounted separately from monotonic active wall time, independent of the stage clock.
    public sealed class GStarNaturalBoothPatrol : MonoBehaviour
    {
        static readonly string[] Args = Environment.GetCommandLineArgs();
        static string Arg(string flag, string fallback)
        {
            int at = Array.IndexOf(Args, flag);
            return at >= 0 && at + 1 < Args.Length ? Args[at + 1] : fallback;
        }
        static int Number(string flag, int fallback, int min, int max) =>
            int.TryParse(Arg(flag, ""), out int v) ? Mathf.Clamp(v, min, max) : fallback;
        public static bool Enabled => !Application.isEditor && Application.productName == GStarLatePlaySoak.QAProduct && Args.Contains("-qaNaturalOral");
        static readonly string InstanceKey = Guid.NewGuid().ToString("N");
        static string Output => Path.GetFullPath(Arg("-qaNaturalOutput", Path.Combine(Application.persistentDataPath, "natural-" + InstanceKey)));
        public static string VisitorDirectory => Path.Combine(Output, "visitor-" + InstanceKey);
        string FileFor(string file) => Path.Combine(Output, file);

        string characterKey, pilot;
        int seed, activeTarget, dashesAtStart, skillInputs, opened, rewards, usedHearts, spicyTicks;
        int lastLevel, lastKills, lastSpawns, lastDash, lastScore, near;
        float previousHp, minHp, hpLost, recovered, nextThink, nextDash, nextSkill, nextBoxSearch, nextItem;
        double activeWall, nextSample, lastWall, ignoredWall, bootWall, nextMenu, stalledSince;
        bool finished, lastActive;
        Vector2 move, center;
        readonly List<OralGuardRoute> routes = new List<OralGuardRoute>();
        readonly List<ExpGem> gems = new List<ExpGem>();
        LevelManager level;
        Character player;
        StatsManager stats;
        OralStageRuntime oral;
        Chest nearbyChest;
        InventorySlot[] inventory;
        AbilitySelectionDialog dialog;
        static readonly System.Reflection.FieldInfo CardTitle = typeof(AbilityCard).GetField("nameText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        static string OfferedName(AbilityCard card) => ((TMPro.TMP_Text)CardTitle.GetValue(card)).text.Trim();
        static readonly System.Reflection.FieldInfo OfferedAbility = typeof(AbilityCard).GetField("ability", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        static bool IsLifeBurn(AbilityCard card)
        {
            var ability = (Ability)OfferedAbility.GetValue(card);
            if (ability is Ver4AugmentOffer offer) ability = offer.Source;
            return ability is SyringeLegendaryAugmentAbility noble && noble.Type == SyringeLegendaryAugmentAbility.LegendaryAugmentType.LifeBurn;
        }
        bool usedLifeBurn;
        float stageTime, nextTrapKey, nextJumpInput;
        bool wasBound;
        int binds, trapKeyInputs, escapedBinds;
        double trappedActiveSeconds;
        Vector2 previousWaypoint, previousSamplePosition;
        readonly List<Vector2> closeEnemies = new List<Vector2>(64);
        static readonly Vector2[] EscapeArrows = {Vector2.up, Vector2.down, Vector2.left, Vector2.right};
        static readonly Vector2[] Headings = Enumerable.Range(0, 8).Select(i => (Vector2)(Quaternion.Euler(0, 0, i * 45) * Vector2.right)).ToArray();
        static double Now => Time.realtimeSinceStartupAsDouble;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Enabled) return;
            var host = new GameObject("G-Star natural oral field pilot");
            DontDestroyOnLoad(host);
            host.AddComponent<GStarNaturalBoothPatrol>();
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Output);
            // Refuse to clobber another or previously completed run.
            using (File.Open(FileFor("natural-started.txt"), FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { }
            bootWall = Now;
            Application.logMessageReceived += OnLog;
            Chest.OnAnyChestOpened += Opened;
            pilot = Arg("-qaPilot", "returning").ToLowerInvariant();
            if (pilot != "returning" && pilot != "first") throw new ArgumentException("-qaPilot is first or returning.");
            seed = Number("-qaSeed", 204831, 1, int.MaxValue);
            activeTarget = Number("-qaActiveSeconds", 660, 15, 7200);
            characterKey = Arg("-qaCharacter", "Ashi");
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Number("-qaFps", 45, 15, 90);
            yield return WaitFor(() => ApothecaryUI.Instance != null, "native menu");
            var ui = ApothecaryUI.Instance;
            ui.Show("main");
            if (!GStarDemo.Enabled || !GStarSession.ResetVisitor()) throw new InvalidOperationException("Fresh QA visitor entry failed.");
            var prefs = GamePreferences.Current.Copy();
            prefs.pauseOnFocusLoss = false;
            GamePreferences.Apply(prefs, false, false);
            ui.Show("prepare");
            var chosen = ui.Config.characters.First(c => string.Equals(OctoberArt.CharacterKey(c), characterKey, StringComparison.OrdinalIgnoreCase));
            characterKey = OctoberArt.CharacterKey(chosen);
            CrossSceneData.CharacterBlueprint = chosen;
            CrossSceneData.StartingLobbyItems = LobbyLoadoutData.ConsumeSelectedCarryItems();
            UnityEngine.Random.InitState(seed);
            if (!StageEntryLoading.Begin(chosen, null, 0)) throw new InvalidOperationException("Fresh oral entry refused.");
            yield return WaitFor(() => !StageEntryLoading.IsLoading && GStarSession.AttemptStarted && (level = FindObjectOfType<LevelManager>()) != null &&
                level.InitialFieldObjectsReady && !level.IsRunFlowPaused && (player=level.PlayerCharacter) != null && (oral=OralStageRuntime.Instance) != null, "real oral field");
            dialog = level.EntityManager.AbilitySelectionDialog;
            stats = FindObjectOfType<StatsManager>();
            inventory = FindObjectsOfType<InventorySlot>(true);
            previousHp = minHp = player.CurrentHealth;
            dashesAtStart = player.Skills.DashSequence;
            previousSamplePosition = player.Position;
            center = OralMapGeometry.TongueCenter(oral.Map);
            for (int i = 0; i < OralMapGeometry.ToothCount; i++)
                if (OralGuardRoute.TryCreate(oral.Map, i, out var route)) routes.Add(route);
            File.WriteAllText(FileFor("timeline.csv"), "active_wall_s,stage_s,hp,max_hp,hp_loss,healed,kills,normal_spawn,level,alive,near,score,dashes,skill_inputs,rewards,boxes,hearts,spicy_ticks,zone,pos_x,pos_y,sample_displacement,bound,trap_key_inputs,escaped_binds,field_event\n");
            File.WriteAllText(FileFor("loadout.txt"), "character=" + characterKey + "\npilot=" + pilot + "\nseed=" + seed + "\nvisitor=" + VisitorDirectory + "\n" +
                string.Join("\n", inventory.Select(x => x.name + "=" + x.Count)) + "\n");
            if (level.CurrentLevelTime > 12 || player.CurrentLevel > 1) throw new InvalidOperationException("Pilot did not enter a fresh field.");
            using (var keys = new GStarLatePlaySoak.PlaybackKeyboard(player))
            {
                lastWall = Now;
                while (!finished && player != null && level != null && player.IsAlive && !level.IsLevelEnded && activeWall < activeTarget)
                {
                    bool playing = IsPlaying();
                    double now = Now, delta = now - lastWall;
                    if (playing && lastActive && delta > 0 && delta <= 2)
                    {
                        activeWall += delta;
                        if (player.IsTrapBound && wasBound) trappedActiveSeconds += delta;
                    }
                    else ignoredWall += delta;
                    lastActive = playing;
                    lastWall = now;
                    Observe();
                    if (!playing)
                    {
                        keys.Set(Vector2.zero, false, false, false);
                        if (stalledSince <= 0) stalledSince = now;
                        HandleMenu(now);
                        if (now - stalledSince > 120) throw new TimeoutException("Field remains inactive: " + ApothecaryUI.Instance?.Page);
                    }
                    else
                    {
                        stalledSince = 0;
                        Drive(keys);
                        if (activeWall >= nextSample)
                        {
                            WriteRow();
                            nextSample = Math.Floor(activeWall / 15 + 1) * 15;
                        }
                    }
                    yield return null;
                }
            }
            if (finished) yield break;
            if (player != null && level != null) { Observe(); WriteRow(); }
            string outcome = player == null || level == null ? "field-exit" : !player.IsAlive ? "death" :
                level.IsLevelEnded ? (RunScore.Current?.Data?.success == true ? "clear" : "ended") : "time-target";
            Finish(0, outcome);
        }

        static IEnumerator WaitFor(Func<bool> condition, string target)
        {
            double deadline = Now + 300;
            while (!condition())
            {
                if (Now >= deadline) throw new TimeoutException(target);
                yield return null;
            }
        }
        bool IsPlaying()
        {
            return !StageEntryLoading.IsLoading && !level.IsRunFlowPaused && Time.timeScale > 0 &&
                    !dialog.MenuOpen && ApothecaryUI.Instance?.Page == "hud" && !player.IsPortalTravelling;
        }
        void Opened(Chest chest) { opened++; }
        void Observe()
        {
            stageTime = level.CurrentLevelTime;
            float health = player.CurrentHealth;
            hpLost += Mathf.Max(0, previousHp - health);
            recovered += Mathf.Max(0, health - previousHp);
            previousHp = health;
            minHp = Mathf.Min(minHp, health);
            lastLevel = player.CurrentLevel;
            lastKills = stats.MonstersKilled;
            lastSpawns = level.NormalMonstersSpawned;
            lastDash = player.Skills.DashSequence - dashesAtStart;
            lastScore = RunScore.Current?.Data?.Total ?? 0;
            spicyTicks = OralTasteEvents.Instance?.BurnTicks ?? 0;
        }
        void HandleMenu(double now)
        {
            if (now < nextMenu) return;
            nextMenu = now + .5;
            if (dialog.MenuOpen && !dialog.SelectionAnimating)
            {
                var cards = dialog.GetComponentsInChildren<AbilityCard>().Where(c => c.gameObject.activeInHierarchy).ToArray();
                var card = pilot == "first" ? cards.FirstOrDefault() : cards.OrderBy(IsLifeBurn).ThenByDescending(c => c.SelectionGrade).FirstOrDefault();
                if (card != null)
                {
                    var labels = card.GetComponentsInChildren<TMPro.TMP_Text>()
                        .Where(t => t.enabled && !string.IsNullOrWhiteSpace(t.text))
                        .Select(t => t.text.Replace("\n", " ").Replace("\r", " ")).Distinct();
                    string title = OfferedName(card);
                    if (string.IsNullOrEmpty(title)) throw new InvalidDataException("The selected visible card has no title.");
                    usedLifeBurn |= IsLifeBurn(card);
                    File.AppendAllText(FileFor("loadout.txt"), FormattableString.Invariant($"{stageTime:F1} selected={title} grade={card.SelectionGrade} offers={string.Join(" / ", cards.Select(OfferedName))} · {string.Join(" · ", labels)}\n"));
                    card.Selected();
                    rewards++;
                }
            }
            else if (ApothecaryUI.Instance?.Page == "skillReward")
            {
                var button = ApothecaryUI.Instance.GetComponentsInChildren<Button>().FirstOrDefault(x => x.isActiveAndEnabled && x.interactable);
                if (button != null) { button.onClick.Invoke(); rewards++; }
            }
        }
        void Drive(GStarLatePlaySoak.PlaybackKeyboard keys)
        {
            bool familiar = pilot == "returning";
            if (player.IsTrapBound)
            {
                // The production prompt publishes the same next arrow to PC and touch UI.
                // Press and release the real movement binding, including repeated directions.
                if (!wasBound)
                {
                    wasBound = true;
                    binds++;
                    nextTrapKey = stageTime + (familiar ? .42f : 1.15f);
                }
                var trap = player.GetComponent<PlayerTrapBindRuntime>()?.CurrentTrap;
                int expected = trap == null ? -1 : trap.EscapeTarget;
                if (expected >= 0 && expected < EscapeArrows.Length && stageTime >= nextTrapKey)
                {
                    keys.Set(EscapeArrows[expected], false, false, false);
                    nextTrapKey = stageTime + (familiar ? .29f : .70f);
                    trapKeyInputs++;
                }
                else keys.Set(Vector2.zero, false, false, false);
                return;
            }
            if (wasBound)
            {
                wasBound = false;
                escapedBinds++;
                nextThink = 0;
            }
            bool dash = stageTime >= nextDash && player.CurrentDashCharges > 0;
            bool skill = stageTime >= nextSkill && player.Skills.CanActivate;
            if (stageTime >= nextThink)
            {
                nextThink = stageTime + (familiar ? .20f : .4f);
                Vector2 avoid = Vector2.zero;
                near = 0;
                closeEnemies.Clear();
                foreach (var monster in level.EntityManager.LivingMonsters)
                {
                    if (monster == null || !monster.IsAlive) continue;
                    Vector2 away = player.Position - monster.Position;
                    float distance = away.sqrMagnitude;
                    if (distance < 16) near++;
                    if (distance < 30) closeEnemies.Add(monster.Position);
                    if (distance < 5) avoid += away.normalized / Mathf.Max(.25f, distance);
                }
                float phase = seed % 29 * .18f;
                Vector2 target = center + new Vector2(Mathf.Sin(stageTime * .14f + phase) * 5.5f, Mathf.Sin(stageTime * .19f + phase) * 3f);
                if (familiar && near < 10)
                {
                    StageActorRegistry.CopyActiveGems(gems);
                    float closest = 11;
                    foreach (var gem in gems)
                    {
                        if (gem == null || gem.IsBeingCollected) continue;
                        Vector2 p = gem.transform.position;
                        float sq = (p-player.Position).sqrMagnitude;
                        if (sq < closest) { closest = sq; target = p; }
                    }
                }
                if (stageTime >= nextBoxSearch)
                {
                    nextBoxSearch = stageTime + 2.5f;
                    nearbyChest = null;
                    float best = familiar ? 23 : 6;
                    foreach (var chest in FindObjectsOfType<Chest>())
                    {
                        if (!chest.CanInteract) continue;
                        float sq = ((Vector2)chest.transform.position - player.Position).sqrMagnitude;
                        if (sq < best) { best = sq; nearbyChest = chest; }
                    }
                }
                if (nearbyChest != null && nearbyChest.isActiveAndEnabled && nearbyChest.CanInteract && near < 10)
                {
                    target = nearbyChest.transform.position;
                    if ((target-player.Position).sqrMagnitude < 2.1f)
                        nearbyChest.GetComponentInChildren<LootInteraction>()?.TryInteract();
                }
                move = (target - player.Position).normalized;
                if (familiar && near > 3) move = PickOpenHeading(move);
                else if (avoid.sqrMagnitude > .1f) move = (move + avoid.normalized * (familiar ? .7f : .35f)).normalized;
                previousWaypoint = move;
                var finale = OralSwallowFinale.Instance;
                if (finale != null && finale.VorticesActive)
                {
                    foreach (var vortex in finale.Whirlpools)
                    {
                        Vector2 away = player.Position - vortex.Position;
                        float clearance = Mathf.Max(.1f, away.magnitude - vortex.CollisionRadius);
                        if (clearance < 1.4f) move = (move * .3f + away.normalized / clearance).normalized;
                    }
                }
            }
            ApplyToothRoute(ref dash);
            if (dash) nextDash = stageTime + (familiar ? 3.5f : 8f);
            if (skill) { skillInputs++; nextSkill = stageTime + (familiar ? 4.5f : 10f); }
            if (stageTime >= nextItem && player.CurrentHealth < player.MaxHealth * .60f)
            {
                nextItem = stageTime + 1;
                var heart = inventory.FirstOrDefault(s => s != null && s.Count > 0 && s.CollectableType is HealthCollectable);
                if (heart != null) { heart.UseItem(); usedHearts++; }
            }
            bool inverted = familiar && OralFieldEvents.Instance != null && OralFieldEvents.Instance.ReversesControlsFor(player);
            keys.Set(inverted ? -move : move, dash, skill, false);
        }
        // Choose a viable local opening as the surrounding pack closes; a symmetric
        // cluster otherwise cancels steering to zero and the pilot runs into the crowd.
        Vector2 PickOpenHeading(Vector2 desired)
        {
            Vector2 best = desired;
            float bestScore = float.MinValue;
            foreach (var direction in Headings)
            {
                Vector2 from = player.Position, first = from + direction * .85f, ahead = from + direction * 2.2f;
                float score = .65f * Vector2.Dot(desired, direction) + .50f * Vector2.Dot(previousWaypoint, direction);
                if (OralMapGeometry.ZoneAt(oral.Map, ahead) != OralMapGeometry.ZoneAt(oral.Map, from)) score -= 10;
                foreach (var enemy in closeEnemies)
                {
                    score -= .75f / (.20f + (first - enemy).sqrMagnitude);
                    score -= .9f / (.3f + (ahead - enemy).sqrMagnitude);
                }
                if (score > bestScore) { bestScore = score; best = direction; }
            }
            return best;
        }
        void ApplyToothRoute(ref bool dash)
        {
            if (oral.IsAirborne || routes.Count == 0) return;
            var taste = OralTasteEvents.Instance;
            bool spicy = taste != null && taste.Active && taste.Kind == OralTasteKind.Spicy;
            var zone = OralMapGeometry.ZoneAt(oral.Map, player.Position);
            if (!spicy && zone != OralZone.Teeth) return;
            OralGuardRoute closest = routes[0];
            foreach (var route in routes)
                if ((route.Launch-player.Position).sqrMagnitude < (closest.Launch-player.Position).sqrMagnitude) closest = route;
            if (zone == OralZone.Tongue && spicy)
            {
                Vector2 direction = (closest.Landing-closest.Launch).normalized;
                bool ready = OralMapGeometry.TryJump(oral.Map, player.Position, direction, out _);
                move = ready ? direction : (closest.Launch-player.Position).normalized;
                dash = ready && player.CurrentDashCharges > 0 && stageTime >= nextJumpInput;
                if (dash) nextJumpInput = stageTime + .35f;
            }
            else if (zone == OralZone.Teeth)
            {
                move = spicy ? Vector2.zero : (closest.Launch-player.Position).normalized;
                dash = !spicy && stageTime >= nextDash && player.CurrentDashCharges > 0 && OralMapGeometry.TryJump(oral.Map, player.Position, move, out _);
            }
        }
        void WriteRow()
        {
            int alive = level.EntityManager.LivingMonsters.Count;
            var zone = OralMapGeometry.ZoneAt(oral.Map, player.Position);
            int nearby = 0;
            foreach (var m in level.EntityManager.LivingMonsters)
                if(m != null && m.IsAlive && (m.Position-player.Position).sqrMagnitude < 16) nearby++;
            var displacement = Vector2.Distance(player.Position, previousSamplePosition);
            previousSamplePosition = player.Position;
            File.AppendAllText(FileFor("timeline.csv"), FormattableString.Invariant($"{activeWall:F2},{stageTime:F2},{previousHp:F1},{player.MaxHealth:F1},{hpLost:F1},{recovered:F1},{lastKills},{lastSpawns},{lastLevel},{alive},{nearby},{lastScore},{lastDash},{skillInputs},{rewards},{opened},{usedHearts},{spicyTicks},{zone},{player.Position.x:F2},{player.Position.y:F2},{displacement:F2},{player.IsTrapBound},{trapKeyInputs},{escapedBinds},{OralFieldEvents.Instance?.CurrentEvent}\n"));
        }
        void OnLog(string message, string trace, LogType type)
        {
            if (finished || (type != LogType.Error && type != LogType.Assert && type != LogType.Exception)) return;
            File.AppendAllText(FileFor("errors.txt"), message + "\n" + trace + "\n");
            Finish(1, "error");
        }
        void Finish(int code, string outcome)
        {
            if (finished) return;
            finished = true;
            Application.logMessageReceived -= OnLog;
            Chest.OnAnyChestOpened -= Opened;
            File.WriteAllText(FileFor("summary.txt"), FormattableString.Invariant($"outcome={outcome}\nseed={seed}\ncharacter={characterKey}\npilot={pilot}\nactive_wall_seconds={activeWall:F3}\nstage_seconds={stageTime:F3}\nprocess_wall_seconds={Now-bootWall:F3}\nmenu_loading_excluded_after_entry_s={ignoredWall:F3}\nhp={previousHp:F2}\nhp_min={minHp:F2}\nhp_lost={hpLost:F2}\nhealed={recovered:F2}\nkills={lastKills}\nnormal_spawned={lastSpawns}\nlevel={lastLevel}\nscore={lastScore}\ndashes={lastDash}\nskill_inputs={skillInputs}\nrewards={rewards}\nboxes={opened}\nhearts={usedHearts}\nspicy_ticks={spicyTicks}\nbinds={binds}\nescaped_binds={escapedBinds}\ntrap_key_inputs={trapKeyInputs}\nbound_active_seconds={trappedActiveSeconds:F3}\nlife_burn_selected={usedLifeBurn}\n"));
            Application.Quit(code);
        }
    }
}
#endif
