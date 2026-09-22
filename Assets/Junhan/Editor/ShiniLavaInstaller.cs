using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    public static class ShiniLavaInstaller
    {
        public static void Run()
        {
            try
            {
                const string root="Assets/Junhan/Art/PhoenixSkills/";
                var source=new Texture2D(2,2);source.LoadImage(File.ReadAllBytes(root+"LavaTornadoSource.png"));
                var atlas=new Texture2D(1536,1024,TextureFormat.RGBA32,false);
                var basePixels=new Color32[256*256];
                for(int frame=0;frame<24;frame++)
                {
                    int ox=frame%6*256,oy=(3-frame/6)*256;
                    var input=source.GetPixels32();int bottom=256,left=256,right=0;
                    // Anchor to the lowest continuous molten footprint, never airborne sparks.
                    for(int y=frame<6?25:0;y<256;y++)for(int x=0;x<256;x++)
                    {var c=input[(oy+y)*source.width+ox+x];if(c.r>100&&c.r>c.b*1.5f){bottom=Math.Min(bottom,y);}}
                    for(int y=bottom;y<Math.Min(256,bottom+16);y++)for(int x=0;x<256;x++)
                    {var c=input[(oy+y)*source.width+ox+x];if(c.r>100&&c.r>c.b*1.5f){left=Math.Min(left,x);right=Math.Max(right,x);}}
                    int dx=128-(left+right)/2,dy=8-bottom;
                    var output=new Color32[256*256];
                    for(int y=0;y<256;y++)for(int x=0;x<256;x++)
                    {
                        int sx=x-dx,sy=y-dy;if(sx<0||sx>=256||sy<(frame<6?25:0)||sy>=256)continue;
                        var c=input[(oy+sy)*source.width+ox+sx];
                        c.a=(byte)(255*Mathf.Clamp01((c.r-c.b-25)/55f));
                        output[y*256+x]=c.a==0?new Color32(0,0,0,0):c;
                    }
                    if(frame==0)Array.Copy(output,basePixels,output.Length);
                    // Reuse the exact first-frame lower footprint; only upper lava and flames animate.
                    if(frame<6)for(int y=0;y<24;y++)for(int x=0;x<256;x++)output[y*256+x]=basePixels[y*256+x];
                    else for(int y=0;y<24;y++)for(int x=0;x<256;x++)output[y*256+x]=new Color32(0,0,0,0);
                    atlas.SetPixels32(ox,oy,256,256,output);
                }
                atlas.Apply();string path=root+"LavaTornado.png";File.WriteAllBytes(path,atlas.EncodeToPNG());
                AssetDatabase.Refresh();var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
                importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Point;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.spritePixelsPerUnit=100;
                importer.spritesheet=Enumerable.Range(0,24).Select(i=>new SpriteMetaData {name="Lava_"+i.ToString("00"),rect=new Rect(i%6*256,(3-i/6)*256,256,256),alignment=9,pivot=new Vector2(.5f,24f/256)}).ToArray();
                importer.SaveAndReimport();
                var d=AssetDatabase.LoadAssetAtPath<CharacterSkillDefinition>("Assets/Junhan/Resources/ShiniSkills.asset");
                d.lavaFrames=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
                d.passiveName="불닭의 시작";d.activeName="불닭의 끝";d.passiveDuration=6;d.activeDuration=8;d.cooldown=35;
                d.shiniActiveMoveMultiplier=1.3f;d.shiniActivePoolInterval=1;
                d.passiveDescription="화염침을 보유하고 시작합니다. 이동 시간 2초마다 6초간 장판을 생성하며 적에게 화상 1스택을 부여합니다(대상당 초당 1회). 대쉬 시 필드의 기존 장판에서 토네이도가 솟아 0.6초 후 화상을 소모합니다. 피해: 침 피해 × (1 + 1.5 × 중첩) + 남은 화상 피해.";
                d.activeDescription="8초간 이동속도 30% 증가, 이동 중 장판 생성 간격 2초 → 1초. 기존 장판과 지속 중 생성되는 새 장판에서 토네이도가 발동합니다. 대쉬로도 기존 장판의 토네이도를 발동할 수 있습니다. 재사용 35초. 수치는 ShiniSkills에서 조절할 수 있습니다.";
                EditorUtility.SetDirty(d);AssetDatabase.SaveAssets();
                UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(atlas);
                Debug.Log("[LavaInstaller] Installed 24 anchored frames and updated Shini rules");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
