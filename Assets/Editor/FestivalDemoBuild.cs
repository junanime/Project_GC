using System;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public static class FestivalDemoBuild
    {
        [MenuItem("24투/Build festival demo for Windows PC")]
        public static void Run()
        {
            if (!FestivalDemo.Settings.enabled) throw new InvalidOperationException("Enable the festival settings before creating this build.");
            foreach (var character in FestivalDemo.Catalog.characters)
            {
                if (FestivalDemo.Preset(character) == null) continue;
                string error = FestivalDemo.Validate(character);
                if (error != null) throw new InvalidOperationException(error + " " + character.name);
            }
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROJECT_GC_WINDOWS_OUTPUT")))
                Environment.SetEnvironmentVariable("PROJECT_GC_WINDOWS_OUTPUT", "Builds/FestivalDemoPC");
            WindowsDesktopBuild.Run();
        }
    }
}
