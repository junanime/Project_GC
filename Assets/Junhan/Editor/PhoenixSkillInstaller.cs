using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    public static class PhoenixSkillInstaller
    {
        const string Art="Assets/Junhan/Art/PhoenixSkills/";
        static Sprite[] Import(string name,int columns=1,int rows=1)
        {
            string path=Art+name+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048;importer.wrapMode=TextureWrapMode.Clamp;importer.spritePixelsPerUnit=300;
            importer.spriteImportMode=columns*rows==1?SpriteImportMode.Single:SpriteImportMode.Multiple;
            if(columns*rows>1)
            {
                var t=new Texture2D(2,2);t.LoadImage(File.ReadAllBytes(path));
                int w=t.width/columns,h=t.height/rows;
                importer.spritesheet=Enumerable.Range(0,columns*rows).Select(i=>new SpriteMetaData {
                    name=name+"_"+i.ToString("00"),rect=new Rect(i%columns*w,(rows-1-i/columns)*h,w,h),alignment=9,
                    pivot=name=="FireWrapParts"&&i==1?new Vector2(.9f,.34f):name=="FireWrapParts"&&i==2?new Vector2(.1f,.30f):new Vector2(.5f,.5f)
                }).ToArray();UnityEngine.Object.DestroyImmediate(t);
            }
            importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        }
        public static void Run()
        {
            try
            {
                AssetDatabase.Refresh();const string path="Assets/Junhan/Resources/HyukiSkills.asset";
                var d=AssetDatabase.LoadAssetAtPath<CharacterSkillDefinition>(path);
                if(d==null){d=ScriptableObject.CreateInstance<CharacterSkillDefinition>();AssetDatabase.CreateAsset(d,path);}
                d.kind=CharacterSkillDefinition.SkillKind.Hyuki;d.passiveName="자는 동안에도 천재";d.activeName="잠깐 진심";
                d.passiveDescription="대기 1초마다 수면 1스택. 5스택마다 수정 1개가 회전합니다. 대기를 끝내면 전부 소모하여 4초간 스택당 이동속도 +8%.";
                d.activeDescription="거리 4 이내 적을 즉시 최대 5초간 빙결합니다. 다음 빙결침 적중 시 해제됩니다. 보스는 감속 · 재사용 60초.";
                d.passiveDuration=4;d.activeDuration=IceSkillRules.FreezeDuration;d.cooldown=IceSkillRules.ActiveCooldown;
                d.passiveIcon=Import("HyukiPassive")[0];d.activeIcon=Import("HyukiActive")[0];d.phoenixFrames=Import("IcePhoenix",4,4);d.icePrison=Import("IcePrison",4,2);
                d.iceComponents=Import("IceComponents",2,2);
                const string stormPath=Art+"BlizzardReference.png";
                var stormImporter=(TextureImporter)AssetImporter.GetAtPath(stormPath);
                stormImporter.textureType=TextureImporterType.Default;stormImporter.wrapMode=TextureWrapMode.Repeat;stormImporter.mipmapEnabled=false;stormImporter.alphaIsTransparency=true;
                stormImporter.textureCompression=TextureImporterCompression.Uncompressed;stormImporter.maxTextureSize=2048;stormImporter.SaveAndReimport();
                d.blizzardTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(stormPath);
                var character=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Hyuki Character Blueprint.asset");
                character.skills=d;EditorUtility.SetDirty(character);EditorUtility.SetDirty(d);
                var shini=AssetDatabase.LoadAssetAtPath<CharacterSkillDefinition>("Assets/Junhan/Resources/ShiniSkills.asset");
                shini.phoenixFrames=Import("FirePhoenix",4,4);shini.verticalFireTrail=Import("VerticalFire",4,1);EditorUtility.SetDirty(shini);
                shini.fireWrapParts=Import("FireWrapParts",2,2);
                AssetDatabase.SaveAssets();Debug.Log("[Phoenix] Installed Hyuki skills, 5-second shared freeze, 16-pose phoenix assets and vertical fire.");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
