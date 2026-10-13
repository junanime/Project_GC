using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Vampire.Editor
{
    // Move only Stage 2's Resources outside the player during a public Stage 1 build.
    // GUIDs and original files survive; the journal also permits recovery after interruption.
    public sealed class WebDemoStageScope : IDisposable
    {
        const string Root = "Assets/Editor/WebDemoExcluded";
        const string Journal = "Library/WebDemoStageBackup.json";
        const string Geometry = "Assets/Resources/VisibleBodyGeometry.asset";
        static readonly string[] Sources = { "Assets/Resources/Stage2Snails", "Assets/Resources/StageTwoDefinition.asset" };
        [Serializable] sealed class Backup { public string[] sources; public string geometryOriginal; }
        public WebDemoStageScope()
        {
            if(File.Exists(Journal)||AssetDatabase.IsValidFolder(Root))
                throw new InvalidOperationException("Restore interrupted Stage 1 resource exclusion before building.");
            foreach(var source in Sources) if(!File.Exists(source)&&!Directory.Exists(source))
                throw new InvalidOperationException("Missing Stage 2 source: "+source);
            var geometry=AssetDatabase.LoadAssetAtPath<VisibleBodyGeometry>(Geometry);
            if(geometry==null)throw new InvalidOperationException("Missing shared body geometry.");
            File.WriteAllText(Journal,JsonUtility.ToJson(new Backup{sources=Sources,geometryOriginal=Convert.ToBase64String(File.ReadAllBytes(Geometry))}));
            AssetDatabase.CreateFolder("Assets/Editor","WebDemoExcluded");
            try
            {
                // A shared alpha-bound catalog also references these sprites; sever only those entries.
                geometry.entries=geometry.entries.Where(e=>!AssetDatabase.GetAssetPath(e.sprite).StartsWith("Assets/Resources/Stage2Snails/")).ToArray();
                EditorUtility.SetDirty(geometry);AssetDatabase.SaveAssetIfDirty(geometry);
                foreach(var source in Sources) Move(source,Root+"/"+Path.GetFileName(source));
                var roots=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&p.Contains("/Resources/")&&!p.Contains("/Editor/")&&!AssetDatabase.IsValidFolder(p))
                    .Concat(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path)).ToArray();
                var leaked=AssetDatabase.GetDependencies(roots,true).Where(p=>p.StartsWith(Root+"/")).ToArray();
                if(leaked.Length>0)throw new InvalidOperationException("Stage 2 dependencies remain: "+string.Join(", ",leaked));
                Debug.Log("[WebDemoStageScope] PASS: Stage 1 dependency graph excludes Stage 2.");
            }
            catch { Restore();throw; }
        }
        static void Move(string from,string to)
        {
            string error=AssetDatabase.MoveAsset(from,to);
            if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
        }
        public static void Verify(BuildReport report)
        {
            var leaked=report.packedAssets.SelectMany(p=>p.contents).Where(c=>
                c.sourceAssetPath.Contains("/Stage2Snails/")||c.sourceAssetPath.EndsWith("/StageTwoDefinition.asset")).ToArray();
            if(leaked.Length>0)throw new InvalidOperationException("Stage 2 assets leaked into public build: "+string.Join(", ",leaked.Select(c=>c.sourceAssetPath)));
            Debug.Log("[WebDemoStageScope] PASS: no Stage 2 resources in packed build.");
        }
        [MenuItem("24투/Restore web build Stage 2 resources")]
        public static void Restore()
        {
            if(!File.Exists(Journal))return;
            var saved=JsonUtility.FromJson<Backup>(File.ReadAllText(Journal));
            if(saved?.sources==null||!saved.sources.SequenceEqual(Sources))throw new InvalidOperationException("Invalid Stage 2 exclusion journal.");
            foreach(var source in Sources)
            {
                var excluded=Root+"/"+Path.GetFileName(source);
                if(File.Exists(excluded)||Directory.Exists(excluded))Move(excluded,source);
            }
            if(!string.IsNullOrEmpty(saved.geometryOriginal))
            { File.WriteAllBytes(Geometry,Convert.FromBase64String(saved.geometryOriginal));AssetDatabase.ImportAsset(Geometry,ImportAssetOptions.ForceSynchronousImport); }
            if(Directory.Exists(Root)&&Directory.EnumerateFileSystemEntries(Root).Any())
                throw new InvalidOperationException("Unexpected files in temporary exclusion folder; keeping journal.");
            if(AssetDatabase.IsValidFolder(Root))AssetDatabase.DeleteAsset(Root);
            File.Delete(Journal);
            Debug.Log("[WebDemoStageScope] Restored Stage 2 development assets.");
        }
        public void Dispose()=>Restore();
    }
}
