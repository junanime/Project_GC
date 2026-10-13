using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Vampire.Editor
{
 public static class SkillRefreshBuild
 {
  public static void Run(){
   const string output="Builds/SkillRefresh-20261009-PC/24tu.exe";Directory.CreateDirectory(Path.GetDirectoryName(output));
   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
   bool pass=report!=null&&report.summary.result==BuildResult.Succeeded;Debug.Log("SKILL_REFRESH_BUILD "+(pass?"PASS":"FAIL")+" "+output);EditorApplication.Exit(pass?0:1);
  }
 }
}
