using System;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace UNIArt.Editor
{
    public static class UPMUpdater
    {
        static UPMUpdater() { }

        public static void CheckPackageInstalled(string packageName, Action<bool> callback)
        {
            var listRequest = Client.List(true); // 列出所有已安装的包，包括预览包
            UpdateWhile(
                () => { },
                () => !listRequest.IsCompleted,
                () =>
                {
                    if (listRequest.Status != StatusCode.Success)
                    {
                        Debug.LogError($"Failed to list packages: {listRequest.Error.message}");
                        callback?.Invoke(false);
                        return;
                    }
                    var _hasPkg = listRequest.Result.Any(p => p.name == packageName);
                    callback?.Invoke(_hasPkg);
                }
            );
        }

        public static void InstallPackage(string packageName, Action<bool> callback = null)
        {
            var addRequest = Client.Add(packageName);
            UpdateWhile(
                () => { },
                () => !addRequest.IsCompleted,
                () =>
                {
                    if (addRequest.Status == StatusCode.Success)
                    {
                        Debug.Log($"Successfully installed package: {packageName}");
                        callback?.Invoke(true);
                        return;
                    }

                    Debug.LogError($"Failed to install package: {addRequest.Error}");
                    callback?.Invoke(false);
                }
            );
        }

        public static void RemovePackage(string packageName, Action<bool> callback = null)
        {
            var request = UnityEditor.PackageManager.Client.Remove(packageName);
            UpdateWhile(
                () => { },
                () => !request.IsCompleted,
                () =>
                {
                    if (request.Status == StatusCode.Success)
                    {
                        Debug.Log($"Successfully removed package: {request.PackageIdOrName}");
                        callback?.Invoke(true);
                        return;
                    }

                    Debug.LogWarning($"Failed to remove package: {request.Error}");
                    callback?.Invoke(false);
                }
            );
        }

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
                        Debug.LogError(
                            $"Failed to search packages: {searchRequest.Error?.message}"
                        );
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
                        Debug.LogError($"Failed to list packages: {listRequest.Error.message}");
                        callback?.Invoke(string.Empty);
                        return;
                    }
                    var _uniartPkg = listRequest.Result.FirstOrDefault(p => p.name == packageName);
                    if (_uniartPkg == null)
                    {
                        Debug.LogWarning($"{packageName} is not installed.");
                        callback?.Invoke(string.Empty);
                        return;
                    }

                    callback.Invoke(_uniartPkg.version);
                }
            );
        }

        public static void IsPackageLatest(
            string packageName,
            Action<bool, string, string> callback
        )
        {
            CurrentVersion(
                packageName,
                version =>
                {
                    // Debug.LogWarning($"{packageName} version: {version}");
                    if (string.IsNullOrEmpty(version))
                    {
                        callback?.Invoke(true, string.Empty, string.Empty);
                        return;
                    }
                    LatestVersion(
                        packageName,
                        latestVersion =>
                        {
                            // Debug.LogWarning($"{packageName} latest version: {latestVersion}");
                            if (string.IsNullOrEmpty(latestVersion))
                            {
                                callback?.Invoke(true, string.Empty, string.Empty);
                                return;
                            }
                            callback?.Invoke(latestVersion == version, version, latestVersion);
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
            IsPackageLatest(
                packageName,
                (isLatest, currentVersion, latestVersion) =>
                {
                    if (isLatest)
                    {
                        Debug.Log(
                            $"The latest version of {packageName} {currentVersion} is installed."
                        );
                    }
                    else
                    {
                        UpdatePackage(packageName, latestVersion);
                    }
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
