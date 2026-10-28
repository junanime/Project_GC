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
        public static void Run() => Build(false);

        [MenuItem("24투/Steam/Build Windows Steam release")]
        public static void RunSteam() => Build(true);

        static void Build(bool steam)
        {
            int exitCode = 1;
            uint steamAppId = 0;
            const string steamSettings = "Assets/Resources/SteamReleaseSettings.json";
            bool createdSettings = false;
            var oldIcons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone);
            try
            {
                if (PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone).Split(';').Contains("GC_STEAM"))
                    throw new InvalidOperationException("Remove the global GC_STEAM define; the Steam builder supplies it only for its own build.");
                if (steam)
                {
                    if (!uint.TryParse(Environment.GetEnvironmentVariable("PROJECT_GC_STEAM_APP_ID"), out steamAppId) || steamAppId == 0 || steamAppId == 480)
                        throw new InvalidOperationException("A real PROJECT_GC_STEAM_APP_ID is required. No placeholder or Spacewar build will be produced.");
                    if (File.Exists(steamSettings)) throw new InvalidOperationException("Unexpected SteamReleaseSettings asset exists; inspect it before building.");
                    File.WriteAllText(steamSettings, JsonUtility.ToJson(new SteamReleaseSettings { appId = steamAppId }));
                    createdSettings = true;
                    AssetDatabase.ImportAsset(steamSettings, ImportAssetOptions.ForceSynchronousImport);
                }
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
                if (string.IsNullOrWhiteSpace(folder)) folder = steam ? "Builds/SteamWindows" : "Builds/WindowsDesktop";
                folder = Path.GetFullPath(folder);
                Directory.CreateDirectory(folder);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Path.Combine(folder, "24tu.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.CompressWithLz4HC,
                    extraScriptingDefines = steam ? new[] { "GC_STEAM" } : Array.Empty<string>()
                });
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed; inspect the Unity build log.");
                File.Copy("LICENSE", Path.Combine(folder, "LICENSE.txt"), true);
                File.Copy("README.md", Path.Combine(folder, "PLAY_GUIDE.md"), true);
                File.Copy("Tools/Distribution/ThirdPartyNotices.md", Path.Combine(folder, "ThirdPartyNotices.md"), true);
                var steamPackage = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().FirstOrDefault(p => p.name == "com.rlabrecque.steamworks.net");
                if (steamPackage == null || !File.Exists(Path.Combine(steamPackage.resolvedPath, "LICENSE.md")))
                    throw new InvalidOperationException("Steamworks.NET package license is missing.");
                File.Copy(Path.Combine(steamPackage.resolvedPath, "LICENSE.md"), Path.Combine(folder, "Steamworks.NET-LICENSE.txt"), true);
                if (steam && !Directory.GetFiles(folder, "steam_api64.dll", SearchOption.AllDirectories).Any())
                    throw new InvalidOperationException("Steam native library missing from Windows build.");
                File.WriteAllText(Path.Combine(folder, "desktop-build.json"), JsonUtility.ToJson(new BuildInfo
                {
                    utc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion,
                    product = PlayerSettings.productName, company = PlayerSettings.companyName,
                    version = PlayerSettings.bundleVersion, bytes = report.summary.totalSize,
                    scenes = scenes, sourceCommit = Environment.GetEnvironmentVariable("PROJECT_GC_SOURCE_COMMIT") ?? "unknown",
                    localChangesIncluded = true, iconAsset = AssetDatabase.GetAssetPath(icon)
                    , steamEnabled = steam, steamAppId = steamAppId
                }, true));
                Debug.Log("[WindowsDesktopBuild] PASS bytes=" + report.summary.totalSize + " path=" + folder);
                exitCode = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, oldIcons);
                if (createdSettings) AssetDatabase.DeleteAsset(steamSettings);
                AssetDatabase.SaveAssets();
            }
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        [Serializable]
        sealed class BuildInfo
        {
            public string utc, unity, product, company, version, sourceCommit, iconAsset;
            public string[] scenes;
            public ulong bytes;
            public bool localChangesIncluded;
            public bool steamEnabled;
            public uint steamAppId;
        }
    }
}
