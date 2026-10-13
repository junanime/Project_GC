using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.U2D.Sprites;
namespace Vampire.Editor
{
 public static class ShiniEmberInstaller
 {
  const string Folder="Assets/Junhan/Art/ShiniEmber/";
  static Sprite[] Import(string path,int cols,int rows,bool cast,bool flame,out Vector2[] mouths,out float height)
  {
   var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Multiple;
   imp.filterMode=FilterMode.Point;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=4096;imp.mipmapEnabled=false;imp.alphaIsTransparency=true;imp.isReadable=true;imp.spritePixelsPerUnit=100;imp.SaveAndReimport();
   var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);int w=tex.width/cols,h=tex.height/rows;var metas=new SpriteMetaData[cols*rows];mouths=new Vector2[metas.Length];height=1;
   for(int n=0;n<metas.Length;n++){
    int x=n%cols*w,y=tex.height-(n/cols+1)*h;var pix=tex.GetPixels(x,y,w,h);int x0=w,y0=h,x1=0,y1=0;float fx=0,fn=0;int mx=0,my0=h,my1=0,nx=w;float ny=0,nn=0;
    for(int j=0;j<h;j++)for(int i=0;i<w;i++){var c=pix[j*w+i];if(c.a<.6f)continue;x0=Math.Min(x0,i);x1=Math.Max(x1,i);y0=Math.Min(y0,j);y1=Math.Max(y1,j);
     if(cast&&j<h*.3f&&c.r>.47f&&c.g<.49f&&c.r>c.g*1.3f){fx+=i;fn++;}
     if(cast&&j>h*.2f&&j<h*.85f&&c.r>.59f&&c.g>.45f&&c.b<.37f&&c.g/c.r>.5f){mx=Math.Max(mx,i);my0=Math.Min(my0,j);my1=Math.Max(my1,j);}
     if(flame&&j>h*.3f&&j<h*.65f&&i<w*.18f&&c.r>.7f&&c.g>.37f){if(i<nx){nx=i;ny=j;nn=1;}else if(i==nx){ny+=j;nn++;}}
    }
    if(cast||flame){var pivot=cast?new Vector2(fn>0?fx/fn:(x0+x1)*.5f,y0):new Vector2(nx,nn>0?ny/nn:h*.5f);
     metas[n]=new SpriteMetaData{name="frame_"+n.ToString("D2"),rect=new Rect(x,y,w,h),alignment=9,pivot=new Vector2(pivot.x/w,pivot.y/h)};
     mouths[n]=new Vector2(mx+3-pivot.x,(my0+my1)*.5f-pivot.y)/100;
    }else metas[n]=new SpriteMetaData{name="frame_"+n.ToString("D2"),rect=new Rect(x+x0,y+y0,x1-x0+1,y1-y0+1),alignment=9,pivot=new Vector2(.5f,.5f)};
    if(n==0)height=(y1-y0+1)/100f;
   }
   imp=(TextureImporter)AssetImporter.GetAtPath(path);
   var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(imp);provider.InitSpriteEditorDataProvider();
   var old=provider.GetSpriteRects();var rects=metas.Select(m=>new SpriteRect{name=m.name,rect=m.rect,alignment=SpriteAlignment.Custom,pivot=m.pivot,spriteID=old.FirstOrDefault(o=>o.name==m.name)?.spriteID??GUID.Generate()}).ToArray();provider.SetSpriteRects(rects);
   var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();if(names!=null)names.SetNameFileIdPairs(rects.Select(s=>new SpriteNameFileIdPair(s.name,s.spriteID)));provider.Apply();imp.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
  }
  public static void Run()
  {
   AssetDatabase.Refresh();var art=AssetDatabase.LoadAssetAtPath<ShiniEmberArt>("Assets/Junhan/Resources/ShiniEmberArt.asset");
   if(art==null){art=ScriptableObject.CreateInstance<ShiniEmberArt>();AssetDatabase.CreateAsset(art,"Assets/Junhan/Resources/ShiniEmberArt.asset");}
   art.cast=Import(Folder+"Cast.png",4,3,true,false,out var mouths,out var height);art.mouths=mouths;art.castHeight=height;
   art.flames=Import(Folder+"Flame.png",2,4,false,true,out _,out _);
   art.digits=Import(Folder+"Digits.png",5,2,false,false,out _,out _);
   art.ember=Import(Folder+"Ember.png",1,1,false,false,out _,out _)[0];
   // EXP sprite has 405 visible pixels, 1500 PPU, and its prefab visual uses scale .65.
   art.emberHeight=405f/1500*.65f*1.2f;
   var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Junhan/Resources/ShiniBreath.mat");if(mat==null){mat=new Material(Shader.Find("Vampire/ShiniBreath"));AssetDatabase.CreateAsset(mat,"Assets/Junhan/Resources/ShiniBreath.mat");}art.flameMaterial=mat;
   foreach(var name in new[]{"Passive","Active"}){var imp=(TextureImporter)AssetImporter.GetAtPath("Assets/Junhan/Art/ShiniSkills/"+name+".png");imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=4096;imp.filterMode=FilterMode.Point;imp.SaveAndReimport();}
   var d=Resources.Load<CharacterSkillDefinition>("ShiniSkills");d.passiveName="잘 먹겠습니다!";d.activeName="후우우—!";
   d.passiveDescription="적 처치 시 69% 확률로 불씨 1개가 떨어집니다. 획득 범위 안에서 주울 수 있습니다.\n보유 한도 24 → 30 → 36개. 가득 차면 바닥에 남습니다.";
   d.activeDescription="불씨 6개로 전방에 1.5초간 불을 뿜습니다. 꾹 누르면 이후 0.25초마다 1개를 소모해 지속합니다.\n재사용 대기시간 없음. 피해량 100% → 120% → 140%.";
   d.activeDuration=1.5f;d.cooldown=0;d.shiniActiveMoveMultiplier=1;
   d.passiveIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Junhan/Art/ShiniSkills/Passive.png");d.activeIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Junhan/Art/ShiniSkills/Active.png");
   var ashi=Resources.Load<CharacterSkillDefinition>("AshiSkills");ashi.activeDescription="12초간 발사체 수·이동속도·발사체 속도 ×2.\n재사용 45 → 41 → 37 → 33 → 29초.";
   var ari=Resources.Load<CharacterSkillDefinition>("AriSkills");ari.activeDescription="암석 봉황으로 변신합니다. 대쉬 1회 즉시 충전, 충전시간 0.5초.\n접촉 피해 35, 밀치기 0.6. 지속 8 → 10 → 12 → 14 → 16초. 재사용 35초.";
   var hyuki=Resources.Load<CharacterSkillDefinition>("HyukiSkills");hyuki.passiveDescription="상태이상에 걸린 적에게 추가 피해를 줍니다.\n추가 피해 10% → 20% → 30% → 40% → 50%.";
   foreach(var o in new UnityEngine.Object[]{art,d,ashi,ari,hyuki,mat})EditorUtility.SetDirty(o);AssetDatabase.SaveAssets();Debug.Log("[ShiniEmberInstaller] PASS");
   if(Application.isBatchMode)EditorApplication.Exit(0);
  }
 }
}
