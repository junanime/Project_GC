using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public static class WebDemoAudioSmoke
    {
        public static void Run()
        {
            int exit = 1;
            try
            {
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                using (var scope = new WebDemoAudioScope(scenes))
                {
                    if (scope.Count < 60) throw new Exception("Missing game audio dependencies");
                    long decoded = 0; int checkedClips = 0;
                    foreach (var path in scope.Paths)
                    {
                        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                        var setting = importer.defaultSampleSettings;
                        if (setting.loadType != AudioClipLoadType.DecompressOnLoad || setting.quality < .99f || !setting.preloadAudioData ||
                            setting.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate) throw new Exception("Wrong web profile: " + path);
                        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                        if (clip == null || clip.samples <= 0) throw new Exception("Unreadable sound: " + path);
                        decoded += (long)clip.samples * clip.channels * 4; checkedClips++;
                    }
                    var folder = Path.GetFullPath("../work/WebInputAudio-20261024");
                    Directory.CreateDirectory(folder); scope.WriteManifest(folder);
                    Debug.Log($"WEB_AUDIO_PASS clips={checkedClips} decodedBytes={decoded} original source rate and channels retained");
                }
                if (File.Exists("Library/WebDemoAudioBackup.json")) throw new Exception("Audio imports not restored");
                Debug.Log("WEB_AUDIO_PASS original imports restored"); exit = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            EditorApplication.Exit(exit);
        }
    }
}
