using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public static class MobileWebTextureChecks
    {
        public static void Run()
        {
            int code = 1;
            var previousSubtarget = EditorUserBuildSettings.webGLBuildSubtarget;
            try
            {
                EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;
                foreach (var path in new[] {
                    "Assets/Junhan/Art/AshiSkills/marathon-wind-sheet.png",
                    "Assets/Junhan/Art/CharacterDesigns/Hyuki_Captured.png",
                    "Assets/Resources/ToadUpdate/AcidFx.png" })
                {
                    var original = File.ReadAllBytes(path + ".meta");
                    var before = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    int width = before.width, height = before.height;
                    var sprites = Shape(path);
                    try
                    {
                        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                        var settings = importer.GetPlatformTextureSettings("WebGL");
                        settings.name = "WebGL";
                        if (!settings.overridden) settings.maxTextureSize = importer.maxTextureSize;
                        settings.overridden = true;
                        settings.format = TextureImporterFormat.ASTC_4x4;
                        settings.textureCompression = TextureImporterCompression.CompressedHQ;
                        settings.compressionQuality = 50;
                        settings.crunchedCompression = false;
                        importer.SetPlatformTextureSettings(settings);
                        importer.SaveAndReimport();
                        var after = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (after.width != width || after.height != height || Shape(path) != sprites)
                            throw new Exception("ASTC changed dimensions or sprite identity: " + path);
                        if (after.format != TextureFormat.ASTC_4x4)
                            throw new Exception("ASTC import was not retained: " + path + " " + after.format);
                        Debug.Log("[MobileWebTextureChecks] PASS " + path + " " + width + "x" + height + " " + after.format + " sprites unchanged");
                    }
                    finally
                    {
                        File.WriteAllBytes(path + ".meta", original);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                }
                code = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            finally { EditorUserBuildSettings.webGLBuildSubtarget = previousSubtarget; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static string Shape(string path) => string.Join("\n", AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).Select(s => {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long id);
            return guid + ":" + id + ":" + s.name + ":" + JsonUtility.ToJson(s.rect) + ":" + JsonUtility.ToJson(s.pivot) + ":" + s.pixelsPerUnit;
        }));
    }
}
