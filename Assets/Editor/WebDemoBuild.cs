using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Vampire.Editor
{
    public static class WebDemoBuild
    {
        [MenuItem("24투/Build browser demo (10,000 silver)")]
        public static void Run()
        {
            int exitCode = 1;
            string oldTemplate = PlayerSettings.WebGL.template;
            var oldCompression = PlayerSettings.WebGL.compressionFormat;
            bool oldFallback = PlayerSettings.WebGL.decompressionFallback;
            bool oldCache = PlayerSettings.WebGL.dataCaching;
            bool oldHashes = PlayerSettings.WebGL.nameFilesAsHashes;
            int oldWidth = PlayerSettings.defaultWebScreenWidth;
            int oldHeight = PlayerSettings.defaultWebScreenHeight;
            int oldMemory = PlayerSettings.WebGL.initialMemorySize;
            var oldSubtarget = EditorUserBuildSettings.webGLBuildSubtarget;
            bool mobile = Environment.GetEnvironmentVariable("PROJECT_GC_WEB_MOBILE") == "1";
            WebDemoTextureScope textures = null;
            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                    throw new InvalidOperationException("Install WebGL Build Support for this Unity version first.");
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                    throw new InvalidOperationException("Switch to WebGL first, or launch Unity with -buildTarget WebGL.");
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length != 2 || !scenes[0].EndsWith("Main Menu.unity") || scenes[1] != "Assets/Scenes/Game/Level 1.unity")
                    throw new InvalidOperationException("Expected the current lobby and Level 1 in that order.");

                EditorUserBuildSettings.webGLBuildSubtarget = mobile ? WebGLTextureSubtarget.ASTC : WebGLTextureSubtarget.DXT;
                textures = new WebDemoTextureScope(scenes, mobile);

                PlayerSettings.WebGL.template = mobile ? "PROJECT:24tuMobileWeb" : "PROJECT:24tuWebDemo";
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                // Works on static hosts without special Content-Encoding configuration.
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.dataCaching = true;
                PlayerSettings.WebGL.nameFilesAsHashes = true;
                PlayerSettings.WebGL.initialMemorySize = 256;
                PlayerSettings.defaultWebScreenWidth = 1280;
                PlayerSettings.defaultWebScreenHeight = 720;
                string folder = Environment.GetEnvironmentVariable("PROJECT_GC_WEB_OUTPUT");
                if (string.IsNullOrWhiteSpace(folder)) folder = "Builds/WebDemo";
                folder = Path.GetFullPath(folder);
                Directory.CreateDirectory(folder);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = folder,
                    target = BuildTarget.WebGL,
                    extraScriptingDefines = mobile ? new[] { "PROJECT_GC_WEB_DEMO", "PROJECT_GC_MOBILE_WEB" } : new[] { "PROJECT_GC_WEB_DEMO" },
                    options = BuildOptions.CompressWithLz4HC
                });
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Web build failed; inspect the Unity build log.");
                File.Copy("LICENSE", Path.Combine(folder, "LICENSE.txt"), true);
                File.Copy("Tools/Distribution/ThirdPartyNotices.md", Path.Combine(folder, "ThirdPartyNotices.md"), true);
                File.Copy("Documentation/WebDemo.md", Path.Combine(folder, "WEB_DEMO_GUIDE.md"), true);
                File.Copy("Tools/Distribution/Launch-WebDemo.ps1", Path.Combine(folder, "Launch-WebDemo.ps1"), true);
                File.Copy("Tools/Distribution/Launch-WebDemo.cmd", Path.Combine(folder, "Launch-WebDemo.cmd"), true);
                File.Copy(mobile ? "Tools/Distribution/MobileWebDemo-Readme.txt" : "Tools/Distribution/WebDemo-Readme.txt", Path.Combine(folder, "시작방법.txt"), true);
                File.Copy("Assets/Junhan/Art/PhoenixSkills/HyukiActive.png", Path.Combine(folder, "game-icon.png"), true);
                File.WriteAllText(Path.Combine(folder, "web-build.json"), JsonUtility.ToJson(new BuildInfo
                {
                    utc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion,
                    product = PlayerSettings.productName, version = PlayerSettings.bundleVersion,
                    bytes = report.summary.totalSize, scenes = scenes,
                    sourceCommit = Environment.GetEnvironmentVariable("PROJECT_GC_SOURCE_COMMIT") ?? "unknown",
                    localChangesIncluded = true, sessionMinimumSilver = WebDemoStartup.StartingSilver,
                    profile = mobile ? "mobile-web-astc-qa" : "desktop-web-dxt", textureCompression = mobile ? "ASTC 4x4" : "DXT"
                }, true));
                Debug.Log("[WebDemoBuild] PASS bytes=" + report.summary.totalSize + " path=" + folder);
                exitCode = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                PlayerSettings.WebGL.template = oldTemplate;
                PlayerSettings.WebGL.compressionFormat = oldCompression;
                PlayerSettings.WebGL.decompressionFallback = oldFallback;
                PlayerSettings.WebGL.dataCaching = oldCache;
                PlayerSettings.WebGL.nameFilesAsHashes = oldHashes;
                PlayerSettings.WebGL.initialMemorySize = oldMemory;
                PlayerSettings.defaultWebScreenWidth = oldWidth;
                PlayerSettings.defaultWebScreenHeight = oldHeight;
                EditorUserBuildSettings.webGLBuildSubtarget = oldSubtarget;
                AssetDatabase.SaveAssets();
                if (textures != null) textures.Dispose();
            }
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        [Serializable]
        sealed class BuildInfo
        {
            public string utc, unity, product, version, sourceCommit, profile, textureCompression;
            public string[] scenes;
            public ulong bytes;
            public bool localChangesIncluded;
            public int sessionMinimumSilver;
        }
    }
}
