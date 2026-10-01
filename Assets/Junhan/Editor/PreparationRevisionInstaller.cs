using System;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.EditorTools
{
    public static class PreparationRevisionInstaller
    {
        public static void Install()
        {
            AssetDatabase.Refresh();
            var import=typeof(OctoberContentInstaller).GetMethod("Import",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(string name in new[]{"BossAltar","BossAltarIcon","ItemChestIcon","PrescriptionCase"})
            {
                import.Invoke(null,new object[]{"Assets/Resources/OctoberUI/"+name+".png"});
                var texture=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/OctoberUI/"+name+".png");
                texture.maxTextureSize=name.EndsWith("Icon")?256:name=="BossAltar"?512:1024;texture.SaveAndReimport();
            }
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            var balance=Resources.Load<Ver4AugmentBalance>("Ver4AugmentBalance");
            string folder="Assets/Prefabs/PreparationWeapons";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var weapons=config.weapons.ToList();
            for(int i=0;i<5;i++)
            {
                var type=(SyringeSpecialAugmentAbility.SpecialAugmentType)(16+i);
                string path=folder+"/"+type+".prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab==null)
                {
                    var data=new GameObject(type.ToString());data.AddComponent<SyringeSpecialAugmentAbility>().ConfigureNewAugment(type,balance.plannedIcons[i]);
                    prefab=PrefabUtility.SaveAsPrefabAsset(data,path);UnityEngine.Object.DestroyImmediate(data);
                }
                if(!weapons.Any(w=>w.Type==type))weapons.Add(prefab.GetComponent<SyringeSpecialAugmentAbility>());
            }
            config.weapons=weapons.OrderBy(w=>(int)w.Type).ToArray();EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        public static void InstallBatch(){Install();Debug.Log("[PreparationRevision] Import complete");EditorApplication.Exit(0);}
    }
}
