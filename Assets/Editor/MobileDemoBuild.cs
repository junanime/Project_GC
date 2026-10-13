using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vampire.Editor
{
    // Private tester APK, not a Play Store release. Retains the existing Galaxy test identity.
    public static class MobileDemoBuild
    {
        [MenuItem("24투/Build Android tester APK")]
        public static void Android()
        {
            int exit = 1;
            string output = Environment.GetEnvironmentVariable("PROJECT_GC_ANDROID_APK");
            if (string.IsNullOrEmpty(output)) output = "Builds/Mobile/24tu-Android-20261006.apk";
            output = Path.GetFullPath(output);
            var settings = File.ReadAllBytes("ProjectSettings/ProjectSettings.asset");
            bool oldBundle = EditorUserBuildSettings.buildAppBundle;
            bool oldExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            var textures = new Dictionary<string, byte[]>();
            string oldJavaOptions = Environment.GetEnvironmentVariable("JAVA_TOOL_OPTIONS");
            try
            {
                // Localization bundles have non-ASCII names. Force UTF-8 in both the
                // Gradle launcher and its child JVMs on Korean Windows installations.
                Environment.SetEnvironmentVariable("JAVA_TOOL_OPTIONS", (oldJavaOptions ?? "") + " -Dfile.encoding=UTF-8");
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android) ||
                    EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    throw new InvalidOperationException("Launch with Android build target and installed SDK/NDK/JDK.");
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length != 2 || !scenes[0].EndsWith("Main Menu.unity") || !scenes[1].EndsWith("Level 1.unity"))
                    throw new InvalidOperationException("Expected the current lobby and Level 1.");
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.junanime.projectgc.galaxytest");
                PlayerSettings.bundleVersion = "0.1.20261006";
                PlayerSettings.Android.bundleVersionCode = 20261006;
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.useAPKExpansionFiles = false;
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                PlayerSettings.runInBackground = false;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Junhan/Art/PhoenixSkills/HyukiActive.png");
                if (icon == null) throw new InvalidOperationException("Approved Hyuki icon missing.");
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android,
                    PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Android).Select(_ => icon).ToArray());
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                CompressLargeTextures(scenes, textures);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes, locationPathName = output, target = BuildTarget.Android,
                    options = BuildOptions.CompressWithLz4HC
                });
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Android APK build failed; see build log.");
                Debug.Log("[MobileDemoBuild] PASS bytes=" + new FileInfo(output).Length + " errors=" + report.summary.totalErrors + " path=" + output);
                exit = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                Environment.SetEnvironmentVariable("JAVA_TOOL_OPTIONS", oldJavaOptions);
                // Keep desktop/web settings and authored import data unchanged.
                EditorUserBuildSettings.buildAppBundle = oldBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = oldExport;
                AssetDatabase.SaveAssets();
                File.WriteAllBytes("ProjectSettings/ProjectSettings.asset", settings);
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var entry in textures) File.WriteAllBytes(entry.Key + ".meta", entry.Value);
                }
                finally { AssetDatabase.StopAssetEditing(); }
                Debug.Log("[MobileDemoBuild] Restored project settings and " + textures.Count + " texture imports.");
            }
            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }

        static void CompressLargeTextures(string[] scenes, Dictionary<string, byte[]> backup)
        {
            var resources = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") &&
                p.Contains("/Resources/") && !p.Contains("/Editor/") && !AssetDatabase.IsValidFolder(p));
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in AssetDatabase.GetDependencies(scenes.Concat(resources).ToArray(), true).Distinct())
                {
                    if (!path.StartsWith("Assets/") || !(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                    if (importer.textureType != TextureImporterType.Sprite && importer.textureType != TextureImporterType.Default) continue;
                    importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                    if ((long)w * h < 262144) continue;
                    backup.Add(path, File.ReadAllBytes(path + ".meta"));
                    var platform = importer.GetPlatformTextureSettings("Android");
                    platform.name = "Android";
                    if (!platform.overridden) platform.maxTextureSize = importer.maxTextureSize;
                    platform.overridden = true;
                    platform.format = importer.DoesSourceTextureHaveAlpha() ? TextureImporterFormat.ETC2_RGBA8 : TextureImporterFormat.ETC2_RGB4;
                    platform.textureCompression = TextureImporterCompression.Compressed;
                    platform.compressionQuality = 50;
                    platform.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(platform);
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.Log("[MobileDemoBuild] ETC2 normal quality textures=" + backup.Count + "; source dimensions/slices retained.");
        }
    }
}
