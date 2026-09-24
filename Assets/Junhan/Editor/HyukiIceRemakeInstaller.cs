using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    public static class HyukiIceRemakeInstaller
    {
        const string Art="Assets/Junhan/Art/CharacterDesigns/";
        public static void Run()
        {
            try
            {
                AssetDatabase.Refresh();
                var c=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Hyuki Character Blueprint.asset");
                float ppu=c.walkSpriteSequence[0].pixelsPerUnit;
                c.idleSpriteSequence=Import("Hyuki_ColdIdle",ppu);
                c.dashSpriteSequence=Import("Hyuki_IceDash",ppu);
                c.idleFrameTime=.10f;c.dashFrameTime=.0275f;c.fitDashAnimationToDuration=true;c.dashArtMovesRight=true;
                c.resultIdleSpriteSequence=c.idleSpriteSequence;c.resultIdleFrameTime=c.idleFrameTime;
                c.idleHandOffset=new Vector2(.20f,-.06f);
                c.description="추워하면서도 빙결침을 능청스럽게 날리는 얼음 재능꾼.";
                var d=c.skills;
                d.passiveName="하다보면";
                d.passiveDescription="빙결침의 즉시 빙결 확률은 10%로 시작합니다. 즉시 빙결 추첨 실패마다 +1%p(최대 100%), 성공 시 10%로 초기화됩니다. 적별 냉기 4스택 확정 빙결과 액티브 빙결은 누적 확률을 초기화하지 않습니다.";
                const string icon="Assets/Junhan/Art/PhoenixSkills/HyukiHadaBomyeon.png";
                var imp=(TextureImporter)AssetImporter.GetAtPath(icon);Configure(imp);imp.spriteImportMode=SpriteImportMode.Single;imp.SaveAndReimport();
                d.passiveIcon=AssetDatabase.LoadAssetAtPath<Sprite>(icon);
                EditorUtility.SetDirty(c);EditorUtility.SetDirty(d);AssetDatabase.SaveAssets();
                Debug.Log("[HyukiRemake] installed 8 fixed-foot idle frames, 8 ice dash frames, icon and passive; walk PPU preserved: "+ppu);
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Configure(TextureImporter i)
        {
            i.textureType=TextureImporterType.Sprite;i.mipmapEnabled=false;i.alphaIsTransparency=true;
            i.filterMode=FilterMode.Point;i.textureCompression=TextureImporterCompression.Uncompressed;i.maxTextureSize=2048;
            var s=new TextureImporterSettings();i.ReadTextureSettings(s);s.spriteMeshType=SpriteMeshType.FullRect;i.SetTextureSettings(s);
        }
        static Sprite[] Import(string name,float ppu)
        {
            var i=(TextureImporter)AssetImporter.GetAtPath(Art+name+".png");Configure(i);
            i.spriteImportMode=SpriteImportMode.Multiple;i.spritePixelsPerUnit=ppu;
            i.spritesheet=Enumerable.Range(0,8).Select(n=>new SpriteMetaData{
                name=name+"_"+n.ToString("00"),rect=new Rect(n%4*512,(1-n/4)*512,512,512),alignment=9,
                pivot=new Vector2(.5f,(70+ppu*.27f)/512f)
            }).ToArray();i.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(i.assetPath).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        }
    }
}
