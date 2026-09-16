using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire.Tests.Editor
{
    // Batch-only check. Temporary gallery objects are never saved into a scene.
    [InitializeOnLoad]
    public static class AugmentIconPlaySmoke
    {
        const string Key = "AugmentIconPlaySmoke";
        static double deadline;
        static bool checkedIcons;
        static AugmentIconPlaySmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            AugmentIconTests.InstallAndValidate();
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + "Done", false);
            SessionState.SetBool(Key + "Failed", false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            Attach();
            EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                SessionState.SetBool(Key + "Failed", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (SessionState.GetBool(Key + "Done", false))
            {
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                else if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    bool failed = SessionState.GetBool(Key + "Failed", false);
                    SessionState.SetBool(Key, false);
                    Debug.Log("[AugmentIconPlaySmoke] FINISHED failed=" + failed);
                    EditorApplication.Exit(failed ? 1 : 0);
                }
                return;
            }
            if (EditorApplication.timeSinceStartup > deadline || SessionState.GetBool(Key + "Failed", false))
            {
                SessionState.SetBool(Key + "Failed", true);
                SessionState.SetBool(Key + "Done", true); return;
            }
            if (!EditorApplication.isPlaying || checkedIcons || Time.realtimeSinceStartup < 5) return;
            checkedIcons = true;
            try
            {
                var level = UnityEngine.Object.FindObjectOfType<LevelManager>();
                if (level == null) throw new Exception("Level 1 did not initialize LevelManager.");
                var gallery = new GameObject("Temporary Augment Icon Gallery", typeof(Canvas));
                gallery.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                int count = 0;
                foreach (string raw in Directory.GetFiles("Assets/Junhan/Art/AugmentIcons", "*.png", SearchOption.AllDirectories))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(raw.Replace('\\', '/'));
                    var go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(gallery.transform, false);
                    var image = go.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true;
                    image.rectTransform.sizeDelta = new Vector2(64, 64);
                    image.rectTransform.anchoredPosition = new Vector2((count % 9 - 4) * 70, (count / 9 - 1) * 70);
                    Canvas.ForceUpdateCanvases();
                    var mesh = image.canvasRenderer.GetMesh();
                    if (sprite == null || image.mainTexture != sprite.texture || mesh.vertexCount < 4)
                        throw new Exception("UI sprite failed to produce geometry: " + raw);
                    count++;
                }
                if (count != 27) throw new Exception("Expected 27 UI sprites.");
                UnityEngine.Object.Destroy(gallery);
                Debug.Log("[AugmentIconPlaySmoke] PASS Level 1 initialized; 27 UI sprites produced geometry and correct textures.");
            }
            catch (Exception e) { Debug.LogException(e); }
            SessionState.SetBool(Key + "Done", true);
        }
    }
}
