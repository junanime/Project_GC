using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    public static class ShiniSkillInstaller
    {
        const string Art="Assets/Junhan/Art/ShiniSkills/";
        static Rect Body(Color[] pixels,int w,int h)
        {
            int x0=w,x1=0,y0=h,y1=0;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                var c=pixels[y*w+x];
                // Coral body/feet and pink cheeks, excluding yellow and red flame tongues.
                if(c.a>.8f && c.r>.75f && c.g>.22f && c.g<.82f && c.b>.20f && c.b<.75f && c.r>c.g*1.12f)
                {x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);}
            }
            if(x0>x1)throw new Exception("No Shini body pixels");
            return new Rect(x0,y0,x1-x0+1,y1-y0+1);
        }
        static Sprite[] Import(string name,Sprite[] originals=null)
        {
            string path=Art+name+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048;importer.wrapMode=TextureWrapMode.Repeat;
            importer.spriteImportMode=originals==null?SpriteImportMode.Single:SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit=400;
            if(originals!=null)
            {
                var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(path));
                var originalTexture=new Texture2D(2,2);originalTexture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(originals[0])));
                var bodies=new Rect[8];var cells=new Rect[8];float ppu=0;
                for(int i=0;i<8;i++)
                {
                    int x=i%4*texture.width/4,y=(1-i/4)*texture.height/2,w=texture.width/4,h=texture.height/2;
                    cells[i]=new Rect(x,y,w,h);bodies[i]=Body(texture.GetPixels(x,y,w,h),w,h);
                    Rect o=originals[i].rect;var ob=Body(originalTexture.GetPixels((int)o.x,(int)o.y,(int)o.width,(int)o.height),(int)o.width,(int)o.height);
                    ppu+=bodies[i].height/(ob.height/originals[i].pixelsPerUnit)/8;
                }
                importer.spritePixelsPerUnit=ppu;
                importer.spritesheet=Enumerable.Range(0,8).Select(i=>new SpriteMetaData{name=name+"_"+i.ToString("00"),rect=cells[i],alignment=9,
                    pivot=new Vector2(bodies[i].center.x/cells[i].width,(bodies[i].yMin+ppu*.27f)/cells[i].height)}).ToArray();
                UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(originalTexture);
            }
            importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        }
        public static void Run()
        {
            try
            {
                AssetDatabase.Refresh();
                var character=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Shini Character Blueprint.asset");
                const string path="Assets/Junhan/Resources/ShiniSkills.asset";
                var data=AssetDatabase.LoadAssetAtPath<CharacterSkillDefinition>(path);
                if(data==null){data=ScriptableObject.CreateInstance<CharacterSkillDefinition>();AssetDatabase.CreateAsset(data,path);}
                data.kind=CharacterSkillDefinition.SkillKind.Shini;
                data.passiveName="불닭의 시작";data.activeName="불닭의 끝";
                data.passiveDescription="실제 대쉬 경로에 불꽃길을 남깁니다. 각 구간은 생성 후 3초 유지됩니다. 화염침과 화상 중첩을 공유합니다.";
                data.activeDescription="봉황 소환·흡수 완료 후 15초간 기존 불닭 모습으로 변신하고 일반 이동에도 불꽃길을 남깁니다. 각 구간 3초 유지. 활성화 시작부터 재사용 35초.";
                data.passiveDuration=3;data.activeDuration=15;data.cooldown=35;
                data.passiveIcon=Import("Passive")[0];data.activeIcon=Import("Active")[0];
                data.burningIdle=Import("Idle",character.idleSpriteSequence);data.burningWalk=Import("Walk",character.walkSpriteSequence);data.burningDash=Import("Dash",character.dashSpriteSequence);
                data.fireTrail=Enumerable.Range(0,7).Select(i=>Import("Trail"+i)[0]).ToArray();
                data.burnVfx=Enumerable.Range(0,3).Select(i=>Import("Burn"+i)[0]).ToArray();
                character.skills=data;EditorUtility.SetDirty(character);EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
                Debug.Log("[Shini] Installed skills and normalized body sprite scales.");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
