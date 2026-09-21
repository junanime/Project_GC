using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public static class Ver4MobileBuild
    {
        [MenuItem("24투/Build Galaxy test APK")]
        public static void BuildGalaxy()
        {
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))
                throw new InvalidOperationException("Install Android Build Support, SDK/NDK and OpenJDK for this editor first.");
            // Local test build; no release keystore or store upload is required.
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,"com.junanime.projectgc.galaxytest");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.useCustomKeystore=false;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.runInBackground=false;
            EditorUserBuildSettings.buildAppBundle=false;
            string output=Environment.GetEnvironmentVariable("PROJECT_GC_ANDROID_APK");
            if(string.IsNullOrWhiteSpace(output)) output=Path.GetFullPath("../work/Android/24tu-Ver4-Galaxy.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            if(scenes.Length==0) throw new InvalidOperationException("No enabled build scenes.");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=scenes,locationPathName=output,target=BuildTarget.Android,options=BuildOptions.Development });
            Debug.Log($"[GalaxyBuild] {report.summary.result}, errors={report.summary.totalErrors}, bytes={report.summary.totalSize}, path={output}");
            if(Application.isBatchMode) EditorApplication.Exit(report.summary.result==BuildResult.Succeeded ? 0:1);
        }
    }
}
