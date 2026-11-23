using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public static class WkuPromoBuild
    {
        public static void Run()
        {
            string output=Environment.GetEnvironmentVariable("PROJECT_GC_WINDOWS_OUTPUT");
            if(string.IsNullOrEmpty(output))throw new InvalidOperationException("Explicit dedicated WKU QA output is required.");
            PlayerSettings.productName="24시간의 사투 - 원광대 학과홍보 QA";
            PlayerSettings.bundleVersion="0.1.20261124.2";
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(output);
            File.Copy("Documentation/WkuDepartmentPromo.md",Path.Combine(output,"학과홍보-QA-안내.md"),true);
            FestivalDemoBuild.Run();
        }
    }
}
