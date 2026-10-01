using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Vampire.Editor
{
    // Builds the same standalone payload used by a local installer or a future Steam depot.
    // Keep company/product names unchanged: they are part of the existing save namespace.
    public static class WindowsDesktopBuild
    {
        [MenuItem("24투/Build Windows desktop test release")]
        public static void Run()
        {
            int exitCode = 1;
            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                    throw new InvalidOperationException("Windows x64 Build Support is required.");
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length != 2 || !scenes[0].EndsWith("Main Menu.unity") || scenes[1] != "Assets/Scenes/Game/Level 1.unity")
                    throw new InvalidOperationException("Expected the current lobby and Level 1 in that order.");
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Junhan/Art/PhoenixSkills/HyukiActive.png");
                if (icon == null) throw new InvalidOperationException("Approved Hyuki active skill icon is missing.");
                var iconSizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone);
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, iconSizes.Select(_ => icon).ToArray());
                AssetDatabase.SaveAssets();
                string folder = Environment.GetEnvironmentVariable("PROJECT_GC_WINDOWS_OUTPUT");
                if (string.IsNullOrWhiteSpace(folder)) folder = "Builds/WindowsDesktop";
                folder = Path.GetFullPath(folder);
                Directory.CreateDirectory(folder);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Path.Combine(folder, "24tu.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.CompressWithLz4HC
                });
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed; inspect the Unity build log.");
                File.Copy("LICENSE", Path.Combine(folder, "LICENSE.txt"), true);
                File.Copy("README.md", Path.Combine(folder, "PLAY_GUIDE.md"), true);
                File.Copy("Tools/Distribution/ThirdPartyNotices.md", Path.Combine(folder, "ThirdPartyNotices.md"), true);
                File.WriteAllText(Path.Combine(folder, "desktop-build.json"), JsonUtility.ToJson(new BuildInfo
                {
                    utc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion,
                    product = PlayerSettings.productName, company = PlayerSettings.companyName,
                    version = PlayerSettings.bundleVersion, bytes = report.summary.totalSize,
                    scenes = scenes, sourceCommit = Environment.GetEnvironmentVariable("PROJECT_GC_SOURCE_COMMIT") ?? "unknown",
                    localChangesIncluded = true, iconAsset = AssetDatabase.GetAssetPath(icon)
                }, true));
                Debug.Log("[WindowsDesktopBuild] PASS bytes=" + report.summary.totalSize + " path=" + folder);
                exitCode = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        [Serializable]
        sealed class BuildInfo
        {
            public string utc, unity, product, company, version, sourceCommit, iconAsset;
            public string[] scenes;
            public ulong bytes;
            public bool localChangesIncluded;
        }
    }
}
