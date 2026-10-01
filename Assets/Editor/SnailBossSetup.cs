using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vampire;
public static class SnailBossSetup
{
    public const string PrefabPath="Assets/Junhan/Prefabs/Boss/RollCakeSnail.prefab";
    [MenuItem("Tools/Junhan2/Install roll cake snail")]
    public static void Install()
    {
        foreach(string file in Directory.GetFiles("Assets/Resources/SnailBoss","*.png"))
        {
            string path=file.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            if(importer==null){AssetDatabase.ImportAsset(path);importer=(TextureImporter)AssetImporter.GetAtPath(path);}
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=128;importer.filterMode=FilterMode.Bilinear;importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            bool grounded=Path.GetFileNameWithoutExtension(path).StartsWith("Body")||Path.GetFileNameWithoutExtension(path).StartsWith("Groggy")||
                Path.GetFileNameWithoutExtension(path).StartsWith("Puff")||Path.GetFileNameWithoutExtension(path).StartsWith("DashPuff")||
                Path.GetFileNameWithoutExtension(path).StartsWith("Spit")||Path.GetFileNameWithoutExtension(path).StartsWith("BasicSpit")||Path.GetFileNameWithoutExtension(path).StartsWith("Toppings");
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=grounded?new Vector2(.5f,0):new Vector2(.5f,.5f);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
        string settingsPath="Assets/Resources/SnailBossSettings.asset";
        var tuning=AssetDatabase.LoadAssetAtPath<SnailBossSettings>(settingsPath);
        if(tuning==null){tuning=ScriptableObject.CreateInstance<SnailBossSettings>();AssetDatabase.CreateAsset(tuning,settingsPath);}
        var root=new GameObject("RollCakeSnail");root.layer=LayerMask.NameToLayer("Monster Full");
        try
        {
            var rb=root.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.freezeRotation=true;rb.bodyType=RigidbodyType2D.Kinematic;
            var col=root.AddComponent<BoxCollider2D>();col.isTrigger=true;col.size=new Vector2(3.5f,2.6f);col.offset=new Vector2(0,1.3f);
            var controller=root.AddComponent<SnailBossRuntime>();controller.settings=tuning;
            root.AddComponent<SnailBossVisual>();
            // Preview for the existing summon presentation; runtime rig takes over on spawn.
            var preview=new GameObject("Summon preview");preview.transform.SetParent(root.transform,false);
            var sr=preview.AddComponent<SpriteRenderer>();sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/SnailBoss/Shell1.png");
            preview.transform.localPosition=new Vector3(.46f,1.76f,0);preview.transform.localScale=Vector3.one*(3.234f/sr.sprite.bounds.size.x);
            sr.sortingOrder=500;
            var body=SnailBossArt.Make(preview.transform,"Body1",4.2f,502);
            body.transform.position=root.transform.position+Vector3.down*(4.2f*.035f);
            body.transform.localScale=Vector3.one*(4.2f*1.12f/body.sprite.bounds.size.x)/preview.transform.localScale.x;
            var top=SnailBossArt.Make(preview.transform,"Toppings1",4.2f*.53f,503);
            top.transform.position=root.transform.position+new Vector3(.546f,1.764f+sr.sprite.bounds.size.y*preview.transform.localScale.y*.5f-.15f,0);
            top.transform.localScale/=preview.transform.localScale.x;
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        } finally {UnityEngine.Object.DestroyImmediate(root);}
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        foreach(var guid in AssetDatabase.FindAssets("t:LevelBlueprint"))
        {
            var level=AssetDatabase.LoadAssetAtPath<LevelBlueprint>(AssetDatabase.GUIDToAssetPath(guid));
            if(level.finalBoss==null||level.finalBoss.bossPrefab==null)continue;
            level.finalBoss.bossPrefab=prefab;EditorUtility.SetDirty(level);
        }
        AssetDatabase.SaveAssets();Debug.Log("SNAIL_INSTALL_PASS");
        var fieldData=AssetDatabase.LoadAssetAtPath<MonsterBlueprint>("Assets/Prefabs/Monsters/genral Monster BP/AcidLeechMonster_Blueprint.asset");
        if(fieldData!=null){fieldData.name="필드 미니 롤케이크";EditorUtility.SetDirty(fieldData);AssetDatabase.SaveAssets();}
    }
}
