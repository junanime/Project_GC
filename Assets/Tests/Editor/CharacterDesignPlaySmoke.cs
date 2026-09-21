using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class CharacterDesignPlaySmoke
    {
        const string Key = "CharacterDesignSmoke";
        static string Output => Path.GetFullPath("Library/CharacterDesignProof");
        static double deadline, next;
        static int stage, index, previousFrame;
        static Sprite firstIdle;
        static Character player;
        static TrapMonster trap;
        static Vector3 dashStart;
        static CharacterDesignPlaySmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            CharacterDesignTests.Verify(); Directory.CreateDirectory(Output);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Failed", false); SessionState.SetBool(Key + "Done", false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            var view = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(view).position = new Rect(0, 0, 1280, 741);
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            stage = index = 0; previousFrame = -2; next = 0; deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string text, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) SessionState.SetBool(Key + "Failed", true);
        }
        static void Check(bool condition, string message) => CharacterDesignTests.Check(condition, message);
        static SpriteRenderer Renderer => player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (SessionState.GetBool(Key + "Done", false) || SessionState.GetBool(Key + "Failed", false) || EditorApplication.timeSinceStartup > deadline)
            {
                if (EditorApplication.timeSinceStartup > deadline) SessionState.SetBool(Key + "Failed", true);
                if (EditorApplication.isPlaying) { Time.timeScale = 1; EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                bool failed = SessionState.GetBool(Key + "Failed", false); SessionState.SetBool(Key, false);
                Debug.Log("[CharacterDesignSmoke] FINISHED failed=" + failed); EditorApplication.Exit(failed ? 1 : 0); return;
            }
            if (!EditorApplication.isPlaying || ApothecaryUI.Instance == null || EditorApplication.timeSinceStartup < next) return;
            // Editor ticks can run while rendering is stalled by screenshot capture.
            // Let new UI graphics register before testing their real raycast targets.
            if (Time.frameCount < previousFrame + 2) return;
            previousFrame = Time.frameCount;
            next = EditorApplication.timeSinceStartup + .35;
            try
            {
                var ui = ApothecaryUI.Instance; var expected = ui.Config.characters[index];
                switch (stage++)
                {
                    case 0:
                        EventSystem.current.GetComponent<InputSystemUIInputModule>().enabled = false;
                        var prefs = GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss = false; prefs.muteOnFocusLoss = false; GamePreferences.Apply(prefs, false);
                        ui.Show("unlock", 0); break;
                    case 1:
                        var card = ui.GetComponentsInChildren<Button>().First(b => b.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == expected.name));
                        Check(card.GetComponentsInChildren<Image>().Any(i => i.sprite == expected.profileSprite), expected.name + " portrait card"); Click(card); break;
                    case 2:
                        Check(ui.PreviewCharacter == expected, expected.name + " right panel matches selected card");
                        Check(expected.idleSpriteSequence.Contains(ui.CharacterPreview.sprite), expected.name + " right panel idle not profile");
                        firstIdle = ui.CharacterPreview.sprite; Capture("selection-" + index); next += .12; break;
                    case 3:
                        Check(firstIdle != ui.CharacterPreview.sprite, expected.name + " UI idle animates");
                        Click(Button("선택하고 출전 준비")); break;
                    case 4:
                        Check(ui.Page == "prepare" && ui.SelectedCharacter == expected, expected.name + " selection reaches preparation");
                        Capture("prepare-" + index); Click(Button("뒤로")); break;
                    case 5:
                        Click(Button("게임 시작")); Check(ui.SelectedCharacter == expected, "selection survives main navigation"); break;
                    case 6: Click(Button("출전하기")); next += 2; break;
                    case 7:
                        player = UnityEngine.Object.FindObjectOfType<LevelManager>().PlayerCharacter;
                        Check(ui.Page == "hud" && player.Blueprint == expected, expected.name + " actual game actor uses selection");
                        Check(UnityEngine.Object.FindObjectOfType<SyringeDartAbility>() != null, "ordinary needle remains available");
                        UnityEngine.Object.FindObjectOfType<LevelManager>().enabled = false;
                        player.Move(Vector2.zero); break;
                    case 8:
                        Check(expected.idleSpriteSequence.Contains(Renderer.sprite), expected.name + " gameplay idle"); CaptureActor("idle-" + index);
                        player.Move(Vector2.right); break;
                    case 9:
                        Check(expected.walkSpriteSequence.Contains(Renderer.sprite), expected.name + " gameplay walk"); CaptureActor("walk-" + index);
                        player.Move(Vector2.zero); player.LookDirection = Vector2.right; Time.timeScale = .2f;
                        dashStart = player.transform.position; Check(player.TryDash(), "dash starts");
                        Check(Renderer.sprite == expected.dashSpriteSequence[0], "dash starts at frame zero");
                        Check(!Renderer.flipX, "rightward dash uses approved source orientation"); next = EditorApplication.timeSinceStartup + .65; break;
                    case 10:
                        Check(expected.dashSpriteSequence.Contains(Renderer.sprite), "dash animation active");
                        if (index == 2) Check(Renderer.sprite == expected.dashSpriteSequence[1], "moonwalk second pose during second half");
                        Check(player.transform.position.x > dashStart.x, "dash moves right without changing combat"); CaptureActor("dash-" + index); Time.timeScale = 1; next += .7; break;
                    case 11:
                        Check(expected.idleSpriteSequence.Contains(Renderer.sprite), "dash returns to own idle");
                        // Fixture only: test shared capture visuals without adding any new trap behavior.
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Trap Monster.prefab");
                        trap = UnityEngine.Object.Instantiate(prefab).GetComponent<TrapMonster>(); trap.enabled = false;
                        player.LookDirection = Vector2.left; Renderer.flipX = true;
                        var bind = PlayerTrapBindRuntime.GetOrCreate(player);
                        Check(bind.TryBind(trap, player.transform.position), "capture begins"); player.Move(Vector2.right); break;
                    case 12:
                        Check(expected.capturedSpriteSequence.Contains(Renderer.sprite) && Renderer.flipX, expected.name + " capture expression and facing lock");
                        Check(!player.TryDash(), "capture still blocks dash"); CaptureActor("captured-" + index);
                        PlayerTrapBindRuntime.GetOrCreate(player).Release(trap); UnityEngine.Object.Destroy(trap.gameObject); player.Move(Vector2.zero); break;
                    case 13:
                        Check(expected.idleSpriteSequence.Contains(Renderer.sprite), "release returns to selected character");
                        ui.OpenRunBook(); Check(Time.timeScale == 0, "status pause"); ui.CloseRunBook();
                        SceneManager.LoadScene(0); next += 1; break;
                    case 14:
                        Check(ui.SelectedCharacter == expected, "selection survives return to lobby");
                        if (++index == 4) SessionState.SetBool(Key + "Done", true); else stage = 0; break;
                }
            }
            catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key + "Failed", true); }
        }
        static Button Button(string label) => ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b => b.name == "Button " + label);
        static void Click(Button button)
        {
            Check(button.interactable, "click enabled " + button.name); Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position), button = PointerEventData.InputButton.Left };
            var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject == button.gameObject, "actual pointer target " + button.name + " hits=" + string.Join(",", hits.Select(h => h.gameObject.name)));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        static void Capture(string name)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Output + "/" + name + ".png", tex.EncodeToPNG()); UnityEngine.Object.Destroy(tex);
        }
        static void CaptureActor(string name)
        {
            var go = new GameObject("Character proof camera"); var cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = .75f;
            cam.transform.position = new Vector3(Renderer.transform.position.x, Renderer.transform.position.y, -10);
            var target = new RenderTexture(512, 512, 24); var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try { cam.targetTexture = target; cam.Render(); RenderTexture.active = target; tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); tex.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", tex.EncodeToPNG()); }
            finally { RenderTexture.active = previous; cam.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
