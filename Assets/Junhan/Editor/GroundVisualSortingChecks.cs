using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Vampire;

// A preview scene keeps validation independent of the user's open gameplay scene.
public static class GroundVisualSortingChecks
{
    [MenuItem("Tools/Validation/Check Ground Visual Sorting")]
    public static void Run()
    {
        int background = SortingLayer.GetLayerValueFromName("Background");
        int ground = SortingLayer.GetLayerValueFromName(GroundVisualSorting.LayerName);
        Require(SortingLayer.NameToID(GroundVisualSorting.LayerName) != 0, "GroundEffects layer missing");
        Require(background < ground, "Ground must be above Background");
        foreach (var name in new[] { "Default", "Foreground", "Monster Full" })
            Require(ground < SortingLayer.GetLayerValueFromName(name), "Ground must be below " + name);

        Scene scene = EditorSceneManager.NewPreviewScene();
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 1);
        var target = new RenderTexture(32, 32, 24);
        var pixels = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("Sorting validation camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 1;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = target;

            SpriteRenderer floor = MakeSprite(scene, sprite, "Infinite background", Color.blue);
            var backgroundGroup = floor.gameObject.AddComponent<SortingGroup>();
            GroundVisualSorting.ApplyBackground(floor, -1000);
            Require(backgroundGroup.sortingOrder == -1000, "Background SortingGroup must follow its renderer");
            SpriteRenderer room = MakeSprite(scene, sprite, "Mini-stage background", Color.yellow);
            GroundVisualSorting.ApplyBackground(room, -800);
            Expect(camera, pixels, Color.yellow, "Mini-stage covers infinite background");

            SpriteRenderer effect = MakeSprite(scene, sprite, "Ground effect", Color.green);
            GroundVisualSorting.Apply(effect, short.MinValue);
            Expect(camera, pixels, Color.green, "Ground remains above mini-stage background");
            GroundVisualSorting.Apply(effect, short.MaxValue);

            SpriteRenderer actor = MakeSprite(scene, sprite, "Actor or projectile", Color.red);
            actor.sortingOrder = short.MinValue;
            foreach (var layer in new[] { "Default", "Foreground", "Monster Full" })
            {
                actor.sortingLayerName = layer;
                Expect(camera, pixels, Color.red, "Actor stays above ground on " + layer);
            }
            actor.enabled = false;
            effect.gameObject.AddComponent<SortingGroup>().sortingLayerName = "Monster Full";
            var child = new GameObject("Inactive ground particles");
            child.transform.SetParent(effect.transform);
            var particleRenderer = child.AddComponent<ParticleSystem>().GetComponent<Renderer>();
            child.SetActive(false);
            GroundVisualSorting.ApplyHierarchy(effect.gameObject, 123);
            Require(particleRenderer.sortingLayerName == GroundVisualSorting.LayerName, "Inactive particles use the ground layer");
            Require(effect.GetComponent<SortingGroup>().sortingLayerName == GroundVisualSorting.LayerName, "Ground SortingGroup uses the ground layer");
            actor.enabled = true;
            actor.sortingLayerName = "Default";
            Expect(camera, pixels, Color.red, "Grouped ground remains below actors");
            Debug.Log("GROUND_SORTING_CHECKS_PASSED: layer hierarchy, background groups, six rendered overlap cases, inactive particles.");
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(pixels);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static SpriteRenderer MakeSprite(Scene scene, Sprite sprite, string name, Color color)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        return renderer;
    }

    private static void Expect(Camera camera, Texture2D pixels, Color expected, string message)
    {
        camera.Render();
        RenderTexture.active = camera.targetTexture;
        pixels.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
        pixels.Apply();
        Color actual = pixels.GetPixel(16, 16);
        Require(Mathf.Abs(actual.r - expected.r) < 0.05f && Mathf.Abs(actual.g - expected.g) < 0.05f && Mathf.Abs(actual.b - expected.b) < 0.05f,
            message + ": expected " + expected + ", got " + actual);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
