using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public static class DiscoveryArtInstaller
    {
        public static void Install()
        {
            const string scroll = "Assets/Resources/ToadUpdate/NobleScroll.png";
            AssetDatabase.ImportAsset(scroll);
            var importer = (TextureImporter)AssetImporter.GetAtPath(scroll);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.spritesheet = new[] {
                new SpriteMetaData { name="Cover", rect=new Rect(28,104,600,1090), pivot=new Vector2(.5f,.5f), alignment=0 },
                new SpriteMetaData { name="Roller", rect=new Rect(630,588,610,120), pivot=new Vector2(.5f,.5f), alignment=0 }
            };
            importer.SaveAndReimport();
            const string previewPath="Assets/Resources/DiscoveryPreviewArt.asset";
            var preview=AssetDatabase.LoadAssetAtPath<DiscoveryPreviewArt>(previewPath);
            if(preview==null){preview=ScriptableObject.CreateInstance<DiscoveryPreviewArt>();AssetDatabase.CreateAsset(preview,previewPath);}
            preview.health=Icon("Health");preview.potion=Icon("Red Potion");preview.magnet=Icon("Magnet");
            preview.coin=AssetDatabase.LoadAssetAtPath<CoinBlueprint>("Assets/Blueprints/Coin/Coin.asset").coinSprites[CoinType.Bronze1];preview.gem=PrefabIcon("Assets/Prefabs/Exp Gem/經驗球.prefab");
            preview.monster=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/FoodMonsterAnimations/PopcornPom/PopcornPom_FrontRun_01.png");
            preview.ring=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Placeholders/LegacyReplacement/Sprites/UI/CircleOutline.png");
            EditorUtility.SetDirty(preview);
            var paths = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:MonsterBlueprint"))
                foreach (var path in AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(guid), true))
                    if (path.EndsWith(".png")) paths.Add(path);
            foreach (var folder in new[]{"Assets/Resources/ToadUpdate", "Assets/Resources/SnailBoss"})
                if (AssetDatabase.IsValidFolder(folder)) foreach (var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{folder})) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            var entries = new List<VisibleBodyGeometry.Entry>();
            foreach (var path in paths)
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                if (sprites.Length == 0) continue;
                var texture = new Texture2D(2,2); texture.LoadImage(File.ReadAllBytes(path));
                var pixels = texture.GetPixels32();
                foreach (var sprite in sprites)
                {
                    var r = sprite.rect;float sx=(float)texture.width/sprite.texture.width,sy=(float)texture.height/sprite.texture.height;
                    int x0=Mathf.FloorToInt(r.x*sx), y0=Mathf.FloorToInt(r.y*sy),x1=Mathf.Min(texture.width,Mathf.CeilToInt(r.xMax*sx)),y1=Mathf.Min(texture.height,Mathf.CeilToInt(r.yMax*sy));
                    int minX=x1,minY=y1,maxX=x0-1,maxY=y0-1;
                    for(int y=y0;y<y1;y++) for(int x=x0;x<x1;x++)
                        if(pixels[y*texture.width+x].a>32){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
                    if(maxX<minX)continue;
                    var bounds=new Rect((minX/sx-r.x-sprite.pivot.x)/sprite.pixelsPerUnit,(minY/sy-r.y-sprite.pivot.y)/sprite.pixelsPerUnit,
                        (maxX-minX+1)/(sx*sprite.pixelsPerUnit),(maxY-minY+1)/(sy*sprite.pixelsPerUnit));
                    var edgePoints=new List<Vector2>();
                    int step=Mathf.Max(1,(maxY-minY)/64);
                    for(int y=minY;y<=maxY;y+=step)
                    {
                        int left=maxX+1,right=minX-1;
                        for(int x=minX;x<=maxX;x++)if(pixels[y*texture.width+x].a>32){left=Mathf.Min(left,x);right=Mathf.Max(right,x);}
                        if(right<left)continue;
                        edgePoints.Add(new Vector2((left/sx-r.x-sprite.pivot.x)/sprite.pixelsPerUnit,(y/sy-r.y-sprite.pivot.y)/sprite.pixelsPerUnit));
                        edgePoints.Add(new Vector2(((right+1)/sx-r.x-sprite.pivot.x)/sprite.pixelsPerUnit,((y+1)/sy-r.y-sprite.pivot.y)/sprite.pixelsPerUnit));
                    }
                    entries.Add(new VisibleBodyGeometry.Entry{sprite=sprite,bounds=bounds,outline=Hull(edgePoints)});
                }
                Object.DestroyImmediate(texture);
            }
            const string dataPath="Assets/Resources/VisibleBodyGeometry.asset";
            var data=AssetDatabase.LoadAssetAtPath<VisibleBodyGeometry>(dataPath);
            if(data==null){data=ScriptableObject.CreateInstance<VisibleBodyGeometry>();AssetDatabase.CreateAsset(data,dataPath);}
            data.entries=entries.OrderBy(e=>AssetDatabase.GetAssetPath(e.sprite)).ThenBy(e=>e.sprite.name).ToArray();
            EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
            Debug.Log("DISCOVERY_ART baked sprite alpha bounds: "+entries.Count);
        }
        static Sprite Icon(string name)
        {
            return PrefabIcon("Assets/Prefabs/Chest/"+name+".prefab");
        }
        static float Cross(Vector2 o,Vector2 a,Vector2 b)=>(a.x-o.x)*(b.y-o.y)-(a.y-o.y)*(b.x-o.x);
        static Vector2[] Hull(List<Vector2> input)
        {
            var p=input.Distinct().OrderBy(v=>v.x).ThenBy(v=>v.y).ToArray();if(p.Length<3)return p;
            var hull=new List<Vector2>();
            foreach(var point in p){while(hull.Count>=2&&Cross(hull[hull.Count-2],hull[hull.Count-1],point)<=0)hull.RemoveAt(hull.Count-1);hull.Add(point);}
            int lower=hull.Count;
            for(int i=p.Length-2;i>=0;i--){while(hull.Count>lower&&Cross(hull[hull.Count-2],hull[hull.Count-1],p[i])<=0)hull.RemoveAt(hull.Count-1);hull.Add(p[i]);}
            hull.RemoveAt(hull.Count-1);return hull.ToArray();
        }
        static Sprite PrefabIcon(string path)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null)return null;
            return prefab.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r=>!r.name.ToLowerInvariant().Contains("shadow"))?.sprite;
        }
    }
}
