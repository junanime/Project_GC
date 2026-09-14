using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public class AshiAnimationTests
    {
        private const string BlueprintPath =
            "Assets/Blueprints/Characters/Main Character Blueprint.asset";

        [Test]
        public void MainBlueprintUsesEightEyebrowWalkFrames()
        {
            CharacterBlueprint blueprint = LoadBlueprint();

            Assert.That(blueprint.walkSpriteSequence, Has.Length.EqualTo(8));
            Assert.That(blueprint.walkFrameTime, Is.EqualTo(0.08f).Within(0.0001f));
            Assert.That(Array.TrueForAll(blueprint.walkSpriteSequence, sprite => sprite != null), Is.True);
            Assert.That(Array.TrueForAll(
                blueprint.walkSpriteSequence,
                sprite => sprite.name.Contains("Ashi_Run_Eyebrows")), Is.True);
        }

        [Test]
        public void MainBlueprintContainsEightFrameIdleAndDashAnimations()
        {
            CharacterBlueprint blueprint = LoadBlueprint();

            AssertSequence(blueprint.idleSpriteSequence, "Ashi_Idle_Eyebrows");
            AssertSequence(blueprint.dashSpriteSequence, "Ashi_Dash_Eyebrows");
            Assert.That(blueprint.idleFrameTime, Is.EqualTo(0.125f).Within(0.0001f));
            Assert.That(blueprint.dashFrameTime, Is.EqualTo(0.0275f).Within(0.0001f));
        }

        [Test]
        public void StateSpritesKeepComparableWorldSize()
        {
            CharacterBlueprint blueprint = LoadBlueprint();
            Vector2 walkSize = blueprint.walkSpriteSequence[0].bounds.size;

            foreach (Sprite sprite in blueprint.idleSpriteSequence)
            {
                Assert.That(sprite.bounds.size.x, Is.EqualTo(walkSize.x).Within(0.08f));
                Assert.That(sprite.bounds.size.y, Is.EqualTo(walkSize.y).Within(0.08f));
            }

            foreach (Sprite sprite in blueprint.dashSpriteSequence)
            {
                Assert.That(sprite.bounds.size.x, Is.EqualTo(walkSize.x).Within(0.08f));
                Assert.That(sprite.bounds.size.y, Is.EqualTo(walkSize.y).Within(0.08f));
            }
        }

        [Test]
        public void CharacterExposesIdleStateAndNoAttackAnimationTrigger()
        {
            Assert.That(typeof(Character).GetMethod("StartIdleAnimation"), Is.Not.Null);
            Assert.That(typeof(Character).GetMethod("TriggerSyringeAttackAnimation"), Is.Null);

            MethodInfo shotgunLaunch = typeof(SyringeDartAbility).GetMethod(
                "LaunchNeedleShotgunProjectile",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(shotgunLaunch, Is.Not.Null);
            Assert.That(shotgunLaunch.ReturnType, Is.EqualTo(typeof(void)));
        }

        private static void AssertSequence(Sprite[] sequence, string expectedName)
        {
            Assert.That(sequence, Has.Length.EqualTo(8));
            Assert.That(Array.TrueForAll(sequence, sprite => sprite != null), Is.True);
            Assert.That(Array.TrueForAll(sequence, sprite => sprite.name.Contains(expectedName)), Is.True);
        }

        private static CharacterBlueprint LoadBlueprint()
        {
            CharacterBlueprint blueprint =
                AssetDatabase.LoadAssetAtPath<CharacterBlueprint>(BlueprintPath);
            Assert.That(blueprint, Is.Not.Null, BlueprintPath);
            return blueprint;
        }
    }

    public static class AshiAnimationTestRunner
    {
        public static void RunAll()
        {
            AshiAnimationTests tests = new AshiAnimationTests();
            Action[] cases =
            {
                tests.MainBlueprintUsesEightEyebrowWalkFrames,
                tests.MainBlueprintContainsEightFrameIdleAndDashAnimations,
                tests.StateSpritesKeepComparableWorldSize,
                tests.CharacterExposesIdleStateAndNoAttackAnimationTrigger
            };

            int failed = 0;
            foreach (Action test in cases)
            {
                try
                {
                    test();
                    Debug.Log($"[AshiAnimationTests][PASS] {test.Method.Name}");
                }
                catch (Exception exception)
                {
                    failed++;
                    Debug.LogException(new Exception(
                        $"[AshiAnimationTests][FAIL] {test.Method.Name}", exception));
                }
            }

            Debug.Log($"[AshiAnimationTests] Completed | Total={cases.Length}, Failed={failed}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }
    }
}
