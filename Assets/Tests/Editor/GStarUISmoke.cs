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
        public static void Run() => Begin(false);
        public static void RunWithFieldFinishes() => Begin(true);
        static void Begin(bool runFieldFinishes)
        {
            SessionState.SetBool(Key+"ThenField",runFieldFinishes);
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
            started = false; deadline = EditorApplication.timeSinceStartup + 600;
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
                bool fieldNext=SessionState.GetBool(Key+"ThenField",false);
                SessionState.SetBool(Key+"ThenField",false);
                if(fieldNext && !failed)
                {
                    EditorApplication.update-=Tick;
                    Application.logMessageReceived-=Log;
                    EditorApplication.delayCall+=GStarFlowSmoke.Run;
                }
                else EditorApplication.Exit(failed?1:0);
                return;
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
        static void CheckResultLayout()
        {
            var portrait=Named<Image>("GStar result portrait");
            Check(portrait!=null && portrait.sprite!=null && portrait.preserveAspect,"game character portrait keeps its native aspect");
            Check(Has("누구나 경품 추첨") && Has("순위가 높을수록") && !Has("1·2·3위 상품"),"all ranks see the draw invitation");
            var consent=Named<Button>("GStar consent");
            if(consent!=null)
            {
                var box=Named<Image>("GStar consent box");var label=Named<TextMeshProUGUI>("GStar consent label");
                Check(box!=null && Mathf.Abs(Bounds(box).width-Bounds(box).height)<1,"consent is an explicit square checkbox");
                Check(label!=null && label.alignment==TextAlignmentOptions.MidlineLeft,"consent label is joined to checkbox and left aligned");
                Check(!Bounds(consent).Overlaps(Bounds(Button("register"))),"consent hit area does not overlap registration button");
            }
            var gift = Named<Image>("GStar prize draw gift");
            Check(gift != null && gift.sprite == Resources.Load<Sprite>("GStarDemo/prize-draw-gift") && gift.sprite != null,
                "the common prize invitation includes its own transparent gift sprite");
            Check(Button("register").GetComponent<GStarPillGraphic>()==null && Button("register").GetComponent<Image>()!=null,"register uses exact plate skin without a second pill frame");
            Check(Button("result next").GetComponent<GStarPillGraphic>()==null,"result next has no duplicate button frame");
            var tray=Named<GStarPillGraphic>("GStar ranking tabs backing");Check(tray!=null && tray.color.a==1,"opaque ranking tray replaces baked middle selection");
            for(int i=0;i<2;i++) Check(!Bounds(Button("ranking stage "+i)).Overlaps(Bounds(Button("ranking stage "+(i+1)))),"ranking tabs have distinct nonoverlapping bounds "+i);
            foreach(var text in UI.GetComponentsInChildren<TextMeshProUGUI>())
            {
                if(text.name=="GStar heading outline" || text.GetComponentInParent<TMP_InputField>()!=null || string.IsNullOrEmpty(text.text))continue;
                text.ForceMeshUpdate();
                Check(!text.isTextOverflowing || text.overflowMode==TextOverflowModes.Ellipsis,"result text fits or intentionally ellipsizes: "+text.name);
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
            var route=typeof(ApothecaryUI).GetMethod("GStarInitialPage",BindingFlags.Instance|BindingFlags.NonPublic);
            UI.Show((string)route.Invoke(UI,new object[]{false})); return id;
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
                var hook = Text(new[] { "혀 끝에서 이빨로 대시!", "달콤한 냄새를 따라 보스 찾기", "잠든 중간보스를 골라 깨우기" }[i]);
                Check(hook != null && !hook.isTextOverflowing && !Bounds(hook).Overlaps(Bounds(button)),
                    "stage hook is legible above its own challenge button " + i);
                Check(button.GetComponentInChildren<TextMeshProUGUI>().text == "도전하기", "each stage has the same challenge caption");
                Check(button.IsInteractable() == (count > 0), "stage challenge availability " + i);
                var skin=button.targetGraphic as Image;
                Check(skin!=null && skin.GetComponentInParent<Mask>()!=null,"button tint stays inside rounded plate mask "+i);
            }
            Check(Button("change character").IsInteractable() == (count > 0), "character change follows attempt availability");
            Check(Named<Image>("GStar next instruction backing")!=null,"live next instruction replaces old separator art");
        }
        static IEnumerator KeyPress(InputKey key)
        {
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));InputSystem.Update();
            Check(keyboard[key].wasPressedThisFrame,"real key reached the test keyboard: "+key);
            UI.SendMessage("Update",SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            yield return Settled();
        }
        static IEnumerator FilmRanking(string sequence,int frames)
        {
            string folder=Path.Combine(Output,sequence);
            Directory.CreateDirectory(folder);
            int offset=Directory.GetFiles(folder,"*.png").Length;
            for(int i=0;i<frames;i++)
            {
                yield return new WaitForSecondsRealtime(.10f);
                Capture(sequence+"/"+(offset+i).ToString("D3"),960,540);
            }
        }
        static IEnumerator FilmNameEntry(string sequence,string nickname)
        {
            var field=Named<TMP_InputField>("GStar input name");
            field.ActivateInputField();
            foreach(char letter in nickname)
            {
                field.text+=letter;
                field.caretPosition=field.text.Length;
                yield return FilmRanking(sequence,1);
            }
            field.DeactivateInputField();
            EventSystem.current.SetSelectedGameObject(null);
        }

        static IEnumerator Checks()
        {
            Application.runInBackground = true;
            var preferences = GamePreferences.Current.Copy(); preferences.pauseOnFocusLoss = false; preferences.muteOnFocusLoss = false; GamePreferences.Apply(preferences, false);
            var oldInput=InputSystem.settings;
            var input=Object.Instantiate(oldInput);
            input.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            input.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings=input;
            DisableDesktopPointer(); keyboard = InputSystem.AddDevice<Keyboard>("GStar QA keyboard");
            if(!keyboard.enabled)InputSystem.EnableDevice(keyboard);
            Check(GStarDemo.DataDirectory == SessionState.GetString(Key + "Data", "") && GStarLeaderboard.ExportDirectory == GStarDemo.DataDirectory, "session and ranking storage are isolated");
            Check(GStarSession.RemainingAttempts == 2 && UI.Page == "main", "fresh visitor starts on title with two attempts");
            Check(GStarLeaderboard.CaptureCompleted("seed-record", "seed-visitor", 1, Score(false, 50)), "seed independent competitor record");
            Check(GStarLeaderboard.Submit("앞선참가자", "01000000002", 1, "seed-record", "seed-visitor", Score(false, 50)).success, "seed competitor ranked");
            string sameFilm="same-"+GStarSession.SessionId.Substring(0,8);
            string differentFilm="different-"+GStarSession.SessionId.Substring(0,8);
            File.WriteAllLines(Path.Combine(Output,"rank-film-directories.txt"),new[]{sameFilm,differentFilm});
            string first = Complete(1, true); yield return Settled();
            yield return FilmRanking(sameFilm,5);
            ValidateNext(1);
            Check(Has("위장 클리어!") && Has("그만하기") && Named<TMP_InputField>("GStar input name")==null,"first ending shows result summary and second stage choice without an identity form");
            Capture("01-first-next");Capture("01-first-next-1280",1280,720);
            string visitor = GStarSession.SessionId; Click("change character");
            yield return Wait(() => UI != null && UI.Page == "prepare", 20, "character change opens actual preparation scene"); yield return Settled(); DisableDesktopPointer();
            Check(GStarSession.SessionId == visitor && GStarSession.RemainingAttempts == 1, "character/loadout change preserves used chance");
            string second = Complete(1, false, 10); yield return Settled();
            Check(UI.Page=="result" && Has("두 판의 도전 기록") && Has("이번 체험 최고 기록"),"second stage repeat opens both scores, highlights its best");
            Check(Has($"{Score(true).Total:N0}점") && Has($"{Score(false,10).Total:N0}점"),"both completed scores are visible");
            Check(!GStarSession.TryBeginAttempt(1,out _),"third departure is refused");
            CheckResultLayout(); Capture("02-same-before");
            yield return FilmRanking(sameFilm,4);
            yield return FilmNameEntry(sameFilm,"체험검수");
            Named<TMP_InputField>("GStar input name").text="";
            yield return CheckInputFocus();
            yield return RegisterInputs(false);
            Check(Has("연락처 저장 동의") && GStarLeaderboard.GetRunRank(first)==null,"optional contact needs consent");
            Click("consent");yield return Settled();Click("register");yield return Settled();
            var firstRank=GStarLeaderboard.GetRunRank(first);
            Check(firstRank!=null && firstRank.score==Score(true).Total && GStarLeaderboard.GetRunRank(second)==null,"only the higher-scoring repeat entered the ranking");
            Check(GStarLeaderboard.GetTop(1).Count==2 && Has("내 순위")&&!Button("register").IsInteractable(),"one prize place per person per stage");
            Check(!UI.GetComponentsInChildren<TextMeshProUGUI>().Any(v=>v.text.Contains(TestPhone)),"contact stays private after registration, including locked fields");
            yield return FilmRanking(sameFilm,5);
            Capture("03-same-ranked");Click("score details");yield return Settled();
            Check(Has("1차 · 위장 점수 내역"),"first score details");Click("other details");yield return Settled();
            Check(Has("2차 · 위장 점수 내역"),"second score details");Click("close details");yield return Settled();
            Click("result next");yield return Settled();
            Check(UI.Page=="gstarSurvey"&&GStarSession.VisitorFinished,"same-stage visit goes to survey");
            Check(GStarSession.ResetVisitor(),"start isolated two-stage visit");
            Set("gstarDraftName",null);Set("gstarDraftPhone",null);
            string third=Complete(0,false,50);yield return Settled();ValidateNext(1);Capture("04-different-first");
            yield return FilmRanking(differentFilm,4);
            string fourth=Complete(2,true,250);yield return Settled();
            Check(Has("구강 랭킹")&&Has("대장 랭킹"),"different stages show two registration targets");
            Capture("05-different-before");
            yield return FilmRanking(differentFilm,4);yield return FilmNameEntry(differentFilm,"체험검수");
            yield return RegisterInputs(false,"");
            Check(GStarLeaderboard.GetRunRank(third)?.score==Score(false,50).Total &&
                GStarLeaderboard.GetRunRank(fourth)?.score==Score(true,250).Total,"one click registers each stage's best without phone");
            Check(Has("등록 완료 · 구강 1위")&&Has("등록 완료 · 대장 1위"),"both personal stage rankings visible simultaneously");
            Click("ranking stage 0");yield return Settled();Check(Has("내 순위")&&Has("구강 TOP 3"),"first stage ranking visible");Capture("06-different-mouth");yield return FilmRanking(differentFilm,5);
            Click("ranking stage 2");yield return Settled();Check(Has("내 순위")&&Has("대장 TOP 3"),"second stage ranking visible");Capture("07-different-colon");yield return FilmRanking(differentFilm,5);
            Click("result next");yield return Settled();
            Check(UI.Page=="gstarSurvey"&&GStarSession.VisitorFinished,"registered two-stage visitor reaches survey");
            yield return FilmRanking(differentFilm,4);
            Check(Text("체험 설문").color == Color.black && Text("여러분의 의견을 들려주세요!").colorGradient.bottomLeft.g > .7f, "survey caption black and feedback heading mint");
            foreach(string qr in new[]{"survey-qr","homepage-qr"})
            {
                var image=Named<Image>("GStar "+qr);
                Check(image!=null&&image.sprite!=null,"real QR sprite visible: "+qr);
                Check(image.preserveAspect&&!image.raycastTarget,"QR preserves aspect and does not block controls: "+qr);
                Check(image.sprite.texture.filterMode==FilterMode.Point&&image.sprite.texture.mipmapCount==1,"QR modules remain sharp without mipmaps: "+qr);
                var bounds=Bounds(image);Check(Mathf.Abs(bounds.width-bounds.height)<.5f,"QR uses a square display region: "+qr);
            }
            Check(Named<Button>("GStar new visitor") == null, "public survey has no reset control"); Capture("10-survey");Capture("10-survey-1280",1280,720);
            yield return KeyPress(InputKey.Escape); Check(UI.Page == "gstarSurvey", "Escape cannot reset or leave end screen");
            yield return KeyPress(InputKey.F8);
            Check(Directory.Exists(Path.Combine(GStarDemo.DataDirectory,"analytics","exports")) &&
                Directory.GetFiles(Path.Combine(GStarDemo.DataDirectory,"analytics","exports"),"character-summary.csv",SearchOption.AllDirectories).Length>0,
                "F8 exports QA summary from the actual paused end screen");
            yield return KeyPress(InputKey.F9); Check(Named<Button>("GStar new visitor") != null, "F9 opens real staff control");
            Check(!Bounds(Named<TextMeshProUGUI>("GStar staff status")).Overlaps(Bounds(Button("close staff"))),"staff export status does not overlap close button");Capture("11-staff");
            Click("export records"); yield return Settled();
            Check(File.Exists(Path.Combine(GStarLeaderboard.ExportDirectory, "prize-top3-private.csv")), "staff exports prize contacts locally");
            Check(!Bounds(Named<TextMeshProUGUI>("GStar staff status")).Overlaps(Bounds(Button("close staff"))),"prize export status remains separated from close button");Capture("11b-staff-after-export");
            int before = GStarLeaderboard.GetTop(1, 100).Count; Click("new visitor");
            yield return Wait(() => UI != null && UI.Page == "main", 20, "staff reset returns to title"); yield return Settled();
            Check(GStarSession.RemainingAttempts == 2 && GStarSession.SessionId != visitor && !GStarSession.VisitorFinished, "new visitor receives exactly two chances");
            Check(GStarLeaderboard.GetTop(1, 100).Count == before && GStarLeaderboard.GetRunRank(first).score == firstRank.score, "staff reset retains all cumulative rankings"); Capture("12-new-visitor-ranking-kept");
            Complete(1, false, 10); yield return Settled();
            ValidateNext(1);Click("finish experience");yield return Settled();CheckResultLayout();
            Check(UI.Page=="result"&&GStarSession.RegistrationRequested,"early finish opens one-score registration");
            Check(string.IsNullOrEmpty(Named<TMP_InputField>("GStar input name").text) &&
                string.IsNullOrEmpty(Named<TMP_InputField>("GStar input phone").text),
                "a new visitor's first defeat never prefills the preceding visitor's details");
            Capture("13-first-defeat");Click("result next");yield return Settled();
            Check(UI.Page=="gstarSurvey"&&GStarSession.VisitorFinished&&GStarLeaderboard.GetRunRank(GStarSession.LastRunId)==null,
                "skip registration finishes this visitor without ranking their score");
            InputSystem.RemoveDevice(keyboard);keyboard=null;
            InputSystem.settings=oldInput;Object.Destroy(input);
            SessionState.SetBool(Key + "Done", true);
        }
        internal static void Capture(string name,int requestedWidth=0,int requestedHeight=0)
        {
            // Render actual runtime canvases through the active game camera, including in batch mode.
            var camera = Camera.main ?? Object.FindObjectOfType<Camera>();
            Check(camera != null, "game camera exists for " + name);
            int width = requestedWidth>0?requestedWidth:Mathf.Max(1, Screen.width), height = requestedHeight>0?requestedHeight:Mathf.Max(1, Screen.height);
            var overlays = Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).OrderBy(c => c.sortingOrder).ToArray();
            var cameras = overlays.Select(c => c.worldCamera).ToArray(); var distances = overlays.Select(c => c.planeDistance).ToArray();
            // Independently sorted child canvases (e.g. the reward modal) are drawn between
            // overlays by the normal display. Include them when mapping into camera space.
            var sorted = Object.FindObjectsOfType<Canvas>()
                .Where(c => (c.isRootCanvas || c.overrideSorting) && overlays.Contains(c.rootCanvas))
                .OrderBy(c => SortingLayer.GetLayerValueFromID(c.sortingLayerID))
                .ThenBy(c => c.sortingOrder).ThenBy(c => c.renderOrder).ToArray();
            var layers = sorted.Select(c => c.sortingLayerID).ToArray(); var orders = sorted.Select(c => c.sortingOrder).ToArray();
            var overrides = sorted.Select(c => c.overrideSorting).ToArray();
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
                }
                for (int i = 0; i < sorted.Length; i++)
                {
                    sorted[i].overrideSorting = true; sorted[i].sortingLayerID = captureLayer;
                    sorted[i].sortingOrder = 32760 - (sorted.Length - 1 - i);
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
                }
                for (int i = 0; i < sorted.Length; i++)
                {
                    sorted[i].sortingLayerID = layers[i]; sorted[i].sortingOrder = orders[i]; sorted[i].overrideSorting = overrides[i];
                }
                camera.targetTexture = targetBefore; RenderTexture.active = activeBefore; RenderTexture.ReleaseTemporary(target);
                if (pixels != null) Object.Destroy(pixels); Canvas.ForceUpdateCanvases();
            }
        }
    }
    public sealed class GStarUITestHost : MonoBehaviour { }
}
