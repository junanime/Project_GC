using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public class MonsterRemakeTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);
        private static CharacterBlueprint CharacterData => Load<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
        private static TrapMonsterBlueprint TrapData => Load<TrapMonsterBlueprint>("Assets/Prefabs/Monsters/genral Monster BP/Trap Monster.asset");
        private static SniperMonsterBlueprint SniperData => Load<SniperMonsterBlueprint>("Assets/Prefabs/Monsters/genral Monster BP/Sniper Monster.asset");

        [Test]
        public void AssetsAndArrowOnlyEscapeAreInstalled()
        {
            Assert.That(CharacterData.capturedSpriteSequence, Has.Length.EqualTo(3));
            Assert.That(TrapData.captureRearSprites, Has.Length.EqualTo(8));
            Assert.That(TrapData.captureFrontSprites, Has.Length.EqualTo(8));
            Assert.That(SniperData.fireSprites, Has.Length.EqualTo(8));
            foreach (var sequence in new[] { CharacterData.capturedSpriteSequence, TrapData.captureRearSprites,
                TrapData.captureFrontSprites, SniperData.fireSprites })
                foreach (var sprite in sequence) Assert.That(sprite, Is.Not.Null);
            Assert.That(TrapData.bindDuration, Is.Zero);
            Assert.That(TrapData.useArrowEscapeMiniGame && TrapData.bindPlayerOnActivate, Is.True);
            var bullet = Load<GameObject>("Assets/Junhan/Prefabs/Projectiles/SniperBulletProjectile.prefab");
            Assert.That(bullet.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Takoyaki"));
        }

        [Test]
        public void CapturedSpriteKeepsOriginalCanvasScaleAndPixelFiltering()
        {
            var reference = CharacterData.idleSpriteSequence[0];
            foreach (var frame in CharacterData.capturedSpriteSequence)
            {
                Assert.That(frame.bounds.size.x, Is.EqualTo(reference.bounds.size.x).Within(0.002f));
                Assert.That(frame.bounds.size.y, Is.EqualTo(reference.bounds.size.y).Within(0.002f));
                Assert.That(frame.pixelsPerUnit, Is.EqualTo(reference.pixelsPerUnit));
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frame));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(importer.mipmapEnabled, Is.False);
            }
        }

        [Test]
        public void RecoilDoesNotMoveActorAndResetsOnReuse()
        {
            var go = new GameObject("RecoilTest");
            try
            {
                go.transform.position = new Vector3(3, 5, 0);
                var renderer = go.AddComponent<SpriteRenderer>();
                var visual = go.AddComponent<SniperRecoilVisual>();
                visual.Configure(renderer, SniperData);
                visual.Fire(Vector2.left);
                Assert.That(visual.IsPlaying && renderer.flipX, Is.True);
                Assert.That(renderer.sprite, Is.SameAs(SniperData.fireSprites[2]));
                Assert.That(go.transform.position, Is.EqualTo(new Vector3(3, 5, 0)));
                visual.ResetVisual();
                Assert.That(visual.IsPlaying, Is.False);
                Assert.That(renderer.sprite, Is.SameAs(SniperData.walkSpriteSequence[0]));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void MuzzleMirrorsWithFacingAndUsesRendererTransform()
        {
            var go = new GameObject("MuzzleTest");
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                go.transform.position = new Vector3(3, 5, 0);
                var right = SniperMonster.CalculateMuzzlePosition(renderer, SniperData.fireSprites[0]);
                renderer.flipX = true;
                var left = SniperMonster.CalculateMuzzlePosition(renderer, SniperData.fireSprites[0]);
                Assert.That(right.x, Is.GreaterThan(3));
                Assert.That(left.x, Is.LessThan(3));
                Assert.That(right.x - 3, Is.EqualTo(3 - left.x).Within(0.0001f));
                Assert.That(right.y, Is.EqualTo(left.y).Within(0.0001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void BoundAttackGateRejectsNewEmissionAndRestoresOnRelease()
        {
            var owner = new GameObject("BoundOwner");
            owner.SetActive(false); // Exercise the gate independently of scene services.
            var trapObject = new GameObject("TrapOwner");
            trapObject.SetActive(false);
            try
            {
                var character = owner.AddComponent<Character>();
                var bind = owner.AddComponent<PlayerTrapBindRuntime>();
                bind.Init(character);
                var trap = trapObject.AddComponent<TrapMonster>();
                var weapon = owner.AddComponent<ProjectileAbility>();
                typeof(Ability).GetField("playerCharacter", Hidden).SetValue(weapon, character);
                Assert.That(bind.TryBind(trap, Vector3.zero), Is.True);
                Assert.That(weapon.AttacksBlocked && character.IsTrapBound, Is.True);
                Assert.That(character.TryDash(), Is.False);
                // No EntityManager is needed: blocked attacks must return before touching any pool.
                Assert.That(weapon.SpawnPlayerProjectile(-1, Vector2.zero, 1, 0, 1, 0), Is.Null);
                foreach (Type type in new[] { typeof(GrenadeThrowableAbility), typeof(MolotovAbility), typeof(DaggerAbility), typeof(SyringeDartAbility) })
                {
                    var attack = (Ability)owner.AddComponent(type);
                    typeof(Ability).GetField("playerCharacter", Hidden).SetValue(attack, character);
                    if (type == typeof(DaggerAbility))
                        type.GetMethod("DamageMonster", Hidden).Invoke(attack, new object[] { null, 1f, Vector2.zero });
                    else if (type == typeof(SyringeDartAbility))
                    {
                        Assert.That(type.GetMethod("LaunchSyringeProjectile", Hidden).Invoke(attack, new object[] { Vector2.right, false }), Is.False);
                        type.GetMethod("LaunchNeedleShotgunProjectile", Hidden).Invoke(attack, new object[] { Vector2.right });
                        type.GetMethod("FireHeavySnipe", Hidden).Invoke(attack, new object[] { 1f });
                    }
                    else type.GetMethod("LaunchThrowable", Hidden).Invoke(attack, null);
                }
                bind.Release(trap);
                Assert.That(weapon.AttacksBlocked || character.IsTrapBound, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(trapObject); }
        }

        [Test]
        public void SeaweedWrapUsesRearPlayerFrontOrderAndHoldsLastFrame()
        {
            var go = new GameObject("TrapVisualTest");
            var playerGo = new GameObject("PlayerVisualTest");
            try
            {
                var original = go.AddComponent<SpriteRenderer>();
                var visual = go.AddComponent<SeaweedTrapVisual>();
                Assert.That(visual.Configure(TrapData, original), Is.True);
                var player = playerGo.AddComponent<SpriteRenderer>();
                player.sprite = CharacterData.capturedSpriteSequence[2];
                player.sortingOrder = 12;
                player.flipX = true;
                visual.Capture(player);
                typeof(SeaweedTrapVisual).GetField("elapsed", Hidden).SetValue(visual, 20f);
                typeof(SeaweedTrapVisual).GetMethod("Refresh", Hidden).Invoke(visual, null);
                Assert.That(visual.FrameIndex, Is.EqualTo(7));
                Assert.That(visual.Rear.sortingOrder, Is.EqualTo(11));
                Assert.That(visual.Front.sortingOrder, Is.EqualTo(13));
                Assert.That(visual.Front.flipX && visual.Rear.flipX, Is.True);
                visual.Hide();
                Assert.That(visual.Front.enabled || visual.Rear.enabled, Is.False);
                visual.ResetDormant();
                Assert.That(visual.FrameIndex, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(playerGo); }
        }
    }

    public static class MonsterRemakeTestRunner
    {
        public static void RunAll()
        {
            int count = 0;
            foreach (Type type in new[] { typeof(MonsterRemakeTests), typeof(AshiAnimationTests) })
                foreach (var method in type.GetMethods())
                    if (Attribute.IsDefined(method, typeof(TestAttribute)))
                    {
                        method.Invoke(Activator.CreateInstance(type), null);
                        Debug.Log("[MonsterRemakeTests] PASS " + method.Name);
                        count++;
                    }
            Debug.Log("[MonsterRemakeTests] ALL PASS " + count);
        }
    }
}
