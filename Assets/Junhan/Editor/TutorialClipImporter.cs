using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    /// <summary>Preserve the exact 4x3 animation atlas; Unity's default NPOT scaling bends frames.</summary>
    public sealed class TutorialClipImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/TutorialClips/Sheets/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
        }
    }
}
