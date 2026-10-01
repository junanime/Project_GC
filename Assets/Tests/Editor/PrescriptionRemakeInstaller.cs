using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Vampire.Tests.Editor
{
    public static class PrescriptionRemakeInstaller
    {
        public static void Install()
        {
            const string path="Assets/Junhan/Resources/RemakeBalance.asset";
            var balance=AssetDatabase.LoadAssetAtPath<RemakeBalance>(path);
            if(balance==null){balance=ScriptableObject.CreateInstance<RemakeBalance>();AssetDatabase.CreateAsset(balance,path);}
            balance.levelUpChest=AssetDatabase.LoadAssetAtPath<ChestBlueprint>("Assets/Blueprints/Chests/Boss Chest.asset");
            EditorUtility.SetDirty(balance);
            AssetDatabase.SaveAssets();
            if(balance.levelUpChest==null || !balance.levelUpChest.abilityChest || balance.levelUpChest.legendaryAugmentChest)throw new System.Exception("Missing normal level-up chest");
            Debug.Log("[PrescriptionRemake] Assets installed");
        }
    }
}
