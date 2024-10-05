using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace UNIArt.Editor
{
    public static class UniArtPackageAutoUpdater
    {
        private const string packageName = "com.parful.uniart";
        private static ListRequest listRequest;
        private static SearchRequest searchRequest;
        private static AddRequest addRequest;
        private static string currentVersion;
        private static string latestVersion;

        static UniArtPackageAutoUpdater()
        {
            if (Application.isPlaying)
                return;
            // Unity启动时自动检测包版本
            // CheckPackageVersion();
        }

        // [MenuItem("Tools/Update UNIArt")]
        public static void CheckPackageVersion()
        {
            listRequest = Client.List(true); // 列出所有已安装的包，包括预览包
            EditorApplication.update += CheckPackageList;
        }

        private static void CheckPackageList()
        {
            if (listRequest.IsCompleted)
            {
                if (listRequest.Status == StatusCode.Success)
                {
                    var _uniartPkg = listRequest.Result.FirstOrDefault(p => p.name == packageName);
                    if (_uniartPkg == null)
                        return;
                    currentVersion = _uniartPkg.version;
                    CheckForNewVersion();
                }
                else if (listRequest.Status >= StatusCode.Failure)
                {
                    // Debug.LogError($"Failed to list packages: {listRequest.Error.message}");
                }

                EditorApplication.update -= CheckPackageList;
            }
        }

        private static void CheckForNewVersion()
        {
            searchRequest = Client.Search(packageName);
            EditorApplication.update += CheckSearchResult;
        }

        private static void CheckSearchResult()
        {
            if (searchRequest.IsCompleted)
            {
                EditorApplication.update -= CheckSearchResult;

                if (searchRequest.Status == StatusCode.Failure)
                {
                    // Debug.LogError($"Failed to search for packages: {searchRequest.Error.message}");
                    return;
                }

                if (searchRequest.Result.Length <= 0)
                {
                    // Debug.LogWarning($"No package found with name {packageName}");
                    return;
                }

                latestVersion = searchRequest.Result[0].versions.latestCompatible;

                if (latestVersion != currentVersion)
                {
                    Debug.Log(
                        $"A newer version ({latestVersion}) of {packageName} is available. Upgrading..."
                    );
                    UpdatePackage();
                }
                else
                {
                    Debug.Log(
                        $"The latest version of {packageName} {currentVersion} is installed."
                    );
                }
            }
        }

        private static void UpdatePackage()
        {
            addRequest = Client.Add($"{packageName}@{latestVersion}");
            EditorApplication.update += PackageAddProgress;
        }

        private static void PackageAddProgress()
        {
            if (addRequest.IsCompleted)
            {
                EditorApplication.update -= PackageAddProgress;

                if (addRequest.Status == StatusCode.Success)
                {
                    Debug.Log($"Successfully updated {packageName} to version {latestVersion}");
                }
                else if (addRequest.Status >= StatusCode.Failure)
                {
                    Debug.LogError($"Failed to update {packageName}: {addRequest.Error.message}");
                }
            }
        }
    }
}
