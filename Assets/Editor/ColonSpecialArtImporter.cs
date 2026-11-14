using UnityEditor;
using UnityEngine;
namespace Vampire.Editor {
 public sealed class ColonSpecialArtImporter:AssetPostprocessor {
  void OnPreprocessTexture(){if(!assetPath.StartsWith("Assets/Resources/ColonSpecials/"))return;var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.spritePixelsPerUnit=128;t.filterMode=FilterMode.Point;t.mipmapEnabled=false;t.alphaIsTransparency=true;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=512;var s=new TextureImporterSettings();t.ReadTextureSettings(s);s.spriteAlignment=9;s.spritePivot=new Vector2(.5f,0);t.SetTextureSettings(s);}
 }
}
