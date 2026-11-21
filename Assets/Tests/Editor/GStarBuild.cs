using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public static class GStarBuild
    {
        public static void PrepareArt()
        {
            foreach(string path in Directory.GetFiles("Assets/Resources/GStarDemo","*.png"))
            {
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null)continue;
                string artwork = Path.GetFileNameWithoutExtension(path);
                bool qr=artwork.EndsWith("-qr",StringComparison.Ordinal);
                bool hourglass = artwork.StartsWith("broken-hourglass-", StringComparison.Ordinal);
                bool sprite = qr || hourglass || artwork == "prize-draw-gift";
                importer.textureType=sprite?TextureImporterType.Sprite:TextureImporterType.Default;
                if(sprite)importer.spriteImportMode=SpriteImportMode.Single;
                importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
                importer.mipmapEnabled=false;importer.filterMode=qr||hourglass?FilterMode.Point:FilterMode.Bilinear;
                if(qr)importer.wrapMode=TextureWrapMode.Clamp;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
        public static void Run()
        {
            PrepareArt();
            string oldName=PlayerSettings.productName,oldDefines=PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
            bool oldDev=EditorUserBuildSettings.development;
            try
            {
                PlayerSettings.productName="24시간의 사투 지스타2026";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone,string.Join(";",oldDefines.Split(';').Where(x=>x!="GC_STEAM")));
                EditorUserBuildSettings.development=false;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                    locationPathName="Builds/GStar2026-Windows/24tu-GStar2026.exe",
                    target=BuildTarget.StandaloneWindows64,options=BuildOptions.None
                });
                if(report==null||report.summary.result!=BuildResult.Succeeded)throw new Exception("GStar build failed");
                Debug.Log("[GStarBuild] Success bytes="+report.summary.totalSize);
            }
            finally
            {
                PlayerSettings.productName=oldName;
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone,oldDefines);
                EditorUserBuildSettings.development=oldDev;
            }
        }
    }
}
