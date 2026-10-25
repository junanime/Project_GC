using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public static class SteamCompilationChecks
    {
        public static void Run()
        {
            int exitCode = 1;
            try
            {
                string root = Path.Combine(Environment.GetEnvironmentVariable("PROJECT_GC_STEAM_TEST_OUTPUT") ?? Path.GetTempPath(), "player-compilation-" + Guid.NewGuid().ToString("N"));
                Compile(root, BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, true);
                Compile(root, BuildTarget.WebGL, BuildTargetGroup.WebGL, false);
                Debug.Log("[SteamCompilationChecks] COMPLETE Steam Windows and WebGL scripts; no player packaged or uploaded");
                exitCode = 0;
            }
            catch (Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(exitCode);
        }
        static void Compile(string root, BuildTarget target, BuildTargetGroup group, bool steam)
        {
            string folder = Path.Combine(root, target.ToString());
            Directory.CreateDirectory(folder);
            var compiled = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
            {
                target = target, group = group,
                extraScriptingDefines = steam ? new[] { "GC_STEAM" } : Array.Empty<string>()
            }, folder);
            if (compiled.assemblies == null || !compiled.assemblies.Any(x => Path.GetFileName(x) == "Assembly-CSharp.dll"))
                throw new Exception("Game scripts were not compiled for " + target);
            bool includesSteam = compiled.assemblies.Any(x => Path.GetFileName(x) == "com.rlabrecque.steamworks.net.dll");
            if (includesSteam != steam) throw new Exception("Steam dependency isolation failed for " + target);
            Debug.Log("[SteamCompilationChecks] PASS " + target + " assemblies=" + compiled.assemblies.Count() + " Steam=" + includesSteam);
        }
    }
}
