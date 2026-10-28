using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    // Bake transparent padding out of needle tip geometry without enabling runtime Read/Write.
    public static class NeedleBarrierGeometryBaker
    {
        [MenuItem("Tools/24tu/Bake needle barrier tip geometry")]
        public static void Bake()
        {
            var data=AssetDatabase.LoadAssetAtPath<VisibleBodyGeometry>("Assets/Resources/VisibleBodyGeometry.asset");
            var entries=new List<VisibleBodyGeometry.Entry>(data.entries);
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Junhan/Prefabs/Projectiles"}))
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var shot=prefab.GetComponent<Projectile>();
                if(!(shot is SyringeProjectile)&&!(shot is SyringePoisonProjectile))continue;
                var serialized=new SerializedObject(shot);
                var renderer=(SpriteRenderer)serialized.FindProperty("projectileSpriteRenderer").objectReferenceValue;
                if(renderer==null||renderer.sprite==null)continue;
                var sprite=renderer.sprite;var path=AssetDatabase.GetAssetPath(sprite);
                var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(path));
                var pixels=texture.GetPixels32();var r=sprite.rect;
                float sx=(float)texture.width/sprite.texture.width,sy=(float)texture.height/sprite.texture.height;
                var points=new List<Vector2>();
                for(int y=Mathf.FloorToInt(r.y*sy);y<Mathf.Min(texture.height,Mathf.CeilToInt(r.yMax*sy));y++)
                {
                    int left=int.MaxValue,right=-1;
                    for(int x=Mathf.FloorToInt(r.x*sx);x<Mathf.Min(texture.width,Mathf.CeilToInt(r.xMax*sx));x++)
                        if(pixels[y*texture.width+x].a>32){left=Mathf.Min(left,x);right=Mathf.Max(right,x);}
                    if(right<0)continue;
                    points.Add(new Vector2((left/sx-r.x-sprite.pivot.x)/sprite.pixelsPerUnit,(y/sy-r.y-sprite.pivot.y)/sprite.pixelsPerUnit));
                    points.Add(new Vector2(((right+1)/sx-r.x-sprite.pivot.x)/sprite.pixelsPerUnit,((y+1)/sy-r.y-sprite.pivot.y)/sprite.pixelsPerUnit));
                }
                Object.DestroyImmediate(texture);
                if(points.Count<3)continue;
                var hull=DiscoveryArtInstaller.Hull(points);
                entries.RemoveAll(e=>e.sprite==sprite);
                entries.Add(new VisibleBodyGeometry.Entry{sprite=sprite,bounds=Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y)),outline=hull});
            }
            data.entries=entries.ToArray();EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
        }
    }
}
