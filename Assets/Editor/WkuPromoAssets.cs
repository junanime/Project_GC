using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Editor
{
    public static class WkuPromoAssets
    {
        public static void Prepare()
        {
            AssetDatabase.Refresh();
            foreach(string path in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/WkuPromo"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.alphaIsTransparency=true;importer.SaveAndReimport();
            }
            const string output="Assets/Resources/WkuPromo/MonsterLooks.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<WkuMonsterLookCatalog>(output);
            if(catalog==null){catalog=ScriptableObject.CreateInstance<WkuMonsterLookCatalog>();AssetDatabase.CreateAsset(catalog,output);}
            else foreach(var child in AssetDatabase.LoadAllAssetsAtPath(output).OfType<Sprite>().ToArray())Object.DestroyImmediate(child,true);
            var rows=new List<WkuMonsterLook>();var used=new HashSet<string>();
            void Add(string id,string label,int stage,Sprite[] frames,float interval=.12f)
            {
                frames=frames?.Where(s=>s!=null).ToArray();if(frames==null||frames.Length==0)return;
                string key=frames[0].texture.GetInstanceID()+"/"+frames[0].rect;
                if(!used.Add(key))return;
                rows.Add(new WkuMonsterLook{id=id,label=label,stage=stage,frames=frames,frameSeconds=interval});
            }
            Sprite Crop(Texture2D texture,Rect rect,string label)
            {
                var sprite=Sprite.Create(texture,rect,new Vector2(.5f,0),100,0,SpriteMeshType.FullRect);sprite.name=label;AssetDatabase.AddObjectToAsset(sprite,catalog);return sprite;
            }
            var atlas=Resources.Load<Texture2D>("WkuPromo/SourceArt/OralMonsters");var flavors=Resources.Load<Texture2D>("WkuPromo/SourceArt/OralFlavors");
            var rects=new[]{new Rect(47,557,465,301),new Rect(596,556,345,380),new Rect(1051,556,422,329),new Rect(89,100,364,359),new Rect(569,99,374,360),new Rect(1059,96,398,363)};
            string[] names={"줄무늬 사탕","구미 곰","말랑 찹쌀떡","초코 샌드","쿠키 조각","면발 뭉치"};
            for(int kind=0;kind<6;kind++)for(int variant=0;variant<3;variant++)
            {
                var texture=variant==0?atlas:flavors;var rect=rects[kind];
                if(variant==0)rect=new Rect(rect.x*atlas.width/1536f,rect.y*atlas.height/1024f,rect.width*atlas.width/1536f,rect.height*atlas.height/1024f);
                else{int cell=(variant-1)*6+kind;rect=new Rect((cell%3*470+2)*flavors.width/1410f,(cell/3*384+2)*flavors.height/1536f,rect.width*flavors.width/1410f,rect.height*flavors.height/1536f);}
                Add("oral-"+kind+"-"+variant,names[kind]+(variant==0?"":" "+variant),0,new[]{Crop(texture,rect,"oral-"+kind+"-"+variant)});
            }
            string[] folders={"Germ","Cheesecake","PopcornPom","SharkIcecream"};
            string[] patterns={"Germ_Float_*.png","Cheesecake_Walk_*.png","PopcornPom_FrontRun_*.png","SharkIcecream_Walk_*.png"};
            string[] labels={"병균 도넛","치즈케이크","팝콘 폼","상어 아이스크림"};
            for(int i=0;i<folders.Length;i++)Add("stomach-"+folders[i],labels[i],1,Directory.GetFiles("Assets/Art/FoodMonsterAnimations/"+folders[i],patterns[i]).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace(Path.DirectorySeparatorChar,'/'))).ToArray());
            var requested=FestivalDemo.Settings.normalMonsters.Select(s=>s.blueprint).ToArray();
            var level=AssetDatabase.FindAssets("t:LevelBlueprint").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<LevelBlueprint>).First(l=>l.monsters!=null&&requested.All(n=>l.monsters.Any(p=>p.monsterBlueprints.Any(b=>b!=null&&((Object)b).name==n))));
            foreach(string id in requested)
            {
                var pool=level.monsters.First(p=>p.monsterBlueprints.Any(b=>b!=null&&((Object)b).name==id));var blueprint=pool.monsterBlueprints.First(b=>b!=null&&((Object)b).name==id);
                Sprite[] frames=blueprint is SugarCubeMonsterBlueprint sugar?sugar.sugarCubeWalkSpriteSequence:blueprint.walkSpriteSequence;
                if(frames==null||!frames.Any(s=>s!=null))frames=new[]{pool.monstersPrefab.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r=>!r.name.ToLowerInvariant().Contains("shadow")&&r.sprite!=null)?.sprite};
                Add("stomach-original-"+id,blueprint.name,1,frames,Mathf.Max(.05f,blueprint.walkFrameTime));
            }
            string[] colonNames={"동글 똥알","표고 갓둥이","양배추 겹겹이","호두 주름이","세층 똥탑","검은콩 반질이","한바퀴 똥말이","갈라진 똥돌"};
            for(int i=0;i<8;i++)Add("colon-N"+(i+1).ToString("00"),colonNames[i],2,new[]{Resources.Load<Sprite>("ColonMonsters/N"+(i+1).ToString("00"))});
            catalog.looks=rows.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            if(rows.Count<30)throw new Exception("Expected at least 30 genuine ordinary monster appearances.");
            Directory.CreateDirectory("Library/WkuPromoQA");File.WriteAllText("Library/WkuPromoQA/monster-catalog.txt",string.Join("\n",rows.Select(r=>$"stage {r.stage}\t{r.id}\t{r.label}\t{r.frames.Length} frames")));
        }
    }
}
