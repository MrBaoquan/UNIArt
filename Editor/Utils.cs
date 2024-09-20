using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UNIArt.Editor
{
    public static class Utils
    {
        [MenuItem("Tools/Test")]
        private static void test()
        {
            AssetDatabase
                .FindAssets("t:AnimationClip", new[] { "Assets/ArtAssets/Animations" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ForEach(_ =>
                {
                    Debug.LogWarning(
                        AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_).GetType()
                    );
                    Debug.Log(
                        AssetDatabase.IsMainAsset(
                            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_)
                        )
                    );
                });
        }

        const string packageName = "com.parful.uniart";

        public static string ToForwardSlash(this string path)
        {
            return path.Replace("\\", "/");
        }

        public static string PackageAssetPath(string assetPath)
        {
            return $"Packages/{packageName}/{assetPath}";
        }

        public static void RemoveHierarchyWindowItemOnGUI(List<string> methods)
        {
            FieldInfo eventField = typeof(EditorApplication).GetField(
                "hierarchyWindowItemOnGUI",
                BindingFlags.Static | BindingFlags.Public
            );

            if (eventField == null)
                return;

            Delegate eventDelegate = (Delegate)eventField.GetValue(null);
            if (eventDelegate == null)
                return;

            Delegate[] subscribers = eventDelegate.GetInvocationList();

            subscribers
                .Where(_ => methods.Contains(_.Method.Name))
                .ToList()
                .ForEach(_ =>
                {
                    EditorApplication.hierarchyWindowItemOnGUI -=
                        (EditorApplication.HierarchyWindowItemCallback)_;
                });
        }

        public static void Delay(Action callback, float duration)
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(
                DelayCoroutine(() => callback?.Invoke(), duration)
            );
        }

        private static IEnumerator DelayCoroutine(Action callback, float duration)
        {
            yield return new EditorWaitForSeconds(duration);
            callback();
        }

        public static bool IsPrefabStage()
        {
            return PrefabStageUtility.GetCurrentPrefabStage() != null;
        }

        public static string PrefabStageAssetPath()
        {
            var _prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (_prefabStage == null)
                return string.Empty;

            return _prefabStage.assetPath;
        }

        public static string GetPrefabAssetPathByAnyGameObject(GameObject selected)
        {
            if (selected == null)
                return string.Empty;

            if (PrefabUtility.IsPartOfPrefabAsset(selected))
            {
                return AssetDatabase.GetAssetPath(selected);
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
                return string.Empty;

            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(selected);
            if (prefabRoot != null)
            {
                return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot);
            }

            if (selected.transform.IsChildOf(prefabStage.prefabContentsRoot.transform))
            {
                return prefabStage.assetPath;
            }

            return string.Empty;
        }

        public static (float width, float height) CalculateScaledImageSize(
            float windowWidth,
            float windowHeight,
            float imageWidth,
            float imageHeight
        )
        {
            // 如果图片尺寸小于窗口尺寸，返回原始图片尺寸
            if (imageWidth <= windowWidth && imageHeight <= windowHeight)
            {
                return (imageWidth, imageHeight);
            }

            // 计算图片和窗口的宽高比
            float imageAspectRatio = imageWidth / imageHeight;
            float windowAspectRatio = windowWidth / windowHeight;

            // 目标尺寸
            float targetWidth,
                targetHeight;

            // 判断根据宽度或高度进行缩放
            if (imageAspectRatio > windowAspectRatio)
            {
                // 以窗口宽度为基准进行缩放
                targetWidth = windowWidth;
                targetHeight = windowWidth / imageAspectRatio;
            }
            else
            {
                // 以窗口高度为基准进行缩放
                targetHeight = windowHeight;
                targetWidth = windowHeight * imageAspectRatio;
            }

            // 返回缩放后的宽高
            return (targetWidth, targetHeight);
        }

        public static string GetAnimationFolderByPath(string path)
        {
            if (UNIArtSettings.IsTemplateAsset(path))
            {
                return UNIArtSettings.GetExternalTemplateRootBySubAsset(path) + "/Animations";
            }
            return "Assets/ArtAssets/Animations";
        }

        public static string GetFolderByAssetType(string assetPath)
        {
            // 获取资源类型
            System.Type assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);

            if (assetType == null)
            {
                Debug.LogWarning("No asset found at the specified path.");
                return null;
            }

            // 根据资源类型返回对应的文件夹
            if (assetType == typeof(Texture2D) || assetType == typeof(Sprite))
            {
                return "Textures";
            }
            else if (assetType == typeof(Material))
            {
                return "Materials";
            }
            else if (assetType == typeof(AudioClip))
            {
                return "Audio";
            }
            else if (assetType == typeof(AnimationClip))
            {
                return "Animations";
            }
            else if (assetType == typeof(AnimatorController))
            {
                return "Animations";
            }
            else if (assetType == typeof(Mesh))
            {
                return "Models";
            }
            else if (assetType == typeof(GameObject))
            {
                return "Prefabs";
            }
            else if (assetType == typeof(Shader))
            {
                return "Shaders";
            }
            else if (assetType == typeof(ScriptableObject))
            {
                return "ScriptableObjects";
            }
            else if (
                assetType == typeof(UnityEngine.Font) || assetType == typeof(TMPro.TMP_FontAsset)
            )
            {
                return "Fonts";
            }
            else
            {
                // Debug.LogWarning(assetType);
                // 返回默认文件夹
                return "Misc";
            }
        }

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        public static bool CreateFolderIfNotExist(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Directory.CreateDirectory($"{ProjectRoot}/{folderPath}");
                AssetDatabase.Refresh();
                return true;
            }
            return true;
        }

        public static bool DeleteProjectAsset(string path)
        {
            return AssetDatabase.DeleteAsset(path);
        }

        public static void ForceRecompile()
        {
            AssetDatabase.ImportAsset(
                PackageAssetPath("Editor/UNIArt.Editor.asmdef"),
                ImportAssetOptions.ForceUpdate
            );
        }

        // 获取指定路径的资源被哪些资源依赖
        public static List<string> GetReverseDependencies(
            string[] searchInFolders,
            string assetPath
        )
        {
            return AssetDatabase
                .FindAssets("t:Object", searchInFolders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(_ => _ != assetPath)
                .Where(_ => AssetDatabase.GetDependencies(_, false).Contains(assetPath))
                .ToList();
        }

        // 在当前模板内是否被其他预制体依赖
        public static bool CheckIfHasReverseDependencies(
            string assetPath,
            List<string> excludeFiles
        )
        {
            var _dependencyTemplateRoot = UNIArtSettings.GetExternalTemplateRootBySubAsset(
                assetPath
            );
            var _reverseDependencies = GetReverseDependencies(
                    new string[] { _dependencyTemplateRoot },
                    assetPath
                )
                .Where(_ => !excludeFiles.Contains(_));
            return _reverseDependencies.Count() > 0;
        }

        // 移动资源到模板资源文件夹, 同步移动依赖
        public static void MoveAssetsWithDependencies(
            string[] assetPaths,
            string dropUponPath,
            bool includeSelf = false
        )
        {
            if (!dropUponPath.StartsWith(UNIArtSettings.Project.ArtFolder))
            {
                return;
            }

            bool isDropOnTemplate = UNIArtSettings.IsTemplateAsset(dropUponPath);
            var assetRoot = isDropOnTemplate
                ? UNIArtSettings.GetExternalTemplateRootBySubAsset(dropUponPath)
                : UNIArtSettings.Project.ArtFolder;

            var _folders = assetPaths.Where(_ => AssetDatabase.IsValidFolder(_)).ToList();

            if (_folders.Count > 0)
            {
                var _folderAssets = AssetDatabase
                    .FindAssets("t:Object", _folders.ToArray())
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .ToList();
                assetPaths = assetPaths.Concat(_folderAssets).Distinct().ToArray();
            }

            Func<string, bool> filterDependencyCondition = isDropOnTemplate
                //? _path => true // 拖拽到模板库 处理所有依赖
                ? _path => !_path.StartsWith(assetRoot) // 拖拽到模板库 仅处理非当前模板库内的资源依赖
                : _path => UNIArtSettings.IsTemplateAsset(_path); // 拖拽到项目库 仅处理模板库内中的资源依赖

            Func<string, bool> excludeCondition = _path =>
            {
                return !UNIArtSettings.Project.dependencyExcludeFolders.Any(
                        _folder => _path.StartsWith(_folder)
                    ) && !UNIArtSettings.Project.dependencyExcludeFiles.Contains(_path);
            };

            assetPaths
                .Where(_ => !AssetDatabase.IsValidFolder(_))
                .ToList()
                .ForEach(assetPath =>
                {
                    // 依赖资源的处理
                    var _dependencies = AssetDatabase
                        .GetDependencies(assetPath, false)
                        .Where(_ => !_.StartsWith("Packages/")) // 排除包内资源
                        .Where(excludeCondition)
                        .Where(_ => _ != assetPath)
                        .Where(filterDependencyCondition);

                    // Debug.LogError(assetPath);
                    // Debug.Log($"dependencies: {_dependencies.Count()}");

                    foreach (var _dependencyPath in _dependencies)
                    {
                        var _templateFolder = ".*";
                        if (UNIArtSettings.IsTemplateAsset(_dependencyPath))
                        {
                            var _templateID = UNIArtSettings.GetTemplateNameBySubAsset(
                                _dependencyPath
                            );

                            if (
                                CheckIfHasReverseDependencies(
                                    _dependencyPath,
                                    new List<string> { assetPath }
                                )
                            )
                            {
                                Debug.LogWarning(
                                    $"{_dependencyPath} has more than one reference in template library, skip."
                                );
                                continue;
                            }
                            _templateFolder = @$"#Templates/{_templateID}";
                        }
                        var _folder = GetFolderByAssetType(_dependencyPath);
                        var _regex = $@"^Assets/(ArtAssets/)?.*?({_templateFolder}/)?({_folder}/)?";
                        var _srcPath = Regex.Replace(_dependencyPath, _regex, string.Empty);
                        var _dstPath = $"{assetRoot}/{_folder}/{_srcPath}";
                        if (_srcPath == _dstPath) // 源路径和目标路径一致则忽略
                        {
                            continue;
                        }

                        CreateFolderIfNotExist(Path.GetDirectoryName(_dstPath));
                        _dstPath = AssetDatabase.GenerateUniqueAssetPath(_dstPath);
                        AssetDatabase.MoveAsset(_dependencyPath, _dstPath);
                        AssetDatabase.Refresh();
                    }

                    // 资源本身的处理
                    var _oldPath = assetPath;
                    var _newPath = $"{dropUponPath}/{Path.GetFileName(_oldPath)}";

                    var _depFolder = _folders.Where(_ => assetPath.StartsWith(_)).Min();
                    if (!string.IsNullOrEmpty(_depFolder))
                    {
                        _depFolder = Path.GetDirectoryName(_depFolder).ToForwardSlash() + "/";
                        _newPath = $"{dropUponPath}/{_oldPath.Replace(_depFolder, string.Empty)}";
                        CreateFolderIfNotExist(Path.GetDirectoryName(_newPath));
                    }

                    _newPath = AssetDatabase.GenerateUniqueAssetPath(_newPath);
                    // Debug.Log($"Move {_oldPath} to {_newPath}");

                    if (includeSelf)
                    {
                        AssetDatabase.MoveAsset(_oldPath, _newPath);
                        AssetDatabase.Refresh();
                    }

                    // 移动预览图依赖
                    if (isDropOnTemplate && UNIArtSettings.IsTemplateAsset(assetPath)) // 释放目标和源目标都是模板资源
                    {
                        var _previewPath = UNIArtSettings.GetPreviewPathByAsset(assetPath);
                        var _dstPreviewPath = UNIArtSettings.GetPreviewPathByAsset(_newPath);
                        if (File.Exists(_previewPath))
                        {
                            CreateFolderIfNotExist(Path.GetDirectoryName(_dstPreviewPath));
                            Debug.Log(_dstPreviewPath);
                            _dstPreviewPath = AssetDatabase.GenerateUniqueAssetPath(
                                _dstPreviewPath
                            );
                            AssetDatabase.MoveAsset(_previewPath, _dstPreviewPath);
                            AssetDatabase.Refresh();
                        }
                    }
                });
            AssetDatabase.Refresh();
        }
    }
}
