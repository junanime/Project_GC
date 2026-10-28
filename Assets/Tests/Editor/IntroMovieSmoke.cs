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
    [InitializeOnLoad]
    public static class IntroMovieSmoke
    {
        const string Key = "IntroMovieSmoke";
        static bool launched;
        static int checks;
        static double deadline;
        static IntroMovieSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + "Done", false);
            SessionState.SetBool(Key + "Failed", false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position = new Rect(0, 0, 1280, 741);
            Attach();
            EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            deadline = EditorApplication.timeSinceStartup + 200;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= Log;
            Application.logMessageReceived += Log;
        }
        static void Log(string m, string s, LogType t)
        {
            if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) SessionState.SetBool(Key + "Failed", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) SessionState.SetBool(Key + "Failed", true);
            if (SessionState.GetBool(Key + "Done", false) || SessionState.GetBool(Key + "Failed", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key, false);
                Debug.Log("[IntroMovieSmoke] FINISHED checks=" + checks + " failed=" + SessionState.GetBool(Key + "Failed", false));
                EditorApplication.Exit(SessionState.GetBool(Key + "Failed", false) ? 1 : 0);
                return;
            }
            if (!launched && EditorApplication.isPlaying && ApothecaryUI.Instance != null)
            {
                launched = true;
                var host = new GameObject("Intro smoke runner");
                Object.DontDestroyOnLoad(host);
                host.AddComponent<IntroMovieSmokeRunner>().StartCoroutine(Checks());
            }
        }
        static void Check(bool ok, string label)
        {
            if (!ok) throw new Exception("[IntroMovieSmoke] FAIL " + label);
            checks++; Debug.Log("[IntroMovieSmoke] PASS " + label);
        }
        static void Escape(IntroMovie movie)
        {
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(UnityEngine.InputSystem.Key.Escape));
            InputSystem.Update();
            Check(GameInput.GetKeyDown(KeyCode.Escape), "Esc reaches input backend");
            movie.SendMessage("Update");
            ApothecaryUI.Instance.SendMessage("Update");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); InputSystem.Update();
        }
        static void Tap(GameObject target)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)target.transform;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var e = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(e, hits);
            Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == target, "Pointer raycast reaches " + target.name);
            ExecuteEvents.Execute(target, e, ExecuteEvents.pointerClickHandler);
        }
        static void Capture(string name) => typeof(Ver4PlaySmoke).GetMethod("CaptureView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "intro-" + name + ".png" });
        static void ResetSession() => typeof(IntroMovie).GetMethod("ResetSession", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        static IEnumerator WaitForMovie(IntroMovie movie)
        {
            float end = Time.realtimeSinceStartup + 25;
            while (movie != null && movie.Player != null && movie.Player.frame < 12 && Time.realtimeSinceStartup < end) yield return null;
            Check(movie != null && movie.Player.frame >= 12 && movie.Player.isPlaying, "Actual MP4 decoded and progressing");
        }
        static IEnumerator Checks()
        {
            Application.runInBackground = true;
            var prefs = GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss = false; prefs.muteOnFocusLoss = false; prefs.reducedMotion = false; GamePreferences.Apply(prefs, false);
            var ui = ApothecaryUI.Instance;
            var movie = Object.FindObjectOfType<IntroMovie>();
            Check(movie != null && IntroMovie.BlocksMenuInput, "Initial menu starts behind intro");
            yield return WaitForMovie(movie);
            Check(movie.Player.width == 1280 && movie.Player.height == 720, "Original 1280x720 video dimensions");
            Check(Math.Abs(movie.Player.length - 38.04) < 1, "Original full length retained");
            Check(GameAudioManager.Instance.MusicVolume == 0, "Lobby music suppressed during movie");
            Check(ui.Page == "main" && !ui.GetComponentsInChildren<Button>().Any(b => b.name == "Button 게임 시작"), "Underlying menu cannot be clicked");
            Capture("playing");
            Escape(movie); Check(!movie.ConfirmationOpen && movie.Player.isPlaying, "First Esc only reveals hint");
            Escape(movie); Check(movie.ConfirmationOpen && !movie.Player.isPlaying, "Second Esc pauses and asks");
            Check(ui.Page == "main", "Esc never opens settings beneath video");
            yield return new WaitForSecondsRealtime(.3f);
            Capture("confirm-pc");
            double paused = movie.Player.time;
            yield return new WaitForSecondsRealtime(.5f);
            Check(Math.Abs(movie.Player.time - paused) < .1, "Dialog holds exact playback position");
            var buttons = movie.GetComponentInParent<ApothecaryUI>().GetComponentsInChildren<Button>();
            var keep = buttons.First(b => b.name == "Button 계속 보기");
            Check(keep.GetComponent<StitchPatchButton>() != null && keep.GetComponent<ApothecaryButtonFeedback>().sparkle != null, "Existing stitch buttons and gold focus effect reused");
            Tap(keep.gameObject); yield return new WaitForSecondsRealtime(.5f);
            Check(!movie.ConfirmationOpen && movie.Player.time > paused, "Continue resumes from same scene");
            Escape(movie); yield return new WaitForSecondsRealtime(3.2f); Escape(movie);
            Check(!movie.ConfirmationOpen, "Expired first Esc does not accidentally skip later");
            Escape(movie); Escape(movie);
            Check(!movie.ConfirmationOpen && movie.Player.isPlaying, "Esc in confirmation returns to video");

            typeof(MobileGameplayInput).GetProperty("Active").SetValue(null, true);
            var overlay = ui.GetComponentsInChildren<Button>().First(b => b.name == "Intro screen");
            Tap(overlay.gameObject); Check(!movie.ConfirmationOpen, "First touch only arms skip");
            Tap(overlay.gameObject); Check(movie.ConfirmationOpen, "Second touch opens confirmation");
            yield return new WaitForSecondsRealtime(.3f); Capture("confirm-touch");
            Tap(ui.GetComponentsInChildren<Button>().First(b => b.name == "Button 건너뛰기").gameObject);
            yield return null; yield return null;
            Check(!IntroMovie.Active && ui.Page == "main", "Confirmed skip shows menu");
            Check(GameAudioManager.Instance.MusicVolume > 0, "Lobby music restored");
            Check(ui.GetComponentsInChildren<Button>().Any(b => b.name == "Button 게임 시작"), "Menu interaction restored");
            Capture("main-after-skip");
            typeof(MobileGameplayInput).GetProperty("Active").SetValue(null, false);
            yield return SceneManager.LoadSceneAsync("Assets/Scripts/boyoung/test/Main Menu.unity"); yield return null;
            ui = ApothecaryUI.Instance;
            Check(!IntroMovie.Active, "Returning to lobby never repeats intro");

            ResetSession(); IntroMovie.TryShow(ui); movie = Object.FindObjectOfType<IntroMovie>();
            yield return WaitForMovie(movie);
            float timeout = Time.realtimeSinceStartup + 50;
            float report = 0;
            while (IntroMovie.Active && Time.realtimeSinceStartup < timeout)
            {
                if (Time.realtimeSinceStartup > report)
                {
                    report = Time.realtimeSinceStartup + 10;
                    Debug.Log("[IntroMovieSmoke] Playback time=" + movie.Player.time + " frame=" + movie.Player.frame + " playing=" + movie.Player.isPlaying + " focus=" + Application.isFocused + " timescale=" + Time.timeScale);
                }
                yield return null;
            }
            yield return null; yield return null;
            Check(!IntroMovie.Active && ui.Page == "main", "Natural end of full movie goes directly to menu");
            Capture("main-after-end");

            var config = Resources.Load<IntroMovieSettings>("IntroMovieSettings");
            string original = config.relativePath;
            try
            {
                ResetSession(); config.relativePath = "Intro/missing-smoke.mp4"; IntroMovie.TryShow(ui); yield return null;
                Check(!IntroMovie.Active && ui.GetComponentsInChildren<Button>().Any(b => b.name == "Button 게임 시작"), "Missing replacement movie falls back to usable menu");
            }
            finally { config.relativePath = original; }
            Tap(ui.GetComponentsInChildren<Button>().First(b => b.name == "Button 게임 시작").gameObject);
            Check(ui.Page == "prepare", "Start button still opens preparation");
            SessionState.SetBool(Key + "Done", true);
        }
    }
    public sealed class IntroMovieSmokeRunner : MonoBehaviour { }
}
