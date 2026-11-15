#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static class ColonChestnutLifecycleReview
    {
        // Invoke after the real 1 -> 3 -> 1 presentation, with the live hedgehog idle.
        public static IEnumerator Run(LevelManager level, ColonBossRuntime boss, Action<bool,string> check)
        {
            var player = level.PlayerCharacter; var playerBefore = player.transform.position;
            var target = FiniteStomachMap.SafeSpawn((Vector2)boss.transform.position + Vector2.right * 16, .4f);
            ColonContentSmoke.Position(player, target); boss.Visual.Pose = 6; boss.Visual.Bare = true;
            var entry = ColonMonsterRoster.Active.Specials.Find(e => e.id == "S02");
            int pool = -1;
            for (int i = 0; i < level.CurrentLevelBlueprint.monsters.Length; i++)
                foreach (var bp in level.CurrentLevelBlueprint.monsters[i].monsterBlueprints)
                    if (ColonMonsterRoster.Active.Resolve(bp) == entry.blueprint) pool = i;
            check(pool >= 0, "S02 recall uses the real stage-two special-monster pool");
            ExplodingMonster Spawn() => level.EntityManager.SpawnMonster(pool,
                FiniteStomachMap.SafeSpawn((Vector2)boss.transform.position + Vector2.down, .4f), entry.blueprint, 0, false) as ExplodingMonster;
            var rack = ColonChestnutRack.Create(boss);
            var nut = Spawn(); check(nut != null, "real S02 exploding monster was spawned");
            nut.TakeItemPureDamage(Mathf.Min(1,nut.HP*.1f)); float hp = nut.HP; uint generation = nut.SpawnGeneration;
            var source = nut.BodyRenderer.sprite; var rb = nut.GetComponent<Rigidbody2D>(); var bodyType = rb.bodyType;
            int score = RunScore.Current.Data.special;
            var carrier = ColonChestnutRecall.Begin(boss, rack, nut, 0);
            float until = Time.time + 4; while (carrier != null && !carrier.Docked && Time.time < until) yield return null;
            yield return null; yield return null;
            check(carrier != null && carrier.Docked, "living S02 walked and climbed into a spine socket");
            check(nut.SpawnGeneration == generation && Mathf.Approximately(nut.HP, hp) && nut.BodyRenderer.sprite == source,
                "recall preserves instance generation, wounded HP and original S02 sprite");
            check(nut.IsCarrierControlled && !nut.IsFieldRuntimeSuspended && nut.BodyHitbox.enabled,
                "carried S02 keeps a targetable hitbox and accepts native damage/status effects");
            check(Vector2.Distance(nut.BodyRenderer.transform.position, boss.Visual.ChestnutSocket(0)) < .08f,
                "the actual S02 body occupies its authored spine socket");
            var playerCollider = player.GetComponentsInChildren<Collider2D>().FirstOrDefault(c => c != player.CollectableCollider && c.enabled);
            typeof(ExplodingMonster).GetMethod("TryExplodeFromCollider", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(nut, new object[]{playerCollider});
            check(nut.IsAlive && Mathf.Approximately(nut.HP, hp), "carried contact cannot trigger autonomous self-explosion");
            carrier.Depart((Vector2)boss.transform.position + Vector2.left * 2.4f, Vector2.left);
            until = Time.time + 1; while (carrier != null && carrier.Holding && Time.time < until) yield return null;
            check(nut.SpawnGeneration == generation && Mathf.Approximately(nut.HP, hp) && nut.IsAlive && !nut.IsCarrierControlled && nut.HasLockedTrajectory,
                "departure releases the same wounded living S02 onto its native trajectory");
            check(rb.bodyType == bodyType && RunScore.Current.Data.special == score,
                "carry/departure restores native physics and grants zero kill score");
            nut.DespawnForStageTransfer(); yield return null;

            nut = Spawn(); carrier = ColonChestnutRecall.Begin(boss, rack, nut, 1);
            until = Time.time + 4; while (carrier != null && !carrier.Docked && Time.time < until) yield return null;
            check(carrier != null && carrier.Docked, "second S02 docks for death/reward regression");
            int deaths = 0; score = RunScore.Current.Data.special;
            Action<Monster> onDeath = m => { if (m == nut) deaths++; }; Monster.Died += onDeath;
            try
            {
                nut.TakeItemPureDamage(nut.HP + 100); nut.TakeItemPureDamage(100);
                yield return null; yield return null;
                check(deaths == 1 && RunScore.Current.Data.special == score + 1,
                    "killing a carried S02 calls one native death and exactly one special-monster reward");
                check(carrier == null || !carrier.Holding, "dead chestnuts cannot remain attached or be resurrected by recall");
            }
            finally { Monster.Died -= onDeath; }
            if (nut != null && nut.gameObject.activeSelf) nut.DespawnForStageTransfer(); yield return null;

            nut = Spawn(); carrier = ColonChestnutRecall.Begin(boss, rack, nut, 2); hp = nut.HP; generation = nut.SpawnGeneration;
            until = Time.time + 4; while (carrier != null && !carrier.Docked && Time.time < until) yield return null;
            check(carrier != null && carrier.Docked, "third S02 docks for encounter-interruption regression");
            bool enabledBefore = boss.enabled; boss.enabled = true; boss.gameObject.SetActive(false); yield return null;
            check(nut.IsAlive && !nut.IsCarrierControlled && nut.SpawnGeneration == generation && Mathf.Approximately(nut.HP, hp),
                "boss interruption releases its real living chestnut without health/reward mutation");
            check(nut.GetComponent<Rigidbody2D>().bodyType == bodyType && nut.BodyHitbox.enabled,
                "interruption restores the chestnut's native body and hitbox");
            boss.gameObject.SetActive(true); boss.enabled = enabledBefore; yield return null;
            nut.DespawnForStageTransfer(); yield return null;

            // Exercise the actual generation-reset API with an old control component still
            // queued for destruction: it may never overwrite a fresh spawn's state.
            nut = Spawn(); rack = ColonChestnutRack.Create(boss); carrier = ColonChestnutRecall.Begin(boss, rack, nut, 0);
            generation = nut.SpawnGeneration; nut.PrepareForSpawnRuntime(false); nut.SetCarrierControlled(false);
            nut.GetComponent<Rigidbody2D>().bodyType = bodyType;
            yield return null; yield return null;
            check(nut.SpawnGeneration != generation && !nut.IsCarrierControlled && (carrier == null || !carrier.Holding),
                "stale carrier discards itself after the real spawn generation is replaced");
            nut.DespawnForStageTransfer(); yield return null;

            nut = Spawn();
            int nativeOrder=nut.BodyRenderer.sortingOrder,nativeLayer=nut.BodyRenderer.sortingLayerID;
            var nativeShadows=nut.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r!=nut.BodyRenderer&&r.name.ToLowerInvariant().Contains("shadow")).ToArray();
            var nativeShadowStates=nativeShadows.Select(r=>r.enabled).ToArray();
            carrier = ColonChestnutRecall.Begin(boss, rack, nut, 0); generation = nut.SpawnGeneration;
            until=Time.time+4;while(carrier!=null&&!carrier.Docked&&Time.time<until)yield return null;
            yield return null;yield return null;
            check(carrier!=null&&carrier.Docked,"pool-return test first reaches the carried sorting/shadow state");
            // The pool's exact same-frame disable -> enable -> Prepare -> Setup sequence,
            // on the same object, while Unity still queues the old component for Destroy.
            nut.gameObject.SetActive(false);
            check(nut.BodyRenderer.sortingOrder==nativeOrder&&nut.BodyRenderer.sortingLayerID==nativeLayer&&
                nativeShadows.Select((r,i)=>r.enabled==nativeShadowStates[i]).All(v=>v),
                "inactive pool return restores original render order, layer and shadow visibility");
            nut.gameObject.SetActive(true); nut.PrepareForSpawnRuntime(false);
            level.EntityManager.LivingMonsters.Remove(nut); nut.Setup(pool,nut.transform.position,entry.blueprint,0);
            nut.GetComponent<ColonMonsterVisual>().Configure(nut,entry);
            check(nut.GetComponent<CircleCollider2D>().enabled&&nut.BodyRenderer.sortingOrder==nativeOrder&&
                nativeShadows.Select((r,i)=>r.enabled==nativeShadowStates[i]).All(v=>v),
                "same-frame native respawn restores feet and retains original visual/shadow state");
            var freshCarrier = ColonChestnutRecall.Begin(boss,rack,nut,1);
            check(nut.SpawnGeneration != generation && freshCarrier != null && freshCarrier != carrier,
                "same-frame pool reuse accepts new recall despite a released old component");
            yield return null; yield return null;
            check(freshCarrier != null && freshCarrier.Holding && nut.IsCarrierControlled,
                "old component destruction cannot cancel the new spawn's carrier control");
            freshCarrier.Cancel(); nut.DespawnForStageTransfer(); rack.Restore(); Object.Destroy(rack.gameObject);
            boss.Visual.ExternalChestnuts = false; boss.Visual.ResetPose(); ColonContentSmoke.Position(player, playerBefore);
        }
    }
}
#endif
