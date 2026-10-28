using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Vampire.Editor
{
    // Temporarily move Stage 2 Resources to an editor-only folder; preserve GUIDs.
    public sealed class WebDemoStageScope : IDisposable
    {
        const string Holding = "Assets/Editor/WebDemoStageExcluded";
        static readonly string[] Names = { "ColonMap", "ColonMonsters" };
        public WebDemoStageScope()
        {
            Restore();
            if(Directory.Exists("Assets/Resources/Stage2Snails")||File.Exists("Assets/Resources/StageTwoDefinition.asset"))
                throw new InvalidOperationException("Retired Stage 2 resources must not be restored into this release.");
            if(!AssetDatabase.IsValidFolder(Holding)) AssetDatabase.CreateFolder("Assets/Editor","WebDemoStageExcluded");
            try { foreach(var name in Names) Move("Assets/Resources/"+name,Holding+"/"+name); }
            catch { Restore(); throw; }
        }
        static void Move(string from,string to)
        {
            if(!AssetDatabase.IsValidFolder(from)) throw new InvalidOperationException("Missing stage resource folder: "+from);
            var error=AssetDatabase.MoveAsset(from,to);
            if(!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        [MenuItem("24투/Restore browser build stage resources")]
        public static void Restore()
        {
            foreach(var name in Names)
            {
                var from=Holding+"/"+name;
                if(AssetDatabase.IsValidFolder(from)) Move(from,"Assets/Resources/"+name);
            }
            if(AssetDatabase.IsValidFolder(Holding))
            {
                if(Directory.EnumerateFileSystemEntries(Holding).Any()) throw new InvalidOperationException("Unexpected files in "+Holding);
                AssetDatabase.DeleteAsset(Holding);
            }
        }
        public static void Verify(BuildReport report)
        {
            var leaked=report.packedAssets.SelectMany(p=>p.contents).Where(c=>
                c.sourceAssetPath.Contains("/ColonMap/")||c.sourceAssetPath.Contains("/ColonMonsters/")||
                c.sourceAssetPath.Contains("/Stage2Snails/")||c.sourceAssetPath.EndsWith("/StageTwoDefinition.asset")).ToArray();
            if(leaked.Length>0)throw new InvalidOperationException("Stage 2 assets leaked into build: "+string.Join(", ",leaked.Select(x=>x.sourceAssetPath)));
            Debug.Log("[WebDemoStageScope] PASS: no Stage 2 map/monster assets in the browser build.");
        }
        public void Dispose()=>Restore();
    }
}
