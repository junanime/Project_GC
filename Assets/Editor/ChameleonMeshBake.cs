using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public static class ChameleonMeshBake
    {
        public static void RunSmoke(){Bake();ChameleonSmoke.Run();}
        [MenuItem("24투/Bake chameleon body meshes")]
        public static void Bake()
        {
            var meshes=new List<ChameleonArt.BodyMesh>();
            foreach(var kind in new[]{"Drift","Foam","Fanta","Latte"})
            {
                string path="Assets/Resources/Chameleons/"+kind+".png";
                var raw=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    if(!raw.LoadImage(File.ReadAllBytes(path)))throw new InvalidOperationException("Cannot read "+path);
                    var pixels=raw.GetPixels32();
                    var frames=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
                    if(frames.Length!=16)throw new InvalidOperationException("Expected 16 chameleon poses: "+kind);
                    foreach(var source in frames)
                    {
                        var sprite=Sprite.Create(raw,source.rect,new Vector2(.5f,0),source.rect.width/ChameleonArt.Width);
                        try
                        {
                            sprite.name=source.name;var mesh=ChameleonArt.BakeBodyMesh(sprite,pixels);
                            if(mesh.vertices.Length<6||mesh.vertices.Length>ushort.MaxValue||mesh.triangles.Any(n=>n>=mesh.vertices.Length))
                                throw new InvalidOperationException("Invalid body mesh: "+source.name);
                            for(int y=0;y<mesh.height;y++)for(int x=0;x<mesh.width;x++)
                                if(mesh.Contains(x,y)!=(pixels[((int)source.rect.y+y)*raw.width+(int)source.rect.x+x].a>51))
                                    throw new InvalidOperationException("Hit mask differs from source: "+source.name);
                            if(mesh.Contains(-1,0)||mesh.Contains(mesh.width,0)||mesh.Contains(0,mesh.height))
                                throw new InvalidOperationException("Hit mask accepts outside pixels: "+source.name);
                            meshes.Add(mesh);
                        }
                        finally{UnityEngine.Object.DestroyImmediate(sprite);}
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(raw);}
            }
            const string output="Assets/Resources/Chameleons/BodyMeshes.json";
            var json=JsonUtility.ToJson(new ChameleonArt.BodyMeshBank{entries=meshes.ToArray()});
            var decoded=JsonUtility.FromJson<ChameleonArt.BodyMeshBank>(json);
            if(decoded.entries.Length!=64||decoded.entries.Where((m,i)=>!m.vertices.SequenceEqual(meshes[i].vertices)||!m.triangles.SequenceEqual(meshes[i].triangles)||m.hitMask!=meshes[i].hitMask||m.width!=meshes[i].width||m.height!=meshes[i].height).Any())
                throw new InvalidOperationException("Body mesh serialization changed geometry.");
            if(!File.Exists(output)||File.ReadAllText(output)!=json){File.WriteAllText(output,json);AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceSynchronousImport);}
            Debug.Log("CHAMELEON_MESH_BAKE_PASS 64 original contours and pixel-exact hit masks, exact serialization, no runtime pixel readback.");
        }
    }
}
