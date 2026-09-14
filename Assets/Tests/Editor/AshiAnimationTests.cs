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
        public void MainBlueprintContainsBothSixteenFrameFaceVariants()
        {
            CharacterBlueprint blueprint = LoadBlueprint();

            Assert.That(blueprint.walkSpriteSequence, Has.Length.EqualTo(16));
            Assert.That(blueprint.eyebrowWalkSpriteSequence, Has.Length.EqualTo(16));
            Assert.That(blueprint.walkFrameTime, Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(Array.TrueForAll(blueprint.walkSpriteSequence, sprite => sprite != null), Is.True);
            Assert.That(Array.TrueForAll(blueprint.eyebrowWalkSpriteSequence, sprite => sprite != null), Is.True);
            Assert.That(blueprint.walkSpriteSequence[0], Is.Not.SameAs(blueprint.eyebrowWalkSpriteSequence[0]));
        }

        [Test]
        public void MainBlueprintContainsAlternatingWingSequences()
        {
            CharacterBlueprint blueprint = LoadBlueprint();

            Assert.That(blueprint.syringeRearWingAttackSpriteSequence, Has.Length.EqualTo(4));
            Assert.That(blueprint.syringeFrontWingAttackSpriteSequence, Has.Length.EqualTo(4));
            Assert.That(Array.TrueForAll(blueprint.syringeRearWingAttackSpriteSequence, sprite => sprite != null), Is.True);
            Assert.That(Array.TrueForAll(blueprint.syringeFrontWingAttackSpriteSequence, sprite => sprite != null), Is.True);
            Assert.That(blueprint.syringeAttackMinDuration, Is.GreaterThan(0f));
            Assert.That(blueprint.syringeAttackMaxDuration, Is.GreaterThanOrEqualTo(blueprint.syringeAttackMinDuration));
        }

        [Test]
        public void WingAnimatorDoesNotReplaceTheWalkingBodySprite()
        {
            CharacterBlueprint blueprint = LoadBlueprint();
            GameObject root = new GameObject("AshiAnimationTest");

            try
            {
                SpriteRenderer bodyRenderer = root.AddComponent<SpriteRenderer>();
                Sprite expectedBody = blueprint.walkSpriteSequence[5];
                bodyRenderer.sprite = expectedBody;
                CharacterSyringeAttackAnimator animator =
                    root.AddComponent<CharacterSyringeAttackAnimator>();

                animator.Init(null, blueprint, bodyRenderer);
                animator.PlayShot(0.25f);

                Assert.That(animator.IsPlaying, Is.True);
                Assert.That(animator.ActiveWingIsFront, Is.True);
                Assert.That(bodyRenderer.sprite, Is.SameAs(expectedBody),
                    "Wing attacks must remain an overlay so walking never stops.");
                Assert.That(root.transform.Find("SyringeAttackWingVisual"), Is.Not.Null);

                // A second successful shot on a later frame uses the opposite wing.
                typeof(CharacterSyringeAttackAnimator)
                    .GetField("lastShotFrame", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(animator, -1);
                animator.PlayShot(0.1f);
                Assert.That(animator.ActiveWingIsFront, Is.False);
                Assert.That(bodyRenderer.sprite, Is.SameAs(expectedBody));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CharacterExposesRuntimeFaceComparisonAndShotTrigger()
        {
            Assert.That(Enum.GetValues(typeof(AshiFaceStyle)), Has.Length.EqualTo(2));
            Assert.That(typeof(Character).GetMethod("SetAshiFaceStyle"), Is.Not.Null);
            Assert.That(typeof(Character).GetMethod("TriggerSyringeAttackAnimation"), Is.Not.Null);

            MethodInfo shotgunLaunch = typeof(SyringeDartAbility).GetMethod(
                "LaunchNeedleShotgunProjectile",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(shotgunLaunch, Is.Not.Null);
            Assert.That(shotgunLaunch.ReturnType, Is.EqualTo(typeof(bool)),
                "Shotgun animation should only fire after at least one projectile spawned.");
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
                tests.MainBlueprintContainsBothSixteenFrameFaceVariants,
                tests.MainBlueprintContainsAlternatingWingSequences,
                tests.WingAnimatorDoesNotReplaceTheWalkingBodySprite,
                tests.CharacterExposesRuntimeFaceComparisonAndShotTrigger
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
