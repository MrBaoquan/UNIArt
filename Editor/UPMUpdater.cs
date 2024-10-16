using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace UNIArt.Editor
{
    public static class UPMUpdater
    {
        static UPMUpdater() { }

        public static void LatestVersion(string packageName, Action<string> callback)
        {
            var searchRequest = Client.Search(packageName);
            UpdateWhile(
                () => { },
                () => !searchRequest.IsCompleted,
                () =>
                {
                    if (
                        searchRequest.Status == StatusCode.Failure
                        || searchRequest.Result.Length <= 0
                    )
                    {
                        // Debug.LogError($"Failed to search for packages: {searchRequest.Error.message}");
                        callback?.Invoke(string.Empty);
                        return;
                    }

                    var latestVersion = searchRequest.Result[0].versions.latestCompatible;
                    callback?.Invoke(latestVersion);
                }
            );
        }

        public static void CurrentVersion(string packageName, Action<string> callback)
        {
            var listRequest = Client.List(true); // 列出所有已安装的包，包括预览包
            UpdateWhile(
                () => { },
                () => !listRequest.IsCompleted,
                () =>
                {
                    if (listRequest.Status != StatusCode.Success)
                    {
                        callback?.Invoke(string.Empty);
                        return;
                    }
                    var _uniartPkg = listRequest.Result.FirstOrDefault(p => p.name == packageName);
                    if (_uniartPkg == null)
                    {
                        callback?.Invoke(string.Empty);
                        return;
                    }

                    callback.Invoke(_uniartPkg.version);
                }
            );
        }

        public static void IsPackageLatest(string packageName, Action<bool> callback)
        {
            CurrentVersion(
                packageName,
                version =>
                {
                    // Debug.LogWarning($"{packageName} version: {version}");
                    if (string.IsNullOrEmpty(version))
                    {
                        callback?.Invoke(true);
                        return;
                    }
                    LatestVersion(
                        packageName,
                        latestVersion =>
                        {
                            // Debug.LogWarning($"{packageName} latest version: {latestVersion}");
                            if (string.IsNullOrEmpty(latestVersion))
                            {
                                callback?.Invoke(true);
                                return;
                            }
                            callback?.Invoke(latestVersion == version);
                        }
                    );
                }
            );
        }

        public static void UpdatePackage(string packageName, string latestVersion)
        {
            var addRequest = Client.Add($"{packageName}@{latestVersion}");
            UpdateWhile(
                () => { },
                () => !addRequest.IsCompleted,
                () =>
                {
                    if (addRequest.Status == StatusCode.Success)
                    {
                        Debug.Log($"Successfully updated {packageName} to version {latestVersion}");
                    }
                    else if (addRequest.Status >= StatusCode.Failure)
                    {
                        Debug.LogError(
                            $"Failed to update {packageName}: {addRequest.Error.message}"
                        );
                    }
                }
            );
        }

        public static void UpdatePackage(string packageName)
        {
            LatestVersion(
                packageName,
                latestVersion =>
                {
                    if (string.IsNullOrEmpty(latestVersion))
                    {
                        Debug.LogWarning($"No new version of {packageName} found.");
                        return;
                    }
                    UpdatePackage(packageName, latestVersion);
                }
            );
        }

        private static IDisposable UpdateWhile(
            Action update,
            Func<bool> condition,
            Action onComplete = null,
            Action onCancel = null
        )
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            EditorApplication.CallbackFunction _tempCallback = null;
            _tempCallback = () =>
            {
                if (cts.IsCancellationRequested)
                {
                    onCancel?.Invoke();
                    EditorApplication.update -= _tempCallback;
                    return;
                }

                if (condition())
                {
                    update();
                }
                else
                {
                    EditorApplication.update -= _tempCallback;
                    onComplete?.Invoke();
                }
            };
            EditorApplication.update += _tempCallback;
            return cts;
        }
    }
}
