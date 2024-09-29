using UnityEngine;
using UnityEditor;

namespace UNIArt.Editor
{
    public class PsdPostProcessor : AssetPostprocessor
    {
        // 在导入纹理后调用
        void OnPostprocessTexture(Texture2D texture)
        {
            // 检查文件扩展名
            if (assetPath.EndsWith(".psd", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!assetPath.StartsWith(UNIArtSettings.Project.ArtFolder))
                    return;
                if (UNIArtSettings.PsdEntityExists(assetPath))
                    return;

                Utils.HookUpdateOnce(() =>
                {
                    PSUtils.Dispose(assetPath);
                    PSUtils.CreatePSDGameObject(
                        assetPath,
                        _gameObj =>
                        {
                            TmplBrowser.RefreshContentView();
                        }
                    );
                });

                // 可以在这里修改导入设置，例如：
                TextureImporter importer = (TextureImporter)assetImporter;
                importer.textureType = TextureImporterType.Default;
                // importer.spriteImportMode = SpriteImportMode.Single; // 设置为单一 Sprite
                // importer.alphaIsTransparency = true; // 支持透明度

                // 其他设置...
                // importer.mipmapEnabled = false; // 禁用 Mipmap
                // importer.filterMode = FilterMode.Bilinear; // 设置过滤模式
            }
        }
    }
}
