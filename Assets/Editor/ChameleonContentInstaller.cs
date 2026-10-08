using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    public static class ChameleonContentInstaller
    {
        const string Root="Assets/Resources/Chameleons/";
        public static void Run()
        {
            foreach(var name in new[]{"Drift","Foam","Fanta","Latte","Projectiles"})Import(name);
            var settings=AssetDatabase.LoadAssetAtPath<ChameleonSettings>(Root+"Settings.asset");
            if(settings==null){settings=ScriptableObject.CreateInstance<ChameleonSettings>();AssetDatabase.CreateAsset(settings,Root+"Settings.asset");}
            settings.foamPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Junhan/Prefabs/StageEvents/AntacidBubbleZone.prefab");EditorUtility.SetDirty(settings);
            var original=AssetDatabase.LoadAssetAtPath<MiniBossMonsterBlueprint>("Assets/Resources/ToadUpdate/AcidToadBlueprint.asset");
            var blueprints=new MonsterBlueprint[4];
            for(int i=0;i<4;i++)
            {
                var kind=(ChameleonKind)i;string path=Root+kind+"Blueprint.asset";
                var bp=i==2?original:AssetDatabase.LoadAssetAtPath<MiniBossMonsterBlueprint>(path);
                if(bp==null){bp=UnityEngine.Object.Instantiate(original);AssetDatabase.CreateAsset(bp,path);}
                bp.name=ChameleonArt.Names[i];bp.description="고유 탄막 3발 · 3초 은신 후 외곽 경고 1초 순간이동 · 고유 필드 스킬";
                var frames=AssetDatabase.LoadAllAssetsAtPath(Root+kind+".png").OfType<Sprite>().OrderBy(s=>s.name).ToArray();
                bp.walkSpriteSequence=frames.Skip(2).Take(4).ToArray();bp.resultSprite=frames[0];bp.walkFrameTime=.175f;
                bp.visualScaleMultiplier=1;bp.useGroundShadowFootprint=true;bp.groundShadowCenterUV=new Vector2(.5f,.035f);bp.groundShadowSizeUV=new Vector2(.8f,.15f);
                EditorUtility.SetDirty(bp);blueprints[i]=bp;
            }
            foreach(var guid in AssetDatabase.FindAssets("t:LevelBlueprint"))
            {
                var level=AssetDatabase.LoadAssetAtPath<LevelBlueprint>(AssetDatabase.GUIDToAssetPath(guid));bool changed=false;
                foreach(var c in level.monsters??Array.Empty<LevelBlueprint.MonstersContainer>())
                    if(c.monstersPrefab!=null&&c.monstersPrefab.GetComponent<AcidToadMonster>()!=null){c.monsterBlueprints=blueprints;changed=true;}
                if(changed)EditorUtility.SetDirty(level);
            }
            const string prefab="Assets/Junhan/Prefabs/Boss/AcidToad.prefab";var root=PrefabUtility.LoadPrefabContents(prefab);
            try{var anim=root.GetComponentInChildren<SpriteAnimator>(true);anim.GetComponent<SpriteRenderer>().sprite=blueprints[2].resultSprite;PrefabUtility.SaveAsPrefabAsset(root,prefab);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();Debug.Log("CHAMELEON_INSTALL_PASS");
        }
        static void Import(string name)
        {
            string path=Root+name+".png";AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.isReadable=true;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=8192;importer.filterMode=FilterMode.Point;importer.npotScale=TextureImporterNPOTScale.None;
            importer.SaveAndReimport();var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var ordered=new List<Rect>();
            int rows=name=="Projectiles"?1:4;
            for(int row=0;row<rows;row++)
            {
                int columns=name=="Projectiles"?5:(name=="Foam"||name=="Latte")?(row==2?5:row==3?3:4):4;
                for(int col=0;col<columns;col++)
                {
                    var cell=new Rect(col*texture.width/columns,texture.height-(row+1)*texture.height/rows,(col+1)*texture.width/columns-col*texture.width/columns,texture.height/rows);
                    var parts=Components(texture,cell).OrderByDescending(r=>r.width*r.height).ToList();
                    if(parts.Count==0)throw new Exception("Empty atlas cell "+name+" "+row+","+col);
                    ordered.Add(parts[0]);
                }
            }
            var data=new SpriteMetaData[ordered.Count];
            for(int i=0;i<data.Length;i++)data[i]=new SpriteMetaData{name=name+"_"+i.ToString("00"),rect=ordered[i],alignment=9,pivot=new Vector2(.5f,0)};
            importer.spritesheet=data;importer.spritePixelsPerUnit=ordered[0].width/ChameleonArt.Width;importer.SaveAndReimport();
            Debug.Log("CHAMELEON_ATLAS "+name+" frames="+data.Length+" source="+texture.width+"x"+texture.height);
        }
        static List<Rect> Components(Texture2D texture,Rect cell)
        {
            var p=texture.GetPixels32();int w=texture.width,h=texture.height;var seen=new bool[p.Length];var queue=new Queue<int>();var found=new List<Rect>();
            for(int seed=0;seed<p.Length;seed++)
            {
                if(!cell.Contains(new Vector2(seed%w,seed/w))||seen[seed]||p[seed].a<128)continue;int minX=w,minY=h,maxX=0,maxY=0,area=0;queue.Enqueue(seed);seen[seed]=true;
                while(queue.Count>0)
                {
                    int at=queue.Dequeue(),x=at%w,y=at/w;area++;minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
                    foreach(int delta in new[]{-1,1,-w,w}){int n=at+delta;if(n<0||n>=p.Length||!cell.Contains(new Vector2(n%w,n/w))||Math.Abs(n%w-x)>1||seen[n]||p[n].a<128)continue;seen[n]=true;queue.Enqueue(n);}
                }
                if(area<1500)continue;int x0=Math.Max((int)cell.xMin,minX-2),y0=Math.Max((int)cell.yMin,minY-2);found.Add(new Rect(x0,y0,Math.Min((int)cell.xMax,maxX+3)-x0,Math.Min((int)cell.yMax,maxY+3)-y0));
            }
            return found;
        }
    }
}
