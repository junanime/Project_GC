using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

namespace Vampire.EditorTools
{
    public static class ApothecaryUIInstaller
    {
        public const string Art = "Assets/Junhan/Art/ApothecaryUI/";
        public const string ConfigPath = "Assets/Junhan/Resources/ApothecaryUIConfig.asset";
        [MenuItem("Tools/Apothecary UI/Install or refresh catalog")]
        public static void Install()
        {
            AssetDatabase.Refresh();
            foreach(string name in new[]{"MainBackground","PanelBackground","AshiFailure","Button"})
            {
                string path=Art+name+".png";
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null)throw new Exception("Missing UI art: "+path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.maxTextureSize=2048;importer.spritePixelsPerUnit=100;
                if(name=="Button")
                {
                    importer.spriteImportMode=SpriteImportMode.Multiple;
                    importer.spritesheet=new[]{new SpriteMetaData{name="Button",rect=new Rect(0,105,2172,540),alignment=9,pivot=new Vector2(.5f,.5f),border=new Vector4(250,110,250,110)}};
                }
                importer.SaveAndReimport();
            }
            var config=AssetDatabase.LoadAssetAtPath<ApothecaryUIConfig>(ConfigPath);
            if(config==null){config=ScriptableObject.CreateInstance<ApothecaryUIConfig>();AssetDatabase.CreateAsset(config,ConfigPath);}
            config.hideFlags=HideFlags.DontUnloadUnusedAsset;
            config.mainBackground=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"MainBackground.png");
            config.panelBackground=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"PanelBackground.png");
            config.failureAshi=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"AshiFailure.png");
            config.buttonBody=AssetDatabase.LoadAllAssetsAtPath(Art+"Button.png").OfType<Sprite>().First();
            config.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Cafe24Danjunghae-v2.asset");
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
                var selector=UnityEngine.Object.FindObjectOfType<CharacterSelector>(true);
                var chars=new SerializedObject(selector).FindProperty("characterBlueprints");
                config.characters=Enumerable.Range(0,chars.arraySize).Select(i=>chars.GetArrayElementAtIndex(i).objectReferenceValue as CharacterBlueprint).Where(c=>c!=null).ToArray();
                // Ashi is always the initial portrait; other existing characters retain their order.
                config.characters=config.characters.OrderBy(c=>c.name=="아시"?0:1).ToArray();
                config.relics=All<RelicBlueprint>().Where(r=>!string.IsNullOrEmpty(r.relicId)).GroupBy(r=>r.relicId).Select(g=>g.First()).ToArray();
                var shop=UnityEngine.Object.FindObjectOfType<LobbyShopUIManager>(true);
                var items=shop!=null?new SerializedObject(shop).FindProperty("shopItems"):null;
                config.items=items!=null?Enumerable.Range(0,items.arraySize).Select(i=>items.GetArrayElementAtIndex(i).objectReferenceValue as MerchantItemBlueprint).Where(i=>i!=null&&i.canBuyInLobby).Distinct().ToArray():All<MerchantItemBlueprint>().Where(i=>i.canBuyInLobby).ToArray();
                EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
                var level=UnityEngine.Object.FindObjectOfType<LevelManager>();
                var blueprint=new SerializedObject(level).FindProperty("levelBlueprint").objectReferenceValue as LevelBlueprint;
                config.augments=blueprint.abilityPrefabs.Where(p=>p!=null).Select(p=>p.GetComponent<Ability>()).Where(a=>a!=null).Select(CatalogEntry).OrderBy(a=>a.tier).ToArray();
                config.hideFlags=HideFlags.None;EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
                Debug.Log($"[Apothecary] Installed {config.characters.Length} characters, {config.relics.Length} relics, {config.items.Length} items, {config.augments.Length} augments.");
            }
            finally { if(previous.Length>0)EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
        static T[] All<T>() where T:UnityEngine.Object => AssetDatabase.FindAssets("t:"+typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>).Where(x=>x!=null).ToArray();
        static ApothecaryUIConfig.AugmentInfo CatalogEntry(Ability ability)
        {
            string title=ability.Name,description=ability.Description;
            if(ability is SyringeSpecialAugmentAbility special)title=Ver4AugmentCatalog.ParentNames[(int)special.Type];
            else if(ability is SyringeLegendaryAugmentAbility)
            {
                string[] names={"생명 연소","복제 배양","고슴도침","대물침","이기어침","신경차단","독 전염","장기압착","위산 연동파","점막 요새","아귀","산탄침"};
                int index=new SerializedObject(ability).FindProperty("augmentType").intValue;
                if(index>=0&&index<names.Length)title=names[index];
            }
            else if(title=="General")title="일반 증강";
            if(string.IsNullOrWhiteSpace(description))description="플레이 중 증강 선택에서 획득합니다.";
            return new ApothecaryUIConfig.AugmentInfo{title=title,description=description,icon=ability.Image,tier=ability.Tier};
        }
        public static void InstallBatch() {Install();EditorApplication.Exit(0);}
    }
}
