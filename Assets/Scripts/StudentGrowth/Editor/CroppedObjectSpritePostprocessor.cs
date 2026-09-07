using UnityEditor;
using UnityEngine;

namespace StudentGrowth.Editor
{
    public sealed class CroppedObjectSpritePostprocessor : AssetPostprocessor
    {
        private const string CroppedObjectsPath = "Assets/Art/CroppedObjects/";
        private const float PixelsPerUnit = 48f;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(CroppedObjectsPath, System.StringComparison.Ordinal))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
