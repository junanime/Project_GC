#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    // Boot through OralTasteContentSmoke.Run -oralGuardEscort. Tests use the
    // actual stage, actual monster pools, actual projectile collision and rewards.
    public static class OralGuardEscortSmoke
    {
        const string Root = "Library/OralGuardEscortProof";
        static void Check(bool condition, string text)
        {
            if (!condition) throw new Exception("ORAL_GUARD_FAIL " + text);
            File.AppendAllText(Root + "/checks.txt", "PASS " + text + "\n");
        }
        static void Set(object owner, string name, object value)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) { field.SetValue(owner, value); return; }
            }
            throw new MissingFieldException(name);
        }
        static void Clear(LevelManager level)
        {
            foreach (var m in level.EntityManager.LivingMonsters.ToArray()) if (m != null) m.DespawnForStageTransfer();
            foreach (var p in UnityEngine.Object.FindObjectsOfType<Projectile>()) p.RemoveForItem();
        }
        public static IEnumerator Checks(LevelManager level)
        {
            Directory.CreateDirectory(Root); File.WriteAllText(Root + "/checks.txt", "");
            var ambient = OralAmbientContent.Instance; var map = FiniteStomachMap.Instance; var player = level.PlayerCharacter;
            var center = OralMapGeometry.TongueCenter(map); Clear(level); ambient.ClearAll();
            Check(ambient.BeginGuard(), "five mint guards start");
            var guard = ambient.Guard;
            Check(guard.GetComponentsInChildren<OralGuardMember>().Length == 5, "five independently hittable members");
            Check(guard.Health.Current == 80 && Mathf.Approximately(OralGuardEscort.MoveSpeed, 1.2f), "shared HP80 and speed1.2");
            var route = guard.Route; var start = guard.Position; float age = guard.Age;
            yield return new WaitForSeconds(.5f);
            Check(Mathf.Abs(Vector2.Distance(start, guard.Position) / (guard.Age - age) - 1.2f) < .03f, "actual motion follows speed1.2");
            Check(guard.Route.Tooth == route.Tooth, "fixed random molar does not follow player");
            age = guard.Age; level.SetRunFlowPaused(true); yield return new WaitForSecondsRealtime(.2f);
            Check(guard.Age == age && !guard.TakeMemberDamage(4, 8), "pause stops movement and damage"); level.SetRunFlowPaused(false);
            yield return null;
            var group = level.CurrentLevelBlueprint.monsters.Select((g, i) => new { g, i })
                .First(x => x.g.monsterBlueprints.Any(b => b is MeleeMonsterBlueprint && OralMonsterRoster.IsBlueprint(b)));
            var blueprint = group.g.monsterBlueprints.First(b => b is MeleeMonsterBlueprint && OralMonsterRoster.IsBlueprint(b));
            var enemy = level.EntityManager.SpawnMonster(group.i, guard.MemberPosition(4) + Vector2.right * .05f, blueprint, 0, false);
            Check(OralGuardEscort.TryTarget(enemy, center + Vector2.right * 8, out var selected) &&
                Vector2.Distance(selected, guard.MemberPosition(4)) < .1f, "enemy acquires closest tail member");
            float hp = guard.Health.Current;
            float timeout = Time.time + 2;
            while (guard.Health.Current >= hp && Time.time < timeout) yield return null;
            Check(guard.Health.Current < hp, "actual melee contact hurts tail and shared health");
            // Put the player and a walking guard on opposite sides of the same
            // enemy: its final rendered facing must follow the escort target.
            Vector2 facingGuard = Enumerable.Range(0, 5).Where(i => guard.MemberWalking(i))
                .Select(i => guard.MemberPosition(i)).OrderBy(p => p.x).First();
            Vector2 playerBeforeFacing = player.Position;
            OralStageRuntime.Instance.TeleportPlayer(facingGuard + Vector2.left * 3.5f, OralZone.Tongue);
            var enemyBody = enemy.GetComponent<Rigidbody2D>();
            enemy.transform.position = facingGuard + Vector2.left * .8f;
            enemyBody.position = enemy.transform.position; enemyBody.velocity = Vector2.zero;
            Physics2D.SyncTransforms(); yield return new WaitForSeconds(.12f);
            Check(OralGuardEscort.TryTarget(enemy, player.Position, out var facingTarget) &&
                (player.Position.x - enemy.Position.x) * (facingTarget.x - enemy.Position.x) < 0,
                "facing fixture keeps actual player opposite the selected guard");
            Check(enemy.BodyRenderer.flipX == (facingTarget.x < enemy.Position.x),
                "rendered melee facing follows guard instead of being overwritten toward player");
            OralStageRuntime.Instance.TeleportPlayer(playerBeforeFacing, OralZone.Tongue);
            Clear(level);
            yield return new WaitForSeconds(.8f);
            var tail = guard.GetComponentsInChildren<OralGuardMember>()[4]; hp = guard.Health.Current;
            tail.TakeDamage(999); Check(guard.Health.Current == hp, "generic player damage cannot hurt escort");
            var ranged = AssetDatabase.FindAssets("t:RangedMonsterBlueprint").Select(id =>
                AssetDatabase.LoadAssetAtPath<RangedMonsterBlueprint>(AssetDatabase.GUIDToAssetPath(id)))
                .First(b => b.projectilePrefab != null && b.projectilePrefab.GetComponent<Projectile>() != null);
            int pool = level.EntityManager.AddPoolForProjectile(ranged.projectilePrefab);
            var shot = level.EntityManager.SpawnProjectile(pool, guard.MemberPosition(4) + Vector2.right * .65f + Vector2.up * .15f,
                9, 0, 6, 1 << LayerMask.NameToLayer("Player Full"));
            shot.SetSourceMonster(ranged); shot.Launch(Vector2.left);
            timeout = Time.time + 1.5f;
            while (guard.Health.Current >= hp && Time.time < timeout) yield return null;
            Check(guard.Health.Current <= hp - 9, "actual sourced enemy projectile hits neutral guard collider"); Clear(level);
            yield return new WaitForSeconds(.8f);
            int failed = ambient.GuardFailed, completed = ambient.GuardCompleted;
            Check(guard.TakeMemberDamage(4, 999), "tail can cause lethal escort failure"); yield return new WaitForSeconds(1.3f);
            Check(!ambient.GuardActive && ambient.GuardFailed == failed + 1 && ambient.GuardCompleted == completed && ambient.GuardReward == null,
                "failed escort removes all members and creates no chest");

            Check(ambient.BeginGuard(), "new escort after failure starts independently"); guard = ambient.Guard;
            var fixedGoal = guard.Route.Landing; int frames = 0; bool sawPartial = false;
            timeout = Time.time + guard.Lifetime + 3;
            while (ambient.GuardActive && Time.time < timeout)
            {
                for (int i = 0; i < 5; i++) if (guard.MemberWalking(i))
                    Check(OralMapGeometry.TongueDistance(map, guard.MemberPosition(i), out _) >= OralGuardEscort.Radius, "walking member remains on tongue " + frames + "/" + i);
                sawPartial |= guard.ArrivedCount > 0 && guard.ArrivedCount < 5;
                Check(guard.Route.Landing == fixedGoal, "destination remains selected molar");
                frames++; yield return new WaitForSeconds(.2f);
            }
            Check(sawPartial, "leader and tail jump sequentially");
            Check(ambient.GuardCompleted == completed + 1 && ambient.GuardReward != null && ambient.GuardReward.CanInteract,
                "all five safe arrivals award one usable chest");
            var reward = ambient.GuardReward; yield return new WaitForSeconds(1);
            Check(ambient.GuardCompleted == completed + 1 && ambient.GuardReward == reward, "completion does not duplicate reward");
            ambient.ClearAll(); Clear(level); File.WriteAllText(Root + "/result.txt", "PASS");
        }
    }
}
#endif
