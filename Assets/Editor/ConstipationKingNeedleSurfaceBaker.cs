using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    public static class ConstipationKingNeedleSurfaceBaker
    {
        const string Root = "Assets/Resources/ConstipationKing/";

        [MenuItem("Tools/24 Hours/Bake Constipation King Needle Surfaces")]
        public static void Bake()
        {
            using (var writer = new BinaryWriter(File.Create(Root + "NeedleSurfaces.bytes")))
            {
                writer.Write(12);
                for (int i = -1; i <= 10; i++)
                {
                    string name = i < 0 ? "Body" : "Pose" + i;
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        if (!texture.LoadImage(File.ReadAllBytes(Root + name + ".png")))
                            throw new InvalidDataException("Cannot read approved king sprite: " + name);
                        var pixels = texture.GetPixels32();
                        var bits = new byte[(pixels.Length + 7) / 8];
                        for (int pixel = 0; pixel < pixels.Length; pixel++)
                            if (pixels[pixel].a >= 128) bits[pixel >> 3] |= (byte)(1 << (pixel & 7));
                        writer.Write(name); writer.Write(texture.width); writer.Write(texture.height);
                        writer.Write(bits.Length); writer.Write(bits);
                    }
                    finally { Object.DestroyImmediate(texture); }
                }
            }
            AssetDatabase.ImportAsset(Root + "NeedleSurfaces.bytes", ImportAssetOptions.ForceSynchronousImport);
            ConstipationKingNeedleSurface.ClearCache();
            Debug.Log("KING_NEEDLE_SURFACES_BAKED 12 approved source alpha masks");
        }
    }
}
