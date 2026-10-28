using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class AugmentSelectionMotionSmoke
    {
        const string Key = "AugmentSelectionMotionSmoke";
        static bool started;
        static int checks;
        static double deadline;
        static AugmentSelectionMotionSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key + "Failed", false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position = new Rect(0, 0, 1280, 741);
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started = false; deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) SessionState.SetBool(Key + "Failed", true); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) { Debug.LogError("Selection motion test timeout"); SessionState.SetBool(Key + "Failed", true); }
            if (SessionState.GetBool(Key + "Done", false) || SessionState.GetBool(Key + "Failed", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                bool failed = SessionState.GetBool(Key + "Failed", false); SessionState.SetBool(Key, false);
                Debug.Log("[SelectionMotion] FINISHED checks=" + checks + " failed=" + failed);
                EditorApplication.Exit(failed ? 1 : 0); return;
            }
            var level = Object.FindObjectOfType<LevelManager>();
            if (started || !EditorApplication.isPlaying || level == null || level.PlayerCharacter == null || level.CurrentLevelTime < 1) return;
            started = true; level.StartCoroutine(Checks(level));
        }
        static object Get(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
        static void Check(bool valid, string name)
        { if (!valid) throw new Exception("[SelectionMotion] FAIL " + name); checks++; Debug.Log("[SelectionMotion] PASS " + name); }
        static void Capture(string name)
        {
            typeof(Ver4PlaySmoke).GetMethod("CaptureView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "selection-" + name + ".png" });
        }
        static IEnumerator Checks(LevelManager level)
        {
            Application.runInBackground = true;
            RunScore.Exclude("Augment animation regression verification");
            // Discovery cards are a separate pause owner after a new weapon is
            // acquired. Isolate this test to the selection animation itself.
            if (TutorialGuide.Instance != null) { TutorialGuide.Instance.Close(); TutorialGuide.Instance.enabled = false; }
            var prefs = GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss = false; GamePreferences.Apply(prefs, false);
            var dialog = level.EntityManager.AbilitySelectionDialog;
            if (dialog.MenuOpen) dialog.Close(); dialog.Open(false);
            yield return new WaitForSecondsRealtime(1);
            var cards = (List<AbilityCard>)Get(dialog, "abilityCards");
            var offers = (List<Ability>)Get(dialog, "displayedAbilities");
            var parent = (RectTransform)Get(dialog, "panelContent");
            var originalParent = cards[1].transform.parent;
            Check(Resources.Load<Texture2D>("AugmentSelection/CardBack") != null, "Compressed approved back loads");
            Check(Resources.Load<Shader>("AugmentSelection/CardBackTint").isSupported, "Border shader supported");
            Check(Resources.Load<Shader>("AugmentSelection/NeedleProjection").isSupported, "Needle shader supported");
            Capture("before");

            // Freeze the real renderer at fixed times for repeatable visual review.
            var motion = AugmentSelectionMotion.Create(parent, cards[1], cards, null); motion.ManualPreview = true;
            Check(motion.transform.parent == parent && motion.GetComponentsInParent<RectMask2D>().Length == 0, "Orbit outside card/layout clipping");
            for (int frame = 0; frame <= 100; frame++)
            {
                motion.Sample(frame * AugmentSelectionMotion.Duration / 100);
                Canvas.ForceUpdateCanvases();
                Capture("frame-" + frame.ToString("D3"));
                if (frame == 42) Check(motion.GetComponentsInChildren<AugmentOrbitGraphic>().Length == 4, "Two depth layers each for wind and eight needles");
                yield return null;
            }
            Check(motion.ShowingBack && motion.CardOpacity == 0, "180 degree turn ends fully invisible");
            motion.Cancel(); yield return null;
            Check(cards[1].transform.parent == originalParent && cards[1].transform.GetSiblingIndex() == 1, "Card parent and slot restored");
            Check(cards.All(c => c.GetComponentsInChildren<AugmentCardProjection>(true).Length == 0), "Projection modifiers released");

            // All six grade accents are applied only by the shared back material.
            var manager = Object.FindObjectOfType<AbilityManager>();
            var source = manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).First();
            foreach (AugmentUpgradeGrade grade in Enum.GetValues(typeof(AugmentUpgradeGrade)))
            {
                var example = new GameObject("Back grade preview").AddComponent<Ver4AugmentOffer>();
                example.Configure(manager.Ver4, Ver4RewardKind.NewSpecial, grade, source, 0, 0, "색상 확인", "테두리만 등급 색상으로 변경");
                cards[1].Init(dialog, example, 0); cards[1].SettleForSelection();
                motion = AugmentSelectionMotion.Create(parent, cards[1], cards, null); motion.ManualPreview = true; motion.Sample(AugmentSelectionMotion.Duration * (2.92f / 3.27f));
                var back = motion.GetComponentInChildren<AugmentBackGraphic>();
                Check(back.material.GetColor("_Accent") == StitchPatchTheme.CardAccent(grade), "Grade border " + grade);
                Canvas.ForceUpdateCanvases(); Capture("back-" + grade);
                motion.Cancel(); Object.Destroy(example.gameObject); yield return null;
            }
            cards[1].Init(dialog, offers[1], 0); yield return new WaitForSecondsRealtime(.6f);
            // Real selection at timeScale=0: lock skip/reroll/close and duplicate selection.
            cards[1].Selected();
            Check(dialog.SelectionAnimating && dialog.MenuOpen && Time.timeScale == 0, "Selection remains paused during animation");
            cards[0].Selected(); cards[1].Selected(); dialog.SkipAbilities(); dialog.RerollAbilities(); dialog.Close();
            Check(dialog.MenuOpen && Object.FindObjectsOfType<AugmentSelectionMotion>().Length == 1, "Rapid inputs cannot close, reroll or create duplicate animation");
            yield return new WaitForSecondsRealtime(AugmentSelectionMotion.Duration + .2f);
            Check(!dialog.MenuOpen && !dialog.SelectionAnimating && Time.timeScale == 1, "Unscaled animation finishes and resumes gameplay");
            dialog.Open(false); yield return new WaitForSecondsRealtime(.6f);
            Check(cards.All(c => c.GetComponent<CanvasGroup>().alpha == 1), "Next reward reuses visible cards");
            Check(((Button)Get(dialog, "skipButton")).interactable, "Skip restored for next reward");
            // Mobile landscape has the same relative orbit, independent of screen size.
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position = new Rect(0, 0, 960, 581);
            yield return new WaitForSecondsRealtime(.5f);
            motion = AugmentSelectionMotion.Create(parent, cards[0], cards, null); motion.ManualPreview = true; motion.Sample(AugmentSelectionMotion.Duration * (1.2f / 3.27f));
            Canvas.ForceUpdateCanvases(); Capture("mobile-layout"); motion.Cancel(); yield return null;
            dialog.Close();
            Check(AugmentSelectionMotion.Duration == 1.5f, "Approved total selection duration is 1.5 seconds");
            dialog.OpenLegendary();
            yield return new WaitForSecondsRealtime(.6f);
            cards = (List<AbilityCard>)Get(dialog, "abilityCards");
            Check(cards.All(c => c.UsesNoblePanel && c.transform.Find("Noble scroll seal") == null), "Noble panels appear without scroll covers");
            Check(cards.All(c => ((Image)Get(c, "abilityImage")).enabled), "Noble art is visible before the first click");
            cards[0].Selected();
            float selectedAt = Time.realtimeSinceStartup;
            Check(dialog.SelectionAnimating && dialog.MenuOpen && Time.timeScale == 0, "First noble click starts shared motion while paused");
            cards[0].Selected(); cards[1].Selected(); dialog.Close();
            Check(Object.FindObjectsOfType<AugmentSelectionMotion>().Length == 1, "Noble selection also rejects duplicate inputs");
            while (dialog.SelectionAnimating && Time.realtimeSinceStartup - selectedAt < 2.5f) yield return null;
            float elapsed = Time.realtimeSinceStartup - selectedAt;
            Check(elapsed >= 1.45f && elapsed < 1.7f, "Noble motion finishes in 1.5 seconds within frame tolerance: " + elapsed);
            Check(!dialog.MenuOpen && Time.timeScale == 1, "Noble motion resumes gameplay after completion");
            SessionState.SetBool(Key + "Done", true);
        }
    }
}
