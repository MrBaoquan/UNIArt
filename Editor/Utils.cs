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
            else
            {
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

            Func<string, bool> assetDependencyCondition = isDropOnTemplate
                ? _path => !_path.StartsWith(assetRoot) // 拖拽为模板资源 处理非目标模板库内的资源依赖
                : _path => UNIArtSettings.IsTemplateAsset(_path); // 拖拽为项目资源 处理目标模板库内中的资源依赖

            var _folders = assetPaths.Where(_ => AssetDatabase.IsValidFolder(_));
            assetPaths = assetPaths.Except(_folders).ToArray();
            var _folderAssets = _folders
                .SelectMany(_ => AssetDatabase.FindAssets("t:Object", new[] { _ }))
                .Select(_ => AssetDatabase.GUIDToAssetPath(_));

            assetPaths = assetPaths.Concat(_folderAssets).ToArray();

            assetPaths
                .ToList()
                .ForEach(assetPath =>
                {
                    var _dependencies = AssetDatabase
                        .GetDependencies(assetPath)
                        .Where(_ => _.StartsWith(UNIArtSettings.Project.ArtFolder)) // 只处理UNIArt相关资源
                        .Where(assetDependencyCondition)
                        .Where(_ => _ != assetPath);

                    foreach (var _dependencyPath in _dependencies)
                    {
                        var _templateFolder = ".*";
                        if (UNIArtSettings.IsTemplateAsset(_dependencyPath))
                        {
                            var _templateID = UNIArtSettings.GetTemplateNameBySubAsset(
                                _dependencyPath
                            );
                            _templateFolder = @$"#Templates/{_templateID}";
                        }
                        var _folder = GetFolderByAssetType(_dependencyPath);
                        var _regex = $@"^Assets/(ArtAssets/)?.*?({_templateFolder}/)?({_folder}/)?";
                        var _srcPath = Regex.Replace(_dependencyPath, _regex, string.Empty);
                        var _dstPath = $"{assetRoot}/{_folder}/{_srcPath}";

                        // Debug.LogWarning($"origin: {_srcPath}, new: {_dstPath}");

                        CreateFolderIfNotExist(Path.GetDirectoryName(_dstPath));
                        _dstPath = AssetDatabase.GenerateUniqueAssetPath(_dstPath);
                        AssetDatabase.MoveAsset(_dependencyPath, _dstPath);
                    }

                    var _oldPath = assetPath;
                    var _newPath = $"{dropUponPath}/{Path.GetFileName(_oldPath)}";
                    _newPath = AssetDatabase.GenerateUniqueAssetPath(_newPath);

                    // 移动预览图依赖
                    if (isDropOnTemplate && UNIArtSettings.IsTemplateAsset(assetPath)) // 释放目标和源目标都是模板资源
                    {
                        var _previewPath = UNIArtSettings.GetPreviewPathByAsset(assetPath);
                        var _destPreviewPath = UNIArtSettings.GetPreviewPathByAsset(_newPath);
                        if (File.Exists(_previewPath))
                        {
                            CreateFolderIfNotExist(Path.GetDirectoryName(_destPreviewPath));
                            _destPreviewPath = AssetDatabase.GenerateUniqueAssetPath(
                                _destPreviewPath
                            );
                            AssetDatabase.MoveAsset(_previewPath, _destPreviewPath);
                            AssetDatabase.Refresh();
                        }
                    }

                    if (includeSelf)
                    {
                        AssetDatabase.MoveAsset(_oldPath, _newPath);
                        AssetDatabase.Refresh();
                    }
                });
        }
    }
}
