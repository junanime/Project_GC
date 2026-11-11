using UnityEditor;
using UnityEngine;
namespace Vampire.Editor {
 public sealed class OralArtImporter:AssetPostprocessor {
  void OnPreprocessTexture(){if(!assetPath.StartsWith("Assets/Resources/OralMap/")||!assetPath.EndsWith(".png"))return;
   var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;t.alphaIsTransparency=true;
   bool pixel=assetPath.EndsWith("Monsters.png")||assetPath.Contains("/CeilingBats/")||assetPath.Contains("/LemonWhole")||assetPath.Contains("/LemonSpring");
   t.mipmapEnabled=!pixel;t.filterMode=pixel?FilterMode.Point:FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;
   t.npotScale=TextureImporterNPOTScale.None;t.maxTextureSize=2048;t.textureCompression=TextureImporterCompression.Uncompressed;
   t.isReadable=assetPath.EndsWith("Monsters.png")||assetPath.EndsWith("Teeth.png")||assetPath.Contains("/Event")||assetPath.Contains("/CeilingBats/");
  }
 }
}
