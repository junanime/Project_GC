using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    // Keep original source files and desktop imports intact. Web uses decoded
    // buffers for consistent pitch and to avoid per-track MediaElement autoplay.
    public sealed class WebDemoAudioScope : IDisposable
    {
        const string BackupPath = "Library/WebDemoAudioBackup.json";
        [Serializable] public sealed class Entry { public string path, original, sha256; }
        [Serializable] sealed class Backup { public List<Entry> entries = new List<Entry>(); }
        readonly Backup backup = new Backup();
        public int Count => backup.entries.Count;
        public System.Collections.Generic.IEnumerable<string> Paths => backup.entries.Select(e => e.path);

        public WebDemoAudioScope(string[] scenes)
        {
            if (File.Exists(BackupPath)) throw new InvalidOperationException("Restore interrupted web audio imports before building.");
            var resources = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p.Contains("/Resources/") && !p.Contains("/Editor/") && !AssetDatabase.IsValidFolder(p));
            using (var hash = SHA256.Create())
                foreach (var path in AssetDatabase.GetDependencies(scenes.Concat(resources).ToArray(), true).Distinct().OrderBy(p=>p))
                    if (path.StartsWith("Assets/") && AssetImporter.GetAtPath(path) is AudioImporter)
                        backup.entries.Add(new Entry { path = path, original = Convert.ToBase64String(File.ReadAllBytes(path + ".meta")),
                            sha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() });
            File.WriteAllText(BackupPath, JsonUtility.ToJson(backup));
            try
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var entry in backup.entries)
                    {
                        var importer = (AudioImporter)AssetImporter.GetAtPath(entry.path);
                        var settings = importer.defaultSampleSettings;
                        settings.loadType = AudioClipLoadType.DecompressOnLoad;
                        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                        settings.quality = 1;
                        settings.preloadAudioData = true;
                        // Unity 2022.3 rejects WebGL sample overrides. It converts
                        // to AAC internally; temporarily adjust defaults instead.
                        importer.defaultSampleSettings = settings;
                        importer.loadInBackground = false;
                        AssetDatabase.WriteImportSettingsIfDirty(entry.path);
                        AssetDatabase.ImportAsset(entry.path, ImportAssetOptions.ForceUpdate);
                    }
                }
                finally { AssetDatabase.StopAssetEditing(); }
            }
            catch { Restore(); throw; }
            Debug.Log("[WebDemoAudio] Prepared " + Count + " original clips: quality 100%, original sample rate/channels, preloaded buffers.");
        }

        public void WriteManifest(string folder)
        {
            // Hashes prove PC/mobile use the same source assets; no copied/replaced music.
            File.WriteAllLines(Path.Combine(folder, "audio-sources.sha256"), backup.entries.Select(e => e.sha256 + "  " + e.path));
        }
        public void Dispose() => Restore();
        [MenuItem("24투/Restore web build audio imports")]
        public static void Restore()
        {
            if (!File.Exists(BackupPath)) return;
            var saved = JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath));
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var entry in saved.entries)
                {
                    if (!entry.path.StartsWith("Assets/") || entry.path.Contains("..")) throw new InvalidOperationException("Invalid audio backup path.");
                    File.WriteAllBytes(entry.path + ".meta", Convert.FromBase64String(entry.original));
                    AssetDatabase.ImportAsset(entry.path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            File.Delete(BackupPath);
            Debug.Log("[WebDemoAudio] Restored original audio imports.");
        }
    }
}
