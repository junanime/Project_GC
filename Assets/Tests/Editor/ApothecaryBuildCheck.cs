using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Vampire.Tests.Editor
{
    public static class ApothecaryBuildCheck
    {
        public static void Run()
        {
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                locationPathName="Builds/ApothecaryPlayer/24tu.exe",
                target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development
            });
            if(report==null)throw new Exception("Build pipeline returned no report; see the preceding Unity build error.");
            UnityEngine.Debug.Log("[ApothecaryBuild] "+report.summary.result+" errors="+report.summary.totalErrors);
            EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);
        }
    }
}
