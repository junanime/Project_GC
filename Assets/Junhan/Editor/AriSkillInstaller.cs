using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    public static class AriSkillInstaller
    {
        // Anatomical calibration, not full bounds: wings/tail/dust must not shrink the torso.
        // Coordinates use top-left origin in each lossless 4x2 sheet cell.
        static Sprite[] Frames(string name,float[] ppu,float[] hips,float[] ground)
        {
            return Enumerable.Range(0,8).Select(i=>
            {
                string path="Assets/Junhan/Art/AriSkills/Marble/"+name+i+".png";
                var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if(t==null)throw new Exception("Missing marble frame: "+path);
                var imp=(TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
                imp.filterMode=FilterMode.Point;imp.mipmapEnabled=false;imp.alphaIsTransparency=true;
                imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=512;
                var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);
                settings.spriteAlignment=(int)SpriteAlignment.Custom;
                settings.spritePivot=new Vector2(hips[i]/t.width,1-ground[i]/t.height);
                settings.spriteMeshType=SpriteMeshType.FullRect;imp.SetTextureSettings(settings);
                imp.spritePixelsPerUnit=ppu[i];imp.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }).ToArray();
        }
        static float[] Same(float v)=>Enumerable.Repeat(v,8).ToArray();
        static Sprite Icon(string name)
        {
            string path="Assets/Junhan/Art/AriSkills/"+name+".png";
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
            imp.filterMode=FilterMode.Point;imp.mipmapEnabled=false;imp.alphaIsTransparency=true;
            imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=2048;
            imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        public static void Run()
        {
            try
            {
                AssetDatabase.Refresh();
                const string path="Assets/Junhan/Resources/AriSkills.asset";
                var d=AssetDatabase.LoadAssetAtPath<CharacterSkillDefinition>(path);
                if(d==null){d=ScriptableObject.CreateInstance<CharacterSkillDefinition>();AssetDatabase.CreateAsset(d,path);}
                d.kind=CharacterSkillDefinition.SkillKind.Ari;d.activeDuration=8;d.cooldown=35;
                d.ariRollDamage=10;d.ariPhoenixDamage=35;d.ariRollPush=.3f;d.ariPhoenixPush=.6f;
                d.ariRollWidth=1.1f;d.ariPhoenixRange=1.3f;d.ariDashCooldown=.5f;d.ariVisualScale=1.5f;
                d.ariTransformTime=.56f;
                d.passiveName="데굴데굴";d.activeName="나 멋지지";
                d.passiveDescription="침술진을 보유하고 시작합니다. 대쉬 중 몸 크기 1.1배 범위의 적에게 10 피해를 주고 0.3만큼 밀칩니다. 같은 적은 대쉬당 1회 적중. 보스·고정형 적은 밀리지 않습니다.";
                d.activeDescription="8초간 암석 봉황으로 변신합니다. 대쉬 1회 즉시 충전, 충전시간 0.5초. 성체 몸 크기 1.3배 범위에 35 피해, 밀치기 거리 0.6. 체력·피격 판정 유지. 진행 중인 강화 대쉬 후 외형 복귀. 재사용 35초.";
                d.ariPhoenixIdle=Frames("Idle",Same(350),Same(288),Same(416));
                d.ariPhoenixWalk=Frames("Walk",Same(338),Same(291),Same(396));
                d.ariPhoenixDash=Frames("Dash",Same(350),
                    new float[]{267,260,290,292,270,280,282,285},new float[]{430,430,416,416,416,375,386,387});
                // Growth is intentional only in transformation; no bounds auto-fit or runtime scaling.
                d.ariPhoenixTransform=Frames("Transform",new float[]{180,205,270,335,335,340,343,343},
                    new float[]{222,240,252,288,264,280,282,282},new float[]{397,397,397,397,377,377,377,377});
                var ari=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Ari Character Blueprint.asset");
                ari.skills=d;d.passiveIcon=Icon("AriPassiveIcon");d.activeIcon=Icon("AriActiveIcon");
                var formation=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Junhan/Prefabs/Abilities/Rare/AcupunctureFormation.prefab");
                if(formation==null)throw new Exception("Missing actual formation prefab");
                if(!ari.startingAbilities.Contains(formation))ari.startingAbilities=ari.startingAbilities.Concat(new[]{formation}).ToArray();
                EditorUtility.SetDirty(ari);EditorUtility.SetDirty(d);AssetDatabase.SaveAssets();
                Debug.Log("[AriInstaller] Final marble 32 frames, calibrated scale, 1.1/1.3 contact and 0.3/0.6 distance installed.");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
