using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static partial class ChestLootSmoke
    {
        static bool UseHourglass => DayThreeBatch ? BatchPass == 1 : !Environment.GetCommandLineArgs().Contains("-hourglassControl");
        static string HourglassFolder => UseHourglass ? "hourglass-on" : "hourglass-off";

        static IEnumerator HourglassChecks(LevelManager level)
        {
            var player = level.PlayerCharacter;
            var entities = level.EntityManager;
            var slot = entities.Inventory.Slots.Single(x => x.CollectableType is RedPotionCollectable);
            var blueprint = UnityEditor.AssetDatabase.LoadAssetAtPath<ChestBlueprint>("Assets/Blueprints/Chests/Default Chest.asset");
            var chestType = Object.Instantiate(blueprint);
            var drop = blueprint.lootTable.lootTable.First(x=>x.item != null && x.item.GetComponent<RedPotion>() != null);
            chestType.lootTable = new LootTable<GameObject> { lootTable = new[]{ new Loot<GameObject>{item=drop.item,dropChance=1} } };
            var origin = player.Position;
            Vector2 site = FiniteStomachMap.SafeSpawn(origin + Vector2.right * 2.3f, .4f);
            var chest = entities.SpawnChest(chestType,site);
            while (!chest.CanInteract) yield return null;
            int frame=0;
            Directory.CreateDirectory(Output+"/frames");
            if (UseHourglass)
                foreach (string previous in Directory.GetFiles(Output+"/frames","*.png")) File.Delete(previous);
            var baseAttack = player.AttackSpeedMultiplier;
            var baseMove = player.CurrentMoveSpeed;
            chest.OpenChest();
            int before=slot.Count;
            float wait=0;
            while (slot.Count==before && wait<7f)
            {
                yield return null;
                wait+=Time.deltaTime;
                if (UseHourglass && Time.frameCount%4==0)
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/frames/"+(frame++).ToString("D4")+".png"));
            }
            Check(slot.Count==before+1 && level.HourglassRemaining==0,"chest delivers a stored hourglass without walking");
            Check(slot.IconImage.sprite!=null && slot.IconImage.sprite.name.StartsWith("broken-hourglass-"),"hourglass slot uses the new sand art");
            var normalSeed = 45817;
            UnityEngine.Random.InitState(normalSeed);
            Set(level,"levelTime",207f);
            var from=level.CurrentLevelTime;
            int startSpawn=level.NormalMonstersSpawned;
            int startLevel=player.CurrentLevel;
            float startHp=player.CurrentHealth;
            if (UseHourglass)
            {
                float previousScale=Time.timeScale;
                Time.timeScale=0;
                slot.UsePointerItem();
                Check(slot.Count==before+1 && level.HourglassRemaining==0,"a blocked use keeps the stored item");
                Time.timeScale=previousScale;
                slot.UsePointerItem();
                Check(slot.Count==before && Mathf.Approximately(level.HourglassRemaining,20f),"actual inventory use activates twenty seconds");
                Check(!level.ActivateHourglass(),"second use cannot extend an active hourglass");
                Check(Mathf.Abs(player.AttackSpeedMultiplier/baseAttack-1.12f)<.001f &&
                      Mathf.Abs(player.CurrentMoveSpeed/baseMove-1.15f)<.001f,"live player coefficients");
                level.SetRunFlowPaused(true);
                for(int i=0;i<10;i++)yield return null;
                Check(Mathf.Abs(level.HourglassRemaining-20f)<.001f && Mathf.Abs(level.CurrentLevelTime-from)<.001f,
                    "paused field freezes clock and boost");
                level.SetRunFlowPaused(false);
            }
            float elapsed=0, nearbySeconds=0, exposure=0, stepDistance=0;
            int hpLossFrames=0;
            Vector2 prev=origin;
            float prevHp=startHp;
            var dialog=entities.AbilitySelectionDialog;
            Time.captureDeltaTime=1f/30;
            while(elapsed<20.5f)
            {
                if(dialog.MenuOpen)
                {
                    if(!dialog.SelectionAnimating)
                    {
                        var card=dialog.GetComponentsInChildren<AbilityCard>().FirstOrDefault();
                        if(card!=null)card.Selected();
                    }
                    yield return null;
                    continue;
                }
                var goal=origin+new Vector2(Mathf.Sin(elapsed*.42f)*4f,Mathf.Sin(elapsed*.26f)*2.5f);
                player.Move((goal-player.Position).normalized);
                yield return null;
                if (Time.deltaTime<=0 || level.IsRunFlowPaused) continue;
                if (!player.IsAlive || level.IsLevelEnded) throw new InvalidOperationException("Paired run ended before the twenty-second item window");
                elapsed+=Time.deltaTime;
                stepDistance+=Vector2.Distance(prev,player.Position);prev=player.Position;
                if(player.CurrentHealth+.01f<prevHp)hpLossFrames++;
                prevHp=player.CurrentHealth;
                int nearby=0;
                foreach(var mob in entities.LivingMonsters)
                    if(mob!=null && mob.IsAlive && (mob.CenterTransform.position-(Vector3)player.Position).sqrMagnitude<6.25f) nearby++;
                nearbySeconds+=Time.deltaTime*nearby;
                if(nearby>0)exposure+=Time.deltaTime;
                if(UseHourglass && elapsed<5f && Time.frameCount%3==0)
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/frames/"+(frame++).ToString("D4")+".png"));
            }
            float advance=level.CurrentLevelTime-from;
            int spawns=level.NormalMonstersSpawned-startSpawn;
            Check(Mathf.Abs(advance-elapsed-(UseHourglass?20f:0f))<.18f,"verified stage seconds against unaccelerated field seconds");
            Check(level.HourglassRemaining<.001f && !player.HourglassActive,"pace expires");
            Check(spawns>0,"ordinary field director generated real monsters");
            string csv="used,seed,field_seconds,clock_seconds,natural_spawns,avg_mobs_within_2_5,seconds_with_mob_in_range,damage_events,distance,level_before,level_after,hp_before,hp_after\n"+
                FormattableString.Invariant($"{UseHourglass},{normalSeed},{elapsed:F3},{advance:F3},{spawns},{nearbySeconds/elapsed:F3},{exposure:F3},{hpLossFrames},{stepDistance:F2},{startLevel},{player.CurrentLevel},{startHp:F1},{player.CurrentHealth:F1}");
            File.WriteAllText(Output+"/gameplay.csv",csv);
            Object.Destroy(chestType);
            if (UseHourglass) yield return HourglassStatusCombinations(level);
        }

        // Run after the paired combat measurement and its CSV, so these deliberately
        // isolated status probes never change the encounter counts, RNG or damage sample.
        static IEnumerator HourglassStatusCombinations(LevelManager level)
        {
            // The live 20-second combat sample is already recorded. This separate
            // expiry/stack-order fixture must keep the world running while the
            // stationary character waits for two unrelated status timers.
            var player = level.PlayerCharacter;
            player.AddMaxHealthBonus(100000);
            Set(player, "currentHealth", player.MaxHealth);
            Set(player, "nextLevelExp", 100000f);
            player.CollectableCollider.enabled = false;
            var dialog = level.EntityManager.AbilitySelectionDialog;
            while (dialog.MenuOpen)
            {
                if (!dialog.SelectionAnimating) dialog.GetComponentsInChildren<AbilityCard>().FirstOrDefault()?.Selected();
                yield return null;
            }
            var pools = level.CurrentLevelBlueprint.monsters;
            int chameleonPool = Array.FindIndex(pools, p => p.monstersPrefab != null && p.monstersPrefab.GetComponent<AcidToadMonster>() != null);
            int ordinaryPool = Array.FindIndex(pools, p => p.monstersPrefab != null && p.monstersPrefab.GetComponent<Monster>()?.GetType() == typeof(MeleeMonster) &&
                p.monsterBlueprints.Any(bp => bp is MeleeMonsterBlueprint && !(bp is EliteMonsterBlueprint)));
            Check(chameleonPool >= 0 && ordinaryPool >= 0, "real stomach chameleon and ordinary pools available");
            var ordinaryType = pools[ordinaryPool].monsterBlueprints.First(bp => bp is MeleeMonsterBlueprint && !(bp is EliteMonsterBlueprint));
            var entities = level.EntityManager;
            Vector2 site = FiniteStomachMap.SafeSpawn(level.PlayerCharacter.Position + Vector2.right * 22, .9f);
            var chameleon = (AcidToadMonster)entities.SpawnMonster(chameleonPool, site, pools[chameleonPool].monsterBlueprints.First(bp => bp != null), 0);
            var before = entities.SpawnMonster(ordinaryPool, site + Vector2.up, ordinaryType, 0);
            var during = entities.SpawnMonster(ordinaryPool, site + Vector2.down, ordinaryType, 0);
            chameleon.AutoPatterns = false;
            var camelBody = chameleon.GetComponent<Rigidbody2D>();
            var bodyA = before.GetComponent<Rigidbody2D>();
            var bodyB = during.GetComponent<Rigidbody2D>();
            var honeyA = before.GetComponent<HoneySlowStatus>() ?? before.gameObject.AddComponent<HoneySlowStatus>();
            var honeyB = during.GetComponent<HoneySlowStatus>() ?? during.gameObject.AddComponent<HoneySlowStatus>();
            foreach (var probe in new Monster[]{chameleon,before,during}) probe.SetFieldRuntimeSuspended(true);
            float baseA = bodyA.drag, baseB = bodyB.drag;
            Check(baseA > 0 && baseB > 0 && camelBody.drag == 0, "chameleon retains authored zero drag before item use");
            const float slow = .5f;
            float honeyFactor = 1 / (slow * slow);
            float fieldFactor = 1 / LevelManager.HourglassMonsterSpeed;
            honeyA.Apply(25, slow, false);
            Check(Mathf.Approximately(bodyA.drag, baseA * honeyFactor), "existing honey applies its full drag before the boost");
            Check(level.ActivateHourglass(), "second independent field boost starts through normal activation");
            honeyB.Apply(1, slow, false);
            Check(camelBody.drag == 0 && Mathf.Approximately(bodyA.drag, baseA * honeyFactor * fieldFactor) &&
                Mathf.Approximately(bodyB.drag, baseB * honeyFactor * fieldFactor), "both application orders compose and chameleon drag stays zero");
            level.SetRunFlowPaused(true);
            Check(camelBody.drag == 0 && Mathf.Approximately(bodyA.drag, baseA * honeyFactor), "pausing strips only field pace");
            level.SetRunFlowPaused(false);
            Check(Mathf.Approximately(bodyA.drag, baseA * honeyFactor * fieldFactor), "resuming restores both live modifiers");

            float savedScale=Time.timeScale, savedCapture=Time.captureDeltaTime;
            Time.captureDeltaTime=1f/12; Time.timeScale=2;
            float timeout=Time.realtimeSinceStartup+75;
            bool sawHoneyFirst=false, sawClockFirst=false;
            try
            {
                while(honeyA.IsActive || level.HourglassRemaining>0)
                {
                    if (!player.IsAlive || level.IsLevelEnded)
                        throw new InvalidOperationException("The isolated timing fixture ended the run before expiry");
                    if (Time.realtimeSinceStartup >= timeout)
                        throw new TimeoutException("Combined expiry: field=" + level.CurrentLevelTime + " clock=" + level.HourglassRemaining +
                            " honey=" + honeyA.IsActive + " paused=" + level.IsRunFlowPaused + " timeScale=" + Time.timeScale);
                    if(dialog.MenuOpen && !dialog.SelectionAnimating)
                        dialog.GetComponentsInChildren<AbilityCard>().FirstOrDefault()?.Selected();
                    if(!honeyB.IsActive && level.HourglassRemaining>0 && level.PlayerCharacter.HourglassActive && !sawHoneyFirst)
                    {
                        Check(Mathf.Approximately(bodyB.drag,baseB*fieldFactor), "short honey restores the still-active hourglass base");
                        sawHoneyFirst=true;
                    }
                    if(honeyA.IsActive && level.HourglassRemaining<=0 && !sawClockFirst)
                    {
                        Check(Mathf.Approximately(bodyA.drag,baseA*honeyFactor) && camelBody.drag==0,
                            "hourglass expiry keeps long honey and preserves independent chameleon movement");
                        sawClockFirst=true;
                    }
                    yield return null;
                }
                Check(sawHoneyFirst && sawClockFirst && Mathf.Approximately(bodyA.drag,baseA) &&
                    Mathf.Approximately(bodyB.drag,baseB) && camelBody.drag==0, "both orders finish at the unmodified movement base");

                honeyA.Apply(5, slow, false);
                before.DespawnForStageTransfer();
                var recycled=entities.SpawnMonster(ordinaryPool, site, ordinaryType, 0);
                Check(recycled==before && !honeyA.IsActive, "pool reuse clears an unfinished honey slow");
                float freshDrag=bodyA.drag;
                honeyA.Apply(3,slow,false);
                Check(freshDrag>0 && Mathf.Approximately(bodyA.drag,freshDrag*honeyFactor), "reused monster applies honey to its new spawn drag");
                honeyA.enabled=false;
                Check(Mathf.Approximately(bodyA.drag,freshDrag), "disabling reused slow restores this spawn's own drag");
            }
            finally
            {
                Time.captureDeltaTime=savedCapture; Time.timeScale=savedScale;
            }
        }
    }
}
