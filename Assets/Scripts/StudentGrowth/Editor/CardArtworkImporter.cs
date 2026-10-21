using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StudentGrowth.EditorTools
{
    /// <summary>
    /// 把新画好的卡图按像素画规格导入：Sprite 类型、Point 过滤、不压缩、无 mipmap。
    /// 只处理本工程卡图目录下的 PNG。
    /// </summary>
    public static class CardArtworkImporter
    {
        private const string ArtFolder = "Assets/Art/CardReferences/FanworkPixel/Generated/PNG";

        [MenuItem("Tools/Student Growth/Art/Fix Card Artwork Import Settings")]
        public static void FixImportSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder });
            List<Object> changed = new List<Object>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }

                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }

                if (importer.filterMode != FilterMode.Point)
                {
                    importer.filterMode = FilterMode.Point;
                    dirty = true;
                }

                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    dirty = true;
                }

                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    dirty = true;
                }

                if (importer.alphaIsTransparency != true)
                {
                    importer.alphaIsTransparency = true;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed.Add(AssetDatabase.LoadAssetAtPath<Object>(path));
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("[CardArt] 导入设置已修正：" + changed.Count + " 张（路径 " + ArtFolder + "）");
        }
    }
}
