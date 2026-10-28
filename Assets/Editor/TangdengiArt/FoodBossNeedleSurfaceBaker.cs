using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    public static class FoodBossNeedleSurfaceBaker
    {
        [MenuItem("24tu/Art/Bake boss needle surfaces")]
        public static void Bake()
        {
            const string folder="Assets/Resources/FoodBosses/";
            var paths=Directory.GetFiles(folder,"*.png").Where(p=>Path.GetFileName(p).StartsWith("Walk")||Path.GetFileName(p).StartsWith("CrabBody")).OrderBy(p=>p).ToArray();
            using(var writer=new BinaryWriter(File.Create(folder+"NeedleSurfaces.bytes"))){
                writer.Write(paths.Length);
                foreach(var path in paths){
                    var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(File.ReadAllBytes(path));
                    var pixels=texture.GetPixels32();var bits=new byte[(pixels.Length+7)/8];
                    for(int i=0;i<pixels.Length;i++)if(pixels[i].a>=128)bits[i>>3]|=(byte)(1<<(i&7));
                    writer.Write(Path.GetFileNameWithoutExtension(path));writer.Write(texture.width);writer.Write(texture.height);writer.Write(bits.Length);writer.Write(bits);Object.DestroyImmediate(texture);
                }
            }
            AssetDatabase.Refresh();Debug.Log("BOSS_NEEDLE_SURFACES_BAKED "+paths.Length);
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
    }
}
