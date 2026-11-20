#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public static partial class OralCeilingLemonSmoke
    {
        const string FieldMedia = "field-balance";

        static IEnumerator GStarBalanceChecks(LevelManager level)
        {
            Directory.CreateDirectory("Library/GStarUIQA/" + FieldMedia + "/lemon");
            Directory.CreateDirectory("Library/GStarUIQA/" + FieldMedia + "/bubble");
            Set(level.PlayerCharacter, "isInvincible", true);
            yield return CheckOpeningWaves(level, 60, .65f, "stomach");
            yield return CheckSandBackdrop(level, "stomach");
            CheckFieldInstructions();
            yield return CheckOverchargeRoutes(level);

            var progression = StageProgression.Ensure(level);
            Check(progression.BeginDirectOral(), "enter oral stage for field balance checks");
            while (progression.Travelling) yield return null;
            Silence(level);
            var events = OralFieldEvents.Instance;
            events.AutoSchedule = false;
            foreach (var cavity in events.Cavities) { cavity.Healed = true; cavity.Active = false; }
            yield return CheckOpeningWaves(level, 52, .70f, "oral");
            yield return CheckOralRemix(level, events);
            yield return CheckGStarSpring(level, events.Tastes);
            yield return CheckSodaLifetime(level, events);
            yield return CheckBoothEncounters(level, events);

            // Travel through the existing non-exhibit bridge solely to restore the
            // stomach source blueprint and dispose oral terrain before the colon fixture.
            GStarDemo.TestOverride = false;
            Check(progression.BeginOralToStomach(), "leave oral fixture");
            while (progression.Travelling) yield return null;
            GStarDemo.TestOverride = true;
            Silence(level); Clear(level);
            yield return ColonContentSmoke.Enter(level);
            Silence(level); Clear(level);
            yield return CheckSandBackdrop(level, "colon");
            yield return CheckSummonTiers(level);
        }

        static IEnumerator CheckOpeningWaves(LevelManager level, int expected, float hpScale, string stage)
        {
            Clear(level); Set(level, "levelTime", 0f); Set(level, "bonusWaveTenths", 0);
            var map = FiniteStomachMap.Instance;
            var start = map.IsOral ? OralMapGeometry.TongueCenter(map) : map.ClampToFloor(map.WorldRect.center, 6f);
            ColonContentSmoke.Position(level.PlayerCharacter, start);
            var formation = Field(level,"waveFormation").GetValue(level);
            formation.GetType().GetMethod("Reset").Invoke(formation,null);
            // Both stage start tables contain exclusively normal rolls at t=0.
            for (int i = 0; i < 40; i++) level.SpawnMonsterFromCurrentSpawnTable();
            var actors = level.EntityManager.LivingMonsters.Where(GStarFieldBalance.IsOrdinary).ToArray();
            var opening = actors.Take(8).ToArray();
            Check(opening.Length == 8 && opening.All(m => Vector2.Distance(start,m.Position) >= 2.60f
                && Vector2.Distance(start,m.Position) <= 6.2f && map.FloorClearance(m.Position) >= .65f),
                stage + ": eight reachable opening targets inside attacking distance with clear space around the player");
            Check(opening.Select(m => (m.Position-start).normalized).Aggregate(Vector2.zero,(a,b)=>a+b).magnitude > 6,
                stage + ": one attackable group leaves a wide escape lane");
            Check(actors.Length == expected, stage + ": forty ordinary rolls spawn " + actors.Length);
            Check(actors.All(m => Mathf.Approximately(m.HP, m.Blueprint.hp * hpScale) &&
                Mathf.Approximately(m.SpawnMaxHealth, m.HP)), stage + ": fresh current and max HP match target ratio " + hpScale);
            Time.timeScale = 4;
            yield return new WaitForSeconds(6);
            Time.timeScale = 1;
            GStarUISmoke.Capture(FieldMedia + "/" + stage + "-wave", 1280, 720);
            Clear(level);
        }

        static void CheckFieldInstructions()
        {
            var toast = Object.FindObjectOfType<StageEventToastUI>();
            Check(toast != null, "field-event banner exists");
            var instruction = (UnityEngine.UI.Text)Field(toast,"instruction").GetValue(toast);
            foreach (StageEventVisualKind kind in System.Enum.GetValues(typeof(StageEventVisualKind)))
            {
                toast.ResetForStage();
                toast.ShowField(kind,"이름이 바뀐 이벤트");
                if (instruction == null) instruction = (UnityEngine.UI.Text)Field(toast,"instruction").GetValue(toast);
                Check(instruction.text == StageEventCornerArt.Instruction(kind) && instruction.text.Length > 5
                    && !instruction.text.Contains("\n"), "typed one-line action for " + kind);
            }
            toast.ShowImmediateEncounter("감염|플레이어가 직접 고른 행동");
            Check(instruction.text == "플레이어가 직접 고른 행동","encounter action overrides field event art");
            toast.ResetForStage();
        }

        static IEnumerator CheckOverchargeRoutes(LevelManager level)
        {
            var center = (Vector2)level.PlayerCharacter.Position;
            var host = new GameObject("QA blood clot at center of field");
            host.transform.position = center;
            var clot = host.AddComponent<BloodClotOvercharge>();
            Check(clot.Begin() && clot.CanProgress,"start a real blood-clot overcharge");
            for (int i=0;i<6;i++) level.SpawnMonsterFromCurrentSpawnTable();
            var group = level.EntityManager.LivingMonsters.Where(GStarFieldBalance.IsOrdinary).Take(8).ToArray();
            float radius = RemakeBalance.Current.overchargeRadius;
            Check(group.Length == 8 && group.All(m => Vector2.Distance(center,m.Position) >= radius
                && Vector2.Distance(center,m.Position) < radius + 2),
                "next eight ordinary rolls arrive at the outside of the overcharge ring");
            var left = (group[0].Position-center).normalized;
            int side = group.Count(m => Vector2.Dot(left,(m.Position-center).normalized) > .65f);
            Check(side == 4,"four ordinary rolls per entrance, leaving space between both approaches");
            Object.Destroy(host); Clear(level); yield return null;
        }

        static IEnumerator CheckGStarSpring(LevelManager level, OralTasteEvents taste)
        {
            var map = FiniteStomachMap.Instance; var player = level.PlayerCharacter;
            var tongue = OralMapGeometry.TongueCenter(map);
            Place(player, tongue); yield return new WaitForSeconds(.7f);
            Check(taste.Begin(OralTasteKind.Sour), "start lemon field interaction");
            var start = taste.LemonPosition;
            var target = OralMapGeometry.ToothWalkCenter(map, OralMapGeometry.NearestTooth(map, start));
            var foe = level.EntityManager.SpawnMonster(0, start, level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0]);
            var monsterTarget = OralMapGeometry.Clamp(map, start + taste.LemonDirection * taste.LemonBounceDistance, .55f, OralZone.Tongue);
            var enemyBounce = foe.GetComponent<OralTasteBounce>();
            Place(player, start + Vector2.down * (taste.LemonLength * .42f + .15f));
            Time.captureFramerate = 15;
            bool launched = false, enemyLaunched = false, enemyLanded = false;
            bool metersTracked = true; int airborneSamples = 0;
            var meters = Object.FindObjectOfType<PlayerCombatBars>();
            var playerArt = player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
            for (int frame = 0; frame < 38; frame++)
            {
                if (frame >= 6 && !launched) player.Move(Vector2.up);
                else player.Move(Vector2.zero);
                yield return null;
                if (enemyBounce == null) enemyBounce = foe.GetComponent<OralTasteBounce>();
                if (enemyBounce != null && enemyBounce.Active) enemyLaunched = true;
                if (enemyLaunched && !enemyLanded && !enemyBounce.Active)
                {
                    Check(Vector2.Distance(foe.Position, monsterTarget) < .5f, "monster keeps its tongue recoil distance");
                    enemyLanded = true;
                }
                var flight = player.GetComponent<OralTasteBounce>();
                if (!launched && flight != null && flight.Active)
                {
                    var takeoff = (Vector2)Field(flight, "from").GetValue(flight);
                    target = OralMapGeometry.ToothWalkCenter(map, OralMapGeometry.NearestTooth(map, takeoff));
                    launched = true;
                }
                if (flight != null && flight.Active && flight.VisualWorldOffset.y > .25f)
                {
                    var center = Camera.main.WorldToScreenPoint(PromptSpriteGeometry.WorldBounds(playerArt).center);
                    metersTracked &= meters.HealthRect.position.y > center.y && meters.DashRect.position.y < center.y;
                    airborneSamples++;
                }
                GStarUISmoke.Capture(FieldMedia + "/lemon/" + frame.ToString("D3"), 960, 540);
            }
            Time.captureFramerate = 0;
            Check(launched && enemyLanded, "spring launches both actors");
            Check(airborneSamples > 2 && metersTracked, "HP and dash meters bracket the airborne character throughout fast travel");
            Check(Vector2.Distance(player.Position, target) < .12f && OralMapGeometry.ZoneAt(map, player.Position) == OralZone.Teeth &&
                player.GetComponent<Rigidbody2D>().simulated, "player rests on nearest walkable tooth with normal physics restored");
            taste.Configure(level); Clear(level); Place(player, tongue);
        }

        static IEnumerator CheckSodaLifetime(LevelManager level, OralFieldEvents events)
        {
            var player = level.PlayerCharacter;
            var bp = level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];
            var monster = level.EntityManager.SpawnMonster(0, player.Position + Vector2.right * 2.5f, bp);
            // Hold the subject beside the player for a readable shield-expiry shot.
            var body = monster.GetComponent<Rigidbody2D>();
            var constraints = body.constraints;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            Check(events.ForceEvent(OralEventKind.Soda), "start real carbonation wave");
            float until = Time.time + 10;
            MonsterNeedleBarrier bubble = null;
            while ((bubble == null || !bubble.SodaTint) && Time.time < until)
            { yield return null; bubble = monster.GetComponent<MonsterNeedleBarrier>(); }
            Check(bubble != null && bubble.SodaTint && bubble.IsProtected, "wave contact grants soda shield");
            float granted = Time.time;
            GStarUISmoke.Capture(FieldMedia + "/soda-coated", 1280, 720);
            Time.timeScale = 3;
            while (Time.time - granted < 19.15f) yield return null;
            Check(bubble.IsProtected && bubble.Hits == 0, "untouched soda shield still protects before 20 seconds");
            Time.timeScale = 1; Time.captureFramerate = 15;
            float expiredAt = -1;
            for (int frame = 0; frame < 36; frame++)
            {
                yield return null;
                if (!bubble.IsProtected && expiredAt < 0) expiredAt = Time.time - granted;
                GStarUISmoke.Capture(FieldMedia + "/bubble/" + frame.ToString("D3"), 960, 540);
            }
            Time.captureFramerate = 0;
            Check(expiredAt > 19.65f && expiredAt < 20.35f && !bubble.VisualRoot.gameObject.activeSelf &&
                !bubble.Surface.enabled && bubble.Hits == 0, "untouched shield expires and clears collision and art at " + expiredAt.ToString("F2") + "s");
            body.constraints = constraints;
        }

        static IEnumerator CheckSummonTiers(LevelManager level)
        {
            for (int i = 1; i <= 10; i++)
            {
                Check(GStarFieldBalance.SummonTier(i * 90 - .01f) == i - 1 && GStarFieldBalance.SummonTier(i * 90) == i,
                    "90-second boundary " + i);
            }
            Check(GStarFieldBalance.SummonTier(9000) == 10, "post-15-minute cap");
            var director = level.GetComponent<ColonBossDirector>();
            var map = FiniteStomachMap.Instance;
            var layout = level.GetComponent<FieldObjectLayout>();
            var entry = map.ClampToField(map.UVToWorld(new Vector2(.78f,.72f)),map.WallWidth+2);
            var rebuilt = ColonBossDirector.Generate(map,entry,director.Seed,layout.Slots);
            Check(rebuilt.Count == 6 && director.Placements.Count == rebuilt.Count &&
                rebuilt.Select((h,i) => h.position == director.Placements[i].position &&
                    h.kind == director.Placements[i].kind && h.wallNormal == director.Placements[i].wallNormal).All(same => same),
                "all six instantiated habitats match the independently rebuilt seeded layout");
            var habitat = director.Habitats.First(h => h.Kind == ColonBossKind.Pudding);
            ColonContentSmoke.Position(level.PlayerCharacter, habitat.transform.position);
            Set(level, "levelTime", 89.9f);
            Object.FindObjectOfType<GameTimer>()?.SetTime(level.LevelDuration - level.CurrentLevelTime);
            Check(director.TrySummon(habitat, level.PlayerCharacter), "summon pudding before first boundary");
            var boss = ColonBossRuntime.Active.Single(b => b.Habitat == habitat); boss.AutoPatterns = false;
            while (boss.Arriving) yield return null;
            yield return null;
            GStarUISmoke.Capture(FieldMedia + "/early-miniboss", 1280, 720);
            Set(level, "levelTime", 450f);
            Check(boss.SummonTier == 0 && boss.Health == 2400 && boss.MaxHealth == 2400, "first-tier pudding stays at 2400 after advancing to 7:30");
            Object.Destroy(boss.gameObject); yield return null;

            var balletHome = director.Habitats.First(h => h.Kind == ColonBossKind.Flamingo);
            ColonContentSmoke.Position(level.PlayerCharacter,balletHome.transform.position);
            Check(director.TrySummon(balletHome,level.PlayerCharacter),"wake the flamingo from its habitat");
            var flamingo = ColonBossRuntime.Active.Single(b => b.Habitat == balletHome);
            flamingo.AutoPatterns = false;
            while (flamingo.Arriving) yield return null;
            Check(flamingo.PlayPattern(0),"flamingo starts the arabesque dash");
            float balletUntil = Time.time + 5f, caught = -1f;
            while (flamingo.Busy && Time.time < balletUntil)
            {
                if (caught < 0 && flamingo.Visual.BalletLanding > .05f && Mathf.Abs(flamingo.Visual.Roll) > .25f)
                {
                    caught = Time.time;
                    GStarUISmoke.Capture(FieldMedia + "/flamingo-opening",1280,720);
                }
                yield return null;
            }
            Check(caught >= 0 && !flamingo.Busy && Time.time-caught >= .7f && Mathf.Abs(flamingo.Visual.Roll) < .01f,
                "flamingo wobbles on landing, opens a counterattack window, and returns upright");
            Object.Destroy(flamingo.gameObject); yield return null;

            var kingDirector = ConstipationKingDirector.Ensure(level);
            Object.FindObjectOfType<GameTimer>()?.SetTime(level.LevelDuration - level.CurrentLevelTime);
            ColonContentSmoke.Position(level.PlayerCharacter, kingDirector.Throne.transform.position);
            Check(kingDirector.TrySummon(level.PlayerCharacter), "summon king from actual throne at 7:30");
            var king = kingDirector.Boss; king.AutoPatterns = false;
            while (king.Arriving) yield return null;
            yield return new WaitForSeconds(4);
            GStarUISmoke.Capture(FieldMedia + "/king", 1280, 720);
            Set(level, "levelTime", 900f);
            Check(king.SummonTier == 5 && king.Health == 13500 && king.MaxHealth == 13500, "7:30 king keeps 13500 HP after clock crosses 15 minutes");
            Check(king.PlayPattern(7),"king requests tribute through the real pattern");
            float until = Time.time + 4f;
            ConstipationKingMinion[] carriers;
            do
            {
                yield return null;
                carriers = Object.FindObjectsOfType<ConstipationKingMinion>().Where(m => m.IsTribute).ToArray();
            } while (carriers.Length < 3 && Time.time < until);
            Check(carriers.Length == 3,"three tribute carriers can be intercepted");
            foreach (var carrier in carriers) carrier.TakeDamage(carrier.MaxHealth);
            until = Time.time + 2;
            while (king.Visual.Pose != KingPose.EmptyTray && Time.time < until) yield return null;
            Check(king.Visual.Pose == KingPose.EmptyTray && king.Health == 13500, "three denied gifts leave the king exposed with an empty tray");
            GStarUISmoke.Capture(FieldMedia + "/denied-tribute",1280,720);
            king.TakeDamage(king.MaxHealth);
            Check(king.Health == 6750 && king.Transitioning && !king.Dead, "king phase gate uses summoned HP");
        }
    }
}
#endif
