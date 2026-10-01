using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Vampire.EditorTools
{
    public static class TrainingUIInstaller
    {
        public static void Install()
        {
            AssetDatabase.Refresh();
            var import=typeof(OctoberContentInstaller).GetMethod("Import",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(string path in Directory.GetFiles("Assets/Resources/TrainingUI","*.png"))
            {
                string normalized=path.Replace('\\','/');
                import.Invoke(null,new object[]{normalized});
                var texture=(TextureImporter)AssetImporter.GetAtPath(normalized);
                texture.maxTextureSize=path.Contains("TrainingBanner")?2048:path.Contains("BasicNeedle")?2048:256;
                texture.filterMode=FilterMode.Bilinear;
                if(path.Contains("BasicNeedle"))texture.spriteImportMode=SpriteImportMode.Single;
                texture.SaveAndReimport();
            }
            foreach(string folder in new[]{"Special","Planned"})
                foreach(string path in Directory.GetFiles("Assets/Junhan/Art/AugmentIcons/"+folder,"*.png"))
                {
                    var texture=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                    texture.spriteImportMode=SpriteImportMode.Single;
                    texture.filterMode=FilterMode.Bilinear;texture.alphaIsTransparency=true;
                    texture.maxTextureSize=2048;texture.mipmapEnabled=false;texture.SaveAndReimport();
                }
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            var basic=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/TrainingUI/BasicNeedle.png");
            if(basic!=null)
            {
                config.basicNeedle=basic;EditorUtility.SetDirty(config);
                foreach(string guid in AssetDatabase.FindAssets("t:Prefab"))
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    var dart=prefab.GetComponent<SyringeDartAbility>();if(dart==null)continue;
                    var so=new SerializedObject(dart);so.FindProperty("image").objectReferenceValue=basic;
                    so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(dart);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[TrainingUI] Artwork imported, original sprite bindings retained.");
        }
        public static void InstallBatch(){Install();EditorApplication.Exit(0);}
    }
}
