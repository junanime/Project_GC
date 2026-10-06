using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    // High-quality desktop-browser compression without resizing sprites or changing PC imports.
    // Original meta bytes include any pre-existing local edits and are restored after the build.
    public sealed class WebDemoTextureScope : IDisposable
    {
        const string BackupPath = "Library/WebDemoTextureBackup.json";
        [Serializable] sealed class Entry { public string path, original; }
        [Serializable] sealed class Backup { public List<Entry> entries = new List<Entry>(); }
        readonly Backup backup = new Backup();

        public WebDemoTextureScope(string[] scenes)
        {
            if (File.Exists(BackupPath))
                throw new InvalidOperationException("An interrupted Web texture build has a backup. Run 24투/Restore web build texture imports first.");
            var resources = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p.Contains("/Resources/") && !p.Contains("/Editor/") && !AssetDatabase.IsValidFolder(p));
            foreach (string path in AssetDatabase.GetDependencies(scenes.Concat(resources).ToArray(), true).Distinct())
            {
                if (!path.StartsWith("Assets/") || !(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                if (importer.textureType != TextureImporterType.Sprite && importer.textureType != TextureImporterType.Default) continue;
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                // Tiny pixel icons stay lossless. Preserve source dimensions, filtering and sprite slicing.
                if ((long)width * height < 262144 || width % 4 != 0 || height % 4 != 0) continue;
                backup.entries.Add(new Entry { path = path, original = Convert.ToBase64String(File.ReadAllBytes(path + ".meta")) });
            }
            File.WriteAllText(BackupPath, JsonUtility.ToJson(backup));
            try { Apply(); }
            catch { Restore(); throw; }
            Debug.Log("[WebDemo] Temporarily compressed " + backup.entries.Count + " large textures for WebGL.");
        }

        void Apply()
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var entry in backup.entries)
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(entry.path);
                    var settings = importer.GetPlatformTextureSettings("WebGL");
                    settings.name = "WebGL";
                    if (!settings.overridden) settings.maxTextureSize = importer.maxTextureSize;
                    settings.overridden = true;
                    settings.format = importer.DoesSourceTextureHaveAlpha() ? TextureImporterFormat.DXT5 : TextureImporterFormat.DXT1;
                    settings.textureCompression = TextureImporterCompression.CompressedHQ;
                    settings.compressionQuality = 100;
                    settings.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(settings);
                    AssetDatabase.WriteImportSettingsIfDirty(entry.path);
                    AssetDatabase.ImportAsset(entry.path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }

        public void Dispose() => Restore();

        [MenuItem("24투/Restore web build texture imports")]
        public static void Restore()
        {
            if (!File.Exists(BackupPath)) return;
            var saved = JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath));
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var entry in saved.entries)
                {
                    if (!entry.path.StartsWith("Assets/") || entry.path.Contains("..")) throw new InvalidOperationException("Invalid texture backup path.");
                    File.WriteAllBytes(entry.path + ".meta", Convert.FromBase64String(entry.original));
                    AssetDatabase.ImportAsset(entry.path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            File.Delete(BackupPath);
            Debug.Log("[WebDemo] Restored original texture imports (including pre-existing local edits).");
        }
    }
}
