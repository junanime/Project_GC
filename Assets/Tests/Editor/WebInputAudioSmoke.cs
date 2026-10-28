using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Uses the real UI raycaster and InputSystem touchscreen, not direct handler calls.
    [InitializeOnLoad]
    public static class WebInputAudioSmoke
    {
        const string Key = "WebInputAudioSmoke.Active";
        static bool started;
        static double deadline;
        static int failures, passed;
        static WebInputAudioSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Done", false);
            SessionState.SetInt(Key + "Failures", 0); SessionState.SetInt(Key + "Passed", 0);
            SessionState.SetInt(Key + "EditorInput", (int)InputSystem.settings.editorInputBehaviorInPlayMode);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            var view = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            view.position = new Rect(0, 0, 1280, 741); view.Focus();
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started = false; deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Assert) return;
            Check(false, message); SessionState.SetBool(Key + "Done", true);
        }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Check(false, "timeout"); SessionState.SetBool(Key + "Done", true); }
            if (SessionState.GetBool(Key + "Done", false))
            {
                if (EditorApplication.isPlaying)
                {
                    InputSystem.settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt(Key + "EditorInput", 0);
                    EditorApplication.ExitPlaymode(); return;
                }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key, false);
                passed = SessionState.GetInt(Key + "Passed", 0); failures = SessionState.GetInt(Key + "Failures", 0);
                Debug.Log($"INPUT_AUDIO_DONE passed={passed} failures={failures}");
                EditorApplication.Exit(failures == 0 ? 0 : 1); return;
            }
            if (started || !EditorApplication.isPlaying || ApothecaryUI.Instance == null) return;
            started = true; GamePlatform.PreviewTouchControls = true;
            var host = new GameObject("Input audio verification").AddComponent<WebInputAudioRunner>();
            Object.DontDestroyOnLoad(host); host.StartCoroutine(Checks());
        }
        static void Check(bool value, string label)
        {
            string count = Key + (value ? "Passed" : "Failures");
            SessionState.SetInt(count, SessionState.GetInt(count, 0) + 1);
            if (value) Debug.Log("INPUT_AUDIO_PASS " + label);
            else Debug.LogError("INPUT_AUDIO_FAIL " + label);
        }
        static void AudioState(string phase)
        {
            var listeners = Object.FindObjectsOfType<AudioListener>().Where(x => x.enabled).ToArray();
            Check(listeners.Length == 1, phase + " has one active audio listener");
            Check(GameAudioManager.Instance.GetComponents<AudioSource>().Count(s=>s.loop && s.isPlaying && s.volume > 0) == 1,
                phase + " has exactly one audible requested music track");
            Debug.Log($"INPUT_AUDIO {phase} master={AudioListener.volume} pause={AudioListener.pause} intro={IntroMovie.Active}");
            foreach (var source in Object.FindObjectsOfType<AudioSource>())
                if (source.clip != null) Debug.Log($"INPUT_AUDIO {phase} source={source.name} clip={source.clip.name} load={source.clip.loadState} type={source.clip.loadType} playing={source.isPlaying} volume={source.volume} pitch={source.pitch} time={source.time}");
        }
        static IEnumerator Checks()
        {
            var prefs = GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss = false; prefs.muteOnFocusLoss = false;
            GamePreferences.Apply(prefs, false);
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return new WaitForSecondsRealtime(1);
            var intro = Object.FindObjectOfType<IntroMovie>();
            if (intro != null) typeof(IntroMovie).GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(intro, null);
            yield return new WaitForSecondsRealtime(1); AudioState("main");
            ApothecaryUI.Instance.Show("prepare");
            yield return new WaitForSecondsRealtime(.5f); AudioState("prepare");
            CrossSceneData.CharacterBlueprint = ApothecaryUI.Instance.Config.characters[0];
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game/Level 1.unity");
            yield return null; yield return null;
            while (StageEntryLoading.IsLoading || Object.FindObjectOfType<LevelManager>()?.PlayerCharacter == null) yield return null;
            var level = Object.FindObjectOfType<LevelManager>();
            while (level.EntityManager == null || level.EntityManager.AbilitySelectionDialog == null) yield return null;
            RunScore.Exclude("Touch and audio regression verification");
            if (TutorialGuide.IsOpen) TutorialGuide.Instance.Close();
            if (level.EntityManager.AbilitySelectionDialog.MenuOpen) level.EntityManager.AbilitySelectionDialog.Close();
            level.SetRunFlowPaused(true); Time.timeScale = 1;
            ApothecaryUI.Instance.Show("hud");
            yield return new WaitForSecondsRealtime(.5f); AudioState("battle");
            GameAudioManager.StartBossAudio();
            yield return new WaitForSecondsRealtime(.5f); AudioState("boss");
            GameAudioManager.EnterAugmentSelectionAudio();
            yield return new WaitForSecondsRealtime(.5f); AudioState("augment over boss");
            GameAudioManager.FinishStageBossAudio();
            yield return new WaitForSecondsRealtime(.5f); AudioState("boss ended under augment");
            GameAudioManager.ExitAugmentSelectionAudio();
            yield return new WaitForSecondsRealtime(.5f); AudioState("field restored");
            GameAudioManager.EndRunAudio(false);
            yield return new WaitForSecondsRealtime(.5f);
            Check(!GameAudioManager.Instance.GetComponents<AudioSource>().Any(s=>s.loop && s.isPlaying), "stopped run music stays stopped");
            GameAudioManager.PlayIngameMusic();
            yield return new WaitForSecondsRealtime(.5f); AudioState("next run music");
            var move = Object.FindObjectsOfType<MobileTouchControl>().FirstOrDefault(c => c.name == "MOVE");
            Check(move != null, "mobile joystick exists");
            if (move != null)
            {
                Canvas.ForceUpdateCanvases();
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, move.transform.TransformPoint(new Vector2(move.Radius * .8f, 0)));
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                Debug.Log("INPUT_AUDIO_RAYCAST " + string.Join(" | ", hits.Take(12).Select(h => h.gameObject.name + " canvas=" + h.sortingOrder)));
                Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<MobileTouchControl>() == move, "joystick is reachable through visible UI");
                var screen = InputSystem.AddDevice<Touchscreen>();
                yield return null;
                var module = EventSystem.current.currentInputModule as UnityEngine.InputSystem.UI.InputSystemUIInputModule;
                Debug.Log($"INPUT_AUDIO_INPUT module={EventSystem.current.currentInputModule} focus={Application.isFocused} touch={screen.enabled} point={point} scale={Time.timeScale} actions={module?.actionsAsset} enabled={module?.leftClick?.action.enabled} controls={string.Join(",", module?.leftClick?.action.controls.Select(c=>c.path) ?? Array.Empty<string>())}");
                move.gameObject.AddComponent<WebTouchProbe>();
                var start = level.PlayerCharacter.transform.position;
                InputSystem.QueueStateEvent(screen, new TouchState { touchId = 71, phase = UnityEngine.InputSystem.TouchPhase.Began, position = point });
                yield return new WaitForSecondsRealtime(.7f);
                Check(move.Knob.anchoredPosition.x > move.Radius * .5f, "real touchscreen contact moves joystick handle");
                Check(level.PlayerCharacter.transform.position.x > start.x + .2f, "real touchscreen contact moves player");
                var dash = Object.FindObjectsOfType<Button>().Single(b => b.name == "HUD dash");
                Vector2 dashPoint = RectTransformUtility.WorldToScreenPoint(null, dash.transform.position);
                int charges = level.PlayerCharacter.CurrentDashCharges;
                InputSystem.QueueStateEvent(screen, new TouchState { touchId = 73, phase = UnityEngine.InputSystem.TouchPhase.Began, position = dashPoint });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(screen, new TouchState { touchId = 73, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = dashPoint });
                yield return new WaitForSecondsRealtime(.1f);
                Check(level.PlayerCharacter.CurrentDashCharges == charges - 1, "second real touch activates dash while moving");
                Check(move.Knob.anchoredPosition.x > 0, "second touch release preserves held movement");
                // Skills intentionally cannot interrupt a dash. Keep the first
                // finger down and test the next action after the dash completes.
                while (level.PlayerCharacter.IsDashing) yield return null;
                yield return null;
                var active = Object.FindObjectsOfType<Button>().Single(b => b.name == "Active skill Space");
                Vector2 activePoint = RectTransformUtility.WorldToScreenPoint(null, active.transform.position);
                InputSystem.QueueStateEvent(screen, new TouchState { touchId = 74, phase = UnityEngine.InputSystem.TouchPhase.Began, position = activePoint });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(screen, new TouchState { touchId = 74, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = activePoint });
                yield return new WaitForSecondsRealtime(.1f);
                Check(level.PlayerCharacter.Skills.Active || level.PlayerCharacter.Skills.CooldownRemaining > 0 || level.PlayerCharacter.Skills.IsSummoning,
                    "second real touch activates skill while moving");
                InputSystem.QueueStateEvent(screen, new TouchState { touchId = 71, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = point });
                yield return new WaitForSecondsRealtime(.1f);
                Check(move.Knob.anchoredPosition == Vector2.zero, "touch release resets joystick");
                module.enabled = false;
                var recoveryStart = level.PlayerCharacter.transform.position;
                InputSystem.QueueStateEvent(screen, new TouchState { touchId = 72, phase = UnityEngine.InputSystem.TouchPhase.Began, position = point });
                yield return new WaitForSecondsRealtime(.35f);
                Check(move.Knob.anchoredPosition.x > 0 && level.PlayerCharacter.transform.position.x > recoveryStart.x + .1f,
                    "lost UI pointer-down recovers from actual touchscreen state");
                InputSystem.QueueStateEvent(screen, new TouchState { touchId = 72, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = point });
                yield return new WaitForSecondsRealtime(.1f);
                Check(move.Knob.anchoredPosition == Vector2.zero, "fallback touch releases without UI pointer-up");
                module.enabled = true;
                InputSystem.RemoveDevice(screen);
                var bridge = new GameObject("Browser touch verification").AddComponent<MobileWebLifecycle>();
                string Sample(int id, int phase) => FormattableString.Invariant($"{{\"id\":{id},\"phase\":{phase},\"x\":{point.x/Screen.width},\"y\":{point.y/Screen.height}}}");
                var browserStart = level.PlayerCharacter.transform.position;
                bridge.SetBrowserTouch(Sample(0,0));
                yield return new WaitForSecondsRealtime(.3f);
                Check(move.Knob.anchoredPosition.x > 0 && level.PlayerCharacter.transform.position.x > browserStart.x + .1f,
                    "browser canvas touch moves actual player without Unity pointer event");
                bridge.SetBrowserTouch(Sample(9,2));
                Check(move.Knob.anchoredPosition.x > 0, "browser second finger cannot release joystick owner");
                bridge.SetBrowserTouch(Sample(0,2));
                Check(move.Knob.anchoredPosition == Vector2.zero, "browser owner release stops movement");
                bridge.SetBrowserTouch(Sample(1,0)); bridge.SetBrowserBlocked("1");
                Check(move.Knob.anchoredPosition == Vector2.zero && Time.timeScale == 0, "rotation/background cancels browser joystick and pauses");
                bridge.SetBrowserTouch(Sample(1,1));
                Check(move.Knob.anchoredPosition == Vector2.zero, "blocked browser cannot keep moving");
                bridge.SetBrowserBlocked("0");
                Check(Time.timeScale == 1, "browser return restores prior gameplay speed");
                bridge.SetBrowserTouch(Sample(2,0)); bridge.SetBrowserTouchCount("0");
                yield return null;
                Check(move.Knob.anchoredPosition == Vector2.zero, "browser lost release clears all gestures");
                Object.Destroy(bridge.gameObject);
            }
            typeof(Ver4PlaySmoke).GetMethod("CaptureView", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { "web-audit-mobile-controls.png" });
            GamePlatform.PreviewTouchControls = false;
            SessionState.SetBool(Key + "Done", true);
        }
    }
    public sealed class WebInputAudioRunner : MonoBehaviour { }
    public sealed class WebTouchProbe : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData data) => Debug.Log("INPUT_AUDIO_DOWN " + data);
        public void OnPointerUp(PointerEventData data) => Debug.Log("INPUT_AUDIO_UP " + data);
    }
}
