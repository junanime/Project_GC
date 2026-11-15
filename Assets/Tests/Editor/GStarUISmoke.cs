using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using InputKey = UnityEngine.InputSystem.Key;

namespace Vampire.Tests.Editor
{
    // Real scene and live uGUI interaction. Only the completed combat record is synthetic.
    [InitializeOnLoad]
    public static class GStarUISmoke
    {
        const string Key = "GStarUISmoke";
        const string Output = "Library/GStarUIQA";
        const string TestPhone = "01000000001";
        static bool started;
        static double deadline;
        static Keyboard keyboard;
        static GStarUITestHost host;
        static GStarUISmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            GStarBuild.PrepareArt();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/checks.txt", ""); File.WriteAllText(Output + "/errors.txt", "");
            SessionState.SetString(Key + "Data", Path.GetFullPath(Path.Combine(Output, "data-" + Guid.NewGuid().ToString("N"))));
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key + "Fail", false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            var gameView = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gameView).position = new Rect(0, 0, 1672, 962);
            Attach(); EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void IsolateBeforeScene()
        {
            if (!SessionState.GetBool(Key, false)) return;
            GStarDemo.TestOverride = true;
            GStarDemo.TestDataDirectory = SessionState.GetString(Key + "Data", "");
            GStarSession.TestDirectory = GStarDemo.TestDataDirectory; GStarSession.ReloadForTests();
            GStarLeaderboard.TestDirectory = GStarDemo.TestDataDirectory; GStarLeaderboard.ReloadForTests();
        }
        static void Attach()
        {
            started = false; deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string text, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            File.AppendAllText(Output + "/errors.txt", text + "\n" + trace + "\n");
            SessionState.SetBool(Key + "Fail", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline)
            { File.AppendAllText(Output + "/errors.txt", "UI test deadline\n"); SessionState.SetBool(Key + "Fail", true); }
            if (SessionState.GetBool(Key + "Done", false) || SessionState.GetBool(Key + "Fail", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                GStarDemo.TestOverride = null; GStarDemo.TestDataDirectory = null;
                GStarSession.TestDirectory = null; GStarSession.ReloadForTests();
                GStarLeaderboard.TestDirectory = null; GStarLeaderboard.ReloadForTests();
                bool failed = SessionState.GetBool(Key + "Fail", false);
                SessionState.SetBool(Key, false);
                File.WriteAllText(Output + "/result.txt", failed ? "FAIL" : "PASS: live result registration, rankings, next attempts, preparation and staff reset");
                EditorApplication.Exit(failed ? 1 : 0); return;
            }
            if (!EditorApplication.isPlaying || started || ApothecaryUI.Instance == null) return;
            started = true;
            host = new GameObject("G-STAR UI QA").AddComponent<GStarUITestHost>();
            Object.DontDestroyOnLoad(host.gameObject); host.StartCoroutine(Checks());
        }
        static void Check(bool ok, string text)
        {
            if (!ok) throw new Exception("GSTAR_UI_FAIL: " + text);
            File.AppendAllText(Output + "/checks.txt", "PASS " + text + "\n");
        }
        static IEnumerator Settled()
        { yield return null; Canvas.ForceUpdateCanvases(); yield return null; yield return new WaitForSecondsRealtime(.15f); }
        static IEnumerator Wait(Func<bool> condition, float seconds, string text)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            Check(condition(), text);
        }
        static ApothecaryUI UI => ApothecaryUI.Instance;
        static T Named<T>(string name) where T : Component => UI.GetComponentsInChildren<T>().FirstOrDefault(v => v.name == name);
        static TextMeshProUGUI Text(string value) => UI.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(v => v.text == value);
        static bool Has(string value) => UI.GetComponentsInChildren<TextMeshProUGUI>().Any(v => v.text.Contains(value));
        static void Set(string field, object value)
        { typeof(ApothecaryUI).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(UI, value); }
        static Button Button(string id)
        { var button = Named<Button>("GStar " + id); Check(button != null, "button exists: " + id); return button; }
        static void DisableDesktopPointer()
        {
            if (EventSystem.current == null) return;
            var module = EventSystem.current.GetComponent<InputSystemUIInputModule>(); if (module != null) module.enabled = false;
            EventSystem.current.SetSelectedGameObject(null);
        }
        static void Click(string id)
        {
            var button = Button(id); Check(button.IsInteractable(), "button enabled: " + id);
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject == button.gameObject, "pointer reaches visible control: " + id);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        static Rect Bounds(Component component)
        {
            var corners = new Vector3[4]; ((RectTransform)component.transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
        static Dictionary<string, Rect> Geometry()
        {
            var result = new Dictionary<string, Rect>();
            foreach (string name in new[] { "GStar input name", "GStar input phone", "GStar register", "GStar result next", "GStar total score", "GStar ranking stage 0", "GStar ranking stage 1", "GStar ranking stage 2" })
                result[name] = Bounds(Named<RectTransform>(name));
            foreach (string text in new[] { "01 기록 · 랭킹", "02 다음 도전", "03 체험 종료", "이번 도전 점수" }) result[text] = Bounds(Text(text));
            result["title"] = Bounds(Text("스테이지 클리어!") ?? Text("스테이지 클리어 실패"));
            return result;
        }
        static void SameGeometry(Dictionary<string, Rect> expected)
        {
            var actual = Geometry();
            foreach (var entry in expected)
            {
                Rect a = actual[entry.Key], b = entry.Value;
                Check(Mathf.Abs(a.x - b.x) < .5f && Mathf.Abs(a.y - b.y) < .5f && Mathf.Abs(a.width - b.width) < .5f && Mathf.Abs(a.height - b.height) < .5f,
                    "clear/failure geometry identical: " + entry.Key);
            }
        }
        static RunScoreData Score(bool success, int normal = 200) => new RunScoreData
        { rules = GStarScoreRules.Version, success = success, normal = normal, special = 3, elite = 1, miniBoss = 1, boss = success ? 1 : 0, cleared = 1, overcharge = 1, gold = 50, silver = 35, seconds = 900 };
        static string Complete(int stage, bool success, int normal = 200)
        {
            Check(GStarSession.TryBeginAttempt(stage, out var reason), "reserve synthetic combat attempt: " + reason);
            Check(GStarSession.MarkAttemptStarted(), "mark synthetic attempt played");
            string id = GStarSession.RunId;
            Check(GStarLeaderboard.CaptureCompleted(id, GStarSession.SessionId, stage, Score(success, normal)), "capture finished combat snapshot");
            Check(GStarSession.CompleteAttempt(success), "complete attempt once");
            Set("passed", success); Set("gstarBoard", stage); Set("gstarNotice", ""); Set("gstarConsent", false);
            UI.Show("result"); return id;
        }
        static IEnumerator RegisterInputs(bool consent = true, string phoneValue = TestPhone)
        {
            var name = Named<TMP_InputField>("GStar input name"); var phone = Named<TMP_InputField>("GStar input phone");
            Check(name != null && phone != null && name.interactable && phone.interactable, "real TMP registration fields editable");
            name.text = "체험검수"; phone.text = phoneValue;
            if (consent) { Click("consent"); yield return Settled(); }
            Click("register"); yield return Settled();
        }
        static IEnumerator CheckInputFocus()
        {
            var field = Named<TMP_InputField>("GStar input name");
            Check(field != null && string.IsNullOrEmpty(field.text) && ((TMP_Text)field.placeholder).text == "별명으로 하셔도 괜찮아요", "nickname hint shown in empty real input");
            Check(field.placeholder.enabled, "unfocused empty name field shows hint");
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, field.transform.position),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject == field.gameObject, "pointer reaches actual name input");
            ExecuteEvents.Execute(field.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(field.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(field.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return Settled();
            Check(field.isFocused && !field.placeholder.enabled, "real input focus hides placeholder");
            Check(field.caretBlinkRate > 0 && field.caretWidth == 3 && field.customCaretColor && field.caretColor.r > .8f, "focused input has blinking three-pixel pink caret");
            Capture("01a-name-focused");
            field.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
            yield return Settled();
            Check(!field.isFocused && field.placeholder.enabled, "empty input hint returns after losing focus");
        }
        static void ValidateNext(int count)
        {
            Check(UI.Page == "gstarNext" && GStarSession.RemainingAttempts == count, "next page remaining attempts " + count);
            var number = Text(count.ToString()); Check(number != null, "live remaining count text");
            Color c = number.colorGradient.bottomLeft;
            Check(count == 1 ? c.r > .8f && c.g < .25f : c.r > .8f && c.g > .6f, "remaining count uses approved warning color");
            for (int i = 0; i < 3; i++)
            {
                var button = Button("challenge " + i);
                Check(button.GetComponentInChildren<TextMeshProUGUI>().text == "도전하기", "each stage has the same challenge caption");
                Check(button.IsInteractable() == (count > 0), "stage challenge availability " + i);
            }
            Check(Button("change character").IsInteractable() == (count > 0), "character change follows attempt availability");
        }
        static IEnumerator KeyPress(InputKey key)
        {
            keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return Settled();
        }
        static IEnumerator Checks()
        {
            Application.runInBackground = true;
            var preferences = GamePreferences.Current.Copy(); preferences.pauseOnFocusLoss = false; preferences.muteOnFocusLoss = false; GamePreferences.Apply(preferences, false);
            DisableDesktopPointer(); keyboard = InputSystem.AddDevice<Keyboard>("GStar QA keyboard");
            Check(GStarDemo.DataDirectory == SessionState.GetString(Key + "Data", "") && GStarLeaderboard.ExportDirectory == GStarDemo.DataDirectory, "session and ranking storage are isolated");
            Check(GStarSession.RemainingAttempts == 3 && UI.Page == "prepare", "fresh visitor starts with three attempts");
            Check(GStarLeaderboard.CaptureCompleted("seed-record", "seed-visitor", 1, Score(false, 50)), "seed independent competitor record");
            Check(GStarLeaderboard.Submit("앞선참가자", "01000000002", 1, "seed-record", "seed-visitor", Score(false, 50)).success, "seed competitor ranked");
            string first = Complete(1, true); yield return Settled();
            Check(Has("스테이지 클리어!") && Has("이번 도전 점수"), "clear title and live score visible");
            var geometry = Geometry(); Capture("01-clear-unregistered");
            yield return CheckInputFocus();
            yield return RegisterInputs(false);
            Check(Has("연락처 저장 동의") && GStarLeaderboard.GetRunRank(first) == null, "registration requires actual consent");
            Click("consent"); yield return Settled(); Click("register"); yield return Settled();
            var firstRank = GStarLeaderboard.GetRunRank(first);
            Check(firstRank != null && firstRank.rank == 1 && Has("내 순위") && Has("등록 완료"), "registration updates official rank live");
            Check(!Named<TMP_InputField>("GStar input name").interactable && !Button("register").IsInteractable(), "submitted record is locked against duplicate editing");
            Check(!UI.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.GetComponentInParent<TMP_InputField>() == null && t.text.Contains(TestPhone)), "contact never appears in public ranking text");
            Capture("02-clear-ranked"); Click("score details"); yield return Settled(); Check(Has("이번 도전 점수 내역"), "score breakdown opens"); Capture("03-score-details"); Click("close details"); yield return Settled();
            Click("result next"); yield return Settled(); ValidateNext(2); Capture("04-next-two");
            string visitor = GStarSession.SessionId; Click("change character");
            yield return Wait(() => UI != null && UI.Page == "prepare", 20, "character change opens actual preparation scene"); yield return Settled(); DisableDesktopPointer();
            Check(GStarSession.SessionId == visitor && GStarSession.RemainingAttempts == 2, "character/loadout change preserves used chance"); Capture("05-preparation-return");
            string second = Complete(1, false, 10); yield return Settled(); SameGeometry(geometry);
            var failedTitle = Text("스테이지 클리어 실패"); Check(failedTitle != null && failedTitle.colorGradient.bottomLeft.b > failedTitle.colorGradient.bottomLeft.r, "failure title uses purple treatment"); Capture("06-failure-unregistered");
            yield return RegisterInputs();
            var secondRank = GStarLeaderboard.GetRunRank(second);
            Check(secondRank != null && secondRank.score == firstRank.score && secondRank.runId == first && GStarLeaderboard.GetTop(1).Count == 2, "lower repeat retains official best and one prize place");
            Check(Has("개인 최고 기록"), "lower score explains official personal-best ranking"); Capture("07-failure-ranked");
            Click("result next"); yield return Settled(); ValidateNext(1); Capture("08-next-one-red");
            string third = Complete(2, true, 250); yield return Settled();
            Check(((TMP_Text)Named<TMP_InputField>("GStar input phone").placeholder).text == "필수로 입력하지 않으셔도 됩니다!", "phone placeholder explicitly says optional");
            yield return RegisterInputs(false, "");
            Check(GStarLeaderboard.GetParticipantRank("", GStarSession.SessionId, 2) != null && Has("내 순위"), "anonymous registration needs no phone consent and shows own rank");
            Check(GStarLeaderboard.GetRunRank(third).stageId == 2 && GStarLeaderboard.GetTop(2).Count == 1, "third attempt uses independent stage leaderboard");
            Click("result next"); yield return Settled(); ValidateNext(0); Capture("09-next-zero-blocked");
            Check(!GStarSession.TryBeginAttempt(1, out _), "fourth attempt refused by persistent session");
            Click("finish experience"); yield return Settled();
            Check(UI.Page == "gstarSurvey" && GStarSession.VisitorFinished, "finish opens survey and closes visitor");
            Check(Text("체험 설문").color == Color.black && Text("여러분의 의견을 들려주세요!").colorGradient.bottomLeft.g > .7f, "survey caption black and feedback heading mint");
            foreach(string qr in new[]{"survey-qr","homepage-qr"})
            {
                var image=Named<Image>("GStar "+qr);
                Check(image!=null&&image.sprite!=null,"real QR sprite visible: "+qr);
                Check(image.preserveAspect&&!image.raycastTarget,"QR preserves aspect and does not block controls: "+qr);
                Check(image.sprite.texture.filterMode==FilterMode.Point&&image.sprite.texture.mipmapCount==1,"QR modules remain sharp without mipmaps: "+qr);
                var bounds=Bounds(image);Check(Mathf.Abs(bounds.width-bounds.height)<.5f,"QR uses a square display region: "+qr);
            }
            Check(Named<Button>("GStar new visitor") == null, "public survey has no reset control"); Capture("10-survey");
            yield return KeyPress(InputKey.Escape); Check(UI.Page == "gstarSurvey", "Escape cannot reset or leave end screen");
            yield return KeyPress(InputKey.F8);
            Check(Directory.Exists(Path.Combine(GStarDemo.DataDirectory,"analytics","exports")) &&
                Directory.GetFiles(Path.Combine(GStarDemo.DataDirectory,"analytics","exports"),"character-summary.csv",SearchOption.AllDirectories).Length>0,
                "F8 exports QA summary from the actual paused end screen");
            yield return KeyPress(InputKey.F9); Check(Named<Button>("GStar new visitor") != null, "F9 opens real staff control"); Capture("11-staff");
            Click("export records"); yield return Settled();
            Check(File.Exists(Path.Combine(GStarLeaderboard.ExportDirectory, "prize-top3-private.csv")), "staff exports prize contacts locally");
            int before = GStarLeaderboard.GetTop(1, 100).Count; Click("new visitor");
            yield return Wait(() => UI != null && UI.Page == "prepare", 20, "staff reset returns to preparation"); yield return Settled();
            Check(GStarSession.RemainingAttempts == 3 && GStarSession.SessionId != visitor && !GStarSession.VisitorFinished, "new visitor receives exactly three chances");
            Check(GStarLeaderboard.GetTop(1, 100).Count == before && GStarLeaderboard.GetRunRank(first).score == firstRank.score, "staff reset retains all cumulative rankings"); Capture("12-new-visitor-ranking-kept");
            InputSystem.RemoveDevice(keyboard); keyboard = null;
            SessionState.SetBool(Key + "Done", true);
        }
        static void Capture(string name)
        {
            // Render actual runtime canvases through the active game camera, including in batch mode.
            var camera = Camera.main ?? Object.FindObjectOfType<Camera>();
            Check(camera != null, "game camera exists for " + name);
            int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
            var overlays = Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).OrderBy(c => c.sortingOrder).ToArray();
            var cameras = overlays.Select(c => c.worldCamera).ToArray(); var distances = overlays.Select(c => c.planeDistance).ToArray();
            var layers = overlays.Select(c => c.sortingLayerID).ToArray(); var orders = overlays.Select(c => c.sortingOrder).ToArray();
            var overrides = overlays.Select(c => c.overrideSorting).ToArray();
            int captureLayer = SortingLayer.layers.OrderBy(layer => layer.value).Last().id;
            var targetBefore = camera.targetTexture; var activeBefore = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32); Texture2D pixels = null;
            try
            {
                camera.targetTexture = target;
                // Reproduce ScreenSpaceOverlay priority when a camera is required by batch capture.
                for (int i = 0; i < overlays.Length; i++)
                {
                    var canvas = overlays[i]; canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .1f;
                    canvas.overrideSorting = true; canvas.sortingLayerID = captureLayer;
                    canvas.sortingOrder = 32760 - (overlays.Length - 1 - i);
                }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", pixels.EncodeToPNG());
                Check(width >= 960 && height >= 540, "actual UI capture has readable resolution: " + name);
            }
            finally
            {
                for (int i = 0; i < overlays.Length; i++)
                {
                    overlays[i].renderMode = RenderMode.ScreenSpaceOverlay; overlays[i].worldCamera = cameras[i]; overlays[i].planeDistance = distances[i];
                    overlays[i].sortingLayerID = layers[i]; overlays[i].sortingOrder = orders[i]; overlays[i].overrideSorting = overrides[i];
                }
                camera.targetTexture = targetBefore; RenderTexture.active = activeBefore; RenderTexture.ReleaseTemporary(target);
                if (pixels != null) Object.Destroy(pixels); Canvas.ForceUpdateCanvases();
            }
        }
    }
    public sealed class GStarUITestHost : MonoBehaviour { }
}
