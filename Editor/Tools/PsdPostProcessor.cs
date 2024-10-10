using UnityEngine;
using UnityEditor;
using System.IO;

namespace UNIArt.Editor
{
    public class PsdPostProcessor : AssetPostprocessor
    {
        void OnPostprocessTexture(Texture2D texture)
        {
            if (!assetPath.EndsWith(".psd", System.StringComparison.OrdinalIgnoreCase))
                return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            Utils.HookUpdateOnce(() => ImportPSDEntity(texture));
        }

        // 在导入纹理后调用
        void ImportPSDEntity(Texture2D texture)
        {
            if (!assetPath.StartsWith(UNIArtSettings.Project.ArtFolder))
                return;
            if (UNIArtSettings.PsdEntityExists(assetPath))
            {
                return;
            }

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
        }
    }
}
